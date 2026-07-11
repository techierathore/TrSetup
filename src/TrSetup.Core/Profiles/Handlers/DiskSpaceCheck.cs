using System.Globalization;
using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// REQ-FN-029 — the free-disk-space floor check. Reads the free space on the configured
/// <c>path</c> (default the TrSetup-managed root's drive) and compares it to the required
/// <c>floorGb</c>. A breach is a <see cref="CheckStatus.Warn"/> (NEVER a <see cref="CheckStatus.Fail"/>) —
/// low disk is guidance, not a hard block — carrying the free-vs-required figures in the evidence.
/// There is no fixer: TrSetup cannot free the user's disk, so the row offers guidance only.
/// </summary>
public sealed class DiskSpaceCheck : ProfileHeavyCheck
{
    /// <summary>Bytes in one gibibyte, used to convert the raw free bytes to the GB figures shown.</summary>
    public const long BytesPerGb = 1024L * 1024 * 1024;

    private readonly Func<string, long?> objFreeBytesResolver;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aRequirement">The disk-space requirement (reads <c>floorGb</c> and optional <c>path</c>).</param>
    /// <param name="aProfileName">The owning profile name — the app this row is scoped to.</param>
    /// <param name="aFreeBytesResolver">
    /// Resolver for a path's drive free bytes (returns <c>null</c> when the drive cannot be read);
    /// defaults to a real <see cref="DriveInfo"/> lookup. Injectable so tests need no real disk state.
    /// </param>
    public DiskSpaceCheck(
        ProfileRequirement aRequirement,
        string aProfileName,
        Func<string, long?>? aFreeBytesResolver = null)
        : base(aRequirement, aProfileName)
    {
        objFreeBytesResolver = aFreeBytesResolver ?? RealFreeBytes;
    }

    /// <inheritdoc />
    public override string Category => ProfileBoardCategories.Capacity;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "A minimum amount of free disk space the app's build/run workflow needs on the managed drive.",
        "Downloads, managed runtimes and build outputs need headroom; below the floor those steps can fail mid-way.",
        "WORKFLOW §0");

    /// <inheritdoc />
    public override Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vFloorText = Requirement.Param("floorGb");
        if (!long.TryParse(vFloorText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vFloorGb) || vFloorGb <= 0)
        {
            return Task.FromResult(CheckResult.Warn($"Invalid disk-space floor '{vFloorText}'; expected a positive integer of GB."));
        }

        var vPath = Requirement.Param("path") ?? TrSetupPaths.ManagedRoot;
        var vFreeBytes = objFreeBytesResolver(vPath);
        if (vFreeBytes is null)
        {
            return Task.FromResult(CheckResult.Warn($"Could not read free space on '{vPath}'; require {vFloorGb} GB free."));
        }

        return Task.FromResult(Evaluate(vFreeBytes.Value, vFloorGb, vPath));
    }

    private static CheckResult Evaluate(long aFreeBytes, long aFloorGb, string aPath)
    {
        var vFreeGb = (double)aFreeBytes / BytesPerGb;
        var vFreeText = vFreeGb.ToString("0.0", CultureInfo.InvariantCulture);
        if (vFreeGb < aFloorGb)
        {
            return CheckResult.Warn($"Only {vFreeText} GB free on '{aPath}'; {aFloorGb} GB required.");
        }

        return CheckResult.Pass($"{vFreeText} GB free on '{aPath}' (floor {aFloorGb} GB).");
    }

    private static long? RealFreeBytes(string aPath)
    {
        try
        {
            var vRoot = Path.GetPathRoot(Path.GetFullPath(aPath));
            if (string.IsNullOrEmpty(vRoot))
            {
                return null;
            }

            return new DriveInfo(vRoot).AvailableFreeSpace;
        }
        catch (Exception vException) when (vException is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
