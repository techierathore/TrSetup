namespace TrSetup.Core.Downloads;

/// <summary>
/// The evidence trail of one installer download (REQ-FN-017): what happened, where the
/// payload landed, and the checksum facts backing it.
/// </summary>
/// <param name="Outcome">How the download ended.</param>
/// <param name="FilePath">Absolute path of the downloaded payload under the managed root, or <c>null</c> when nothing was kept.</param>
/// <param name="Evidence">
/// Human-readable evidence: the URL, the target path, and either the verified checksum,
/// the expected-vs-actual mismatch, an explicit "no published checksum" note, or the error.
/// </param>
public sealed record DownloadResult(DownloadOutcome Outcome, string? FilePath, string Evidence)
{
    /// <summary>Whether a payload was downloaded and kept (verified, or explicitly recorded as having no published checksum).</summary>
    public bool Succeeded => Outcome is DownloadOutcome.Verified or DownloadOutcome.NoPublishedChecksum;
}
