---
agent-notes: { ctx: "discovery tracking for md2doc", deps: [CLAUDE.md, docs/methodology/phases.md], state: active, last: "cam@2026-03-11" }
---

# Discovery: md2doc

**Date:** 2026-03-11
**Lead:** Cam (with Dani for design pressure-testing)
**Status:** Complete
**Prior Phase:** None

## Vision

A daily-driver CLI tool (C#/.NET) that converts Markdown to polished, professional DOCX files. Local-only execution for sensitive material. Cross-platform (Windows + Linux). Output quality must be noticeably superior to pandoc.

**North star:** The output DOCX is indistinguishable from a document manually crafted by someone who knows Word's style system.

## Key Decisions

- Chose C#/.NET over Rust/Go because the .NET ecosystem has first-class DOCX support (Open XML SDK) and the user prioritizes easy interaction with Microsoft properties.
- Chose extended Markdown parser (CommonMark + GFM + admonitions, definition lists, attributes) over strict CommonMark because the user needs full feature coverage for technical documentation.
- Chose cascade-with-warnings template model over pure override because pure override causes silent style degradation when template is missing styles (a known pandoc pain point). Warnings teach users which styles md2doc expects.
- Chose Playwright for .NET over mermaid-cli/PuppeteerSharp/API for Mermaid rendering because it's Microsoft-maintained, .NET-native, works offline, and avoids requiring Node.js as a second runtime.
- Mermaid rendering is opt-in with one-time Chromium download (~300MB) to avoid penalizing users who don't use diagrams.
- Chose PNG at 2-3x DPI over SVG/EMF for diagram embedding because DOCX has poor native SVG support and high-res PNG is reliable cross-platform.
- Preview-and-adjust mode confirmed as a feature (not fire-and-forget only).
- No file size constraints on output.

## "Polished" Definition (Dani's Tiered Model)

**P0 — Essential (ship-blocking):** Typography (readable fonts, proper spacing, 4+ heading levels, widow/orphan control), tables (auto-sized, header distinction, thin borders, cross-page splitting, alternating rows), code blocks (monospace + background + syntax highlighting), lists (correct nesting, task list checkboxes), hyperlinks, images (embedded, aspect-preserved), page numbers, configurable page size/margins, YAML front matter → document metadata, inline code styling.

**P1 — Expected (differentiators):** Font pairing, coherent color palette, smart typography (curly quotes, em-dashes), blockquotes with colored left border, footnotes with bidirectional navigation, LaTeX math, auto-generated TOC, page headers, title/cover page from front matter, Mermaid at high resolution, admonitions/callouts (NOTE, WARNING, TIP).

**P2 — Delighters:** 3-5 genuinely different built-in style presets, multi-file concatenation, internal cross-references with page numbers, DOCX accessibility (alt text, tagged content), image captions.

## Styling System

- `--template <path>` — custom DOCX template (cascade: template styles take priority, md2doc fills gaps with defaults + CLI warnings for missing styles)
- `--style <preset>` — built-in style preset (e.g., "corporate", "technical", "minimal")
- Default: sensible built-in style when neither flag given

## Constraints

- Local-only (sensitive material, air-gappable)
- Cross-platform (Windows + Linux)
- .NET runtime dependency acceptable
- Daily use → CLI UX must be fast, clear errors, predictable

## Audience

Technical clients and internal teams. "Polished" = professional and readable, not flashy.

## Success Criteria

1. Supports all Markdown the user produces (extended CommonMark + GFM + extras)
2. Output quality noticeably better than pandoc
3. Custom DOCX templates respected — styles map correctly

## Failure Criteria

1. Markdown features not supported
2. Output worse than or equivalent to pandoc
3. Template styles ignored or broken

## Artifacts Produced

- This discovery document

## Open Questions

- None remaining from discovery phase

## Next Phase

- Phase 1b: Human Model Elicitation (Pat) → `docs/product-context.md`
- Phase 2: Sacrificial Concepts (Dani)
