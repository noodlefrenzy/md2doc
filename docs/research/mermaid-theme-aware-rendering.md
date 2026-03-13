---
agent-notes: { ctx: "Design doc for theme-aware Mermaid rendering", deps: [MermaidRenderer.cs, ResolvedTheme.cs, DiagramCache.cs, MermaidDiagramRenderer.cs], state: proposed, last: "archie@2026-03-13" }
---

# Design: Theme-Aware Mermaid Diagram Rendering

**Author:** Archie (architecture lead)
**Date:** 2026-03-13
**Status:** Proposed -- awaiting human confirmation before ADR/implementation

## Problem

Mermaid diagrams rendered by md2 always look identical regardless of the active theme preset. A document using a "corporate" theme with navy blue headings and Calibri fonts still produces Mermaid diagrams with Mermaid's default grey/purple color scheme and Trebuchet MS fonts. This breaks visual consistency between the document body and its diagrams.

## Current Implementation

### Rendering Flow

1. **ConvertCommand** (line ~194) registers `MermaidDiagramRenderer` as an AST transform.
2. **MermaidDiagramRenderer.Transform()** walks the AST, finds `FencedCodeBlock` nodes with `info == "mermaid"`, calls `MermaidRenderer.RenderAsync()` synchronously, and attaches the PNG path to the block.
3. **MermaidRenderer.RenderAsync()** checks the `DiagramCache` (keyed on SHA256 of mermaid source text), and if cache-miss:
   - Launches a Playwright page via `BrowserManager`
   - Calls `BuildHtml()` which produces a minimal HTML page
   - The HTML embeds the bundled `mermaid.min.js` and calls `mermaid.initialize({ startOnLoad: true, theme: 'default' })`
   - Waits for SVG to appear, screenshots it at 2x DPI, stores the PNG

### The Gap

The `mermaid.initialize()` call is hardcoded to `theme: 'default'`. No theme properties are passed in. Furthermore:

- **MermaidRenderer** has no access to `ResolvedTheme` -- it is constructed with only `BrowserManager`, `DiagramCache`, and a logger.
- **MermaidDiagramRenderer** (the AST transform) has no access to `ResolvedTheme` either -- `TransformContext` carries only `DocumentMetadata`, `TransformOptions`, and `CancellationToken`.
- **Theme resolution happens AFTER the transform phase** in ConvertCommand (theme resolves at line ~276, transforms run at line ~197). However, theme resolution depends only on CLI args/files, not on the parsed AST, so this ordering can be changed.
- **DiagramCache** keys on `SHA256(versionSalt + mermaidSource)`. Theme properties are not part of the cache key, so the same diagram source with different themes would incorrectly return a cached PNG rendered with the wrong theme.

## Mermaid JS Theming Capabilities

Mermaid supports theming via its `initialize()` configuration:

```javascript
mermaid.initialize({
  startOnLoad: true,
  theme: 'base',         // 'base' is the ONLY theme that supports full customization
  themeVariables: {
    // Core palette
    primaryColor:       '#1B3A5C',  // Node background fill
    primaryTextColor:   '#FFFFFF',  // Text inside primary nodes
    primaryBorderColor: '#0D1F33',  // Node border stroke
    secondaryColor:     '#4A90D9',  // Secondary element fills
    tertiaryColor:      '#F5F5F5',  // Tertiary fills (alt rows, etc.)
    lineColor:          '#333333',  // Connector/edge lines
    textColor:          '#333333',  // General label text

    // Typography
    fontFamily: 'Calibri, sans-serif',
    fontSize:   '14px',

    // Specialized
    mainBkg:            '#1B3A5C',  // Flowchart node background
    noteBkgColor:       '#F5F5F5',  // Note background
    noteBorderColor:    '#E0E0E0',  // Note border
  }
});
```

Key facts:
- The `base` theme is the only one designed for full customization via `themeVariables`.
- The built-in themes (`default`, `dark`, `forest`, `neutral`) derive colors algorithmically from primary/secondary/tertiary and ignore most individual overrides.
- `themeVariables` accepts hex colors with `#` prefix (ResolvedTheme stores them without `#`).
- `fontFamily` accepts standard CSS font-family strings.

## Proposed Approach

### Option A: Pass themeVariables through initialize() (Recommended)

Map `ResolvedTheme` properties to Mermaid `themeVariables` and inject them into the `mermaid.initialize()` call in `BuildHtml()`.

**ResolvedTheme to Mermaid mapping:**

