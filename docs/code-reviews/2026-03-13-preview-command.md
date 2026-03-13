---
agent-notes:
  ctx: "Review of CLI preview command with hot-reload"
  deps: [src/Md2.Cli/PreviewCommand.cs, src/Md2.Cli/Program.cs, tests/Md2.Integration.Tests/PreviewCommandTests.cs]
  state: active
  last: "code-reviewer@2026-03-13"
---
# Code Review: PreviewCommand (CLI preview with hot-reload)

**Date:** 2026-03-13
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:** `src/Md2.Cli/PreviewCommand.cs`, `src/Md2.Cli/Program.cs`, `src/Md2.Cli/Md2.Cli.csproj`, `tests/Md2.Integration.Tests/PreviewCommandTests.cs`
**Verdict:** Changes requested

## Context

Issue #52 adds an `md2 preview <input>` subcommand that opens a live HTML preview in Chromium with file-watching hot-reload and theme support (`--preset`, `--theme`, `--style`). The command wires `PreviewSession` from `Md2.Preview` into the CLI. This review compares the new command against the established patterns in `ConvertCommand`.

## Findings

### Critical

No critical findings.

### Important

**1. Missing "Unknown preset" error handling**

`PreviewCommand.ExecuteAsync` calls `ThemeCascadeResolver.Resolve(cascadeInput)` at line 85 without catching the `ArgumentException` thrown by `PresetRegistry.Get()` when a preset name is not found. `ConvertCommand` handles this at line 278:

```csharp
catch (ArgumentException ex) when (ex.Message.Contains("Unknown preset"))
{
    await Console.Error.WriteLineAsync($"Error: {ex.Message.Split(...)[0]}");
    return 2;
}
```

Without this, `md2 preview foo.md --preset bogus` produces the raw exception message including the `(Parameter 'name')` suffix from `ArgumentException`, which is not user-friendly.

**Fix:** Add the same catch clause before the generic `catch (Exception)`.

**2. Missing `--verbose`/`--debug`/`--quiet` flags**

`ConvertCommand` supports three verbosity flags that control structured logging and expose diagnostic output (cascade trace, full exception stacks with `--debug`). `PreviewCommand` has none. The generic error handler at line 142 shows only `ex.Message` with no option to get the full stack trace. Users debugging Playwright launch failures or theme issues have no diagnostic path.

**Fix:** Add at minimum `--verbose` and `--debug` flags. In the error handler, include `ex.ToString()` when debug mode is active, and add a hint ("Run with --debug for full diagnostics.") otherwise -- matching the ConvertCommand pattern.

**3. Missing `OperationCanceledException` handler**

`ConvertCommand` explicitly catches `OperationCanceledException` and returns exit code 1 with "Operation cancelled." `PreviewCommand` does not. While `PreviewSession.RunAsync` handles cancellation internally, cancellation during the setup phase (theme resolution, Playwright initialization at line 85-90) would fall through to the generic `catch (Exception)` and produce a confusing "A task was canceled" message.

**Fix:** Add `catch (OperationCanceledException)` between the `Md2Exception` and generic `Exception` catch clauses.

**4. Test coverage gaps**

The three integration tests cover error paths (missing file, missing theme) and command metadata. Missing:

- **Cancellation test:** Start the preview command with a `CancellationTokenSource` that cancels after a short delay. Verifies clean shutdown without Playwright (which will throw on launch in CI -- that exception path is worth testing too).
- **Invalid preset test:** `md2 preview foo.md --preset nonexistent` should return exit code 2 (currently broken per finding #1).
- **Invalid style override test:** `md2 preview foo.md --style bad` should return exit code 2.

The happy path (Playwright opens browser) is hard to test in CI without a display, but the validation and cancellation paths are fully testable.

### Suggestions

**5. Theme validation code duplication**

Lines 73-113 of `PreviewCommand.ExecuteAsync` are nearly identical to lines 218-271 of `ConvertCommand.ExecuteAsync` -- theme file parsing, validation, style override parsing, and error formatting. This is a candidate for extraction into a shared helper (e.g., `ThemeHelper.ResolveFromCliOptions(preset, themeFile, styles)`). Not blocking, but worth addressing before a third command copies the same block.

### Clean

**Pierrot:** No security or compliance concerns. The preview server binds to localhost only. Theme file paths are validated for existence. `HtmlPreviewRenderer` sanitizes font names and hex colors before CSS interpolation. No secrets, credentials, or user-input injection risks in the CLI wiring layer.

## Lessons

1. **Pattern consistency across commands matters.** When a codebase establishes patterns (error handling for unknown presets, verbosity flags, cancellation handling), new commands must follow them. A checklist of "what does the existing command handle?" before writing a new one prevents these gaps. The fastest way to review a new command is to diff it structurally against the existing one.

2. **Error handling is not just about catching exceptions -- it is about message quality.** The difference between `catch (ArgumentException)` with message cleanup and a generic `catch (Exception)` is the difference between "Unknown preset 'bogus'. Available presets: default, academic, corporate" and "Unknown preset 'bogus'. (Parameter 'name')". The user-facing message is the product.

3. **Cancellation has a surface area larger than the main loop.** It is tempting to think "the session handles Ctrl+C" and skip `OperationCanceledException` in the CLI layer. But cancellation can fire at any point -- during theme resolution, during Playwright initialization, during file reads. Every async method between `GetCancellationToken()` and the session's internal handler is a potential cancellation site.

4. **Test the setup path, not just the steady state.** When the happy path requires external infrastructure (browser, display server), test the next most valuable thing: the validation and error paths that run before the infrastructure is needed. These paths are cheap to test and high-value because they are what users see when something is misconfigured.

5. **Duplicated validation blocks are a refactoring signal.** Two commands with identical 40-line theme validation sequences is manageable. Three is a maintenance burden. The right time to extract is now, while the pattern is fresh and the duplication count is low.
