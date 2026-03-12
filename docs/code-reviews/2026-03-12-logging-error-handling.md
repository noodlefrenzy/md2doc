---
agent-notes:
  ctx: "Review of #71 logging and #72 error handling"
  deps: [ConversionPipeline.cs, Md2Exception.cs, Md2ConversionException.cs, FrontMatterParseException.cs, ConvertCommand.cs, ImageBuilder.cs, LoggingTests.cs, ErrorHandlingTests.cs]
  state: active
  last: "code-reviewer@2026-03-12"
---
# Code Review: Logging (#71) and Error Handling (#72)

**Date:** 2026-03-12
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `src/Md2.Core/Pipeline/ConversionPipeline.cs`
- `src/Md2.Core/Exceptions/Md2Exception.cs`
- `src/Md2.Core/Exceptions/Md2ConversionException.cs`
- `src/Md2.Core/Parsing/FrontMatterParseException.cs`
- `src/Md2.Cli/ConvertCommand.cs`
- `src/Md2.Emit.Docx/ImageBuilder.cs`
- `tests/Md2.Core.Tests/Pipeline/LoggingTests.cs`
- `tests/Md2.Core.Tests/ErrorHandlingTests.cs`
**Verdict:** Approved with suggestions

## Context

Sprint 5 issues #71 and #72 add structured logging via `Microsoft.Extensions.Logging` and a custom exception hierarchy (`Md2Exception` with `UserMessage` property) to separate user-facing error messages from developer diagnostics. The CLI gains a `--debug` flag for full stack traces, and `--verbose` now routes through `ILogger` instead of ad-hoc `Console.Error.WriteLine`. The `ImageBuilder` had a layering violation (direct console write in a library) that was cleaned up.

## Findings

### Critical

None.

### Important

**I1. `Md2ConversionException` is defined but never thrown anywhere.** (`src/Md2.Core/Exceptions/Md2ConversionException.cs`)

The exception type exists and is tested for construction, but no code in the pipeline or emitter actually throws it. This means the `Md2Exception` catch block in `ConvertCommand.cs` will only ever catch `FrontMatterParseException`. Errors during transform or emit (e.g., a visitor encountering an unexpected node, `IOException` on the output stream) will still fall through to the generic `catch (Exception)` block, showing the unhelpful "An unexpected error occurred" message rather than a targeted user message.

This should be addressed by wrapping known failure points in the pipeline -- particularly in `ConversionPipeline.Emit` and the transform loop -- with `try/catch` blocks that throw `Md2ConversionException` with actionable `UserMessage` values. Without this, half the error handling story is plumbing with no payload.

**I2. `--verbose` and `--debug` are not mutually exclusive and `--debug` silently wins.** (`src/Md2.Cli/ConvertCommand.cs`, line 86)

The ternary `debug ? Debug : verbose ? Information : Warning` means `--verbose --debug` silently uses Debug level, and `--verbose` alone sets Information. This is fine behavior, but it should be documented in the help text or one should imply the other. More importantly, the `--verbose` flag description still says "Enable verbose output" without explaining that `--debug` is the superset. Users who discover `--verbose` first may never find `--debug`.

Consider: make `--debug` imply `--verbose` explicitly in the help text, or add a note like "Use --debug for full diagnostics including stack traces."

**I3. Silent exception swallowing in `ImageBuilder.GetImageDimensions`.** (`src/Md2.Emit.Docx/ImageBuilder.cs`, line 167-171)

The old code had a `Console.Error.WriteLine` that was correctly identified as a layering violation and removed. But the replacement is a bare `catch (Exception)` with only a comment. Now when image dimension reading fails (corrupt file, permission denied, etc.), the user gets no indication at all -- the image silently renders at default 6x4 inches. This is a regression in observability.

Now that `ILogger` is available in the pipeline, the right fix is to pass a logger into `ImageBuilder` (or have the caller log) so this becomes a `LogWarning` call. The user should know when their image dimensions could not be read.

### Suggestions

**S1. `FrontMatterParseException` does not set a user-friendly `UserMessage`.** (`src/Md2.Core/Parsing/FrontMatterParseException.cs`, line 10)

