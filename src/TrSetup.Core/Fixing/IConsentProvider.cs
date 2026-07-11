using TrSetup.Core.Checks;

namespace TrSetup.Core.Fixing;

/// <summary>
/// The consent gate every fix must pass through. Implementations (GUI dialog, TUI prompt)
/// MUST display the check's <see cref="Check.FixPreview"/> — the literal commands / URLs —
/// before returning a granted token.
/// </summary>
public interface IConsentProvider
{
    /// <summary>
    /// Shows the check's fix preview to the user and asks for explicit approval.
    /// </summary>
    /// <param name="aCheck">The check whose fix is about to run; its <see cref="Check.FixPreview"/> is what must be displayed.</param>
    /// <param name="aCancellationToken">Cancels waiting for the user's decision.</param>
    /// <returns>A granted or declined <see cref="ConsentToken"/> recording the preview that was shown.</returns>
    Task<ConsentToken> RequestConsentAsync(Check aCheck, CancellationToken aCancellationToken = default);
}
