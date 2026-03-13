---
agent-notes: { ctx: "retroactive Wei debate for ADR-0012 preview server", deps: [docs/adrs/0012-html-preview-server.md], state: active, last: "wei@2026-03-13" }
---

# ADR Debate: ADR-0012 — HTML Preview Server with Hot-Reload

**Date:** 2026-03-13
**Challenger:** Wei
**Architecture Lead:** Archie
**Status:** Complete (retroactive — debate conducted after implementation per process-improvement #83)

---

## Challenges

### 1. [Alternative Technology] TcpListener vs. Kestrel Minimal API

The ADR originally specified HttpListener, which was then replaced with raw TcpListener due to VS Code port forwarding incompatibility. But .NET ships Kestrel — the most battle-tested HTTP server in the ecosystem. A 3-line `WebApplication.CreateSlimBuilder()` gives you correct HTTP parsing, connection management, keep-alive, chunked encoding, and proxy compatibility for free. Instead, the team hand-rolled an HTTP/1.1 parser that reads one byte at a time and doesn't handle edge cases like oversized headers, pipelined requests, or HTTP/1.0 clients.

**Severity:** Important
**Counter-argument:** Kestrel adds `Microsoft.AspNetCore.App` as a dependency (~30MB framework reference). The preview server serves 3 routes with tiny payloads. The raw TcpListener works and the attack surface is localhost-only. The byte-at-a-time reader is adequate for a local preview tool that handles one connection at a time.
**Verdict:** Accept as-is. The dependency cost outweighs the correctness benefit for a localhost-only development tool. Document the limitation.

---

### 2. [Assumption Surfacing] Playwright for Browser — Heavy Dependency for "Open a URL"

Using Playwright to open a Chromium window is using a 200MB testing framework to do what `Process.Start("xdg-open", url)` does. The `--no-browser` flag was added because Playwright doesn't work in headless environments (devcontainers, SSH, CI). In practice, most developers will use `--no-browser` with their existing browser via port forwarding. So the Playwright browser launch is the default path that most users won't use.

**Severity:** Important
**Counter-argument:** Playwright gives a controlled environment — the Chromium version is known, the window is programmatically managed, and lifecycle is clean (close browser on Ctrl+C). System browser launch has no reliable "close on exit" mechanism. The dependency already exists for Mermaid rendering. But the `--no-browser` evidence suggests the counter-argument is weaker than it appears.
**Verdict:** Accept with note. Consider making `--no-browser` the default in environments where `DISPLAY` is unset or `BROWSER` env var is absent. System browser launch (`xdg-open`/`open`/`start`) could be a lightweight alternative that doesn't require Playwright.

---

### 3. [Cost of Being Wrong] Polling-Based Hot-Reload vs. SSE/WebSocket

The JS snippet polls `/reload` every 200ms. That is 5 requests/second per open tab, forever. For a single user this is fine. But the design does not degrade gracefully: if the server is slow (e.g., large document re-render), polls queue up. If a user opens multiple tabs, polls multiply. Server-Sent Events (SSE) would be zero overhead when idle and instant notification on change — a strictly better protocol for this use case.

**Severity:** Minor
**Counter-argument:** SSE requires keeping a connection open, which complicates the "Connection: close" model chosen for proxy compatibility. The polling cost is negligible for localhost. Multiple tabs are not a realistic scenario for a preview tool.
**Verdict:** Accept as-is. Polling is simpler and adequate. SSE is a valid improvement for a future version if latency matters.

---

### 4. [Inversion] Does the Preview Server Need to Exist at All?

What if `md2 preview` just wrote an HTML file and opened it in the system browser? No server, no watcher, no Playwright. The user hits Ctrl+S in their editor, then Ctrl+R in their browser. This is how most static site generators work. The hot-reload is a convenience, not a necessity.

**Severity:** Minor
**Counter-argument:** The user's stated product context says "fast feedback loop." Manual refresh adds friction. The Sprint 9 retro showed the preview feature was valued. The server + watcher + auto-reload is the differentiator.
**Verdict:** Reject. The fast feedback loop is the feature's reason to exist. File-based preview is `md2 convert --format html`.

---

### 5. [Security] CSS Injection via Theme Values

Theme values are sanitized with regex (`SanitizeFont` strips non-alphanumeric, `SanitizeHex` strips non-hex). But these are custom-built sanitizers, not CSP or a templating engine's auto-escaping. If a new theme property is added without sanitization, it is a CSS injection vector in the preview HTML. The server binds to localhost, so the blast radius is limited, but CSS injection can still exfiltrate data via `url()` if the page has sensitive content.

**Severity:** Minor
**Counter-argument:** All theme values pass through `ResolvedTheme`, which validates at construction time. The HTML is served only to localhost. The sanitization functions are tested. The risk is residual, not active.
**Verdict:** Accept with note. Add a comment in `HtmlPreviewRenderer.GenerateCss` reminding future developers that new theme properties MUST be sanitized before interpolation.

---

## Summary

| # | Challenge | Severity | Verdict |
|---|-----------|----------|---------|
| 1 | TcpListener vs. Kestrel | Important | Accept as-is — dependency cost too high |
| 2 | Playwright for browser launch | Important | Accept — consider system browser alternative |
| 3 | Polling vs. SSE | Minor | Accept as-is — simpler, adequate |
| 4 | Does server need to exist | Minor | Reject — fast feedback is the feature |
| 5 | CSS injection via themes | Minor | Accept with note — add sanitization reminder |

## Honest Assessment

The architecture is pragmatic. The biggest risk — HttpListener proxy incompatibility — was discovered and fixed during implementation, not during design review. This is exactly the kind of issue a pre-implementation Wei debate would have caught: "What happens when this runs behind a reverse proxy?" The retroactive nature of this debate means it validates rather than improves the design, but it documents the trade-offs for future maintainers.
