// agent-notes: { ctx: "TDD tests for ThemeCascadeResolver 4-layer merge", deps: [src/Md2.Themes/ThemeCascadeResolver.cs], state: active, last: "tara@2026-03-12" }

using Shouldly;
using Md2.Themes;

namespace Md2.Themes.Tests;

public class ThemeCascadeResolverTests
{
    [Fact]
    public void Resolve_NoLayers_ReturnsDefaultPresetValues()
    {
        var input = new ThemeCascadeInput();
        var result = ThemeCascadeResolver.Resolve(input);

        // Should match default preset values
        result.HeadingFont.ShouldBe("Calibri");
        result.BodyFont.ShouldBe("Cambria");
        result.MonoFont.ShouldBe("Cascadia Code");
        result.BaseFontSize.ShouldBe(11.0);
        result.PrimaryColor.ShouldBe("1B3A5C");
        result.PageWidth.ShouldBe(11906u);
    }

    [Fact]
    public void Resolve_PresetOnly_UsesPresetValues()
    {
        var input = new ThemeCascadeInput { PresetName = "default" };
        var result = ThemeCascadeResolver.Resolve(input);

        result.HeadingFont.ShouldBe("Calibri");
        result.BodyFont.ShouldBe("Cambria");
        result.Heading1Size.ShouldBe(28.0);
    }

    [Fact]
    public void Resolve_ThemeOverridesPreset()
    {
        var themeYaml = new ThemeDefinition
        {
            Typography = new ThemeTypographySection { HeadingFont = "Arial" },
            Colors = new ThemeColorsSection { Primary = "FF0000" }
        };
        var input = new ThemeCascadeInput { Theme = themeYaml };
        var result = ThemeCascadeResolver.Resolve(input);

        // Theme overrides preset
        result.HeadingFont.ShouldBe("Arial");
        result.PrimaryColor.ShouldBe("FF0000");
        // Preset fills in the rest
        result.BodyFont.ShouldBe("Cambria");
        result.SecondaryColor.ShouldBe("4A90D9");
    }

    [Fact]
    public void Resolve_CliOverridesEverything()
    {
        var themeYaml = new ThemeDefinition
        {
            Typography = new ThemeTypographySection { HeadingFont = "Arial" }
        };
        var cliOverrides = new ThemeDefinition
        {
            Typography = new ThemeTypographySection { HeadingFont = "Helvetica" }
        };
        var input = new ThemeCascadeInput
        {
            Theme = themeYaml,
            CliOverrides = cliOverrides
        };
        var result = ThemeCascadeResolver.Resolve(input);

        // CLI wins over theme
        result.HeadingFont.ShouldBe("Helvetica");
    }

    [Fact]
    public void Resolve_PartialTheme_PresetFillsGaps()
    {
        var themeYaml = new ThemeDefinition
        {
            Docx = new ThemeDocxSection { BaseFontSize = 14.0 }
        };
        var input = new ThemeCascadeInput { Theme = themeYaml };
        var result = ThemeCascadeResolver.Resolve(input);

        result.BaseFontSize.ShouldBe(14.0);
        // All other values come from default preset
        result.Heading1Size.ShouldBe(28.0);
        result.LineSpacing.ShouldBe(1.15);
        result.HeadingFont.ShouldBe("Calibri");
    }

