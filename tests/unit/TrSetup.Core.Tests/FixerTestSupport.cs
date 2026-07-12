using TrSetup.Core.Checks;
using TrSetup.Core.ConfigWriting;
using TrSetup.Core.Downloads;
using TrSetup.Core.Elevation;
using TrSetup.Core.Fixing;
using TrSetup.Core.Tests.TestDoubles;

namespace TrSetup.Core.Tests;

/// <summary>
/// Shared helpers for the P2 fixer tests (REQ-FN-014..016): wires the fixer frameworks around
/// fakes so no fix ever touches the network or the real machine, and drives the standard
/// consent → fix → re-verify pipeline with granted consent.
/// </summary>
internal static class FixerTestSupport
{
    /// <summary>
    /// Builds the fixer bundle around a fake process runner (for elevation) and an optional fake
    /// downloader; the managed-block config writer writes to whatever real paths the check resolves
    /// (tests point those at temp directories).
    /// </summary>
    /// <param name="aProcessRunner">The fake process choke-point elevated children launch through.</param>
    /// <param name="aDownloader">Optional fake installer downloader (a verified fake is used when omitted).</param>
    /// <returns>A fixer bundle safe for unit tests.</returns>
    internal static CheckFixServices Fix(FakeProcessRunner aProcessRunner, IInstallerDownloader? aDownloader = null)
        => new(aDownloader ?? new FakeInstallerDownloader(), new ManagedBlockWriter(), new ElevationRunner(aProcessRunner));

    /// <summary>A consent-granting pipeline for driving a real check's fixer end to end.</summary>
    /// <returns>A pipeline whose consent gate always approves.</returns>
    internal static FixPipeline GrantingPipeline() => new(new FakeConsentProvider(aGrant: true));

    /// <summary>A granted consent token carrying the check's own preview, for calling FixAsync directly.</summary>
    /// <param name="aCheck">The check whose preview backs the token.</param>
    /// <returns>A granted consent token.</returns>
    internal static ConsentToken GrantFor(Check aCheck) => ConsentToken.Granted(aCheck.FixPreview ?? string.Empty);

    /// <summary>Creates a fresh private temp directory the caller is responsible for deleting.</summary>
    /// <param name="aTag">A short tag folded into the directory name.</param>
    /// <returns>The created directory path.</returns>
    internal static string NewTempDir(string aTag)
    {
        var vDir = Path.Combine(Path.GetTempPath(), $"trsetup-{aTag}-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vDir);
        return vDir;
    }
}
