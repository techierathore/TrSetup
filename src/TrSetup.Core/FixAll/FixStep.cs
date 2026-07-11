using TrSetup.Core.Checks;

namespace TrSetup.Core.FixAll;

/// <summary>
/// One step of a fix-all plan (REQ-FN-019): a failing check to fix plus the ids of the
/// checks whose fixes must complete first (e.g. Node before Appium, Android SDK before AVD).
/// Cluster feeds (role fixers, app profiles) build plans out of these steps.
/// </summary>
public sealed class FixStep
{
    /// <summary>
    /// Creates the step.
    /// </summary>
    /// <param name="aCheck">The check this step fixes (via the standard consent → fix → re-verify pipeline).</param>
    /// <param name="aDependsOn">Check ids that must be fixed before this step, or null/empty when independent.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aCheck"/> is null.</exception>
    public FixStep(Check aCheck, IReadOnlyCollection<string>? aDependsOn = null)
    {
        Check = aCheck ?? throw new ArgumentNullException(nameof(aCheck));
        DependsOn = aDependsOn ?? Array.Empty<string>();
    }

    /// <summary>The check this step fixes.</summary>
    public Check Check { get; }

    /// <summary>Stable id of the step — the wrapped check's id.</summary>
    public string Id => Check.Id;

    /// <summary>Ids of the checks whose fixes must run before this one; ids not present in the plan are ignored.</summary>
    public IReadOnlyCollection<string> DependsOn { get; }
}
