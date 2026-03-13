// agent-notes: { ctx: "Integration tests for preview CLI command", deps: [PreviewCommand], state: active, last: "tara@2026-03-13" }

using System.CommandLine;
using Shouldly;

namespace Md2.Integration.Tests;

public class PreviewCommandTests
{
    [Fact]
    public async Task Preview_MissingFile_ReturnsExitCode2()
    {
        var command = Md2.Cli.PreviewCommand.Create();
        var result = await command.Parse("nonexistent.md").InvokeAsync();

        result.ShouldBe(2);
    }

    [Fact]
    public async Task Preview_InvalidThemeFile_ReturnsExitCode2()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "# Test");
        try
        {
            var command = Md2.Cli.PreviewCommand.Create();
            var result = await command.Parse($"{tempFile} --theme nonexistent.yaml").InvokeAsync();

            result.ShouldBe(2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Create_ReturnsCommandNamedPreview()
    {
        var command = Md2.Cli.PreviewCommand.Create();

        command.Name.ShouldBe("preview");
        command.Description.ShouldBe("Open a live HTML preview of a Markdown file");
    }

    [Fact]
    public async Task Preview_InvalidPreset_ReturnsExitCode2()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "# Test");
        try
        {
            var command = Md2.Cli.PreviewCommand.Create();
            var result = await command.Parse($"{tempFile} --preset nonexistent").InvokeAsync();

            result.ShouldBe(2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
