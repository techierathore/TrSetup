namespace TrSetup.Core.Downloads;

/// <summary>
/// Testability seam over <see cref="InstallerDownloader"/> (REQ-FN-017): fixers depend on this
/// interface so unit tests can supply a fake that returns a canned <see cref="DownloadResult"/>
/// without touching the network or the real managed root.
/// </summary>
public interface IInstallerDownloader
{
    /// <summary>
    /// Downloads the pinned payload into the managed tools root and verifies its SHA-256.
    /// </summary>
    /// <param name="aRequest">What to download, where under the managed root, and the published checksum (if any).</param>
    /// <param name="aProgress">Optional live sink for human-readable progress lines.</param>
    /// <param name="aCancellationToken">Cancels the download.</param>
    /// <returns>The download's evidence trail (outcome, kept path, checksum facts).</returns>
    Task<DownloadResult> DownloadAsync(
        DownloadRequest aRequest,
        IProgress<string>? aProgress = null,
        CancellationToken aCancellationToken = default);
}
