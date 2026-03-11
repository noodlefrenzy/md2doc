---
agent-notes:
  ctx: "dependency rationale and transitive dep inventory"
  deps: [docs/sbom/sbom.md, docs/architecture.md]
  state: active
  last: "pierrot@2026-03-11"
  key: ["rationale for all 6 direct deps", "YamlDotNet safe deser is security-critical"]
---
# Dependency Decisions

<!-- This file explains WHY each dependency exists. Maintained by Pierrot. -->
<!-- Every top-level dependency must have an entry here before it's accepted. -->

## Top-Level Dependencies

### Markdig

- **Package:** `Markdig`
- **Version:** latest stable (pin at scaffolding)
- **License:** BSD-2-Clause
- **Why we're using it:** Markdig is the de facto standard Markdown parser for .NET. It supports CommonMark, GFM (tables, task lists, strikethrough), and a rich extension API. We need a parser that produces a traversable AST (not just HTML output) because we are generating DOCX, not HTML. Markdig's `MarkdownDocument` AST is rich, extensible, and carries source position information.
- **Alternatives considered:**
  - **Markdownsharp:** Older, less actively maintained, no extension API, no AST (HTML-only output). Rejected.
  - **CommonMark.NET:** CommonMark-only (no GFM). Less extensible. Lower community adoption. Rejected.
  - **Custom parser:** Enormous scope for no benefit. Rejected.
- **Security notes:** No known CVEs. Markdig supports `.DisableHtml()` to prevent raw HTML pass-through, which we should enable in preview mode for defense-in-depth (see threat model E-5). BSD-2-Clause is a permissive license with no copyleft concerns.
- **Added:** 2026-03-11 by Archie (ADR-0003)

### YamlDotNet

- **Package:** `YamlDotNet`
- **Version:** >= 13.0.0 (SECURITY MINIMUM -- see below)
- **License:** MIT
- **Why we're using it:** We need a YAML parser for two purposes: (1) YAML front matter in Markdown files, and (2) the YAML theme DSL (`theme.yaml`). YamlDotNet is the standard .NET YAML library with strong typing support, schema validation capabilities, and safe deserialization defaults in modern versions.
- **Alternatives considered:**
  - **SharpYaml:** Less actively maintained. Smaller community. No clear advantage. Rejected.
  - **System.Text.Json with YAML adapter:** No mature adapter exists. JSON != YAML semantics. Rejected.
  - **Tomlyn (TOML instead of YAML):** TOML is less expressive for our theme DSL (nested structures, variable interpolation). Users expect YAML for configuration. Rejected.
- **SECURITY NOTES (CRITICAL):**
  - Versions < 5.0.0 contain a critical unsafe deserialization vulnerability that allows arbitrary type instantiation and remote code execution via crafted YAML files.
  - We require >= 13.0.0 to ensure modern API defaults and `DeserializerBuilder` safety.
  - Implementation MUST use `new DeserializerBuilder().Build()` and deserialize into strongly-typed records only.
  - Implementation MUST NOT use `Deserialize<object>()`, `WithTagMapping` for untrusted types, or the legacy `Deserializer()` constructor.
  - See threat model entries T-2, E-4, X-2.
- **Added:** 2026-03-11 by Archie (ADR-0009)

### DocumentFormat.OpenXml (Open XML SDK)

- **Package:** `DocumentFormat.OpenXml`
- **Version:** >= 3.0.0
- **License:** MIT
- **Why we're using it:** The Open XML SDK is Microsoft's official library for reading and writing Office Open XML files (DOCX, PPTX, XLSX). We need it to generate DOCX output, extract styles from DOCX templates, and (in v2) generate PPTX. It provides strongly-typed access to the entire OOXML schema with IntelliSense and compile-time safety.
- **Alternatives considered:**
  - **NPOI:** Apache-2.0. Also supports OOXML but through a Java-port API that feels foreign in C#. Less type-safe. Would work but the Open XML SDK is a more natural fit for our use case. Rejected.
  - **DocX (Xceed):** Limited to DOCX (no PPTX). Commercial license for some features. Less control over low-level styling. Rejected.
  - **Raw System.IO.Packaging + XDocument:** Maximum control but enormous implementation effort. We'd be reimplementing the SDK. Rejected.
- **Security notes:** No known CVEs in 3.x. Uses .NET's XML parser with DTD processing disabled by default (XXE safe). The SDK validates OOXML structure on read, which protects against some malformed input. MIT license, Microsoft-backed. See threat model T-1, T-5, X-1.
- **Added:** 2026-03-11 by Archie (ADR-0004)

### Microsoft.Playwright

