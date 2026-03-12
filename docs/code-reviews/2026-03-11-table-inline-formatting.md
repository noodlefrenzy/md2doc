---
agent-notes:
  ctx: "Review of inline formatting in table cells via delegate"
  deps: [src/Md2.Emit.Docx/TableBuilder.cs, src/Md2.Emit.Docx/DocxAstVisitor.cs, tests/Md2.Integration.Tests/CompositionTests.cs]
  state: active
  last: "code-reviewer@2026-03-11"
---
# Code Review: Inline Formatting in Table Cells

**Date:** 2026-03-11
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:**
- `src/Md2.Emit.Docx/TableBuilder.cs`
- `src/Md2.Emit.Docx/DocxAstVisitor.cs`
- `tests/Md2.Integration.Tests/CompositionTests.cs`

**Verdict:** Approved with suggestions

## Context

Table cells previously stripped all inline formatting (bold, italic, code, links, strikethrough) by extracting plain text via `ExtractInlineText`. This change introduces an `InlineVisitorDelegate` -- a callback from `TableBuilder` into `DocxAstVisitor.VisitInline` -- so that table cells get the same rich inline rendering as body paragraphs.

The approach adds a new delegate type, a second constructor overload on `TableBuilder`, a `BuildElementsFromInline` dispatch method, an `ApplyHeaderOverrides` method for header-row styling, and retains the old plain-text path as `BuildRunFromInlineFallback` for backward compatibility with unit tests.

14 new integration tests in `CompositionTests.cs` validate bold, italic, strikethrough, inline code, hyperlinks, combined formatting, mixed plain+formatted content, column widths, and text preservation through the full pipeline.

## Findings

### Critical

None.

### Important

**1. Circular reference between visitor and builder via delegate**

`DocxAstVisitor` creates `TableBuilder` and passes its own `VisitInline` method as a delegate. `TableBuilder` then calls back into the visitor during rendering. This creates a circular ownership pattern: visitor owns builder, builder calls visitor.

Why this matters: This is not a bug, but it is surprising. The delegate silently closes over `DocxAstVisitor`'s mutable state (`_mainDocumentPart`, `_theme`). If the builder is ever reused across documents or if the visitor's lifetime changes, this closure becomes a source of stale-state bugs. The `private` visibility of `VisitInline` further obscures the coupling -- nothing in the method's declaration signals that it is called from outside the class.

Fix: Make `VisitInline` `internal` and add a comment on `TableBuilder._inlineVisitor` noting the back-reference to the owning visitor.

**2. `isHeader` passed as `bold` parameter is a semantic mismatch**

In `BuildElementsFromInline`, the delegate is called as `_inlineVisitor(inline, isHeader, false, false)`. The second parameter maps to `bold` in the delegate signature. Then `ApplyHeaderOverrides` also forces Bold on every run. The result is correct (header text is bold), but the intent is unclear -- two independent mechanisms both set bold, and the guard `if (runProps.Bold == null)` silently deduplicates them.

Why this matters: A developer modifying header behavior needs to understand both paths. If someone changes the delegate call to pass `bold=false` thinking `ApplyHeaderOverrides` handles it, that is correct. If someone removes `ApplyHeaderOverrides` thinking the delegate handles it, header text loses its white color override. The coupling between these two mechanisms is not documented.

Fix: Either (a) always pass `bold=false` to the delegate and let `ApplyHeaderOverrides` be the single source of truth, or (b) add a comment explaining why both paths exist.

**3. No test coverage for header cells with inline formatting**

`ApplyHeaderOverrides` (lines 216-241) is the most complex new code in this diff. It walks the element tree, forces Bold and white Color on every Run, and handles the case where runs already have a Color property (hyperlink runs). None of the 14 composition tests exercise this path for header cells. All formatting tests use data rows.

Why this matters: The header styling path has distinct logic (forced bold, forced white color, special handling for existing Color properties on hyperlink runs). If this code regresses, no test will catch it. Header cells with links are especially interesting because the white-color override must reach inside Hyperlink elements.

Fix: Add tests for (a) header cell with bold text verifying white color, (b) header cell with a hyperlink verifying the link run gets white color, (c) header cell with italic verifying italic is preserved alongside forced bold.

**4. Image links inside table cells could produce invalid Open XML**

