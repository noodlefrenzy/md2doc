---
agent-notes: { ctx: "ADR for extending unified theme schema to cover PPTX", deps: [docs/adrs/0009-yaml-theme-dsl.md, docs/adrs/0014-slide-document-ir.md], state: proposed, last: "archie@2026-03-15", key: ["Wei debate complete — per-format color overrides, ResolvedPptxTheme sub-object, MARP theme collision resolved, template extraction deferred"] }
---

# ADR-0016: Unified Theme Schema Extension for PPTX

## Status

Proposed

## Context

ADR-0009 established the YAML theme DSL with a 4-layer cascade and noted that a `pptx:` section would be added in v2. That time has come.

The user requires:
1. **Shared presets** — `md2 convert deck.md -o deck.pptx --preset nightowl` should produce a PPTX styled consistently with the DOCX output from the same preset.
2. **PPTX-specific properties** — Slide dimensions, layout backgrounds, placeholder positioning, chart palettes, and other PPTX-only concerns that have no DOCX equivalent.
3. **PPTX template extraction** — `md2 theme extract template.pptx -o theme.yaml` should reverse-engineer a theme from an existing PPTX file.
4. **4-layer cascade** — The same cascade (CLI > YAML > preset > template) applies to PPTX.

**Options evaluated:**

1. **Additive `pptx:` section.** The theme YAML gets a `pptx:` section alongside the existing `docx:` section. Shared properties (`typography`, `colors`) apply to both formats. Format-specific properties live in their respective sections.

2. **Separate PPTX theme files.** PPTX themes are completely separate YAML files with a different schema. No sharing with DOCX themes.

3. **Single flat schema.** All properties (DOCX and PPTX) live in one flat namespace. The emitter picks what it needs.

## Decision

Use **Option 1: Additive `pptx:` section** in the existing theme YAML schema.

**Schema structure:**

```yaml
meta:
  name: nightowl
  description: "Dark theme inspired by Night Owl color palette"
  version: 1

typography:          # Shared across DOCX and PPTX
  headingFont: "Segoe UI"
  bodyFont: "Segoe UI"
  monoFont: "Cascadia Code"

colors:              # Shared defaults — can be overridden per-format
  primary: "7e57c2"
  secondary: "42a5f5"
  bodyText: "d6deeb"
  codeBackground: "011627"
  codeBorder: "1d3b53"
  link: "42a5f5"
  tableHeaderBackground: "7e57c2"
  tableHeaderForeground: "ffffff"
  tableBorder: "1d3b53"
  tableAlternateRow: "0b2942"
  blockquoteBorder: "42a5f5"
  blockquoteText: "a0b4c8"

docx:                # DOCX-specific (existing, unchanged)
  baseFontSize: 11
  heading1Size: 28
  # ... existing properties ...
  colors:            # (Wei debate) Per-format color overrides
    bodyText: "333333"           # Dark text for white page background
  page:
    width: "8.5in"
    height: "11in"

pptx:                # PPTX-specific (new)
  slideSize: "16:9"                # or "4:3", "16:10", custom "WxH"
  baseFontSize: 24                 # PPTX base is larger than DOCX
  heading1Size: 44
  heading2Size: 36
  heading3Size: 28
  colors:            # (Wei debate) Per-format color overrides
    bodyText: "d6deeb"           # Light text for dark slide background
  titleSlide:
    titleSize: 54
    subtitleSize: 28
    backgroundColor: "011627"      # Override per-layout
  sectionDivider:
    titleSize: 44
    backgroundColor: "0b2942"
  content:
    titleSize: 36
    bodySize: 24
    bulletIndent: 36               # points per level
  twoColumn:
    gutter: 48                     # points between columns
  background:
    color: "011627"                # Default slide background
    image: null                    # Default background image
  chartPalette:                    # Colors for native PPTX charts
    - "7e57c2"
    - "42a5f5"
    - "66bb6a"
    - "ffa726"
    - "ef5350"
    - "ab47bc"
  codeBlock:
    fontSize: 14
    padding: 12                    # points
    borderRadius: 8               # points (approximated in PPTX)
```

**(Wei debate: per-format color overrides.)** The shared `colors` section provides defaults. Both `docx:` and `pptx:` sections can include a `colors:` sub-section that overrides any shared color. This is critical for dark-background slides (PPTX) vs. white-page documents (DOCX) where the same body text color would be unreadable in one format.

**Cascade precedence for colors:** `CLI --style > format-specific colors > shared colors > preset format-specific > preset shared > template`

**Cascade behavior:**

The 4-layer cascade resolves properties exactly as it does for DOCX:

```
Layer 4 (highest):  CLI --style overrides (e.g., --style pptx.baseFontSize=28)
Layer 3:            theme.yaml pptx: section
Layer 2:            Preset defaults (preset's pptx: section)
Layer 1 (lowest):   template.pptx styles (extracted at runtime)
```

Shared properties (`typography`, `colors`) are resolved once and used by both emitters. Format-specific properties are resolved only when the corresponding emitter is active.

**PPTX template extraction — deferred to post-v2:**

**(Wei debate: extraction complexity.)** PPTX template extraction involves multiple slide masters (which is "the" theme?), indexed theme colors (dk1/lt1/accent1-6 → md2 color vocabulary mapping is ambiguous), and multi-level font size inheritance across slide masters and layouts. This feature could cost as much implementation effort as the entire PPTX emitter. **Deferred to post-v2.** Ship v2 with preset-based theming only. Users create theme YAML files by hand or by tweaking presets. The DOCX template extraction workflow is unaffected.

