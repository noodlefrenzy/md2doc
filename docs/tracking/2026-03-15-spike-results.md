---
agent-notes:
  ctx: "Spike results for ADR-0014 and ADR-0015 required spikes"
  deps: [docs/adrs/0014-slide-document-ir.md, docs/adrs/0015-marp-parser-architecture.md]
  state: complete
  last: "archie@2026-03-15"
---

# Spike Results — Sprint 1

## Spike #104: AST Fragment Reparenting (ADR-0014 Required Spike #1)

**Result: PASS — reparenting works safely.**

### Findings

1. **Splitting works.** Snapshotting the block list before mutation, then calling `parent.Remove(block)` + `fragment.Add(block)` correctly reparents nodes. Do NOT iterate the MarkdownDocument directly while removing — snapshot to a `List<Block>` first.

2. **SetData/GetData annotations survive.** Annotations set via `SetData()` (syntax tokens, Mermaid paths, OMML, arbitrary keys) are stored on the node itself and survive reparenting. This validates the ADR-0014 design: transforms run on the full document first, annotations are preserved when nodes are distributed into per-slide fragments.

3. **SyntaxHighlightAnnotator works on fragments.** The annotator can run either on the full document before splitting OR on individual fragments after splitting. Both produce correct syntax tokens.

4. **Annotate-then-split (preferred pipeline order) works.** Running `SyntaxHighlightAnnotator` on the full `MarkdownDocument`, then splitting at `ThematicBreakBlock` boundaries, preserves all tokens on the correct slides. This is the preferred order per ADR-0014 principle #4.

5. **Descendants() enumeration works on fragments.** After reparenting, `Descendants<T>()` correctly enumerates all nested nodes within a fragment.

6. **Inline content preserved.** `EmphasisInline` and other inline types survive reparenting.

7. **LinkReferenceDefinitions are accessible.** They can be enumerated from the full doc before splitting. They end up on whichever slide contains them (typically the last). The MARP parser should collect them before splitting and duplicate into each slide's fragment if cross-slide references are needed.

### Key Implementation Note

When splitting, you MUST snapshot the children before mutating:

```csharp
var allBlocks = fullDoc.ToList();  // snapshot
foreach (var block in allBlocks)   // iterate snapshot
{
    parent.Remove(block);          // safe to mutate original
    fragment.Add(block);
}
```

### Decision

Proceed with Option 2 from ADR-0014 (Markdig + post-processing with AST reparenting). No fallback to re-parsing needed.

---

## Spike #105: Fragmented List Marker Detection (ADR-0015 Required Spike #1)

**Result: PASS — Markdig preserves bullet markers.**

### Findings

1. **`ListBlock.BulletType` preserves the marker character.** `-` lists report `BulletType == '-'`, `*` lists report `BulletType == '*'`, `+` lists report `BulletType == '+'`.

2. **Mixed markers produce separate `ListBlock` instances.** When consecutive list items use different markers (e.g., `- static\n* animated`), Markdig creates separate `ListBlock` instances at each marker boundary. Each has its own `BulletType`. This is exactly what we need for MARP fragmented list detection.

3. **Paragraph-separated lists are distinct.** Lists separated by blank lines are always distinct `ListBlock` instances with their own `BulletType`.

4. **Nested lists preserve markers.** Inner lists with `*` and outer lists with `-` produce separate `ListBlock` instances with correct `BulletType` values.

5. **Source text recovery via `Span.Start` works.** Even if `BulletType` were unreliable, we can recover the marker character from the original source text using `item.Span.Start`.

6. **Ordered lists are correctly distinguished.** `ListBlock.IsOrdered == true` for numbered lists.

### Decision

The `FragmentedListDetector` can rely on `ListBlock.BulletType` to distinguish animated (`*`) from static (`-`) items. Adjacent `ListBlock` instances with alternating markers represent the MARP fragmented list pattern. No custom Markdig extension needed.

### Implementation Approach

```csharp
// Detect MARP fragmented list pattern:
// Adjacent ListBlocks where some use '*' (animated) and some use '-' (static)
bool IsFragmentedList(ListBlock list) => list.BulletType == '*';
```
