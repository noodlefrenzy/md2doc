// agent-notes: { ctx: "PPTX emitter with full content types: text, links, tables, code, quotes, animations, slide numbers", deps: [DocumentFormat.OpenXml, Md2.Core.Slides, Md2.Core.Emit, Md2.Core.Pipeline, Markdig, Markdig.Extensions.Tables], state: active, last: "sato@2026-03-15" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Core.Ast;
using Md2.Core.Emit;
using Md2.Core.Pipeline;
using Md2.Core.Slides;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
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

    public async Task EmitAsync(SlideDocument doc, ResolvedTheme theme, EmitOptions options, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        cancellationToken.ThrowIfCancellationRequested();

        using var memStream = new MemoryStream();
        using (var presentationDoc = PresentationDocument.Create(memStream, PresentationDocumentType.Presentation, autoSave: true))
        {
            CreatePresentationStructure(presentationDoc, doc, theme, options);
        }

        memStream.Position = 0;
        await memStream.CopyToAsync(output, cancellationToken);
    }

    private static void CreatePresentationStructure(PresentationDocument presentationDoc, SlideDocument doc, ResolvedTheme theme, EmitOptions options)
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

            CreateSlide(slidePart, slide, theme, options, slideLayoutPart, slideSize, slideIndex, totalSlides);

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
        EmitOptions options, SlideLayoutPart layoutPart, Md2.Core.Slides.SlideSize slideSize, int slideNumber, int totalSlides)
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
            ApplyBackgroundImage(slideElement, slidePart, slide, slideSize, options.InputBaseDirectory);
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
                // Check if paragraph is a standalone image
                var imageLink = GetSoleImageLink(paragraph);
                if (imageLink != null && !string.IsNullOrEmpty(imageLink.Url))
                {
                    var altText = GetInlineText(imageLink);
                    var pic = CreateInlinePicture(shapeId++, slidePart, imageLink.Url, altText,
                        leftMargin, currentY, contentWidth, options.InputBaseDirectory);
                    if (pic != null)
                    {
                        shapeTree.Append(pic);
                        // Estimate height from image dimensions or default
                        currentY += 3657600L; // ~4" default
                        continue;
                    }
                }

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
                var isMermaid = string.Equals(codeBlock.Info, "mermaid", StringComparison.OrdinalIgnoreCase);
                var isChart = string.Equals(codeBlock.Info, "chart", StringComparison.OrdinalIgnoreCase);

                // Mermaid diagram handling (#138/#140)
                if (isMermaid)
                {
                    var mermaidSource = codeBlock.Lines.ToString();

                    // Try native flowchart shapes first (#138)
                    var flowchart = MermaidFlowchartParser.TryParse(mermaidSource);
                    if (flowchart != null)
                    {
                        var mermaidShapes = CreateMermaidFlowchartShapes(ref shapeId, flowchart,
                            leftMargin, currentY, contentWidth, slideHeight - currentY - 457200L, theme);
                        foreach (var s in mermaidShapes)
                            shapeTree.Append(s);
                        currentY += EstimateFlowchartHeight(flowchart, slideHeight - currentY - 457200L);
                        continue;
                    }

                    // Image fallback (#140): check for pre-rendered PNG
                    // Mermaid image paths are absolute paths from our trusted rendering cache —
                    // bypass ResolveImagePath (which rejects absolute paths for user security)
                    var imagePath = codeBlock.GetMermaidImagePath();
                    if (imagePath != null && File.Exists(imagePath))
                    {
                        var pic = CreateTrustedPicture(shapeId++, slidePart, imagePath, "Mermaid diagram",
                            leftMargin, currentY, contentWidth);
                        if (pic != null)
                        {
                            shapeTree.Append(pic);
                            currentY += 3657600L; // ~4" default
                            continue;
                        }
                    }

                    // Final fallback: render as code block
                }

                // Chart code fence handling (#141/#142)
                if (isChart)
                {
                    var chartSource = codeBlock.Lines.ToString();
                    var chartData = ChartDataParser.TryParse(chartSource);
                    if (chartData != null)
                    {
                        var chartFrame = CreateChartGraphicFrame(ref shapeId, slidePart, chartData,
                            leftMargin, currentY, contentWidth, 3200400L, theme);
                        if (chartFrame != null)
                        {
                            shapeTree.Append(chartFrame);
                            currentY += 3657600L;
                            continue;
                        }
                    }
                }

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
        Md2.Core.Slides.Slide slide, Md2.Core.Slides.SlideSize slideSize, string? baseDirectory)
    {
        var bgImage = slide.Directives.BackgroundImage;
        if (string.IsNullOrEmpty(bgImage)) return;

        // Strip url() wrapper if present (MARP format)
        if (bgImage.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            bgImage = bgImage[4..].TrimEnd(')').Trim('"', '\'', ' ');

        // Only support local file paths (not URLs)
        if (bgImage.StartsWith("http://") || bgImage.StartsWith("https://"))
            return;

        var resolvedPath = ResolveImagePath(bgImage, baseDirectory);
        if (resolvedPath == null) return;

        try
        {
            var mimeType = GetImageMimeType(resolvedPath);
            if (mimeType == null) return;

            var imagePart = slidePart.AddNewPart<ImagePart>(mimeType, "rIdBg");
            using var imageStream = File.OpenRead(resolvedPath);
            imagePart.FeedData(imageStream);

            slideElement.CommonSlideData!.Background = new Background(
                new BackgroundProperties(
                    new A.BlipFill(
                        new A.Blip { Embed = "rIdBg" },
                        new A.Stretch(new A.FillRectangle()))));
        }
        catch
        {
            // Silently skip if image can't be loaded
        }
    }

    // ── Inline images (#135) ───────────────────────────────────────────

    private static uint _imageCounter;

    /// <summary>
    /// Creates a PPTX Picture element for an inline image.
    /// Follows the same path resolution + safety pattern as DOCX ImageBuilder.
    /// </summary>
    private static P.Picture? CreateInlinePicture(uint shapeId, SlidePart slidePart,
        string imagePath, string? altText, long x, long y, long maxWidth, string? baseDirectory)
    {
        if (imagePath.StartsWith("http://") || imagePath.StartsWith("https://"))
            return null;

        var resolvedPath = ResolveImagePath(imagePath, baseDirectory);
        if (resolvedPath == null) return null;

        var mimeType = GetImageMimeType(resolvedPath);
        if (mimeType == null) return null;

        try
        {
            var imgId = $"rIdImg{Interlocked.Increment(ref _imageCounter)}";
            var imagePart = slidePart.AddNewPart<ImagePart>(mimeType, imgId);
            using var imageStream = File.OpenRead(resolvedPath);
            imagePart.FeedData(imageStream);

            var (widthEmu, heightEmu) = GetImageDimensions(resolvedPath);

            // Scale to fit within maxWidth, maintain aspect ratio
            if (widthEmu > maxWidth)
            {
                var scale = (double)maxWidth / widthEmu;
                widthEmu = maxWidth;
                heightEmu = (long)(heightEmu * scale);
            }

            return new P.Picture(
                new P.NonVisualPictureProperties(
                    new P.NonVisualDrawingProperties { Id = shapeId, Name = altText ?? $"Image {shapeId}" },
                    new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                    new ApplicationNonVisualDrawingProperties()),
                new P.BlipFill(
                    new A.Blip { Embed = imgId },
                    new A.Stretch(new A.FillRectangle())),
                new P.ShapeProperties(
                    new A.Transform2D(
                        new A.Offset { X = x, Y = y },
                        new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        }
        catch
        {
            return null;
        }
    }

    // ── Trusted image embedding (mermaid cache, not user input) ────────

    /// <summary>
    /// Creates a PPTX Picture from a trusted absolute path (e.g. mermaid rendering cache).
    /// Bypasses ResolveImagePath security checks — only use for paths generated by our pipeline.
    /// </summary>
    private static P.Picture? CreateTrustedPicture(uint shapeId, SlidePart slidePart,
        string absolutePath, string? altText, long x, long y, long maxWidth)
    {
        var mimeType = GetImageMimeType(absolutePath);
        if (mimeType == null) return null;

        try
        {
            var imgId = $"rIdImg{Interlocked.Increment(ref _imageCounter)}";
            var imagePart = slidePart.AddNewPart<ImagePart>(mimeType, imgId);
            using var imageStream = File.OpenRead(absolutePath);
            imagePart.FeedData(imageStream);

            var (widthEmu, heightEmu) = GetImageDimensions(absolutePath);
            if (widthEmu > maxWidth)
            {
                var scale = (double)maxWidth / widthEmu;
                widthEmu = maxWidth;
                heightEmu = (long)(heightEmu * scale);
            }

            return new P.Picture(
                new P.NonVisualPictureProperties(
                    new P.NonVisualDrawingProperties { Id = shapeId, Name = altText ?? $"Image {shapeId}" },
                    new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                    new ApplicationNonVisualDrawingProperties()),
                new P.BlipFill(
                    new A.Blip { Embed = imgId },
                    new A.Stretch(new A.FillRectangle())),
                new P.ShapeProperties(
                    new A.Transform2D(
                        new A.Offset { X = x, Y = y },
                        new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        }
        catch
        {
            return null;
        }
    }

    // ── Shared image utilities (following DOCX ImageBuilder pattern) ──

    /// <summary>
    /// Resolves an image path with safety checks. Same pattern as DOCX ImageBuilder.IsPathSafe.
    /// Rejects absolute paths and paths that resolve outside the base directory.
    /// </summary>
    private static string? ResolveImagePath(string imagePath, string? baseDirectory)
    {
        if (string.IsNullOrEmpty(imagePath)) return null;

        if (Path.IsPathRooted(imagePath))
            return null;

        if (!string.IsNullOrEmpty(baseDirectory))
        {
            var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, imagePath));
            var normalizedBase = Path.GetFullPath(baseDirectory);
            if (!normalizedBase.EndsWith(Path.DirectorySeparatorChar))
                normalizedBase += Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(normalizedBase, StringComparison.Ordinal))
                return null;

            return File.Exists(fullPath) ? fullPath : null;
        }

        return File.Exists(imagePath) ? Path.GetFullPath(imagePath) : null;
    }

    private const long EmuPerInch = 914400L;

    private static (long width, long height) GetImageDimensions(string imagePath)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            var header = new byte[24];
            if (stream.Read(header, 0, 24) < 24)
                return (EmuPerInch * 6, EmuPerInch * 4);

            // PNG
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                int w = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                int h = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return ((long)w * EmuPerInch / 96, (long)h * EmuPerInch / 96);
            }

            // JPEG
            if (header[0] == 0xFF && header[1] == 0xD8)
                return GetJpegDimensions(imagePath);

            return (EmuPerInch * 6, EmuPerInch * 4);
        }
        catch
        {
            return (EmuPerInch * 6, EmuPerInch * 4);
        }
    }

    private static (long width, long height) GetJpegDimensions(string path)
    {
        using var stream = File.OpenRead(path);
        stream.Position = 2;

        while (stream.Position < stream.Length)
        {
            int m1 = stream.ReadByte();
            if (m1 != 0xFF) break;
            int m2 = stream.ReadByte();
            if (m2 == -1) break;

            if (m2 >= 0xC0 && m2 <= 0xC2)
            {
                var buf = new byte[7];
                if (stream.Read(buf, 0, 7) < 7) break;
                int h = (buf[3] << 8) | buf[4];
                int w = (buf[5] << 8) | buf[6];
                return ((long)w * EmuPerInch / 96, (long)h * EmuPerInch / 96);
            }

            int lenHi = stream.ReadByte();
            int lenLo = stream.ReadByte();
            if (lenHi == -1 || lenLo == -1) break;
            stream.Position += (lenHi << 8 | lenLo) - 2;
        }

        return (EmuPerInch * 6, EmuPerInch * 4);
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

    /// <summary>
    /// Returns the LinkInline if the paragraph contains only a single image, null otherwise.
    /// </summary>
    private static LinkInline? GetSoleImageLink(ParagraphBlock paragraph)
    {
        if (paragraph.Inline == null) return null;

        LinkInline? imageLink = null;
        foreach (var child in paragraph.Inline)
        {
            if (child is LinkInline { IsImage: true } link)
            {
                if (imageLink != null) return null; // multiple images
                imageLink = link;
            }
            else if (child is LiteralInline literal && string.IsNullOrWhiteSpace(literal.Content.ToString()))
            {
                // whitespace is ok
            }
            else
            {
                return null; // non-image content
            }
        }

        return imageLink;
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

    // ── Mermaid flowchart → native shapes (#138) ─────────────────────────

    private static List<OpenXmlCompositeElement> CreateMermaidFlowchartShapes(
        ref uint shapeId, FlowchartGraph graph,
        long x, long y, long availableWidth, long availableHeight,
        ResolvedTheme theme)
    {
        var shapes = new List<OpenXmlCompositeElement>();
        var nodes = graph.Nodes.Values.ToList();
        if (nodes.Count == 0) return shapes;

        var pptx = theme.Pptx;
        var primaryColor = pptx?.PrimaryColor ?? theme.PrimaryColor;
        var secondaryColor = pptx?.SecondaryColor ?? theme.SecondaryColor;
        var textColor = pptx?.BodyTextColor ?? theme.BodyTextColor ?? "000000";
        var fontName = theme.BodyFont ?? "Calibri";
        var fontSize = (int)((pptx?.BaseFontSize ?? 16.0) * 100);

        // Layout: arrange nodes in a grid based on flow direction
        var isVertical = graph.Direction is FlowDirection.TopToBottom or FlowDirection.BottomToTop;
        var cols = isVertical ? Math.Min(nodes.Count, 4) : 1;
        var rows = isVertical ? (int)Math.Ceiling((double)nodes.Count / cols) : nodes.Count;

        if (!isVertical)
        {
            cols = nodes.Count;
            rows = 1;
        }

        // Use topological order if possible, fallback to declaration order
        var orderedNodes = TopologicalSort(nodes, graph.Edges);

        var nodeWidth = Math.Min(availableWidth / cols - 91440L, 2743200L); // max ~3"
        var nodeHeight = 548640L; // ~0.6"
        var hGap = (availableWidth - cols * nodeWidth) / Math.Max(cols, 1);
        var vGap = Math.Max((availableHeight - rows * nodeHeight) / Math.Max(rows, 1), 182880L);

        var nodePositions = new Dictionary<string, (long cx, long cy)>();

        for (int i = 0; i < orderedNodes.Count; i++)
        {
            var node = orderedNodes[i];
            int col, row;
            if (isVertical)
            {
                col = i % cols;
                row = i / cols;
            }
            else
            {
                col = i;
                row = 0;
            }

            var nx = x + col * (nodeWidth + hGap) + hGap / 2;
            var ny = y + row * (nodeHeight + vGap);

            // Center position for connector endpoints
            nodePositions[node.Id] = (nx + nodeWidth / 2, ny + nodeHeight / 2);

            var fillColor = i == 0 ? primaryColor : secondaryColor;
            var nodeTextColor = IsLightColor(fillColor) ? "333333" : "FFFFFF";

            var shape = CreateFlowchartNodeShape(shapeId++, node, nx, ny, nodeWidth, nodeHeight,
                fillColor, nodeTextColor, fontName, fontSize);
            shapes.Add(shape);
        }

        // Add connectors between nodes
        foreach (var edge in graph.Edges)
        {
            if (nodePositions.TryGetValue(edge.FromId, out var from) &&
                nodePositions.TryGetValue(edge.ToId, out var to))
            {
                var connector = CreateConnectorShape(shapeId++, from, to, edge, theme);
                shapes.Add(connector);
            }
        }

        return shapes;
    }

    private static long EstimateFlowchartHeight(FlowchartGraph graph, long availableHeight)
    {
        var nodes = graph.Nodes.Count;
        var isVertical = graph.Direction is FlowDirection.TopToBottom or FlowDirection.BottomToTop;
        var cols = isVertical ? Math.Min(nodes, 4) : nodes;
        var rows = isVertical ? (int)Math.Ceiling((double)nodes / cols) : 1;
        return Math.Min(rows * 731520L, availableHeight); // ~0.8" per row
    }

    private static P.Shape CreateFlowchartNodeShape(uint shapeId, FlowchartNode node,
        long x, long y, long width, long height,
        string fillColor, string textColor, string fontName, int fontSize)
    {
        var preset = node.Shape switch
        {
            NodeShape.Diamond => A.ShapeTypeValues.Diamond,
            NodeShape.RoundedRectangle => A.ShapeTypeValues.RoundRectangle,
            NodeShape.Circle => A.ShapeTypeValues.Ellipse,
            NodeShape.Hexagon => A.ShapeTypeValues.Hexagon,
            _ => A.ShapeTypeValues.Rectangle
        };

        var runProps = new A.RunProperties { Language = "en-US", FontSize = fontSize };
        runProps.Append(new A.LatinFont { Typeface = fontName });
        runProps.Append(new A.SolidFill(new A.RgbColorModelHex { Val = textColor.TrimStart('#') }));

        var textBody = new P.TextBody(
            new A.BodyProperties
            {
                Anchor = A.TextAnchoringTypeValues.Center,
                Wrap = A.TextWrappingValues.Square,
            },
            new A.ListStyle(),
            new A.Paragraph(
                new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Center },
                new A.Run(runProps, new A.Text(node.Label))));

        var shapeProperties = new P.ShapeProperties(
            new A.Transform2D(
                new A.Offset { X = x, Y = y },
                new A.Extents { Cx = width, Cy = height }),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = preset });

        shapeProperties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = fillColor.TrimStart('#') }));
        shapeProperties.Append(new A.Outline(
            new A.SolidFill(new A.RgbColorModelHex { Val = fillColor.TrimStart('#') }))
        { Width = 12700 });

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = shapeId, Name = $"Mermaid_{node.Id}_{shapeId}" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties()),
            shapeProperties,
            textBody);
    }

    private static P.ConnectionShape CreateConnectorShape(uint shapeId,
        (long cx, long cy) from, (long cx, long cy) to,
        FlowchartEdge edge, ResolvedTheme theme)
    {
        var lineColor = theme.Pptx?.BodyTextColor ?? theme.BodyTextColor ?? "666666";
        var lineWidth = edge.Style == EdgeStyle.Thick ? 25400 : 12700;

        var outline = new A.Outline { Width = lineWidth };
        outline.Append(new A.SolidFill(new A.RgbColorModelHex { Val = lineColor.TrimStart('#') }));
        outline.Append(new A.TailEnd { Type = A.LineEndValues.Triangle });

        if (edge.Style == EdgeStyle.Dashed)
            outline.Append(new A.PresetDash { Val = A.PresetLineDashValues.Dash });

        var shapeProperties = new P.ShapeProperties(
            new A.Transform2D(
                new A.Offset { X = Math.Min(from.cx, to.cx), Y = Math.Min(from.cy, to.cy) },
                new A.Extents
                {
                    Cx = Math.Max(Math.Abs(to.cx - from.cx), 12700L),
                    Cy = Math.Max(Math.Abs(to.cy - from.cy), 12700L)
                }),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.StraightConnector1 });

        shapeProperties.Append(outline);

        // Flip if needed
        if (to.cx < from.cx)
            shapeProperties.Elements<A.Transform2D>().First().HorizontalFlip = true;
        if (to.cy < from.cy)
            shapeProperties.Elements<A.Transform2D>().First().VerticalFlip = true;

        return new P.ConnectionShape(
            new P.NonVisualConnectionShapeProperties(
                new P.NonVisualDrawingProperties { Id = shapeId, Name = $"Connector_{shapeId}" },
                new P.NonVisualConnectorShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            shapeProperties);
    }

    private static List<FlowchartNode> TopologicalSort(List<FlowchartNode> nodes, List<FlowchartEdge> edges)
    {
        // Simple topological sort; falls back to declaration order on cycles
        var inDegree = nodes.ToDictionary(n => n.Id, _ => 0);
        var adjacency = nodes.ToDictionary(n => n.Id, _ => new List<string>());

        foreach (var edge in edges)
        {
            if (inDegree.ContainsKey(edge.ToId))
                inDegree[edge.ToId]++;
            if (adjacency.ContainsKey(edge.FromId))
                adjacency[edge.FromId].Add(edge.ToId);
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var sorted = new List<FlowchartNode>();
        var visited = new HashSet<string>();

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id)) continue;
            var node = nodes.FirstOrDefault(n => n.Id == id);
            if (node != null) sorted.Add(node);

            foreach (var neighbor in adjacency.GetValueOrDefault(id, []))
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        // Add any remaining nodes (cycles)
        foreach (var node in nodes)
            if (!visited.Contains(node.Id))
                sorted.Add(node);

        return sorted;
    }

    private static bool IsLightColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length < 6) return true;
        var r = Convert.ToInt32(hex[..2], 16);
        var g = Convert.ToInt32(hex[2..4], 16);
        var b = Convert.ToInt32(hex[4..6], 16);
        return (r * 299 + g * 587 + b * 114) / 1000 > 128;
    }

    // ── Chart → native PPTX charts (#141) ────────────────────────────────

    private static A.GraphicFrame? CreateChartGraphicFrame(ref uint shapeId, SlidePart slidePart,
        ChartData chartData, long x, long y, long width, long height, ResolvedTheme theme)
    {
        try
        {
            var chartPart = slidePart.AddNewPart<ChartPart>($"rIdChart{shapeId}");
            var palette = theme.Pptx?.ChartPalette ?? new[] { "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47" };

            var chartSpace = new C.ChartSpace(
                new C.EditingLanguage { Val = new StringValue("en-US") });

            var chart = new C.Chart();

            if (!string.IsNullOrEmpty(chartData.Title))
            {
                chart.Append(new C.Title(
                    new C.ChartText(
                        new C.RichText(
                            new A.BodyProperties(),
                            new A.ListStyle(),
                            new A.Paragraph(
                                new A.Run(
                                    new A.RunProperties { Language = "en-US", FontSize = 1400 },
                                    new A.Text(chartData.Title))))),
                    new C.Overlay { Val = false }));
            }

            var plotArea = new C.PlotArea(new C.Layout());

            // Create category axis data (shared across series)
            var categoryData = new C.CategoryAxisData(CreateStringReference(chartData.Labels));

            switch (chartData.Type)
            {
                case ChartType.Bar:
                    plotArea.Append(CreateBarChart(chartData, categoryData, palette, C.BarDirectionValues.Bar));
                    plotArea.Append(CreateCategoryAxis(100U));
                    plotArea.Append(CreateValueAxis(200U));
                    break;
                case ChartType.Column:
                    plotArea.Append(CreateBarChart(chartData, categoryData, palette, C.BarDirectionValues.Column));
                    plotArea.Append(CreateCategoryAxis(100U));
                    plotArea.Append(CreateValueAxis(200U));
                    break;
                case ChartType.Line:
                    plotArea.Append(CreateLineChart(chartData, categoryData, palette));
                    plotArea.Append(CreateCategoryAxis(100U));
                    plotArea.Append(CreateValueAxis(200U));
                    break;
                case ChartType.Pie:
                    plotArea.Append(CreatePieChart(chartData, categoryData, palette));
                    break;
            }

            chart.Append(plotArea);

            if (chartData.Series.Count > 1)
            {
                chart.Append(new C.Legend(
                    new C.LegendPosition { Val = C.LegendPositionValues.Bottom },
                    new C.Overlay { Val = false }));
            }

            chart.Append(new C.PlotVisibleOnly { Val = true });
            chartSpace.Append(chart);

            chartPart.ChartSpace = chartSpace;

            var id = shapeId++;

            return new A.GraphicFrame(
                new A.NonVisualGraphicFrameProperties(
                    new A.NonVisualDrawingProperties { Id = id, Name = $"Chart {id}" },
                    new A.NonVisualGraphicFrameDrawingProperties()),
                new A.Transform2D(
                    new A.Offset { X = x, Y = y },
                    new A.Extents { Cx = width, Cy = height }),
                new A.Graphic(
                    new A.GraphicData(
                        new C.ChartReference { Id = $"rIdChart{id}" })
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" }));
        }
        catch
        {
            return null;
        }
    }

    private static C.BarChart CreateBarChart(ChartData data, C.CategoryAxisData catData,
        IReadOnlyList<string> palette, C.BarDirectionValues direction)
    {
        var barChart = new C.BarChart(
            new C.BarDirection { Val = direction },
            new C.BarGrouping { Val = C.BarGroupingValues.Clustered });

        for (int i = 0; i < data.Series.Count; i++)
        {
            var series = data.Series[i];
            var color = palette[i % palette.Count];

            var barSeries = new C.BarChartSeries(
                new C.Index { Val = (uint)i },
                new C.Order { Val = (uint)i },
                new C.SeriesText(new C.NumericValue(series.Name)));

            barSeries.Append(new C.ChartShapeProperties(
                new A.SolidFill(new A.RgbColorModelHex { Val = color.TrimStart('#') })));

            barSeries.Append((C.CategoryAxisData)catData.CloneNode(true));
            barSeries.Append(CreateNumberValues(series.Values));

            barChart.Append(barSeries);
        }

        barChart.Append(new C.AxisId { Val = 100U });
        barChart.Append(new C.AxisId { Val = 200U });

        return barChart;
    }

    private static C.LineChart CreateLineChart(ChartData data, C.CategoryAxisData catData,
        IReadOnlyList<string> palette)
    {
        var lineChart = new C.LineChart(
            new C.Grouping { Val = C.GroupingValues.Standard });

        for (int i = 0; i < data.Series.Count; i++)
        {
            var series = data.Series[i];
            var color = palette[i % palette.Count];

            var lineSeries = new C.LineChartSeries(
                new C.Index { Val = (uint)i },
                new C.Order { Val = (uint)i },
                new C.SeriesText(new C.NumericValue(series.Name)));

            lineSeries.Append(new C.ChartShapeProperties(
                new A.Outline(
                    new A.SolidFill(new A.RgbColorModelHex { Val = color.TrimStart('#') }))
                { Width = 25400 }));

            lineSeries.Append((C.CategoryAxisData)catData.CloneNode(true));
            lineSeries.Append(CreateNumberValues(series.Values));

            lineChart.Append(lineSeries);
        }

        lineChart.Append(new C.AxisId { Val = 100U });
        lineChart.Append(new C.AxisId { Val = 200U });

        return lineChart;
    }

    private static C.PieChart CreatePieChart(ChartData data, C.CategoryAxisData catData,
        IReadOnlyList<string> palette)
    {
        var pieChart = new C.PieChart();

        // Pie chart uses first series only
        var series = data.Series[0];

        var pieSeries = new C.PieChartSeries(
            new C.Index { Val = 0U },
            new C.Order { Val = 0U },
            new C.SeriesText(new C.NumericValue(series.Name)));

        // Add data point colors from palette
        for (int p = 0; p < series.Values.Count; p++)
        {
            var dpt = new C.DataPoint(
                new C.Index { Val = (uint)p },
                new C.ChartShapeProperties(
                    new A.SolidFill(new A.RgbColorModelHex
                    {
                        Val = palette[p % palette.Count].TrimStart('#')
                    })));
            pieSeries.Append(dpt);
        }

        pieSeries.Append((C.CategoryAxisData)catData.CloneNode(true));
        pieSeries.Append(CreateNumberValues(series.Values));

        pieChart.Append(pieSeries);

        return pieChart;
    }

    private static C.CategoryAxis CreateCategoryAxis(uint axisId)
    {
        return new C.CategoryAxis(
            new C.AxisId { Val = axisId },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = C.AxisPositionValues.Bottom },
            new C.CrossingAxis { Val = 200U });
    }

    private static C.ValueAxis CreateValueAxis(uint axisId)
    {
        return new C.ValueAxis(
            new C.AxisId { Val = axisId },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = C.AxisPositionValues.Left },
            new C.CrossingAxis { Val = 100U });
    }

    private static C.StringReference CreateStringReference(IReadOnlyList<string> values)
    {
        var cache = new C.StringCache(
            new C.PointCount { Val = (uint)values.Count });

        for (int i = 0; i < values.Count; i++)
        {
            cache.Append(new C.StringPoint(
                new C.NumericValue(values[i]))
            { Index = (uint)i });
        }

        return new C.StringReference(
            new C.Formula(""),
            cache);
    }

    private static C.Values CreateNumberValues(IReadOnlyList<double> values)
    {
        var cache = new C.NumberingCache(
            new C.FormatCode("General"),
            new C.PointCount { Val = (uint)values.Count });

        for (int i = 0; i < values.Count; i++)
        {
            cache.Append(new C.NumericPoint(
                new C.NumericValue(values[i].ToString("G")))
            { Index = (uint)i });
        }

        return new C.Values(
            new C.NumberReference(
                new C.Formula(""),
                cache));
    }
}
