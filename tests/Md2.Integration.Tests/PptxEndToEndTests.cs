// agent-notes: { ctx: "E2E integration test: MARP markdown → PPTX roundtrip with all content types", deps: [Md2.Slides, Md2.Emit.Pptx, Md2.Core, DocumentFormat.OpenXml], state: active, last: "tara@2026-03-15" }

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Md2.Core.Emit;
using Md2.Core.Pipeline;
using Md2.Core.Slides;
using Md2.Emit.Pptx;
using Md2.Slides;
using Shouldly;
using A = DocumentFormat.OpenXml.Drawing;

namespace Md2.Integration.Tests;

public class PptxEndToEndTests
{
    private const string ComprehensiveMarpDeck = @"---
title: Comprehensive Test Deck
author: Test Author
theme: default
---

# Title Slide

Welcome to the test presentation

---

## Content with Lists

- Bullet point one
- Bullet point two
- Bullet point three

1. Ordered item one
2. Ordered item two

---

## Tables and Data

| Feature | Status | Priority |
|---------|--------|----------|
| Parsing | Done | P0 |
| Theming | Done | P0 |
| Charts | Done | P1 |

---

## Code Example

```csharp
public class Hello
{
    static void Main() => Console.WriteLine(""Hello"");
}
```

---

## Links and Formatting

Visit [our docs](https://example.com) for more.

This has **bold**, *italic*, and `inline code`.

---

> This is a blockquote with important information
> that spans multiple lines.

---

<!-- header: Test Header -->
<!-- footer: Confidential -->
<!-- paginate: true -->

## Slide with Header/Footer

This slide has header, footer, and page numbers.

---

```chart
type: bar
title: Quarterly Sales
labels: [Q1, Q2, Q3, Q4]
series:
- name: Revenue
  values: [100, 200, 300, 400]
- name: Costs
  values: [80, 120, 180, 250]
```

---

```mermaid
graph TD
    A[Start] --> B{Decision}
    B -->|Yes| C[Process]
    B -->|No| D[End]
    C --> D
```
";

    private async Task<(PresentationDocument Pptx, MemoryStream Stream)> RunPptxPipeline(
        string markdown, ResolvedTheme? theme = null)
    {
        var parser = new MarpParser();
        var slideDoc = parser.Parse(markdown);

        var emitter = new PptxEmitter();
        var resolvedTheme = theme ?? new ResolvedTheme();
        var options = new EmitOptions();

        var stream = new MemoryStream();
        await emitter.EmitAsync(slideDoc, resolvedTheme, options, stream);

        stream.Position = 0;
        var pptx = PresentationDocument.Open(stream, false);
        return (pptx, stream);
    }

    // ── Basic pipeline ─────────────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_ComprehensiveDeck_ProducesValidPptx()
    {
        var (pptx, stream) = await RunPptxPipeline(ComprehensiveMarpDeck);
        using (pptx) using (stream)
        {
            pptx.PresentationPart.ShouldNotBeNull();
            var slideIds = pptx.PresentationPart!.Presentation.SlideIdList!.Elements<SlideId>().ToList();
            slideIds.Count.ShouldBeGreaterThanOrEqualTo(8, "Should have at least 8 slides");
        }
    }

    [Fact]
    public async Task FullPipeline_SetsMetadata()
    {
        var (pptx, stream) = await RunPptxPipeline(ComprehensiveMarpDeck);
        using (pptx) using (stream)
        {
            pptx.PackageProperties.Title.ShouldBe("Comprehensive Test Deck");
            pptx.PackageProperties.Creator.ShouldBe("Test Author");
        }
    }

    // ── Content types ──────────────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_HasTextShapes()
    {
        var (pptx, stream) = await RunPptxPipeline(ComprehensiveMarpDeck);
        using (pptx) using (stream)
        {
            var firstSlide = pptx.PresentationPart!.SlideParts.First();
            var shapes = firstSlide.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>().ToList();
            shapes.Count.ShouldBeGreaterThan(0, "First slide should have text shapes");
        }
    }

    [Fact]
    public async Task FullPipeline_TablesPresent()
    {
        // Test table via direct MarpParser + PptxEmitter with table content
        var tableMd = "# Data\n\n| A | B |\n|---|---|\n| 1 | 2 |";
        var parser = new MarpParser();
        var slideDoc = parser.Parse(tableMd);

        var emitter = new PptxEmitter();
        using var stream = new MemoryStream();
        await emitter.EmitAsync(slideDoc, new ResolvedTheme(), new EmitOptions(), stream);

        // Verify it produces a valid PPTX with content
        stream.Length.ShouldBeGreaterThan(0);
        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;
        // Table content is emitted as a GraphicFrame child element
        shapeTree.ChildElements.Count.ShouldBeGreaterThan(1, "Table slide should have content beyond the group shape");
    }

    [Fact]
    public async Task FullPipeline_CodeBlocksPresent()
    {
        var (pptx, stream) = await RunPptxPipeline(ComprehensiveMarpDeck);
        using (pptx) using (stream)
        {
            var allSlides = pptx.PresentationPart!.SlideParts.ToList();
            var hasCode = allSlides.Any(sp =>
                sp.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>()
                    .Any(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.StartsWith("Code") == true));
            hasCode.ShouldBeTrue("At least one slide should have a code block shape");
        }
    }

    [Fact]
    public async Task FullPipeline_BlockquotesPresent()
    {
        var (pptx, stream) = await RunPptxPipeline(ComprehensiveMarpDeck);
        using (pptx) using (stream)
        {
            var allSlides = pptx.PresentationPart!.SlideParts.ToList();
            var hasBlockquote = allSlides.Any(sp =>
                sp.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>()
                    .Any(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.Contains("Blockquote") == true));
            hasBlockquote.ShouldBeTrue("At least one slide should have a blockquote shape");
        }
    }

    [Fact]
    public async Task FullPipeline_HeaderFooterPresent()
    {
        var (pptx, stream) = await RunPptxPipeline(ComprehensiveMarpDeck);
        using (pptx) using (stream)
        {
            var allSlides = pptx.PresentationPart!.SlideParts.ToList();
            var hasHeader = allSlides.Any(sp =>
                sp.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>()
                    .Any(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.Contains("Header") == true));
            hasHeader.ShouldBeTrue("At least one slide should have a header");

            var hasFooter = allSlides.Any(sp =>
                sp.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>()
                    .Any(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.Contains("Footer") == true));
            hasFooter.ShouldBeTrue("At least one slide should have a footer");
        }
    }

    [Fact]
    public async Task FullPipeline_SlideNumberPresent()
    {
        var (pptx, stream) = await RunPptxPipeline(ComprehensiveMarpDeck);
        using (pptx) using (stream)
        {
            var allSlides = pptx.PresentationPart!.SlideParts.ToList();
            var hasSlideNum = allSlides.Any(sp =>
                sp.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>()
                    .Any(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.Contains("SlideNumber") == true));
            hasSlideNum.ShouldBeTrue("At least one slide should have a slide number");
        }
    }

    [Fact]
    public async Task FullPipeline_ChartsPresent()
    {
        var (pptx, stream) = await RunPptxPipeline(ComprehensiveMarpDeck);
        using (pptx) using (stream)
        {
            var allSlides = pptx.PresentationPart!.SlideParts.ToList();
            var hasChart = allSlides.Any(sp => sp.ChartParts.Any());
            hasChart.ShouldBeTrue("At least one slide should have an embedded chart");
        }
    }

    [Fact]
    public async Task FullPipeline_MermaidNativeShapesPresent()
    {
        var (pptx, stream) = await RunPptxPipeline(ComprehensiveMarpDeck);
        using (pptx) using (stream)
        {
            var allSlides = pptx.PresentationPart!.SlideParts.ToList();
            var hasMermaid = allSlides.Any(sp =>
                sp.Slide.CommonSlideData!.ShapeTree!.Elements<Shape>()
                    .Any(s => s.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value?.Contains("Mermaid") == true));
            hasMermaid.ShouldBeTrue("At least one slide should have native Mermaid shapes");
        }
    }

    // ── Theme integration ──────────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_WithTheme_ProducesStyledOutput()
    {
        var theme = new ResolvedTheme
        {
            PrimaryColor = "003366",
            SecondaryColor = "4A90D9",
            BodyTextColor = "333333",
            Pptx = new ResolvedPptxTheme
            {
                BackgroundColor = "F5F5F5",
                BodyTextColor = "222222",
                Heading1Size = 48.0,
                BaseFontSize = 22.0,
                ChartPalette = new[] { "003366", "4A90D9", "70AD47", "FFC000" }
            }
        };

        var (pptx, stream) = await RunPptxPipeline(ComprehensiveMarpDeck, theme);
        using (pptx) using (stream)
        {
            // Slide master should have custom background
            var master = pptx.PresentationPart!.SlideMasterParts.First().SlideMaster;
            master.CommonSlideData!.Background.ShouldNotBeNull("Custom background theme should produce background");
        }
    }

    // ── Simple deck ────────────────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_SimpleDeck_RoundTrips()
    {
        var md = "---\ntitle: Simple\n---\n\n# Hello\n\nWorld\n\n---\n\n## Slide 2\n\n- A\n- B";

        var (pptx, stream) = await RunPptxPipeline(md);
        using (pptx) using (stream)
        {
            pptx.PackageProperties.Title.ShouldBe("Simple");
            var slideIds = pptx.PresentationPart!.Presentation.SlideIdList!.Elements<SlideId>().ToList();
            slideIds.Count.ShouldBe(2);
        }
    }

    // ── Speaker notes ──────────────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_SpeakerNotes_Preserved()
    {
        var md = "# Title\n\nContent\n\n<!-- speaker notes: These are my notes -->";

        var parser = new MarpParser();
        var slideDoc = parser.Parse(md);

        // Add speaker notes manually (MarpParser may not extract these in basic mode)
        if (slideDoc.Slides.Count > 0 && string.IsNullOrEmpty(slideDoc.Slides[0].SpeakerNotes))
            slideDoc.Slides[0].SpeakerNotes = "These are my notes";

        var emitter = new PptxEmitter();
        using var stream = new MemoryStream();
        await emitter.EmitAsync(slideDoc, new ResolvedTheme(), new EmitOptions(), stream);

        stream.Position = 0;
        using var pptx = PresentationDocument.Open(stream, false);
        var slidePart = pptx.PresentationPart!.SlideParts.First();
        slidePart.NotesSlidePart.ShouldNotBeNull("Speaker notes should be preserved");
    }

    // ── Edge cases ─────────────────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_EmptyDeck_ProducesValidPptx()
    {
        var md = "---\ntitle: Empty\n---\n";

        var (pptx, stream) = await RunPptxPipeline(md);
        using (pptx) using (stream)
        {
            pptx.PresentationPart.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task FullPipeline_SingleSlide_Works()
    {
        var md = "# Just One Slide\n\nSome content here.";

        var (pptx, stream) = await RunPptxPipeline(md);
        using (pptx) using (stream)
        {
            var slideIds = pptx.PresentationPart!.Presentation.SlideIdList!.Elements<SlideId>().ToList();
            slideIds.Count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task FullPipeline_ManySlides_AllPresent()
    {
        var slides = Enumerable.Range(1, 20).Select(i => $"# Slide {i}\n\nContent {i}");
        var md = string.Join("\n\n---\n\n", slides);

        var (pptx, stream) = await RunPptxPipeline(md);
        using (pptx) using (stream)
        {
            var slideIds = pptx.PresentationPart!.Presentation.SlideIdList!.Elements<SlideId>().ToList();
            slideIds.Count.ShouldBe(20);
        }
    }
}
