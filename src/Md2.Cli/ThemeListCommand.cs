// agent-notes: { ctx: "md2 theme list — lists available presets", deps: [PresetRegistry.cs], state: active, last: "sato@2026-03-13" }

using System.CommandLine;
using Md2.Themes;

namespace Md2.Cli;

public static class ThemeListCommand
{
    public static Command Create()
    {
        var command = new Command("list", "List available theme presets");

        command.SetAction((ParseResult parseResult) =>
        {
            var presets = PresetRegistry.ListPresets();

            foreach (var name in presets)
            {
                var theme = PresetRegistry.GetPreset(name);
                var description = theme.Meta?.Description ?? "";
                Console.WriteLine($"  {name,-15} {description}");
            }

            return 0;
        });

        return command;
    }
}
