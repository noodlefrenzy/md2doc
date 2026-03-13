// agent-notes: { ctx: "CLI entry point with System.CommandLine", deps: [System.CommandLine, ConvertCommand, ThemeResolveCommand, ThemeExtractCommand, ThemeValidateCommand, ThemeListCommand, DoctorCommand], state: active, last: "sato@2026-03-13" }

using System.CommandLine;
using Md2.Cli;

var rootCommand = ConvertCommand.Create();

// Theme subcommands
var themeCommand = new Command("theme", "Theme management commands");
themeCommand.AddCommand(ThemeResolveCommand.Create());
themeCommand.AddCommand(ThemeExtractCommand.Create());
themeCommand.AddCommand(ThemeValidateCommand.Create());
themeCommand.AddCommand(ThemeListCommand.Create());
rootCommand.AddCommand(themeCommand);
rootCommand.AddCommand(DoctorCommand.Create());

return await rootCommand.InvokeAsync(args);
