---
agent-notes:
  ctx: "Wave 3 review: MarpParser, PptxEmitter, CLI wiring"
  deps: [src/Md2.Slides/MarpParser.cs, src/Md2.Emit.Pptx/PptxEmitter.cs, src/Md2.Cli/ConvertCommand.cs, src/Md2.Slides/MarpSlideExtractor.cs]
  state: active
  last: "code-reviewer@2026-03-15"
---
# Code Review: PPTX Wave 3 -- MarpParser, PptxEmitter, CLI Wiring

**Date:** 2026-03-15
**Reviewed by:** Vik (simplicity), Tara (testing), Pierrot (security)
**Files reviewed:** `src/Md2.Slides/MarpParser.cs`, `src/Md2.Emit.Pptx/PptxEmitter.cs`, `src/Md2.Cli/ConvertCommand.cs`, `src/Md2.Slides/MarpSlideExtractor.cs`, `tests/Md2.Slides.Tests/MarpParserTests.cs`, `tests/Md2.Emit.Pptx.Tests/PptxEmitterTests.cs`
**Verdict:** Approved with suggestions

## Context

Wave 3 wires up the full PPTX pipeline end-to-end: MarpParser orchestrates front-matter extraction, Markdig parsing, directive handling, layout inference, and md2 extension application. PptxEmitter produces valid Open XML PPTX files with text shapes, lists, code blocks, and speaker notes. ConvertCommand gains `.pptx` extension detection to route to the slide pipeline. This is Sprint 2 scope -- text-only output, no images or theme-based styling yet.

## Findings

### Critical

No critical findings.

### Important

**1. ConvertCommand: PPTX path parses markdown twice (Vik)**
`ConvertCommand.cs` lines 176-182 parse the markdown through `ConversionPipeline` (the DOCX pipeline) regardless of output format, then at line 276 the PPTX branch parses it again through `MarpParser`. The DOCX-path `ConversionPipeline` parse is wasted work on the PPTX path -- it creates a full Markdig AST, builds `DocumentMetadata`, and logs parse timing, all of which is discarded. Move the `isPptx` branch check earlier, before the DOCX pipeline parse at line 180, so the PPTX path skips `ConversionPipeline` entirely. Beyond the wasted CPU, this also instantiates `ConversionPipeline` and its logger unnecessarily.

**2. ConvertCommand: Default output extension always `.docx` (Vik)**
`ConvertCommand.cs` line 148-149: when no `-o` is specified, the output path defaults to `.docx` via `Path.ChangeExtension(input.FullName, ".docx")`. A user running `md2 slides.md` expects `slides.docx`, which is correct for DOCX. But there is no way for a MARP-format input to auto-detect as PPTX without the user explicitly passing `-o slides.pptx`. This is a UX gap -- consider sniffing for `marp: true` in front matter or adding a `--format pptx` flag so users do not have to always specify `-o`.

**3. MarpParser: Silent catch-all on YAML parse failure (Vik, Pierrot)**
`MarpParser.cs` line 132: the bare `catch` swallows all exceptions from `YamlDeserializer.Deserialize`, returning `(null, markdown)`. This means malformed YAML front matter (typos, encoding issues, hostile input) is silently ignored. The user gets no feedback that their front matter was discarded. At minimum, log a warning. The same pattern exists in `MarpExtensionParser.cs` line 74.

**4. PptxEmitter: No CancellationToken support (Tara)**
`PptxEmitter.EmitAsync` accepts no `CancellationToken`. The DOCX path in `ConvertCommand` threads `cancellationToken` through transforms and catches `OperationCanceledException`. The PPTX path at line 288 calls `pptxEmitter.EmitAsync` without cancellation support. For large decks (50+ slides), this means Ctrl+C does not cancel the emit phase. The `ISlideEmitter` interface itself should probably accept a `CancellationToken`, but at minimum `EmitAsync` should check for cancellation between slides.

### Suggestions

**5. PptxEmitter: Fixed shape height of 400000 EMU (Vik)**
`PptxEmitter.cs` line 227: every text shape gets `Cy = 400000L` (about 0.44 inches) regardless of content length. Multi-paragraph or multi-line code blocks will overflow. This is acceptable for Sprint 2 text-only scope, but flag it as tech debt -- auto-height calculation or `txBody` auto-fit properties should be added before shipping.

**6. MarpParser: Enum.Parse for build type with no error handling (Vik)**
`MarpParser.cs` line 94: `Enum.Parse<BuildAnimationType>(ext.Build, ignoreCase: true)` will throw `ArgumentException` on unrecognized values. This surfaces as an unhandled crash with a confusing stack trace. Prefer `Enum.TryParse` with a warning log on failure.

**7. PptxEmitter: Nested lists not handled (Tara)**
`PptxEmitter.cs` lines 158-171 only walk direct `ListItemBlock` children. Nested lists (a `ListBlock` inside a `ListItemBlock`) are silently dropped. No test covers this case.

**8. Test coverage gap: empty slide (Tara)**
No test verifies behavior when a slide has zero content blocks (two consecutive `---` separators). `MarpSlideExtractor` line 57 does create an empty slide in this case, and `PptxEmitter` would produce a slide with no shapes. This should have an explicit test to document expected behavior.

**9. Test coverage gap: malformed front matter (Tara)**
No test covers what happens when YAML front matter is syntactically invalid. Given the silent `catch` in `MarpParser.ExtractFrontMatter`, there should be a test asserting the fallback behavior (body treated as full markdown, metadata empty).

## Lessons

1. **Route early, not late.** When a CLI has divergent pipelines (DOCX vs PPTX), the branch point should be as early as possible. Doing shared work "just in case" before branching wastes resources and makes the code harder to follow. The pattern to internalize: detect the output format immediately after argument resolution, then call entirely separate methods.

2. **Silent catch-all is an anti-pattern for user-facing tools.** When parsing user-authored content (YAML front matter, extension comments), swallowing all exceptions hides authoring mistakes. The user writes `tramsition: fade` instead of `transition: fade`, gets no error, and wonders why their transition does not work. Even a stderr warning costs nothing and saves debugging time.

3. **CancellationToken should thread through the entire pipeline.** If the entry point accepts cancellation (and it does -- System.CommandLine provides it), every long-running phase should respect it. Forgetting to pass it through one phase means the user experience degrades silently -- Ctrl+C appears to hang until that phase completes. Check for this at the interface level.

4. **Fixed dimensions in Open XML are deceptive.** Hard-coded EMU values work in demos but break with real content. Open XML has `txBody` auto-fit modes (`spAutoFit`, `normAutofit`) that let PowerPoint calculate height at render time. Prefer these over fixed `Cy` values whenever possible.
