using TrSetup.Core.Checks;
using TrSetup.Core.FixAll;

namespace TrSetupUI.Services;

/// <summary>
/// The per-step view state the fix-run screen (REQ-UI-004) binds to: the check being fixed,
/// its 1-based position in the dependency-ordered plan, whether it is the currently active
/// step, and its streamed outcome once the step completes.
/// </summary>
public sealed class FixAllStepView
{
    /// <summary>
    /// Creates a pending step view for a planned check.
    /// </summary>
    /// <param name="aCheck">The check this step fixes.</param>
    /// <param name="aNumber">The 1-based position of the step in the ordered plan.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aCheck"/> is null.</exception>
    public FixAllStepView(Check aCheck, int aNumber)
    {
        Check = aCheck ?? throw new ArgumentNullException(nameof(aCheck));
        Number = aNumber;
    }

    /// <summary>The check this step fixes.</summary>
    public Check Check { get; }

    /// <summary>The 1-based position of this step in the dependency-ordered plan.</summary>
    public int Number { get; }

    /// <summary>Whether this step is the one currently executing (consent gate / live output).</summary>
    public bool IsActive { get; set; }

    /// <summary>The step's outcome once it completes, or <c>null</c> while it is pending/active.</summary>
    public FixAllStepStatus? Status { get; set; }

    /// <summary>The full step result (raw output, reason) once it completes, or <c>null</c>.</summary>
    public FixAllStepResult? Result { get; set; }
}
