// agent-notes: { ctx: "md2 theme resolve — displays cascade resolution table with PPTX support", deps: [ThemeCascadeResolver.cs, ThemeResolveFormatter.cs, ThemeParser.cs, PresetRegistry.cs], state: active, last: "sato@2026-03-15" }

using System.CommandLine;
using System.Globalization;
using Md2.Themes;

namespace Md2.Cli;

public static class ThemeResolveCommand
{
    public static Command Create()
    {
        var presetOption = new Option<string?>("--preset")
        {
            Description = "Preset name (default: 'default')"
        };

        var themeOption = new Option<FileInfo?>("--theme")
        {
            Description = "Path to a theme YAML file"
        };

        var templateOption = new Option<FileInfo?>("--template")
        {
            Description = "Path to a DOCX template file"
        };

        var styleOption = new Option<string[]>("--style")
        {
            Description = "Style overrides as key=value pairs (e.g. --style colors.primary=#FF0000)",
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.ZeroOrMore,
        };

        var command = new Command("resolve", "Display resolved theme properties with cascade layer attribution")
        {
            presetOption,
            themeOption,
            templateOption,
            styleOption
        };

        command.SetAction((ParseResult parseResult) =>
        {
            var preset = parseResult.GetValue(presetOption);
            var themeFile = parseResult.GetValue(themeOption);
            var templateFile = parseResult.GetValue(templateOption);
            var styles = parseResult.GetValue(styleOption) ?? [];

            return Execute(preset, themeFile, templateFile, styles);
        });

        return command;
    }

