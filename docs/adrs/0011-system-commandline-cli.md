---
agent-notes: { ctx: "ADR selecting System.CommandLine for CLI framework", deps: [docs/architecture.md], state: active, last: "archie@2026-03-11" }
---

# ADR-0011: Use System.CommandLine for CLI Framework

## Status

Proposed

## Context

md2 is a CLI tool that needs argument parsing, subcommands (`theme extract`, `theme validate`, `theme list`, `preview`), option handling (repeatable `--style` flags, mutually exclusive options), help generation, tab completion, and middleware for cross-cutting concerns (verbosity, error handling).

**Options evaluated:**

1. **System.CommandLine** (`System.CommandLine` NuGet package) -- Microsoft's official CLI parsing library for .NET. Supports commands, subcommands, options, arguments, middleware, tab completion, help generation, and response files. Actively developed (GA release tracking .NET 9). Used by .NET tooling itself (`dotnet` CLI uses it internally).

2. **Spectre.Console.Cli** -- Part of the Spectre.Console library. Rich terminal UI (tables, progress bars, trees). Strong CLI parsing with command pattern. Well-maintained by Patrik Svensson. More opinionated about command structure.

3. **CliFx** -- Lightweight, attribute-based CLI framework. Clean API. Smaller community than the above two.

4. **McMaster.Extensions.CommandLineUtils** -- Fork of Microsoft's older ASP.NET Core CLI utilities. Mature but less actively developed. Being superseded by System.CommandLine.

5. **Manual parsing.** `args` array parsing with switch statements. No tab completion, no help generation, no middleware. Not practical for a tool with md2's CLI surface area.

## Decision

Use **System.CommandLine** as the CLI framework.

**Rationale:**

- Microsoft-maintained with long-term support trajectory. The `dotnet` CLI itself uses it, which is the strongest possible signal for .NET CLI tooling.
- Native support for subcommands (essential for `md2 theme extract`, `md2 theme validate`, `md2 preview`).
- Built-in tab completion generation (important for a daily-driver CLI -- the product context explicitly calls out "must not feel clunky").
- Middleware pipeline for cross-cutting concerns (timing, verbosity, error wrapping).
- Response file support (`@options.rsp`) for complex invocations.

**Note on Spectre.Console:** We may also use `Spectre.Console` (not `Spectre.Console.Cli`) as a complementary library for terminal output formatting (progress bars for Chromium download, styled error messages, table output for `md2 theme list`). Spectre.Console as an output library is compatible with System.CommandLine as the parsing framework. They serve different purposes.

## Consequences

### Positive

- Full-featured CLI parsing with minimal boilerplate.
- Tab completion improves daily-use ergonomics.
- Subcommand support is first-class.
- Middleware enables clean separation of concerns (e.g., `--verbose` handling, timing, error formatting).
- Microsoft-maintained, aligning with the .NET ecosystem choice.

### Negative

- System.CommandLine has had a long pre-release period. API surface has changed between previews. We must pin to a specific version and be prepared for minor API changes at GA.
- The library's API can be verbose for simple cases (defining `Option<string>`, `Argument<FileInfo>`, handler binding).
- Less rich terminal output than Spectre.Console.Cli. Mitigated by using Spectre.Console for output formatting separately.

### Neutral

- If System.CommandLine's GA release introduces breaking changes, migration cost is bounded to the `Md2.Cli` project only. The core pipeline is CLI-framework-agnostic.
