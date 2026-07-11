using TrSetup.Core.ConfigWriting;
using TrSetup.Core.Downloads;
using TrSetup.Core.Elevation;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Fixing;

/// <summary>
/// The bundle of P2 fixer frameworks a <see cref="Checks.Check"/> uses to remediate itself:
/// the installer downloader (REQ-FN-017, pinned URLs + checksums into the managed root), the
/// idempotent managed-block config writer (REQ-FN-018), and the elevation runner
/// (REQ-FN-020, Windows UAC child / *nix sudo terminal handoff — never a stored password).
/// Passing one shared instance to every check keeps fixer wiring composed in one place.
/// </summary>
public sealed class CheckFixServices
{
    /// <summary>
    /// Creates the bundle.
    /// </summary>
    /// <param name="aDownloader">The installer download framework (REQ-FN-017).</param>
    /// <param name="aConfigWriter">The idempotent managed-block config writer (REQ-FN-018).</param>
    /// <param name="aElevationRunner">The elevation/consent runner (REQ-FN-020).</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
    public CheckFixServices(
        IInstallerDownloader aDownloader,
        ManagedBlockWriter aConfigWriter,
        ElevationRunner aElevationRunner)
    {
        Downloader = aDownloader ?? throw new ArgumentNullException(nameof(aDownloader));
        ConfigWriter = aConfigWriter ?? throw new ArgumentNullException(nameof(aConfigWriter));
        ElevationRunner = aElevationRunner ?? throw new ArgumentNullException(nameof(aElevationRunner));
    }

    /// <summary>The installer download framework (REQ-FN-017).</summary>
    public IInstallerDownloader Downloader { get; }

    /// <summary>The idempotent managed-block config writer (REQ-FN-018).</summary>
    public ManagedBlockWriter ConfigWriter { get; }

    /// <summary>The elevation/consent runner (REQ-FN-020).</summary>
    public ElevationRunner ElevationRunner { get; }

    /// <summary>
    /// Builds a bundle of the real framework instances around a process runner choke-point.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point elevated children launch through.</param>
    /// <returns>A bundle wired with the production frameworks.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aProcessRunner"/> is null.</exception>
    public static CheckFixServices CreateDefault(IProcessRunner aProcessRunner)
    {
        ArgumentNullException.ThrowIfNull(aProcessRunner);
        return new CheckFixServices(
            new InstallerDownloader(),
            new ManagedBlockWriter(),
            new ElevationRunner(aProcessRunner));
    }
}
