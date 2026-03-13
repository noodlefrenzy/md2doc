// agent-notes: { ctx: "Performance benchmarks for Mermaid and math rendering", deps: [MermaidRenderer, LatexToOmmlConverter, BrowserManager, DiagramCache], state: active, last: "tara@2026-03-13" }

using System.Diagnostics;
using Md2.Diagrams;
using Md2.Math;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Md2.Integration.Tests;

[Trait("Category", "Integration")]
[Trait("Category", "Benchmark")]
public class RenderingBenchmarkTests : IAsyncLifetime
{
    private BrowserManager _browserManager = null!;

    public async Task InitializeAsync()
    {
        _browserManager = new BrowserManager(NullLogger<BrowserManager>.Instance);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _browserManager.DisposeAsync();
    }

    /// <summary>
    /// AC-4.8.7: 10 Mermaid diagrams should render in under 15 seconds total.
    /// </summary>
    [Fact]
    public async Task Mermaid_10Diagrams_Under15Seconds()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"md2-bench-mermaid-{Guid.NewGuid():N}");
        var cache = new DiagramCache(cacheDir, MermaidRenderer.MermaidVersion);
        var renderer = new MermaidRenderer(_browserManager, cache);

        var diagrams = new[]
        {
            "graph TD\n    A[Start] --> B{Decision}\n    B -->|Yes| C[OK]\n    B -->|No| D[Retry]",
            "sequenceDiagram\n    Alice->>Bob: Hello\n    Bob-->>Alice: Hi",
            "classDiagram\n    Animal <|-- Duck\n    Animal <|-- Fish\n    Animal : +int age\n    Animal : +String gender",
            "flowchart LR\n    A --> B --> C --> D --> E",
            "pie title Pets\n    \"Dogs\" : 386\n    \"Cats\" : 85\n    \"Birds\" : 15",
            "graph TB\n    A --> B\n    A --> C\n    B --> D\n    C --> D\n    D --> E",
            "sequenceDiagram\n    participant A\n    participant B\n    participant C\n    A->>B: req1\n    B->>C: req2\n    C-->>B: resp2\n    B-->>A: resp1",
            "flowchart TD\n    S[Start] --> A\n    A --> B\n    A --> C\n    B --> D\n    C --> D\n    D --> E[End]",
            "graph LR\n    A((Source)) --> B[Process] --> C((Sink))",
            "flowchart TB\n    subgraph one\n    a1-->a2\n    end\n    subgraph two\n    b1-->b2\n    end\n    one --> two"
        };

        var sw = Stopwatch.StartNew();

        foreach (var diagram in diagrams)
        {
            var path = await renderer.RenderAsync(diagram);
            File.Exists(path).ShouldBeTrue();
        }

        sw.Stop();

        try
        {
            Directory.Delete(cacheDir, true);
        }
        catch { }

        sw.Elapsed.TotalSeconds.ShouldBeLessThan(15.0,
            $"10 Mermaid diagrams took {sw.Elapsed.TotalSeconds:F1}s (limit: 15s)");

        // Output timing for reference
        Console.Error.WriteLine($"[Benchmark] 10 Mermaid diagrams: {sw.Elapsed.TotalSeconds:F1}s ({sw.Elapsed.TotalSeconds / 10:F2}s avg)");
    }

    /// <summary>
    /// AC-4.7.5: 25 math expressions should render in under 10 seconds total.
    /// </summary>
    [Fact]
    public async Task Math_25Expressions_Under10Seconds()
    {
        var converter = new LatexToOmmlConverter(_browserManager);

        var expressions = new[]
        {
            @"E = mc^2",
            @"\frac{-b \pm \sqrt{b^2 - 4ac}}{2a}",
            @"\int_0^\infty e^{-x^2} dx = \frac{\sqrt{\pi}}{2}",
            @"\sum_{n=1}^{\infty} \frac{1}{n^2} = \frac{\pi^2}{6}",
            @"\nabla \times \mathbf{E} = -\frac{\partial \mathbf{B}}{\partial t}",
            @"\begin{pmatrix} a & b \\ c & d \end{pmatrix}",
            @"\lim_{x \to 0} \frac{\sin x}{x} = 1",
            @"f'(x) = \lim_{h \to 0} \frac{f(x+h) - f(x)}{h}",
            @"\oint_C \mathbf{F} \cdot d\mathbf{r}",
            @"\binom{n}{k} = \frac{n!}{k!(n-k)!}",
            @"\prod_{i=1}^{n} x_i",
            @"\sqrt[n]{a}",
            @"\log_b(xy) = \log_b x + \log_b y",
            @"\sin^2\theta + \cos^2\theta = 1",
            @"e^{i\pi} + 1 = 0",
            @"\frac{d}{dx}\left(\frac{f}{g}\right) = \frac{f'g - fg'}{g^2}",
            @"\mathbb{P}(A|B) = \frac{\mathbb{P}(B|A)\mathbb{P}(A)}{\mathbb{P}(B)}",
            @"\sigma = \sqrt{\frac{1}{N}\sum_{i=1}^{N}(x_i - \mu)^2}",
            @"\hat{H}\psi = E\psi",
            @"\vec{F} = m\vec{a}",
            @"\iint_S \mathbf{F} \cdot d\mathbf{S}",
            @"\mathcal{L}\{f(t)\} = \int_0^\infty e^{-st} f(t) dt",
            @"\det(A) = \sum_{\sigma} \text{sgn}(\sigma) \prod_i a_{i,\sigma(i)}",
            @"\forall \epsilon > 0, \exists \delta > 0",
            @"\alpha + \beta = \gamma"
        };

        var sw = Stopwatch.StartNew();

        var results = await converter.ConvertBatchAsync(expressions);

        sw.Stop();

        results.Count.ShouldBe(25);
        foreach (var omml in results)
        {
            omml.ShouldContain("m:oMath", Case.Insensitive);
        }

        sw.Elapsed.TotalSeconds.ShouldBeLessThan(10.0,
            $"25 math expressions took {sw.Elapsed.TotalSeconds:F1}s (limit: 10s)");

        Console.Error.WriteLine($"[Benchmark] 25 math expressions: {sw.Elapsed.TotalSeconds:F1}s ({sw.Elapsed.TotalSeconds / 25 * 1000:F0}ms avg)");
    }
}
