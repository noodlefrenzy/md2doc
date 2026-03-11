// agent-notes: { ctx: "Creates OpenXml Paragraph/Run elements from theme", deps: [ResolvedTheme, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Pipeline;

namespace Md2.Emit.Docx;

public class ParagraphBuilder
{
    private readonly ResolvedTheme _theme;

    public ParagraphBuilder(ResolvedTheme theme)
    {
        _theme = theme;
    }

    public Paragraph CreateHeadingParagraph(int level, string text)
    {
        var styleId = $"Heading{level}";
        var fontSize = _theme.GetHeadingSize(level);
        var halfPoints = ((int)(fontSize * 2)).ToString();

        var paragraph = new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = styleId },
                new WidowControl(),
                new OutlineLevel { Val = level - 1 }
            ),
            new Run(
                new RunProperties(
                    new RunFonts { Ascii = _theme.HeadingFont, HighAnsi = _theme.HeadingFont },
                    new FontSize { Val = halfPoints },
                    new FontSizeComplexScript { Val = halfPoints },
                    new Color { Val = _theme.PrimaryColor },
                    new Bold()
                ),
                new Text(text) { Space = SpaceProcessingModeValues.Preserve }
            )
        );

        return paragraph;
    }

    public Paragraph CreateBodyParagraph()
    {
        return new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = "Normal" },
                new WidowControl()
            )
        );
    }

    public Run CreateRun(string text, bool bold = false, bool italic = false, bool strikethrough = false)
    {
        var halfPoints = ((int)(_theme.BaseFontSize * 2)).ToString();

        var runProperties = new RunProperties(
            new RunFonts { Ascii = _theme.BodyFont, HighAnsi = _theme.BodyFont },
            new FontSize { Val = halfPoints },
            new FontSizeComplexScript { Val = halfPoints },
            new Color { Val = _theme.BodyTextColor }
        );

        if (bold) runProperties.Append(new Bold());
        if (italic) runProperties.Append(new Italic());
        if (strikethrough) runProperties.Append(new Strike());

        return new Run(
            runProperties,
            new Text(text) { Space = SpaceProcessingModeValues.Preserve }
        );
    }

    public Run CreateInlineCodeRun(string text)
    {
        var halfPoints = ((int)(_theme.BaseFontSize * 2)).ToString();

        return new Run(
            new RunProperties(
                new RunFonts { Ascii = _theme.MonoFont, HighAnsi = _theme.MonoFont },
                new FontSize { Val = halfPoints },
                new FontSizeComplexScript { Val = halfPoints },
                new Color { Val = _theme.BodyTextColor },
                new Shading { Val = ShadingPatternValues.Clear, Fill = _theme.CodeBackgroundColor }
            ),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve }
        );
    }

    public Run CreateHyperlinkRun(string text)
    {
        return new Run(
            new RunProperties(
                new RunFonts { Ascii = _theme.BodyFont, HighAnsi = _theme.BodyFont },
                new Color { Val = _theme.LinkColor },
                new Underline { Val = UnderlineValues.Single }
            ),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve }
        );
    }

    public Run CreateLineBreakRun()
    {
        return new Run(new Break());
    }
}
