---
agent-notes:
  ctx: "review of ThemeValidator schema+warning validation"
  deps: [src/Md2.Themes/ThemeValidator.cs, tests/Md2.Themes.Tests/ThemeValidatorTests.cs]
  state: active
  last: "code-reviewer@2026-03-12"
---
# Code Review: ThemeValidator

**Date:** 2026-03-12
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `/workspaces/md2doc/src/Md2.Themes/ThemeValidator.cs`
- `/workspaces/md2doc/tests/Md2.Themes.Tests/ThemeValidatorTests.cs`
- `/workspaces/md2doc/src/Md2.Themes/ThemeDefinition.cs` (context)

**Verdict:** Approved with suggestions

## Context

ThemeValidator is a static validation utility for ThemeDefinition instances. It separates issues into two severity levels: Errors (schema violations like invalid hex colors, non-positive font sizes) and Warnings (unusual-but-valid values like extreme font sizes or tight margins). Each issue carries a property path for precise error reporting. The implementation uses source-generated regex for hex validation and a clean pattern of validate-then-warn with error-path deduplication to avoid double-reporting.

## Findings

### Critical

No critical findings.

### Important

**1. Missing validation for negative page margins**

The validator checks that page Width and Height are positive, that font sizes are positive, and that TableBorderWidth and BlockquoteIndentTwips are non-negative. However, the four page margin properties (MarginTop, MarginBottom, MarginLeft, MarginRight) are `int?` and are never validated. A negative margin would produce invalid DOCX output. The WarnContentWidth method even consumes these values (defaulting null to 0) but never checks them first.

This matters because margins are user-facing YAML values -- they are the most likely place a user would make a typo like `-1440` instead of `1440`.

**Fix:** Add `ValidateNonNegativeInt` calls for all four margin properties in `ValidatePage`, similar to the TableBorderWidth and BlockquoteIndentTwips checks.

**2. Unsafe uint-to-int cast in WarnContentWidth (line 179)**

`page.Width.Value` is `uint`, but is cast to `int` for the arithmetic. For any width value above `int.MaxValue` (~2.1 billion), this silently wraps to a negative number. While such values are unrealistic for twips, the more practical concern is when margins exceed width: the result goes negative and the warning message reports a confusing negative inch value.

**Fix:** Consider clamping the reported value to zero, or restructuring to use `long` arithmetic to avoid the cast entirely.

### Suggestions

**3. NormalizeHex strips '#' before validation -- no integration test documents this**

ThemeColorsSection setters call `NormalizeHex(value)` which strips leading `#` characters. This means the validator's regex `^[0-9A-Fa-f]{6}$` never sees a `#`-prefixed color. The behavior is arguably correct (be liberal in what you accept), but there is no test that explicitly exercises the end-to-end path: set a color with `#FF0000`, validate, expect no error. Adding such a test would document the design decision and prevent future confusion when someone reads the regex and assumes `#`-prefixed values are rejected.

**4. Duplicate enumeration of font-size properties**

Lines 85-91 enumerate all seven font-size properties for `ValidatePositiveDouble`, then lines 102-108 enumerate the same seven for `WarnFontSizeRange`. If a property is added to one list but not the other, the error/warning relationship breaks silently. A data-driven approach (array of tuples mapping accessor + path) or a combined validate-and-warn helper would eliminate this maintenance risk.

**5. Test file agent-notes state is "red" but all 58 tests pass**

The `state: red` in ThemeValidatorTests.cs should be updated to `active`.

**6. No coverage for degenerate double values (NaN, Infinity)**

`double.NaN` passes `ValidatePositiveDouble` because `NaN <= 0` is false. `double.PositiveInfinity` also passes. Both values would produce broken DOCX output. Consider adding explicit checks for `double.IsNaN` and `double.IsInfinity` in `ValidatePositiveDouble`, and corresponding test cases.

## Lessons

1. **Validate at every boundary the user can reach.** The validator checks font sizes and border widths but misses margins. When writing a validator, the discipline is to walk through every settable property on the model and decide: does this need validation? A checklist approach (enumerate all properties, mark each as validated/not-needed/skipped-intentionally) prevents gaps.

2. **Beware of normalization before validation.** When a model normalizes input in its setters (like stripping `#` from hex colors), the validator sees the normalized form, not the raw user input. This is fine as long as it is documented and tested. The risk is that someone reads the validator in isolation and draws incorrect conclusions about what inputs are accepted. An integration test bridging the gap is cheap insurance.

3. **Data-driven validation reduces maintenance risk.** When the same set of properties must be processed by multiple validation steps (check validity, then check reasonableness), enumerating them in parallel lists creates a synchronization burden. Grouping the property accessor, path string, and validation rules into a single data structure makes it impossible to add a property to one pass but forget the other.

4. **Degenerate floating-point values are valid doubles.** `NaN`, `PositiveInfinity`, and `NegativeInfinity` are all valid `double` values that pass many naive range checks. Any validator accepting `double` input from an external source (YAML parsing) should explicitly handle these cases. The pattern is: check for `IsNaN`/`IsInfinity` first, then check range.

5. **Type-level constraints do not eliminate the need for validation messages.** `Width` is `uint?`, which makes negative values structurally impossible. But the validator still checks for zero, and the error message says "must be positive." This is correct -- the type system handles one class of invalid input, the validator handles another. Recognizing which constraints are enforced where prevents both gaps and redundant checks.
