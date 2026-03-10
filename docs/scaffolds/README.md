---
agent-notes: { ctx: "explains scaffold stubs, moved to docs/ during setup", deps: [CLAUDE.md], state: canonical, last: "diego@2026-02-21" }
---

# Project Scaffolds

These are **stub files** that get moved to `docs/` during scaffolding or kickoff. They are templates for project-specific documentation — fill them in as your project takes shape.

| File | Purpose | Owner |
|------|---------|-------|
| `code-map.md` | Package structure, public APIs, data flow | Coordinator |
| `config-manifest.md` | Environment variables, feature flags, config files | Ines |
| `performance-budget.md` | Latency targets, resource constraints | Vik (review), Ines (verify) |
| `tech-debt.md` | Technical debt register | Grace (tracks), Pat (prioritizes) |
| `test-strategy.md` | Test pyramid, coverage targets, flaky test policy | Tara |

**Scaffold commands** (`/scaffold-*`) and `/kickoff` move these files into `docs/` automatically.
