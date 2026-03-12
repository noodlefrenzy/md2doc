---
agent-notes:
  ctx: "review of ThemeDefinition, ThemeParser, PresetRegistry"
  deps: [src/Md2.Themes/ThemeDefinition.cs, src/Md2.Themes/ThemeParser.cs, src/Md2.Themes/PresetRegistry.cs, src/Md2.Themes/Presets/default.yaml]
  state: active
  last: "code-reviewer@2026-03-12"
---
# Code Review: Md2.Themes -- ThemeParser, ThemeDefinition, PresetRegistry

**Date:** 2026-03-12
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `src/Md2.Themes/ThemeDefinition.cs`
- `src/Md2.Themes/ThemeParser.cs`
- `src/Md2.Themes/ThemeParseException.cs`
- `src/Md2.Themes/PresetRegistry.cs`
- `src/Md2.Themes/Presets/default.yaml`
- `src/Md2.Themes/Md2.Themes.csproj`
- `tests/Md2.Themes.Tests/ThemeParserTests.cs`
- `tests/Md2.Themes.Tests/ThemeDefinitionTests.cs`
- `tests/Md2.Themes.Tests/PresetRegistryTests.cs`

**Verdict:** Changes requested

## Context

Issues #35 and #38 introduce a new `Md2.Themes` project that externalizes the theme system. `ThemeDefinition` is the deserialization model (all nullable for partial theme/cascade support). `ThemeParser` wraps YamlDotNet deserialization. `PresetRegistry` loads embedded YAML presets from assembly resources with caching. The `default.yaml` preset is intended to replace the hardcoded values in `ResolvedTheme.CreateDefault()`.

## Findings

### Critical

**C1. Color format mismatch between default.yaml and ResolvedTheme will cause integration bugs.**

`ResolvedTheme` (in `src/Md2.Core/Pipeline/ResolvedTheme.cs`) stores all color values **without** the `#` prefix (e.g., `"1B3A5C"`). The `default.yaml` preset stores them **with** the `#` prefix (e.g., `"#1B3A5C"`). The `ThemeDefinition` model stores colors as raw strings with no normalization.

When the cascade/merge layer maps `ThemeDefinition` colors onto `ResolvedTheme`, one of two things will happen: either the `#` prefix leaks through and breaks every Open XML color attribute (they expect bare hex), or someone has to remember to strip it during mapping. This is a silent data corruption bug -- the DOCX will render with missing or wrong colors and there will be no error.

**Why this matters:** The comment in `default.yaml` says "These values match the hardcoded defaults." They do not. The test `GetPreset_Default_MatchesResolvedThemeDefaults` asserts typography and sizes but does not cross-check color values against `ResolvedTheme`, so this mismatch is invisible to the test suite.

