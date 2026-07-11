using TrSetup.Core.Fixing;

namespace TrSetup.Core.Checks;

/// <summary>
/// The automated fix of a check. Receives the granted <see cref="ConsentToken"/> (proof the
/// user saw the preview and approved) and returns the fixer's own raw output — the pipeline
/// still re-verifies before anything is called fixed.
/// </summary>
/// <param name="aConsent">The granted consent token issued after the fix preview was shown.</param>
/// <param name="aCancellationToken">Cancels the fix run.</param>
/// <returns>The fixer's self-reported outcome plus its full raw output.</returns>
public delegate Task<FixResult> CheckFix(ConsentToken aConsent, CancellationToken aCancellationToken);
