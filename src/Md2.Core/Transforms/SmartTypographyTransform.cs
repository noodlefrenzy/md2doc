// agent-notes: { ctx: "AST transform for smart typography: curly quotes, dashes, ellipsis", deps: [IAstTransform, Markdig], state: active, last: "sato@2026-03-11" }

using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Md2.Core.Transforms;

public class SmartTypographyTransform : IAstTransform
{
    public string Name => "SmartTypography";
    public int Order => 20;

    public MarkdownDocument Transform(MarkdownDocument doc, TransformContext context)
    {
        foreach (var block in doc.Descendants<ParagraphBlock>())
        {
            if (block.Inline == null)
                continue;

            TransformInlineContainer(block.Inline);
        }

        return doc;
    }

    private static void TransformInlineContainer(ContainerInline container)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    TransformLiteral(literal);
                    break;
                case CodeInline:
                    // Skip code spans — do not transform
                    break;
                case ContainerInline nested:
                    TransformInlineContainer(nested);
                    break;
            }
        }
    }

    private static void TransformLiteral(LiteralInline literal)
    {
        var text = literal.Content.ToString();
        if (string.IsNullOrEmpty(text))
            return;

        var transformed = ApplySmartTypography(text);

        if (transformed != text)
        {
            literal.Content = new Markdig.Helpers.StringSlice(transformed);
        }
    }

    internal static string ApplySmartTypography(string text)
    {
        // Order matters: process longer patterns first

        // --- → em-dash (must come before --)
        text = text.Replace("---", "\u2014");

        // -- → en-dash
        text = text.Replace("--", "\u2013");

        // ... → ellipsis
        text = text.Replace("...", "\u2026");

        // Double quotes → curly double quotes
        text = ReplaceDoubleQuotes(text);

        // Single quotes / apostrophes → curly single quotes
        text = ReplaceSingleQuotes(text);

        return text;
    }

    private static string ReplaceDoubleQuotes(string text)
    {
        // Simple state machine: alternate between opening and closing
        var chars = text.ToCharArray();
        bool expectingOpen = true;

        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] == '"')
            {
                chars[i] = expectingOpen ? '\u201C' : '\u201D';
                expectingOpen = !expectingOpen;
            }
        }

        return new string(chars);
    }

    private static string ReplaceSingleQuotes(string text)
    {
        var chars = text.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] != '\'')
                continue;

            // Apostrophe: letter before and letter after (e.g., it's, don't)
            bool letterBefore = i > 0 && char.IsLetter(chars[i - 1]);
            bool letterAfter = i < chars.Length - 1 && char.IsLetter(chars[i + 1]);

            if (letterBefore && letterAfter)
            {
                // Apostrophe → right single quote
                chars[i] = '\u2019';
            }
            else if (letterBefore)
            {
                // Closing single quote
                chars[i] = '\u2019';
            }
            else
            {
                // Opening single quote
                chars[i] = '\u2018';
            }
        }

        return new string(chars);
    }
}
