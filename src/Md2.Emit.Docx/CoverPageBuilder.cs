// agent-notes: { ctx: "Builds DOCX cover page from front matter metadata", deps: [ParagraphBuilder, DocumentMetadata, ResolvedTheme, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-12" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Ast;
using Md2.Core.Pipeline;

namespace Md2.Emit.Docx;

/// <summary>
/// Builds a cover page from document front matter metadata.
/// Returns paragraphs for title, subtitle, author, date, and abstract,
/// followed by a section break.
/// </summary>
public sealed class CoverPageBuilder
{
    private readonly ParagraphBuilder _paragraphBuilder;

    public CoverPageBuilder(ParagraphBuilder paragraphBuilder)
    {
        _paragraphBuilder = paragraphBuilder;
    }

    /// <summary>
    /// Builds cover page elements from metadata. Returns empty list if no title is present.
    /// </summary>
    public IReadOnlyList<OpenXmlElement> Build(DocumentMetadata? metadata, ResolvedTheme theme)
    {
        if (metadata is null || string.IsNullOrWhiteSpace(metadata.Title))
            return Array.Empty<OpenXmlElement>();

        var elements = new List<OpenXmlElement>();

        // Vertical spacing before title (push content down the page)
        for (int i = 0; i < 6; i++)
        {
            elements.Add(new Paragraph());
        }

        // Title
        elements.Add(CreateCoverTitle(metadata.Title, theme));

        // Subtitle (from Subject field)
        if (!string.IsNullOrWhiteSpace(metadata.Subject))
        {
            elements.Add(CreateCoverSubtitle(metadata.Subject, theme));
        }

        // Spacer
        elements.Add(new Paragraph());
        elements.Add(new Paragraph());

        // Author
        if (!string.IsNullOrWhiteSpace(metadata.Author))
        {
            elements.Add(CreateCoverField(metadata.Author, theme));
        }

        // Date
        if (!string.IsNullOrWhiteSpace(metadata.Date))
        {
            elements.Add(CreateCoverField(metadata.Date, theme));
        }

        // Abstract (from custom fields)
        if (metadata.CustomFields.TryGetValue("abstract", out var abstractText)
            && !string.IsNullOrWhiteSpace(abstractText))
        {
            elements.Add(new Paragraph()); // spacer
            elements.Add(CreateCoverAbstract(abstractText, theme));
        }

        // Section break (next page) after cover
        elements.Add(CreateSectionBreak());

        return elements;
    }

    private static Paragraph CreateCoverTitle(string title, ResolvedTheme theme)
    {
        var fontSize = theme.Heading1Size + 8; // Larger than H1
        var halfPoints = ((int)(fontSize * 2)).ToString();

        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new SpacingBetweenLines { After = "200" }
            ),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = theme.HeadingFont, HighAnsi = theme.HeadingFont },
                    new FontSize { Val = halfPoints },
                    new FontSizeComplexScript { Val = halfPoints },
                    new Color { Val = theme.PrimaryColor },
                    new Bold()
                ),
                new Text(title) { Space = SpaceProcessingModeValues.Preserve }
            )
        );
    }

    private static Paragraph CreateCoverSubtitle(string subtitle, ResolvedTheme theme)
    {
        var fontSize = theme.Heading2Size;
        var halfPoints = ((int)(fontSize * 2)).ToString();

        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new SpacingBetweenLines { After = "120" }
            ),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = theme.HeadingFont, HighAnsi = theme.HeadingFont },
                    new FontSize { Val = halfPoints },
                    new FontSizeComplexScript { Val = halfPoints },
                    new Color { Val = theme.SecondaryColor },
                    new Italic()
                ),
                new Text(subtitle) { Space = SpaceProcessingModeValues.Preserve }
            )
        );
    }

    private static Paragraph CreateCoverField(string text, ResolvedTheme theme)
    {
        var halfPoints = ((int)(theme.BaseFontSize * 2 + 4)).ToString(); // slightly larger than body

        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Center }
            ),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = theme.BodyFont, HighAnsi = theme.BodyFont },
                    new FontSize { Val = halfPoints },
                    new FontSizeComplexScript { Val = halfPoints },
                    new Color { Val = theme.BodyTextColor }
                ),
                new Text(text) { Space = SpaceProcessingModeValues.Preserve }
            )
        );
    }

    private static Paragraph CreateCoverAbstract(string abstractText, ResolvedTheme theme)
    {
        var halfPoints = ((int)(theme.BaseFontSize * 2)).ToString();

        return new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Both },
                new Indentation { Left = "720", Right = "720" }
            ),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = theme.BodyFont, HighAnsi = theme.BodyFont },
                    new FontSize { Val = halfPoints },
                    new FontSizeComplexScript { Val = halfPoints },
                    new Color { Val = theme.BodyTextColor },
                    new Italic()
                ),
                new Text(abstractText) { Space = SpaceProcessingModeValues.Preserve }
            )
        );
    }

    private static Paragraph CreateSectionBreak()
    {
        return new Paragraph(
            new ParagraphProperties(
                new SectionProperties(
                    new SectionType { Val = SectionMarkValues.NextPage }
                )
            )
        );
    }
}
