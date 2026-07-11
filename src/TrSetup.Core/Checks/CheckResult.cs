namespace TrSetup.Core.Checks;

/// <summary>
/// The result of running a check's detect (or verify) logic: a status plus the evidence
/// that produced it (command output, file contents, HTTP response, ...).
/// </summary>
/// <param name="Status">The detected status of the checked item.</param>
/// <param name="Evidence">
/// Human-readable evidence backing the status — the exact probe output shown in the
/// board's detail pane. Never empty for in-scope results.
/// </param>
public sealed record CheckResult(CheckStatus Status, string Evidence)
{
    /// <summary>
    /// Creates a <see cref="CheckStatus.Pass"/> result.
    /// </summary>
    /// <param name="aEvidence">The evidence backing the pass.</param>
    /// <returns>A pass result carrying the evidence.</returns>
    public static CheckResult Pass(string aEvidence) => new(CheckStatus.Pass, aEvidence);

    /// <summary>
    /// Creates a <see cref="CheckStatus.Warn"/> result.
    /// </summary>
    /// <param name="aEvidence">The evidence backing the warning.</param>
    /// <returns>A warn result carrying the evidence.</returns>
    public static CheckResult Warn(string aEvidence) => new(CheckStatus.Warn, aEvidence);

    /// <summary>
    /// Creates a <see cref="CheckStatus.Fail"/> result.
    /// </summary>
    /// <param name="aEvidence">The evidence backing the failure.</param>
    /// <returns>A fail result carrying the evidence.</returns>
    public static CheckResult Fail(string aEvidence) => new(CheckStatus.Fail, aEvidence);

    /// <summary>
    /// Creates a <see cref="CheckStatus.NotApplicable"/> result.
    /// </summary>
    /// <param name="aEvidence">Why the check is out of scope.</param>
    /// <returns>A not-applicable result carrying the reason.</returns>
    public static CheckResult NotApplicable(string aEvidence) => new(CheckStatus.NotApplicable, aEvidence);
}
