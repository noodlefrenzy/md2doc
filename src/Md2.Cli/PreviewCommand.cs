// agent-notes: { ctx: "CLI subcommand: live preview with hot-reload", deps: [System.CommandLine, PreviewSession, ThemeCascadeResolver, Md2.Preview], state: active, last: "sato@2026-03-13" }

using System.CommandLine;
using System.CommandLine.Invocation;
using Markdig;
using Md2.Core.Exceptions;
using Md2.Core.Pipeline;
using Md2.Parsing;
using Md2.Preview;
using Md2.Themes;

namespace Md2.Cli;

public static class PreviewCommand
{
    public static Command Create()
    {
        var inputArgument = new Argument<FileInfo>(
            name: "input",
            description: "Path to the Markdown input file")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var presetOption = new Option<string?>(
            aliases: new[] { "--preset" },
            description: "Theme preset name (default: 'default')");

        var themeOption = new Option<FileInfo?>(
            aliases: new[] { "--theme" },
            description: "Path to a theme YAML file");

        var styleOption = new Option<string[]>(
            aliases: new[] { "--style" },
            description: "Style overrides as key=value pairs (e.g. --style colors.primary=FF0000)")
        {
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.ZeroOrMore,
        };

        var command = new Command("preview", "Open a live HTML preview of a Markdown file")
        {
            inputArgument,
            presetOption,
            themeOption,
            styleOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var input = context.ParseResult.GetValueForArgument(inputArgument);
            var preset = context.ParseResult.GetValueForOption(presetOption);
            var themeFile = context.ParseResult.GetValueForOption(themeOption);
            var styles = context.ParseResult.GetValueForOption(styleOption) ?? [];
            var cancellationToken = context.GetCancellationToken();

            context.ExitCode = await ExecuteAsync(input, preset, themeFile, styles, cancellationToken);
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(
        FileInfo input,
        string? preset,
        FileInfo? themeFile,
        string[] styles,
        CancellationToken cancellationToken)
    {
        if (!input.Exists)
        {
            await Console.Error.WriteLineAsync($"Error: Input file not found: {input.FullName}");
            return 2;
        }

        try
        {
            // Resolve theme via cascade
            var cascadeInput = new ThemeCascadeInput
            {
                PresetName = preset ?? "default"
            };

            if (themeFile is not null)
            {
                if (!themeFile.Exists)
                {
                    await Console.Error.WriteLineAsync($"Error: Theme file not found: {themeFile.FullName}");
                    return 2;
                }
                cascadeInput.Theme = ThemeParser.ParseFile(themeFile.FullName);

                var issues = ThemeValidator.Validate(cascadeInput.Theme);
                foreach (var issue in issues)
                {
                    var pfx = issue.Severity == ValidationSeverity.Error ? "Error" : "Warning";
                    var path = issue.PropertyPath is not null ? $" [{issue.PropertyPath}]" : "";
                    await Console.Error.WriteLineAsync($"{pfx}: Theme{path}: {issue.Message}");
                }
                if (issues.Any(i => i.Severity == ValidationSeverity.Error))
                {
                    return 2;
                }
            }

            // Apply CLI style overrides
            if (styles.Length > 0)
            {
                cascadeInput.CliOverrides = ThemeResolveCommand.ParseStyleOverrides(styles);

                var cliIssues = ThemeValidator.Validate(cascadeInput.CliOverrides);
                foreach (var issue in cliIssues)
                {
                    var pfx = issue.Severity == ValidationSeverity.Error ? "Error" : "Warning";
                    var path = issue.PropertyPath is not null ? $" [--style {issue.PropertyPath}]" : "";
                    await Console.Error.WriteLineAsync($"{pfx}: Style override{path}: {issue.Message}");
                }
                if (cliIssues.Any(i => i.Severity == ValidationSeverity.Error))
                {
                    return 2;
                }
            }

            var theme = ThemeCascadeResolver.Resolve(cascadeInput);

            // Build Markdig pipeline (same as ConvertCommand but without transform-heavy extensions)
            var pipeline = new Markdig.MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            await Console.Error.WriteLineAsync($"Starting preview for {input.Name}...");
            await Console.Error.WriteLineAsync("Press Ctrl+C to stop.");

            await using var session = new PreviewSession(
                input.FullName,
                theme,
                pipeline,
                msg => Console.Error.WriteLine(msg));

            await session.RunAsync(cancellationToken);
            return 0;
        }
        catch (Md2Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.UserMessage}");
            return 1;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    }
}
