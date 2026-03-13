// agent-notes: { ctx: "Renders Markdig AST to themed HTML for preview", deps: [Markdig, Md2.Core.Pipeline.ResolvedTheme], state: active, last: "sato@2026-03-13" }

using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Md2.Core.Pipeline;

namespace Md2.Preview;

/// <summary>
/// Renders a Markdig AST to a self-contained HTML page with theme-derived CSS.
/// </summary>
public class HtmlPreviewRenderer
{
    /// <summary>
    /// Renders a MarkdownDocument to a complete HTML page string.
    /// </summary>
    public string Render(MarkdownDocument document, ResolvedTheme theme, MarkdownPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(pipeline);

        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        var htmlRenderer = new HtmlRenderer(writer);
        pipeline.Setup(htmlRenderer);
        htmlRenderer.Render(document);
        writer.Flush();

        var bodyHtml = sb.ToString();
        return WrapInPage(bodyHtml, theme);
    }

    /// <summary>
    /// Renders Markdown source text to a complete HTML page string.
    /// </summary>
    public string RenderFromSource(string markdown, ResolvedTheme theme, MarkdownPipeline pipeline)
    {
        var document = Markdown.Parse(markdown, pipeline);
        return Render(document, theme, pipeline);
    }

    private static string WrapInPage(string bodyHtml, ResolvedTheme theme)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>md2 Preview</title>
                <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/katex@0.16.21/dist/katex.min.css">
                <style>
                    {GenerateCss(theme)}
                </style>
            </head>
            <body>
                <div id="content">
                    {bodyHtml}
                </div>
                <script type="module" src="https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs"></script>
                <script defer src="https://cdn.jsdelivr.net/npm/katex@0.16.21/dist/katex.min.js"></script>
                <script>
                    {ReloadScript}
                </script>
            </body>
            </html>
            """;
    }

    private static string SanitizeFont(string value) =>
        Regex.Replace(value, @"[^a-zA-Z0-9 _\-]", "");

    private static string SanitizeHex(string value) =>
        Regex.Replace(value, @"[^a-fA-F0-9]", "");

    private static string GenerateCss(ResolvedTheme theme)
    {
        var bodyFont = SanitizeFont(theme.BodyFont);
        var headingFont = SanitizeFont(theme.HeadingFont);
        var monoFont = SanitizeFont(theme.MonoFont);
        var monoFontFallback = SanitizeFont(theme.MonoFontFallback);
        var primary = SanitizeHex(theme.PrimaryColor);
        var secondary = SanitizeHex(theme.SecondaryColor);
        var bodyText = SanitizeHex(theme.BodyTextColor);
        var codeBg = SanitizeHex(theme.CodeBackgroundColor);
        var codeBorder = SanitizeHex(theme.CodeBlockBorderColor);
        var link = SanitizeHex(theme.LinkColor);
        var tableHeaderBg = SanitizeHex(theme.TableHeaderBackground);
        var tableHeaderFg = SanitizeHex(theme.TableHeaderForeground);
        var tableBorder = SanitizeHex(theme.TableBorderColor);
        var tableAltRow = SanitizeHex(theme.TableAlternateRowBackground);
        var blockquoteBorder = SanitizeHex(theme.BlockquoteBorderColor);
        var blockquoteText = SanitizeHex(theme.BlockquoteTextColor);

        return $$"""
            :root {
                --primary: #{{primary}};
                --secondary: #{{secondary}};
                --body-text: #{{bodyText}};
                --code-bg: #{{codeBg}};
                --code-border: #{{codeBorder}};
                --link: #{{link}};
                --table-header-bg: #{{tableHeaderBg}};
                --table-header-fg: #{{tableHeaderFg}};
                --table-border: #{{tableBorder}};
                --table-alt-row: #{{tableAltRow}};
                --blockquote-border: #{{blockquoteBorder}};
                --blockquote-text: #{{blockquoteText}};
            }

            * { margin: 0; padding: 0; box-sizing: border-box; }

            body {
                font-family: '{{bodyFont}}', 'Georgia', serif;
                font-size: {{theme.BaseFontSize}}pt;
                line-height: {{theme.LineSpacing}};
                color: var(--body-text);
                max-width: 800px;
                margin: 0 auto;
                padding: 40px 20px;
                background: #fff;
            }

            h1, h2, h3, h4, h5, h6 {
                font-family: '{{headingFont}}', 'Calibri', sans-serif;
                color: var(--primary);
                margin-top: 1.5em;
                margin-bottom: 0.5em;
            }
            h1 { font-size: {{theme.Heading1Size}}pt; border-bottom: 2px solid var(--primary); padding-bottom: 0.3em; }
            h2 { font-size: {{theme.Heading2Size}}pt; border-bottom: 1px solid var(--secondary); padding-bottom: 0.2em; }
            h3 { font-size: {{theme.Heading3Size}}pt; }
            h4 { font-size: {{theme.Heading4Size}}pt; }
            h5 { font-size: {{theme.Heading5Size}}pt; }
            h6 { font-size: {{theme.Heading6Size}}pt; font-style: italic; }

