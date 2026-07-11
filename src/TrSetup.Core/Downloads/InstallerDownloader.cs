using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TrSetup.Core.Downloads;

/// <summary>
/// The REQ-FN-017 installer download framework: fetches a caller-pinned official URL into
/// the TrSetup-managed tools root (<see cref="TrSetupPaths.ToolsRoot"/>) and verifies the
/// payload's SHA-256 against the published checksum. A tampered payload is deleted and
/// reported; a source without a published checksum is recorded as such in the evidence —
/// never silently skipped. Downloads land via a temp file, so a failed or interrupted run
/// leaves nothing half-written (REQ-NFR-004).
/// </summary>
public sealed class InstallerDownloader : IInstallerDownloader
{
    private readonly HttpClient objHttpClient;
    private readonly ILogger<InstallerDownloader> objLogger;

    /// <summary>
    /// Creates the downloader.
    /// </summary>
    /// <param name="aHttpClient">HTTP client used for http(s) sources; a private client is created when omitted.</param>
    /// <param name="aLogger">Optional logger; a null logger is used when omitted.</param>
    public InstallerDownloader(HttpClient? aHttpClient = null, ILogger<InstallerDownloader>? aLogger = null)
    {
        objHttpClient = aHttpClient ?? new HttpClient();
        objLogger = aLogger ?? NullLogger<InstallerDownloader>.Instance;
    }

    /// <summary>
    /// Renders the exact download a fix would perform — the pinned URL verbatim, the managed
    /// target path, and the checksum stance — for use as (part of) a check's <c>FixPreview</c>.
    /// </summary>
    /// <param name="aRequest">The download the fix would perform.</param>
    /// <returns>A multi-line preview block showing URL, target path and checksum.</returns>
    public static string BuildFixPreview(DownloadRequest aRequest)
    {
        ArgumentNullException.ThrowIfNull(aRequest);
        var vTargetPath = TargetPath(aRequest);
        var vChecksumLine = aRequest.Sha256Checksum ?? "no published checksum";
        return $"download {aRequest.Url}{Environment.NewLine}" +
               $"  into  {vTargetPath}{Environment.NewLine}" +
               $"  sha256 {vChecksumLine}";
    }

    /// <summary>
    /// Downloads the payload into the managed tools root and verifies its SHA-256.
    /// </summary>
    /// <param name="aRequest">What to download, where under the managed root, and the published checksum (if any).</param>
    /// <param name="aProgress">Optional live sink for human-readable progress lines.</param>
    /// <param name="aCancellationToken">Cancels the download.</param>
    /// <returns>
    /// <see cref="DownloadOutcome.Verified"/> when the checksum matched;
    /// <see cref="DownloadOutcome.NoPublishedChecksum"/> when the source publishes none (recorded in evidence);
    /// <see cref="DownloadOutcome.ChecksumMismatch"/> when the payload was tampered (file deleted);
    /// <see cref="DownloadOutcome.Failed"/> when the fetch itself failed.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aRequest"/> is null.</exception>
    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest aRequest,
        IProgress<string>? aProgress = null,
        CancellationToken aCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aRequest);

        var vFinalPath = TargetPath(aRequest);
        var vTempPath = vFinalPath + ".download";
        Directory.CreateDirectory(Path.GetDirectoryName(vFinalPath)!);

        aProgress?.Report($"downloading {aRequest.Url}");
        try
        {
            await FetchToFileAsync(aRequest.Url, vTempPath, aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception vException) when (vException is HttpRequestException or IOException or UriFormatException)
        {
            TryDelete(vTempPath);
            objLogger.LogWarning(vException, "Download of {Url} failed.", aRequest.Url);
            return new DownloadResult(
                DownloadOutcome.Failed,
                null,
                $"download {aRequest.Url} FAILED: {vException.Message}");
        }

        return VerifyAndFinalize(aRequest, vTempPath, vFinalPath, aProgress);
    }

    private DownloadResult VerifyAndFinalize(
        DownloadRequest aRequest,
        string aTempPath,
        string aFinalPath,
        IProgress<string>? aProgress)
    {
        var vActualChecksum = ComputeSha256(aTempPath);
        if (aRequest.Sha256Checksum is null)
        {
            File.Move(aTempPath, aFinalPath, overwrite: true);
            aProgress?.Report("no published checksum for this source — recorded in evidence");
            objLogger.LogInformation("Downloaded {Url} — no published checksum.", aRequest.Url);
            return new DownloadResult(
                DownloadOutcome.NoPublishedChecksum,
                aFinalPath,
                $"downloaded {aRequest.Url} into {aFinalPath}{Environment.NewLine}" +
                $"sha256 (computed): {vActualChecksum}{Environment.NewLine}" +
                "no published checksum — source publishes none; integrity NOT verified");
        }

        if (!string.Equals(vActualChecksum, aRequest.Sha256Checksum, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(aTempPath);
            aProgress?.Report("sha256 MISMATCH — payload deleted");
            objLogger.LogError("Checksum mismatch for {Url}; payload deleted.", aRequest.Url);
            return new DownloadResult(
                DownloadOutcome.ChecksumMismatch,
                null,
                $"downloaded {aRequest.Url}{Environment.NewLine}" +
                $"sha256 expected: {aRequest.Sha256Checksum}{Environment.NewLine}" +
                $"sha256 actual:   {vActualChecksum}{Environment.NewLine}" +
                "CHECKSUM MISMATCH — payload deleted, nothing installed");
        }

        File.Move(aTempPath, aFinalPath, overwrite: true);
        aProgress?.Report($"sha256 verified — saved {aFinalPath}");
        objLogger.LogInformation("Downloaded and verified {Url}.", aRequest.Url);
        return new DownloadResult(
            DownloadOutcome.Verified,
            aFinalPath,
            $"downloaded {aRequest.Url} into {aFinalPath}{Environment.NewLine}" +
            $"sha256 verified: {vActualChecksum}");
    }

    private async Task FetchToFileAsync(string aUrl, string aTargetPath, CancellationToken aCancellationToken)
    {
        var vUri = new Uri(aUrl);
        await using var vTarget = File.Create(aTargetPath);
        if (vUri.IsFile)
        {
            await using var vSource = File.OpenRead(vUri.LocalPath);
            await vSource.CopyToAsync(vTarget, aCancellationToken).ConfigureAwait(false);
            return;
        }

        await using var vHttpStream =
            await objHttpClient.GetStreamAsync(vUri, aCancellationToken).ConfigureAwait(false);
        await vHttpStream.CopyToAsync(vTarget, aCancellationToken).ConfigureAwait(false);
    }

    private static string TargetPath(DownloadRequest aRequest)
        => Path.Combine(TrSetupPaths.ToolsRoot, aRequest.ToolName, aRequest.FileName);

    private static string ComputeSha256(string aFilePath)
    {
        using var vStream = File.OpenRead(aFilePath);
        return Convert.ToHexStringLower(SHA256.HashData(vStream));
    }

    private static void TryDelete(string aFilePath)
    {
        try
        {
            if (File.Exists(aFilePath))
            {
                File.Delete(aFilePath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup — the temp file never masquerades as an installed payload.
        }
    }
}
