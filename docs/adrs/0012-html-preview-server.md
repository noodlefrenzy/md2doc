---
agent-notes: { ctx: "ADR for HTML preview with hot-reload via Playwright", deps: [docs/adrs/0008-playwright-mermaid-rendering.md, docs/architecture.md], state: active, last: "archie@2026-03-13" }
---

# ADR-0012: HTML Preview Server with Hot-Reload via Playwright

## Status

Accepted

## Context

md2 converts Markdown to DOCX, but authors need a fast feedback loop while writing. Opening the DOCX in Word after every edit is slow (2-5 seconds per cycle) and disrupts flow. A preview mode that shows a live HTML representation of the document — updating on save — would provide sub-second feedback.

**Options evaluated:**

1. **Embedded HTTP server + Playwright browser** — Serve HTML on localhost, open in Playwright-controlled Chromium, inject CSS/JS for hot-reload via file watcher. Same Playwright dependency already used for Mermaid/Math.

2. **Embedded HTTP server + system browser** — Serve HTML, open in user's default browser, use Server-Sent Events (SSE) or WebSocket for reload. No Playwright dependency for preview, but no control over browser state/position, and SSE/WS adds complexity.

3. **File-based preview** — Write HTML to a temp file, open in system browser. No server needed, but no hot-reload. User must manually refresh.

4. **DOCX preview** — Generate DOCX on each save, open in Word. Authentic output but slow (2-5s per cycle) and requires Word to be installed.

## Decision

Use **Option 1: Embedded HTTP server + Playwright browser**.

**Architecture:**

1. **HttpListener-based server.** A minimal HTTP server on `http://localhost:<port>` serves the rendered HTML. Port is auto-selected to avoid conflicts. No ASP.NET dependency.

2. **HTML renderer.** A new `HtmlPreviewRenderer` converts the transformed Markdig AST to themed HTML. It applies the same `ResolvedTheme` as the DOCX emitter, mapping theme colors/fonts/sizes to CSS. The HTML is a self-contained page with embedded CSS (no external dependencies).

3. **File watcher.** A `FileSystemWatcher` monitors the source `.md` file. On change, the pipeline re-runs (parse → transform → render HTML), and the server pushes the new content.

4. **Hot-reload mechanism.** The HTML page includes a small inline JS snippet that polls the server's `/reload` endpoint. When a change is detected, the page content is replaced without a full page reload, preserving scroll position.

5. **Playwright browser.** A Chromium window opens via Playwright (non-headless) pointing to the server URL. This reuses the existing `BrowserManager` from `Md2.Diagrams`. On Ctrl+C, the browser and server shut down cleanly.

6. **Theme parity.** The HTML renderer uses the same theme cascade as the DOCX path (preset → YAML → CLI overrides). Colors, fonts, and sizing are mapped to CSS custom properties for consistency.

**Project structure:**

- `Md2.Preview` — new project containing `PreviewServer`, `HtmlPreviewRenderer`, `FileWatcher`
- `Md2.Cli/PreviewCommand` — CLI wiring (`md2 preview input.md [--preset] [--theme]`)

**Lifecycle:**

```
md2 preview input.md --preset technical
  → Parse + Transform markdown
  → Render to HTML via HtmlPreviewRenderer
  → Start PreviewServer on localhost:auto-port
  → Open Chromium via Playwright (non-headless)
  → Watch input.md for changes
  → On change: re-render, push to server, browser auto-updates
  → On Ctrl+C: close browser, stop server, exit
```

## Consequences

### Positive

- Sub-second feedback loop for authors (HTML render is ~50ms, no DOCX overhead).
- Reuses existing Playwright/Chromium dependency — no new external dependencies.
- Theme parity ensures preview matches DOCX output styling.
- Self-contained HTML with no external assets — works offline.
- Clean lifecycle: Ctrl+C stops everything.

### Negative

- HTML is an approximation of DOCX output. Some DOCX-specific features (page breaks, headers/footers, exact pagination) cannot be previewed.
- Requires Chromium installed (same as Mermaid). `md2 doctor` already checks for this.
- Adds a new project (`Md2.Preview`) to the solution.

### Neutral

- The `HtmlPreviewRenderer` could later be extracted for use in other contexts (e.g., HTML export, email formatting).
- The polling-based hot-reload is simpler than WebSocket but adds a small latency (~200ms poll interval). Acceptable for a preview tool.
