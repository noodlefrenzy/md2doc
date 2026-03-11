// agent-notes: { ctx: "Applies ResolvedTheme to document styles part", deps: [ResolvedTheme, DocumentFormat.OpenXml], state: active, last: "sato@2026-03-11" }

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Md2.Core.Pipeline;

namespace Md2.Emit.Docx;

public static class DocxStyleApplicator
{
    public static void ApplyStyles(MainDocumentPart mainPart, ResolvedTheme theme)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        // Default paragraph style (Normal)
        styles.Append(CreateNormalStyle(theme));

        // Heading styles 1-6
        for (var level = 1; level <= 6; level++)
        {
            styles.Append(CreateHeadingStyle(level, theme));
        }

        stylesPart.Styles = styles;
    }

    private static Style CreateNormalStyle(ResolvedTheme theme)
    {
        var halfPoints = ((int)(theme.BaseFontSize * 2)).ToString();

        return new Style(
            new StyleName { Val = "Normal" },
            new PrimaryStyle(),
            new StyleParagraphProperties(
                new WidowControl(),
                new SpacingBetweenLines
                {
                    Line = ((int)(theme.LineSpacing * 240)).ToString(),
                    LineRule = LineSpacingRuleValues.Auto
                }
            ),
            new StyleRunProperties(
                new RunFonts { Ascii = theme.BodyFont, HighAnsi = theme.BodyFont },
                new FontSize { Val = halfPoints },
                new FontSizeComplexScript { Val = halfPoints },
                new Color { Val = theme.BodyTextColor }
            )
        )
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal",
            Default = true
        };
    }

    private static Style CreateHeadingStyle(int level, ResolvedTheme theme)
    {
        var fontSize = theme.GetHeadingSize(level);
        var halfPoints = ((int)(fontSize * 2)).ToString();
        var styleName = $"heading {level}";
        var styleId = $"Heading{level}";

        return new Style(
            new StyleName { Val = styleName },
            new BasedOn { Val = "Normal" },
            new NextParagraphStyle { Val = "Normal" },
            new PrimaryStyle(),
            new StyleParagraphProperties(
                new WidowControl(),
                new OutlineLevel { Val = level - 1 },
                new SpacingBetweenLines
                {
                    Before = level <= 2 ? "240" : "120",
                    After = "60"
                }
            ),
            new StyleRunProperties(
                new RunFonts { Ascii = theme.HeadingFont, HighAnsi = theme.HeadingFont },
                new FontSize { Val = halfPoints },
                new FontSizeComplexScript { Val = halfPoints },
                new Color { Val = theme.PrimaryColor },
                new Bold()
            )
        )
        {
            Type = StyleValues.Paragraph,
            StyleId = styleId
        };
    }
}
