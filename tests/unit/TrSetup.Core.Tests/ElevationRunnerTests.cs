using TrSetup.Core.Elevation;
using TrSetup.Core.Fixing;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-020 / REQ-NFR-002 — the elevation runner: the Windows UAC launcher command is
/// constructed exactly (Start-Process -Verb RunAs in a visible child, exact command
/// surfaced), elevation never runs without a granted consent token, the WSL/Linux sudo path
/// is a pure terminal handoff that executes nothing and handles no password, and no
/// credential ever passes through the elevation surface.
/// </summary>
public sealed class ElevationRunnerTests
{
    /// <summary>
    /// Scenario: building the Windows elevation request for an msiexec install.
    /// Expect: launcher is powershell.exe, the arguments contain Start-Process with
    /// -Verb RunAs and -Wait, and the exact target command (file and arguments) verbatim.
    /// </summary>
    [Fact]
    public void WindowsElevationRequestWrapsExactCommandInUac()
    {
        var vCommand = new ElevatedCommand("msiexec", "/i node.msi /qn", "Install Node.js LTS");

        var vRequest = ElevationRunner.BuildWindowsElevationRequest(vCommand);

        Assert.Equal("powershell.exe", vRequest.FileName);
        Assert.Contains("Start-Process", vRequest.Arguments);
        Assert.Contains("-Verb RunAs", vRequest.Arguments);
        Assert.Contains("-Wait", vRequest.Arguments);
        Assert.Contains("-FilePath 'msiexec'", vRequest.Arguments);
        Assert.Contains("-ArgumentList '/i node.msi /qn'", vRequest.Arguments);
    }

    /// <summary>
    /// Scenario: the elevated command's arguments contain single quotes.
    /// Expect: quotes are doubled for PowerShell so the exact command survives verbatim.
    /// </summary>
    [Fact]
    public void WindowsElevationEscapesSingleQuotes()
    {
        var vCommand = new ElevatedCommand("cmd.exe", "/c echo 'hi'", "Echo test");

        var vRequest = ElevationRunner.BuildWindowsElevationRequest(vCommand);

        Assert.Contains("-ArgumentList '/c echo ''hi'''", vRequest.Arguments);
    }

    /// <summary>
    /// Scenario: an elevated command without arguments.
    /// Expect: no -ArgumentList part is emitted (Start-Process rejects an empty argument list).
    /// </summary>
    [Fact]
    public void WindowsElevationOmitsEmptyArgumentList()
    {
        var vCommand = new ElevatedCommand("wsl.exe", string.Empty, "Launch WSL");

        var vRequest = ElevationRunner.BuildWindowsElevationRequest(vCommand);

        Assert.DoesNotContain("-ArgumentList", vRequest.Arguments);
        Assert.Contains("-FilePath 'wsl.exe'", vRequest.Arguments);
    }

    /// <summary>
    /// Scenario: RunWindowsElevatedAsync is called with a granted consent token.
    /// Expect: exactly one process request goes through the choke-point and it equals the
    /// built UAC launcher request for that command.
    /// </summary>
    [Fact]
    public async Task GrantedConsentLaunchesUacChildThroughProcessRunner()
    {
        var vProcessRunner = new ElevationFakeProcessRunner();
        var vRunner = new ElevationRunner(vProcessRunner);
        var vCommand = new ElevatedCommand("msiexec", "/i node.msi /qn", "Install Node.js LTS");
        var vConsent = ConsentToken.Granted(vCommand.CommandLine);

        var vResult = await vRunner.RunWindowsElevatedAsync(vCommand, vConsent);

        var vRequest = Assert.Single(vProcessRunner.Requests);
        Assert.Equal(ElevationRunner.BuildWindowsElevationRequest(vCommand).Arguments, vRequest.Arguments);
        Assert.True(vResult.Succeeded);
    }

    /// <summary>
    /// Scenario: RunWindowsElevatedAsync is handed a declined consent token.
    /// Expect: InvalidOperationException and the process choke-point is never touched —
    /// there is no code path to elevation without explicit user consent.
    /// </summary>
    [Fact]
    public async Task DeclinedConsentNeverLaunchesAnything()
    {
        var vProcessRunner = new ElevationFakeProcessRunner();
        var vRunner = new ElevationRunner(vProcessRunner);
        var vCommand = new ElevatedCommand("msiexec", "/i node.msi /qn", "Install Node.js LTS");
        var vConsent = ConsentToken.Declined(vCommand.CommandLine);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => vRunner.RunWindowsElevatedAsync(vCommand, vConsent));

        Assert.Empty(vProcessRunner.Requests);
    }

    /// <summary>
    /// Scenario: building the WSL/Linux sudo handoff for an apt install.
    /// Expect: a one-line "sudo ..." paste command with the exact target command, ready-to-render
    /// instructions stating TrSetup never asks for or stores the sudo password — and no
    /// process execution at all.
    /// </summary>
    [Fact]
    public void SudoHandoffPrintsOneLineAndExecutesNothing()
    {
        var vCommand = new ElevatedCommand("apt-get", "install -y socat", "Install socat for the winrun bridge");

        var vHandoff = ElevationRunner.CreateSudoHandoff(vCommand);

        Assert.Equal("sudo apt-get install -y socat", vHandoff.CommandToPaste);
        Assert.Contains(vHandoff.CommandToPaste, vHandoff.Instructions);
        Assert.Contains("never asks for or stores", vHandoff.Instructions);
        Assert.Equal("Install socat for the winrun bridge", vHandoff.Description);
        Assert.Single(vHandoff.Instructions.Split(Environment.NewLine), aLine => aLine.TrimStart().StartsWith("sudo "));
    }
}
