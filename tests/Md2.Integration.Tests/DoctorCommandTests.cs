// agent-notes: { ctx: "Tests for md2 doctor diagnostic command", deps: [Md2.Cli.DoctorCommand], state: active, last: "tara@2026-03-13" }

using Md2.Cli;
using Shouldly;

namespace Md2.Integration.Tests;

public class DoctorCommandTests
{
    [Fact]
    public void RunChecks_WritesHeaderToOutput()
    {
        var (output, _) = RunDoctor();

        output.ShouldContain("md2 doctor");
    }

    [Fact]
    public void RunChecks_DotNetRuntime_AlwaysPasses()
    {
        var (output, _) = RunDoctor();

        output.ShouldContain("[OK]");
        output.ShouldContain(".NET Runtime:");
    }

    [Fact]
    public void RunChecks_Os_ReportsOsInfo()
    {
        var (output, _) = RunDoctor();

        output.ShouldContain("OS:");
    }

    [Fact]
    public void RunChecks_ChromiumStatus_ReportsPassOrFail()
    {
        var (output, _) = RunDoctor();

        // Should report status for Chromium
        output.ShouldContain("Chromium:");
        (output.Contains("Installed") || output.Contains("Not installed"))
            .ShouldBeTrue("Chromium check should report Installed or Not installed");
    }

    [Fact]
    public void RunChecks_SyntaxHighlighting_IsOperational()
    {
        var (output, _) = RunDoctor();

        output.ShouldContain("Syntax highlighting:");
        output.ShouldContain("TextMateSharp operational");
    }

    [Fact]
    public void RunChecks_DiagramCache_IsAccessible()
    {
        var (output, _) = RunDoctor();

        output.ShouldContain("Diagram cache:");
        output.ShouldContain("md2-cache");
    }

    [Fact]
    public void RunChecks_ExitCodeMatchesCheckResults()
    {
        var (output, _, exitCode) = RunDoctorWithExitCode();

        if (output.Contains("[FAIL]"))
        {
            exitCode.ShouldBe(1, "Exit code should be 1 when any check fails");
            output.ShouldContain("Some checks failed");
        }
        else
        {
            exitCode.ShouldBe(0, "Exit code should be 0 when all checks pass");
            output.ShouldContain("All checks passed");
        }
    }

    [Fact]
    public void RunChecks_WritesSeparatorLines()
    {
        var (output, _) = RunDoctor();

        output.ShouldContain("----------------------------------------");
    }

    private static (string Output, string Error) RunDoctor()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        DoctorCommand.RunChecks(output, error);
        return (output.ToString(), error.ToString());
    }

    private static (string Output, string Error, int ExitCode) RunDoctorWithExitCode()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = DoctorCommand.RunChecks(output, error);
        return (output.ToString(), error.ToString(), exitCode);
    }
}
