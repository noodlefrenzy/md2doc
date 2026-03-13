---
agent-notes:
  ctx: "Review of theme-aware Mermaid rendering (issue #89)"
  deps: [src/Md2.Diagrams/MermaidThemeConfig.cs, src/Md2.Diagrams/MermaidRenderer.cs, src/Md2.Diagrams/DiagramCache.cs, src/Md2.Diagrams/MermaidDiagramRenderer.cs]
  state: active
  last: "code-reviewer@2026-03-13"
---
# Code Review: Theme-Aware Mermaid Rendering (Issue #89, ADR-0013)

**Date:** 2026-03-13
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `src/Md2.Diagrams/MermaidThemeConfig.cs`
- `src/Md2.Diagrams/MermaidRenderer.cs`
- `src/Md2.Diagrams/DiagramCache.cs`
- `src/Md2.Diagrams/MermaidDiagramRenderer.cs`
- `src/Md2.Core/Transforms/TransformContext.cs`
- `src/Md2.Core/Pipeline/ConversionPipeline.cs`
- `src/Md2.Cli/ConvertCommand.cs`
- `tests/Md2.Diagrams.Tests/MermaidThemeConfigTests.cs`
- `tests/Md2.Diagrams.Tests/MermaidRendererTests.cs`
- `tests/Md2.Diagrams.Tests/DiagramCacheTests.cs`

**Verdict:** Changes requested (1 Critical, 4 Important)

## Context

Issue #89 makes Mermaid diagrams visually match the document's theme. Previously, all diagrams used Mermaid's `default` theme regardless of the document preset. This change introduces a `MermaidThemeConfig` DTO that maps `ResolvedTheme` properties to Mermaid `themeVariables`, reorders theme resolution before transforms in the CLI pipeline, and includes the theme in the diagram cache key.

The approach follows ADR-0013: always use Mermaid's `base` theme with mapped `themeVariables`, auto-derive contrast text colors for extreme palettes, and hash the theme into the cache key.

## Findings

### Critical

**JavaScript injection via FontFamily in BuildThemedInitScript**

In `MermaidRenderer.cs` line 184, `config.FontFamily` is interpolated directly into a JavaScript string literal:

```csharp
$"fontFamily: '{config.FontFamily}', "
```

The value originates from `ResolvedTheme.HeadingFont`, which comes from user-authored YAML theme files. A theme file containing `heading_font: "Calibri'; alert(1); //"` would break out of the JS string literal and inject arbitrary JavaScript.

While the blast radius is limited (headless Chromium rendering to PNG, not a user-facing browser), this is still a code injection vulnerability. It could cause confusing rendering failures at minimum, and potentially arbitrary code execution within the Chromium sandbox.

**Fix:** Escape single quotes and backslashes in all string values before interpolation, or use `System.Text.Json.JsonSerializer.Serialize()` to produce properly-escaped JSON string literals. The color hex values should also be validated (see Important finding below).

### Important

**No input validation on hex color strings**

Color values in `MermaidThemeConfig` are interpolated into JavaScript as `#{config.PrimaryColor}`. If a `ResolvedTheme` property contains non-hex characters (from a malformed theme YAML), this produces broken JavaScript. Additionally, `DeriveContrastTextColor` slices the string at fixed positions (`hexColor[..2]`, etc.) and would throw on strings shorter than 6 characters.

**Fix:** Validate hex strings in `FromResolvedTheme` with a regex like `^[0-9A-Fa-f]{6}$`. Reject or fall back to defaults for invalid values. This also eliminates the JS injection vector for color properties.

**FromResolvedTheme called inside the per-diagram loop**

In `MermaidDiagramRenderer.cs`, `MermaidThemeConfig.FromResolvedTheme(context.ResolvedTheme)` is called for every Mermaid code block in the document. The theme is constant per-document, so this is wasteful and obscures the invariant.

**Fix:** Compute `themeConfig` once before the `foreach` loop.

**No integration test for theme flow through MermaidDiagramRenderer**

The connection between `MermaidDiagramRenderer.Transform` extracting `context.ResolvedTheme` and passing it to `MermaidRenderer.RenderAsync` is untested. A unit test with a mock renderer verifying the theme config flows through would catch regressions at this seam.

**Cache key concatenation without separator**

In `DiagramCache.cs`, the themed cache path is `ComputeHash(source + (themeKey ?? ""))`. This means `source="AB"` + `themeKey="CD"` and `source="ABC"` + `themeKey="D"` hash identically. In practice the theme key is a fixed-length SHA256 hex string so collisions are impossible, but using a null byte separator (`source + "\0" + themeKey`) makes correctness obvious rather than dependent on knowing the key format.

### Suggestions

**Dead private BuildHtml overload.** The private `BuildHtml(string)` in `MermaidRenderer.cs` (line 141) delegates to the internal two-parameter overload and has no callers. Remove it.

**Missing cache key test for ClusterBackground.** Every other property has a `DiffersWhen...Changes` test except `ClusterBackground`.

**Font size rounding inconsistency.** The cache key uses `FontSizePx.ToString("F2")` (two decimal places) but the rendered JS uses `(int)Math.Round()`. Two themes at 14.3px and 14.7px produce different cache entries but identical rendering (both round to 15px). Not a bug, but a subtle waste.

**Consider making MermaidThemeConfig a record.** It is a pure DTO with init-only properties. A record gives structural equality and signals immutability.

## Lessons

1. **Always escape user input when building code strings.** String interpolation into JavaScript (or SQL, or HTML attributes) is injection-prone even when the execution context seems safe. The right reflex is to use a proper serializer (`JsonSerializer.Serialize` for JS values, parameterized queries for SQL, `HtmlEncode` for HTML). The Mermaid source was correctly `HtmlEncode`d for the HTML context, but the theme variables going into a `<script>` tag were not escaped for the JavaScript context. Different contexts need different escaping.

2. **Validate at the boundary, not at the point of use.** `DeriveContrastTextColor` assumes a 6-character hex string but does not check. `FromResolvedTheme` is the natural validation boundary -- it is where external data enters the DTO. Validate there once, and downstream code can trust the invariants.

3. **Hoist loop-invariant computation.** When something is constant across iterations, computing it inside the loop is not just a performance issue -- it misleads readers into thinking the value might change per iteration. Moving `MermaidThemeConfig.FromResolvedTheme()` above the loop makes the invariant explicit.

4. **Cache key construction should be obviously correct.** Concatenating strings without a separator works when you know one component is fixed-length, but it requires the reader to verify that invariant. A separator character (null byte, pipe, etc.) makes correctness self-evident. Defense in depth means not relying on properties of one component to ensure correctness of the composition.

5. **Test the integration seams, not just the units.** The DTO mapping and the HTML generation are both well-tested in isolation. But the point where `MermaidDiagramRenderer` connects them -- extracting the theme from context and passing it through -- has zero coverage. Seams between well-tested units are where bugs hide.
