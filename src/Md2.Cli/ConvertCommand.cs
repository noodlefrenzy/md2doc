// agent-notes: { ctx: "Root CLI command: markdown to docx with cancellation support", deps: [System.CommandLine, ConversionPipeline, DocxEmitter, ThemeCascadeResolver, ILogger], state: active, last: "sato@2026-03-13" }

using System.CommandLine;
using System.Diagnostics;
using Md2.Core.Exceptions;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Emit.Docx;
using Md2.Emit.Pptx;
using Md2.Diagrams;
using Md2.Highlight;
using Md2.Math;
using Md2.Parsing;
using Md2.Slides;
using Md2.Themes;
using Microsoft.Extensions.Logging;

namespace Md2.Cli;

public static class ConvertCommand
{
    public static RootCommand Create()
    {
        var inputArgument = new Argument<FileInfo>("input")
        {
            Description = "Path to the Markdown input file",
            Arity = ArgumentArity.ExactlyOne
        };

        var outputOption = new Option<FileInfo?>("-o", "--output")
        {
            Description = "Path to the output file (.docx or .pptx). Derived from input name if omitted."
        };

        var quietOption = new Option<bool>("-q", "--quiet")
        {
            Description = "Suppress warnings"
        };

        var verboseOption = new Option<bool>("-v", "--verbose")
        {
            Description = "Enable verbose output"
        };

        var debugOption = new Option<bool>("--debug")
        {
            Description = "Enable debug-level logging with full diagnostics"
        };

        var presetOption = new Option<string?>("--preset")
        {
            Description = "Theme preset name (default: 'default')"
        };

        var themeOption = new Option<FileInfo?>("--theme")
        {
            Description = "Path to a theme YAML file"
        };

        var templateOption = new Option<FileInfo?>("--template")
        {
            Description = "Path to a DOCX template file"
        };

        var styleOption = new Option<string[]>("--style")
        {
            Description = "Style overrides as key=value pairs (e.g. --style colors.primary=FF0000)",
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.ZeroOrMore,
        };

        var tocOption = new Option<bool>("--toc")
        {
            Description = "Include a Table of Contents"
        };

        var tocDepthOption = new Option<int>("--toc-depth")
        {
            Description = "TOC heading depth (1-6, default 3)",
            DefaultValueFactory = _ => 3,
        };

        var coverOption = new Option<bool>("--cover")
        {
            Description = "Include a cover page from front matter metadata"
        };

        var rootCommand = new RootCommand("Convert Markdown to polished DOCX or PPTX files")
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

        rootCommand.SetAction(async (parseResult, ct) =>
        {
            var input = parseResult.GetValue(inputArgument);
            var output = parseResult.GetValue(outputOption);
            var quiet = parseResult.GetValue(quietOption);
            var verbose = parseResult.GetValue(verboseOption);
            var debug = parseResult.GetValue(debugOption);
            var preset = parseResult.GetValue(presetOption);
            var themeFile = parseResult.GetValue(themeOption);
            var templateFile = parseResult.GetValue(templateOption);
            var styles = parseResult.GetValue(styleOption) ?? [];
            var toc = parseResult.GetValue(tocOption);
            var tocDepth = parseResult.GetValue(tocDepthOption);
            var cover = parseResult.GetValue(coverOption);

            return await ExecuteAsync(input!, output, quiet, verbose, debug, preset, themeFile, templateFile, styles, toc, tocDepth, cover, ct);
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
        CancellationToken cancellationToken)
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

        // Detect PPTX output and route to slide pipeline
        var isPptx = Path.GetExtension(outputPath).Equals(".pptx", StringComparison.OrdinalIgnoreCase);

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

            // Resolve theme via 4-layer cascade (before transforms so theme is available to Mermaid rendering)
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

            if (isPptx)
            {
                // PPTX path: MarpParser → SlideDocument → PptxEmitter
                var parseSw2 = Stopwatch.StartNew();
                var marpParser = new MarpParser();
                var slideDoc = marpParser.Parse(markdown);
                parseSw2.Stop();
                logger.LogInformation("MARP parse: {Elapsed}ms ({SlideCount} slides)", parseSw2.ElapsedMilliseconds, slideDoc.Slides.Count);

                var emitSw = Stopwatch.StartNew();
                var emitOptions = new EmitOptions
                {
                    InputBaseDirectory = Path.GetDirectoryName(Path.GetFullPath(input.FullName))
                };
                var pptxEmitter = new PptxEmitter();

                using var fileStream = File.Create(outputPath);
                await pptxEmitter.EmitAsync(slideDoc, theme, emitOptions, fileStream);
                emitSw.Stop();
                logger.LogInformation("Emit: {Elapsed}ms", emitSw.ElapsedMilliseconds);
            }
            else
            {
                // DOCX path: ConversionPipeline → DocxEmitter
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

                // Transform (with resolved theme available to Mermaid renderer)
                var transformSw = Stopwatch.StartNew();
                pipeline.RegisterTransform(new YamlFrontMatterExtractor());
                pipeline.RegisterTransform(new SmartTypographyTransform());
                pipeline.RegisterTransform(new MathBlockAnnotator(latexConverter));
                pipeline.RegisterTransform(new MermaidDiagramRenderer(mermaidRenderer));
                pipeline.RegisterTransform(new SyntaxHighlightAnnotator());
                var transformOptions = new TransformOptions { RenderMermaid = true };
                var transformResult = pipeline.Transform(doc, transformOptions, cancellationToken, resolvedTheme: theme);
                var transformed = transformResult.Document;
                transformSw.Stop();
                logger.LogInformation("Transform: {Elapsed}ms", transformSw.ElapsedMilliseconds);

                // Surface any warnings from transforms (e.g. failed Mermaid/Math rendering)
                if (!quiet)
                {
                    foreach (var warning in transformResult.Warnings)
                    {
                        await Console.Error.WriteLineAsync($"Warning: {warning}");
                    }
                }

                // Emit
                var emitSw = Stopwatch.StartNew();
                var emitOptions = new EmitOptions
                {
                    IncludeToc = toc,
                    TocDepth = tocDepth,
                    IncludeCoverPage = cover,
                    InputBaseDirectory = Path.GetDirectoryName(Path.GetFullPath(input.FullName))
                };
                var emitter = new DocxEmitter();

                using var fileStream = File.Create(outputPath);
                await pipeline.Emit(transformed, theme, emitter, emitOptions, fileStream);
                emitSw.Stop();
                logger.LogInformation("Emit: {Elapsed}ms", emitSw.ElapsedMilliseconds);
            }

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
