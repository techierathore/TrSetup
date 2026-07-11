namespace TrSetup.Core.Checks;

/// <summary>
/// The human explanation attached to a check: what the item is, why it matters,
/// and where the authoritative documentation lives.
/// </summary>
/// <param name="What">One or two sentences describing what the checked item is.</param>
/// <param name="Why">Why this machine's role needs the item (what breaks without it).</param>
/// <param name="DocLink">Link to the authoritative doc/guide section (e.g. WORKFLOW §0b), or <c>null</c> when none exists.</param>
public sealed record CheckExplanation(string What, string Why, string? DocLink = null);
