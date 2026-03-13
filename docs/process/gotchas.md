---
agent-notes:
  ctx: "implementation gotchas and established patterns"
  deps: [CLAUDE.md]
  state: active
  last: "grace@2026-03-12"
---
# Known Patterns and Gotchas

Extracted from CLAUDE.md to reduce context window load. Read this when working on implementation or debugging tasks. Projects populate sections as they discover gotchas.

## Testing Patterns (Tara)

- **Composition matrix is mandatory for container × inline features.** Unit tests on individual builders (TableBuilder, ListBuilder, ParagraphBuilder) verify each in isolation but miss bugs where a container builder bypasses the shared inline visitor. When a builder handles a container type, tests must cover every inline type *inside* that container:

  ```
  Container × Inline = required test
  ─────────────────────────────────
  Table      × {bold, italic, strike, link, code, bold+italic, mixed}
  List       × {bold, italic, strike, link, code}
  Blockquote × {bold, italic, strike, link, code}
  ```

  **Detection signal:** A builder has its own `switch` or pattern match over inline types (like `ExtractInlineText`) instead of delegating to the shared visitor — that's a guaranteed composition coverage gap. See Sprint 3 retro: TableBuilder stripped all inline formatting because it reimplemented text extraction instead of using `DocxAstVisitor.VisitInline`.

- **Synthetic test markdown is not enough.** The `RepresentativeMarkdown` constant in `EndToEndTests` is intentionally minimal. It tests individual features but not real-world combinations (links inside tables, strikethrough inside tables). The showcase sample (`test-sample.md`) exercises these combinations. Add a test that processes the actual showcase sample whenever rendering behavior changes.

- **Assert text content, not just element existence.** A test that checks "a Bold run exists" can false-positive if some unrelated run happens to be bold (e.g., header cells are always bold). Always also assert the *text* inside the formatted run matches what you expect.

## Code Review Findings (Vik)

- **Builder reimplements visitor logic (Bypass-Visitor smell).** If a builder (Table, List, Image) has its own `switch`/pattern-match over Markdig inline types instead of delegating to the shared `DocxAstVisitor.VisitInline` chain, it will silently strip formatting for any inline type it doesn't handle. **Detection signal:** the builder imports `Markdig.Syntax.Inlines` and has a method like `ExtractInlineText` that returns `string` instead of `IEnumerable<OpenXmlElement>`. **Fix:** accept an `InlineVisitorDelegate` and delegate to it. See Sprint 3 fix where `TableBuilder` was refactored to use this pattern.

## Security & Compliance (Pierrot)

<!-- Pierrot: add accepted risks, threat surfaces evaluated, and security
     trade-offs here. Decisions the human explicitly approved should be recorded
     so they aren't re-flagged in future sessions. -->

## Implementation Patterns (Sato)

- **Use `InlineVisitorDelegate` for any builder that handles cell/item content.** The pattern established in the TableBuilder fix: builders accept a delegate `(Inline, bool bold, bool italic, bool strikethrough) → IEnumerable<OpenXmlElement>` and call it instead of reimplementing inline traversal. This ensures all inline formatting (bold, italic, strike, link, code) is preserved. The delegate is wired by `DocxAstVisitor` when constructing builders. Keep the plain-text fallback path for unit tests that construct builders in isolation, but production always uses the delegate.

- **Header overrides are post-hoc, not pre-hoc.** When a container needs to force styling (e.g., table headers force bold + white), apply it *after* the inline visitor produces elements, not by passing flags into the visitor. This avoids double-application (e.g., `isHeader` as `bold=true` plus `ApplyHeaderOverrides` also adding bold) and keeps the visitor's output clean.

- **Guard against invalid nesting.** When a visitor delegate produces block-level elements (e.g., `VisitLink` returns a `Paragraph` for images), filter them out before appending inside another block. Nested `Paragraph` elements produce invalid Open XML. Use `.Where(e => e is not Paragraph)` or similar guards at the delegation boundary.

## Architecture Patterns (Archie)

<!-- Archie: add architectural constraints, integration point knowledge, and
     schema evolution notes here. Patterns that informed past ADRs but aren't
     worth a standalone ADR themselves. -->

## Adapter / Integration Gotchas

