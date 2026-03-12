// agent-notes: { ctx: "md2 theme validate — validates theme YAML file", deps: [ThemeParser.cs, ThemeValidator.cs], state: active, last: "sato@2026-03-12" }

using System.CommandLine;
using System.CommandLine.Invocation;
using Md2.Themes;

namespace Md2.Cli;

public static class ThemeValidateCommand
{
    public static Command Create()
    {
        var themeArgument = new Argument<FileInfo>(
            name: "theme",
            description: "Path to the theme YAML file to validate");

        var command = new Command("validate", "Validate a theme YAML file for errors and warnings")
        {
            themeArgument
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var themeFile = context.ParseResult.GetValueForArgument(themeArgument);
            context.ExitCode = await ExecuteAsync(themeFile);
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(FileInfo themeFile)
    {
        if (!themeFile.Exists)
        {
            await Console.Error.WriteLineAsync($"Error: Theme file not found: {themeFile.FullName}");
            return 2;
        }

        try
        {
            var theme = ThemeParser.ParseFile(themeFile.FullName);
            var issues = ThemeValidator.Validate(theme);

            if (issues.Count == 0)
            {
                Console.WriteLine("Theme is valid.");
                return 0;
            }

            var errors = 0;
            var warnings = 0;

            foreach (var issue in issues)
            {
                var prefix = issue.Severity == ValidationSeverity.Error ? "Error" : "Warning";
                var path = issue.PropertyPath is not null ? $" [{issue.PropertyPath}]" : "";
                Console.Error.WriteLine($"{prefix}{path}: {issue.Message}");

                if (issue.Severity == ValidationSeverity.Error) errors++;
                else warnings++;
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine($"{errors} error(s), {warnings} warning(s).");

            return errors > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: Failed to parse theme file: {ex.Message}");
            return 1;
        }
    }
}
