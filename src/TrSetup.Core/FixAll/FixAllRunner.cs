using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrSetup.Core.Fixing;

namespace TrSetup.Core.FixAll;

/// <summary>
/// The REQ-FN-019 fix-all runner: executes a dependency-ordered plan of failing checks.
/// Each step goes through the standard <see cref="FixPipeline"/> — consent gate first (the
/// user sees the step's FixPreview), then fix, then per-step re-verify. A declined consent
/// halts the whole run leaving every later step untouched; a failed re-verify either stops
/// the run or skips dependents and continues, per <see cref="FixAllFailurePolicy"/>.
/// Per-step status streams through the optional progress sink for the fix-run UI.
/// </summary>
public sealed class FixAllRunner
{
    private readonly FixPipeline objPipeline;
    private readonly ILogger<FixAllRunner> objLogger;

    /// <summary>
    /// Creates the runner around the standard consent → fix → re-verify pipeline.
    /// </summary>
    /// <param name="aPipeline">The pipeline every step runs through (owns the consent gate).</param>
    /// <param name="aLogger">Optional logger; a null logger is used when omitted.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aPipeline"/> is null.</exception>
    public FixAllRunner(FixPipeline aPipeline, ILogger<FixAllRunner>? aLogger = null)
    {
        objPipeline = aPipeline ?? throw new ArgumentNullException(nameof(aPipeline));
        objLogger = aLogger ?? NullLogger<FixAllRunner>.Instance;
    }

    /// <summary>
    /// Orders the plan topologically and runs it step by step.
    /// </summary>
    /// <param name="aSteps">The plan (any order; <see cref="FixAllPlanner.Order"/> is applied first).</param>
    /// <param name="aFailurePolicy">Whether a failed step stops the run or the run continues (skipping dependents).</param>
    /// <param name="aProgress">Optional live per-step status sink for the fix-run UI.</param>
    /// <param name="aCancellationToken">Cancels the run between steps and inside the current step.</param>
    /// <returns>Every step's outcome in execution order, plus whether/why the run halted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aSteps"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the plan's dependencies form a cycle.</exception>
    public async Task<FixAllRunResult> RunAsync(
        IReadOnlyList<FixStep> aSteps,
        FixAllFailurePolicy aFailurePolicy = FixAllFailurePolicy.StopOnFailure,
        IProgress<FixAllStepUpdate>? aProgress = null,
        CancellationToken aCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aSteps);

        var vOrdered = FixAllPlanner.Order(aSteps);
        var vResults = new List<FixAllStepResult>(vOrdered.Count);
        var vFailedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? vHaltReason = null;

        for (var vIndex = 0; vIndex < vOrdered.Count; vIndex++)
        {
            var vStep = vOrdered[vIndex];
            if (vHaltReason is not null)
            {
                Record(vResults, aProgress, vIndex, vOrdered.Count,
                    new FixAllStepResult(vStep.Id, FixAllStepStatus.Skipped, null, vHaltReason));
                continue;
            }

            var vResult = await RunStepAsync(vStep, vIndex, vOrdered.Count, vFailedIds, aProgress, aCancellationToken)
                .ConfigureAwait(false);
            vResults.Add(vResult);
            vHaltReason = DecideHalt(vResult, aFailurePolicy, vFailedIds);
        }

        return new FixAllRunResult(vResults, vHaltReason is not null, vHaltReason);
    }

    private async Task<FixAllStepResult> RunStepAsync(
        FixStep aStep,
        int aIndex,
        int aTotal,
        HashSet<string> aFailedIds,
        IProgress<FixAllStepUpdate>? aProgress,
        CancellationToken aCancellationToken)
    {
        var vFailedDependency = aStep.DependsOn.FirstOrDefault(aFailedIds.Contains);
        if (vFailedDependency is not null)
        {
            var vSkipped = new FixAllStepResult(
                aStep.Id, FixAllStepStatus.Skipped, null, $"dependency '{vFailedDependency}' failed");
            aProgress?.Report(new FixAllStepUpdate(aStep.Id, aIndex + 1, aTotal, FixAllStepPhase.Completed, vSkipped));
            return vSkipped;
        }

        aProgress?.Report(new FixAllStepUpdate(aStep.Id, aIndex + 1, aTotal, FixAllStepPhase.Starting, null));
        var vPipelineResult = await objPipeline.RunAsync(aStep.Check, aCancellationToken).ConfigureAwait(false);
        var vResult = new FixAllStepResult(
            aStep.Id, MapStatus(vPipelineResult.Status), vPipelineResult, DescribeOutcome(vPipelineResult.Status));
        aProgress?.Report(new FixAllStepUpdate(aStep.Id, aIndex + 1, aTotal, FixAllStepPhase.Completed, vResult));
        objLogger.LogInformation("Fix-all step {CheckId}: {Status}.", aStep.Id, vResult.Status);
        return vResult;
    }

    private static string? DecideHalt(
        FixAllStepResult aResult,
        FixAllFailurePolicy aFailurePolicy,
        HashSet<string> aFailedIds)
    {
        if (aResult.Status == FixAllStepStatus.Declined)
        {
            return $"run halted: consent declined for '{aResult.CheckId}'";
        }

        if (aResult.Status != FixAllStepStatus.Failed)
        {
            return null;
        }

        aFailedIds.Add(aResult.CheckId);
        return aFailurePolicy == FixAllFailurePolicy.StopOnFailure
            ? $"run stopped: fix for '{aResult.CheckId}' did not re-verify green"
            : null;
    }

    private static void Record(
        List<FixAllStepResult> aResults,
        IProgress<FixAllStepUpdate>? aProgress,
        int aIndex,
        int aTotal,
        FixAllStepResult aResult)
    {
        aResults.Add(aResult);
        aProgress?.Report(new FixAllStepUpdate(aResult.CheckId, aIndex + 1, aTotal, FixAllStepPhase.Completed, aResult));
    }

    private static FixAllStepStatus MapStatus(FixRunStatus aStatus) => aStatus switch
    {
        FixRunStatus.Fixed => FixAllStepStatus.Fixed,
        FixRunStatus.Declined => FixAllStepStatus.Declined,
        FixRunStatus.ManualOnly => FixAllStepStatus.ManualOnly,
        _ => FixAllStepStatus.Failed
    };

    private static string DescribeOutcome(FixRunStatus aStatus) => aStatus switch
    {
        FixRunStatus.Fixed => "fixed and re-verified green",
        FixRunStatus.Declined => "consent declined after preview; nothing executed",
        FixRunStatus.ManualOnly => "manual-only check; guidance shown, nothing executed",
        _ => "fix ran but did not re-verify green"
    };
}
