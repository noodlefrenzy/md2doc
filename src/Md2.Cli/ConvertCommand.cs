// agent-notes: { ctx: "Root CLI command: markdown to docx conversion", deps: [System.CommandLine, ConversionPipeline, DocxEmitter, ILogger], state: active, last: "sato@2026-03-12" }

using System.CommandLine;
using System.CommandLine.Invocation;
using Markdig;
using Md2.Core.Exceptions;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Emit.Docx;
using Md2.Highlight;
using Md2.Parsing;
using Microsoft.Extensions.Logging;

namespace Md2.Cli;

public static class ConvertCommand
{
    public static RootCommand Create()
    {
        var inputArgument = new Argument<FileInfo>(
            name: "input",
            description: "Path to the Markdown input file")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var outputOption = new Option<FileInfo?>(
            aliases: new[] { "-o", "--output" },
            description: "Path to the output DOCX file. Derived from input name if omitted.");

        var quietOption = new Option<bool>(
            aliases: new[] { "-q", "--quiet" },
            description: "Suppress warnings");

        var verboseOption = new Option<bool>(
            aliases: new[] { "-v", "--verbose" },
            description: "Enable verbose output");

        var debugOption = new Option<bool>(
            aliases: new[] { "--debug" },
            description: "Enable debug-level logging with full diagnostics");

        var rootCommand = new RootCommand("md2 - Convert Markdown to polished DOCX files")
        {
            inputArgument,
            outputOption,
            quietOption,
            verboseOption,
            debugOption
        };

        rootCommand.SetHandler(async (InvocationContext context) =>
        {
            var input = context.ParseResult.GetValueForArgument(inputArgument);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var quiet = context.ParseResult.GetValueForOption(quietOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var debug = context.ParseResult.GetValueForOption(debugOption);

            context.ExitCode = await ExecuteAsync(input, output, quiet, verbose, debug, context);
        });

        return rootCommand;
    }

    private static async Task<int> ExecuteAsync(
        FileInfo input,
        FileInfo? output,
        bool quiet,
        bool verbose,
        bool debug,
        InvocationContext context)
    {
        // Validate input file exists
        if (!input.Exists)
        {
            await Console.Error.WriteLineAsync($"Error: Input file not found: {input.FullName}");
            return 2;
        }

        // Derive output path if not specified
        var outputPath = output?.FullName
            ?? Path.ChangeExtension(input.FullName, ".docx");

        // Configure logging based on flags
        var logLevel = debug ? LogLevel.Debug : verbose ? LogLevel.Information : LogLevel.Warning;
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(logLevel);
            if (verbose || debug)
            {
                builder.AddSimpleConsole(opts =>
                {
                    opts.SingleLine = true;
                    opts.TimestampFormat = "HH:mm:ss ";
                    opts.UseUtcTimestamp = false;
                });
            }
        });
        var logger = loggerFactory.CreateLogger<ConversionPipeline>();

        try
        {
            logger.LogInformation("Reading: {Path}", input.FullName);

            var markdown = await File.ReadAllTextAsync(input.FullName);

            // Parse
            var pipeline = new ConversionPipeline(logger);
            var parserOptions = new ParserOptions();
            var doc = pipeline.Parse(markdown, parserOptions);

            // Transform
            pipeline.RegisterTransform(new YamlFrontMatterExtractor());
            pipeline.RegisterTransform(new SmartTypographyTransform());
            pipeline.RegisterTransform(new SyntaxHighlightAnnotator());
            var transformOptions = new TransformOptions();
            var transformed = pipeline.Transform(doc, transformOptions);

            // Emit
            var theme = ResolvedTheme.CreateDefault();
            var emitOptions = new EmitOptions();
            var emitter = new DocxEmitter();

            using var fileStream = File.Create(outputPath);
            await pipeline.Emit(transformed, theme, emitter, emitOptions, fileStream);

            logger.LogInformation("Written: {Path}", outputPath);

            // Output path to stdout unless suppressed
            if (!quiet)
            {
                Console.WriteLine(outputPath);
            }
            return 0;
        }
        catch (Md2Exception ex)
        {
            // User-facing error: show the user message
            await Console.Error.WriteLineAsync($"Error: {ex.UserMessage}");
            if (debug)
            {
                await Console.Error.WriteLineAsync(ex.ToString());
            }
            return 1;
        }
        catch (Exception ex)
        {
            // Internal error: unexpected failure
            await Console.Error.WriteLineAsync($"Error: An unexpected error occurred. {ex.Message}");
            if (debug)
            {
                await Console.Error.WriteLineAsync(ex.ToString());
            }
            else
            {
                await Console.Error.WriteLineAsync("Run with --debug for full diagnostics.");
            }
            return 1;
        }
    }
}
