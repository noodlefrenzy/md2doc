---
agent-notes:
  ctx: "Sprint 11 retrospective — Release Preparation"
  deps: [docs/sprints/sprint-11-plan.md, docs/tech-debt.md]
  state: active
  last: "grace@2026-03-14"
---
# Sprint 11 Retrospective — Release Preparation

**Sprint:** 11
**Date:** 2026-03-14
**Duration:** 3 sessions (3 waves)
**Sprint Goal:** Dependency hygiene, documentation polish, and theme-aware rendering for v1 release.

## Summary

Sprint 11 delivered all 5 planned items across 3 waves. This was the final pre-release sprint — dependency upgrades, theme-aware rendering, contrast fixes, and README polish. No items deferred. No stretch items planned.

**Completed (5 of 5 planned — 100% delivery):**

### P0 — Must Do (2/2)
- #86 (M) — chore(deps): upgrade Playwright, Markdig, System.CommandLine, TextMateSharp.Grammars, OpenXml
- #87 (S) — docs: update SBOM with actual versions, missing deps, CVE table

### P1 — Should Do (3/3)
- #89 (M) — feat(diagrams): theme-aware Mermaid rendering via themeVariables
- #90 (M) — fix(emit-docx): code block text contrast handling for dark-background themes
- #88 (S) — docs: update README with preview, doctor, theme commands + project structure

## What Went Well

1. **100% delivery across all priority tiers.** Every P0 and P1 item shipped. Sprint 11 had no deferrals or scope cuts.

2. **Architecture Gate followed proactively.** #89 required an ADR (ADR-0013) and Wei debate before implementation. Both were completed, and the debate surfaced a real trade-off (TransformContext vs constructor injection) that was documented as a conscious decision.

3. **Code review caught real issues.** The Wave 2 code review found 1 Critical (JS injection via unsanitized hex in MermaidThemeConfig) and 4 Important findings for #89. The Wave 3 review caught incorrect CLI invocation syntax in the README before it shipped.

4. **Wave planning worked well.** Three waves with clear dependency ordering (deps first → features → docs) kept sessions focused and handoffs clean.

## What Could Be Better

1. **Board status hygiene across sessions.** Issues #89 and #90 were left at "In Progress" on the board after Wave 2, despite being committed and closed via `Closes #N`. The handoff mentioned this but the board wasn't corrected until the next session. Board transitions should happen in the same session as the work.

2. **Test count drift in code-map.** `docs/code-map.md` reports 591 tests but the actual count is 719. The code-map test inventory wasn't updated during Sprint 11's test additions (48 tests for #89, 4 for #90). The code-map should be updated when tests are added.

## Process Observations

- **This is the final sprint before v1 release.** All features, process debt, and documentation are complete. The deferred items (#53, #55, #59) are explicitly post-v1.
- **Dependency upgrades (System.CommandLine 2.0.5) required significant API migration** in Wave 1 — SetAction instead of SetHandler, ParseResult.GetValue instead of InvocationContext. This was the riskiest work in the sprint.
- **Mermaid benchmark remains flaky.** `Mermaid_10Diagrams_Under15Seconds` takes 17-30s in devcontainer. This is a known environment limitation, not a regression.

## Architecture Gate Compliance

**ADRs created this sprint:** 1 (ADR-0013: Mermaid theme-aware rendering)
**Debate tracking artifacts created this sprint:** 1 (2026-03-13-mermaid-theme-debate.md)
**Architecture Gate compliance:** 1/1 ADRs had Wei debates tracked. 6 challenges, all resolved.
**Unrecorded architectural decisions:** 0 — #86 was version bumps with API migration (no new patterns), #87/#88 were docs-only, #90 was a localized fix in CodeBlockBuilder.

## Board Compliance — Sprint 11

**Board compliance:** 3/5 items followed the full status flow within their sessions.

| Issue | Flow | Notes |
|-------|------|-------|
| #86 | In Progress → In Review → Done | Clean |
| #87 | In Progress → In Review → Done | Clean |
| #89 | In Progress → (stuck) → Done | Board not updated to In Review/Done during Wave 2 session. Fixed in Wave 3 session. |
| #90 | In Progress → (stuck) → Done | Same as #89 — board left at In Progress across session boundary. |
| #88 | In Progress → In Review → Done | Clean |

**Finding:** Board transitions for #89 and #90 were skipped during the Wave 2 handoff. The commit messages closed the GitHub issues, but the project board status wasn't updated to Done. This was corrected at the start of the Wave 3 session.

## Metrics

| Metric | Sprint 10 | Sprint 11 |
|--------|-----------|-----------|
| Items planned | 2 | 5 |
| Items completed | 2 (100%) | 5 (100%) |
| Items deferred | 0 | 0 |
| Commits | 2 | 10 |
| New tests | 7 | 52 |
| Process violations | 0 | 1 (board status lag for #89/#90) |
| Total tests | 667 | 719 |

## Operational Baseline Audit — Sprint 11

### Ines: Operational Concerns

| Concern | Status | Finding |
|---------|--------|---------|
| Logging | Foundation | ILogger via Microsoft.Extensions.Logging, 3 levels (--debug/--verbose/default), per-phase timing |
| Error UX | Foundation | Md2Exception.UserMessage/Message split, --debug for stack traces, actionable error messages |
| CLI Contract | Foundation | Exit codes 0/1/2, stdout for output path, stderr for diagnostics, consistent flag naming |
| Config Health | Foundation | ThemeValidator with line-level errors, cascade traceable via --verbose |
| Graceful Degradation | Foundation | 30s Playwright timeouts, Chromium-not-installed detection, partial-failure degradation to warnings |
| Debug Support | Foundation | --debug stack traces, md2 doctor diagnostics, elapsed-time per pipeline stage |

**Minor observations (not blockers):**
- Inconsistent luminance algorithms: `MermaidThemeConfig.DeriveContrastTextColor()` uses Rec. 601 luma; `CodeBlockBuilder.RelativeLuminance()` uses sRGB linearization per WCAG. Both produce reasonable results but different formulas for the same purpose.
- No DEBUG logging when `EnsureContrast()` replaces a token color — could aid theme debugging.
- `ThemeParseException` and `FrontMatterParseException` bypass the UserMessage/Message split pattern (minor pattern drift, behavior is correct).

### Diego: README 5-Minute Test

- **Result:** Pass
- **Execution-verified:** build, help, version, doctor, theme list, theme resolve, theme validate, theme extract, convert, preview --help, dotnet test
- **Read-verified:** preview (requires browser/display)
- **Issues found:**
  - P2: `-q`/`--quiet` description mismatch — README said "suppress output path" but --help says "suppress warnings" → **fixed during boundary**
  - P3: `--debug` flag not documented in README
  - P3: `--version` flag not documented in README
  - Info: Flaky benchmark test (environment-dependent, not a docs issue)

### Gate

All applicable operational concerns are at Foundation level. No blocking gate triggered.
