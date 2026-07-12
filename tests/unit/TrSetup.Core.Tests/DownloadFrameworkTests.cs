using System.Security.Cryptography;
using System.Text;
using TrSetup.Core.Downloads;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-017 — installer download framework: real temp-file downloads land under the
/// TrSetup-managed root, SHA-256 verification passes on an intact payload, a tampered
/// payload fails the checksum and is deleted, a source without a published checksum is
/// recorded explicitly (never silently skipped), and the fix preview surfaces the pinned
/// URL verbatim.
/// </summary>
[Collection(ManagedRootCollection.Name)]
public sealed class DownloadFrameworkTests : IDisposable
{
    private readonly string objRoot;

    /// <summary>
    /// Points the managed root at a private temp directory so tests never touch the real
    /// <c>~/.trsetup</c> / <c>%LOCALAPPDATA%\TrSetup</c>.
    /// </summary>
    public DownloadFrameworkTests()
    {
        objRoot = Path.Combine(Path.GetTempPath(), "trsetup-tests-" + Guid.NewGuid().ToString("N"));
        TrSetupPaths.RootOverride = objRoot;
    }

    /// <summary>Restores the default managed root and deletes the temp directory.</summary>
    public void Dispose()
    {
        TrSetupPaths.RootOverride = null;
        if (Directory.Exists(objRoot))
        {
            Directory.Delete(objRoot, recursive: true);
        }
    }

    /// <summary>
    /// Scenario: a real file:// source is downloaded with the correct published SHA-256.
    /// Expect: outcome Verified, the payload lands under the managed tools root
    /// ({root}/tools/{tool}/{file}) byte-identical to the source, evidence records the checksum.
    /// </summary>
    [Fact]
    public async Task VerifiedDownloadLandsUnderManagedRoot()
    {
        var vPayload = Encoding.UTF8.GetBytes("official installer payload v1.2.3");
        var vSourcePath = WriteSourceFile(vPayload);
        var vChecksum = Convert.ToHexStringLower(SHA256.HashData(vPayload));
        var vRequest = new DownloadRequest(new Uri(vSourcePath).AbsoluteUri, "faketool", "faketool.bin", vChecksum);

        var vResult = await new InstallerDownloader().DownloadAsync(vRequest);

        Assert.Equal(DownloadOutcome.Verified, vResult.Outcome);
        Assert.True(vResult.Succeeded);
        Assert.Equal(Path.Combine(objRoot, "tools", "faketool", "faketool.bin"), vResult.FilePath);
        Assert.Equal(vPayload, await File.ReadAllBytesAsync(vResult.FilePath!));
        Assert.Contains(vChecksum, vResult.Evidence);
    }

    /// <summary>
    /// Scenario: the payload was tampered with — its bytes no longer match the published SHA-256.
    /// Expect: outcome ChecksumMismatch, nothing kept on disk under the tool folder, and the
    /// evidence reports both the expected and the actual checksum.
    /// </summary>
    [Fact]
    public async Task TamperedPayloadFailsChecksumAndReports()
    {
        var vSourcePath = WriteSourceFile(Encoding.UTF8.GetBytes("TAMPERED payload"));
        var vExpectedChecksum = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("the real payload")));
        var vRequest = new DownloadRequest(new Uri(vSourcePath).AbsoluteUri, "faketool", "faketool.bin", vExpectedChecksum);

        var vResult = await new InstallerDownloader().DownloadAsync(vRequest);

        Assert.Equal(DownloadOutcome.ChecksumMismatch, vResult.Outcome);
        Assert.False(vResult.Succeeded);
        Assert.Null(vResult.FilePath);
        Assert.Contains("MISMATCH", vResult.Evidence);
        Assert.Contains(vExpectedChecksum, vResult.Evidence);
        var vToolDir = Path.Combine(objRoot, "tools", "faketool");
        Assert.Empty(Directory.GetFiles(vToolDir));
    }

    /// <summary>
    /// Scenario: the source publishes no checksum (request carries null).
    /// Expect: the download is kept but the outcome is NoPublishedChecksum and the evidence
    /// explicitly records "no published checksum" — never a silent skip.
    /// </summary>
    [Fact]
    public async Task MissingChecksumIsRecordedNeverSilentlySkipped()
    {
        var vSourcePath = WriteSourceFile(Encoding.UTF8.GetBytes("payload without published checksum"));
        var vRequest = new DownloadRequest(new Uri(vSourcePath).AbsoluteUri, "faketool", "faketool.bin");

        var vResult = await new InstallerDownloader().DownloadAsync(vRequest);

        Assert.Equal(DownloadOutcome.NoPublishedChecksum, vResult.Outcome);
        Assert.True(vResult.Succeeded);
        Assert.Contains("no published checksum", vResult.Evidence);
        Assert.True(File.Exists(vResult.FilePath));
    }

    /// <summary>
    /// Scenario: the pinned source URL does not exist.
    /// Expect: outcome Failed with the error in evidence and no file kept anywhere.
    /// </summary>
    [Fact]
    public async Task FailedFetchKeepsNothing()
    {
        var vMissingSource = Path.Combine(objRoot, "no-such-source.bin");
        var vRequest = new DownloadRequest(new Uri(vMissingSource).AbsoluteUri, "faketool", "faketool.bin");

        var vResult = await new InstallerDownloader().DownloadAsync(vRequest);

        Assert.Equal(DownloadOutcome.Failed, vResult.Outcome);
        Assert.Null(vResult.FilePath);
        Assert.Contains("FAILED", vResult.Evidence);
        Assert.Empty(Directory.GetFiles(Path.Combine(objRoot, "tools", "faketool")));
    }

    /// <summary>
    /// Scenario: a fix preview is built for a pinned download.
    /// Expect: the exact URL appears verbatim, the managed target path is shown, and a null
    /// checksum renders as "no published checksum".
    /// </summary>
    [Fact]
    public void FixPreviewSurfacesPinnedUrlVerbatim()
    {
        var vRequest = new DownloadRequest("https://nodejs.org/dist/v22.11.0/node-v22.11.0-x64.msi", "node", "node.msi");

        var vPreview = InstallerDownloader.BuildFixPreview(vRequest);

        Assert.Contains("https://nodejs.org/dist/v22.11.0/node-v22.11.0-x64.msi", vPreview);
        Assert.Contains(Path.Combine(objRoot, "tools", "node", "node.msi"), vPreview);
        Assert.Contains("no published checksum", vPreview);
    }

    /// <summary>
    /// Scenario: the managed-paths static with the override active.
    /// Expect: ToolsRoot is {override}/tools, and clearing the override restores a platform
    /// default ending in "TrSetup" (Windows) or ".trsetup" (Linux/macOS).
    /// </summary>
    [Fact]
    public void ManagedRootHonorsOverrideAndDefault()
    {
        Assert.Equal(Path.Combine(objRoot, "tools"), TrSetupPaths.ToolsRoot);

        TrSetupPaths.RootOverride = null;
        var vDefaultLeaf = OperatingSystem.IsWindows() ? "TrSetup" : ".trsetup";
        Assert.Equal(vDefaultLeaf, Path.GetFileName(TrSetupPaths.ManagedRoot));

        TrSetupPaths.RootOverride = objRoot;
    }

    private string WriteSourceFile(byte[] aBytes)
    {
        Directory.CreateDirectory(objRoot);
        var vSourcePath = Path.Combine(objRoot, "source-" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(vSourcePath, aBytes);
        return vSourcePath;
    }
}
