// agent-notes: { ctx: "Exception for malformed YAML front matter", deps: [], state: "green", last: "sato@2026-03-11" }

namespace Md2.Parsing;

public class FrontMatterParseException : Exception
{
    public FrontMatterParseException(string message, int lineNumber)
        : base(message)
    {
        LineNumber = lineNumber;
    }

    public FrontMatterParseException(string message, int lineNumber, Exception innerException)
        : base(message, innerException)
    {
        LineNumber = lineNumber;
    }

    public int LineNumber { get; }
}
