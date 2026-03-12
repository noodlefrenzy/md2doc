// agent-notes: { ctx: "Walks Markdig AST, dispatches to builders for paragraphs, tables, lists, images, thematic breaks", deps: [ParagraphBuilder, TableBuilder, ListBuilder, ImageBuilder, Markdig, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-12" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Core.Ast;
using Md2.Core.Pipeline;

using Markdig.Extensions.DefinitionLists;
using Markdig.Extensions.Footnotes;
using Md2.Parsing;

using MdFootnote = Markdig.Extensions.Footnotes.Footnote;

using MdTable = Markdig.Extensions.Tables.Table;

namespace Md2.Emit.Docx;

public class DocxAstVisitor
{
    private readonly ParagraphBuilder _paragraphBuilder;
    private readonly TableBuilder _tableBuilder;
    private readonly ListBuilder _listBuilder;
    private readonly ImageBuilder _imageBuilder;
    private readonly CodeBlockBuilder _codeBlockBuilder;
    private readonly MainDocumentPart _mainDocumentPart;
    private readonly ResolvedTheme _theme;

    public DocxAstVisitor(ParagraphBuilder paragraphBuilder, MainDocumentPart mainDocumentPart, ResolvedTheme theme)
    {
        _paragraphBuilder = paragraphBuilder;
        _tableBuilder = new TableBuilder(paragraphBuilder, VisitInline);
        _listBuilder = new ListBuilder(paragraphBuilder, mainDocumentPart);
        _imageBuilder = new ImageBuilder(paragraphBuilder);
        _codeBlockBuilder = new CodeBlockBuilder(paragraphBuilder);
        _mainDocumentPart = mainDocumentPart;
        _theme = theme;
    }

    // Keep backward-compatible constructor
    public DocxAstVisitor(ParagraphBuilder paragraphBuilder, MainDocumentPart mainDocumentPart)
        : this(paragraphBuilder, mainDocumentPart, ResolvedTheme.CreateDefault())
    {
    }

    public IEnumerable<OpenXmlElement> Visit(MarkdownDocument document)
    {
        var elements = new List<OpenXmlElement>();

        foreach (var block in document)
        {
            var blockElements = VisitBlock(block);
            elements.AddRange(blockElements);
        }

        return elements;
    }

    private IEnumerable<OpenXmlElement> VisitBlock(Markdig.Syntax.Block block)
    {
        return block switch
        {
            HeadingBlock heading => VisitHeading(heading),
            ParagraphBlock paragraph => VisitParagraph(paragraph),
            MdTable table => VisitTable(table),
            ListBlock list => VisitList(list),
            MathBlock mathBlock => VisitMathBlock(mathBlock),
            FencedCodeBlock fencedCode => VisitFencedCodeBlock(fencedCode),
            QuoteBlock quote => VisitQuoteBlock(quote, 0),
            AdmonitionBlock admonition => VisitAdmonition(admonition),
            DefinitionList defList => VisitDefinitionList(defList),
            FootnoteGroup footnoteGroup => VisitFootnoteGroup(footnoteGroup),
            ThematicBreakBlock => VisitThematicBreak(),
            _ => Enumerable.Empty<OpenXmlElement>()
        };
    }

    private IEnumerable<OpenXmlElement> VisitTable(MdTable table)
    {
        var availableWidthTwips = (int)(_theme.PageWidth - _theme.MarginLeft - _theme.MarginRight);
        var built = _tableBuilder.Build(table, _theme, availableWidthTwips);
        return new OpenXmlElement[] { built };
    }

    private IEnumerable<OpenXmlElement> VisitList(ListBlock list)
    {
        return _listBuilder.Build(list);
    }

    private static uint _bookmarkId = 1;

