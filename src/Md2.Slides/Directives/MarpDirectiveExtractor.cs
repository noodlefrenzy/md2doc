// agent-notes: { ctx: "Extract MARP directives from HtmlBlock comment nodes", deps: [Markdig, MarpDirective], state: active, last: "sato@2026-03-15" }

using System.Text.RegularExpressions;
using Markdig.Syntax;

namespace Md2.Slides.Directives;

/// <summary>
/// Extracts MARP directives from HTML comment blocks in the Markdig AST.
/// Handles both single-line (<!-- key: value -->) and multi-line comments.
/// Does NOT handle front-matter — that is extracted separately from raw markdown.
/// </summary>
public static partial class MarpDirectiveExtractor
{
    // Matches key: value lines inside HTML comments (one per line)
    [GeneratedRegex(@"(_?\w+)\s*:\s*([^\n\r]*)", RegexOptions.None)]
    private static partial Regex DirectiveLineRegex();

    // Matches the overall comment structure
    [GeneratedRegex(@"^<!--(.+?)-->$", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    // Speaker notes: <!-- content that is NOT a directive -->
    // A directive has the form "key: value" — speaker notes don't match that pattern.

    /// <summary>
    /// Extract all directives from HtmlBlock nodes in the given document.
    /// Returns directives in document order.
    /// </summary>
    public static IReadOnlyList<MarpDirective> Extract(MarkdownDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var directives = new List<MarpDirective>();

        foreach (var block in doc.Descendants<HtmlBlock>())
        {
            var html = block.Lines.ToString().Trim();
            if (!html.StartsWith("<!--") || !html.EndsWith("-->"))
                continue;

            // Extract comment body
            var commentMatch = CommentRegex().Match(html);
            if (!commentMatch.Success)
                continue;

            var body = commentMatch.Groups[1].Value;

            // Find all key: value pairs within the comment
            var matches = DirectiveLineRegex().Matches(body);
            foreach (Match match in matches)
            {
                var key = match.Groups[1].Value;
                var value = match.Groups[2].Value.Trim();
                directives.Add(new MarpDirective(key, value, MarpDirectiveScope.Local));
            }
        }

        return directives;
    }

    /// <summary>
    /// Extract directives from YAML front-matter key-value pairs.
    /// All front-matter directives are Global scope.
    /// </summary>
    public static IReadOnlyList<MarpDirective> ExtractFromFrontMatter(IDictionary<string, string> frontMatter)
    {
        ArgumentNullException.ThrowIfNull(frontMatter);

        var directives = new List<MarpDirective>();

        foreach (var (key, value) in frontMatter)
        {
            directives.Add(new MarpDirective(key, value, MarpDirectiveScope.Global) { SlideIndex = -1 });
        }

        return directives;
    }

    /// <summary>
    /// Checks whether an HTML comment block is a speaker note (not a directive).
    /// Speaker notes are HTML comments that don't match the directive pattern.
    /// </summary>
    public static bool IsSpeakerNote(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;

        html = html.Trim();
        if (!html.StartsWith("<!--") || !html.EndsWith("-->"))
            return false;

        // Extract comment body and check for directive pattern
        var commentMatch = CommentRegex().Match(html);
        if (commentMatch.Success && DirectiveLineRegex().IsMatch(commentMatch.Groups[1].Value))
            return false;

        // Empty comments (<!-- -->) are not speaker notes
        var content = html[4..^3].Trim();
        if (string.IsNullOrEmpty(content))
            return false;

        // It's an HTML comment with content but not a directive — treat as speaker note
        return true;
    }

    /// <summary>
    /// Extract speaker note content from an HTML comment.
    /// </summary>
    public static string? ExtractSpeakerNote(string html)
    {
        if (!IsSpeakerNote(html))
            return null;

        html = html.Trim();
        var content = html[4..^3].Trim(); // strip <!-- and -->
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }
}
