---
agent-notes:
  ctx: "Review of inline image and mermaid caption integration tests"
  deps: [tests/Md2.Integration.Tests/InlineImageAndMermaidCaptionTests.cs, src/Md2.Emit.Docx/DocxAstVisitor.cs]
  state: active
  last: "code-reviewer@2026-03-13"
---
# Code Review: Inline Image and Mermaid Caption Integration Tests

**Date:** 2026-03-13
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:** `tests/Md2.Integration.Tests/InlineImageAndMermaidCaptionTests.cs`
**Verdict:** Approved with suggestions

## Context

Issue #85 adds 7 integration tests covering two edge cases in DOCX emission:
1. Inline images within paragraphs must not produce invalid nested `<w:p>` inside `<w:p>` XML -- the Drawing element must be wrapped in a Run, sharing the paragraph with surrounding text.
2. Mermaid diagram blocks with empty alt text must not produce visible caption paragraphs in the output document.

The tests exercise the full pipeline (parse, transform, emit) and inspect the resulting Open XML structure directly using `DocumentFormat.OpenXml`.

## Findings

### Critical

None.

### Important

**Duplicated RunFullPipeline helper across test classes**

The `RunFullPipeline` method (lines 45-65) is a near-exact copy of the same method in `EndToEndTests.cs`. The `RunFullPipelineWithAstMutation` variant adds a single callback line. If the pipeline API changes, every test class with this copy must be updated independently.

*Why it matters:* Duplicated test infrastructure is one of the most common sources of "test rot" -- tests that fail not because behavior changed, but because the infrastructure drifted. The fix is to extract a shared helper class. The AST mutation variant can accept an optional callback parameter.

**Disposal ordering of WordprocessingDocument and MemoryStream**

Every test declares:
```csharp
using var _ = wordDoc;
using var __ = stream;
```

With `using var`, disposal happens in reverse order of declaration, so `stream` disposes before `wordDoc`. Since `wordDoc` was opened on `stream`, this creates a potential `ObjectDisposedException` during `wordDoc.Dispose()`. It works today because the doc is opened read-only and all queries complete before scope exit, but it is fragile. This is inherited from `EndToEndTests.cs`.

*Fix:* Reverse the declaration order so `wordDoc` disposes first, or switch to explicit `using` blocks.

### Suggestions

**More specific caption suppression assertion.** The test checks that the string "Mermaid diagram" is absent from document text. A more robust check would verify no caption-styled paragraph follows the image paragraph, regardless of text content. The current approach would miss a regression where a caption appears with different text.

**Missing edge case: multiple inline images in one paragraph.** All inline image tests use a single image. Testing `![a](img1) ![b](img2)` in the same paragraph would catch nesting bugs that compound with multiple images.

**Missing edge case: inline image with empty alt text.** Given that empty alt text is the mechanism for caption suppression in mermaid blocks, testing `![](path)` as a regular inline image would verify consistent behavior.

**Unused `using Markdig;` import** on line 5. Only `Markdig.Syntax` is needed.

## Lessons

1. **Test infrastructure is code too.** When you see the same 15-line setup method duplicated across test files, that is a maintenance liability. Extract shared helpers early. The cost of a small `TestHelpers` class is far lower than the cost of updating 5 copies when the API changes.

2. **Disposal order matters with layered streams.** When object A is constructed on top of stream B, always dispose A before B. With C# `using var` (which disposes in reverse declaration order), this means declaring the stream *first*. This is easy to get backwards and the bugs it causes are intermittent, making them hard to diagnose.

3. **Assert the invariant, not the symptom.** Checking that a specific string is absent ("Mermaid diagram") tests one symptom. Checking that no caption-styled paragraph exists after the image paragraph tests the actual invariant. Invariant-based assertions survive refactoring better.

4. **Compound inputs catch compound bugs.** Single-image tests verify the base case. Two images in the same paragraph test whether the fix handles the general case. Many bugs in XML generation only manifest when the same pattern repeats within a container.

5. **AST mutation for testing is a good pattern.** The `RunFullPipelineWithAstMutation` approach -- running the real pipeline but injecting state that would normally come from an external tool (Playwright/Chromium for Mermaid rendering) -- is a clean way to test integration behavior without heavyweight dependencies. This pattern is worth reusing for math/OMML blocks.
