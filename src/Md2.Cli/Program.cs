// agent-notes: { ctx: "CLI entry point with System.CommandLine", deps: [System.CommandLine, ConvertCommand], state: active, last: "sato@2026-03-11" }

using System.CommandLine;
using Md2.Cli;

var rootCommand = ConvertCommand.Create();
return await rootCommand.InvokeAsync(args);