    internal static int Execute(string? preset, FileInfo? themeFile, FileInfo? templateFile, string[] styles)
    {
        try
        {
            var input = new ThemeCascadeInput
            {
                PresetName = preset ?? "default"
            };

            // Load theme YAML if specified
            if (themeFile is not null)
            {
                if (!themeFile.Exists)
                {
                    Console.Error.WriteLine($"Error: Theme file not found: {themeFile.FullName}");
                    return 2;
                }
                input.Theme = ThemeParser.ParseFile(themeFile.FullName);
            }

            // Load template if specified
            if (templateFile is not null)
            {
                if (!templateFile.Exists)
                {
                    Console.Error.WriteLine($"Error: Template file not found: {templateFile.FullName}");
                    return 2;
                }
                // Template extraction is a future feature — for now, just validate the file exists
                var safety = TemplateSafetyChecker.Check(templateFile.FullName);
                if (!safety.IsValid)
                {
                    foreach (var error in safety.Errors)
                        Console.Error.WriteLine($"Error: {error}");
                    return 2;
                }
            }

            // Parse --style overrides into a ThemeDefinition
            if (styles.Length > 0)
            {
                input.CliOverrides = ParseStyleOverrides(styles);
            }

            var (theme, trace) = ThemeCascadeResolver.ResolveWithTrace(input);
            Console.Write(ThemeResolveFormatter.Format(theme, trace));
            return 0;
        }
        catch (ThemeParseException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Parses key=value style overrides into a ThemeDefinition.
    /// Supports dotted paths like "colors.primary=FF0000" or "docx.baseFontSize=14".
    /// </summary>
    internal static ThemeDefinition ParseStyleOverrides(string[] styles)
    {
        var theme = new ThemeDefinition();

        foreach (var style in styles)
        {
            var eqIndex = style.IndexOf('=');
            if (eqIndex < 0)
            {
                Console.Error.WriteLine($"Warning: Ignoring invalid style override '{style}' (expected key=value format).");
                continue;
            }

            var key = style[..eqIndex].Trim().ToLowerInvariant();
            var value = style[(eqIndex + 1)..].Trim();

            if (!ApplyStyleOverride(theme, key, value))
            {
                Console.Error.WriteLine($"Warning: Unknown style property '{key}'. Ignoring.");
            }
        }

        return theme;
    }

    // ── Data-driven style override registry ──────────────────────────────

    private delegate void StringSetter(ThemeDefinition t, string v);
    private delegate void DoubleSetter(ThemeDefinition t, double v);
    private delegate void IntSetter(ThemeDefinition t, int v);
    private delegate void UintSetter(ThemeDefinition t, uint v);

    private static readonly Dictionary<string, StringSetter> StringOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["typography.headingfont"] = (t, v) => { t.Typography ??= new(); t.Typography.HeadingFont = v; },
        ["headingfont"] = (t, v) => { t.Typography ??= new(); t.Typography.HeadingFont = v; },
        ["typography.bodyfont"] = (t, v) => { t.Typography ??= new(); t.Typography.BodyFont = v; },
        ["bodyfont"] = (t, v) => { t.Typography ??= new(); t.Typography.BodyFont = v; },
        ["typography.monofont"] = (t, v) => { t.Typography ??= new(); t.Typography.MonoFont = v; },
        ["monofont"] = (t, v) => { t.Typography ??= new(); t.Typography.MonoFont = v; },
        ["typography.monofontfallback"] = (t, v) => { t.Typography ??= new(); t.Typography.MonoFontFallback = v; },
        ["monofontfallback"] = (t, v) => { t.Typography ??= new(); t.Typography.MonoFontFallback = v; },
        ["colors.primary"] = (t, v) => { t.Colors ??= new(); t.Colors.Primary = v; },
        ["primary"] = (t, v) => { t.Colors ??= new(); t.Colors.Primary = v; },
        ["colors.secondary"] = (t, v) => { t.Colors ??= new(); t.Colors.Secondary = v; },
        ["secondary"] = (t, v) => { t.Colors ??= new(); t.Colors.Secondary = v; },
        ["colors.bodytext"] = (t, v) => { t.Colors ??= new(); t.Colors.BodyText = v; },
        ["bodytext"] = (t, v) => { t.Colors ??= new(); t.Colors.BodyText = v; },
        ["colors.codebackground"] = (t, v) => { t.Colors ??= new(); t.Colors.CodeBackground = v; },
        ["codebackground"] = (t, v) => { t.Colors ??= new(); t.Colors.CodeBackground = v; },
        ["colors.codeborder"] = (t, v) => { t.Colors ??= new(); t.Colors.CodeBorder = v; },
        ["codeborder"] = (t, v) => { t.Colors ??= new(); t.Colors.CodeBorder = v; },
        ["colors.link"] = (t, v) => { t.Colors ??= new(); t.Colors.Link = v; },
        ["link"] = (t, v) => { t.Colors ??= new(); t.Colors.Link = v; },
        ["colors.tableheaderbackground"] = (t, v) => { t.Colors ??= new(); t.Colors.TableHeaderBackground = v; },
        ["tableheaderbackground"] = (t, v) => { t.Colors ??= new(); t.Colors.TableHeaderBackground = v; },
        ["colors.tableheaderforeground"] = (t, v) => { t.Colors ??= new(); t.Colors.TableHeaderForeground = v; },
        ["tableheaderforeground"] = (t, v) => { t.Colors ??= new(); t.Colors.TableHeaderForeground = v; },
        ["colors.tableborder"] = (t, v) => { t.Colors ??= new(); t.Colors.TableBorder = v; },
        ["tableborder"] = (t, v) => { t.Colors ??= new(); t.Colors.TableBorder = v; },
        ["colors.tablealternaterow"] = (t, v) => { t.Colors ??= new(); t.Colors.TableAlternateRow = v; },
        ["tablealternaterow"] = (t, v) => { t.Colors ??= new(); t.Colors.TableAlternateRow = v; },
        ["colors.blockquoteborder"] = (t, v) => { t.Colors ??= new(); t.Colors.BlockquoteBorder = v; },
        ["blockquoteborder"] = (t, v) => { t.Colors ??= new(); t.Colors.BlockquoteBorder = v; },
        ["colors.blockquotetext"] = (t, v) => { t.Colors ??= new(); t.Colors.BlockquoteText = v; },
        ["blockquotetext"] = (t, v) => { t.Colors ??= new(); t.Colors.BlockquoteText = v; },
        // PPTX per-format color overrides
        ["pptx.colors.bodytext"] = (t, v) => { t.Pptx ??= new(); t.Pptx.Colors ??= new(); t.Pptx.Colors.BodyText = v; },
        ["pptx.colors.primary"] = (t, v) => { t.Pptx ??= new(); t.Pptx.Colors ??= new(); t.Pptx.Colors.Primary = v; },
        ["pptx.colors.secondary"] = (t, v) => { t.Pptx ??= new(); t.Pptx.Colors ??= new(); t.Pptx.Colors.Secondary = v; },
        // PPTX slide size
        ["pptx.slidesize"] = (t, v) => { t.Pptx ??= new(); t.Pptx.SlideSize = v; },
        // PPTX background
        ["pptx.background.color"] = (t, v) => { t.Pptx ??= new(); t.Pptx.Background ??= new(); t.Pptx.Background.Color = v; },
    };

    private static readonly Dictionary<string, DoubleSetter> DoubleOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["docx.basefontsize"] = (t, v) => { t.Docx ??= new(); t.Docx.BaseFontSize = v; },
        ["basefontsize"] = (t, v) => { t.Docx ??= new(); t.Docx.BaseFontSize = v; },
        ["docx.heading1size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading1Size = v; },
        ["heading1size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading1Size = v; },
        ["docx.heading2size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading2Size = v; },
        ["heading2size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading2Size = v; },
        ["docx.heading3size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading3Size = v; },
        ["heading3size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading3Size = v; },
        ["docx.heading4size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading4Size = v; },
        ["heading4size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading4Size = v; },
        ["docx.heading5size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading5Size = v; },
        ["heading5size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading5Size = v; },
        ["docx.heading6size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading6Size = v; },
        ["heading6size"] = (t, v) => { t.Docx ??= new(); t.Docx.Heading6Size = v; },
        ["docx.linespacing"] = (t, v) => { t.Docx ??= new(); t.Docx.LineSpacing = v; },
        ["linespacing"] = (t, v) => { t.Docx ??= new(); t.Docx.LineSpacing = v; },
        // PPTX font sizes
        ["pptx.basefontsize"] = (t, v) => { t.Pptx ??= new(); t.Pptx.BaseFontSize = v; },
        ["pptx.heading1size"] = (t, v) => { t.Pptx ??= new(); t.Pptx.Heading1Size = v; },
        ["pptx.heading2size"] = (t, v) => { t.Pptx ??= new(); t.Pptx.Heading2Size = v; },
        ["pptx.heading3size"] = (t, v) => { t.Pptx ??= new(); t.Pptx.Heading3Size = v; },
        // PPTX layout sizes
        ["pptx.titleslide.titlesize"] = (t, v) => { t.Pptx ??= new(); t.Pptx.TitleSlide ??= new(); t.Pptx.TitleSlide.TitleSize = v; },
        ["pptx.titleslide.subtitlesize"] = (t, v) => { t.Pptx ??= new(); t.Pptx.TitleSlide ??= new(); t.Pptx.TitleSlide.SubtitleSize = v; },
        ["pptx.content.titlesize"] = (t, v) => { t.Pptx ??= new(); t.Pptx.Content ??= new(); t.Pptx.Content.TitleSize = v; },
        ["pptx.content.bodysize"] = (t, v) => { t.Pptx ??= new(); t.Pptx.Content ??= new(); t.Pptx.Content.BodySize = v; },
        ["pptx.content.bulletindent"] = (t, v) => { t.Pptx ??= new(); t.Pptx.Content ??= new(); t.Pptx.Content.BulletIndent = v; },
        ["pptx.sectiondivider.titlesize"] = (t, v) => { t.Pptx ??= new(); t.Pptx.SectionDivider ??= new(); t.Pptx.SectionDivider.TitleSize = v; },
        ["pptx.twocolumn.gutter"] = (t, v) => { t.Pptx ??= new(); t.Pptx.TwoColumn ??= new(); t.Pptx.TwoColumn.Gutter = v; },
        ["pptx.codeblock.fontsize"] = (t, v) => { t.Pptx ??= new(); t.Pptx.CodeBlock ??= new(); t.Pptx.CodeBlock.FontSize = v; },
        ["pptx.codeblock.padding"] = (t, v) => { t.Pptx ??= new(); t.Pptx.CodeBlock ??= new(); t.Pptx.CodeBlock.Padding = v; },
        ["pptx.codeblock.borderradius"] = (t, v) => { t.Pptx ??= new(); t.Pptx.CodeBlock ??= new(); t.Pptx.CodeBlock.BorderRadius = v; },
    };

    private static readonly Dictionary<string, IntSetter> IntOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["docx.tableborderwidth"] = (t, v) => { t.Docx ??= new(); t.Docx.TableBorderWidth = v; },
        ["tableborderwidth"] = (t, v) => { t.Docx ??= new(); t.Docx.TableBorderWidth = v; },
        ["docx.blockquoteindenttwips"] = (t, v) => { t.Docx ??= new(); t.Docx.BlockquoteIndentTwips = v; },
        ["blockquoteindenttwips"] = (t, v) => { t.Docx ??= new(); t.Docx.BlockquoteIndentTwips = v; },
        ["docx.page.margintop"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.MarginTop = v; },
        ["margintop"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.MarginTop = v; },
        ["docx.page.marginbottom"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.MarginBottom = v; },
        ["marginbottom"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.MarginBottom = v; },
        ["docx.page.marginleft"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.MarginLeft = v; },
        ["marginleft"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.MarginLeft = v; },
        ["docx.page.marginright"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.MarginRight = v; },
        ["marginright"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.MarginRight = v; },
    };

    private static readonly Dictionary<string, UintSetter> UintOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["docx.page.width"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.Width = v; },
        ["pagewidth"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.Width = v; },
        ["docx.page.height"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.Height = v; },
        ["pageheight"] = (t, v) => { t.Docx ??= new(); t.Docx.Page ??= new(); t.Docx.Page.Height = v; },
    };

    /// <summary>
    /// Applies a single style override. Returns false if the key is unknown.
    /// </summary>
    private static bool ApplyStyleOverride(ThemeDefinition theme, string key, string value)
    {
        if (StringOverrides.TryGetValue(key, out var strSetter))
        {
            strSetter(theme, value);
            return true;
        }

        if (DoubleOverrides.TryGetValue(key, out var dblSetter))
        {
            if (!double.TryParse(value, CultureInfo.InvariantCulture, out var dblVal))
            {
                Console.Error.WriteLine($"Warning: Cannot parse '{value}' as a number for '{key}'. Ignoring.");
                return true; // Key is known, value is bad — don't also warn about unknown key
            }
            dblSetter(theme, dblVal);
            return true;
        }

        if (IntOverrides.TryGetValue(key, out var intSetter))
        {
            if (!int.TryParse(value, CultureInfo.InvariantCulture, out var intVal))
            {
                Console.Error.WriteLine($"Warning: Cannot parse '{value}' as an integer for '{key}'. Ignoring.");
                return true;
            }
            intSetter(theme, intVal);
            return true;
        }

        if (UintOverrides.TryGetValue(key, out var uintSetter))
        {
            if (!uint.TryParse(value, CultureInfo.InvariantCulture, out var uintVal))
            {
                Console.Error.WriteLine($"Warning: Cannot parse '{value}' as a positive integer for '{key}'. Ignoring.");
                return true;
            }
            uintSetter(theme, uintVal);
            return true;
        }

        return false;
    }
}
