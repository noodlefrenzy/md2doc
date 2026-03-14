// agent-notes: { ctx: "md2 theme extract — extracts DOCX template styles to YAML", deps: [DocxStyleExtractor.cs, ThemeDefinition.cs, TemplateSafetyChecker.cs, YamlDotNet], state: active, last: "sato@2026-03-14" }

using System.CommandLine;
using Md2.Themes;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Md2.Cli;

public static class ThemeExtractCommand
{
    public static Command Create()
    {
        var templateArgument = new Argument<FileInfo>("template")
        {
            Description = "Path to the DOCX template file to extract styles from"
        };

        var outputOption = new Option<FileInfo?>("-o", "--output")
        {
            Description = "Output YAML file path (default: stdout)"
        };

        var command = new Command("extract", "Extract styles from a DOCX template into a theme YAML file")
        {
            templateArgument,
            outputOption
        };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var template = parseResult.GetValue(templateArgument);
            var output = parseResult.GetValue(outputOption);

            return await ExecuteAsync(template!, output);
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(FileInfo template, FileInfo? output)
    {
        if (!template.Exists)
        {
            await Console.Error.WriteLineAsync($"Error: Template file not found: {template.FullName}");
            return 2;
        }

        try
        {
            var safety = TemplateSafetyChecker.Check(template.FullName);
            if (!safety.IsValid)
            {
                foreach (var error in safety.Errors)
                {
                    await Console.Error.WriteLineAsync($"Error: {error}");
                }
                return 2;
            }

            var theme = DocxStyleExtractor.Extract(template.FullName);

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();

            var yaml = "# Theme extracted from: " + template.Name + "\n"
                     + "# Edit values below, then use with: md2 --theme <this-file>\n\n"
                     + serializer.Serialize(theme);

            if (output is not null)
            {
                await File.WriteAllTextAsync(output.FullName, yaml);
                Console.WriteLine(output.FullName);
            }
            else
            {
                Console.Write(yaml);
            }

            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    }
}
