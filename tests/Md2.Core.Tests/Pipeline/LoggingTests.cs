// agent-notes: { ctx: "Issue #71 logging tests", deps: [Md2.Core.Pipeline.ConversionPipeline, Microsoft.Extensions.Logging], state: "green", last: "sato@2026-03-12" }

using Markdig.Syntax;
using Md2.Core.Ast;
using Md2.Core.Emit;
using Md2.Core.Pipeline;
using Md2.Core.Transforms;
using Md2.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Md2.Core.Tests.Pipeline;

public class LoggingTests
{
    // ── Constructor accepts ILogger ─────────────────────────────────────

    [Fact]
    public void Constructor_AcceptsILogger()
    {
        // The pipeline should accept an ILogger<ConversionPipeline> in its constructor.
        // This will fail until the constructor overload is added.
        var logger = NullLoggerFactory.Instance.CreateLogger<ConversionPipeline>();
        var pipeline = new ConversionPipeline(logger);

        pipeline.ShouldNotBeNull();
    }

    // ── Parse logging ───────────────────────────────────────────────────

    [Fact]
    public void Parse_LogsAtInformationLevel()
    {
        // Arrange
        var sink = new ListLoggerProvider();
        var factory = LoggerFactory.Create(b => b.AddProvider(sink).SetMinimumLevel(LogLevel.Debug));
        var logger = factory.CreateLogger<ConversionPipeline>();
        var pipeline = new ConversionPipeline(logger);

        // Act
        pipeline.Parse("# Hello", new ParserOptions());

        // Assert — at least one Information-level entry mentioning parse
        sink.Entries.ShouldContain(
            e => e.LogLevel == LogLevel.Information
                 && e.Message.Contains("Parse", StringComparison.OrdinalIgnoreCase));
    }

    // ── Transform logging ───────────────────────────────────────────────

    [Fact]
    public void Transform_LogsEachTransformNameAtInformationLevel()
    {
        // Arrange
        var sink = new ListLoggerProvider();
        var factory = LoggerFactory.Create(b => b.AddProvider(sink).SetMinimumLevel(LogLevel.Debug));
        var logger = factory.CreateLogger<ConversionPipeline>();
        var pipeline = new ConversionPipeline(logger);

        pipeline.RegisterTransform(new StubTransform("AlphaTransform", 10));
        pipeline.RegisterTransform(new StubTransform("BetaTransform", 20));

        var doc = new MarkdownDocument();
        var options = new TransformOptions();

        // Act
        pipeline.Transform(doc, options);

        // Assert — each transform name should appear in an Information log
        sink.Entries.ShouldContain(
            e => e.LogLevel == LogLevel.Information
                 && e.Message.Contains("AlphaTransform", StringComparison.OrdinalIgnoreCase));
        sink.Entries.ShouldContain(
            e => e.LogLevel == LogLevel.Information
                 && e.Message.Contains("BetaTransform", StringComparison.OrdinalIgnoreCase));
    }

    // ── Emit logging ────────────────────────────────────────────────────

    [Fact]
    public async Task Emit_LogsAtInformationLevel()
    {
        // Arrange
        var sink = new ListLoggerProvider();
        var factory = LoggerFactory.Create(b => b.AddProvider(sink).SetMinimumLevel(LogLevel.Debug));
        var logger = factory.CreateLogger<ConversionPipeline>();
        var pipeline = new ConversionPipeline(logger);

        var doc = new MarkdownDocument();
        var theme = new ResolvedTheme();
        var emitOptions = new EmitOptions();
        var emitter = new SpyEmitter();
        using var output = new MemoryStream();

        // Act
        await pipeline.Emit(doc, theme, emitter, emitOptions, output);

        // Assert
        sink.Entries.ShouldContain(
            e => e.LogLevel == LogLevel.Information
                 && e.Message.Contains("Emit", StringComparison.OrdinalIgnoreCase));
    }

    // ── Test infrastructure ─────────────────────────────────────────────

    /// <summary>
    /// A simple log entry record for assertions.
    /// </summary>
    public record LogEntry(LogLevel LogLevel, string Message);

    /// <summary>
    /// Minimal ILoggerProvider that captures log entries to a list.
    /// </summary>
    private sealed class ListLoggerProvider : ILoggerProvider
    {
        public List<LogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new ListLogger(this);

        public void Dispose() { }

        private sealed class ListLogger : ILogger
        {
            private readonly ListLoggerProvider _owner;

            public ListLogger(ListLoggerProvider owner) => _owner = owner;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _owner.Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
            }
        }
    }

    private class StubTransform : IAstTransform
    {
        public StubTransform(string name, int order)
        {
            Name = name;
            Order = order;
        }

        public string Name { get; }
        public int Order { get; }

        public MarkdownDocument Transform(MarkdownDocument doc, TransformContext context) => doc;
    }

    private class SpyEmitter : IFormatEmitter
    {
        public string FormatName => "test";
        public IReadOnlyList<string> FileExtensions => new[] { ".test" };

        public Task EmitAsync(MarkdownDocument doc, ResolvedTheme theme, EmitOptions options, Stream output)
            => Task.CompletedTask;
    }
}
