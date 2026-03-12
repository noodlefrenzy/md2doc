// agent-notes: { ctx: "Root CLI command: markdown to docx conversion", deps: [System.CommandLine, ConversionPipeline, DocxEmitter], state: active, last: "sato@2026-03-12" }

using System.CommandLine;
using System.CommandLine.Invocation;
using Markdig;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Emit.Docx;
using Md2.Parsing;

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

        var rootCommand = new RootCommand("md2 - Convert Markdown to polished DOCX files")
        {
            inputArgument,
            outputOption,
            quietOption,
            verboseOption
        };

        rootCommand.SetHandler(async (InvocationContext context) =>
        {
            var input = context.ParseResult.GetValueForArgument(inputArgument);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var quiet = context.ParseResult.GetValueForOption(quietOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            context.ExitCode = await ExecuteAsync(input, output, quiet, verbose, context);
        });

        return rootCommand;
    }

    private static async Task<int> ExecuteAsync(
        FileInfo input,
        FileInfo? output,
        bool quiet,
        bool verbose,
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

        try
        {
            if (verbose)
            {
                await Console.Error.WriteLineAsync($"Reading: {input.FullName}");
            }

            var markdown = await File.ReadAllTextAsync(input.FullName);

            // Parse
            var pipeline = new ConversionPipeline();
            var parserOptions = new ParserOptions();
            var doc = pipeline.Parse(markdown, parserOptions);

            // Transform
            pipeline.RegisterTransform(new YamlFrontMatterExtractor());
            pipeline.RegisterTransform(new SmartTypographyTransform());
            var transformOptions = new TransformOptions();
            var transformed = pipeline.Transform(doc, transformOptions);

            // Emit
            var theme = ResolvedTheme.CreateDefault();
            var emitOptions = new EmitOptions();
            var emitter = new DocxEmitter();

            using var fileStream = File.Create(outputPath);
            await pipeline.Emit(transformed, theme, emitter, emitOptions, fileStream);

            if (verbose)
            {
                await Console.Error.WriteLineAsync($"Written: {outputPath}");
            }

            // Output path to stdout unless suppressed
            if (!quiet)
            {
                Console.WriteLine(outputPath);
            }
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            if (verbose)
            {
                await Console.Error.WriteLineAsync(ex.StackTrace);
            }
            return 1;
        }
    }
}