| ResolvedTheme Property | Mermaid themeVariable | Rationale |
|------------------------|----------------------|-----------|
| `PrimaryColor` | `primaryColor`, `mainBkg` | Node fills match document primary |
| `SecondaryColor` | `secondaryColor` | Secondary elements match document accent |
| `BodyTextColor` | `textColor`, `primaryTextColor` | Label text matches document body |
| `TableHeaderBackground` | `primaryColor` (alternative) | Already maps to same primary |
| `TableHeaderForeground` | `primaryTextColor` (alternative) | Already maps to same |
| `CodeBackgroundColor` | `tertiaryColor`, `noteBkgColor` | Subtle backgrounds match code bg |
| `CodeBlockBorderColor` | `noteBorderColor` | Border style consistency |
| `LinkColor` | `lineColor` | Connectors use accent color |
| `HeadingFont` | `fontFamily` | Diagram labels use document heading font |
| `BaseFontSize` | `fontSize` | Base text size (converted to px) |

**Changes required:**

1. **MermaidRenderer.cs** -- `RenderAsync()` and `BuildHtml()` accept a new parameter (a simple DTO or `ResolvedTheme` directly) and inject `themeVariables` into the `mermaid.initialize()` call.

2. **MermaidDiagramRenderer.cs** -- Constructor or `Transform()` receives `ResolvedTheme` and passes it through to `MermaidRenderer.RenderAsync()`.

3. **TransformContext.cs** -- Add an optional `ResolvedTheme?` property, or pass it directly to `MermaidDiagramRenderer`'s constructor.

4. **ConvertCommand.cs** -- Move theme resolution BEFORE the transform phase (it has no dependency on parsing/AST, only on CLI args). Then pass the resolved theme to `MermaidDiagramRenderer`.

5. **DiagramCache.cs** -- Include theme-relevant properties in the cache key hash. Not the entire `ResolvedTheme` (page margins are irrelevant), just the subset that affects Mermaid output. A helper method like `ComputeMermaidThemeHash(ResolvedTheme)` would produce a stable string from the mapped properties, appended to the cache key.

### Option B: CSS Variable Override (Not Recommended)

