---
agent-notes:
  ctx: "Session handoff — PPTX v2 Sprints 3+4 complete, Sprint 5 next"
  deps: [CLAUDE.md, docs/plans/v2-pptx-implementation-plan.md, docs/code-map.md]
  state: active
  last: "grace@2026-03-15"
---
# Session Handoff

**Created:** 2026-03-15
**Sprint:** v2-4 (PPTX) — complete
**Wave:** All waves complete for Sprints 3 and 4
**Session summary:** Completed all 8 Sprint 3 issues (theme integration) and 9 of 10 Sprint 4 issues (content types + images). Added Precedent-Blindness gotcha. 50 new tests. #130 (logos) deferred to post-v2.0 per Pat.

## What Was Done

### Sprint 3 — Theme Integration (8/8 complete)
- `ThemePptxSection` YAML schema with nested layout sections, per-format color overrides (#120)
- `ResolvedPptxTheme` sub-object per ADR-0016 Wei debate (#121)
- `pptx:` sections in all 10 presets with preset-appropriate styling (#122)
- Slide master/layout generation with theme-based backgrounds (#123)
- Speaker notes emission verified (#124)
- `<!-- fit -->` heading auto-scale via `NormalAutoFit` (#125)
- `MarpThemeMapper`: MARP theme → md2 preset hint (#126)
- PPTX `--style` overrides in CLI theme resolve (#127)
- `ThemeValidator` extended for `pptx:` section
- `ThemeCascadeResolver` extended with PPTX sub-object cascade
- Preset snapshot tests regenerated (10 presets)

### Sprint 4 — Content Types (9/10 complete)
- Header/footer rendering from MARP directives (#128)
- Slide numbers via `paginate` directive (#129)
- Native PPTX tables with theme-styled header/alternating rows (#131)
- Code blocks with background fill, border, padding from theme (#132)
- Build animations — click-to-reveal bullet timing tree (#133)
- Background images from local files with path safety (#134)
- Inline images with PNG/JPEG dimension reading, aspect-ratio scaling, path traversal rejection (#135)
- Rich text paragraphs with clickable hyperlinks (#136)
- Blockquote shapes with italic text and left border bar (#137)
- **Deferred:** #130 (logos in headers/footers) — md2 extension feature, not in existing MARP decks. Pat demoted to P1 post-v2.0.

### Process improvements
- Added **Precedent-Blindness anti-pattern** to `docs/process/gotchas.md` — don't treat established codebase patterns as open architectural decisions
- Fixed board status: #130 moved back to Backlog (was accidentally batch-moved to Done)

## Current State
- **Branch:** `pptx/v2`
- **Last commit:** `8666a62` feat: implement inline images with path safety, fix background image security gap
- **Uncommitted changes:** none (clean tree)
- **Tests:** 27 Pptx, 213 Themes, 135 Slides, 134 Core, 186 Docx, 46 Parsing, 37 Highlight (~985 non-Playwright)
- **Board status:** Sprints 1-4 at Done (except #130 at Backlog). Sprint 5 (#138-#143) at Backlog. Sprint 6 (#144-#150) at Backlog. Tech debt (#151-#154) at Ready.
- **Product context:** `docs/product-context.md` exists (last updated 2026-03-11)

## Sprint Progress
- **Wave plan:** `docs/plans/v2-pptx-implementation-plan.md`
- **Sprint 1:** COMPLETE (6 issues)
- **Sprint 2:** COMPLETE (10 issues)
- **Sprint 3:** COMPLETE (8 issues)
- **Sprint 4:** 9/10 COMPLETE (#130 deferred post-v2.0)
- **Issues completed this session:** #120-#129, #131-#137 (17 issues)
- **Next:** Sprint boundary for Sprints 3+4, then Sprint 5

## What To Do Next (in order)
1. Read `docs/code-map.md` to orient
2. Read `docs/plans/v2-pptx-implementation-plan.md` for sprint overview
3. **Run `/sprint-boundary`** for Sprints 3+4 combined
4. **Sprint 5: Mermaid native shapes + charts (v2.1 scope, 6 issues)**
   - #138 (L): Mermaid flowchart → native PPTX shapes — parse Mermaid graph structure → rectangles, diamonds, connectors
   - #139 (S): Mermaid shape styling from theme — colors, fonts from `ResolvedPptxTheme`
   - #140 (M): Mermaid image fallback for complex types — sequence, Gantt, ER → PNG embed
   - #141 (L): Chart code fence → native PPTX charts — bar, column, line, pie
   - #142 (M): Chart data format (CSV/YAML) — define and document
   - #143 (S): Chart palette from theme — `pptx.chartPalette` colors
5. **Sprint 6: Integration, docs, ship (7 issues, #144-#150)**
6. **Tech debt items at Ready:** #151 (bare MarkdownPipelineBuilder), #152 (CancellationToken), #153 (ILogger), #154 (README)

## Tracking Artifacts
- `docs/tracking/2026-03-15-pptx-marp-discovery.md` — Discovery phase
- `docs/tracking/2026-03-15-pptx-marp-architecture.md` — Architecture phase
- `docs/tracking/2026-03-15-pptx-marp-debate.md` — Wei debate
- `docs/tracking/2026-03-15-pptx-marp-plan.md` — Planning phase

## Proxy Decisions (Review Required)
- **#130 (logos) deferred post-v2.0.** Pat's call: logos are an md2 extension feature, not used in existing MARP decks. The human's existing decks won't be missing logos. Ship in v2.1.

## Key Context
- **Image embedding follows DOCX pattern.** `PptxEmitter` uses `EmitOptions.InputBaseDirectory` for path resolution and `ResolveImagePath()` with the same safety checks as `ImageBuilder.IsPathSafe`. No new abstractions needed.
- **Table implementation uses `A.GraphicFrame`** (not `P.Shape`) appended to ShapeTree. PPTX tables are GraphicFrames, not shapes. Theme-styled headers and alternating rows.
- **Build animations** use a basic timing tree with `ClickEffect` nodes. Full PPTX animation API (appear/fade/fly effects) is very complex — current implementation is click-advance only.
- **Hyperlinks** use `A.HyperlinkOnClick` on `A.RunProperties` with `InvalidUrl` for external URLs.
- **Slide layout names are lowercase:** `"title"`, `"section-divider"`, `"content"`, `"two-column"`, `"blank"`.
- **ADR-0016 cascade for colors:** `pptx.colors > shared colors > preset pptx.colors > preset shared > template`.
- **Shouldly 4.3.0:** `ShouldContain(string, string)` second arg maps to `Case` enum. Use named `customMessage:`.
- **System.CommandLine 2.0.5:** `SetAction` not `SetHandler`, `ParseResult.GetValue()` not `InvocationContext`.
- **Preset snapshot tests** auto-regenerate: delete `tests/Md2.Themes.Tests/Snapshots/Presets/*.json` and run twice.
- **New gotcha:** Precedent-Blindness — don't flag "architecture decisions" for problems another module already solves.
