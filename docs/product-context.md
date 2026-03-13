---
agent-notes: { ctx: "human product philosophy for Pat proxy mode", deps: [CLAUDE.md], state: active, last: "pat@2026-03-13" }
---

# Product Context — md2doc

**Last updated:** 2026-03-11
**Source:** Phase 1b elicitation with the human

## Decision Profile

| Dimension | Position | Notes |
|-----------|----------|-------|
| Quality vs. speed | **Quality** | Pandoc already exists for "good enough." This tool's reason to exist is better output. No shortcuts on polish. |
| Scope appetite | **Complete** | Prefers building the full thing once over incremental MVPs. All-at-once delivery. |
| Risk tolerance | **High** | "Who dares wins." Comfortable with heavy dependencies (Playwright/Chromium) if they serve the quality bar. |
| Decision delegation | **Full trust** | Wants the team to make technical choices and explain after. Does not want to be consulted on every library pick. |
| User model | **Self first, others eventually** | Primary user is the human (technical, CLI-fluent). Future audience: other technical users. |

## Non-Negotiables

1. **The tool must not feel clunky.** CLI UX must be fast, predictable, and minimal-friction for daily use.
2. **Hands-off mode must work.** Once the human trusts the output, they should be able to skip preview and run fire-and-forget. Mermaid may need a browser to pop, but preview should be skippable (e.g., `--no-preview` or making preview opt-in rather than default).
3. **No excessive per-run tweaking.** The defaults and built-in presets must be good enough that the human doesn't need to fiddle with flags every time.
4. **Built-in style adjustments must have small blast radius.** When the human says "modern is a bit too modern," the resulting code change should be localized to the preset definition — not a cascading refactor. Style presets must be architected for easy, isolated modification.
5. **Output quality is the product.** If the output isn't noticeably better than pandoc, the tool has no reason to exist.

## Proxy Guidelines (for Pat)

When the human is unavailable and a product question arises:

- **Bias toward quality over speed** in all trade-offs.
- **Bias toward completeness** — don't cut features to ship earlier unless they're genuinely independent and can be added without rework.
- **Accept heavy dependencies** if they serve the quality bar. Don't optimize for minimal footprint at the cost of output quality.
- **Protect the CLI UX** — every flag, every default, every error message matters. If a design decision makes the tool clunkier to use daily, push back.
- **Style presets are a first-class concern.** Treat them like a product surface, not an afterthought. They must be easy to adjust and visually distinct from each other.

## Escalation Triggers

Escalate to the human (do not proxy) when:
- A decision would fundamentally change the tool's scope or purpose
- A dependency choice would make the tool non-portable or non-local
- A UX decision would change the daily workflow (e.g., making preview mandatory)
- A quality trade-off is proposed that would bring output closer to pandoc-level

## Correction Log

| Date | What Changed | Why | Source |
|------|-------------|-----|--------|
| 2026-03-13 | Preview (#51, #52) restored to Sprint 9 as P1 core scope | Was incorrectly deferred "post-v1" in Sprint 8 plan. Human confirmed preview is a core feature needed for visual testing, discussed multiple times. Demotion rationale ("power-user feature") contradicted discovery notes and non-negotiable #2. | Sprint 8 retro, human feedback |
