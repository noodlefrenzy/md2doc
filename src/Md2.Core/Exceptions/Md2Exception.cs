// agent-notes: { ctx: "Base exception for all md2 errors", deps: [], state: active, last: "sato@2026-03-12" }

namespace Md2.Core.Exceptions;

/// <summary>
/// Base exception for all md2 errors. UserMessage is the human-friendly
/// text shown to CLI users; Message is the developer-oriented detail.
/// </summary>
public class Md2Exception : Exception
{
    public Md2Exception(string message)
        : base(message)
    {
        UserMessage = message;
    }

    public Md2Exception(string message, string userMessage)
        : base(message)
    {
        UserMessage = userMessage;
    }

    public Md2Exception(string message, Exception innerException)
        : base(message, innerException)
    {
        UserMessage = message;
    }

    public Md2Exception(string message, string userMessage, Exception innerException)
        : base(message, innerException)
    {
        UserMessage = userMessage;
    }

    public string UserMessage { get; }
}