The constructor calls `base(message)` which sets `UserMessage = message`. The message passed from `FrontMatterExtractor` is `"Failed to parse YAML front matter: {yamlException.Message}"`. YamlDotNet messages can be cryptic. Consider adding a `userMessage` parameter like: `"The YAML front matter in your document has a syntax error (line {lineNumber}). Check for missing colons, incorrect indentation, or unquoted special characters."` This is the whole point of the `UserMessage` pattern -- make it actionable for non-developers.

**S2. No Debug-level logging in the pipeline.** (`src/Md2.Core/Pipeline/ConversionPipeline.cs`)

All pipeline log statements are at `LogInformation`. The `--debug` flag sets the minimum to `LogLevel.Debug`, but there is nothing logged at Debug level. Consider adding Debug-level entries for things like: number of transforms registered, markdown length after transform, theme values being used, emitter configuration. This would make `--debug` actually more useful than `--verbose`.

**S3. `ListLoggerProvider.Entries` is not thread-safe.** (`tests/Md2.Core.Tests/Pipeline/LoggingTests.cs`, line 117)

The `List<LogEntry>` used as a log sink is a plain `List<T>`. This works today because the pipeline is single-threaded, but if anyone adds `Parallel` or async logging in the future, this test infrastructure will produce intermittent failures. A `ConcurrentBag<LogEntry>` or `lock` would be more defensive. Low priority since the current usage is safe.

**S4. Test agent-notes state says "red" but tests are green.** (`LoggingTests.cs` line 1, `ErrorHandlingTests.cs` line 1)

Both test files have `state: "red"` in their agent-notes, but all 13 tests pass. Update to `state: active` or `state: "green"` to reflect the actual state.

**S5. Consider testing the `ConvertCommand` error paths end-to-end.** The CLI-level catch blocks for `Md2Exception` and generic `Exception` are not covered by any test. An integration test that invokes `ExecuteAsync` with a file containing malformed YAML front matter would verify the full error presentation path -- that the user sees the `UserMessage`, not the internal detail.

### Clean

**Pierrot:** No security or compliance concerns in this change. The `--debug` flag exposes stack traces to stderr, which is appropriate for a CLI tool (the user is the operator). No secrets are logged. The new dependencies (`Microsoft.Extensions.Logging` and `Microsoft.Extensions.Logging.Abstractions`) are MIT-licensed Microsoft packages with no known vulnerabilities. User input paths logged via structured logging templates (`{Path}`) rather than string interpolation, which is correct practice.

## Lessons

1. **Define exception types where they are thrown, not just where they are caught.** Creating an exception hierarchy is only half the work. The value comes from wrapping known failure points with domain-specific exceptions that carry actionable user messages. An exception type that exists but is never thrown is dead code that creates a false sense of coverage.

2. **The `UserMessage` pattern is only as good as the messages you write.** Separating user-facing text from developer detail is an excellent pattern. But if every constructor just passes `message` through to `UserMessage`, you have the mechanism without the benefit. The hard part is writing messages that tell non-technical users what to do next. "Failed to parse YAML front matter: (Line: 3, Col: 5) - ..." is a developer message wearing a user message hat.

3. **Removing observability is not the same as fixing a layering violation.** When you find `Console.Error.WriteLine` in a library, the fix is not to delete it -- it is to replace it with the proper abstraction (in this case, `ILogger`). The old code was wrong in *how* it reported; the new code is wrong in *whether* it reports. Both are bugs, but silent failures are harder to diagnose.

4. **Debug-level logging needs actual Debug-level content.** Adding a `--debug` flag is a great UX improvement. But if the only difference between `--verbose` and `--debug` is that `--debug` also shows stack traces on errors, users will not find it useful during normal operation. Populate the Debug level with the kind of detail that helps diagnose "why did my document render wrong" -- theme values, node counts, transform timings, skipped elements.

5. **Test infrastructure deserves the same care as production code.** The `ListLoggerProvider` in the tests is a reusable piece of test infrastructure. Making it thread-safe from the start costs almost nothing (`ConcurrentBag` instead of `List`) and prevents a class of flaky test that is notoriously hard to debug when it eventually appears.
