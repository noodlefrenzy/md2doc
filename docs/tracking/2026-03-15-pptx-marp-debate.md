---
agent-notes: { ctx: "adversarial debate tracking for PPTX MARP ADRs", deps: [docs/adrs/0014-slide-document-ir.md, docs/adrs/0015-marp-parser-architecture.md, docs/adrs/0016-unified-theme-pptx-extension.md], state: active, last: "wei@2026-03-15" }
---

# Debate: PPTX MARP Architecture (ADRs 0014-0016)

**Date:** 2026-03-15
**Participants:** Wei (challenger), Archie (defender)
**Rounds:** 1 (sufficient — all actionable challenges resolved)

## ADR-0014: SlideDocument IR

| Challenge | Technique | Severity | Resolution |
|-----------|-----------|----------|------------|
| Two parallel pipelines will drift | Cost of being wrong | High | **Accepted.** Explicit `SlidePipeline` class — not generalized. Accept maintenance cost. |
| `SlideLayout` enum is closed for open problem | Assumption surfacing | Medium | **Accepted.** Replaced with open `record SlideLayout(string Name)` with well-known constants. |
| AST fragment reparenting may break transforms | Assumption + Historical precedent | High | **Accepted.** Required spike added to ADR. Fallback: re-parse from source text. |
| Annotation-only approach dismissed too quickly | Inversion | Medium | **Rejected.** IR makes emitter dramatically simpler. Rejection rationale added to ADR. |
| Per-slide transform perf at scale | Scale attack | Medium | **Accepted as design change.** Transforms run on full document before splitting. |
| `PresentationMetadata` duplicates `DocumentMetadata` | Assumption surfacing | Low | **Accepted.** Shared `IDocumentMetadata` interface. |

## ADR-0015: MARP Parser Architecture

| Challenge | Technique | Severity | Resolution |
|-----------|-----------|----------|------------|
| Rebuilding Marpit — directive handling underestimated | Assumption surfacing + Historical precedent | High | **Accepted.** Directive handling expanded to 3 classes (extraction, classification, cascading). |
| FragmentedListDetector relies on discarded info | Assumption surfacing | High | **Accepted.** Required spike added. Custom Markdig extension as fallback. |
| MARP syntax changes over time | Cost of being wrong | Medium | **Accepted.** Pinned to Marpit v3.x. Compatibility scope section added. |
| Why not consume marp-cli HTML output? | Inversion | Medium | **Rejected.** Loses AST structure, adds Node.js dep, blocks theme DSL. Documented. |
| AST splitting cross-slide references | Scale attack | Medium | **Accepted.** Footnote/link-ref handling documented in Negative consequences. |
| Image syntax is complex mini-DSL | Scale attack | Low-Medium | **Accepted.** Supported/unsupported syntax enumerated in compatibility scope. |

## ADR-0016: Unified Theme Extension

| Challenge | Technique | Severity | Resolution |
|-----------|-----------|----------|------------|
| Shared colors not visually consistent across formats | Assumption surfacing | High | **Accepted.** Per-format `colors:` override sub-section added to schema. |
| ResolvedTheme god object | Scale attack | High | **Accepted.** Mandatory `ResolvedPptxTheme` sub-object (not "alternatively"). |
| Separate theme files with shared tokens | Inversion | Medium | **Rejected.** User explicitly asked for shared YAMLs. Per-format overrides provide same flexibility. |
| MARP theme directive collision | Assumption surfacing | High | **Accepted — critical.** MARP `theme:` is hint for preset selection, not a cascade layer. Documented. |
| PPTX template extraction complexity | Cost of being wrong | Medium | **Accepted.** Deferred to post-v2. |
| Preview renderer interaction | Scale attack | Low | **Accepted.** Non-goals section added — preview is DOCX-only for v2. |

## Threat Model Update (Pierrot)

Pierrot updated `docs/security/threat-model.md` with 4 new DFDs and 16 new STRIDE entries covering:
- MARP directive YAML deserialization (3 entry points)
- Shape geometry injection (Mermaid → native PPTX shapes, unsandboxed)
- Chart data injection (code fence → OOXML chart XML)
- PPTX template reading surface
- 15 new implementation verification checklist items
