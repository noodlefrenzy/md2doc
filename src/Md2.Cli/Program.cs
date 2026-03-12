// agent-notes: { ctx: "CLI entry point with System.CommandLine", deps: [System.CommandLine, ConvertCommand, ThemeResolveCommand], state: active, last: "sato@2026-03-12" }

using System.CommandLine;
using Md2.Cli;

var rootCommand = ConvertCommand.Create();

// Theme subcommands
var themeCommand = new Command("theme", "Theme management commands");
themeCommand.AddCommand(ThemeResolveCommand.Create());
rootCommand.AddCommand(themeCommand);

return await rootCommand.InvokeAsync(args);
