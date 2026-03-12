// agent-notes: { ctx: "Exception for pipeline/emit conversion errors", deps: [Md2Exception], state: active, last: "sato@2026-03-12" }

namespace Md2.Core.Exceptions;

/// <summary>
/// Exception for errors during pipeline conversion (parse, transform, emit).
/// </summary>
public class Md2ConversionException : Md2Exception
{
    public Md2ConversionException(string message)
        : base(message) { }

    public Md2ConversionException(string message, Exception innerException)
        : base(message, innerException) { }

    public Md2ConversionException(string message, string userMessage)
        : base(message, userMessage) { }

    public Md2ConversionException(string message, string userMessage, Exception innerException)
        : base(message, userMessage, innerException) { }
}
