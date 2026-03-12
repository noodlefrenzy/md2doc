---
agent-notes:
  ctx: "review of md2 theme resolve CLI command"
  deps: [src/Md2.Cli/ThemeResolveCommand.cs, src/Md2.Themes/ThemeResolveFormatter.cs, tests/Md2.Themes.Tests/ThemeResolveFormatterTests.cs, src/Md2.Cli/Program.cs]
  state: active
  last: "code-reviewer@2026-03-12"
---
# Code Review: Theme Resolve Command (#40)

**Date:** 2026-03-12
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `src/Md2.Cli/ThemeResolveCommand.cs` (new, 262 lines)
- `src/Md2.Themes/ThemeResolveFormatter.cs` (new, 62 lines)
- `tests/Md2.Themes.Tests/ThemeResolveFormatterTests.cs` (new, 205 lines)
- `src/Md2.Cli/Program.cs` (modified, wiring)

**Verdict:** Approved with suggestions

## Context

Issue #40 adds a `md2 theme resolve` subcommand that displays resolved theme properties in a table showing Property/Value/Source columns. The command accepts `--preset`, `--theme`, `--template`, and `--style` flags. Style overrides are parsed from `key=value` CLI arguments and mapped onto `ThemeDefinition` via a large switch statement. `ThemeResolveFormatter` produces the aligned table output.

## Findings

### Critical

No critical findings.

### Important

**1. Dead code / no-op variable assignment (Vik) -- `ThemeResolveCommand.cs` line 143**

```csharp
var effectiveKey = key.Contains('.') ? key : key;
```

This ternary always assigns `key` regardless of the condition. The comment says "strip optional section prefix for flat keys" but no stripping occurs. This is either a leftover from a refactor or an unfinished feature. Either way, it is dead logic that will confuse readers. If the intent was to make `effectiveKey` equal `key` in all cases, remove the ternary entirely and use `key` directly. If the intent was to strip a prefix, the logic needs to actually do that.

**2. The giant switch statement will not scale (Vik) -- `ThemeResolveCommand.cs` lines 145-259**

The `ApplyStyleOverride` method is a 115-line switch with two match arms per property and repetitive `??= new` initialization. Every new theme property requires adding two more `case` arms. This is manageable today at ~30 properties, but it is already the largest method in the diff and the pattern is fragile -- it is easy to forget the short-form alias, and the duplication invites copy-paste errors.

A data-driven approach (dictionary mapping keys to `Action<ThemeDefinition, string>` delegates) would cut this to ~40 lines, eliminate the dual-case pattern, and make adding properties a one-liner. This is not blocking, but it should be addressed before the property count grows further.

**3. Page layout properties missing from `--style` overrides (Vik)**

`ThemeCascadeResolver` resolves `PageWidth`, `PageHeight`, `MarginTop`, `MarginBottom`, `MarginLeft`, and `MarginRight` from `ThemeDocxSection.Page`, but `ApplyStyleOverride` has no cases for any of these. A user who tries `--style docx.page.width=12240` will get a silent "Unknown style property" warning. This is an incomplete feature surface -- the style override parser should support the same properties the resolver handles, or the help text should document the limitation.

**4. Silent failure on unparseable numeric values (Vik) -- lines 218, 221, etc.**

```csharp
if (double.TryParse(value, out var baseFs)) theme.Docx.BaseFontSize = baseFs;
```

When a user passes `--style baseFontSize=abc`, the parse fails silently -- no warning, no error, the property is simply not set. This contradicts the pattern on line 127 where malformed `key=value` input gets a warning. The user will see no feedback that their override was ignored. At minimum, emit a warning on parse failure.

**5. No tests for `ParseStyleOverrides` or `Execute` (Tara)**

`ThemeResolveCommand.ParseStyleOverrides` is `internal` (testable via `InternalsVisibleTo`), and `Execute` is also `internal`, but neither has unit tests. The formatter has 12 tests with good coverage, but the more complex logic -- parsing key=value strings, mapping to theme properties, error code returns, file-not-found handling -- is completely untested. This is the riskier code path. At minimum, test:
- Valid key=value parsing for each section (typography, colors, docx)
- Invalid format (no `=` sign) produces a warning
- Unknown key produces a warning
- Unparseable numeric value behavior (whatever is chosen)
- `Execute` returns 2 for missing files
- `Execute` returns 0 for a basic resolve

### Suggestions

**6. `CultureInfo.InvariantCulture` for numeric parsing (Pierrot) -- lines 218-254**

`double.TryParse(value, ...)` and `int.TryParse(value, ...)` use the current culture by default. On a machine with a locale where the decimal separator is `,`, `--style baseFontSize=14.5` would fail silently. Use `CultureInfo.InvariantCulture` to match the YAML parser's behavior and ensure consistent cross-locale behavior.

**7. The `ResolvedTheme` parameter is unused in `Format` (Vik) -- `ThemeResolveFormatter.cs` line 18**

`Format(ResolvedTheme theme, ...)` accepts `theme` but never reads it -- all data comes from the `trace` list. If this parameter is reserved for future use (e.g., showing a "resolved value" column distinct from the trace value), document it with a comment. Otherwise, remove it to avoid confusion.

**8. Test file agent-notes shows `state: red` (Tara) -- `ThemeResolveFormatterTests.cs` line 2**

The tests are passing (119 pass in the test project). The agent-notes state should be `active`, not `red`. This is a minor metadata inconsistency.

**9. Alignment test could be more precise (Tara) -- `ThemeResolveFormatterTests.cs` lines 134-168**

`Format_ColumnsAreAligned_AllRowsSameColumnPositions` only checks that rows are long enough, not that the actual column content starts at the correct offset. A more robust assertion would verify that the value and source content in each data row begins at the same character position as the header columns.

**10. No `--verbose` / `--debug` support (Ines)**

`ConvertCommand` supports `--verbose` and `--debug` flags with structured logging. `ThemeResolveCommand` has no logging at all. For a diagnostic command this is less critical than for conversion, but emitting debug-level information about which layers are being loaded and which overrides are applied would be valuable when users are debugging theme resolution issues.

## Lessons

1. **Switch-based property mapping is a maintenance trap.** When you find yourself writing `case "x" or "y":` for 30+ properties with identical structure per arm, that is a signal to switch to a data-driven approach. A `Dictionary<string, Action<ThemeDefinition, string>>` is both shorter and self-documenting -- the mapping table IS the documentation of supported keys.

2. **Test the parsing, not just the formatting.** It is tempting to test the "pretty" output layer and skip the "boring" input parsing layer. But parsing user input is where bugs actually live -- malformed input, locale issues, missing keys, edge cases in splitting on `=`. The formatter is deterministic given valid input; the parser faces the full chaos of user-supplied strings.

3. **Silent failures are UX bugs.** When a user provides `--style baseFontSize=abc` and nothing happens, they will spend time wondering why the font size did not change. A warning on stderr costs one line of code and saves minutes of debugging. The code already emits warnings for unknown keys and malformed key=value pairs -- the numeric parse failure is an inconsistency in the error reporting pattern.

4. **Culture-sensitive parsing in CLI tools is a recurring source of bugs.** `double.TryParse` without `CultureInfo.InvariantCulture` is correct exactly once -- when your entire user base shares your locale. For any tool that might run on CI, in Docker, or on a colleague's machine in a different country, always specify the culture explicitly for numeric parsing.

5. **Unused parameters signal incomplete design.** When a public method accepts a parameter it does not use, readers must guess whether it is (a) an oversight, (b) reserved for future use, or (c) needed by a caller convention. A one-line comment resolves the ambiguity; removing the parameter resolves it better.
