// agent-notes: { ctx: "md2 theme resolve — displays cascade resolution table", deps: [ThemeCascadeResolver.cs, ThemeResolveFormatter.cs, ThemeParser.cs, PresetRegistry.cs], state: active, last: "sato@2026-03-12" }

using System.CommandLine;
using System.CommandLine.Invocation;
using Md2.Themes;

namespace Md2.Cli;

public static class ThemeResolveCommand
{
    public static Command Create()
    {
        var presetOption = new Option<string?>(
            aliases: ["--preset"],
            description: "Preset name (default: 'default')");

        var themeOption = new Option<FileInfo?>(
            aliases: ["--theme"],
            description: "Path to a theme YAML file");

        var templateOption = new Option<FileInfo?>(
            aliases: ["--template"],
            description: "Path to a DOCX template file");

        var styleOption = new Option<string[]>(
            aliases: ["--style"],
            description: "Style overrides as key=value pairs (e.g. --style colors.primary=#FF0000)")
        {
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

        command.SetHandler((InvocationContext context) =>
        {
            var preset = context.ParseResult.GetValueForOption(presetOption);
            var themeFile = context.ParseResult.GetValueForOption(themeOption);
            var templateFile = context.ParseResult.GetValueForOption(templateOption);
            var styles = context.ParseResult.GetValueForOption(styleOption) ?? [];

            context.ExitCode = Execute(preset, themeFile, templateFile, styles);
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

            ApplyStyleOverride(theme, key, value);
        }

        return theme;
    }

    private static void ApplyStyleOverride(ThemeDefinition theme, string key, string value)
    {
        // Strip optional section prefix for flat keys
        var effectiveKey = key.Contains('.') ? key : key;

        switch (effectiveKey)
        {
            // Typography
            case "typography.headingfont" or "headingfont":
                theme.Typography ??= new ThemeTypographySection();
                theme.Typography.HeadingFont = value;
                break;
            case "typography.bodyfont" or "bodyfont":
                theme.Typography ??= new ThemeTypographySection();
                theme.Typography.BodyFont = value;
                break;
            case "typography.monofont" or "monofont":
                theme.Typography ??= new ThemeTypographySection();
                theme.Typography.MonoFont = value;
                break;
            case "typography.monofontfallback" or "monofontfallback":
                theme.Typography ??= new ThemeTypographySection();
                theme.Typography.MonoFontFallback = value;
                break;

            // Colors
            case "colors.primary" or "primary":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.Primary = value;
                break;
            case "colors.secondary" or "secondary":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.Secondary = value;
                break;
            case "colors.bodytext" or "bodytext":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.BodyText = value;
                break;
            case "colors.codebackground" or "codebackground":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.CodeBackground = value;
                break;
            case "colors.codeborder" or "codeborder":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.CodeBorder = value;
                break;
            case "colors.link" or "link":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.Link = value;
                break;
            case "colors.tableheaderbackground" or "tableheaderbackground":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.TableHeaderBackground = value;
                break;
            case "colors.tableheaderforeground" or "tableheaderforeground":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.TableHeaderForeground = value;
                break;
            case "colors.tableborder" or "tableborder":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.TableBorder = value;
                break;
            case "colors.tablealternaterow" or "tablealternaterow":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.TableAlternateRow = value;
                break;
            case "colors.blockquoteborder" or "blockquoteborder":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.BlockquoteBorder = value;
                break;
            case "colors.blockquotetext" or "blockquotetext":
                theme.Colors ??= new ThemeColorsSection();
                theme.Colors.BlockquoteText = value;
                break;

            // Docx sizes
            case "docx.basefontsize" or "basefontsize":
                theme.Docx ??= new ThemeDocxSection();
                if (double.TryParse(value, out var baseFs)) theme.Docx.BaseFontSize = baseFs;
                break;
            case "docx.heading1size" or "heading1size":
                theme.Docx ??= new ThemeDocxSection();
                if (double.TryParse(value, out var h1)) theme.Docx.Heading1Size = h1;
                break;
            case "docx.heading2size" or "heading2size":
                theme.Docx ??= new ThemeDocxSection();
                if (double.TryParse(value, out var h2)) theme.Docx.Heading2Size = h2;
                break;
            case "docx.heading3size" or "heading3size":
                theme.Docx ??= new ThemeDocxSection();
                if (double.TryParse(value, out var h3)) theme.Docx.Heading3Size = h3;
                break;
            case "docx.heading4size" or "heading4size":
                theme.Docx ??= new ThemeDocxSection();
                if (double.TryParse(value, out var h4)) theme.Docx.Heading4Size = h4;
                break;
            case "docx.heading5size" or "heading5size":
                theme.Docx ??= new ThemeDocxSection();
                if (double.TryParse(value, out var h5)) theme.Docx.Heading5Size = h5;
                break;
            case "docx.heading6size" or "heading6size":
                theme.Docx ??= new ThemeDocxSection();
                if (double.TryParse(value, out var h6)) theme.Docx.Heading6Size = h6;
                break;
            case "docx.linespacing" or "linespacing":
                theme.Docx ??= new ThemeDocxSection();
                if (double.TryParse(value, out var ls)) theme.Docx.LineSpacing = ls;
                break;
            case "docx.tableborderwidth" or "tableborderwidth":
                theme.Docx ??= new ThemeDocxSection();
                if (int.TryParse(value, out var tbw)) theme.Docx.TableBorderWidth = tbw;
                break;
            case "docx.blockquoteindenttwips" or "blockquoteindenttwips":
                theme.Docx ??= new ThemeDocxSection();
                if (int.TryParse(value, out var bqi)) theme.Docx.BlockquoteIndentTwips = bqi;
                break;

            default:
                Console.Error.WriteLine($"Warning: Unknown style property '{key}'. Ignoring.");
                break;
        }
    }
}