    [Fact]
    public void Resolve_AllFontSizes_Cascade()
    {
        var theme = new ThemeDefinition
        {
            Docx = new ThemeDocxSection
            {
                Heading1Size = 32,
                Heading2Size = 26,
                Heading3Size = 20,
                Heading4Size = 16,
                Heading5Size = 13,
                Heading6Size = 12
            }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var result = ThemeCascadeResolver.Resolve(input);

        result.Heading1Size.ShouldBe(32.0);
        result.Heading2Size.ShouldBe(26.0);
        result.Heading3Size.ShouldBe(20.0);
        result.Heading4Size.ShouldBe(16.0);
        result.Heading5Size.ShouldBe(13.0);
        result.Heading6Size.ShouldBe(12.0);
    }

    [Fact]
    public void Resolve_AllColors_Cascade()
    {
        var theme = new ThemeDefinition
        {
            Colors = new ThemeColorsSection
            {
                Primary = "AA0000",
                Secondary = "BB0000",
                BodyText = "CC0000",
                CodeBackground = "DD0000",
                CodeBorder = "EE0000",
                Link = "FF0000",
                TableHeaderBackground = "110000",
                TableHeaderForeground = "220000",
                TableBorder = "330000",
                TableAlternateRow = "440000",
                BlockquoteBorder = "550000",
                BlockquoteText = "660000"
            }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var result = ThemeCascadeResolver.Resolve(input);

        result.PrimaryColor.ShouldBe("AA0000");
        result.SecondaryColor.ShouldBe("BB0000");
        result.BodyTextColor.ShouldBe("CC0000");
        result.CodeBackgroundColor.ShouldBe("DD0000");
        result.CodeBlockBorderColor.ShouldBe("EE0000");
        result.LinkColor.ShouldBe("FF0000");
        result.TableHeaderBackground.ShouldBe("110000");
        result.TableHeaderForeground.ShouldBe("220000");
        result.TableBorderColor.ShouldBe("330000");
        result.TableAlternateRowBackground.ShouldBe("440000");
        result.BlockquoteBorderColor.ShouldBe("550000");
        result.BlockquoteTextColor.ShouldBe("660000");
    }

    [Fact]
    public void Resolve_PageLayout_Cascade()
    {
        var theme = new ThemeDefinition
        {
            Docx = new ThemeDocxSection
            {
                Page = new ThemePageSection
                {
                    Width = 12240,
                    Height = 15840,
                    MarginTop = 1800
                }
            }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var result = ThemeCascadeResolver.Resolve(input);

        result.PageWidth.ShouldBe(12240u);
        result.PageHeight.ShouldBe(15840u);
        result.MarginTop.ShouldBe(1800);
        // Non-overridden margins from preset
        result.MarginBottom.ShouldBe(1440);
        result.MarginLeft.ShouldBe(1800);
    }

    [Fact]
    public void Resolve_TableStyling_Cascade()
    {
        var theme = new ThemeDefinition
        {
            Docx = new ThemeDocxSection
            {
                TableBorderWidth = 8,
                BlockquoteIndentTwips = 1440
            }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var result = ThemeCascadeResolver.Resolve(input);

        result.TableBorderWidth.ShouldBe(8);
        result.BlockquoteIndentTwips.ShouldBe(1440);
    }

    [Fact]
    public void Resolve_ThreeLayers_CorrectPrecedence()
    {
        // Preset: default (headingFont=Calibri)
        // Theme: headingFont=Arial, bodyFont=Georgia
        // CLI: headingFont=Helvetica
        var theme = new ThemeDefinition
        {
            Typography = new ThemeTypographySection
            {
                HeadingFont = "Arial",
                BodyFont = "Georgia"
            }
        };
        var cli = new ThemeDefinition
        {
            Typography = new ThemeTypographySection
            {
                HeadingFont = "Helvetica"
            }
        };
        var input = new ThemeCascadeInput
        {
            PresetName = "default",
            Theme = theme,
            CliOverrides = cli
        };
        var result = ThemeCascadeResolver.Resolve(input);

        result.HeadingFont.ShouldBe("Helvetica");  // CLI wins
        result.BodyFont.ShouldBe("Georgia");         // Theme wins over preset
        result.MonoFont.ShouldBe("Cascadia Code");   // Preset fallback
    }

    [Fact]
    public void Resolve_ReturnsResolutionTrace()
    {
        var theme = new ThemeDefinition
        {
            Typography = new ThemeTypographySection { HeadingFont = "Arial" }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var (result, trace) = ThemeCascadeResolver.ResolveWithTrace(input);

        result.HeadingFont.ShouldBe("Arial");

        // Trace should show where HeadingFont came from
        trace.ShouldContain(e => e.Property == "HeadingFont" && e.Source == CascadeLayer.Theme);
        // BodyFont came from preset
        trace.ShouldContain(e => e.Property == "BodyFont" && e.Source == CascadeLayer.Preset);
    }

    [Fact]
    public void Resolve_UnknownPreset_ThrowsArgumentException()
    {
        var input = new ThemeCascadeInput { PresetName = "nonexistent" };
        Should.Throw<ArgumentException>(() => ThemeCascadeResolver.Resolve(input));
    }

    // ── PPTX sub-object cascade ──────────────────────────────────────

    [Fact]
    public void Resolve_DefaultPreset_PptxSubObjectPopulated()
    {
        var input = new ThemeCascadeInput();
        var result = ThemeCascadeResolver.Resolve(input);

        result.Pptx.ShouldNotBeNull();
        result.Pptx!.BaseFontSize.ShouldBe(24.0);
        result.Pptx.Heading1Size.ShouldBe(44.0);
        result.Pptx.SlideSize.ShouldBe(Md2.Core.Slides.SlideSize.Widescreen16x9);
    }

    [Fact]
    public void Resolve_PptxFontSizes_CascadeFromTheme()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection
            {
                BaseFontSize = 28,
                Heading1Size = 48
            }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var result = ThemeCascadeResolver.Resolve(input);

        result.Pptx!.BaseFontSize.ShouldBe(28.0);
        result.Pptx.Heading1Size.ShouldBe(48.0);
        // Non-overridden values use defaults
        result.Pptx.Heading2Size.ShouldBe(36.0);
    }

    [Fact]
    public void Resolve_PptxSlideSize_ParsedFromString()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection { SlideSize = "4:3" }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var result = ThemeCascadeResolver.Resolve(input);

        result.Pptx!.SlideSize.ShouldBe(Md2.Core.Slides.SlideSize.Standard4x3);
    }

    [Fact]
    public void Resolve_PptxPerFormatColors_Override()
    {
        var theme = new ThemeDefinition
        {
            Colors = new ThemeColorsSection { BodyText = "333333" },
            Pptx = new ThemePptxSection
            {
                Colors = new ThemeColorsSection { BodyText = "d6deeb" }
            }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var result = ThemeCascadeResolver.Resolve(input);

        // Shared colors → DOCX path
        result.BodyTextColor.ShouldBe("333333");
        // PPTX per-format override
        result.Pptx!.BodyTextColor.ShouldBe("d6deeb");
    }

    [Fact]
    public void Resolve_PptxLayoutThemes_CascadeFromTheme()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection
            {
                TitleSlide = new ThemePptxTitleSlideSection
                {
                    TitleSize = 60,
                    SubtitleSize = 32,
                    BackgroundColor = "011627"
                },
                Content = new ThemePptxContentSection
                {
                    TitleSize = 40,
                    BodySize = 26,
                    BulletIndent = 40
                }
            }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var result = ThemeCascadeResolver.Resolve(input);

        result.Pptx!.TitleSlide.TitleSize.ShouldBe(60.0);
        result.Pptx.TitleSlide.SubtitleSize.ShouldBe(32.0);
        result.Pptx.TitleSlide.BackgroundColor.ShouldBe("011627");
        result.Pptx.Content.TitleSize.ShouldBe(40.0);
        result.Pptx.Content.BodySize.ShouldBe(26.0);
        result.Pptx.Content.BulletIndent.ShouldBe(40.0);
    }

    [Fact]
    public void Resolve_PptxChartPalette_CascadesFromTheme()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection
            {
                ChartPalette = new List<string> { "AA0000", "BB0000", "CC0000" }
            }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var result = ThemeCascadeResolver.Resolve(input);

        result.Pptx!.ChartPalette.Count.ShouldBe(3);
        result.Pptx.ChartPalette[0].ShouldBe("AA0000");
    }

    [Fact]
    public void Resolve_PptxCodeBlock_CascadesFromTheme()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection
            {
                CodeBlock = new ThemePptxCodeBlockSection
                {
                    FontSize = 16,
                    Padding = 14
                }
            }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var result = ThemeCascadeResolver.Resolve(input);

        result.Pptx!.CodeBlock.FontSize.ShouldBe(16.0);
        result.Pptx.CodeBlock.Padding.ShouldBe(14.0);
        // Default
        result.Pptx.CodeBlock.BorderRadius.ShouldBe(8.0);
    }

    [Fact]
    public void Resolve_PptxBackground_CascadesFromTheme()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection
            {
                Background = new ThemePptxBackgroundSection { Color = "011627" }
            }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var result = ThemeCascadeResolver.Resolve(input);

        result.Pptx!.BackgroundColor.ShouldBe("011627");
    }

    [Fact]
    public void Resolve_PptxCliOverridesTheme()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection { BaseFontSize = 28 }
        };
        var cli = new ThemeDefinition
        {
            Pptx = new ThemePptxSection { BaseFontSize = 32 }
        };
        var input = new ThemeCascadeInput { Theme = theme, CliOverrides = cli };
        var result = ThemeCascadeResolver.Resolve(input);

        result.Pptx!.BaseFontSize.ShouldBe(32.0);
    }

    [Fact]
    public void Resolve_PptxTrace_IncludesPptxProperties()
    {
        var theme = new ThemeDefinition
        {
            Pptx = new ThemePptxSection { BaseFontSize = 28 }
        };
        var input = new ThemeCascadeInput { Theme = theme };
        var (_, trace) = ThemeCascadeResolver.ResolveWithTrace(input);

        trace.ShouldContain(e => e.Property == "Pptx.BaseFontSize" && e.Source == CascadeLayer.Theme);
    }
}
