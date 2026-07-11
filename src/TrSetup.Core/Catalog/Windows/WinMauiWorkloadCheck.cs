using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// F-WINCHK: "MAUI workload" — <c>dotnet workload list</c> on the Windows side, looking for
/// the maui workload entry. The fixer runs <c>dotnet workload install maui</c> through a
/// visible UAC child (the install writes into the shared SDK, so it needs admin) — idempotent
/// (REQ-FN-015 / REQ-FN-020).
/// </summary>
public sealed class WinMauiWorkloadCheck : WindowsCheckBase
{
    private const string Script =
        "$vDotnet = Get-Command dotnet -ErrorAction SilentlyContinue\n" +
        "if (-not $vDotnet) { Write-Output 'DOTNET-MISSING'; exit 1 }\n" +
        "dotnet workload list 2>&1\n";

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect runs through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WinMauiWorkloadCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null) : base(aProcessRunner, aFix)
    {
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix ? "(UAC) dotnet workload install maui" : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "win.maui-workload";

    /// <inheritdoc />
    public override string Title => "MAUI workload";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The .NET MAUI workload on the Windows host's SDK.",
        "The MAUI Blazor Hybrid head (Windows unpackaged + Android) cannot build without the maui workload.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunWindowsScriptAsync(Script, TimeSpan.FromSeconds(60), aCancellationToken)
            .ConfigureAwait(false);
        if (vRun.StandardOutput.Contains("DOTNET-MISSING", StringComparison.Ordinal))
        {
            return CheckResult.Fail(ViaBridge(".NET SDK not found on the Windows host."));
        }

        if (!vRun.Succeeded)
        {
            return CheckResult.Fail(ViaBridge($"dotnet workload list failed.\n{vRun.ToEvidenceString()}"));
        }

        if (vRun.StandardOutput.Contains("maui", StringComparison.OrdinalIgnoreCase))
        {
            return CheckResult.Pass(ViaBridge("MAUI workload installed (dotnet workload list contains 'maui')."));
        }

        return CheckResult.Fail(ViaBridge(
            "MAUI workload not installed (dotnet workload list has no 'maui' entry)."));
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => RunWindowsElevatedFixAsync(
            "dotnet workload install maui", "Install the .NET MAUI workload", aConsent, aCancellationToken);
}
