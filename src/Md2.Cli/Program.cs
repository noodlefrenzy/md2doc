// agent-notes: { ctx: "CLI entry point with System.CommandLine", deps: [System.CommandLine, ConvertCommand, ThemeResolveCommand, ThemeExtractCommand, ThemeValidateCommand, ThemeListCommand, DoctorCommand, PreviewCommand], state: active, last: "sato@2026-03-13" }

using System.CommandLine;
using Md2.Cli;

var rootCommand = ConvertCommand.Create();

// Theme subcommands
var themeCommand = new Command("theme", "Theme management commands");
themeCommand.Add(ThemeResolveCommand.Create());
themeCommand.Add(ThemeExtractCommand.Create());
themeCommand.Add(ThemeValidateCommand.Create());
themeCommand.Add(ThemeListCommand.Create());
rootCommand.Add(themeCommand);
rootCommand.Add(DoctorCommand.Create());
rootCommand.Add(PreviewCommand.Create());

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
