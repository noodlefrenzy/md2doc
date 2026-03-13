---
agent-notes:
  ctx: "Review of #80 TransformResult warnings and #81 ThemeParseException"
  deps: [src/Md2.Core/Pipeline/TransformResult.cs, src/Md2.Core/Pipeline/ConversionPipeline.cs, src/Md2.Cli/ConvertCommand.cs, src/Md2.Themes/ThemeParseException.cs]
  state: active
  last: "code-reviewer@2026-03-13"
---
# Code Review: TransformResult Warnings (#80) and ThemeParseException Hierarchy (#81)

**Date:** 2026-03-13
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `src/Md2.Core/Pipeline/TransformResult.cs` (new)
- `src/Md2.Core/Pipeline/ConversionPipeline.cs`
- `src/Md2.Cli/ConvertCommand.cs`
- `src/Md2.Themes/ThemeParseException.cs`
- `tests/Md2.Core.Tests/Pipeline/ConversionPipelineTests.cs`
- `tests/Md2.Integration.Tests/CompositionTests.cs`
- `tests/Md2.Integration.Tests/EndToEndTests.cs`
- `tests/Md2.Themes.Tests/ThemeParserTests.cs`

**Verdict:** Approved with suggestions

## Context

Two small, focused changes:

**#80** changes `ConversionPipeline.Transform()` to return a `TransformResult` (document + warnings list) instead of a bare `MarkdownDocument`. The CLI then writes any warnings to stderr, suppressed by `--quiet`. This is a clean API evolution -- transforms can already call `context.AddWarning()`, but the warnings were previously silently discarded.

**#81** changes `ThemeParseException` to extend `Md2Exception` instead of `Exception`, bringing it into the project's exception hierarchy so the CLI's catch block shows `UserMessage` instead of the raw `Message`.

Both changes are proportional to the problem, well-tested at the unit and integration level, and mechanically simple.

## Findings

### Critical

None.

### Important

**1. No CLI-level test for warning output (Tara)**

The unit tests verify that `TransformResult.Warnings` is populated correctly, but there is no test verifying the CLI behavior: that warnings appear on stderr when `--quiet` is false, and are suppressed when `--quiet` is true. The `ConvertCommand.cs` change at lines 202-209 is untested integration logic. If someone later refactors the warning output (e.g., moves it, changes the format, accidentally deletes it), no test would catch the regression.

This is the most impactful gap. A CLI integration test that captures stderr and asserts on warning presence/absence would close it.

### Suggestions

**1. Defensive copy in TransformResult constructor (Vik)**

`TransformResult` at line 12 accepts `IReadOnlyList<string>` but stores the reference directly. Since `TransformContext.Warnings` is a mutable `List<string>`, the `TransformResult` consumer could theoretically see mutations if someone held a reference to the context and continued adding warnings after `Transform()` returned. In practice this cannot happen today because the context is scoped to the `Transform()` method, but a defensive `.ToList()` or `.AsReadOnly()` in the constructor would make the contract explicit and protect against future refactors:

```csharp
Warnings = warnings.ToList().AsReadOnly();
```

**2. Consider a record type for TransformResult (Vik)**

`TransformResult` is a simple immutable data carrier with two properties. A `record` would give you structural equality, deconstruction, and `ToString()` for free, with less boilerplate:

```csharp
public record TransformResult(MarkdownDocument Document, IReadOnlyList<string> Warnings);
```

This is purely stylistic and does not affect correctness.

**3. Warning format consistency (Vik)**

The CLI prefixes transform warnings with `"Warning: "` (line 207 of ConvertCommand.cs), which matches the existing pattern used for theme validation warnings (line 230). Good. One minor note: theme validation warnings include a bracketed path like `"Warning: Theme [colors.primary]: ..."`, while transform warnings are bare. If the warning count grows, consider a structured format, but this is fine for now.

**4. ThemeParseException lacks the two-message constructor (Vik)**

`Md2Exception` offers a `(string message, string userMessage)` constructor for cases where the developer message and user-facing message should differ. `ThemeParseException` does not expose this variant. This is fine today since theme parse errors are inherently user-facing (bad YAML), but if you later want to include parser internals in `Message` while keeping `UserMessage` clean, you would need to add it. Low priority.

### Clean

- **Pierrot:** No security or compliance concerns in this change. No new attack surface, no user input handling changes, no secrets, no new dependencies.
- **Ines (operational):** Warning output to stderr at the right point in the pipeline, respects `--quiet`, uses appropriate severity. Transform-phase errors already have logging via the pipeline logger. The `--debug` flag behavior is unchanged and correct.

## Lessons

1. **Return types are API contracts.** Changing `Transform()` from returning `MarkdownDocument` to `TransformResult` is a breaking change for all callers. This diff correctly updates all call sites (CLI, unit tests, integration tests). When evolving a return type, grep for every caller before committing -- the compiler will catch it in C#, but in dynamic languages this class of bug is silent. The mechanical update across tests is tedious but necessary; skipping integration test updates is how "it compiles" becomes "it crashes in CI."

2. **Warnings are better than silent failures.** The prior design had `TransformContext.AddWarning()` but nothing read the warnings. This is a common pattern: someone adds a reporting mechanism during implementation but the consumer is "coming later" and never arrives. When you add a `List<Warning>` to a context object, ask immediately: "Who reads this? Where does it surface?" If the answer is "nobody yet," file a ticket right then.

3. **Exception hierarchies pay off at the catch site.** The `ThemeParseException` change is two lines of diff, but the value is at `ConvertCommand.cs` line 326-334: the `catch (Md2Exception ex)` block now catches theme parse errors and shows `UserMessage` instead of falling through to the generic `catch (Exception)` which says "An unexpected error occurred." Every custom exception type in a project should extend the project's base exception, or you lose the ability to distinguish "expected user errors" from "bugs" at the top-level handler.

4. **Defensive copies vs. trust boundaries.** The `TransformResult` stores a reference to the context's mutable list. This is safe today because the context is method-scoped, but "safe today" is not "safe after refactoring." Defensive copies cost almost nothing for small collections and make the immutability guarantee real rather than incidental. The general rule: if a type claims to be immutable (via `IReadOnlyList<T>` or similar), make sure the backing data actually cannot change.