- **execa v9 `stdin: 'pipe'` default hangs subprocesses.** execa v9 changed `stdin` from `'inherit'` to `'pipe'`. CLI tools that check stdin connectivity (e.g., `claude -p`, `gemini`) see a connected pipe and wait for EOF, which never comes — the subprocess hangs until timeout. **Detection signal:** subprocess calls work with `--version` or `--help` (which exit immediately) but hang with actual workload flags. **Fix:** always set `stdin: 'ignore'` unless you explicitly need to write to the subprocess's stdin. Audit all execa/child_process calls to explicitly configure all three stdio channels.

- **Health checks that don't exercise the real code path.** A health check like `tool --version` exits immediately without reading stdin, so it succeeds even when the actual call (`tool -p "prompt"`) would hang. **Detection signal:** health check passes but actual tool invocation fails/hangs. **Fix:** health checks should exercise the same flags and stdio configuration as the real invocation, just with minimal input.

## Library API Gotchas

- **TextMateSharp undocumented API behavior (Sprint 4).** TextMateSharp has no public API docs. Key discoveries:
  - `Theme.Match()` takes `IList<string>` (the full scopes list), not a single scope string.
  - `ThemeTrieElementRule` exposes `foreground` and `fontStyle` as **public fields**, not properties — IntelliSense won't show them as settable properties.
  - `IToken` has `EndIndex` directly — don't compute it from the next token's `StartIndex`.
  - `TokenizeLine()` takes `LineText` which has implicit conversion from `string`.
  - Colors from `Theme.GetColor()` include a `#` prefix — strip it for OpenXml hex color values.
  - **Detection signal:** compile errors or runtime nulls when calling TextMateSharp. **Fix:** use reflection (`GetFields()`/`GetMembers()`) to discover the actual API surface.

- **Shouldly assertion message parameters (v4.x).** Not all Shouldly assertion methods accept a custom message parameter. Notably, `ShouldNotStartWith` does NOT accept a 3rd argument for a custom message in Shouldly 4.x — it causes CS1503. **Detection signal:** compile error on assertion with custom message. **Fix:** remove the message parameter or use a different assertion.

## Build and Run

<!-- Add build, bundling, and runtime gotchas here -->

## Process

- **Plans don't replace process (Plan-as-Bypass anti-pattern).** A detailed implementation plan (from plan mode, a prior session, or a human-provided spec) is **input** to the V-Team phases, not a bypass. The plan still needs: GitHub issues (Grace), architecture gate if applicable (Archie + Wei as standalone agents), TDD (Tara → Sato), code review (Vik + Tara + Pierrot), and Done Gate. **Detection signal:** if the coordinator's first tool call is `Read` on a source file (not `docs/code-map.md`, governance docs, or the sprint plan), it's likely in bypass mode. See `2026-02-20-process-violation-plan-bypass.md` for the full retro.

- **Wei must be invoked as a standalone agent.** The coordinator's own analysis of trade-offs is not a substitute for invoking Wei as a standalone agent during architecture debates. If an ADR claims "Wei debate resolved" but no Wei agent was spawned, the gate has not passed.

- **"Invoke the team" means spawn subagents (Solo-Coordinator anti-pattern).** When the human uses language like "invoke the team", "use the team", "have Cam look at this", or names any persona, the coordinator MUST spawn those agents via the Task tool. The coordinator doing the work inline — even if the output is good — violates the explicit human request. **Detection signal:** the human asked for a named persona or "the team" but no Task tool calls with `subagent_type` matching a persona appear in the response. **Fix:** parse the request for persona names or team-level language, then spawn the appropriate agents before doing any work.

- **Quick-Test Bypass anti-pattern.** The coordinator writes tests directly "to save time" instead of invoking Tara. The tests look reasonable but miss text content assertions, edge cases, and structural invariants. They become the committed suite and the gaps become permanent. **Detection signal:** test code appears in the coordinator's response with no Tara agent invocation. **Fix:** always invoke Tara for test authoring. Even for exploratory/diagnostic tests, hand them to Tara for review before committing. See `docs/process/team-governance.md` § Quick-Test Bypass for the full pattern.

- **Use scripts for stable logic, commands for evolving knowledge.** Static scripts are ideal when the rules are well-defined and unlikely to change. But when automation requires understanding things that change externally — evolving formats, shifting best practices, new API conventions — prefer a Claude Code command over a script. Commands bring current understanding (and can web-search) on every run.

