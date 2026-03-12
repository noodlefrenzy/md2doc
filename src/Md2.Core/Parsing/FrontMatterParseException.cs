// agent-notes: { ctx: "Exception for malformed YAML front matter", deps: [Md2Exception], state: active, last: "sato@2026-03-12" }

using Md2.Core.Exceptions;

namespace Md2.Parsing;

public class FrontMatterParseException : Md2Exception
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
