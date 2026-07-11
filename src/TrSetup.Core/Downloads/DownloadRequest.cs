namespace TrSetup.Core.Downloads;

/// <summary>
/// Describes one installer/tool download (REQ-FN-017). The URL is the caller's pinned
/// official source — the downloader never invents or rewrites URLs, and the same URL is
/// surfaced verbatim in the fix preview the user consents to.
/// </summary>
/// <param name="Url">The pinned official download URL, exactly as shown in the fix preview (http(s) or file scheme).</param>
/// <param name="ToolName">Managed subfolder under the tools root the payload lands in (e.g. <c>node</c>, <c>android-cmdline-tools</c>).</param>
/// <param name="FileName">Target file name inside the tool folder (e.g. <c>node-v22.x-x64.msi</c>).</param>
/// <param name="Sha256Checksum">
/// The publisher's SHA-256 checksum (hex), or <c>null</c> when the source publishes none —
/// a null checksum is recorded as "no published checksum" in the evidence, never silently skipped.
/// </param>
public sealed record DownloadRequest(
    string Url,
    string ToolName,
    string FileName,
    string? Sha256Checksum = null);
