---
agent-notes:
  ctx: "Review of image caption implementation for issue #33"
  deps: [src/Md2.Emit.Docx/ImageBuilder.cs, src/Md2.Emit.Docx/DocxAstVisitor.cs, tests/Md2.Emit.Docx.Tests/ImageBuilderTests.cs]
  state: active
  last: "code-reviewer@2026-03-12"
---
# Code Review: Image Captions from Alt Text (#33)

**Date:** 2026-03-12
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `/workspaces/md2doc/src/Md2.Emit.Docx/ImageBuilder.cs`
- `/workspaces/md2doc/src/Md2.Emit.Docx/DocxAstVisitor.cs`
- `/workspaces/md2doc/tests/Md2.Emit.Docx.Tests/ImageBuilderTests.cs`

**Verdict:** Changes requested

## Context

Issue #33 adds image captions to the DOCX emitter. When an image has non-empty alt text in Markdown (`![caption](path.png)`), the emitter now generates two paragraphs: one for the image and one for a styled caption below it. The `ImageBuilder.BuildImage` return type changed from `Paragraph` to `IReadOnlyList<Paragraph>`, and both call sites in `DocxAstVisitor` were updated.

## Findings

### Critical

**1. Paragraph-inside-Paragraph: invalid OOXML from inline images (Vik)**

File: `/workspaces/md2doc/src/Md2.Emit.Docx/DocxAstVisitor.cs`, lines 529-533

When a Markdown image appears inline (e.g., `![caption](img.png)` inside a paragraph), the call chain is:

```
VisitParagraph -> VisitInlineContainer -> VisitInline -> VisitLink (IsImage=true)
```

`VisitLink` now returns `Paragraph` objects (from `BuildImage`). These flow back up through `VisitInlineContainer` and get appended as children of the enclosing paragraph at line 424:

```csharp
foreach (var element in runs)
{
    paragraph.Append(element);  // Appending Paragraph inside Paragraph
}
```

A `Paragraph` nested inside another `Paragraph` is invalid Open XML. Word will either fail to open the file, silently drop the content, or trigger its document repair mechanism. This is a document corruption bug.

The Mermaid call site at line 382 is safe because `VisitFencedCodeBlock` returns directly to block-level processing.

**Fix:** `VisitLink` needs to handle the image case differently from regular inline content. When `link.IsImage` is true, the returned paragraphs need to be surfaced to block level rather than appended as inline children. This likely requires the visitor to support a "deferred block elements" pattern, or the image-inside-paragraph case needs to extract just the drawing run and skip the caption when the image appears inline (as opposed to being the sole content of a paragraph).

### Important

**2. Test coverage gap: no integration test for inline image rendering (Tara)**

All 7 caption tests exercise `ImageBuilder.BuildImage` in isolation. None test the `DocxAstVisitor` path where `VisitLink` handles `IsImage=true`. This is exactly the path where the Critical bug above manifests. An integration test that parses `![alt](img.png)` through the full visitor pipeline and validates the resulting document structure would have caught this.

**3. Mermaid diagrams always get a caption "Mermaid diagram" (Vik)**

File: `/workspaces/md2doc/src/Md2.Emit.Docx/DocxAstVisitor.cs`, line 381

The hardcoded `altText = "Mermaid diagram"` means every Mermaid diagram now gets a caption reading "Mermaid diagram" below it. Before this change, that string was only used for accessibility (the Drawing element's `Description` attribute). Now it visually appears in the document. This is likely unintentional -- the alt text should probably be null/empty to suppress the caption, or derived from the diagram content.

**4. `BuildCaptionParagraph` bypasses `ParagraphBuilder` (Vik)**

File: `/workspaces/md2doc/src/Md2.Emit.Docx/ImageBuilder.cs`, lines 86-110

The caption paragraph is constructed directly with `new Paragraph(...)` and `new Run(...)`, bypassing the `ParagraphBuilder` that is used everywhere else in the emitter. This means:
- If `ParagraphBuilder` applies any default properties (widow control, spacing defaults, font fallbacks), captions will not get them.
- The caption styling is duplicated rather than leveraging the existing builder infrastructure.
- Future changes to default paragraph/run formatting will need to be applied in two places.

### Suggestions

**5. Stale TDD comment block in tests (Tara)**

File: `/workspaces/md2doc/tests/Md2.Emit.Docx.Tests/ImageBuilderTests.cs`, lines 83-98

The comment block says "these tests will NOT COMPILE" and "this is TDD red phase." The tests compile and pass. This comment is now misleading and should be removed or updated to describe what the tests cover.

**6. Test boilerplate could use a shared fixture (Tara)**

All 7 caption tests repeat the same pattern: create PNG, create in-memory document, build image, assert, delete PNG. A shared test fixture or helper method would reduce ~150 lines of duplicated setup/teardown. The `try/finally` with `File.Delete` pattern is also fragile -- if `CreateInMemoryDocument` throws, the temp file leaks. Consider using `IDisposable` test fixtures or `[ClassFixture]`.

**7. Caption font size calculation with non-integer `BaseFontSize` (Vik)**

File: `/workspaces/md2doc/src/Md2.Emit.Docx/ImageBuilder.cs`, line 88-89

```csharp
var captionFontSize = theme.BaseFontSize - 2;
var halfPoints = ((int)(captionFontSize * 2)).ToString();
```

When `BaseFontSize` is 11.0 (default), this produces 9.0 * 2 = 18, which is correct. But if `BaseFontSize` were, say, 10.5, the caption would be 8.5pt and `halfPoints` would truncate to 17 via `(int)` cast. This matches how other builders in the codebase handle it, so it is consistent, but worth noting that the truncation is a floor, not a round.

### Clean

**Pierrot:** No security or compliance concerns in this change. The alt text flows through Open XML SDK text properties, not raw XML construction, so injection is not a risk. No new user input surfaces, no new dependencies, no secrets handling.

## Lessons

1. **Return type changes need call-chain analysis.** When you change a method's return type, trace every caller -- and every caller's caller -- to verify the new type is handled correctly at each level. Here, the return type change from `Paragraph` to `IReadOnlyList<Paragraph>` was valid at the immediate call sites but created an invalid document structure two levels up. The compiler was satisfied because `Paragraph` is an `OpenXmlElement`, so the type system did not flag the nesting violation.

2. **Block-level vs. inline-level is a fundamental distinction in document models.** In both HTML and OOXML, you cannot nest block elements inside inline contexts. When a method returns block-level elements (paragraphs), the caller must ensure those elements end up at block level in the output tree. Mixing levels silently produces corrupt or unpredictable output. This is a class of bug that compilers and type systems rarely catch.

3. **Integration tests catch structural bugs that unit tests miss.** The `ImageBuilder` unit tests verified that `BuildImage` returns the right number of paragraphs with the right properties. But the bug is in how those paragraphs are consumed by the visitor, which only shows up when the full pipeline runs. A test that parses `![alt](img.png)` and validates the OOXML tree would have caught this immediately.

4. **Hardcoded strings that were previously invisible can become visible.** The "Mermaid diagram" alt text was originally an accessibility attribute (screen readers only). When the same string gets repurposed as visible caption text, it needs to be re-evaluated for user-facing quality.
