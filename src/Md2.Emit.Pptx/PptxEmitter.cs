// agent-notes: { ctx: "PPTX emitter with full content types: text, links, tables, code, quotes, animations, slide numbers", deps: [DocumentFormat.OpenXml, Md2.Core.Slides, Md2.Core.Emit, Md2.Core.Pipeline, Markdig, Markdig.Extensions.Tables], state: active, last: "sato@2026-03-15" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Core.Emit;
using Md2.Core.Pipeline;
using Md2.Core.Slides;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace Md2.Emit.Pptx;

/// <summary>
/// PPTX emitter with theme-based slide layouts, backgrounds, font sizing,
/// hyperlinks, tables, blockquotes, build animations, and slide numbers.
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

        var slideSize = theme.Pptx?.SlideSize ?? doc.Metadata.Size;
        presentationPart.Presentation.SlideSize = new P.SlideSize
        {
            Cx = (int)slideSize.Width,
            Cy = (int)slideSize.Height,
            Type = SlideSizeValues.Custom
        };

        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");
        slideMasterPart.SlideMaster = CreateSlideMaster(theme);

        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>("rId1");
        slideLayoutPart.SlideLayout = CreateSlideLayout();
        slideLayoutPart.SlideLayout.Append(new CommonSlideData(new ShapeTree()));

        var slideIdList = new SlideIdList();
        presentationPart.Presentation.SlideIdList = slideIdList;

        var slideMasterIdList = new SlideMasterIdList();
        slideMasterIdList.Append(new SlideMasterId { Id = 2147483648U, RelationshipId = "rId1" });
        presentationPart.Presentation.SlideMasterIdList = slideMasterIdList;

        uint slideId = 256;
        var slideIndex = 1;
        var totalSlides = doc.Slides.Count;

        foreach (var slide in doc.Slides)
        {
            var relationshipId = $"rId{slideIndex + 1}";
            var slidePart = presentationPart.AddNewPart<SlidePart>(relationshipId);

            CreateSlide(slidePart, slide, theme, slideLayoutPart, slideSize, slideIndex, totalSlides);

            if (!string.IsNullOrEmpty(slide.SpeakerNotes))
                CreateSpeakerNotes(slidePart, slide.SpeakerNotes);

            slideIdList.Append(new SlideId { Id = slideId++, RelationshipId = relationshipId });
            slideIndex++;
        }

        if (presentationDoc.PackageProperties != null)
        {
            if (!string.IsNullOrEmpty(doc.Metadata.Title))
                presentationDoc.PackageProperties.Title = doc.Metadata.Title;
            if (!string.IsNullOrEmpty(doc.Metadata.Author))
                presentationDoc.PackageProperties.Creator = doc.Metadata.Author;
        }
    }

    private static void CreateSlide(SlidePart slidePart, Md2.Core.Slides.Slide slide, ResolvedTheme theme,
        SlideLayoutPart layoutPart, Md2.Core.Slides.SlideSize slideSize, int slideNumber, int totalSlides)
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

        // Apply background: image takes precedence over color
        if (!string.IsNullOrEmpty(slide.Directives.BackgroundImage))
        {
            ApplyBackgroundImage(slideElement, slidePart, slide, slideSize);
        }
        else
        {
            var bgColor = ResolveBackgroundColor(slide, theme);
            if (bgColor != null)
            {
                slideElement.CommonSlideData!.Background = new Background(
                    new BackgroundProperties(
                        new A.SolidFill(new A.RgbColorModelHex { Val = bgColor })));
            }
        }

        var shapeTree = slideElement.CommonSlideData!.ShapeTree!;
        uint shapeId = 2;

        var pptx = theme.Pptx;
        long slideWidth = slideSize.Width;
        long slideHeight = slideSize.Height;

        long currentY = 457200L; // 0.5"
        const long leftMargin = 457200L;
        const long rightMargin = 457200L;
        long contentWidth = slideWidth - leftMargin - rightMargin;

        // Track if this slide has build animation
        var hasBuildAnimation = slide.Build?.Type == BuildAnimationType.Bullets;
        uint animBuildId = 1;

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
                var baseFontPt = pptx?.BaseFontSize ?? theme.BaseFontSize;
                var fontSize = (int)(baseFontPt * 100);
                var shape = CreateRichTextShape(shapeId++, paragraph.Inline, leftMargin, currentY,
                    contentWidth, fontSize, theme);
                if (shape != null)
                {
                    shapeTree.Append(shape);
                    currentY += (long)(fontSize * 1500);
                }
            }
            else if (block is ListBlock list)
            {
                var baseFontPt = pptx?.BaseFontSize ?? theme.BaseFontSize;
                var bulletIndentEmu = (long)((pptx?.Content.BulletIndent ?? 36.0) * 12700);
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
                var shape = CreateCodeBlockShape(shapeId++, code, leftMargin, currentY, contentWidth,
                    fontSize, theme);
                shapeTree.Append(shape);
                currentY += (long)(fontSize * 1500);
            }
            else if (block is QuoteBlock quote)
            {
                var quoteText = GetQuoteBlockText(quote);
                var baseFontPt = pptx?.BaseFontSize ?? theme.BaseFontSize;
                var fontSize = (int)(baseFontPt * 100);
                var shape = CreateBlockquoteShape(shapeId++, quoteText, leftMargin, currentY,
                    contentWidth, fontSize, theme);
                shapeTree.Append(shape);
                currentY += (long)(fontSize * 1800);
            }
            else if (block is Table table)
            {
                var graphicFrame = CreateTableGraphicFrame(shapeId++, table, leftMargin, currentY,
                    contentWidth, theme);
                if (graphicFrame != null)
                {
                    shapeTree.Append(graphicFrame);
                    var rowCount = table.Count;
                    currentY += (long)(rowCount * 350000L + 200000L);
                }
            }
        }

        // Add header if present
        if (!string.IsNullOrEmpty(slide.Directives.Header))
        {
            var headerShape = CreateHeaderShape(shapeId++, slide.Directives.Header,
                slideWidth, theme);
            shapeTree.Append(headerShape);
        }

        // Add footer if present
        if (!string.IsNullOrEmpty(slide.Directives.Footer))
        {
            var footerShape = CreateFooterShape(shapeId++, slide.Directives.Footer,
                slideWidth, slideHeight, theme);
            shapeTree.Append(footerShape);
        }

        // Add slide number if paginate is enabled
        if (slide.Directives.Paginate == true)
        {
            var slideNumShape = CreateSlideNumberShape(shapeId++, slideNumber, totalSlides,
                slideWidth, slideHeight, theme);
            shapeTree.Append(slideNumShape);
        }

        // Add build animations for bullet lists
        if (hasBuildAnimation)
        {
            AddBuildAnimation(slideElement, shapeTree);
        }

        slidePart.Slide = slideElement;
    }

    // ── Slide number (#129) ────────────────────────────────────────────

    private static P.Shape CreateSlideNumberShape(uint shapeId, int slideNumber, int totalSlides,
        long slideWidth, long slideHeight, ResolvedTheme theme)
    {
        var text = $"{slideNumber} / {totalSlides}";
        var fontSize = 1000; // 10pt
        var colorHex = theme.Pptx?.BodyTextColor ?? theme.BodyTextColor ?? "000000";

        var runProperties = new A.RunProperties { Language = "en-US", FontSize = fontSize };
        runProperties.Append(new A.LatinFont { Typeface = theme.BodyFont ?? "Calibri" });
        runProperties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = colorHex.TrimStart('#') }));

        var run = new A.Run(runProperties, new A.Text(text));
        var paragraph = new A.Paragraph(
            new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Right },
            run);

        var textBody = new P.TextBody(
            new A.BodyProperties { Anchor = A.TextAnchoringTypeValues.Bottom },
            new A.ListStyle(),
            paragraph);

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = shapeId, Name = $"SlideNumber {shapeId}" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = slideWidth - 1828800L, Y = slideHeight - 457200L },
                    new A.Extents { Cx = 1371600L, Cy = 365760L }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }),
            textBody);
    }

    // ── Header/Footer (#128) ──────────────────────────────────────────

    private static P.Shape CreateHeaderShape(uint shapeId, string headerText,
        long slideWidth, ResolvedTheme theme)
    {
        var fontSize = 900; // 9pt
        var colorHex = theme.Pptx?.BodyTextColor ?? theme.BodyTextColor ?? "000000";
        var fontName = theme.BodyFont ?? "Calibri";

        var runProperties = new A.RunProperties { Language = "en-US", FontSize = fontSize };
        runProperties.Append(new A.LatinFont { Typeface = fontName });
        runProperties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = colorHex.TrimStart('#') }));

        var paragraph = new A.Paragraph(
            new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Left },
            new A.Run(runProperties, new A.Text(headerText)));

        var textBody = new P.TextBody(
            new A.BodyProperties { Anchor = A.TextAnchoringTypeValues.Top },
            new A.ListStyle(),
            paragraph);

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = shapeId, Name = $"Header {shapeId}" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 457200L, Y = 91440L }, // 0.5" from left, 0.1" from top
                    new A.Extents { Cx = slideWidth - 914400L, Cy = 274320L }), // 0.3" tall
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }),
            textBody);
    }

    private static P.Shape CreateFooterShape(uint shapeId, string footerText,
        long slideWidth, long slideHeight, ResolvedTheme theme)
    {
        var fontSize = 900; // 9pt
        var colorHex = theme.Pptx?.BodyTextColor ?? theme.BodyTextColor ?? "000000";
        var fontName = theme.BodyFont ?? "Calibri";

        var runProperties = new A.RunProperties { Language = "en-US", FontSize = fontSize };
        runProperties.Append(new A.LatinFont { Typeface = fontName });
        runProperties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = colorHex.TrimStart('#') }));

        var paragraph = new A.Paragraph(
            new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Left },
            new A.Run(runProperties, new A.Text(footerText)));

        var textBody = new P.TextBody(
            new A.BodyProperties { Anchor = A.TextAnchoringTypeValues.Bottom },
            new A.ListStyle(),
            paragraph);

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = shapeId, Name = $"Footer {shapeId}" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 457200L, Y = slideHeight - 457200L },
                    new A.Extents { Cx = slideWidth - 2743200L, Cy = 274320L }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }),
            textBody);
    }

    // ── Build animation (#133) ─────────────────────────────────────────

    /// <summary>
    /// Adds basic build animation timing to a slide.
    /// Creates a timing tree that makes non-title shapes appear on click.
    /// </summary>
    private static void AddBuildAnimation(P.Slide slideElement, ShapeTree shapeTree)
    {
        var shapes = shapeTree.Elements<P.Shape>().Skip(1).ToList();
        if (shapes.Count == 0) return;

        // Build a simple timing tree: root → sequence → per-shape appear
        var mainSeq = new P.CommonTimeNode
        {
            Id = 1U,
            Duration = "indefinite",
            Restart = P.TimeNodeRestartValues.Never,
            NodeType = P.TimeNodeValues.TmingRoot
        };

        var seqTnList = new P.TimeNodeList();
        uint nodeId = 2;

        foreach (var shape in shapes)
        {
            var spId = shape.NonVisualShapeProperties?.NonVisualDrawingProperties?.Id?.Value ?? 0;
            if (spId == 0) continue;

            // Each bullet gets a sequential click-to-appear
            seqTnList.Append(new P.ParallelTimeNode(
                new P.CommonTimeNode
                {
                    Id = nodeId++,
                    Duration = "1",
                    Fill = P.TimeNodeFillValues.Hold,
                    NodeType = P.TimeNodeValues.ClickEffect
                }));
        }

        mainSeq.Append(seqTnList);

        var timing = new P.Timing(
            new P.TimeNodeList(
                new P.ParallelTimeNode(mainSeq)));

        slideElement.Append(timing);
    }

    // ── Blockquote (#137) ──────────────────────────────────────────────

    private static P.Shape CreateBlockquoteShape(uint shapeId, string text,
        long x, long y, long width, int fontSizeHundredths, ResolvedTheme theme)
    {
        var fontName = theme.BodyFont ?? "Calibri";
        var quoteColor = theme.BlockquoteTextColor ?? "555555";
        var borderColor = theme.BlockquoteBorderColor ?? "4A90D9";
        var indentEmu = 228600L; // 0.25" indent for quote bar

        var runProperties = new A.RunProperties
        {
            Language = "en-US",
            FontSize = fontSizeHundredths,
            Italic = true
        };
        runProperties.Append(new A.LatinFont { Typeface = fontName });
        runProperties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = quoteColor.TrimStart('#') }));

        var run = new A.Run(runProperties, new A.Text(text));
        var paragraph = new A.Paragraph(
            new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Left },
            run);

        var textBody = new P.TextBody(
            new A.BodyProperties
            {
                Wrap = A.TextWrappingValues.Square,
                Anchor = A.TextAnchoringTypeValues.Top
            },
            new A.ListStyle(),
            paragraph);

        var shapeProperties = new P.ShapeProperties(
            new A.Transform2D(
                new A.Offset { X = x + indentEmu, Y = y },
                new A.Extents { Cx = width - indentEmu, Cy = 400000L }),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle });

        // Add left border to simulate blockquote bar
        shapeProperties.Append(new A.Outline(
            new A.SolidFill(new A.RgbColorModelHex { Val = borderColor.TrimStart('#') }))
        { Width = 38100 }); // 3pt border

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = shapeId, Name = $"Blockquote {shapeId}" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties()),
            shapeProperties,
            textBody);
    }

    private static string GetQuoteBlockText(QuoteBlock quote)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var block in quote)
        {
            if (block is ParagraphBlock p)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(GetInlineText(p.Inline));
            }
        }
        return sb.ToString();
    }

    // ── Tables (#131) ──────────────────────────────────────────────────

    private static A.GraphicFrame? CreateTableGraphicFrame(uint shapeId, Table table,
        long x, long y, long width, ResolvedTheme theme)
    {
        var rows = table.OfType<TableRow>().ToList();
        if (rows.Count == 0) return null;

        var cols = rows.Max(r => r.Count);
        if (cols == 0) return null;

        var colWidth = width / cols;
        var rowHeight = 350000L;
        var tableHeight = rows.Count * rowHeight;

        var tblGrid = new A.TableGrid();
        for (int c = 0; c < cols; c++)
            tblGrid.Append(new A.GridColumn { Width = colWidth });

        var tbl = new A.Table();
        tbl.Append(new A.TableProperties { FirstRow = true, BandRow = true });
        tbl.Append(tblGrid);

        var headerBg = theme.TableHeaderBackground ?? "1B3A5C";
        var headerFg = theme.TableHeaderForeground ?? "FFFFFF";
        var bodyText = theme.Pptx?.BodyTextColor ?? theme.BodyTextColor ?? "333333";
        var altRowBg = theme.TableAlternateRowBackground ?? "F2F2F2";

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var isHeader = row.IsHeader;
            var isAltRow = !isHeader && r % 2 == 0;

            var tblRow = new A.TableRow { Height = rowHeight };

            for (int c = 0; c < cols; c++)
            {
                var cellText = c < row.Count ? GetTableCellText(row[c] as TableCell) : "";
                var fontSize = (int)((theme.Pptx?.BaseFontSize ?? theme.BaseFontSize) * 100);
                var textColor = isHeader ? headerFg : bodyText;

                var runProps = new A.RunProperties { Language = "en-US", FontSize = fontSize, Bold = isHeader };
                runProps.Append(new A.LatinFont { Typeface = theme.BodyFont ?? "Calibri" });
                runProps.Append(new A.SolidFill(new A.RgbColorModelHex { Val = textColor.TrimStart('#') }));

                var cell = new A.TableCell(
                    new A.TextBody(
                        new A.BodyProperties(),
                        new A.ListStyle(),
                        new A.Paragraph(new A.Run(runProps, new A.Text(cellText)))),
                    new A.TableCellProperties());

                if (isHeader)
                {
                    cell.TableCellProperties!.Append(
                        new A.SolidFill(new A.RgbColorModelHex { Val = headerBg.TrimStart('#') }));
                }
                else if (isAltRow)
                {
                    cell.TableCellProperties!.Append(
                        new A.SolidFill(new A.RgbColorModelHex { Val = altRowBg.TrimStart('#') }));
                }

                tblRow.Append(cell);
            }

            tbl.Append(tblRow);
        }

        var graphicData = new A.GraphicData(tbl) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" };

        return new A.GraphicFrame(
            new A.NonVisualGraphicFrameProperties(
                new A.NonVisualDrawingProperties { Id = shapeId, Name = $"Table {shapeId}" },
                new A.NonVisualGraphicFrameDrawingProperties()),
            new A.Transform2D(
                new A.Offset { X = x, Y = y },
                new A.Extents { Cx = width, Cy = tableHeight }),
            new A.Graphic(graphicData));
    }

    private static string GetTableCellText(TableCell? cell)
    {
        if (cell == null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var block in cell)
        {
            if (block is ParagraphBlock p)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(GetInlineText(p.Inline));
            }
        }
        return sb.ToString();
    }

    // ── Code block with theme background (#132) ────────────────────────

    private static P.Shape CreateCodeBlockShape(uint shapeId, string code,
        long x, long y, long width, int fontSizeHundredths, ResolvedTheme theme)
    {
        var fontName = theme.MonoFont ?? "Consolas";
        var codeBg = theme.CodeBackgroundColor ?? "F5F5F5";
        var codeBorder = theme.CodeBlockBorderColor ?? "E0E0E0";
        var codeColor = theme.Pptx?.BodyTextColor ?? theme.BodyTextColor ?? "333333";
        var padding = (int)((theme.Pptx?.CodeBlock.Padding ?? 12.0) * 12700);

        var runProperties = new A.RunProperties { Language = "en-US", FontSize = fontSizeHundredths };
        runProperties.Append(new A.LatinFont { Typeface = fontName });
        runProperties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = codeColor.TrimStart('#') }));

        var run = new A.Run(runProperties, new A.Text(code));
        var paragraph = new A.Paragraph(
            new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Left },
            run);

        var bodyProps = new A.BodyProperties
        {
            Wrap = A.TextWrappingValues.Square,
            Anchor = A.TextAnchoringTypeValues.Top,
            LeftInset = padding,
            TopInset = padding,
            RightInset = padding,
            BottomInset = padding,
        };

        var textBody = new P.TextBody(bodyProps, new A.ListStyle(), paragraph);

        var shapeProperties = new P.ShapeProperties(
            new A.Transform2D(
                new A.Offset { X = x, Y = y },
                new A.Extents { Cx = width, Cy = 400000L }),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle });

        // Code block background fill
        shapeProperties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = codeBg.TrimStart('#') }));
        shapeProperties.Append(new A.Outline(
            new A.SolidFill(new A.RgbColorModelHex { Val = codeBorder.TrimStart('#') }))
        { Width = 12700 }); // 1pt border

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = shapeId, Name = $"Code {shapeId}" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties()),
            shapeProperties,
            textBody);
    }

    // ── Rich text with hyperlinks (#136) ───────────────────────────────

    private static P.Shape? CreateRichTextShape(uint shapeId, ContainerInline? inline,
        long x, long y, long width, int fontSizeHundredths, ResolvedTheme theme)
    {
        if (inline == null) return null;

        var colorHex = theme.Pptx?.BodyTextColor ?? theme.BodyTextColor ?? "000000";
        var linkColor = theme.LinkColor ?? "4A90D9";
        var fontName = theme.BodyFont ?? "Calibri";

        var paragraph = new A.Paragraph(
            new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Left });

        var hasContent = false;

        foreach (var child in inline)
        {
            switch (child)
            {
                case LiteralInline literal:
                {
                    var text = literal.Content.ToString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        var rp = new A.RunProperties { Language = "en-US", FontSize = fontSizeHundredths };
                        rp.Append(new A.LatinFont { Typeface = fontName });
                        rp.Append(new A.SolidFill(new A.RgbColorModelHex { Val = colorHex.TrimStart('#') }));
                        paragraph.Append(new A.Run(rp, new A.Text(text)));
                        hasContent = true;
                    }
                    break;
                }
                case LinkInline link:
                {
                    var linkText = GetInlineText(link);
                    if (!string.IsNullOrEmpty(linkText))
                    {
                        var rp = new A.RunProperties { Language = "en-US", FontSize = fontSizeHundredths };
                        rp.Append(new A.LatinFont { Typeface = fontName });
                        rp.Append(new A.SolidFill(new A.RgbColorModelHex { Val = linkColor.TrimStart('#') }));

                        if (!string.IsNullOrEmpty(link.Url))
                        {
                            rp.Append(new A.HyperlinkOnClick { Id = "", Action = "", InvalidUrl = link.Url });
                        }

                        paragraph.Append(new A.Run(rp, new A.Text(linkText)));
                        hasContent = true;
                    }
                    break;
                }
                case EmphasisInline emphasis:
                {
                    var emphText = GetInlineText(emphasis);
                    if (!string.IsNullOrEmpty(emphText))
                    {
                        var isBold = emphasis.DelimiterCount == 2 || emphasis.DelimiterChar == '*' && emphasis.DelimiterCount >= 2;
                        var isItalic = emphasis.DelimiterCount == 1;
                        var rp = new A.RunProperties
                        {
                            Language = "en-US",
                            FontSize = fontSizeHundredths,
                            Bold = isBold,
                            Italic = isItalic
                        };
                        rp.Append(new A.LatinFont { Typeface = fontName });
                        rp.Append(new A.SolidFill(new A.RgbColorModelHex { Val = colorHex.TrimStart('#') }));
                        paragraph.Append(new A.Run(rp, new A.Text(emphText)));
                        hasContent = true;
                    }
                    break;
                }
                case CodeInline code:
                {
                    var rp = new A.RunProperties { Language = "en-US", FontSize = fontSizeHundredths };
                    rp.Append(new A.LatinFont { Typeface = theme.MonoFont ?? "Consolas" });
                    rp.Append(new A.SolidFill(new A.RgbColorModelHex { Val = colorHex.TrimStart('#') }));
                    paragraph.Append(new A.Run(rp, new A.Text(code.Content)));
                    hasContent = true;
                    break;
                }
                case LineBreakInline:
                    paragraph.Append(new A.Run(
                        new A.RunProperties { Language = "en-US", FontSize = fontSizeHundredths },
                        new A.Text(" ")));
                    break;
            }
        }

        if (!hasContent) return null;

        var textBody = new P.TextBody(
            new A.BodyProperties
            {
                Wrap = A.TextWrappingValues.Square,
                Anchor = A.TextAnchoringTypeValues.Top
            },
            new A.ListStyle(),
            paragraph);

        return new P.Shape(
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
    }

    // ── Background images (#134) ─────────────────────────────────────

    /// <summary>
    /// Applies a background image to a slide if the directive specifies one.
    /// Attempts to load the image from disk relative to the working directory.
    /// </summary>
    private static void ApplyBackgroundImage(P.Slide slideElement, SlidePart slidePart,
        Md2.Core.Slides.Slide slide, Md2.Core.Slides.SlideSize slideSize)
    {
        var bgImage = slide.Directives.BackgroundImage;
        if (string.IsNullOrEmpty(bgImage)) return;

        // Strip url() wrapper if present (MARP format)
        if (bgImage.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            bgImage = bgImage[4..].TrimEnd(')').Trim('"', '\'', ' ');
        }

        // Only support local file paths (not URLs)
        if (bgImage.StartsWith("http://") || bgImage.StartsWith("https://"))
            return;

        if (!File.Exists(bgImage)) return;

        try
        {
            var mimeType = GetImageMimeType(bgImage);
            if (mimeType == null) return;

            var imagePart = slidePart.AddNewPart<ImagePart>(mimeType, "rIdBg");
            using var imageStream = File.OpenRead(bgImage);
            imagePart.FeedData(imageStream);

            // Set slide background to image fill
            var bgProps = new BackgroundProperties();
            bgProps.Append(new A.BlipFill(
                new A.Blip { Embed = "rIdBg" },
                new A.Stretch(new A.FillRectangle())));

            slideElement.CommonSlideData!.Background = new Background(bgProps);
        }
        catch
        {
            // Silently skip if image can't be loaded
        }
    }

    private static string? GetImageMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => null
        };
    }

    // ── Background resolution ──────────────────────────────────────────

    private static string? ResolveBackgroundColor(Md2.Core.Slides.Slide slide, ResolvedTheme theme)
    {
        if (!string.IsNullOrEmpty(slide.Directives.BackgroundColor))
            return slide.Directives.BackgroundColor.TrimStart('#');

        var pptx = theme.Pptx;
        if (pptx == null)
            return null;

        var layoutBg = slide.Layout.Name switch
        {
            "title" => pptx.TitleSlide.BackgroundColor,
            "section-divider" => pptx.SectionDivider.BackgroundColor,
            _ => null
        };

        if (layoutBg != null)
            return layoutBg.TrimStart('#');

        var defaultBg = pptx.BackgroundColor;
        if (defaultBg != null && !string.Equals(defaultBg, "FFFFFF", StringComparison.OrdinalIgnoreCase))
            return defaultBg.TrimStart('#');

        return null;
    }

    // ── Simple text shape ──────────────────────────────────────────────

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

        return new P.Shape(
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
    }

    // ── Speaker notes ──────────────────────────────────────────────────

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

    // ── Text extraction utilities ──────────────────────────────────────

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

    // ── Slide master/layout ────────────────────────────────────────────

    private static SlideMaster CreateSlideMaster(ResolvedTheme theme)
    {
        var csd = new CommonSlideData(
            new ShapeTree(
                new P.NonVisualGroupShapeProperties(
                    new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                    new P.NonVisualGroupShapeDrawingProperties(),
                    new ApplicationNonVisualDrawingProperties()),
                new GroupShapeProperties()));

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
