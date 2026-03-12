// agent-notes: { ctx: "md2 theme list — lists available presets", deps: [PresetRegistry.cs], state: active, last: "sato@2026-03-12" }

using System.CommandLine;
using System.CommandLine.Invocation;
using Md2.Themes;

namespace Md2.Cli;

public static class ThemeListCommand
{
    public static Command Create()
    {
        var command = new Command("list", "List available theme presets");

        command.SetHandler((InvocationContext context) =>
        {
            var presets = PresetRegistry.ListPresets();

            foreach (var name in presets)
            {
                var theme = PresetRegistry.GetPreset(name);
                var description = theme.Meta?.Description ?? "";
                Console.WriteLine($"  {name,-15} {description}");
            }

            context.ExitCode = 0;
        });

        return command;
    }
}
