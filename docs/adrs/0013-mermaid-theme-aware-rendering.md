---
agent-notes: { ctx: "ADR for theme-aware Mermaid diagram rendering", deps: [docs/research/mermaid-theme-aware-rendering.md, MermaidRenderer.cs, ResolvedTheme.cs], state: active, last: "archie@2026-03-13" }
---

# ADR-0013: Theme-Aware Mermaid Diagram Rendering

## Status

Accepted

## Context

Mermaid diagrams rendered by md2 always use Mermaid's built-in `default` theme regardless of the document's theme preset. A document using the `hackterm` preset (phosphor green, dark backgrounds) still produces diagrams with grey/purple Mermaid defaults. This breaks visual consistency between the document body and embedded diagrams.

The root cause is that `MermaidRenderer.BuildHtml()` hardcodes `theme: 'default'` in the `mermaid.initialize()` call, and the rendering pipeline has no access to the resolved theme during the transform phase.

## Decision

We will make Mermaid diagrams theme-aware by:

1. **Always using Mermaid's `base` theme** with `themeVariables` mapped from `ResolvedTheme`. The `base` theme is the only Mermaid theme that supports full customization via `themeVariables`. Even the default preset will use `base` with mapped defaults.

2. **Creating a `MermaidThemeConfig` DTO** in `Md2.Diagrams` to decouple the diagram renderer from the full `ResolvedTheme` surface. This DTO maps the subset of theme properties relevant to Mermaid rendering.

3. **Reordering theme resolution before transforms** in `ConvertCommand`. Theme resolution depends only on CLI args/files, not the parsed AST, so this reordering is safe. This gives transforms access to the resolved theme.

4. **Including the theme hash in the diagram cache key** to ensure different themes produce different cached PNGs. The cache key becomes `SHA256(versionSalt + mermaidSource + themeConfig.ToCacheKey())`.

5. **Respecting per-diagram `%%{init}%%` overrides.** If a Mermaid diagram contains inline theme directives, they take precedence over the document theme. This is Mermaid's native behavior and we do not interfere with it.

6. **Auto-deriving contrast for extreme palettes.** When a theme's primary color is very dark or very light, the mapped `primaryTextColor` will be automatically adjusted for readability using luminance calculation.

### Property Mapping

| ResolvedTheme | Mermaid themeVariable |
|---|---|
| PrimaryColor | primaryColor, mainBkg |
| SecondaryColor | secondaryColor |
| BodyTextColor | textColor, lineColor |
| TableHeaderForeground | primaryTextColor |
| CodeBackgroundColor | tertiaryColor, noteBkgColor |
| CodeBlockBorderColor | noteBorderColor, nodeBorder |
| TableAlternateRowBackground | clusterBkg |
| HeadingFont | fontFamily (with sans-serif fallback) |
| BaseFontSize | fontSize (pt → px) |

## Consequences

### Positive

- Diagrams visually match the document theme — hackterm gets green diagrams, editorial gets crimson, etc.
- Uses Mermaid's official, documented theming API (`base` theme + `themeVariables`)
- Cache correctness — different themes produce different cache entries
- Backward-compatible: `RenderAsync()` gains an optional parameter; callers that pass nothing get reasonable defaults

### Negative

- Default diagrams look slightly different (switching from `default` to `base` theme). The `base` theme with mapped default colors produces a clean, neutral look, but it is not identical to the old `default` theme output.
- Font availability depends on system-installed fonts. Chromium's headless browser can only use fonts installed on the host OS. CSS fallback chains mitigate but don't solve this completely.
- Cache invalidation: existing cached PNGs (from before this change) will be cache misses and re-rendered. No migration needed — they just re-render on first use.

### Neutral

- Pipeline reordering (theme resolution before transforms) changes execution order but not semantics. Theme resolution has no dependency on the AST.
- The `MermaidThemeConfig` DTO adds a new type but keeps the coupling boundary clean between `Md2.Diagrams` and `Md2.Themes`.
