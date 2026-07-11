namespace TrSetup.Core.Downloads;

/// <summary>
/// How one installer download ended (REQ-FN-017).
/// </summary>
public enum DownloadOutcome
{
    /// <summary>The payload downloaded and its SHA-256 matched the published checksum.</summary>
    Verified = 0,

    /// <summary>
    /// The payload downloaded but the source publishes no checksum — recorded explicitly
    /// in the evidence, never silently treated as verified.
    /// </summary>
    NoPublishedChecksum = 1,

    /// <summary>The payload's SHA-256 did not match the published checksum; the file was deleted.</summary>
    ChecksumMismatch = 2,

    /// <summary>The download itself failed (network/IO error); nothing was kept on disk.</summary>
    Failed = 3
}
