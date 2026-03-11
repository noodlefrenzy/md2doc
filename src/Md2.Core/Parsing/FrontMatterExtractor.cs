// agent-notes: { ctx: "Extracts YAML front matter into DocumentMetadata", deps: [Markdig, YamlDotNet, DocumentMetadata], state: "green", last: "sato@2026-03-11" }
// NOTE: Lives in Md2.Core assembly but uses namespace Md2.Parsing to avoid circular dependency
// (Md2.Parsing cannot reference Md2.Core, but this class needs DocumentMetadata from Md2.Core.Ast).

using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Md2.Core.Ast;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Md2.Parsing;

public static class FrontMatterExtractor
{
    public static DocumentMetadata Extract(MarkdownDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var frontMatterBlock = doc.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (frontMatterBlock is null)
        {
            return new DocumentMetadata();
        }

        var yaml = ExtractYamlText(frontMatterBlock);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new DocumentMetadata();
        }

        Dictionary<string, object> parsed;
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            parsed = deserializer.Deserialize<Dictionary<string, object>>(yaml)
                     ?? [];
        }
        catch (YamlException ex)
        {
            throw new FrontMatterParseException(
                $"Failed to parse YAML front matter: {ex.Message}",
                ex.Start.Line > 0 ? (int)ex.Start.Line : 1,
                ex);
        }

        var metadata = new DocumentMetadata();
        var customFields = new Dictionary<string, string>();

        foreach (var kvp in parsed)
        {
            var value = kvp.Value?.ToString()?.Trim() ?? string.Empty;

            switch (kvp.Key.ToLowerInvariant())
            {
                case "title":
                    metadata.Title = value;
                    break;
                case "author":
                    metadata.Author = value;
                    break;
                case "date":
                    metadata.Date = value;
                    break;
                case "subject":
                    metadata.Subject = value;
                    break;
                case "keywords":
                    metadata.Keywords = value;
                    break;
                default:
                    customFields[kvp.Key] = value;
                    break;
            }
        }

        metadata.CustomFields = customFields.AsReadOnly();
        return metadata;
    }

    private static string ExtractYamlText(YamlFrontMatterBlock block)
    {
        var lines = new List<string>();
        for (var i = 0; i < block.Lines.Count; i++)
        {
            var line = block.Lines.Lines[i].Slice.ToString();
            // Skip the --- delimiters
            if (line.TrimStart().StartsWith("---"))
                continue;
            lines.Add(line);
        }
        return string.Join("\n", lines);
    }
}
