namespace TrSetup.Core.Fixing;

/// <summary>
/// Proof that the user saw a check's fix preview and explicitly approved (or declined) the fix.
/// A fixer only ever runs when handed a granted token — there is no code path that fixes
/// without preview + consent.
/// </summary>
public sealed class ConsentToken
{
    private ConsentToken(bool aIsGranted, string aPreviewShown)
    {
        IsGranted = aIsGranted;
        PreviewShown = aPreviewShown;
        IssuedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Whether the user approved the fix after seeing the preview.</summary>
    public bool IsGranted { get; }

    /// <summary>The exact fix preview (literal commands / URLs) that was shown to the user.</summary>
    public string PreviewShown { get; }

    /// <summary>When the consent decision was made (UTC).</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>
    /// Creates a granted token recording the preview the user approved.
    /// </summary>
    /// <param name="aPreviewShown">The literal fix preview that was displayed before approval.</param>
    /// <returns>A granted consent token.</returns>
    public static ConsentToken Granted(string aPreviewShown) => new(true, aPreviewShown);

    /// <summary>
    /// Creates a declined token recording the preview the user rejected.
    /// </summary>
    /// <param name="aPreviewShown">The literal fix preview that was displayed before the decline.</param>
    /// <returns>A declined consent token.</returns>
    public static ConsentToken Declined(string aPreviewShown) => new(false, aPreviewShown);
}
