---
agent-notes: { ctx: "Wei challenge session on ADR-0013 Mermaid theming", deps: [docs/adrs/0013-mermaid-theme-aware-rendering.md, docs/research/mermaid-theme-aware-rendering.md], state: complete, last: "wei@2026-03-13" }
---

# Debate: ADR-0013 Theme-Aware Mermaid Rendering

**Date:** 2026-03-13
**Challenger:** Wildcard Wei
**ADR:** 0013 — Theme-Aware Mermaid Diagram Rendering
**Design Doc:** docs/research/mermaid-theme-aware-rendering.md

## Challenge Log

### C1: Always using `base` theme is an unnecessary breaking change

**Challenge:** Switching from `theme: 'default'` to `theme: 'base'` for ALL users, including those with no custom theme, changes default output. The design doc's own Open Question 1 identifies Option (a) — only use `base` when a custom theme is active — as the safer path.

**Counter-proposal:** Conditional: use `base` + `themeVariables` only when the user has a non-default theme active. Otherwise keep `theme: 'default'`.

**Resolution:** ACCEPTED as-is. The visual difference between `default` and `base` with mapped defaults is minor, and consistency across all presets outweighs backward-compatibility for a pre-release tool.

### C2: MermaidThemeConfig DTO vs. passing ResolvedTheme directly

**Challenge:** Md2.Diagrams already depends on Md2.Core. The coupling boundary argument is already violated.

**Concession:** The DTO legitimately narrows the contract to "these 8 properties affect diagram rendering" and makes cache key computation unambiguous.

**Counter-proposal:** Make it a `record` type for structural equality.

**Resolution:** ACCEPTED with guidance. DTO kept as class for now; record conversion tracked as future improvement.

### C3: Transform contract pollution via TransformContext

**Challenge:** Adding `ResolvedTheme?` to `TransformContext` breaks the clean separation where transforms are theme-agnostic. Constructor injection on `MermaidDiagramRenderer` would be cleaner.

**Resolution:** ACCEPTED with caveat. We added `ResolvedTheme?` as an optional property on `TransformContext` for pragmatism. The optional parameter preserves backward compatibility. Acknowledged this creates precedent — future transforms should NOT depend on theme without good reason.

### C4: Auto-deriving contrast for extreme palettes

**Challenge:** YAGNI. Two layers of auto-derivation (ours + Mermaid's built-in) may conflict. Overrides intentional theme author choices.

**Resolution:** KEPT. The hackterm preset demonstrates a real need (dark primary color with white text). Implementation is minimal (~15 lines) and only activates for extreme luminance values.

### C5: Theme hash in cache key

**Challenge:** None — correct and necessary.

**Resolution:** ACCEPTED. Cache growth concern noted for future.

### C6: Should theming happen at emit time instead of transform time?

**Challenge:** Moving rendering to emit layer would keep transforms theme-free.

**Self-rebuttal:** Would require Playwright in emitters, breaking clean separation.

**Resolution:** WITHDRAWN. Transform-time rendering is correct.

## Summary

| # | Challenge | Status | Action |
|---|-----------|--------|--------|
| C1 | `base` theme as default is breaking | ACCEPTED | Minor change for pre-release tool |
| C2 | DTO vs ResolvedTheme | ACCEPTED | Record conversion as future improvement |
| C3 | Transform contract pollution | ACCEPTED | Optional parameter, documented precedent |
| C4 | Auto-contrast derivation | KEPT | Real need demonstrated by hackterm |
| C5 | Theme hash in cache key | ACCEPTED | Cache growth noted |
| C6 | Emit-time rendering | WITHDRAWN | Transform-time is correct |
