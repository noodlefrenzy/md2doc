// agent-notes: { ctx: "Walks Markdig AST, dispatches to builders for paragraphs, tables, lists, images, thematic breaks", deps: [ParagraphBuilder, TableBuilder, ListBuilder, ImageBuilder, Markdig, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-12" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Md2.Core.Pipeline;

using MdTable = Markdig.Extensions.Tables.Table;

namespace Md2.Emit.Docx;

public class DocxAstVisitor
{
    private readonly ParagraphBuilder _paragraphBuilder;
    private readonly TableBuilder _tableBuilder;
    private readonly ListBuilder _listBuilder;
    private readonly ImageBuilder _imageBuilder;
    private readonly MainDocumentPart _mainDocumentPart;
    private readonly ResolvedTheme _theme;

    public DocxAstVisitor(ParagraphBuilder paragraphBuilder, MainDocumentPart mainDocumentPart, ResolvedTheme theme)
    {
        _paragraphBuilder = paragraphBuilder;
        _tableBuilder = new TableBuilder(paragraphBuilder, VisitInline);
        _listBuilder = new ListBuilder(paragraphBuilder, mainDocumentPart);
        _imageBuilder = new ImageBuilder(paragraphBuilder);
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
            LiteralInline literal => VisitLiteral(literal, bold, italic, strikethrough),
            EmphasisInline emphasis => VisitEmphasis(emphasis, bold, italic, strikethrough),
            CodeInline code => VisitCodeInline(code),
            LinkInline link => VisitLink(link, bold, italic, strikethrough),
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
            var imageParagraph = _imageBuilder.BuildImage(_mainDocumentPart, link.Url, altText, _theme);
            return new OpenXmlElement[] { imageParagraph };
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

        return ExtractInlineText(block.Inline);
    }

    private static string ExtractInlineText(ContainerInline container)
    {
        var parts = new List<string>();
        foreach (var inline in container)
        {
            parts.Add(ExtractInlineTextSingle(inline));
        }
        return string.Join("", parts);
    }

    private static string ExtractInlineTextSingle(Markdig.Syntax.Inlines.Inline inline)
    {
        return inline switch
        {
            LiteralInline literal => literal.Content.ToString(),
            EmphasisInline emphasis => ExtractInlineText(emphasis),
            CodeInline code => code.Content,
            LinkInline link => ExtractInlineText(link),
            _ => string.Empty
        };
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
