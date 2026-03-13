---
agent-notes:
  ctx: "Review of #54 md2 doctor diagnostic command"
  deps: [src/Md2.Cli/DoctorCommand.cs, tests/Md2.Integration.Tests/DoctorCommandTests.cs, src/Md2.Cli/Program.cs]
  state: active
  last: "code-reviewer@2026-03-13"
---
# Code Review: md2 doctor Diagnostic Command (#54)

**Date:** 2026-03-13
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `src/Md2.Cli/DoctorCommand.cs` (new)
- `tests/Md2.Integration.Tests/DoctorCommandTests.cs` (new)
- `src/Md2.Cli/Program.cs` (modified)

**Verdict:** Changes requested

## Context

Issue #54 adds an `md2 doctor` command that checks five environmental prerequisites: .NET runtime version, OS info, Chromium installation (for Mermaid diagrams), TextMateSharp syntax highlighting, and the diagram cache directory. It reports pass/fail/warn for each check, writes fix suggestions to stderr, and exits with code 1 if any check fails.

The design is clean and proportional -- a single static class, ~115 lines, with output/error writers injected for testability. The wiring into `Program.cs` is a one-liner.

## Findings

### Critical

None.

### Important

**1. CodeTokenizer created but never disposed**

`DoctorCommand.cs` line 62 creates `new CodeTokenizer()` without a `using` statement. `CodeTokenizer` implements `IDisposable`. While the current `Dispose()` implementation is empty, the class contract says "I hold resources." A future TextMateSharp update could add real cleanup, and static analysis tools will flag this.

Fix: `using var tokenizer = new CodeTokenizer();`

**2. CancellationToken accepted but never observed**

`RunChecksAsync` accepts a `CancellationToken` that is never checked or forwarded. The entire method is synchronous wrapped in `Task.FromResult`. This creates a false API promise -- callers think they can cancel the operation but cannot. Either use the token (check between steps) or remove it from the signature to be honest about the contract.

**3. Tests cannot detect regressions -- assertions are too weak**

All eight tests assert only that a label string (e.g., "Chromium", "Syntax highlighting") appears in the output. They do not assert whether the check passed or failed. A test named `RunChecks_ChecksDotNetRuntime` passes whether the output says `[OK] .NET Runtime` or `[FAIL] .NET Runtime`.

The `RunChecks_ReturnsZeroWhenAllPass` test has a conditional assertion (line 89: `if (!text.Contains("[FAIL]"))`) which means it literally cannot fail -- if a check breaks, the assertion is skipped. This is the most dangerous kind of test: it provides confidence without providing protection.

Fix: For deterministic checks (.NET, OS, cache), assert the specific status symbol. For environment-dependent checks (Chromium), use a skip/trait mechanism rather than conditional assertions. Best: make the checks injectable so failure paths can be tested directly.

### Suggestions

**1. Extract check dependencies for testability**

The command directly calls `BrowserManager.IsChromiumInstalled()` (static) and `new CodeTokenizer()` (concrete). This means the failure and warning branches (Chromium not installed, TextMateSharp returns no tokens, TextMateSharp throws) are untestable without environmental manipulation. A simple improvement: accept check functions as constructor parameters or delegates. Even `Func<bool> isChromiumInstalled` would let tests exercise both branches.

**2. Use an enum for check status instead of magic strings**

`WriteCheck` accepts `"pass"`, `"fail"`, `"warn"` as raw strings. A typo like `"Pass"` silently produces `"[??]"`. An enum makes this compile-time safe. For a diagnostic tool, output correctness is the entire value proposition.

**3. Diagram cache path appears hardcoded**

`Path.Combine(Path.GetTempPath(), "md2-cache")` on line 79 duplicates knowledge of where the diagram cache lives. If the actual renderer uses a different path, doctor would report a false positive. Extract this to a shared constant.

**4. Broad exception catches could swallow catastrophic errors**

`catch (Exception ex)` at lines 72 and 85 catches everything including `OutOfMemoryException`, `StackOverflowException`, etc. For `Directory.CreateDirectory`, narrowing to `IOException` and `UnauthorizedAccessException` would be more precise. Low priority for a diagnostic tool.

### Clean

- **Pierrot:** No security or compliance concerns. No user input reaches dangerous APIs. No secrets, no new network surface, no auth changes.
- **Ines (operational):** Good stdout/stderr separation. Exit code 0/1 supports scripting. Fix suggestions in stderr are actionable.

## Lessons

1. **IDisposable is a contract, not a suggestion.** When a class implements `IDisposable`, wrap it in `using` even if you know the current `Dispose()` is empty. The interface is a signal to future maintainers and to static analysis. Code that ignores `IDisposable` today becomes a resource leak when the underlying library adds real cleanup. The cost of `using` is zero; the cost of tracking down a leaked handle is hours.

2. **Tests that cannot fail are worse than no tests.** A conditional assertion like `if (!broken) { assert(works) }` creates false confidence. The test suite reports green, the team assumes coverage exists, and when the code actually breaks nobody finds out until production. If a test depends on environmental state, mark it as conditional explicitly (skip/trait) so the test runner reports it as "skipped" rather than "passed." The test report should never lie about what was actually verified.

3. **Diagnostic tools need testable failure paths.** A `doctor` command exists precisely to report problems, so the failure branches are the most important code paths. If you cannot test the failure output without actually breaking your environment, the design needs a seam. Dependency injection does not have to mean a full DI container -- a constructor parameter, a delegate, or even a `protected virtual` method gives tests the hook they need. Design for testability is especially important in code whose entire purpose is detecting and reporting failures.

4. **Unused parameters are misleading API surface.** A `CancellationToken` parameter that is never observed tells callers "you can cancel this" when they cannot. API signatures are documentation -- they communicate contracts. An unused parameter is a broken promise. Either honor the contract or remove the parameter and add it back when you actually need it.