`VisitLink` returns a `Paragraph` element for image links. When called via the delegate inside `BuildCellParagraph`, this Paragraph gets appended as a child of the cell's existing Paragraph (line 181). A Paragraph nested inside a Paragraph is invalid in the Open XML schema and could cause document corruption or rendering issues in Word.

Why this matters: While images in table cells may be uncommon in this project's use cases, the code path exists and is silently reachable. A user writing `![screenshot](path.png)` inside a table cell would get a corrupt document with no error message.

Fix: Either (a) handle the image case specially in `BuildElementsFromInline` by detecting Paragraph-typed elements and appending them as siblings rather than children, or (b) add a guard that skips image inlines inside table cells with a TODO, and add a test documenting the limitation.

**5. Fallback path is dead code in production**

`BuildRunFromInlineFallback` exists solely so existing unit tests in `TableBuilderTests.cs` (which construct `TableBuilder` without a delegate) continue to pass. Production always passes the delegate. This means unit tests are testing a code path users never hit, and the production code path is only tested through integration tests.

Why this matters: The test pyramid is inverted for table inline formatting. Unit tests cover the fallback; integration tests cover production. If someone "fixes" a bug in the fallback path thinking it affects production, their fix has no effect.

Fix: Either update `TableBuilderTests` to pass a delegate (or a simple mock/stub), or mark the fallback with `[Obsolete("Test-only path, see #NNN")]` and create a tracking issue.

### Suggestions

**6. Unnecessary `ToList()` materialization (TableBuilder.cs:197)**

`_inlineVisitor(...).ToList()` eagerly materializes results. For data cells (non-header), the list is only iterated once. The `ToList()` is only needed for header cells where `ApplyHeaderOverrides` mutates the elements. Consider conditionally materializing only when `isHeader` is true.

**7. Single-element array allocation in `ApplyHeaderOverrides` (TableBuilder.cs:220)**

`element is Run r ? new[] { r } : element.Descendants<Run>()` allocates a one-element array per Run. A minor allocation that could be avoided with an `if`/`else` pattern.

**8. Comment the `GetDataCells` header-skip assumption (CompositionTests.cs:48)**

The helper assumes exactly one header row (standard for Markdown tables). A one-line comment would prevent confusion.

**9. Unrelated changes in the diff**

The diff includes `.devcontainer/devcontainer.json` (bind mount) and `ConvertCommand.cs` (`SmartTypographyTransform` registration). These should be separate commits per the project's one-commit-per-issue convention.

## Lessons

1. **Delegates as dependency inversion have a documentation cost.** Using a delegate to break a direct dependency (TableBuilder does not import DocxAstVisitor) is a well-known pattern, and it is the right call here. But unlike an interface, a delegate does not make the contract visible in the type system. When the delegate closes over mutable state (as it does here, capturing `_mainDocumentPart`), the coupling is real but invisible. Always document what the delegate closes over and what invariants the caller assumes.

2. **"Two paths, one correct" is a maintenance trap.** When the fallback path exists only for tests and the production path is only tested through integration tests, you have a gap. The fix is not to remove the fallback (it serves test isolation) but to ensure unit tests also cover the production path, even if through a simple stub delegate. A good rule: if you add a new code path, at least one unit test should exercise it directly.

3. **Header styling in tables is deceptively complex.** Headers need forced bold, forced color (for visibility on dark backgrounds), and must handle elements that already carry formatting (hyperlink runs with their own color). The `ApplyHeaderOverrides` tree-walk pattern is correct, but it is the kind of code that breaks silently -- a wrong color on a dark background is invisible in automated tests unless you assert the color value explicitly. When writing table-related code, always test header and data rows separately.

4. **Images inside table cells are a cross-cutting edge case.** When you wire a general-purpose visitor (which can emit Paragraphs, Runs, Hyperlinks, or images) into a specific context (a table cell that expects Run-level elements), you inherit all the visitor's output types, including ones that are invalid in the new context. Always enumerate what the delegate can return and verify each return type is valid in the calling context.

5. **Integration tests and unit tests serve different purposes in a pipeline architecture.** The 14 composition tests are valuable because they catch wiring bugs that unit tests cannot (e.g., "did we actually pass the delegate?"). But they are slow and opaque -- when one fails, you cannot tell whether the bug is in parsing, the visitor, the builder, or the theme. Maintain both layers: unit tests for each builder's logic, integration tests for the wiring between them.
