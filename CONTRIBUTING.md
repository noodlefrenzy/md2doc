# Contributing to md2

Thanks for your interest in contributing! This guide will help you get started.

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Git
- Chromium (installed automatically via Playwright on first use)

### Setup

```bash
# Fork the repo on GitHub first, then:
git clone https://github.com/<your-username>/md2doc.git
cd md2doc
git remote add upstream https://github.com/noodlefrenzy/md2doc.git
dotnet build
dotnet test
```

If tests involving Mermaid diagrams or math equations fail, run the Playwright install:

```bash
pwsh src/Md2.Cli/bin/Debug/net9.0/.playwright/node/install.ps1
```

Or use the built-in doctor command to check your environment:

```bash
dotnet run --project src/Md2.Cli -- doctor
```

## How to Contribute

### Reporting Bugs

Open an issue with:
- Steps to reproduce
- Expected vs actual behavior
- Sample Markdown input (if applicable)
- .NET version (`dotnet --version`)

### Suggesting Features

Open an issue describing:
- The use case / problem you're solving
- Proposed solution (if you have one)
- Example input/output

### Submitting Code

We use a **fork-and-PR** workflow. Outside contributors do not get push access to the main repo.

1. **Fork** the repository on GitHub if you haven't already.
2. **Create a feature branch** from `main`:
   ```bash
   git fetch upstream
   git checkout -b feat/my-change upstream/main
   ```
3. **Write tests** for any new functionality. We use [Shouldly](https://shouldly.org/) for assertions.
4. **Run the full test suite** before submitting:
   ```bash
   dotnet test
   ```
5. **Follow existing code style** — the codebase uses standard C# conventions.
6. **Keep commits focused** — one logical change per commit, using [conventional commits](https://www.conventionalcommits.org/) format:
   ```
   feat: add PPTX table support
   fix: handle empty code blocks without crashing
   docs: clarify theme cascade order
   ```
7. **Push to your fork** and **open a pull request** against `main` with a clear description of what changed and why.

## Project Structure

```
src/
  Md2.Cli/          — CLI entry point (System.CommandLine)
  Md2.Core/         — Pipeline orchestration, transforms, shared types
  Md2.Parsing/      — Markdig configuration and extensions
  Md2.Emit.Docx/    — DOCX emitter (Open XML SDK)
  Md2.Highlight/    — Syntax highlighting (TextMateSharp)
  Md2.Themes/       — YAML theme DSL, cascade resolver, presets
  Md2.Diagrams/     — Mermaid diagram rendering (Playwright)
  Md2.Math/         — LaTeX math to OMML conversion
  Md2.Preview/      — Live HTML preview server with hot-reload
tests/
  Md2.Core.Tests/
  Md2.Parsing.Tests/
  Md2.Emit.Docx.Tests/
  Md2.Themes.Tests/
  Md2.Highlight.Tests/
  Md2.Diagrams.Tests/
  Md2.Math.Tests/
  Md2.Preview.Tests/
  Md2.Integration.Tests/
```

### Architecture

The pipeline flows: **Markdown → Parse (Markdig) → AST Transforms → Theme Resolution → Emit (Open XML)**

Key design decisions are documented in `docs/adrs/`. Read those before making architectural changes.

## Testing

We aim for high test coverage. The test suite currently has 732 tests.

- **Unit tests** live next to the code they test (`tests/Md2.*.Tests/`)
- **Integration tests** cover end-to-end pipeline scenarios (`tests/Md2.Integration.Tests/`)
- Use `dotnet test --filter "FullyQualifiedName~YourTest"` to run specific tests

### Writing Tests

- Use [Shouldly](https://shouldly.org/) for assertions (not `Assert.*`)
- Follow the Arrange-Act-Assert pattern
- Test both happy paths and edge cases
- For DOCX output tests, use the `TestHelper` class to inspect generated documents

## Code Style

- Standard C# naming conventions (PascalCase for public members, camelCase for locals)
- No explicit `this.` unless needed for disambiguation
- Prefer expression-bodied members for simple one-liners
- Use `var` when the type is obvious from the right-hand side

## Pull Request Review

PRs are reviewed for:
- **Correctness** — does it work and handle edge cases?
- **Tests** — are new behaviors tested?
- **Simplicity** — is this the simplest solution that works?
- **Security** — no injection vulnerabilities, safe file handling

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
