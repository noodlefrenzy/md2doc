// agent-notes: { ctx: "md2 doctor — diagnostic command for environment checks", deps: [System.CommandLine, BrowserManager, CodeTokenizer], state: active, last: "sato@2026-03-13" }

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.InteropServices;
using Md2.Diagrams;
using Md2.Highlight;

namespace Md2.Cli;

public static class DoctorCommand
{
    public static Command Create()
    {
        var command = new Command("doctor", "Check md2 environment and dependencies");

        command.SetHandler((InvocationContext context) =>
        {
            context.ExitCode = RunChecks(Console.Out, Console.Error);
        });

        return command;
    }

    public static int RunChecks(TextWriter output, TextWriter error)
    {
        var hasFailure = false;

        output.WriteLine("md2 doctor");
        output.WriteLine(new string('-', 40));

        // 1. .NET Runtime
        var dotnetVersion = RuntimeInformation.FrameworkDescription;
        WriteCheck(output, "pass", ".NET Runtime", dotnetVersion);

        // 2. OS / Architecture
        var os = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";
        WriteCheck(output, "pass", "OS", os);

        // 3. Playwright / Chromium
        if (BrowserManager.IsChromiumInstalled())
        {
            WriteCheck(output, "pass", "Chromium", "Installed");
        }
        else
        {
            WriteCheck(output, "fail", "Chromium", "Not installed");
            error.WriteLine("  Fix: Run 'playwright install chromium' or 'dotnet tool run playwright install chromium'");
            hasFailure = true;
        }

        // 4. TextMateSharp syntax highlighting
        try
        {
            using var tokenizer = new CodeTokenizer();
            var tokens = tokenizer.Tokenize("int x = 1;", "csharp");
            if (tokens.Count > 0)
            {
                WriteCheck(output, "pass", "Syntax highlighting", "TextMateSharp operational");
            }
            else
            {
                WriteCheck(output, "warn", "Syntax highlighting", "TextMateSharp returned no tokens");
            }
        }
        catch (Exception ex)
        {
            WriteCheck(output, "fail", "Syntax highlighting", $"TextMateSharp error: {ex.Message}");
            hasFailure = true;
        }

        // 5. Diagram cache directory
        var cacheDir = Path.Combine(Path.GetTempPath(), "md2-cache");
        try
        {
            Directory.CreateDirectory(cacheDir);
            WriteCheck(output, "pass", "Diagram cache", cacheDir);
        }
        catch (Exception ex)
        {
            WriteCheck(output, "fail", "Diagram cache", $"Cannot create: {ex.Message}");
            hasFailure = true;
        }

        output.WriteLine(new string('-', 40));
        if (hasFailure)
        {
            output.WriteLine("Some checks failed. Fix the issues above and run 'md2 doctor' again.");
            return 1;
        }
        else
        {
            output.WriteLine("All checks passed.");
            return 0;
        }
    }

    private static void WriteCheck(TextWriter output, string status, string name, string detail)
    {
        var symbol = status switch
        {
            "pass" => "[OK]  ",
            "fail" => "[FAIL]",
            "warn" => "[WARN]",
            _ => "[??]  "
        };
        output.WriteLine($"  {symbol} {name}: {detail}");
    }
}
