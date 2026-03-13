---
agent-notes:
  ctx: "software bill of materials, all dependencies"
  deps: [docs/sbom/dependency-decisions.md, docs/architecture.md]
  state: active
  last: "pierrot@2026-03-13"
  key: ["all permissive licenses", "YamlDotNet >= 13.0.0 required", "Playwright CVE-2025-59288 tracked"]
---
# Software Bill of Materials (SBOM)

<!-- This file is maintained by Pierrot. Update it whenever dependencies change. -->
<!-- Sato must notify Pierrot after adding, removing, or upgrading any dependency. -->

**Project:** md2 -- Markdown to DOCX/PPTX converter CLI
**Last updated:** 2026-03-13
**Package manager:** NuGet
**Lock file:** `packages.lock.json` (enable with `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in Directory.Build.props)

## Direct Dependencies (Runtime)

| Package | Version | License | Project | Purpose |
|---------|---------|---------|---------|---------|
| Markdig | 1.1.1 | BSD-2-Clause | Md2.Parsing, Md2.Core | Markdown parsing (CommonMark + GFM + extensions) |
| YamlDotNet | 16.3.0 | MIT | Md2.Parsing, Md2.Core, Md2.Themes | YAML front matter and theme parsing |
| DocumentFormat.OpenXml | 3.4.1 | MIT | Md2.Emit.Docx, Md2.Themes | DOCX generation via Open XML |
| Microsoft.Playwright | 1.58.0 | Apache-2.0 | Md2.Diagrams, Md2.Preview | Mermaid rendering and live preview (headless Chromium 1208) |
| TextMateSharp | 2.0.3 | MIT | Md2.Highlight | Syntax highlighting engine |
| TextMateSharp.Grammars | 2.0.3 | MIT | Md2.Highlight | Bundled TextMate grammar files |
| System.CommandLine | 2.0.5 | MIT | Md2.Cli | CLI argument parsing, help generation |
| Microsoft.Extensions.Logging | 9.0.0 | MIT | Md2.Cli, Md2.Core | Structured logging framework |
| Microsoft.Extensions.Logging.Abstractions | 9.0.0 | MIT | Md2.Cli, Md2.Core | Logging abstractions (ILogger) |
| Microsoft.Extensions.Logging.Console | 9.0.0 | MIT | Md2.Cli | Console log provider for --verbose/--debug |

**SECURITY CONSTRAINT:** YamlDotNet MUST be >= 13.0.0. Versions prior to 5.0.0 contain a critical unsafe deserialization vulnerability. We require 13.0.0+ for modern API safety defaults. See threat model E-4.

## Dev Dependencies

| Package | Version | License | Purpose |
|---------|---------|---------|---------|
| xunit | 2.9.2 | Apache-2.0 | Test framework |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 | VS Test integration |
| Microsoft.NET.Test.Sdk | 17.12.0 | MIT | Test SDK |
| coverlet.collector | 6.0.2 | MIT | Code coverage |
| Shouldly | 4.3.0 | BSD-2-Clause | Fluent test assertions |

## Transitive Dependencies of Note

| Package | License | Pulled in by | Notes |
|---------|---------|--------------|-------|
| DocumentFormat.OpenXml.Framework | MIT | DocumentFormat.OpenXml | Core framework, same maintainer |
| Onigwrap (native) | MIT | TextMateSharp | Oniguruma regex engine native binding |
| System.IO.Packaging | MIT | DocumentFormat.OpenXml | ZIP/OPC package handling |

See [dependency-decisions.md](dependency-decisions.md#transitive-dependencies) for full transitive inventory.

## License Summary

| License | Count (Direct) | Packages |
|---------|---------------|----------|
| MIT | 7 | YamlDotNet, DocumentFormat.OpenXml, TextMateSharp, TextMateSharp.Grammars, System.CommandLine, Microsoft.Extensions.Logging.* |
| BSD-2-Clause | 1 | Markdig |
| Apache-2.0 | 1 | Microsoft.Playwright |

**All direct dependencies use permissive licenses.** No copyleft (GPL, LGPL, AGPL, MPL) concerns in the direct or transitive dependency tree.

## Known Vulnerabilities

| Package | CVE | Severity | Affects us? | Status | Notes |
|---------|-----|----------|-------------|--------|-------|
| Microsoft.Playwright | CVE-2025-59288 | Medium (CVSS 5.3) | Mitigated | Fixed | Signature verification flaw in Chromium download. Fixed in Playwright 1.58.0 (Chromium 1208). |
| Microsoft.Playwright | CVE-2025-9611 | Medium | No | N/A | Affects Playwright MCP Server only, not the .NET SDK we use. |
| YamlDotNet (< 5.0.0) | Historical | Critical | No (we require >= 13.0.0) | Mitigated | Unsafe deserialization in legacy versions. Enforced by version floor. |
| DocumentFormat.OpenXml (2.9.1) | CVE-2023-21538 | Varies | No (we use 3.4.1) | Mitigated | Vulnerability in old versions only. |

**Last NuGet vulnerability scan:** 2026-03-13 — no active CVEs affect current versions.

## Dependency Health

| Package | Version | Maintainer | Org-backed? | Last release | Bus factor | Health |
|---------|---------|-----------|------------|--------------|------------|--------|
| Markdig | 1.1.1 | xoofx (Alexandre Mutel) | No | Active | 1 (widely forked) | Healthy |
| YamlDotNet | 16.3.0 | aaubry (Antoine Aubry) | No | Active | 1 | Healthy |
| DocumentFormat.OpenXml | 3.4.1 | Microsoft (dotnet org) | Yes | Active | Org | Healthy |
| Microsoft.Playwright | 1.58.0 | Microsoft | Yes | Active | Org | Healthy |
| TextMateSharp | 2.0.3 | danipen (Daniel Perez) | No | Active | 1 | Monitor |
| TextMateSharp.Grammars | 2.0.3 | danipen (Daniel Perez) | No | Active | 1 | Monitor |
| System.CommandLine | 2.0.5 | Microsoft (dotnet org) | Yes | Stable (was beta) | Org | Healthy |

**Flags:**
- **TextMateSharp** has a bus factor of 1. If danipen becomes unavailable, we would need to fork or find an alternative syntax highlighting solution. The package is a port of Eclipse's tm4e, so the upstream logic is well-understood. Fallback plan: fork and maintain, or switch to a Roslyn-based highlighter for C# and a simpler regex-based highlighter for other languages.
- **Markdig** also has a bus factor of 1, but Alexandre Mutel is a prolific .NET OSS maintainer with multiple widely-used packages. Risk is lower than TextMateSharp.
- **System.CommandLine** has graduated from beta to stable (2.0.5) — no longer a pre-release dependency risk.

## Version History

| Date | Change | By |
|------|--------|----|
| 2026-03-13 | Sprint 11: Upgraded Markdig 0.38→1.1.1, System.CommandLine beta4→2.0.5, Playwright 1.52→1.58, TextMateSharp.Grammars 1.0.69→2.0.3, OpenXml 3.2→3.4.1. Added logging deps. Updated CVE table. | Pierrot |
| 2026-03-11 | Initial SBOM with all direct dependencies and rationale | Pierrot |