Inject a `<style>` block with CSS variables (`:root { --mermaid-primary: #1B3A5C; }`) into the HTML page. Mermaid does NOT natively consume CSS variables (see mermaid-js/mermaid#6677 -- this is a requested feature, not implemented). This would require patching Mermaid's generated SVG styles after rendering, which is fragile and version-dependent.

### Option C: Post-Process SVG (Not Recommended)

Render with default theme, then do string-replacement on the SVG's inline styles before screenshotting. Extremely fragile -- Mermaid's SVG structure and class names change between versions. No stable API contract for SVG internals.

## Recommendation

**Option A** is the clear winner. It uses Mermaid's official, documented theming API. The `base` theme with `themeVariables` is the intended customization path. The implementation is straightforward: build a JSON object from ResolvedTheme properties and interpolate it into the `mermaid.initialize()` call.

## Detailed Design

### New Type: MermaidThemeConfig

A lightweight DTO in `Md2.Diagrams` that holds only the theme properties relevant to Mermaid rendering. This avoids coupling `Md2.Diagrams` to the full `ResolvedTheme` (which lives in `Md2.Core`). However, since `Md2.Diagrams` already depends on `Md2.Core` (it references `Md2.Core.Exceptions` and `Md2.Core.Ast`), using `ResolvedTheme` directly is also acceptable if the team prefers fewer types.

```
// Sketch -- not implementation code, just shape
public class MermaidThemeConfig
{
    public string PrimaryColor { get; init; }      // hex without #
    public string SecondaryColor { get; init; }
    public string TextColor { get; init; }
    public string BackgroundColor { get; init; }
    public string BorderColor { get; init; }
    public string LineColor { get; init; }
    public string FontFamily { get; init; }
    public double FontSizePt { get; init; }

    public static MermaidThemeConfig FromResolvedTheme(ResolvedTheme theme) => ...;
    public string ToCacheKey() => ...; // deterministic string for hashing
}
```

### Modified BuildHtml

The `BuildHtml` method changes from:

```javascript
mermaid.initialize({ startOnLoad: true, theme: 'default' });
```

to:

```javascript
mermaid.initialize({
  startOnLoad: true,
  theme: 'base',
  themeVariables: {
    primaryColor: '#1B3A5C',
    primaryTextColor: '#FFFFFF',
    // ... mapped from MermaidThemeConfig
  }
});
```

The `theme` value MUST change from `'default'` to `'base'` -- this is essential. Only the `base` theme respects `themeVariables` overrides.

### Font Handling

Mermaid renders in a Chromium browser context. The font specified in `fontFamily` must be available to the browser. Options:

1. **System fonts only.** If the user's `HeadingFont` is installed on the system, Chromium will use it. If not, the CSS fallback chain applies. This is the pragmatic v1 approach.
2. **Web font injection.** Load Google Fonts or embed font files in the HTML. Adds complexity and breaks air-gap. Not recommended for v1.

The `fontFamily` CSS value should include a fallback: `"Calibri, Arial, sans-serif"`. The mapping should append generic fallbacks (`sans-serif` for heading fonts, `monospace` for mono fonts).

### Cache Key Update

Current: `SHA256(versionSalt + mermaidSource)`

Proposed: `SHA256(versionSalt + mermaidSource + themeConfig.ToCacheKey())`

Where `ToCacheKey()` produces a deterministic string like `"pc=1B3A5C;sc=4A90D9;tc=333333;ff=Calibri;fs=11"`. This ensures different themes produce different cache entries.

### Pipeline Reordering

In `ConvertCommand.cs`, move the "Resolve theme via 4-layer cascade" block (currently at line ~211) to BEFORE the "Transform" block (line ~189). Theme resolution reads from CLI args, theme files, and presets -- none of which depend on the parsed Markdown AST. This reordering is safe.

After reordering:
1. Parse
2. Resolve theme (moved up)
3. Transform (now has access to resolved theme)
4. Emit

## Files That Change

| File | Change |
|------|--------|
| `src/Md2.Diagrams/MermaidRenderer.cs` | `BuildHtml()` accepts theme config, generates `themeVariables` JSON. `RenderAsync()` accepts optional theme config. |
| `src/Md2.Diagrams/MermaidDiagramRenderer.cs` | Constructor accepts `MermaidThemeConfig` (or `ResolvedTheme`), passes to renderer. |
| `src/Md2.Diagrams/DiagramCache.cs` | `TryGetCached()`, `GetCachePath()`, `Store()` accept an optional theme key suffix. |
| `src/Md2.Diagrams/MermaidThemeConfig.cs` | **New file.** DTO + mapping from ResolvedTheme. |
| `src/Md2.Cli/ConvertCommand.cs` | Reorder theme resolution before transforms. Pass theme to `MermaidDiagramRenderer`. |
| `src/Md2.Core/Transforms/TransformContext.cs` | Optionally add `ResolvedTheme?` property (alternative: pass theme directly via constructor injection on the transform). |
| Tests | Update existing MermaidRenderer tests to cover themed rendering. Add tests for cache key differentiation. |

## Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| `base` theme looks different from `default` even without custom variables | Medium | Test the visual output. The `base` theme with default colors produces a clean, neutral look. May need to tune the default mapping so that diagrams without a custom theme still look good. |
| Font not available in Chromium context | Low | Use CSS fallback chains. Document that diagram fonts depend on system-installed fonts. |
| Cache invalidation for existing cached PNGs | Low | The cache key changes to include theme hash. Old cache entries (without theme hash) are simply cache misses -- they will be re-rendered. No migration needed. |
| Pipeline reordering side effects | Low | Theme resolution has no dependency on the AST. The only coupling is that `TransformContext` currently does not carry theme data, which is the gap we are filling. |
| Mermaid `themeVariables` behavior changes in future versions | Low | We pin to Mermaid 11.13.0 (embedded). Variable names are stable across minor versions. Test in CI with visual snapshot comparison. |
| Breaking the `MermaidRenderer` public API | Medium | `RenderAsync()` gains a new optional parameter. Existing callers that pass no theme config get the current default behavior. This is backward-compatible. |

## Implementation Cost Estimate

- **Size:** Small-Medium. Core change is in `BuildHtml()` (10-20 lines) plus plumbing (~50 lines across 4 files). New DTO (~30 lines).
- **Risk:** Low. Uses Mermaid's documented API. No new dependencies. Backward-compatible.
- **Testing:** Existing Mermaid rendering tests need theme variants. Cache key tests need theme differentiation cases. Visual inspection recommended for the first pass.

## Open Questions

1. **Should the default (no custom theme) behavior change?** Currently uses `theme: 'default'`. If we switch to `theme: 'base'` with the default ResolvedTheme colors mapped in, the diagrams will look slightly different even for users who have not customized anything. Options: (a) only use `base` theme when a custom theme is explicitly provided, (b) always use `base` with mapped defaults. Option (a) is safer for backward compatibility.

2. **Should there be a per-diagram override?** Mermaid supports `%%{init: {'theme': 'dark'}}%%` frontmatter directives within the diagram source. If a user specifies an inline theme override, should it take precedence over the document theme? The natural answer is yes -- inline overrides win -- but we should verify that Mermaid merges inline `themeVariables` with `initialize()` variables or whether inline completely replaces them.

3. **Dark mode / light mode.** If a theme has a dark background (e.g., `PrimaryColor` is very dark), the auto-derived `primaryTextColor` needs to be light. Should we auto-compute contrast, or require the theme author to specify both primary and primaryText? Mermaid's `base` theme does some auto-derivation from `primaryColor` but the results are not always readable. We may need a contrast-check utility.
