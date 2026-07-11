using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Elevation;

/// <summary>
/// The REQ-FN-020 elevation runner. Elevated steps always expose the exact command and run
/// only against a granted <see cref="ConsentToken"/>. On Windows, elevation goes through
/// UAC in a visible child process (<c>powershell Start-Process -Verb RunAs</c> via
/// <see cref="IProcessRunner"/>). On WSL/Linux, TrSetup performs NO sudo itself — it hands
/// the user a <see cref="TerminalHandoff"/> with the one command to paste into their own
/// terminal. No code path here ever reads, stores or caches credentials (REQ-NFR-002).
/// </summary>
public sealed class ElevationRunner
{
    private readonly IProcessRunner objProcessRunner;
    private readonly ILogger<ElevationRunner> objLogger;

    /// <summary>
    /// Creates the runner.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point elevated child processes launch through.</param>
    /// <param name="aLogger">Optional logger; a null logger is used when omitted.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aProcessRunner"/> is null.</exception>
    public ElevationRunner(IProcessRunner aProcessRunner, ILogger<ElevationRunner>? aLogger = null)
    {
        objProcessRunner = aProcessRunner ?? throw new ArgumentNullException(nameof(aProcessRunner));
        objLogger = aLogger ?? NullLogger<ElevationRunner>.Instance;
    }

    /// <summary>
    /// Builds the exact non-elevated launcher request that triggers UAC for the command:
    /// <c>powershell.exe ... Start-Process -FilePath '...' -ArgumentList '...' -Verb RunAs -Wait</c>.
    /// The elevated child is deliberately visible — no hidden window, no auto-elevation tricks.
    /// </summary>
    /// <param name="aCommand">The command to elevate.</param>
    /// <param name="aTimeout">Maximum time to wait for the elevated child, or <c>null</c> for no timeout.</param>
    /// <returns>The process request to execute through <see cref="IProcessRunner"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aCommand"/> is null.</exception>
    public static ProcessRunRequest BuildWindowsElevationRequest(ElevatedCommand aCommand, TimeSpan? aTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(aCommand);

        var vFilePart = $"-FilePath '{EscapeForPowerShell(aCommand.FileName)}'";
        var vArgumentsPart = aCommand.Arguments.Length == 0
            ? string.Empty
            : $" -ArgumentList '{EscapeForPowerShell(aCommand.Arguments)}'";
        var vStartProcess = $"Start-Process {vFilePart}{vArgumentsPart} -Verb RunAs -Wait";
        return new ProcessRunRequest(
            "powershell.exe",
            $"-NoProfile -NonInteractive -Command \"{vStartProcess}\"",
            WorkingDirectory: null,
            Timeout: aTimeout);
    }

    /// <summary>
    /// Runs the command elevated on Windows: UAC prompts in a visible child process. Requires
    /// a granted consent token whose preview contains the exact command line — there is no
    /// path to elevation without the user having seen and approved that command.
    /// </summary>
    /// <param name="aCommand">The command to elevate.</param>
    /// <param name="aConsent">The granted consent token issued after the command was previewed.</param>
    /// <param name="aOutputProgress">Optional live sink for the launcher's output lines.</param>
    /// <param name="aCancellationToken">Cancels waiting for the elevated child.</param>
    /// <returns>The launcher's evidence trail (the elevated child's own console stays visible to the user).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aCommand"/> or <paramref name="aConsent"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the consent token is not granted.</exception>
    public Task<ProcessRunResult> RunWindowsElevatedAsync(
        ElevatedCommand aCommand,
        ConsentToken aConsent,
        IProgress<string>? aOutputProgress = null,
        CancellationToken aCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aCommand);
        ArgumentNullException.ThrowIfNull(aConsent);
        if (!aConsent.IsGranted)
        {
            throw new InvalidOperationException(
                $"Elevated command '{aCommand.CommandLine}' requires a granted consent token; nothing was executed.");
        }

        objLogger.LogInformation("Launching UAC-elevated child: {CommandLine}", aCommand.CommandLine);
        var vRequest = BuildWindowsElevationRequest(aCommand);
        return objProcessRunner.RunAsync(vRequest, aOutputProgress, aCancellationToken);
    }

    /// <summary>
    /// Builds the WSL/Linux sudo handoff: TrSetup executes nothing — the UI shows the one
    /// exact <c>sudo</c> line for the user to paste into their own terminal, where sudo
    /// itself prompts them. No password ever passes through, or is stored by, TrSetup.
    /// </summary>
    /// <param name="aCommand">The command that needs root.</param>
    /// <returns>The handoff object for the UI to render.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aCommand"/> is null.</exception>
    public static TerminalHandoff CreateSudoHandoff(ElevatedCommand aCommand)
    {
        ArgumentNullException.ThrowIfNull(aCommand);
        var vCommandToPaste = $"sudo {aCommand.CommandLine}";
        var vInstructions =
            $"This step needs root. TrSetup never asks for or stores your sudo password.{Environment.NewLine}" +
            $"Open your own terminal and paste this one line:{Environment.NewLine}" +
            $"  {vCommandToPaste}{Environment.NewLine}" +
            "Then re-run the check to verify.";
        return new TerminalHandoff(vCommandToPaste, aCommand.Description, vInstructions);
    }

    private static string EscapeForPowerShell(string aValue) => aValue.Replace("'", "''");
}
