---
agent-notes:
  ctx: "README update for preview, doctor, theme commands"
  deps: [README.md, src/Md2.Cli/ConvertCommand.cs, src/Md2.Cli/PreviewCommand.cs, src/Md2.Cli/DoctorCommand.cs, src/Md2.Cli/ThemeListCommand.cs, src/Md2.Cli/ThemeValidateCommand.cs, src/Md2.Cli/ThemeResolveCommand.cs, src/Md2.Cli/ThemeExtractCommand.cs]
  state: active
  last: "code-reviewer@2026-03-14"
---
# Code Review: README CLI documentation update (issue #88)

**Date:** 2026-03-14
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:** README.md, src/Md2.Cli/ConvertCommand.cs, src/Md2.Cli/PreviewCommand.cs, src/Md2.Cli/DoctorCommand.cs, src/Md2.Cli/ThemeListCommand.cs, src/Md2.Cli/ThemeValidateCommand.cs, src/Md2.Cli/ThemeResolveCommand.cs, src/Md2.Cli/ThemeExtractCommand.cs, src/Md2.Cli/Program.cs
**Verdict:** Changes requested

## Context

Issue #88 adds missing CLI documentation to the README: new feature bullet points (live preview, TOC, cover pages), missing convert options (`--template`, `--toc`, `--toc-depth`, `--cover`), and full documentation for the `preview`, `doctor`, and `theme` subcommands. Also adds `Md2.Preview` and `Md2.Preview.Tests` to the project structure section.

## Findings

### Critical

**1. All subcommand invocations include a spurious `input.md` before the subcommand name**

Every documented subcommand example includes `input.md` before the subcommand, e.g.:

```
dotnet run --project src/Md2.Cli -- input.md preview input.md
dotnet run --project src/Md2.Cli -- input.md doctor
dotnet run --project src/Md2.Cli -- input.md theme list
```

Looking at `Program.cs` (lines 6-16), `preview`, `doctor`, and `theme` are subcommands on the root command. The root command defines `input` as a required positional argument with `ExactlyOne` arity. In System.CommandLine, when a subcommand is present, the parser routes to the subcommand handler -- the `input.md` before the subcommand name is either parsed as the root command's input argument (which the subcommand handler never reads) or causes ambiguous parsing.

The correct invocations are:

```bash
# Preview
dotnet run --project src/Md2.Cli -- preview input.md

# Doctor
dotnet run --project src/Md2.Cli -- doctor

# Theme commands
dotnet run --project src/Md2.Cli -- theme list
dotnet run --project src/Md2.Cli -- theme resolve --preset modern
dotnet run --project src/Md2.Cli -- theme validate mytheme.yaml
dotnet run --project src/Md2.Cli -- theme extract corporate.docx -o extracted.yaml
```

This affects README.md lines 83, 86, 89, 97, 104, 107, 110, 113, 116. Every subcommand example needs correction.

**Why it matters:** Users copying these examples verbatim will get unexpected behavior or parse errors. README examples are the most-copied text in any project.

### Important

None.

### Suggestions

**1. Preview command has `--theme` and `--style` options not shown in examples**

The `PreviewCommand.cs` source (lines 28-38) supports `--theme` and `--style` options. The README only shows `--preset`. Consider adding at least one example with `--theme` for consistency with the convert command section.

**2. The old `theme resolve` example was removed from the convert section but not noted**

The diff removes the "Inspect theme cascade resolution" example from the convert section (which was the correct location for `theme resolve`). This is fine since it moved to the new Theme Commands section, but the old version actually had the correct invocation syntax (no spurious `input.md`).

### Clean

- **Tara:** No test impact -- this is a docs-only change. No test files were modified or need modification.
- **Pierrot:** No security or compliance concerns in this change. No secrets, credentials, or sensitive data exposed.
- **Ines (operational):** Not activated -- docs-only change with no application behavior modification.

## Lessons

1. **Verify documented CLI invocations against the actual command registration.** When a CLI uses subcommands, the command hierarchy determines valid syntax. The source of truth is `Program.cs` (where commands are composed) plus each command's argument/option definitions. Copy-pasting and modifying examples without testing them is a common source of incorrect documentation.

2. **System.CommandLine subcommand parsing.** In System.CommandLine, when a root command has both a positional argument and subcommands, the parser distinguishes between them by name. Placing a value for the root argument *before* a subcommand name creates ambiguity. Subcommands should appear immediately after `--` (or after any global options), with their own arguments following.

3. **README examples are high-leverage text.** More people will read and copy README examples than any other documentation. Incorrect examples cause disproportionate user friction. When documenting CLI tools, run every example at least once before committing.
