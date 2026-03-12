// agent-notes: { ctx: "Manages heading bookmarks for cross-reference linking", deps: [], state: active, last: "sato@2026-03-12" }

using System.Text.RegularExpressions;

namespace Md2.Emit.Docx;

/// <summary>
/// Manages bookmark IDs for headings. Generates URL-style slugs from heading text,
/// disambiguates duplicates with numeric suffixes, and tracks bookmarks for
/// internal link resolution.
/// </summary>
public sealed partial class BookmarkManager
{
    private readonly Dictionary<string, int> _slugCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _bookmarkIds = new(StringComparer.OrdinalIgnoreCase);
    private int _nextBookmarkId = 1;

    /// <summary>
    /// Registers a heading and returns its unique bookmark ID and slug.
    /// Duplicate heading text gets a numeric suffix (-1, -2, etc.).
    /// </summary>
    public (string slug, int bookmarkId) RegisterHeading(string headingText)
    {
        var baseSlug = Slugify(headingText);
        string slug;

        if (_slugCounts.TryGetValue(baseSlug, out var count))
        {
            slug = $"{baseSlug}-{count}";
            _slugCounts[baseSlug] = count + 1;
        }
        else
        {
            slug = baseSlug;
            _slugCounts[baseSlug] = 1;
        }

        var bookmarkId = _nextBookmarkId++;
        _bookmarkIds[slug] = bookmarkId;
        return (slug, bookmarkId);
    }

    /// <summary>
    /// Resolves an anchor reference (e.g., "#my-heading") to a bookmark ID.
    /// Returns null if the anchor doesn't match any registered heading.
    /// </summary>
    public int? ResolveAnchor(string anchor)
    {
        var slug = anchor.TrimStart('#');
        return _bookmarkIds.TryGetValue(slug, out var id) ? id : null;
    }

    /// <summary>
    /// Converts heading text to a URL-style slug.
    /// Lowercase, spaces to hyphens, remove non-alphanumeric/hyphen chars.
    /// </summary>
    public static string Slugify(string text)
    {
        var slug = text.Trim().ToLowerInvariant();
        slug = WhitespaceRegex().Replace(slug, "-");
        slug = NonSlugRegex().Replace(slug, "");
        slug = MultiHyphenRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        return string.IsNullOrEmpty(slug) ? "heading" : slug;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex NonSlugRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiHyphenRegex();
}
