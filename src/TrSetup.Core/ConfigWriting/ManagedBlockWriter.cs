using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TrSetup.Core.ConfigWriting;

/// <summary>
/// The REQ-FN-018 idempotent config-write framework. Everything TrSetup writes into a user
/// file (<c>.wslconfig</c>, <c>.bashrc</c> PATH line, plists, <c>.ps1</c>) sits inside a
/// managed marker block identified by a stable block id. Upserting the same block id again
/// replaces the block in place — the file always contains exactly one copy — and every byte
/// outside the markers is preserved untouched. A missing file is created.
/// </summary>
public sealed class ManagedBlockWriter
{
    private const string BeginMarkerFormat = ">>> TrSetup managed block: {0} (do not edit between these markers) >>>";
    private const string EndMarkerFormat = "<<< TrSetup managed block: {0} <<<";

    private readonly ILogger<ManagedBlockWriter> objLogger;

    /// <summary>
    /// Creates the writer.
    /// </summary>
    /// <param name="aLogger">Optional logger; a null logger is used when omitted.</param>
    public ManagedBlockWriter(ILogger<ManagedBlockWriter>? aLogger = null)
    {
        objLogger = aLogger ?? NullLogger<ManagedBlockWriter>.Instance;
    }

    /// <summary>
    /// Inserts or replaces the managed block <paramref name="aBlockId"/> in the file:
    /// creates the file when missing, appends the block when absent, replaces it in place
    /// when present — re-runs never duplicate and user content outside the markers is
    /// preserved byte-for-byte.
    /// </summary>
    /// <param name="aFilePath">Absolute path of the config file to write into.</param>
    /// <param name="aBlockId">Stable unique id of the block (e.g. <c>wsl.memory-limits</c>).</param>
    /// <param name="aContent">The block body (without markers); trailing newlines are normalized.</param>
    /// <param name="aCommentSyntax">The comment syntax the file uses for the marker lines.</param>
    /// <returns>What the upsert did, with evidence for the fix trail.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the file contains a begin marker without its end marker (corrupted block).</exception>
    public ManagedBlockWriteResult UpsertBlock(
        string aFilePath,
        string aBlockId,
        string aContent,
        CommentSyntax aCommentSyntax)
    {
        ArgumentNullException.ThrowIfNull(aFilePath);
        ArgumentNullException.ThrowIfNull(aBlockId);
        ArgumentNullException.ThrowIfNull(aContent);
        ArgumentNullException.ThrowIfNull(aCommentSyntax);

        if (!File.Exists(aFilePath))
        {
            return CreateFileWithBlock(aFilePath, aBlockId, aContent, aCommentSyntax);
        }

        return UpsertIntoExistingFile(aFilePath, aBlockId, aContent, aCommentSyntax);
    }

    /// <summary>
    /// Whether the file currently contains the managed block.
    /// </summary>
    /// <param name="aFilePath">Absolute path of the config file.</param>
    /// <param name="aBlockId">Stable unique id of the block.</param>
    /// <param name="aCommentSyntax">The comment syntax the file uses.</param>
    /// <returns><c>true</c> when the begin marker for the block id is present.</returns>
    public bool ContainsBlock(string aFilePath, string aBlockId, CommentSyntax aCommentSyntax)
    {
        ArgumentNullException.ThrowIfNull(aCommentSyntax);
        if (!File.Exists(aFilePath))
        {
            return false;
        }

        var vText = ReadFile(aFilePath, out _);
        return vText.Contains(BeginMarker(aBlockId, aCommentSyntax), StringComparison.Ordinal);
    }

    /// <summary>
    /// Removes the managed block from the file, leaving everything outside the markers
    /// untouched. A missing file or absent block is a no-op.
    /// </summary>
    /// <param name="aFilePath">Absolute path of the config file.</param>
    /// <param name="aBlockId">Stable unique id of the block.</param>
    /// <param name="aCommentSyntax">The comment syntax the file uses.</param>
    /// <returns><c>true</c> when a block was found and removed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the file contains a begin marker without its end marker.</exception>
    public bool RemoveBlock(string aFilePath, string aBlockId, CommentSyntax aCommentSyntax)
    {
        ArgumentNullException.ThrowIfNull(aCommentSyntax);
        if (!File.Exists(aFilePath))
        {
            return false;
        }

        var vText = ReadFile(aFilePath, out var vHadBom);
        if (!TryLocateBlock(vText, aBlockId, aCommentSyntax, out var vStart, out var vEnd))
        {
            return false;
        }

        var vAfter = SkipOneNewline(vText, vEnd);
        WriteFile(aFilePath, vText[..vStart] + vText[vAfter..], vHadBom);
        objLogger.LogInformation("Removed managed block {BlockId} from {FilePath}.", aBlockId, aFilePath);
        return true;
    }