**MARP `theme:` directive interaction:**

**(Wei debate: critical collision.)** When a MARP deck specifies `theme: gaia` in front matter and the user runs `md2 convert deck.md -o deck.pptx --preset nightowl`:

- The MARP `theme:` directive is **ignored for styling** — md2 has its own theme system. MARP themes are CSS-based and have no PPTX equivalent.
- If no `--preset` or `--theme` is specified, the MARP `theme:` directive is used as a **hint** to select an md2 preset: `theme: gaia` → `--preset default`, `theme: uncover` → `--preset modern`. This mapping is documented and overridable.
- The MARP `theme:` value is stored in `PresentationMetadata.Theme` for reference but does not enter the cascade.

**Preset updates:**

Every preset (`default`, `modern`, `technical`, `nightowl`, etc.) must define both `docx:` and `pptx:` sections. The `pptx:` section uses the same color palette and typography but with slide-appropriate font sizes and layout settings.

**ResolvedTheme extension — mandatory sub-object:**

**(Wei debate: god object prevention.)** `ResolvedTheme` gains a `Pptx` sub-object. PPTX properties are NOT added as flat fields on `ResolvedTheme`. This prevents the class from growing unboundedly as formats are added.

```csharp
// Md2.Core/Pipeline/ResolvedTheme.cs
public class ResolvedTheme
{
    // ... existing shared + DOCX properties unchanged ...

    // PPTX-specific (Wei debate: mandatory sub-object, not flat fields)
    public ResolvedPptxTheme? Pptx { get; set; }
}

// Md2.Core/Pipeline/ResolvedPptxTheme.cs
public class ResolvedPptxTheme
{
    public double BaseFontSize { get; set; } = 24.0;
    public double Heading1Size { get; set; } = 44.0;
    public double Heading2Size { get; set; } = 36.0;
    public double Heading3Size { get; set; } = 28.0;
    public SlideSize SlideSize { get; set; } = SlideSize.Widescreen16x9;
    public string BackgroundColor { get; set; } = "FFFFFF";
    public IReadOnlyList<string> ChartPalette { get; set; }
    public ResolvedSlideLayoutTheme TitleSlide { get; set; }
    public ResolvedSlideLayoutTheme SectionDivider { get; set; }
    public ResolvedSlideLayoutTheme Content { get; set; }
    public ResolvedSlideLayoutTheme TwoColumn { get; set; }
    public ResolvedCodeBlockTheme CodeBlock { get; set; }
    // Per-format color overrides (merged from pptx.colors section)
    public string? BodyTextColor { get; set; }
    public string? PrimaryColor { get; set; }
    // ... other overridable colors
}
```

The cascade resolver populates `ResolvedTheme.Pptx` only when the target format is PPTX. The DOCX emitter never touches it. Future formats (Reveal.js, etc.) get their own sub-objects.

## Non-Goals

**(Wei debate: explicit scoping.)**

- **Preview is DOCX-only for v2.** The HTML preview renderer does not render slides. Slide preview is a post-v2 feature.
- **PPTX template extraction is post-v2.** See above.
- **Design token layer is not introduced.** Wei proposed a format-agnostic design token file that both DOCX and PPTX themes import. This is architecturally cleaner but adds a layer of indirection the user didn't ask for. The per-format `colors:` override mechanism achieves the same flexibility within a single file.

## Consequences

### Positive

- **One preset, two formats.** `--preset nightowl` works for both DOCX and PPTX with visually consistent output.
- **Familiar cascade.** Users who understand the DOCX theme system immediately understand the PPTX theme system.
- **Template round-trip.** Extract from PPTX → tweak YAML → apply to future conversions. Same workflow as DOCX.
- **Schema version unchanged.** `meta.version: 1` still works — the `pptx:` section is additive and forward-compatible (older md2 versions ignore unknown sections per ADR-0009).

### Negative

- **Presets get larger.** Every preset must define both `docx:` and `pptx:` sections, roughly doubling preset file size.
- **Shared `colors` need per-format overrides.** (Wei debate.) A color palette that works on white pages may not work on dark slides. The `docx.colors` and `pptx.colors` override mechanism handles this but adds cascade complexity.
- **Shared `typography` may not always map perfectly.** Font sizes are format-specific (11pt DOCX body vs. 24pt PPTX body), so sizes live in format sections. Font *names* transfer well and stay shared.
- **MARP `theme:` directive collision** requires documented mapping rules. Users may be confused when their MARP theme is ignored in favor of md2's preset system.

### Neutral

- The schema validator must now validate the `pptx:` section in addition to `docx:`.
- `md2 theme resolve` output includes PPTX properties when the target format is PPTX.

## Fitness Functions

- [ ] Every preset YAML defines both `docx:` and `pptx:` sections with visually consistent styling.
- [ ] `md2 theme resolve --format pptx` shows PPTX-specific properties and their cascade sources, including per-format color overrides.
- [ ] No DOCX-specific units (twips) appear in `pptx:` section — PPTX uses points and EMUs natively.
- [ ] Shared `typography` and `colors` sections provide sensible defaults; format-specific `colors:` sub-sections override where needed.
- [ ] `ResolvedTheme.Pptx` is a sub-object, not flat fields on `ResolvedTheme`. No PPTX-prefixed properties on the root class.
- [ ] MARP `theme:` directive does not enter the cascade — it is a hint for preset selection only.
