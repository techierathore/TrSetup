using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrSetup.Core.Checks;

namespace TrSetup.Core.Engine;

/// <summary>
/// Owns the check catalog (REQ-FN-004): enumerates the checks applicable to
/// (machine roles ∩ selected app profile), builds the observable board model every head
/// renders, and runs detect sweeps in parallel with per-probe timeouts so a full sweep
/// stays fast (REQ-NFR-001 groundwork: parallel probes, 5 s default timeout, streaming rows).
/// </summary>
public sealed class CheckEngine
{
    /// <summary>Default per-probe timeout applied during detect sweeps (REQ-NFR-001).</summary>
    public static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly IReadOnlyList<Check> objCatalog;
    private readonly ILogger<CheckEngine> objLogger;

    /// <summary>
    /// Creates the engine over a check catalog.
    /// </summary>
    /// <param name="aCatalog">Every check the app knows about, in board order.</param>
    /// <param name="aLogger">Optional logger; a null logger is used when omitted.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aCatalog"/> is null.</exception>
    public CheckEngine(IEnumerable<Check> aCatalog, ILogger<CheckEngine>? aLogger = null)
    {
        ArgumentNullException.ThrowIfNull(aCatalog);
        objCatalog = aCatalog.ToList();
        objLogger = aLogger ?? NullLogger<CheckEngine>.Instance;
    }

    /// <summary>
    /// Enumerates only the checks in scope for the given roles and selected app
    /// (machine roles ∩ check roles, app-specific checks only when their app is selected).
    /// </summary>
    /// <param name="aRoles">The roles this machine holds.</param>
    /// <param name="aSelectedApp">The selected app profile, or <c>null</c> when none.</param>
    /// <returns>The in-scope checks in catalog order.</returns>
    public IReadOnlyList<Check> EnumerateChecks(MachineRole aRoles, string? aSelectedApp)
        => objCatalog.Where(aCheck => aCheck.AppliesTo(aRoles, aSelectedApp)).ToList();

    /// <summary>
    /// Builds the observable board for a (roles, app) scope: every catalog check becomes a
    /// row grouped by category; out-of-scope rows are <see cref="CheckStatus.NotApplicable"/>
    /// immediately and are never rendered as failures.
    /// </summary>
    /// <param name="aRoles">The roles this machine holds.</param>
    /// <param name="aSelectedApp">The selected app profile, or <c>null</c> when none.</param>
    /// <returns>The board model, not yet detected (in-scope rows have no status).</returns>
    public CheckBoard BuildBoard(MachineRole aRoles, string? aSelectedApp)
    {
        var vRows = objCatalog
            .Select(aCheck => new BoardRow(
                aCheck,
                aCheck.AppliesTo(aRoles, aSelectedApp),
                $"Out of scope for roles [{aRoles}]" +
                (aSelectedApp is null ? " with no app selected." : $" and app '{aSelectedApp}'.")))
            .ToList();

        var vGroups = vRows
            .GroupBy(aRow => aRow.Check.Category)
            .Select(aGroup => new BoardGroup(aGroup.Key, aGroup.ToList()))
            .ToList();

        return new CheckBoard(aRoles, aSelectedApp, vGroups);
    }

    /// <summary>
    /// Runs the detect sweep over every in-scope row of the board — all probes in parallel,
    /// each bounded by the probe timeout — streaming each row's result into the board
    /// (raising <see cref="CheckBoard.RowChanged"/>) the moment it lands.
    /// </summary>
    /// <param name="aBoard">The board to detect (built by <see cref="BuildBoard"/>).</param>
    /// <param name="aProbeTimeout">Per-probe timeout; defaults to <see cref="DefaultProbeTimeout"/> (5 s).</param>
    /// <param name="aProgress">Optional additional sink notified per completed row.</param>
    /// <param name="aCancellationToken">Cancels the whole sweep.</param>
    /// <returns>The same board instance, fully detected.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aBoard"/> is null.</exception>
    public async Task<CheckBoard> RunDetectSweepAsync(
        CheckBoard aBoard,
        TimeSpan? aProbeTimeout = null,
        IProgress<BoardRow>? aProgress = null,
        CancellationToken aCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aBoard);
        var vTimeout = aProbeTimeout ?? DefaultProbeTimeout;
        var vTasks = aBoard.Rows
            .Where(aRow => aRow.IsInScope)
            .Select(aRow => DetectRowAsync(aBoard, aRow, vTimeout, aProgress, aCancellationToken));

        await Task.WhenAll(vTasks).ConfigureAwait(false);
        return aBoard;
    }

    /// <summary>
    /// Re-detects a single row (single re-check &lt; 5 s — REQ-NFR-001) and streams the
    /// updated result into the board.
    /// </summary>
    /// <param name="aBoard">The board owning the row.</param>
    /// <param name="aRow">The row to re-check.</param>
    /// <param name="aProbeTimeout">Per-probe timeout; defaults to <see cref="DefaultProbeTimeout"/> (5 s).</param>
    /// <param name="aCancellationToken">Cancels the probe.</param>
    /// <returns>The row's new result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aBoard"/> or <paramref name="aRow"/> is null.</exception>
    public async Task<CheckResult> RecheckRowAsync(
        CheckBoard aBoard,
        BoardRow aRow,
        TimeSpan? aProbeTimeout = null,
        CancellationToken aCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aBoard);
        ArgumentNullException.ThrowIfNull(aRow);
        await DetectRowAsync(aBoard, aRow, aProbeTimeout ?? DefaultProbeTimeout, null, aCancellationToken)
            .ConfigureAwait(false);
        return new CheckResult(aRow.Status ?? CheckStatus.Fail, aRow.Evidence);
    }

    private async Task DetectRowAsync(
        CheckBoard aBoard,
        BoardRow aRow,
        TimeSpan aTimeout,
        IProgress<BoardRow>? aProgress,
        CancellationToken aCancellationToken)
    {
        var vResult = await ProbeWithTimeoutAsync(aRow.Check, aTimeout, aCancellationToken).ConfigureAwait(false);
        aBoard.ApplyResult(aRow, vResult);
        aProgress?.Report(aRow);
    }

    private async Task<CheckResult> ProbeWithTimeoutAsync(
        Check aCheck,
        TimeSpan aTimeout,
        CancellationToken aCancellationToken)
    {
        using var vTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(aCancellationToken);
        vTimeoutCts.CancelAfter(aTimeout);
        try
        {
            // WaitAsync hard-bounds the probe: even a check that ignores its CancellationToken
            // (network probe, stuck subprocess) settles as a timeout when the budget fires,
            // instead of leaving the row un-detected forever (REQ-UI-001 hang fix).
            return await aCheck.DetectAsync(vTimeoutCts.Token)
                .WaitAsync(vTimeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!aCancellationToken.IsCancellationRequested)
        {
            objLogger.LogWarning("Check {CheckId} probe timed out after {Timeout}.", aCheck.Id, aTimeout);
            return CheckResult.Fail($"Probe timed out after {aTimeout.TotalSeconds:0.#} s.");
        }
        catch (Exception vEx) when (vEx is not OperationCanceledException)
        {
            objLogger.LogError(vEx, "Check {CheckId} probe threw.", aCheck.Id);
            return CheckResult.Fail($"Probe threw {vEx.GetType().Name}: {vEx.Message}");
        }
    }
}
