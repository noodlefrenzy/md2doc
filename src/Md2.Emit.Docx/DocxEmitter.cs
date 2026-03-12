// agent-notes: { ctx: "Top-level DOCX emitter implementing IFormatEmitter", deps: [IFormatEmitter, DocxAstVisitor, ParagraphBuilder, DocxStyleApplicator, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Emit;
using Md2.Core.Pipeline;

namespace Md2.Emit.Docx;

public class DocxEmitter : IFormatEmitter
{
    public string FormatName => "docx";

    public IReadOnlyList<string> FileExtensions => new[] { ".docx" };

    public async Task EmitAsync(MarkdownDocument doc, ResolvedTheme theme, EmitOptions options, Stream output)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        // Use a temporary MemoryStream so we can fully create the document, then copy
        using var tempStream = new MemoryStream();

        using (var wordDoc = WordprocessingDocument.Create(tempStream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            // Apply styles
            DocxStyleApplicator.ApplyStyles(mainPart, theme);

            // Create footer with page numbers
            var footerRelId = CreateFooterWithPageNumbers(mainPart);

            // Create header with document title
            var metadata = doc.GetDocumentMetadata();
            var headerTitle = metadata?.Title ?? string.Empty;
            var headerRelId = CreateHeaderWithTitle(mainPart, headerTitle, theme);

            // Walk AST and generate content
            var paragraphBuilder = new ParagraphBuilder(theme);
            var visitor = new DocxAstVisitor(paragraphBuilder, mainPart, theme);
            var elements = visitor.Visit(doc);

            var body = mainPart.Document.Body!;

            // Insert cover page before main content if requested
            if (options.IncludeCoverPage)
            {
                var coverBuilder = new CoverPageBuilder(paragraphBuilder);
                foreach (var coverElement in coverBuilder.Build(metadata, theme))
                {
                    body.Append(coverElement);
                }
            }

            // Insert TOC before main content if requested
            if (options.IncludeToc)
            {
                var tocBuilder = new TocBuilder(paragraphBuilder);
                foreach (var tocElement in tocBuilder.Build(options.TocDepth, theme))
                {
                    body.Append(tocElement);
                }
            }

            foreach (var element in elements)
            {
                body.Append(element);
            }

            // Add section properties (page layout)
            var sectionProperties = CreateSectionProperties(theme, footerRelId, headerRelId, options.IncludeCoverPage);
            body.Append(sectionProperties);

            // Set document properties from front matter
            SetDocumentProperties(wordDoc, doc);

            mainPart.Document.Save();
        }

        // Copy completed document to output stream
        tempStream.Position = 0;
        await tempStream.CopyToAsync(output);
    }

    private static string CreateFooterWithPageNumbers(MainDocumentPart mainPart)
    {
        var footerPart = mainPart.AddNewPart<FooterPart>();
        footerPart.Footer = new Footer(
            new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center }
                ),
                new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
                new Run(new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
                new Run(new FieldChar { FieldCharType = FieldCharValues.End })
            )
        );
        footerPart.Footer.Save();

        return mainPart.GetIdOfPart(footerPart);
    }

    private static string CreateHeaderWithTitle(MainDocumentPart mainPart, string title, ResolvedTheme theme)
    {
        var headerPart = mainPart.AddNewPart<HeaderPart>();

        if (string.IsNullOrWhiteSpace(title))
        {
            headerPart.Header = new Header(new Paragraph());
        }
        else
        {
            var halfPoints = ((int)((theme.BaseFontSize - 1) * 2)).ToString();
            headerPart.Header = new Header(
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right }
                    ),
                    new Run(
                        new RunProperties(
                            new RunFonts { Ascii = theme.BodyFont, HighAnsi = theme.BodyFont },
                            new FontSize { Val = halfPoints },
                            new FontSizeComplexScript { Val = halfPoints },
                            new Color { Val = theme.BodyTextColor },
                            new Italic()
                        ),
                        new Text(title) { Space = SpaceProcessingModeValues.Preserve }
                    )
                )
            );
        }

        headerPart.Header.Save();
        return mainPart.GetIdOfPart(headerPart);
    }

    private static SectionProperties CreateSectionProperties(ResolvedTheme theme, string footerRelId, string headerRelId, bool differentFirstPage)
    {
        var sectProps = new SectionProperties(
            new HeaderReference
            {
                Type = HeaderFooterValues.Default,
                Id = headerRelId
            },
            new FooterReference
            {
                Type = HeaderFooterValues.Default,
                Id = footerRelId
            },
            new PageSize
            {
                Width = theme.PageWidth,
                Height = theme.PageHeight
            },
            new PageMargin
            {
                Top = theme.MarginTop,
                Bottom = theme.MarginBottom,
                Left = (uint)theme.MarginLeft,
                Right = (uint)theme.MarginRight
            }
        );

        // Different first page suppresses header/footer on cover page
        if (differentFirstPage)
        {
            sectProps.Append(new TitlePage());
        }

        return sectProps;
    }

    private static void SetDocumentProperties(WordprocessingDocument wordDoc, MarkdownDocument markdownDoc)
    {
        var metadata = markdownDoc.GetDocumentMetadata();
        if (metadata == null)
            return;

        if (string.IsNullOrEmpty(metadata.Title) && string.IsNullOrEmpty(metadata.Author))
            return;

        var corePropsPart = wordDoc.AddCoreFilePropertiesPart();

        // Build core properties XML using the Dublin Core and cp namespaces
        var nsCP = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        var nsDC = "http://purl.org/dc/elements/1.1/";
        var nsDCTerms = "http://purl.org/dc/terms/";
        var nsXsi = "http://www.w3.org/2001/XMLSchema-instance";

        var xDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(XName.Get("coreProperties", nsCP),
                new XAttribute(XNamespace.Xmlns + "dc", nsDC),
                new XAttribute(XNamespace.Xmlns + "dcterms", nsDCTerms),
                new XAttribute(XNamespace.Xmlns + "xsi", nsXsi)
            )
        );

        var root = xDoc.Root!;

        if (!string.IsNullOrEmpty(metadata.Title))
        {
            root.Add(new XElement(XName.Get("title", nsDC), metadata.Title));
        }

        if (!string.IsNullOrEmpty(metadata.Author))
        {
            root.Add(new XElement(XName.Get("creator", nsDC), metadata.Author));
        }

        if (!string.IsNullOrEmpty(metadata.Subject))
        {
            root.Add(new XElement(XName.Get("subject", nsDC), metadata.Subject));
        }

        if (!string.IsNullOrEmpty(metadata.Keywords))
        {
            root.Add(new XElement(XName.Get("keywords", nsCP), metadata.Keywords));
        }

        using var xmlStream = corePropsPart.GetStream(FileMode.Create);
        xDoc.Save(xmlStream);
    }
}
