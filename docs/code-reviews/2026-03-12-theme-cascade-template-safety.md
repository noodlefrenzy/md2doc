---
agent-notes:
  ctx: "review of ThemeCascadeResolver and TemplateSafetyChecker"
  deps: [src/Md2.Themes/ThemeCascadeResolver.cs, src/Md2.Themes/TemplateSafetyChecker.cs, tests/Md2.Themes.Tests/ThemeCascadeResolverTests.cs, tests/Md2.Themes.Tests/TemplateSafetyTests.cs]
  state: active
  last: "code-reviewer@2026-03-12"
---
# Code Review: ThemeCascadeResolver (#36) and TemplateSafetyChecker (#37)

**Date:** 2026-03-12
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `src/Md2.Themes/ThemeCascadeResolver.cs`
- `src/Md2.Themes/TemplateSafetyChecker.cs`
- `tests/Md2.Themes.Tests/ThemeCascadeResolverTests.cs`
- `tests/Md2.Themes.Tests/TemplateSafetyTests.cs`
**Verdict:** Approved with suggestions

## Context

These two files resolve TD-001 (hardcoded ResolvedTheme). `ThemeCascadeResolver` implements a 4-layer cascade merge (CLI > theme > preset > template) that maps nullable `ThemeDefinition` properties onto the flat `ResolvedTheme` class. Each property is resolved independently -- the first layer to define a value wins. A trace mechanism records where each resolved value came from.

`TemplateSafetyChecker` validates DOCX template files before they enter the pipeline, per ADR-0010. It detects IRM/DRM-protected files (via OLE magic bytes), rejects legacy `.doc` format, warns on macro-enabled `.docm`, and enforces a configurable size limit.

12 tests cover the cascade resolver; 9 tests cover the safety checker. All 21 pass.

## Findings

### Critical

None.

### Important

**1. Reflection-based fallback is fragile and silently wrong on property name drift**

**File:** `ThemeCascadeResolver.cs`, lines 134-139 (and parallel code at 158-160, 180-184, 204-208)

All four `Resolve*` helpers share a fallback path: if no cascade layer provides a value, they instantiate a fresh `ResolvedTheme()` and use `typeof(ResolvedTheme).GetProperty(propertyName)` to look up the default by string name. If a `ResolvedTheme` property is ever renamed, the string at the call site does not update, reflection returns `null`, and the method silently falls back to `""` (strings) or `0` (numerics). The trace will also lie, recording `CascadeLayer.Preset` for a value that came from nowhere.

In practice, the default preset is complete and this fallback never triggers today. But fallback paths that silently produce wrong data are the most dangerous kind of latent bug -- they surface in production under exactly the conditions you least expect.

**Why it matters:** Reflection-based string-to-property coupling breaks the compiler's ability to catch renames. Refactoring tools like "Rename Symbol" will update all typed references but not string literals. The bug would be invisible until a user hits the exact scenario where the preset is incomplete for that property.

**Fix:** Replace reflection with an explicit default parameter: `ResolveString(layers, d => d.Typography?.HeadingFont, "HeadingFont", "Calibri", trace)`. This is compile-time safe, faster, and obvious. Alternatively, add a test that asserts all `propertyName` values resolve to real properties.

**2. Four near-identical Resolve helpers (~90 lines of structural duplication)**

**File:** `ThemeCascadeResolver.cs`, lines 118-209

`ResolveString`, `ResolveDouble`, `ResolveInt`, and `ResolveUint` differ only in their null-check pattern and zero-default type. This is a textbook case for a generic: `ResolveValue<T>(layers, selector, name, defaultValue, trace)` where `T : struct` handles the nullable value types, plus a string overload.

**Why it matters:** Each time a new numeric type is needed (e.g., `float` for opacity), a developer must copy-paste one of these methods. Copy-paste is the most common source of subtle bugs in boilerplate code. With 4 copies, the surface area for a divergence bug is 4x what it needs to be.

**3. Test helper leaks temp files on disk**

**File:** `TemplateSafetyTests.cs`, lines 117-122

`Path.GetTempFileName()` creates a real zero-byte `.tmp` file. `Path.ChangeExtension()` returns a *different* path (e.g., `.docx`). `File.WriteAllBytes` writes to the new path. The original `.tmp` file is never deleted. Over CI runs, these orphan files accumulate.

**Fix:** Use `Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}")` to avoid creating the orphan file in the first place.

### Suggestions

**4. CascadeLayer enum order does not match precedence hierarchy**

The enum declares `Preset, Template, Theme, Cli` -- but the actual precedence is Template (lowest) < Preset < Theme < Cli (highest). Reordering the enum to match precedence would be self-documenting and would enable future `layer > CascadeLayer.Theme` comparisons.

**5. No test verifies Template is lower priority than Preset**

The test suite exercises CLI > Theme > Preset well, but never supplies a `Template` layer. The unique characteristic of Template is that it loses to the Preset -- a test asserting "template defines HeadingFont=X, but default preset Calibri still wins" would close this gap.

**6. Error message references `--allow-macros` flag that does not exist yet**

The `.docm` error message (line 54 of `TemplateSafetyChecker.cs`) tells users to use `--allow-macros to override`. If this flag is planned for a future issue, consider tracking it. If not, remove the reference to avoid promising nonexistent functionality.

**7. Two filesystem accesses where one would suffice**

`new FileInfo(path)` and `File.OpenRead(path)` are two separate filesystem calls. Opening a `FileStream` once and checking `stream.Length` before reading the header would be slightly cleaner, particularly for templates on network shares.

## Lessons

1. **Reflection + string keys = invisible breakage.** Anytime you couple a string literal to a property name, you have opted out of the compiler's refactoring safety net. Prefer explicit defaults over reflection-based lookups. If you must use reflection, pair it with a compile-time test that asserts the mapping is valid.

2. **Structural duplication in helpers is a maintainability multiplier.** Four methods that differ by one type parameter means four places to update, four places to introduce a divergence bug, and four places a reviewer must verify. Generics exist to solve exactly this shape of problem. The right time to consolidate is when you notice the pattern -- before the fifth copy appears.

3. **Test temp file hygiene matters for CI.** `Path.GetTempFileName()` creates a real file. If you change the extension afterward, you have two files -- the original and the renamed one. In a CI pipeline running thousands of tests per day, leaked temp files add up. Use `Guid`-based naming or ensure both files are cleaned up.

4. **Test the lowest-priority layer explicitly.** In a cascade/override system, the most common bug is getting the priority order wrong. If your tests only exercise the top 3 layers, you have no regression protection for the bottom layer's behavior. A single test that verifies "this layer loses to the one above it" is cheap insurance.

5. **Error messages are part of the API contract.** When an error message references a CLI flag (`--allow-macros`), users will try to use it. Either implement the flag or remove the reference. Dangling references in error messages erode user trust more than missing features do.
