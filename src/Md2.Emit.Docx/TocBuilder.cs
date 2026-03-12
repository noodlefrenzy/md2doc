// agent-notes: { ctx: "Builds TOC field code for DOCX documents", deps: [ParagraphBuilder, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-12" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Pipeline;

namespace Md2.Emit.Docx;

/// <summary>
/// Builds a Table of Contents field for DOCX output.
/// Uses the OOXML TOC field code which Word evaluates on open.
/// </summary>
public sealed class TocBuilder
{
    private readonly ParagraphBuilder _paragraphBuilder;

    public TocBuilder(ParagraphBuilder paragraphBuilder)
    {
        _paragraphBuilder = paragraphBuilder;
    }

    /// <summary>
    /// Builds TOC paragraphs: a heading, the TOC field, and a section break paragraph.
    /// </summary>
    /// <param name="depth">Maximum heading depth to include (1-6, default 3).</param>
    /// <param name="theme">Resolved theme for styling.</param>
    public IReadOnlyList<OpenXmlElement> Build(int depth, ResolvedTheme theme)
    {
        depth = Math.Clamp(depth, 1, 6);

        var elements = new List<OpenXmlElement>();

        // TOC heading
        elements.Add(_paragraphBuilder.CreateHeadingParagraph(1, "Table of Contents"));

        // TOC field code paragraph
        // \o "1-N" = heading levels, \h = hyperlinks, \z = hide in web, \u = use outline levels
        var fieldCode = $" TOC \\o \"1-{depth}\" \\h \\z \\u ";

        var tocParagraph = new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = "TOC1" }
            ),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(fieldCode) { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            new Run(
                new RunProperties(
                    new Color { Val = theme.BodyTextColor },
                    new Italic()
                ),
                new Text("Update this table of contents to see entries.")
                { Space = SpaceProcessingModeValues.Preserve }
            ),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End })
        );
        elements.Add(tocParagraph);

        // Page break after TOC
        elements.Add(new Paragraph(
            new Run(new Break { Type = BreakValues.Page })
        ));

        return elements;
    }
}