            p { margin-bottom: 0.8em; }

            a { color: var(--link); text-decoration: none; }
            a:hover { text-decoration: underline; }

            code {
                font-family: '{{monoFont}}', '{{monoFontFallback}}', monospace;
                background: var(--code-bg);
                border: 1px solid var(--code-border);
                border-radius: 3px;
                padding: 0.1em 0.3em;
                font-size: 0.9em;
            }

            pre {
                background: var(--code-bg);
                border: 1px solid var(--code-border);
                border-radius: 4px;
                padding: 12px 16px;
                margin: 1em 0;
                overflow-x: auto;
            }
            pre code {
                border: none;
                padding: 0;
                background: none;
                font-size: 0.85em;
            }

            table {
                border-collapse: collapse;
                width: 100%;
                margin: 1em 0;
            }
            th {
                background: var(--table-header-bg);
                color: var(--table-header-fg);
                font-weight: bold;
                padding: 8px 12px;
                text-align: left;
                border: 1px solid var(--table-border);
            }
            td {
                padding: 8px 12px;
                border: 1px solid var(--table-border);
            }
            tr:nth-child(even) td {
                background: var(--table-alt-row);
            }

            blockquote {
                border-left: 4px solid var(--blockquote-border);
                color: var(--blockquote-text);
                padding: 0.5em 1em;
                margin: 1em 0;
            }
            blockquote blockquote {
                margin: 0.5em 0;
            }

            ul, ol { margin: 0.5em 0 0.5em 2em; }
            li { margin-bottom: 0.3em; }

            hr {
                border: none;
                border-top: 1px solid var(--table-border);
                margin: 2em 0;
            }

            img { max-width: 100%; height: auto; }

            .admonition {
                border-left: 4px solid var(--secondary);
                background: var(--code-bg);
                padding: 12px 16px;
                margin: 1em 0;
                border-radius: 0 4px 4px 0;
            }
            .admonition-title {
                font-weight: bold;
                color: var(--primary);
                margin-bottom: 0.5em;
            }

            dl { margin: 1em 0; }
            dt { font-weight: bold; margin-top: 0.5em; }
            dd { margin-left: 2em; margin-bottom: 0.5em; }

            sup { font-size: 0.75em; }
            del { text-decoration: line-through; color: #999; }
            """;
    }

    private const string ReloadScript = """
        function renderMath() {
            if (typeof katex === 'undefined') return;
            document.querySelectorAll('span.math').forEach(el => {
                const tex = el.textContent.replace(/^\\\(|\\\)$/g, '').trim();
                try { katex.render(tex, el, { throwOnError: false }); }
                catch (e) { /* leave original */ }
            });
            document.querySelectorAll('div.math').forEach(el => {
                const tex = el.textContent.replace(/^\\\[|\\\]$/g, '').trim();
                try { katex.render(tex, el, { displayMode: true, throwOnError: false }); }
                catch (e) { /* leave original */ }
            });
        }

        async function renderMermaid() {
            if (typeof mermaid === 'undefined') return;
            mermaid.initialize({ startOnLoad: false, theme: 'default' });
            const nodes = document.querySelectorAll('pre.mermaid');
            for (const node of nodes) {
                if (node.dataset.processed) continue;
                const id = 'mermaid-' + Math.random().toString(36).slice(2, 9);
                try {
                    const { svg } = await mermaid.render(id, node.textContent);
                    node.innerHTML = svg;
                    node.dataset.processed = 'true';
                } catch (e) {
                    /* leave original code block */
                }
            }
        }

        async function renderAll() {
            renderMath();
            await renderMermaid();
        }

        // Initial render once scripts are loaded
        window.addEventListener('load', () => setTimeout(renderAll, 100));

        (function() {
            let lastVersion = 0;
            async function poll() {
                try {
                    const resp = await fetch('/reload');
                    const data = await resp.json();
                    if (lastVersion > 0 && data.version > lastVersion) {
                        const contentResp = await fetch('/content');
                        const html = await contentResp.text();
                        const scrollY = window.scrollY;
                        document.getElementById('content').innerHTML = html;
                        window.scrollTo(0, scrollY);
                        await renderAll();
                    }
                    lastVersion = data.version;
                } catch (e) {
                    // Server may have stopped
                }
                setTimeout(poll, 200);
            }
            poll();
        })();
        """;
}
