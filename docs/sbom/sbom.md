---
agent-notes:
  ctx: "software bill of materials, all dependencies"
  deps: [docs/sbom/dependency-decisions.md, docs/architecture.md]
  state: active
  last: "pierrot@2026-03-11"
  key: ["all permissive licenses", "YamlDotNet >= 13.0.0 required", "Playwright CVE-2025-59288 tracked"]
---
# Software Bill of Materials (SBOM)

<!-- This file is maintained by Pierrot. Update it whenever dependencies change. -->
<!-- Sato must notify Pierrot after adding, removing, or upgrading any dependency. -->

**Project:** md2 -- Markdown to DOCX/PPTX converter CLI
**Last updated:** 2026-03-11
**Package manager:** NuGet
**Lock file:** `packages.lock.json` (enable with `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in Directory.Build.props)

## Direct Dependencies (Runtime)

| Package | Min Version | License | Project | Purpose |
|---------|-------------|---------|---------|---------|
| Markdig | latest stable | BSD-2-Clause | Md2.Parsing | Markdown parsing (CommonMark + GFM + extensions) |
| YamlDotNet | >= 13.0.0 | MIT | Md2.Parsing, Md2.Themes | YAML front matter and theme parsing |
| DocumentFormat.OpenXml | >= 3.0.0 | MIT | Md2.Emit.Docx, Md2.Emit.Pptx | DOCX/PPTX generation via Open XML |
| Microsoft.Playwright | latest stable | Apache-2.0 | Md2.Diagrams, Md2.Preview | Mermaid rendering and live preview (headless Chromium) |
| TextMateSharp | latest stable | MIT | Md2.Highlight | Syntax highlighting via TextMate grammars |
| System.CommandLine | >= 2.0.0 | MIT | Md2.Cli | CLI argument parsing, help generation |

**Note on versions:** Exact pinned versions will be recorded here once the projects are scaffolded and packages are installed. "latest stable" indicates the package should be pinned to whatever stable version is current at scaffolding time.

**SECURITY CONSTRAINT:** YamlDotNet MUST be >= 13.0.0. Versions prior to 5.0.0 contain a critical unsafe deserialization vulnerability. We require 13.0.0+ for modern API safety defaults. See threat model E-4.

## Dev Dependencies

| Package | Version | License | Purpose |
|---------|---------|---------|---------|
| xunit | latest stable | Apache-2.0 | Test framework |
| xunit.runner.visualstudio | latest stable | Apache-2.0 | VS Test integration |
| FluentAssertions | latest stable | Apache-2.0 | Fluent test assertions |
| Microsoft.NET.Test.Sdk | latest stable | MIT | Test SDK |
| coverlet.collector | latest stable | MIT | Code coverage |

## Transitive Dependencies of Note

Full transitive tree to be generated via `dotnet list package --include-transitive` after project scaffolding. Key transitive dependencies to track:

| Package | License | Pulled in by | Notes |
|---------|---------|--------------|-------|
| DocumentFormat.OpenXml.Framework | MIT | DocumentFormat.OpenXml | Core framework, same maintainer |
| Onigwrap (native) | MIT | TextMateSharp | Oniguruma regex engine native binding |
| System.IO.Packaging | MIT | DocumentFormat.OpenXml | ZIP/OPC package handling |

See [dependency-decisions.md](dependency-decisions.md#transitive-dependencies) for full transitive inventory.

## License Summary

| License | Count (Direct) | Notes |
|---------|---------------|-------|
| MIT | 4 | YamlDotNet, DocumentFormat.OpenXml, TextMateSharp, System.CommandLine |
| BSD-2-Clause | 1 | Markdig |
| Apache-2.0 | 1 | Microsoft.Playwright |

**All direct dependencies use permissive licenses.** No copyleft (GPL, LGPL, AGPL, MPL) concerns in the direct dependency tree. Transitive tree to be audited after scaffolding.

## Known Vulnerabilities

| Package | CVE | Severity | Affects us? | Status | Notes |
|---------|-----|----------|-------------|--------|-------|
| Microsoft.Playwright | CVE-2025-59288 | Medium (CVSS 5.3) | Yes | Monitor | Signature verification flaw in Chromium download. Keep Playwright updated. |
| Microsoft.Playwright | CVE-2025-9611 | Medium | No | N/A | Affects Playwright MCP Server only, not the .NET SDK we use. |
| YamlDotNet (< 5.0.0) | Historical | Critical | No (we require >= 13.0.0) | Mitigated | Unsafe deserialization in legacy versions. Enforced by version floor. |
| DocumentFormat.OpenXml (2.9.1) | CVE-2023-21538 | Varies | No (we require >= 3.0.0) | Mitigated | Transitive dep vulnerability in old versions. We use 3.x+. |

## Dependency Health

| Package | Maintainer | Org-backed? | Last release | Downloads | Bus factor | Health |
|---------|-----------|------------|--------------|-----------|------------|--------|
| Markdig | xoofx (Alexandre Mutel) | No | Active | Very high | 1 (but widely forked) | Healthy |
| YamlDotNet | aaubry (Antoine Aubry) | No | Active | Very high | 1 | Healthy |
| DocumentFormat.OpenXml | Microsoft (dotnet org) | Yes | Active | Very high | Org | Healthy |
| Microsoft.Playwright | Microsoft | Yes | Active | High | Org | Healthy |
| TextMateSharp | danipen (Daniel Perez) | No | Active | Moderate | 1 | Monitor |
| System.CommandLine | Microsoft (dotnet org) | Yes | Active | Very high | Org | Healthy |

**Flags:**
- **TextMateSharp** has a bus factor of 1. If danipen becomes unavailable, we would need to fork or find an alternative syntax highlighting solution. The package is a port of Eclipse's tm4e, so the upstream logic is well-understood. Fallback plan: fork and maintain, or switch to a Roslyn-based highlighter for C# and a simpler regex-based highlighter for other languages.
- **Markdig** also has a bus factor of 1, but Alexandre Mutel is a prolific .NET OSS maintainer with multiple widely-used packages. The project has significant community investment. Risk is lower than TextMateSharp.
