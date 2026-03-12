// agent-notes: { ctx: "Root CLI command: markdown to docx with cancellation support", deps: [System.CommandLine, ConversionPipeline, DocxEmitter, ThemeCascadeResolver, ILogger], state: active, last: "sato@2026-03-12" }

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using Markdig;
using Md2.Core.Exceptions;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Emit.Docx;
using Md2.Diagrams;
using Md2.Highlight;
using Md2.Math;
using Md2.Parsing;
using Md2.Themes;
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

        var presetOption = new Option<string?>(
            aliases: new[] { "--preset" },
            description: "Theme preset name (default: 'default')");

        var themeOption = new Option<FileInfo?>(
            aliases: new[] { "--theme" },
            description: "Path to a theme YAML file");

        var templateOption = new Option<FileInfo?>(
            aliases: new[] { "--template" },
            description: "Path to a DOCX template file");

        var styleOption = new Option<string[]>(
            aliases: new[] { "--style" },
            description: "Style overrides as key=value pairs (e.g. --style colors.primary=FF0000)")
        {
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.ZeroOrMore,
        };

        var tocOption = new Option<bool>(
            aliases: new[] { "--toc" },
            description: "Include a Table of Contents");

        var tocDepthOption = new Option<int>(
            aliases: new[] { "--toc-depth" },
            description: "TOC heading depth (1-6, default 3)")
        { IsRequired = false };
        tocDepthOption.SetDefaultValue(3);

        var coverOption = new Option<bool>(
            aliases: new[] { "--cover" },
            description: "Include a cover page from front matter metadata");

        var rootCommand = new RootCommand("Convert Markdown to polished DOCX files")
        {
            inputArgument,
            outputOption,
            quietOption,
            verboseOption,
            debugOption,
            presetOption,
            themeOption,
            templateOption,
            styleOption,
            tocOption,
            tocDepthOption,
            coverOption
        };
        rootCommand.Name = "md2";

        rootCommand.SetHandler(async (InvocationContext context) =>
        {
            var input = context.ParseResult.GetValueForArgument(inputArgument);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var quiet = context.ParseResult.GetValueForOption(quietOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var debug = context.ParseResult.GetValueForOption(debugOption);
            var preset = context.ParseResult.GetValueForOption(presetOption);
            var themeFile = context.ParseResult.GetValueForOption(themeOption);
            var templateFile = context.ParseResult.GetValueForOption(templateOption);
            var styles = context.ParseResult.GetValueForOption(styleOption) ?? [];
            var toc = context.ParseResult.GetValueForOption(tocOption);
            var tocDepth = context.ParseResult.GetValueForOption(tocDepthOption);
            var cover = context.ParseResult.GetValueForOption(coverOption);

            var cancellationToken = context.GetCancellationToken();
            context.ExitCode = await ExecuteAsync(input, output, quiet, verbose, debug, preset, themeFile, templateFile, styles, toc, tocDepth, cover, cancellationToken, context);
        });

        return rootCommand;
    }

    private static async Task<int> ExecuteAsync(
        FileInfo input,
        FileInfo? output,
        bool quiet,
        bool verbose,
        bool debug,
        string? preset,
        FileInfo? themeFile,
        FileInfo? templateFile,
        string[] styles,
        bool toc,
        int tocDepth,
        bool cover,
        CancellationToken cancellationToken,
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
            var totalSw = Stopwatch.StartNew();
            logger.LogInformation("Reading: {Path}", input.FullName);

            var markdown = await File.ReadAllTextAsync(input.FullName, cancellationToken);

            // Parse
            var parseSw = Stopwatch.StartNew();
            var pipeline = new ConversionPipeline(logger);
            var parserOptions = new ParserOptions();
            var doc = pipeline.Parse(markdown, parserOptions);
            parseSw.Stop();
            logger.LogInformation("Parse: {Elapsed}ms", parseSw.ElapsedMilliseconds);

            // Set up browser-based rendering (Mermaid + Math)
            await using var browserManager = new BrowserManager(
                loggerFactory.CreateLogger<BrowserManager>());
            var cacheDir = Path.Combine(Path.GetTempPath(), "md2-cache");
            var diagramCache = new DiagramCache(cacheDir, MermaidRenderer.MermaidVersion);
            var mermaidRenderer = new MermaidRenderer(browserManager, diagramCache,
                loggerFactory.CreateLogger<MermaidRenderer>());
            var latexConverter = new LatexToOmmlConverter(browserManager,
                loggerFactory.CreateLogger<LatexToOmmlConverter>());

            // Transform
            var transformSw = Stopwatch.StartNew();
            pipeline.RegisterTransform(new YamlFrontMatterExtractor());
            pipeline.RegisterTransform(new SmartTypographyTransform());
            pipeline.RegisterTransform(new MathBlockAnnotator(latexConverter));
            pipeline.RegisterTransform(new MermaidDiagramRenderer(mermaidRenderer));
            pipeline.RegisterTransform(new SyntaxHighlightAnnotator());
            var transformOptions = new TransformOptions { RenderMermaid = true };
            var transformed = pipeline.Transform(doc, transformOptions, cancellationToken);
            transformSw.Stop();
            logger.LogInformation("Transform: {Elapsed}ms", transformSw.ElapsedMilliseconds);

            // Resolve theme via 4-layer cascade
            var cascadeSw = Stopwatch.StartNew();
            var cascadeInput = new ThemeCascadeInput
            {
                PresetName = preset ?? "default"
            };

            if (themeFile is not null)
            {
                if (!themeFile.Exists)
                {
                    await Console.Error.WriteLineAsync($"Error: Theme file not found: {themeFile.FullName}");
                    return 2;
                }
                cascadeInput.Theme = ThemeParser.ParseFile(themeFile.FullName);

                var issues = ThemeValidator.Validate(cascadeInput.Theme);
                foreach (var issue in issues)
                {
                    var prefix = issue.Severity == ValidationSeverity.Error ? "Error" : "Warning";
                    var path = issue.PropertyPath is not null ? $" [{issue.PropertyPath}]" : "";
                    await Console.Error.WriteLineAsync($"{prefix}: Theme{path}: {issue.Message}");
                }
                if (issues.Any(i => i.Severity == ValidationSeverity.Error))
                {
                    return 2;
                }
            }

            if (templateFile is not null)
            {
                if (!templateFile.Exists)
                {
                    await Console.Error.WriteLineAsync($"Error: Template file not found: {templateFile.FullName}");
                    return 2;
                }
                var safety = TemplateSafetyChecker.Check(templateFile.FullName);
                if (!safety.IsValid)
                {
                    foreach (var error in safety.Errors)
                        await Console.Error.WriteLineAsync($"Error: {error}");
                    return 2;
                }
            }

            if (styles.Length > 0)
            {
                cascadeInput.CliOverrides = ThemeResolveCommand.ParseStyleOverrides(styles);

                var cliIssues = ThemeValidator.Validate(cascadeInput.CliOverrides);
                foreach (var issue in cliIssues)
                {
                    var prefix = issue.Severity == ValidationSeverity.Error ? "Error" : "Warning";
                    var path = issue.PropertyPath is not null ? $" [--style {issue.PropertyPath}]" : "";
                    await Console.Error.WriteLineAsync($"{prefix}: Style override{path}: {issue.Message}");
                }
                if (cliIssues.Any(i => i.Severity == ValidationSeverity.Error))
                {
                    return 2;
                }
            }

            (ResolvedTheme theme, IReadOnlyList<CascadeTraceEntry> cascadeTrace) result;
            try
            {
                result = ThemeCascadeResolver.ResolveWithTrace(cascadeInput);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("Unknown preset"))
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message.Split(new[] { " (Parameter" }, StringSplitOptions.None)[0]}");
                return 2;
            }
            var (theme, cascadeTrace) = result;
            cascadeSw.Stop();
            logger.LogInformation("Theme resolved (preset: {Preset}) in {Elapsed}ms", cascadeInput.PresetName, cascadeSw.ElapsedMilliseconds);

            // Show cascade trace in verbose mode
            if (verbose || debug)
            {
                await Console.Error.WriteLineAsync();
                await Console.Error.WriteLineAsync("Cascade resolution:");
                await Console.Error.WriteAsync(ThemeResolveFormatter.Format(theme, cascadeTrace));
                await Console.Error.WriteLineAsync();
            }

            // Emit
            var emitSw = Stopwatch.StartNew();
            var emitOptions = new EmitOptions
            {
                IncludeToc = toc,
                TocDepth = tocDepth,
                IncludeCoverPage = cover
            };
            var emitter = new DocxEmitter();

            using var fileStream = File.Create(outputPath);
            await pipeline.Emit(transformed, theme, emitter, emitOptions, fileStream);
            emitSw.Stop();
            logger.LogInformation("Emit: {Elapsed}ms", emitSw.ElapsedMilliseconds);

            totalSw.Stop();
            logger.LogInformation("Total: {Elapsed}ms — Written: {Path}", totalSw.ElapsedMilliseconds, outputPath);

            // Output path to stdout unless suppressed
            if (!quiet)
            {
                Console.WriteLine(outputPath);
            }
            return 0;
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Operation cancelled.");
            return 1;
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
