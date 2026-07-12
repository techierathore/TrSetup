using TrSetup.Core.ConfigWriting;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-018 — idempotent config writes with managed marker blocks, exercised against real
/// temp files: a missing file is created; upserting the same block twice leaves exactly one
/// block (replaced in place); user edits outside the markers survive re-fix byte-for-byte;
/// all four comment syntaxes render valid markers; a corrupted block is refused.
/// </summary>
public sealed class ConfigWriterTests : IDisposable
{
    private readonly string objDirectory;
    private readonly ManagedBlockWriter objWriter = new();

    /// <summary>Creates a private temp directory for the round-trip files.</summary>
    public ConfigWriterTests()
    {
        objDirectory = Path.Combine(Path.GetTempPath(), "trsetup-configwrite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(objDirectory);
    }

    /// <summary>Deletes the temp directory.</summary>
    public void Dispose() => Directory.Delete(objDirectory, recursive: true);

    /// <summary>
    /// Scenario: the target file does not exist (fresh machine, no .wslconfig yet).
    /// Expect: the file is created containing exactly one managed block with the content.
    /// </summary>
    [Fact]
    public void MissingFileIsCreatedWithSingleBlock()
    {
        var vPath = Path.Combine(objDirectory, "sub", ".wslconfig");

        var vResult = objWriter.UpsertBlock(vPath, "wsl.memory", "[wsl2]\nmemory=24GB", CommentSyntax.Hash);

        Assert.Equal(ManagedBlockOutcome.CreatedFile, vResult.Outcome);
        var vText = File.ReadAllText(vPath);
        Assert.Contains("memory=24GB", vText);
        Assert.Equal(1, CountOccurrences(vText, "# >>> TrSetup managed block: wsl.memory"));
    }

    /// <summary>
    /// Scenario: the fix re-runs — the same block id is upserted twice (second time with new
    /// content) into a file that also holds user content.
    /// Expect: exactly one begin/end marker pair, the new content present, the old gone.
    /// </summary>
    [Fact]
    public void RerunReplacesInPlaceNeverDuplicates()
    {
        var vPath = Path.Combine(objDirectory, ".bashrc");
        File.WriteAllText(vPath, "# user aliases\nalias ll='ls -la'\n");

        objWriter.UpsertBlock(vPath, "path.dotnet", "export PATH=\"$PATH:$HOME/.dotnet\"", CommentSyntax.Hash);
        objWriter.UpsertBlock(vPath, "path.dotnet", "export PATH=\"$PATH:$HOME/.dotnet:$HOME/.dotnet/tools\"", CommentSyntax.Hash);

        var vText = File.ReadAllText(vPath);
        Assert.Equal(1, CountOccurrences(vText, ">>> TrSetup managed block: path.dotnet"));
        Assert.Equal(1, CountOccurrences(vText, "<<< TrSetup managed block: path.dotnet"));
        Assert.Contains(".dotnet/tools", vText);
        Assert.Equal(1, CountOccurrences(vText, "export PATH"));
    }

    /// <summary>
    /// Scenario: after TrSetup wrote its block the user hand-edits the file outside the
    /// markers (above and below); the fix then re-runs with changed block content.
    /// Expect: every user byte outside the markers is preserved exactly; only the block body changed.
    /// </summary>
    [Fact]
    public void UserEditsOutsideMarkersSurviveRefix()
    {
        var vPath = Path.Combine(objDirectory, ".bashrc");
        File.WriteAllText(vPath, "# my prompt\nexport PS1='\\w$ '\n");
        objWriter.UpsertBlock(vPath, "path.dotnet", "export PATH=v1", CommentSyntax.Hash);
        File.AppendAllText(vPath, "# added by the user afterwards\nalias gs='git status'\n");
        var vBefore = File.ReadAllText(vPath);

        var vResult = objWriter.UpsertBlock(vPath, "path.dotnet", "export PATH=v2", CommentSyntax.Hash);

        Assert.Equal(ManagedBlockOutcome.ReplacedBlock, vResult.Outcome);
        var vAfter = File.ReadAllText(vPath);
        Assert.Equal(vBefore.Replace("export PATH=v1", "export PATH=v2"), vAfter);
        Assert.StartsWith("# my prompt\nexport PS1='\\w$ '\n", vAfter);
        Assert.EndsWith("# added by the user afterwards\nalias gs='git status'\n", vAfter);
    }

    /// <summary>
    /// Scenario: upserting identical content a second time.
    /// Expect: outcome Unchanged and the file bytes untouched (mtime-friendly idempotency).
    /// </summary>
    [Fact]
    public void IdenticalRerunLeavesFileUntouched()
    {
        var vPath = Path.Combine(objDirectory, "profile.ps1");
        objWriter.UpsertBlock(vPath, "ps.path", "$env:Path += ';C:\\tools'", CommentSyntax.Hash);
        var vBefore = File.ReadAllBytes(vPath);

        var vResult = objWriter.UpsertBlock(vPath, "ps.path", "$env:Path += ';C:\\tools'", CommentSyntax.Hash);

        Assert.Equal(ManagedBlockOutcome.Unchanged, vResult.Outcome);
        Assert.Equal(vBefore, File.ReadAllBytes(vPath));
    }

    /// <summary>
    /// Scenario: marker rendering across the four supported comment syntaxes
    /// (# shell/ini, ; ini, // jsonc, &lt;!-- --&gt; xml/plist).
    /// Expect: each file carries markers in its own syntax and round-trips to a single block.
    /// </summary>
    [Theory]
    [InlineData("#", "")]
    [InlineData(";", "")]
    [InlineData("//", "")]
    [InlineData("<!--", "-->")]
    public void AllCommentSyntaxesRoundTrip(string aPrefix, string aSuffix)
    {
        var vSyntax = new CommentSyntax(aPrefix, aSuffix);
        var vPath = Path.Combine(objDirectory, "file-" + aPrefix.Length + aSuffix.Length + ".conf");

        objWriter.UpsertBlock(vPath, "test.block", "value=1", vSyntax);
        objWriter.UpsertBlock(vPath, "test.block", "value=2", vSyntax);

        var vText = File.ReadAllText(vPath);
        Assert.Equal(1, CountOccurrences(vText, aPrefix + " >>> TrSetup managed block: test.block"));
        Assert.Contains("value=2", vText);
        Assert.DoesNotContain("value=1", vText);
        if (aSuffix.Length > 0)
        {
            Assert.Contains(">>> " + aSuffix, vText);
        }
    }

    /// <summary>
    /// Scenario: the file holds a begin marker whose end marker was deleted by hand.
    /// Expect: the writer refuses to touch the file (InvalidOperationException) instead of
    /// guessing where the block ends and clobbering user content.
    /// </summary>
    [Fact]
    public void CorruptedBlockIsRefusedNotClobbered()
    {
        var vPath = Path.Combine(objDirectory, "broken.conf");
        objWriter.UpsertBlock(vPath, "b1", "x=1", CommentSyntax.Hash);
        var vText = File.ReadAllText(vPath);
        File.WriteAllText(vPath, vText.Replace("# <<< TrSetup managed block: b1 <<<", string.Empty));

        Assert.Throws<InvalidOperationException>(
            () => objWriter.UpsertBlock(vPath, "b1", "x=2", CommentSyntax.Hash));
    }

    /// <summary>
    /// Scenario: ContainsBlock and RemoveBlock around one write.
    /// Expect: contains is true after upsert; remove deletes the block and leaves the user
    /// content intact; contains is false afterwards.
    /// </summary>
    [Fact]
    public void ContainsAndRemoveBlockWork()
    {
        var vPath = Path.Combine(objDirectory, "removable.conf");
        File.WriteAllText(vPath, "keep-me=true\n");
        objWriter.UpsertBlock(vPath, "b1", "managed=true", CommentSyntax.Hash);
        Assert.True(objWriter.ContainsBlock(vPath, "b1", CommentSyntax.Hash));

        var vRemoved = objWriter.RemoveBlock(vPath, "b1", CommentSyntax.Hash);

        Assert.True(vRemoved);
        Assert.False(objWriter.ContainsBlock(vPath, "b1", CommentSyntax.Hash));
        Assert.Contains("keep-me=true", File.ReadAllText(vPath));
        Assert.DoesNotContain("managed=true", File.ReadAllText(vPath));
    }

    private static int CountOccurrences(string aText, string aNeedle)
    {
        var vCount = 0;
        var vIndex = 0;
        while ((vIndex = aText.IndexOf(aNeedle, vIndex, StringComparison.Ordinal)) >= 0)
        {
            vCount++;
            vIndex += aNeedle.Length;
        }

        return vCount;
    }
}