- **Proxy mode is conservative, not permissive.** When the human is unavailable and Pat is acting as proxy, Pat defaults to the safer, more reversible option. The guardrails are strict:

  | Pat CAN (proxy) | Pat CANNOT (proxy) |
  |-----------------|-------------------|
  | Prioritize backlog items | Approve or reject ADRs |
  | Accept features against existing criteria | Change project scope |
  | Answer questions covered by product-context.md | Make architectural choices |
  | Defer items to next sprint | Merge to main |
  | Apply conservative defaults | Override Pierrot or Tara vetoes |

  When a question falls outside proxy authority, it blocks until the human returns. All proxy decisions are logged in `.claude/handoff.md` under `## Proxy Decisions (Review Required)`.

- **Product-context is a hypothesis, not ground truth.** `docs/product-context.md` captures Pat's model of the human's product philosophy — it's an educated guess that improves over time. The human can correct it at any time. When the human overrides a product-context-based recommendation, Pat updates the doc and logs the correction in the Correction Log table. Don't treat product-context entries as immutable rules.

- **Phase 1b must precede acceptance criteria writing.** Pat's Human Model Elicitation (kickoff Phase 1b) must complete before Pat writes acceptance criteria (Phase 4). The product context informs what "done" means to this human. Skipping 1b means acceptance criteria are written without understanding the human's quality bar, scope appetite, or non-negotiables.

- **Verify GitHub access before board operations.** Any workflow that touches the project board (sprint-boundary, kickoff, resume, handoff) must verify `gh auth status` and board accessibility before attempting board operations. If `gh` commands fail, STOP and ask the user to fix it — don't proceed and fail mid-workflow. The pre-flight checks are in: sprint-boundary Step 0, kickoff Phase 5 Pre-Flight, resume Step 3, and handoff Step 1. The resume check is especially critical — without it, a full sprint runs board-blind and every status transition is silently skipped.

- **Check devcontainer before implementation.** After planning completes (either via `/plan` or `/kickoff` Phase 5), check whether `.devcontainer/` exists. If not, ask the user if they want one before starting implementation. This prevents environment inconsistency issues during TDD cycles.

- **Agents own their gotchas sections.** The agent-attributed sections at the top of this file (Testing Patterns → Tara, Code Review Findings → Vik, etc.) are written by the named agent at the end of their work, as part of the done gate or handoff. Record project-specific operational knowledge that would save time in a future session — not general programming knowledge, not things already in ADRs or `code-map.md`. Keep entries specific: "mock the gateway at HTTP level, not SDK level, because the SDK swallows retry errors" beats "be careful with mocking." If an entry becomes broadly relevant beyond its section, promote it to an ADR, `code-map.md`, or the template itself.

- **Run `/handoff` after completing each wave.** When a session completes a wave but not the full sprint, run `/handoff` before ending the session. A stale handoff forces the next session to reconstruct state from git history, which is slower and error-prone. Sprint 9 demonstrated this: Wave 1 completed in session 1 without updating the handoff, and session 2 had to recover from a Sprint 8-era handoff.

- **Sprint boundary must end with a clean-tree gate.** Multi-step workflows (sprint boundary, kickoff) involve many file operations — archival moves, artifact creation, code reviews. Commits that run partway through the workflow leave late-written files unstaged. The `/sprint-boundary` Step 8 enforces a terminal `git status --porcelain` check and stages any orphaned changes. If you're writing a similar multi-step workflow, end it with the same pattern: check, stage, commit, re-check.

- **Horizontal Blindness anti-pattern.** Cross-cutting concerns (logging, error UX, config, debug support, README accuracy) fall between vertical work items. No single item owns them, so they degrade silently. **Detection signal:** 3+ sprints in with no logging or debug flags, README quick-start is broken, error messages are inconsistent across modules. **Fix:** run the operational baseline audit (`docs/process/operational-baseline.md`). Done Gate #14 catches per-item regressions; sprint boundary Step 5b catches product-level drift.

- **Green-Bar-Red-Product anti-pattern.** Every Done Gate passes individually, but the product isn't shippable — no observability, broken quick-start, inconsistent errors. The per-item gate verifies each item in isolation; it cannot see product-level properties that emerge from the combination. **Detection signal:** all items pass Done Gate, but a new user can't get the product working from the README, or production failures produce no useful diagnostics. **Fix:** Done Gate #14 provides per-item defense; sprint boundary Step 5b provides product-level defense. Both reference `docs/process/operational-baseline.md`.