    private ManagedBlockWriteResult CreateFileWithBlock(
        string aFilePath,
        string aBlockId,
        string aContent,
        CommentSyntax aCommentSyntax)
    {
        var vDirectory = Path.GetDirectoryName(aFilePath);
        if (!string.IsNullOrEmpty(vDirectory))
        {
            Directory.CreateDirectory(vDirectory);
        }

        var vNewline = Environment.NewLine;
        WriteFile(aFilePath, RenderBlock(aBlockId, aContent, aCommentSyntax, vNewline) + vNewline, aHadBom: false);
        objLogger.LogInformation("Created {FilePath} with managed block {BlockId}.", aFilePath, aBlockId);
        return new ManagedBlockWriteResult(
            ManagedBlockOutcome.CreatedFile,
            aFilePath,
            aBlockId,
            $"created {aFilePath} with managed block '{aBlockId}'");
    }

    private ManagedBlockWriteResult UpsertIntoExistingFile(
        string aFilePath,
        string aBlockId,
        string aContent,
        CommentSyntax aCommentSyntax)
    {
        var vText = ReadFile(aFilePath, out var vHadBom);
        var vNewline = vText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var vBlock = RenderBlock(aBlockId, aContent, aCommentSyntax, vNewline);

        if (TryLocateBlock(vText, aBlockId, aCommentSyntax, out var vStart, out var vEnd))
        {
            return ReplaceBlock(aFilePath, aBlockId, vText, vBlock, vStart, vEnd, vHadBom);
        }

        var vSeparator = vText.Length == 0 || vText.EndsWith('\n') ? string.Empty : vNewline;
        WriteFile(aFilePath, vText + vSeparator + vBlock + vNewline, vHadBom);
        objLogger.LogInformation("Appended managed block {BlockId} to {FilePath}.", aBlockId, aFilePath);
        return new ManagedBlockWriteResult(
            ManagedBlockOutcome.AppendedBlock,
            aFilePath,
            aBlockId,
            $"appended managed block '{aBlockId}' to {aFilePath}");
    }

    private ManagedBlockWriteResult ReplaceBlock(
        string aFilePath,
        string aBlockId,
        string aText,
        string aBlock,
        int aStart,
        int aEnd,
        bool aHadBom)
    {
        if (string.Equals(aText[aStart..aEnd], aBlock, StringComparison.Ordinal))
        {
            return new ManagedBlockWriteResult(
                ManagedBlockOutcome.Unchanged,
                aFilePath,
                aBlockId,
                $"managed block '{aBlockId}' in {aFilePath} already up to date");
        }

        WriteFile(aFilePath, aText[..aStart] + aBlock + aText[aEnd..], aHadBom);
        objLogger.LogInformation("Replaced managed block {BlockId} in {FilePath} in place.", aBlockId, aFilePath);
        return new ManagedBlockWriteResult(
            ManagedBlockOutcome.ReplacedBlock,
            aFilePath,
            aBlockId,
            $"replaced managed block '{aBlockId}' in {aFilePath} in place");
    }

    private static bool TryLocateBlock(
        string aText,
        string aBlockId,
        CommentSyntax aCommentSyntax,
        out int aStart,
        out int aEnd)
    {
        var vBeginMarker = BeginMarker(aBlockId, aCommentSyntax);
        var vEndMarker = EndMarker(aBlockId, aCommentSyntax);
        aStart = aText.IndexOf(vBeginMarker, StringComparison.Ordinal);
        aEnd = 0;
        if (aStart < 0)
        {
            return false;
        }

        var vEndMarkerIndex = aText.IndexOf(vEndMarker, aStart, StringComparison.Ordinal);
        if (vEndMarkerIndex < 0)
        {
            throw new InvalidOperationException(
                $"Managed block '{aBlockId}' has a begin marker without an end marker — refusing to touch the file.");
        }

        aEnd = vEndMarkerIndex + vEndMarker.Length;
        return true;
    }

    private static string RenderBlock(string aBlockId, string aContent, CommentSyntax aCommentSyntax, string aNewline)
    {
        var vBody = aContent.TrimEnd('\r', '\n');
        return BeginMarker(aBlockId, aCommentSyntax) + aNewline + vBody + aNewline + EndMarker(aBlockId, aCommentSyntax);
    }

    private static string BeginMarker(string aBlockId, CommentSyntax aCommentSyntax)
        => aCommentSyntax.RenderMarker(string.Format(BeginMarkerFormat, aBlockId));

    private static string EndMarker(string aBlockId, CommentSyntax aCommentSyntax)
        => aCommentSyntax.RenderMarker(string.Format(EndMarkerFormat, aBlockId));

    private static int SkipOneNewline(string aText, int aIndex)
    {
        if (aIndex < aText.Length && aText[aIndex] == '\r')
        {
            aIndex++;
        }

        if (aIndex < aText.Length && aText[aIndex] == '\n')
        {
            aIndex++;
        }

        return aIndex;
    }

    private static string ReadFile(string aFilePath, out bool aHadBom)
    {
        var vBytes = File.ReadAllBytes(aFilePath);
        aHadBom = vBytes.Length >= 3 && vBytes[0] == 0xEF && vBytes[1] == 0xBB && vBytes[2] == 0xBF;
        return new UTF8Encoding(false).GetString(aHadBom ? vBytes.AsSpan(3) : vBytes);
    }

    private static void WriteFile(string aFilePath, string aText, bool aHadBom)
        => File.WriteAllText(aFilePath, aText, new UTF8Encoding(aHadBom));
}