    private IEnumerable<OpenXmlElement> VisitFootnoteGroup(FootnoteGroup group)
    {
        var elements = new List<OpenXmlElement>();

        // Separator line
        var separator = new Paragraph(
            new ParagraphProperties(
                new WidowControl(),
                new ParagraphBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Space = 1, Color = _theme.BodyTextColor }
                ),
                new SpacingBetweenLines { Before = "360" }
            )
        );
        elements.Add(separator);

        // Render each footnote
        foreach (var block in group)
        {
            if (block is MdFootnote footnote)
            {
                var footnotePara = _paragraphBuilder.CreateBodyParagraph();
                var bookmarkName = $"footnote_{footnote.Order}";
                var backBookmarkName = $"footnote_ref_{footnote.Order}";

                var bmId = Interlocked.Increment(ref _bookmarkId);

                // Add bookmark for this footnote
                footnotePara.Append(new BookmarkStart { Id = bmId.ToString(), Name = bookmarkName });

                // Superscript number
                var numRun = new Run(
                    new RunProperties(
                        new RunFonts { Ascii = _theme.BodyFont, HighAnsi = _theme.BodyFont },
                        new FontSize { Val = ((int)(_theme.BaseFontSize * 2 * 0.75)).ToString() },
                        new VerticalTextAlignment { Val = VerticalPositionValues.Superscript },
                        new Color { Val = _theme.LinkColor }
                    ),
                    new Text(footnote.Order.ToString()) { Space = SpaceProcessingModeValues.Preserve }
                );
                footnotePara.Append(numRun);
                footnotePara.Append(new BookmarkEnd { Id = bmId.ToString() });

                // Space after number
                footnotePara.Append(_paragraphBuilder.CreateRun(" "));

                // Footnote content
                foreach (var child in footnote)
                {
                    if (child is ParagraphBlock paraBlock && paraBlock.Inline != null)
                    {
                        var runs = VisitInlineContainer(paraBlock.Inline, false, false, false);
                        foreach (var run in runs)
                            footnotePara.Append(run);
                    }
                }

                elements.Add(footnotePara);
            }
        }

        return elements;
    }

    private IEnumerable<OpenXmlElement> VisitFootnoteLink(FootnoteLink link)
    {
        if (link.IsBackLink)
            return Enumerable.Empty<OpenXmlElement>();

        var footnoteNum = link.Footnote.Order.ToString();
        var bookmarkName = $"footnote_{link.Footnote.Order}";
        var refBookmarkName = $"footnote_ref_{link.Footnote.Order}";

        var bmId = Interlocked.Increment(ref _bookmarkId);

        var elements = new List<OpenXmlElement>();

        // Bookmark for back-navigation
        elements.Add(new BookmarkStart { Id = bmId.ToString(), Name = refBookmarkName });

        // Superscript reference number
        var run = new Run(
            new RunProperties(
                new RunFonts { Ascii = _theme.BodyFont, HighAnsi = _theme.BodyFont },
                new FontSize { Val = ((int)(_theme.BaseFontSize * 2 * 0.75)).ToString() },
                new VerticalTextAlignment { Val = VerticalPositionValues.Superscript },
                new Color { Val = _theme.LinkColor }
            ),
            new Text(footnoteNum) { Space = SpaceProcessingModeValues.Preserve }
        );
        elements.Add(run);
        elements.Add(new BookmarkEnd { Id = bmId.ToString() });

        return elements;
    }

    private IEnumerable<OpenXmlElement> VisitDefinitionList(DefinitionList defList)
    {
        var elements = new List<OpenXmlElement>();

        foreach (var block in defList)
        {
            if (block is DefinitionItem item)
            {
                foreach (var child in item)
                {
                    if (child is DefinitionTerm term)
                    {
                        // Term: bold, no indent
                        var termText = ExtractPlainText(term);
                        var para = _paragraphBuilder.CreateBodyParagraph();
                        para.Append(_paragraphBuilder.CreateRun(termText, bold: true));
                        var props = para.ParagraphProperties!;
                        props.Append(new SpacingBetweenLines { After = "60" });
                        elements.Add(para);
                    }
                    else if (child is ParagraphBlock paragraphBlock)
                    {
                        // Definition: indented
                        var para = _paragraphBuilder.CreateBodyParagraph();
                        var props = para.ParagraphProperties!;
                        props.Append(new Indentation { Left = _theme.BlockquoteIndentTwips.ToString() });

                        if (paragraphBlock.Inline != null)
                        {
                            var runs = VisitInlineContainer(paragraphBlock.Inline, false, false, false);
                            foreach (var run in runs)
                                para.Append(run);
                        }
                        elements.Add(para);
                    }
                }
            }
        }

        return elements;
    }

    private IEnumerable<OpenXmlElement> VisitAdmonition(AdmonitionBlock admonition)
    {
        var elements = new List<OpenXmlElement>();
        var borderColor = GetAdmonitionColor(admonition.AdmonitionType);
        var label = admonition.Title ?? CapitalizeFirst(admonition.AdmonitionType);

        // Label paragraph
        var labelPara = _paragraphBuilder.CreateBodyParagraph();
        var labelProps = labelPara.ParagraphProperties!;
        labelProps.Append(new ParagraphBorders(
            new LeftBorder { Val = BorderValues.Single, Size = 24, Space = 4, Color = borderColor }
        ));
        labelProps.Append(new Indentation { Left = _theme.BlockquoteIndentTwips.ToString() });
        labelProps.Append(new SpacingBetweenLines { After = "60" });

        var labelRun = _paragraphBuilder.CreateRun(label, bold: true);
        if (labelRun.RunProperties != null)
        {
            var existingColor = labelRun.RunProperties.Color;
            if (existingColor != null)
                existingColor.Val = borderColor;
            else
                labelRun.RunProperties.Append(new Color { Val = borderColor });
        }
        labelPara.Append(labelRun);
        elements.Add(labelPara);

        // Content paragraphs
        foreach (var block in admonition)
        {
            if (block is ParagraphBlock paragraphBlock)
            {
                var paragraph = _paragraphBuilder.CreateBodyParagraph();
                var props = paragraph.ParagraphProperties!;
                props.Append(new ParagraphBorders(
                    new LeftBorder { Val = BorderValues.Single, Size = 24, Space = 4, Color = borderColor }
                ));
                props.Append(new Indentation { Left = _theme.BlockquoteIndentTwips.ToString() });

                if (paragraphBlock.Inline != null)
                {
                    var runs = VisitInlineContainer(paragraphBlock.Inline, false, false, false);
                    foreach (var run in runs)
                    {
                        paragraph.Append(run);
                    }
                }
                elements.Add(paragraph);
            }
            else
            {
                var innerElements = VisitBlock(block);
                elements.AddRange(innerElements);
            }
        }

        return elements;
    }

    private static string GetAdmonitionColor(string admonitionType)
    {
        return admonitionType.ToLowerInvariant() switch
        {
            "note" => "4A90D9",      // blue
            "tip" => "28A745",        // green
            "warning" => "FFC107",    // amber
            "important" => "E83E8C",  // magenta
            "caution" => "DC3545",    // red
            _ => "6C757D"             // gray
        };
    }

    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s[1..];
    }

    private IEnumerable<OpenXmlElement> VisitQuoteBlock(QuoteBlock quote, int nestingLevel)
    {
        var elements = new List<OpenXmlElement>();
        var indentTwips = _theme.BlockquoteIndentTwips * (nestingLevel + 1);

        foreach (var block in quote)
        {
            if (block is QuoteBlock nestedQuote)
            {
                elements.AddRange(VisitQuoteBlock(nestedQuote, nestingLevel + 1));
            }
            else if (block is ParagraphBlock paragraphBlock)
            {
                var paragraph = _paragraphBuilder.CreateBodyParagraph();

                // Add blockquote styling: left border, indentation, italic
                var props = paragraph.ParagraphProperties!;
                props.Append(new ParagraphBorders(
                    new LeftBorder
                    {
                        Val = BorderValues.Single,
                        Size = 18,
                        Space = 4,
                        Color = _theme.BlockquoteBorderColor
                    }
                ));
                props.Append(new Indentation { Left = indentTwips.ToString() });

                if (paragraphBlock.Inline != null)
                {
                    var runs = VisitInlineContainer(paragraphBlock.Inline, false, true, false);
                    foreach (var run in runs)
                    {
                        // Override color on runs
                        if (run is Run r)
                        {
                            var runProps = r.RunProperties;
                            if (runProps != null)
                            {
                                var color = runProps.Color;
                                if (color != null)
                                    color.Val = _theme.BlockquoteTextColor;
                                else
                                    runProps.Append(new Color { Val = _theme.BlockquoteTextColor });
                            }
                        }
                        paragraph.Append(run);
                    }
                }

                elements.Add(paragraph);
            }
            else
            {
                // Other block types inside blockquote (lists, code blocks, etc.)
                var innerElements = VisitBlock(block);
                elements.AddRange(innerElements);
            }
        }

        return elements;
    }

    private IEnumerable<OpenXmlElement> VisitFencedCodeBlock(FencedCodeBlock codeBlock)
    {
        // Check if this is a mermaid block with a rendered image
        var mermaidPath = codeBlock.GetMermaidImagePath();
        if (mermaidPath != null && File.Exists(mermaidPath))
        {
            var altText = "Mermaid diagram";
            var imageParagraphs = _imageBuilder.BuildImage(_mainDocumentPart, mermaidPath, altText, _theme);
            return imageParagraphs.ToArray<OpenXmlElement>();
        }

        var code = string.Join("\n", codeBlock.Lines);
        var language = codeBlock.Info;
        var syntaxTokens = codeBlock.GetSyntaxTokens();
        var table = _codeBlockBuilder.Build(code, language, _theme, syntaxTokens);
        return new OpenXmlElement[] { table };
    }

    private IEnumerable<OpenXmlElement> VisitThematicBreak()
    {
        return new[] { _paragraphBuilder.CreateThematicBreak() };
    }

    private IEnumerable<OpenXmlElement> VisitHeading(HeadingBlock heading)
    {
        var level = heading.Level;

        // For headings with complex inline content, build the paragraph with inline visitor
        if (heading.Inline != null && HasComplexInlines(heading.Inline))
        {
            var paragraph = CreateHeadingParagraphWithInlines(level, heading.Inline);
            return new[] { paragraph };
        }

        // Simple heading: extract plain text
        var text = ExtractPlainText(heading);
        var para = _paragraphBuilder.CreateHeadingParagraph(level, text);
        return new[] { para };
    }

    private IEnumerable<OpenXmlElement> VisitParagraph(ParagraphBlock paragraphBlock)
    {
        var paragraph = _paragraphBuilder.CreateBodyParagraph();

        if (paragraphBlock.Inline != null)
        {
            var runs = VisitInlineContainer(paragraphBlock.Inline, false, false, false);
            foreach (var element in runs)
            {
                paragraph.Append(element);
            }
        }

        return new[] { paragraph };
    }

    private Paragraph CreateHeadingParagraphWithInlines(int level, ContainerInline container)
    {
        var styleId = $"Heading{level}";
        var fontSize = new ResolvedTheme().GetHeadingSize(level); // TODO: pass theme through
        var halfPoints = ((int)(fontSize * 2)).ToString();

        // Create heading paragraph with style and widow control
        var paragraph = _paragraphBuilder.CreateHeadingParagraph(level, "");

        // Remove the placeholder run
        var placeholderRun = paragraph.GetFirstChild<Run>();
        if (placeholderRun != null)
        {
            paragraph.RemoveChild(placeholderRun);
        }

        // Add inline-derived runs with heading formatting
        var runs = VisitInlineContainer(container, false, false, false);
        foreach (var element in runs)
        {
            paragraph.Append(element);
        }

        return paragraph;
    }

    private IEnumerable<OpenXmlElement> VisitInlineContainer(ContainerInline container, bool bold, bool italic, bool strikethrough)
    {
        var elements = new List<OpenXmlElement>();

        foreach (var inline in container)
        {
            var inlineElements = VisitInline(inline, bold, italic, strikethrough);
            elements.AddRange(inlineElements);
        }

        return elements;
    }

    private IEnumerable<OpenXmlElement> VisitInline(Markdig.Syntax.Inlines.Inline inline, bool bold, bool italic, bool strikethrough)
    {
        return inline switch
        {
            MathInline mathInline => VisitMathInline(mathInline),
            LiteralInline literal => VisitLiteral(literal, bold, italic, strikethrough),
            EmphasisInline emphasis => VisitEmphasis(emphasis, bold, italic, strikethrough),
            CodeInline code => VisitCodeInline(code),
            LinkInline link => VisitLink(link, bold, italic, strikethrough),
            FootnoteLink footnoteLink => VisitFootnoteLink(footnoteLink),
            LineBreakInline => VisitLineBreak(),
            _ => Enumerable.Empty<OpenXmlElement>()
        };
    }

    private IEnumerable<OpenXmlElement> VisitLiteral(LiteralInline literal, bool bold, bool italic, bool strikethrough)
    {
        var text = literal.Content.ToString();
        if (string.IsNullOrEmpty(text))
            return Enumerable.Empty<OpenXmlElement>();

        var run = _paragraphBuilder.CreateRun(text, bold, italic, strikethrough);
        return new[] { run };
    }

    private IEnumerable<OpenXmlElement> VisitEmphasis(EmphasisInline emphasis, bool bold, bool italic, bool strikethrough)
    {
        var newBold = bold;
        var newItalic = italic;
        var newStrikethrough = strikethrough;

        if (emphasis.DelimiterChar == '~')
        {
            newStrikethrough = true;
        }
        else if (emphasis.DelimiterCount == 2)
        {
            newBold = true;
        }
        else if (emphasis.DelimiterCount == 1)
        {
            newItalic = true;
        }

        return VisitInlineContainer(emphasis, newBold, newItalic, newStrikethrough);
    }

    private IEnumerable<OpenXmlElement> VisitCodeInline(CodeInline code)
    {
        var run = _paragraphBuilder.CreateInlineCodeRun(code.Content);
        return new[] { run };
    }

    private IEnumerable<OpenXmlElement> VisitLink(LinkInline link, bool bold, bool italic, bool strikethrough)
    {
        if (link.Url == null)
            return Enumerable.Empty<OpenXmlElement>();

        // Handle image links
        if (link.IsImage)
        {
            var altText = ExtractInlineText(link);
            var imageParagraphs = _imageBuilder.BuildImage(_mainDocumentPart, link.Url, altText, _theme);
            return imageParagraphs.ToArray<OpenXmlElement>();
        }

        // Extract link text
        var linkText = ExtractInlineText(link);

        try
        {
            var uri = new Uri(link.Url, UriKind.Absolute);
            var rel = _mainDocumentPart.AddHyperlinkRelationship(uri, true);

            var hyperlinkRun = _paragraphBuilder.CreateHyperlinkRun(linkText);
            var hyperlink = new Hyperlink(hyperlinkRun) { Id = rel.Id };

            return new OpenXmlElement[] { hyperlink };
        }
        catch (UriFormatException)
        {
            // If URL is not valid, just emit as plain text
            var run = _paragraphBuilder.CreateRun(linkText, bold, italic, strikethrough);
            return new[] { run };
        }
    }

    private IEnumerable<OpenXmlElement> VisitLineBreak()
    {
        var run = _paragraphBuilder.CreateLineBreakRun();
        return new[] { run };
    }

    private static string ExtractPlainText(LeafBlock block)
    {
        if (block.Inline == null)
            return string.Empty;

        return InlineTextExtractor.Extract(block.Inline);
    }

    private static string ExtractInlineText(ContainerInline container) =>
        InlineTextExtractor.Extract(container);

    private IEnumerable<OpenXmlElement> VisitMathBlock(MathBlock mathBlock)
    {
        var omml = mathBlock.GetOmmlXml();
        if (string.IsNullOrEmpty(omml))
        {
            // Fallback: render as code block
            var code = string.Join("\n", mathBlock.Lines);
            var table = _codeBlockBuilder.Build(code, "latex", _theme, null);
            return new OpenXmlElement[] { table };
        }

        return ParseOmmlIntoParagraph(omml, display: true);
    }

    private IEnumerable<OpenXmlElement> VisitMathInline(MathInline mathInline)
    {
        var omml = mathInline.GetOmmlXml();
        if (string.IsNullOrEmpty(omml))
        {
            // Fallback: render as inline code
            var run = _paragraphBuilder.CreateInlineCodeRun(mathInline.Content.ToString());
            return new[] { run };
        }

        return ParseOmmlIntoRuns(omml);
    }

    private IEnumerable<OpenXmlElement> ParseOmmlIntoParagraph(string omml, bool display)
    {
        try
        {
            var mathElement = ParseOmmlXml(omml);
            if (mathElement == null)
                return Enumerable.Empty<OpenXmlElement>();

            var paragraph = new Paragraph();
            if (display)
            {
                // Center display math
                paragraph.Append(new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { Before = "120", After = "120" }
                ));
            }
            paragraph.Append(mathElement);
            return new OpenXmlElement[] { paragraph };
        }
        catch
        {
            return Enumerable.Empty<OpenXmlElement>();
        }
    }

    private static IEnumerable<OpenXmlElement> ParseOmmlIntoRuns(string omml)
    {
        try
        {
            var mathElement = ParseOmmlXml(omml);
            if (mathElement == null)
                return Enumerable.Empty<OpenXmlElement>();

            return new OpenXmlElement[] { mathElement };
        }
        catch
        {
            return Enumerable.Empty<OpenXmlElement>();
        }
    }

    private static OpenXmlElement? ParseOmmlXml(string omml)
    {
        // The OMML from the XSLT may be wrapped in various elements.
        // We need to find the <m:oMath> or <m:oMathPara> element and parse it.
        try
        {
            // Wrap in a dummy element to parse
            var xml = $"<root xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\">{omml}</root>";
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(xml);

            var nsMgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("m", "http://schemas.openxmlformats.org/officeDocument/2006/math");

            // Try to find oMathPara first (display math), then oMath
            var mathNode = doc.SelectSingleNode("//m:oMathPara", nsMgr)
                ?? doc.SelectSingleNode("//m:oMath", nsMgr);

            if (mathNode == null)
                return null;

            // Parse into Open XML SDK type
            var outerXml = mathNode.OuterXml;
            if (mathNode.LocalName == "oMathPara")
            {
                return new DocumentFormat.OpenXml.Math.Paragraph(outerXml);
            }
            return new DocumentFormat.OpenXml.Math.OfficeMath(outerXml);
        }
        catch
        {
            return null;
        }
    }

    private static bool HasComplexInlines(ContainerInline container)
    {
        foreach (var inline in container)
        {
            if (inline is EmphasisInline or CodeInline or LinkInline or LineBreakInline)
                return true;
        }
        return false;
    }
}
