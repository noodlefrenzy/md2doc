---
agent-notes: { ctx: "ADR for Mermaid rendering via Playwright for .NET", deps: [docs/architecture.md], state: active, last: "archie@2026-03-11", key: ["benchmark gate: 10 diagrams < 15s", "Linux glibc compat is real risk"] }
---

# ADR-0008: Use Playwright for .NET for Mermaid Diagram Rendering

## Status

Proposed

## Context

md2 must render Mermaid diagram code blocks (` ```mermaid `) into high-resolution images embedded in the output DOCX. Mermaid is a JavaScript library that renders diagrams in a browser environment. We need a way to execute Mermaid JS from a C#/.NET process.

**Options evaluated:**

1. **Playwright for .NET** (`Microsoft.Playwright`) -- Microsoft-maintained .NET bindings for the Playwright browser automation framework. Launches a Chromium instance, loads an HTML page with Mermaid JS, screenshots the rendered diagram. Supports headless mode. Cross-platform. Downloads Chromium separately (~300MB). MIT-licensed.

2. **PuppeteerSharp** -- Community .NET port of Puppeteer. Similar approach to Playwright. Less active maintenance than Playwright. Chromium-only (no Firefox/WebKit). Would work but offers no advantage over the Microsoft-backed option.

3. **mermaid-cli (mmdc)** -- Official Mermaid CLI tool. Requires Node.js installed. Invoked as a subprocess. Adds a Node.js runtime dependency, which violates the "no second runtime" philosophy.

4. **Mermaid Ink API** -- Online rendering service. Sends diagram text to a remote server. Violates the local-only, air-gappable constraint.

5. **Kroki** -- Self-hosted diagram rendering service. Requires running a Docker container or server. Overcomplicated for a CLI tool.

## Decision

Use **Playwright for .NET** for Mermaid rendering. This was already decided during discovery (see `docs/tracking/2026-03-11-md2doc-discovery.md`). This ADR documents the architectural integration.

**Architecture:**

1. **Opt-in.** Mermaid rendering is disabled unless code blocks with ` ```mermaid ` are detected in the document (or explicitly enabled). No Chromium download occurs until first use.
2. **One-time setup.** On first Mermaid render, if Chromium is not installed, md2 runs `playwright install chromium` with a progress indicator and user-facing message explaining the download.
3. **Browser lifecycle.** A single Chromium instance is launched per md2 invocation and reused across all Mermaid diagrams in the document. The browser is shut down when the pipeline completes.
4. **Rendering process:**
   a. Create a minimal HTML page that loads Mermaid JS (bundled with md2, not fetched from CDN).
   b. Inject the Mermaid diagram definition.
   c. Wait for rendering completion.
   d. Screenshot the rendered SVG at 2-3x DPI scale for high-resolution output.
   e. Save as PNG to a temp directory.
5. **Caching.** Rendered PNGs are cached by content hash (SHA256 of the Mermaid source). If the same diagram appears in multiple files (multi-file concatenation), it is rendered once.
6. **AST integration.** The `MermaidDiagramRenderer` transform replaces mermaid `FencedCodeBlock` nodes with image reference nodes, attaching the PNG path via `SetData()`.
7. **Parallelization.** Multiple Mermaid diagrams can be rendered in parallel using separate Playwright page contexts within the same browser instance.

**Mermaid JS bundling:** The Mermaid JS library (a single ~2MB minified file) is embedded as a resource in the `Md2.Diagrams` assembly. This ensures air-gap compatibility after the initial Chromium download.

## Consequences

### Positive

- High-quality diagram rendering using the official Mermaid JS library.
- Microsoft-maintained Playwright ensures long-term support and cross-platform coverage.
- No Node.js dependency. Chromium is the only external binary.
- Air-gappable after initial Chromium download. Mermaid JS is bundled.
- Reusable for other browser-based rendering needs (KaTeX math fallback, preview mode).

### Negative

- **Large dependency.** Chromium download is ~300MB. This is significant for air-gapped deployments where the binary must be transferred manually.
- **Cold-start latency.** First Mermaid render in a session incurs browser launch time (~1-2 seconds). Subsequent diagrams are fast.
- **Playwright version pinning.** Playwright pins to specific Chromium versions. Upgrading Playwright may require re-downloading Chromium.
- **Process management complexity.** Must handle browser process cleanup on crash, timeout, and cancellation.
- **Testing complexity.** Mermaid rendering tests require Chromium installed in CI. Must be gated behind an environment flag or test category.

### Neutral

- The same Playwright instance can serve both Mermaid rendering and the `md2 preview` command, though these are separate use cases with different lifecycles.
- PNG output at 2-3x DPI produces large images for complex diagrams. We may need to add PNG optimization in the future.
