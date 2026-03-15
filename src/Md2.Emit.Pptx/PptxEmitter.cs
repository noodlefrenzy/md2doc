// agent-notes: { ctx: "PPTX emitter with theme-based layouts, backgrounds, and fit headings", deps: [DocumentFormat.OpenXml, Md2.Core.Slides, Md2.Core.Emit, Md2.Core.Pipeline, Markdig], state: active, last: "sato@2026-03-15" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Core.Emit;
using Md2.Core.Pipeline;
using Md2.Core.Slides;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace Md2.Emit.Pptx;

/// <summary>
/// PPTX emitter with theme-based slide layouts, backgrounds, and font sizing.
/// Uses ResolvedTheme.Pptx sub-object for PPTX-specific styling per ADR-0016.
/// </summary>
public class PptxEmitter : ISlideEmitter
{
    public string FormatName => "pptx";

    public async Task EmitAsync(SlideDocument doc, ResolvedTheme theme, EmitOptions options, Stream output)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        using var memStream = new MemoryStream();
        using (var presentationDoc = PresentationDocument.Create(memStream, PresentationDocumentType.Presentation, autoSave: true))
        {
            CreatePresentationStructure(presentationDoc, doc, theme);
        }

        memStream.Position = 0;
        await memStream.CopyToAsync(output);
    }

    private static void CreatePresentationStructure(PresentationDocument presentationDoc, SlideDocument doc, ResolvedTheme theme)
    {
        var presentationPart = presentationDoc.AddPresentationPart();
        presentationPart.Presentation = new Presentation();

        // Use PPTX theme slide size, falling back to document metadata, then default
        var slideSize = theme.Pptx?.SlideSize ?? doc.Metadata.Size;
        presentationPart.Presentation.SlideSize = new P.SlideSize
        {
            Cx = (int)slideSize.Width,
            Cy = (int)slideSize.Height,
            Type = SlideSizeValues.Custom
        };

        // Create slide master and layout
        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");
        slideMasterPart.SlideMaster = CreateSlideMaster(theme);

        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>("rId1");
        slideLayoutPart.SlideLayout = CreateSlideLayout();
        slideLayoutPart.SlideLayout.Append(new CommonSlideData(new ShapeTree()));

        // Slide ID list
        var slideIdList = new SlideIdList();
        presentationPart.Presentation.SlideIdList = slideIdList;

        // Slide master ID list
        var slideMasterIdList = new SlideMasterIdList();
        slideMasterIdList.Append(new SlideMasterId { Id = 2147483648U, RelationshipId = "rId1" });
        presentationPart.Presentation.SlideMasterIdList = slideMasterIdList;

        // Create each slide
        uint slideId = 256;
        var slideIndex = 1;

        foreach (var slide in doc.Slides)
        {
            var relationshipId = $"rId{slideIndex + 1}";
            var slidePart = presentationPart.AddNewPart<SlidePart>(relationshipId);

            CreateSlide(slidePart, slide, theme, slideLayoutPart, slideSize);

            if (!string.IsNullOrEmpty(slide.SpeakerNotes))
                CreateSpeakerNotes(slidePart, slide.SpeakerNotes);

            slideIdList.Append(new SlideId { Id = slideId++, RelationshipId = relationshipId });
            slideIndex++;
        }

        // Document properties
        if (presentationDoc.PackageProperties != null)
        {
            if (!string.IsNullOrEmpty(doc.Metadata.Title))
                presentationDoc.PackageProperties.Title = doc.Metadata.Title;
            if (!string.IsNullOrEmpty(doc.Metadata.Author))
                presentationDoc.PackageProperties.Creator = doc.Metadata.Author;
        }
    }

    private static void CreateSlide(SlidePart slidePart, Md2.Core.Slides.Slide slide, ResolvedTheme theme,
        SlideLayoutPart layoutPart, Md2.Core.Slides.SlideSize slideSize)
    {
        slidePart.AddPart(layoutPart, "rId1");

        var slideElement = new P.Slide(
            new CommonSlideData(
                new ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(
                        new A.TransformGroup(
                            new A.Offset { X = 0L, Y = 0L },
                            new A.Extents { Cx = 0L, Cy = 0L },
                            new A.ChildOffset { X = 0L, Y = 0L },
                            new A.ChildExtents { Cx = 0L, Cy = 0L })))));

        // Apply background color (per-slide directive > per-layout theme > default theme background)
        var bgColor = ResolveBackgroundColor(slide, theme);
        if (bgColor != null)
        {
            slideElement.CommonSlideData!.Background = new Background(
                new BackgroundProperties(
                    new A.SolidFill(new A.RgbColorModelHex { Val = bgColor })));
        }

        var shapeTree = slideElement.CommonSlideData!.ShapeTree!;
        uint shapeId = 2;

        var pptx = theme.Pptx;
        long slideWidth = slideSize.Width;

        // Walk the Markdig AST and create text shapes
        long currentY = 457200L; // Start 0.5" from top
        const long leftMargin = 457200L;
        const long rightMargin = 457200L;
        long contentWidth = slideWidth - leftMargin - rightMargin;

        foreach (var block in slide.Content)
        {
            if (block is HeadingBlock heading)
            {
                var (text, isFit) = GetInlineTextWithFit(heading.Inline);
                var fontSize = GetHeadingFontSize(heading.Level, theme);
                var shape = CreateTextShape(shapeId++, text, leftMargin, currentY, contentWidth,
                    fontSize, true, theme, fitText: isFit);
                shapeTree.Append(shape);
                currentY += (long)(fontSize * 2000);
            }
            else if (block is ParagraphBlock paragraph)
            {
                var text = GetInlineText(paragraph.Inline);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var baseFontPt = pptx?.BaseFontSize ?? theme.BaseFontSize;
                    var fontSize = (int)(baseFontPt * 100);
                    var shape = CreateTextShape(shapeId++, text, leftMargin, currentY, contentWidth,
                        fontSize, false, theme);
                    shapeTree.Append(shape);
                    currentY += (long)(fontSize * 1500);
                }
            }
            else if (block is ListBlock list)
            {
                var baseFontPt = pptx?.BaseFontSize ?? theme.BaseFontSize;
                var bulletIndentEmu = (long)((pptx?.Content.BulletIndent ?? 36.0) * 12700); // pt to EMU
                var orderedIndex = int.TryParse(list.OrderedStart, out var start) ? start : 1;
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    var itemText = GetListItemText(item);
                    var bullet = list.IsOrdered ? $"{orderedIndex++}. " : "\u2022 ";
                    var fontSize = (int)(baseFontPt * 100);
                    var shape = CreateTextShape(shapeId++, bullet + itemText,
                        leftMargin + bulletIndentEmu, currentY,
                        contentWidth - bulletIndentEmu, fontSize, false, theme);
                    shapeTree.Append(shape);
                    currentY += (long)(fontSize * 1200);
                }
            }
            else if (block is FencedCodeBlock codeBlock)
            {
                var code = codeBlock.Lines.ToString();
                var codeFontPt = pptx?.CodeBlock.FontSize ?? (theme.BaseFontSize * 0.85);
                var fontSize = (int)(codeFontPt * 100);
                var shape = CreateTextShape(shapeId++, code, leftMargin, currentY, contentWidth,
                    fontSize, false, theme, isCode: true);
                shapeTree.Append(shape);
                currentY += (long)(fontSize * 1500);
            }
        }

        slidePart.Slide = slideElement;
    }

    /// <summary>
    /// Resolves the background color for a slide. Precedence:
    /// 1. Per-slide directive (slide.Directives.BackgroundColor)
    /// 2. Per-layout theme background (theme.Pptx.TitleSlide.BackgroundColor etc.)
    /// 3. Default theme background (theme.Pptx.BackgroundColor)
    /// Returns null if no background should be set (transparent/white default).
    /// </summary>
    private static string? ResolveBackgroundColor(Md2.Core.Slides.Slide slide, ResolvedTheme theme)
    {
        // Per-slide directive overrides everything
        if (!string.IsNullOrEmpty(slide.Directives.BackgroundColor))
            return slide.Directives.BackgroundColor.TrimStart('#');

        var pptx = theme.Pptx;
        if (pptx == null)
            return null;

        // Per-layout background
        var layoutBg = slide.Layout.Name switch
        {
            "title" => pptx.TitleSlide.BackgroundColor,
            "section-divider" => pptx.SectionDivider.BackgroundColor,
            _ => null
        };

        if (layoutBg != null)
            return layoutBg.TrimStart('#');

        // Default theme background (skip FFFFFF to avoid unnecessary background element)
        var defaultBg = pptx.BackgroundColor;
        if (defaultBg != null && !string.Equals(defaultBg, "FFFFFF", StringComparison.OrdinalIgnoreCase))
            return defaultBg.TrimStart('#');

        return null;
    }

    private static P.Shape CreateTextShape(
        uint shapeId, string text,
        long x, long y, long width,
        int fontSizeHundredths, bool isBold,
        ResolvedTheme theme, bool isCode = false, bool fitText = false)
    {
        var fontName = isCode ? (theme.MonoFont ?? "Consolas") : (theme.BodyFont ?? "Calibri");
        if (isBold)
            fontName = theme.HeadingFont ?? fontName;

        var runProperties = new A.RunProperties { Language = "en-US", FontSize = fontSizeHundredths, Bold = isBold };
        runProperties.Append(new A.LatinFont { Typeface = fontName });

        // Resolve text color: PPTX per-format override > shared
        var colorHex = theme.Pptx?.BodyTextColor ?? theme.BodyTextColor ?? "000000";
        runProperties.Append(new A.SolidFill(
            new A.RgbColorModelHex { Val = colorHex.TrimStart('#') }));

        var run = new A.Run(runProperties, new A.Text(text));

        var paragraph = new A.Paragraph(
            new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Left },
            run);

        var bodyProperties = new A.BodyProperties
        {
            Wrap = A.TextWrappingValues.Square,
            RightToLeftColumns = false,
            Anchor = A.TextAnchoringTypeValues.Top
        };

        if (fitText)
            bodyProperties.Append(new A.NormalAutoFit());

        var textBody = new P.TextBody(bodyProperties, new A.ListStyle(), paragraph);

        var shape = new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = shapeId, Name = $"TextBox {shapeId}" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = x, Y = y },
                    new A.Extents { Cx = width, Cy = 400000L }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }),
            textBody);

        return shape;
    }

    private static void CreateSpeakerNotes(SlidePart slidePart, string notes)
    {
        var notesSlidePart = slidePart.AddNewPart<NotesSlidePart>("rId2");

        var notesSlide = new NotesSlide(
            new CommonSlideData(
                new ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(),
                    new P.Shape(
                        new P.NonVisualShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 2U, Name = "Notes Placeholder" },
                            new P.NonVisualShapeDrawingProperties(
                                new A.ShapeLocks { NoGrouping = true }),
                            new ApplicationNonVisualDrawingProperties(
                                new PlaceholderShape { Type = PlaceholderValues.Body, Index = 1U })),
                        new P.ShapeProperties(),
                        new P.TextBody(
                            new A.BodyProperties(),
                            new A.ListStyle(),
                            new A.Paragraph(
                                new A.Run(
                                    new A.RunProperties { Language = "en-US" },
                                    new A.Text(notes))))))));

        notesSlidePart.NotesSlide = notesSlide;
    }

    private static string GetInlineText(ContainerInline? inline)
    {
        return GetInlineTextWithFit(inline).Text;
    }

    private static (string Text, bool IsFit) GetInlineTextWithFit(ContainerInline? inline)
    {
        if (inline == null) return ("", false);

        var text = new System.Text.StringBuilder();
        var isFit = false;

        foreach (var child in inline)
        {
            switch (child)
            {
                case LiteralInline literal:
                    text.Append(literal.Content);
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case EmphasisInline emphasis:
                    var (emphText, emphFit) = GetInlineTextWithFit(emphasis);
                    text.Append(emphText);
                    isFit |= emphFit;
                    break;
                case LinkInline link:
                    var (linkText, linkFit) = GetInlineTextWithFit(link);
                    text.Append(linkText);
                    isFit |= linkFit;
                    break;
                case HtmlInline html:
                    var tag = html.Tag?.Trim();
                    if (tag != null && tag.Contains("fit", StringComparison.OrdinalIgnoreCase)
                        && tag.StartsWith("<!--") && tag.EndsWith("-->"))
                    {
                        isFit = true;
                    }
                    break;
                case LineBreakInline:
                    text.Append(' ');
                    break;
            }
        }

        return (text.ToString().Trim(), isFit);
    }

    private static string GetListItemText(ListItemBlock item)
    {
        foreach (var block in item)
        {
            if (block is ParagraphBlock p)
                return GetInlineText(p.Inline);
        }
        return "";
    }

    /// <summary>
    /// Gets heading font size in hundredths of a point.
    /// Uses PPTX theme sizes (levels 1-3) when available, falls back to shared theme.
    /// </summary>
    private static int GetHeadingFontSize(int level, ResolvedTheme theme)
    {
        var pptx = theme.Pptx;
        if (pptx != null)
        {
            return level switch
            {
                1 => (int)(pptx.Heading1Size * 100),
                2 => (int)(pptx.Heading2Size * 100),
                3 => (int)(pptx.Heading3Size * 100),
                _ => (int)(pptx.BaseFontSize * 100)
            };
        }

        return level switch
        {
            1 => (int)(theme.Heading1Size * 100),
            2 => (int)(theme.Heading2Size * 100),
            3 => (int)(theme.Heading3Size * 100),
            4 => (int)(theme.Heading4Size * 100),
            5 => (int)(theme.Heading5Size * 100),
            6 => (int)(theme.Heading6Size * 100),
            _ => (int)(theme.BaseFontSize * 100)
        };
    }

    private static SlideMaster CreateSlideMaster(ResolvedTheme theme)
    {
        var csd = new CommonSlideData(
            new ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new ApplicationNonVisualDrawingProperties()),
                new GroupShapeProperties()));

        // Apply default background to slide master if theme specifies one
        var bgColor = theme.Pptx?.BackgroundColor;
        if (bgColor != null && !string.Equals(bgColor, "FFFFFF", StringComparison.OrdinalIgnoreCase))
        {
            csd.Background = new Background(
                new BackgroundProperties(
                    new A.SolidFill(new A.RgbColorModelHex { Val = bgColor.TrimStart('#') })));
        }

        return new SlideMaster(
            csd,
            new SlideLayoutIdList(
                new SlideLayoutId { Id = 2147483649U, RelationshipId = "rId1" }));
    }

    private static P.SlideLayout CreateSlideLayout()
    {
        return new P.SlideLayout(
            new CommonSlideData(
                new ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties())))
        {
            Type = SlideLayoutValues.Blank
        };
    }
}