**Fix options:**
1. Pick one canonical format (recommend bare hex, matching Open XML's requirement) and store that in the YAML. Add a YAML comment documenting the convention.
2. Alternatively, if user-facing YAML should accept `#` for ergonomics, add a normalization step in the parser or a dedicated color-value type that strips `#` on deserialization. Either way, document and test the contract.

### Important

**I1. PresetRegistry cache has a TOCTOU race -- double-parse on first concurrent access.**

In `PresetRegistry.GetPreset()` (lines 39-65), the lock is released after the cache-miss check (line 43), and the resource is loaded and parsed outside the lock. Two threads calling `GetPreset("default")` simultaneously will both parse the YAML. Because `ThemeDefinition` is a mutable POCO, they will get different object instances cached under the same key (last writer wins in the second lock block).

This is not a correctness bug today because the parsed objects are value-equivalent and callers likely do not mutate them. But it violates the principle of least surprise for a class named "cache" -- callers may reasonably assume reference equality for the same key.

**Fix:** Use `ConcurrentDictionary<string, Lazy<ThemeDefinition>>` or move the entire load-and-cache into the lock. For a CLI tool the performance difference is negligible; the simpler lock-around-everything approach is fine.

**I2. `ThemeDefinition` classes are mutable POCOs with no equality semantics.**

All properties are mutable `{ get; set; }`. Since these represent deserialized config that should not change after load, consider making them records or at least adding a note that mutation after parse is unsupported. This matters because `PresetRegistry` caches and returns the same instance -- a caller mutating the returned object silently corrupts the cache for all subsequent callers.

**Fix (minimum):** Document that returned `ThemeDefinition` instances must not be mutated, or return a deep copy from `GetPreset()`. Better: use `init` setters (C# 9+), which YamlDotNet supports.

**I3. No validation of color string format.**

Color strings are stored as raw `string?` with no validation. Invalid values like `"red"`, `"#ZZZZZZ"`, or `""` will silently pass through parsing and only fail deep in the DOCX emitter. Fail-fast validation at parse time would save significant debugging time.

**Fix:** Add a regex check or a dedicated `ThemeColor` value type that validates hex format on construction.

### Suggestions

**S1. `ListPresets()` creates a new list on every call.**

`ListPresets()` re-scans manifest resource names each time. For a CLI this is harmless, but since the set of embedded resources is fixed at compile time, caching the result in a `static readonly` field would be cleaner and consistent with the caching pattern already used in `GetPreset()`.

**S2. Test for `Parse_ColorsWithAndWithoutHash_BothWork` documents a behavior but does not assert a contract.**

The test at `ThemeParserTests.cs:184` verifies that both `"#FF0000"` and `"00FF00"` round-trip through the parser as-is. This is fine as a documentation test, but it implicitly says "we accept inconsistent color formats." Once the color format contract is decided (see C1), update this test to reflect the chosen convention -- either normalize to one format or reject the other.

**S3. Consider adding `WhitespaceOnly` to the empty-YAML tests.**

`Parse_EmptyYaml_ReturnsEmptyDefinition` tests `""` but not whitespace-only strings like `"   "` or `"\n"`. The implementation handles this via `string.IsNullOrWhiteSpace`, so coverage exists implicitly, but an explicit test case would document the behavior.

**S4. `ThemeParseException` should have a default parameterless constructor for serialization completeness.**

Minor, but standard .NET exception design guidelines recommend providing a parameterless constructor and a serialization constructor. Since this is a CLI tool and not a library shipped to third parties, this is low priority.

### Clean

- **Vik (simplicity):** The overall architecture is clean and proportional. The separation of `ThemeDefinition` (model), `ThemeParser` (deserialization), and `PresetRegistry` (resource loading) follows single-responsibility well. The code is readable and a junior could navigate it during an incident. No premature abstraction.
- **Tara (testing):** Test coverage is good for the happy path, partial themes, unknown properties, malformed YAML, file I/O, case-insensitivity, and completeness of the default preset. 25 tests all passing. The main gap is the missing cross-check against `ResolvedTheme` color values (covered in C1).
- **Pierrot (security):** No security or compliance concerns in this change. YamlDotNet 16.3.0 is MIT-licensed, no known CVEs. `ParseFile` does not do path traversal beyond what `File.ReadAllText` provides -- the caller controls the path. No user input reaches the parser in the embedded-resource path. No secrets in config.
- **Ines (operational):** This is a library layer; logging and error messages are appropriate for the scope. `ThemeParseException` includes line/column info which aids debugging. The error message for unknown presets lists available options, which is good UX.

## Lessons

1. **Format contracts must be explicit and tested across boundaries.** The `#`-prefix mismatch between `default.yaml` and `ResolvedTheme` is a classic integration seam bug. When two systems share a data format (here, hex colors), write a test that asserts the contract at the boundary, not just within each system independently. The `GetPreset_Default_MatchesResolvedThemeDefaults` test was the right instinct but stopped short of checking colors.

2. **Caches that return mutable objects are traps.** If `PresetRegistry` caches a `ThemeDefinition` and hands out the same reference, any caller that modifies it corrupts the cache for everyone. This is especially insidious because it works in tests (single-threaded, single call) and fails in production. The fix is either immutability (prefer `init` setters or records) or defensive copying.

3. **"Silently accept anything" is a design choice with costs.** Ignoring unknown YAML properties (for forward compatibility) is a reasonable design decision, and the code documents it. But the same permissiveness applied to color format validation means garbage-in-garbage-out. Be intentional about where you are lenient (structure/schema evolution) vs. strict (value semantics).

4. **Embedded resource patterns in .NET need careful naming alignment.** The `ResourcePrefix` / `ResourceSuffix` convention in `PresetRegistry` is clean, but the mapping from preset name to resource name uses `name.ToLowerInvariant()`. If someone adds a preset file with mixed case (e.g., `CorporateBlue.yaml`), the resource name will be `Md2.Themes.Presets.CorporateBlue.yaml` but the lookup will search for `Md2.Themes.Presets.corporateblue.yaml`. This will silently fail. Either enforce lowercase filenames by convention (document it) or normalize the lookup differently.

5. **TOCTOU in caches is rarely a correctness bug but always a code smell.** The double-parse race in `PresetRegistry` is harmless today because the parsed values are equivalent. But "harmless today" is not the same as "correct." Using `ConcurrentDictionary` with `GetOrAdd` or `Lazy<T>` is the idiomatic .NET pattern and communicates the intent more clearly than manual lock juggling.
