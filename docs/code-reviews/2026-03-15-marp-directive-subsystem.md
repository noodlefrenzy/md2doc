---
agent-notes:
  ctx: "Review of MARP directive extractor, classifier, cascader"
  deps: [src/Md2.Slides/Directives/MarpDirectiveExtractor.cs, src/Md2.Slides/Directives/MarpDirectiveClassifier.cs, src/Md2.Slides/Directives/MarpDirectiveCascader.cs, src/Md2.Slides/Directives/MarpDirective.cs]
  state: active
  last: "code-reviewer@2026-03-15"
---
# Code Review: MARP Directive Subsystem (Wave 1)

**Date:** 2026-03-15
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `src/Md2.Slides/Directives/MarpDirective.cs`
- `src/Md2.Slides/Directives/MarpDirectiveExtractor.cs`
- `src/Md2.Slides/Directives/MarpDirectiveClassifier.cs`
- `src/Md2.Slides/Directives/MarpDirectiveCascader.cs`
- `tests/Md2.Slides.Tests/Directives/MarpDirectiveExtractorTests.cs`
- `tests/Md2.Slides.Tests/Directives/MarpDirectiveClassifierTests.cs`
- `tests/Md2.Slides.Tests/Directives/MarpDirectiveCascaderTests.cs`
**Verdict:** Approved with suggestions

## Context

This is the Wave 1 directive subsystem for the MARP-compatible Markdown-to-PPTX pipeline. The subsystem parses MARP directives from HTML comment blocks and YAML front matter, classifies them by scope (global/local/scoped per Marpit v3.x semantics), and cascades them across slides so that each slide gets a fully resolved set of directive values.

The architecture is clean: three static classes with clear responsibilities (extract, classify, cascade) connected by the `MarpDirective` record type. 48 tests cover the happy paths, edge cases, and guard clauses.

## Findings

### Critical

None.

### Important

**Multi-directive comment blocks only capture the first directive**

`MarpDirectiveExtractor.Extract` (line 38) calls `Regex.Match()` which returns only the first match. MARP commonly uses multi-line comment blocks containing several directives:

```html
<!--
class: lead
backgroundColor: #abc
paginate: true
-->
```

With the current implementation, only `class: lead` is extracted. The other two are silently dropped. This is a correctness bug.

The fix is to iterate over `Regex.Matches()` instead. However, there is a subtlety: the current regex uses `RegexOptions.Singleline` which makes `.` match newlines, so the `(.*?)` in the value capture group could match across line boundaries in unexpected ways when multiple directives are present in one comment. Two approaches:

1. Strip `<!--` and `-->`, split the inner content by newlines, and match each line individually with a simpler per-line regex.
2. Change the regex to anchor values to single lines (replace `.*?` with `[^\n]*`).

A corresponding test is missing -- add a test with 2-3 directives inside a single comment block.

### Suggestions

**Custom/unknown directive keys are silently dropped by the cascader**

`MarpDirectiveCascader.BuildSlideDirectives` maps a hardcoded set of keys into typed properties on `SlideDirectives`. Any key not in that set (including `backgroundPosition`, `backgroundRepeat`, `backgroundSize` which are in the classifier's known-keys list) is correctly cascaded in the internal dictionary but then discarded when building the output object.

The classifier's doc comment says "Unknown keys are still valid (passed through as custom fields)" but the cascader does not honor that contract. Consider adding a `Dictionary<string, string>? CustomDirectives` bag to `SlideDirectives`, or explicitly document that custom keys are dropped at this layer.

**Known-key set mismatch between classifier and cascader**

`MarpDirectiveClassifier.KnownDirectiveKeys` includes `backgroundPosition`, `backgroundRepeat`, `backgroundSize`, `size`, `headingDivider`, and `marp`. None of these have corresponding properties in `SlideDirectives` or mappings in `BuildSlideDirectives`. Decide whether to add them or remove them from the known-keys set to keep the layers consistent.

**Regex accepts numeric-only keys**

The regex `(_?\w+)` would match `<!-- 123: value -->`. Harmless, but could be tightened to `(_?[a-zA-Z]\w*)` for cleaner validation.

## Lessons

1. **Single match vs. multi-match is a common regex bug.** When you write a regex to extract "a thing" from text, always ask: "Can there be more than one?" `Regex.Match` vs `Regex.Matches` is a one-character-class difference that silently drops data. The tests missed this because all test inputs used one directive per comment. When testing parsers, always include an input with the maximum expected density of the thing you are parsing.

2. **Contract alignment across pipeline stages matters.** The classifier says it passes through custom keys; the cascader drops them. When building a multi-stage pipeline (extract -> classify -> cascade -> emit), each stage's output contract must match the next stage's input assumptions. A good practice: write an integration test that pushes a custom key through the full pipeline and asserts it arrives at the end.

3. **`RegexOptions.Singleline` changes `.` semantics.** With `Singleline`, `.` matches `\n`. This is often what you want for matching a block of text, but it makes `.*?` (lazy) potentially match across lines in multi-match scenarios. When working with line-oriented data inside a block, consider line-splitting first and then applying per-line patterns.

4. **Hardcoded property mapping is a maintenance trap.** `BuildSlideDirectives` has a TryGetValue call for each known property. Every time a new directive is added, two files must be updated (the `SlideDirectives` class and the `BuildSlideDirectives` method). A dictionary-based approach or attribute-driven mapping would reduce this to a single change point, but at the cost of type safety. At the current scale (7 properties) the hardcoded approach is fine, but watch for it growing.