- **Package:** `Microsoft.Playwright`
- **Version:** latest stable (pin at scaffolding)
- **License:** Apache-2.0
- **Why we're using it:** We need a headless browser to render Mermaid diagrams to PNG images and to power the live preview mode. Playwright for .NET provides a robust, cross-platform headless Chromium integration with screenshot capabilities, page evaluation, and built-in sandboxing.
- **Alternatives considered:**
  - **Puppeteer Sharp:** Community-maintained .NET port of Puppeteer. Less active than Playwright. Microsoft backing gives Playwright better long-term support. Rejected.
  - **Selenium WebDriver:** Heavyweight, designed for testing, not headless rendering. Slower startup. Rejected.
  - **mermaid-cli (mmdc) as external process:** Requires Node.js runtime. Adds a non-.NET dependency. No preview mode support. Rejected for bundling complexity.
  - **mermaid.ink (web service):** Network dependency. No air-gap support. Privacy concerns (diagram content sent to external service). Rejected.
- **Security notes:**
  - CVE-2025-59288 (Medium, CVSS 5.3): Signature verification flaw in Chromium download. Keep updated.
  - CVE-2025-9611: Affects Playwright MCP Server only, not the .NET SDK. Not applicable.
  - Chromium downloads ~300MB on first use. Playwright verifies integrity via checksums.
  - The Chromium process runs in a sandbox. Mermaid rendering pages have no filesystem or network access.
  - See threat model T-4, T-7, E-1.
- **Added:** 2026-03-11 by Archie (ADR-0008)

### TextMateSharp

- **Package:** `TextMateSharp`
- **Version:** latest stable (pin at scaffolding)
- **License:** MIT
- **Why we're using it:** We need syntax highlighting for code blocks in the DOCX output. TextMateSharp is a .NET port of Eclipse's tm4e engine that uses TextMate grammar files (the same grammars used by VS Code). This gives us syntax highlighting for 100+ languages without maintaining per-language regex patterns.
- **Alternatives considered:**
  - **ColorCode (a .NET syntax highlighter):** Supports fewer languages. HTML-output focused (we need styled token runs, not HTML). Rejected.
  - **Roslyn-based highlighting:** Only works for C#/VB. We need polyglot support. Rejected (but could supplement TextMateSharp for C# if needed).
  - **Prism.js via Playwright:** Would require running Chromium for every code block. Extremely slow. Rejected.
  - **No syntax highlighting:** Not acceptable for the product vision. Users expect it.
- **Security notes:** No known CVEs. MIT license. Uses Onigwrap (oniguruma regex native binding) which could theoretically be vulnerable to pathological regex input. Mitigated by per-block tokenization timeout (see threat model D-5). Bus factor = 1 (single maintainer). Fallback plan documented in SBOM.
- **Added:** 2026-03-11 by Archie (ADR-0007)

### System.CommandLine

- **Package:** `System.CommandLine`
- **Version:** >= 2.0.0
- **License:** MIT
- **Why we're using it:** CLI argument parsing, help generation, tab completion, and middleware. This is Microsoft's official .NET CLI library, designed for the exact use case we have: a CLI tool with subcommands, options, and arguments.
- **Alternatives considered:**
  - **CommandLineParser:** Popular community library. Less feature-rich than System.CommandLine (no middleware, weaker completion support). Rejected.
  - **Spectre.Console.Cli:** Beautiful output but overkill for our needs. We don't need rich terminal UI, just robust argument parsing. Could reconsider if we add interactive features. Rejected.
  - **Raw `string[] args` parsing:** Unmaintainable for a CLI with this many options. Rejected.
- **Security notes:** No known CVEs. MIT license. Microsoft-backed. CLI arguments come from the local user, so argument parsing is not a trust boundary concern.
- **Added:** 2026-03-11 by Archie (ADR-0011)

---

## Transitive Dependencies

Full transitive tree to be generated via `dotnet list package --include-transitive` after project scaffolding.

**Known significant transitive dependencies:**

| Package | Version | License | Pulled in by | Notes |
|---------|---------|---------|--------------|-------|
| DocumentFormat.OpenXml.Framework | >= 3.0.0 | MIT | DocumentFormat.OpenXml | Core framework package. Same maintainer (Microsoft). |
| System.IO.Packaging | varies | MIT | DocumentFormat.OpenXml | OPC (ZIP) package handling. Microsoft-maintained. |
| Onigwrap | varies | MIT | TextMateSharp | Native oniguruma regex bindings. Platform-specific. |
| TextMateSharp.Grammars | varies | MIT | TextMateSharp (optional) | Bundled TextMate grammar files. May be pulled in separately. |

### License Flags

Any transitive dependency with a **copyleft license** (GPL, LGPL, AGPL, MPL) or an **uncommon/unknown license** is flagged here for Pierrot's review:

| Package | License | Pulled in by | Risk assessment |
|---------|---------|--------------|-----------------|
| (none flagged) | | | Audit after scaffolding. Run `dotnet nuget-license` or equivalent to scan full tree. |

**Action item:** After project scaffolding, run a full license scan of the transitive dependency tree and update this table. Use `dotnet-project-licenses` tool or NuGet License Report.
