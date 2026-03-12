// agent-notes: { ctx: "LaTeX to OMML via KaTeX MathML + MML2OMML.xsl", deps: [BrowserManager, Microsoft.Playwright, System.Xml.Xsl], state: active, last: "sato@2026-03-12" }

using System.Reflection;
using System.Xml;
using System.Xml.Xsl;
using Md2.Core.Exceptions;
using Md2.Diagrams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace Md2.Math;

/// <summary>
/// Converts LaTeX math expressions to OMML (Office Math Markup Language)
/// using KaTeX for LaTeX→MathML and MML2OMML.xsl for MathML→OMML.
/// </summary>
public sealed class LatexToOmmlConverter
{
    private readonly BrowserManager _browserManager;
    private readonly ILogger<LatexToOmmlConverter> _logger;
    private static readonly Lazy<string> KatexJs = new(LoadKatexJs);
    private static readonly Lazy<XslCompiledTransform> Xslt = new(LoadXslt);

    private IPage? _katexPage;

    public LatexToOmmlConverter(BrowserManager browserManager, ILogger<LatexToOmmlConverter>? logger = null)
    {
        _browserManager = browserManager ?? throw new ArgumentNullException(nameof(browserManager));
        _logger = logger ?? NullLogger<LatexToOmmlConverter>.Instance;
    }

    /// <summary>
    /// Converts a single LaTeX expression to OMML XML string.
    /// </summary>
    public async Task<string> ConvertAsync(string latex, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(latex);

        var mathml = await LatexToMathMlAsync(latex, cancellationToken);
        return MathMlToOmml(mathml);
    }

    /// <summary>
    /// Converts multiple LaTeX expressions to OMML in a single batch.
    /// More efficient than calling ConvertAsync repeatedly (single page reuse).
    /// </summary>
    public async Task<IReadOnlyList<string>> ConvertBatchAsync(
        IReadOnlyList<string> expressions,
        CancellationToken cancellationToken = default)
    {
        if (expressions.Count == 0)
            return Array.Empty<string>();

        var results = new List<string>(expressions.Count);
        foreach (var latex in expressions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mathml = await LatexToMathMlAsync(latex, cancellationToken);
            results.Add(MathMlToOmml(mathml));
        }
        return results;
    }

    private async Task<IPage> GetKatexPageAsync(CancellationToken cancellationToken)
    {
        if (_katexPage is not null)
            return _katexPage;

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Creating KaTeX rendering page");
        var browser = await _browserManager.GetBrowserAsync(cancellationToken);
        var context = await browser.NewContextAsync();
        _katexPage = await context.NewPageAsync();
        _katexPage.SetDefaultTimeout(BrowserManager.PageTimeoutMs);

        var html = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body>"
            + "<script>" + KatexJs.Value + "</script>"
            + "</body></html>";

        await _katexPage.SetContentAsync(html, new PageSetContentOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = BrowserManager.PageTimeoutMs,
        });

        // Verify KaTeX loaded
        var check = await _katexPage.EvaluateAsync<bool>("() => typeof katex !== 'undefined'");
        if (!check)
            throw new Md2ConversionException(
                "KaTeX JS failed to load in Playwright page",
                "Math rendering is unavailable: KaTeX failed to initialize.");

        _logger.LogInformation("KaTeX rendering page ready");
        return _katexPage;
    }

    private async Task<string> LatexToMathMlAsync(string latex, CancellationToken cancellationToken)
    {
        var page = await GetKatexPageAsync(cancellationToken);

        // Use KaTeX's renderToString with output:"mathml" to get pure MathML
        var result = await page.EvaluateAsync<string>(@"(latex) => {
            try {
                return JSON.stringify({ ok: true, mathml: katex.renderToString(latex, { output: 'mathml', throwOnError: true }) });
            } catch (e) {
                return JSON.stringify({ ok: false, error: e.message });
            }
        }", latex);

        // Parse the JSON result
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(result);
        var root = jsonDoc.RootElement;

        if (!root.GetProperty("ok").GetBoolean())
        {
            var error = root.GetProperty("error").GetString() ?? "Unknown KaTeX error";
            throw new Md2ConversionException(
                $"KaTeX failed to render LaTeX: {error}",
                $"Math expression could not be rendered: {error}");
        }

        var mathml = root.GetProperty("mathml").GetString()
            ?? throw new Md2ConversionException("KaTeX returned null MathML", "Math rendering produced no output.");

        _logger.LogDebug("LaTeX -> MathML ({InputLen} chars -> {OutputLen} chars)", latex.Length, mathml.Length);
        return mathml;
    }

    private string MathMlToOmml(string mathml)
    {
        // KaTeX output is an HTML span containing <math> element(s)
        // We need to extract the <math> element and wrap it for XSLT
        var mathContent = ExtractMathElement(mathml);

        // Wrap in a root element with MathML namespace for XSLT processing
        var xmlInput = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + mathContent;

        using var inputReader = new StringReader(xmlInput);
        using var xmlReader = XmlReader.Create(inputReader, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        using var outputWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(outputWriter, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false
        });

        try
        {
            Xslt.Value.Transform(xmlReader, xmlWriter);
        }
        catch (Exception ex) when (ex is not Md2ConversionException)
        {
            throw new Md2ConversionException(
                $"MathML to OMML XSLT transformation failed: {ex.Message}",
                "Failed to convert math expression to Word format.",
                ex);
        }

        xmlWriter.Flush();
        return outputWriter.ToString();
    }

    private static string ExtractMathElement(string katexOutput)
    {
        // KaTeX with output:"mathml" wraps the <math> element in a <span>
        // Extract just the <math>...</math> element
        var mathStart = katexOutput.IndexOf("<math", StringComparison.Ordinal);
        if (mathStart < 0)
            throw new Md2ConversionException(
                "No <math> element found in KaTeX output",
                "Math rendering produced invalid output.");

        var mathEnd = katexOutput.LastIndexOf("</math>", StringComparison.Ordinal);
        if (mathEnd < 0)
            throw new Md2ConversionException(
                "No closing </math> tag found in KaTeX output",
                "Math rendering produced invalid output.");

        return katexOutput[mathStart..(mathEnd + "</math>".Length)];
    }

    private static string LoadKatexJs()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Md2.Math.katex.min.js")
            ?? throw new InvalidOperationException("KaTeX JS resource not found in assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static XslCompiledTransform LoadXslt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Md2.Math.MML2OMML.xsl")
            ?? throw new InvalidOperationException("MML2OMML.xsl resource not found in assembly.");
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        var xslt = new XslCompiledTransform();
        xslt.Load(reader, XsltSettings.Default, null);
        return xslt;
    }
}
