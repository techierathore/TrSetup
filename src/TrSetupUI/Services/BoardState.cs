using TrSetup.Core.Checks;
using TrSetup.Core.Engine;
using TrSetup.Core.FixAll;
using TrSetup.Core.Fixing;
using TrSetup.Core.Settings;

namespace TrSetupUI.Services;

/// <summary>One completed fix-pipeline run kept for the check-detail "Last run output" pane.</summary>
/// <param name="Command">The literal command(s) the fix previewed (the check's FixPreview).</param>
/// <param name="Result">The pipeline outcome including the raw fixer output and re-verify result.</param>
/// <param name="CompletedAt">When the run finished (UTC).</param>
public sealed record BoardFixRun(string Command, FixRunResult Result, DateTimeOffset CompletedAt);

/// <summary>
/// Per-circuit UI state for the whole shell (REQ-UI-001..005): owns the live
/// <see cref="CheckBoard"/> for the current (roles, app) scope, streams detect sweeps into it,
/// runs single fixes through the consent-gated <see cref="FixPipeline"/>, and persists
/// role-picker choices via <see cref="ISettingsStore"/>. Created by the layout and cascaded
/// to every page; pages re-render on <see cref="Changed"/>.
/// </summary>
public sealed class BoardState
{
    private readonly CheckEngine objEngine;
    private readonly ISettingsStore objSettingsStore;
    private readonly TrSetupSettings objSettings;
    private readonly FixPipeline objFixPipeline;
    private readonly FixAllRunner objFixAllRunner;
    private readonly HashSet<string> objCheckingIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> objFixingIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BoardFixRun> objLastRuns = new(StringComparer.OrdinalIgnoreCase);
    private List<Check> objFixAllQueue = new();
    private List<FixAllStepView> objFixAllSteps = new();
    private CancellationTokenSource? objSweepCts;
    private CancellationTokenSource? objFixAllCts;
    private bool objIsInitialized;
    private bool objIsStopRequested;

    /// <summary>
    /// Creates the state over the DI-registered engine, settings store and loaded settings.
    /// </summary>
    /// <param name="aEngine">The check engine that builds boards and runs detect sweeps.</param>
    /// <param name="aSettingsStore">The JSON settings store used to persist role/app choices.</param>
    /// <param name="aLoadResult">The settings loaded at startup, including the first-run flag.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public BoardState(CheckEngine aEngine, ISettingsStore aSettingsStore, SettingsLoadResult aLoadResult)
    {
        objEngine = aEngine ?? throw new ArgumentNullException(nameof(aEngine));
        objSettingsStore = aSettingsStore ?? throw new ArgumentNullException(nameof(aSettingsStore));
        ArgumentNullException.ThrowIfNull(aLoadResult);
        objSettings = aLoadResult.Settings;
        IsFirstRun = aLoadResult.IsFirstRun;
        Consent = new UiConsentProvider();
        Consent.Changed += () => Changed?.Invoke();
        objFixPipeline = new FixPipeline(Consent);
        objFixAllRunner = new FixAllRunner(objFixPipeline);
    }

    /// <summary>Raised whenever any board/UI state changes; pages marshal to the renderer.</summary>
    public event Action? Changed;

    /// <summary>The consent gate the shell renders as a modal fix-preview dialog.</summary>
    public UiConsentProvider Consent { get; }

    /// <summary>Whether no settings file existed yet (drives the first-run redirect to /setup).</summary>
    public bool IsFirstRun { get; private set; }

    /// <summary>
    /// Whether the board still needs the role picker: no roles are configured yet. Unlike
    /// <see cref="IsFirstRun"/> (a startup-time snapshot on the shared settings singleton), this
    /// reflects the live settings, so a refreshed circuit or new tab after Setup builds the board
    /// instead of bouncing back to <c>/setup</c>.
    /// </summary>
    public bool NeedsSetup => objSettings.Roles == MachineRole.None;

    /// <summary>The current board, or <c>null</c> before the first scope is built.</summary>
    public CheckBoard? Board { get; private set; }

    /// <summary>The roles the board is currently scoped to.</summary>
    public MachineRole Roles => objSettings.Roles;

    /// <summary>The app profile the board is currently scoped to, or <c>null</c> for framework-only.</summary>
    public string? SelectedApp => objSettings.SelectedApp;

    /// <summary>
    /// The configured named endpoints the board scopes to (e.g. <c>MacIp</c> → the LAN Mac IP the
    /// Bridges checks probe). Read-only view so the Settings editor (REQ-UI-006) can pre-fill its
    /// endpoint inputs; edits persist through <see cref="SaveSettingsAsync"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Endpoints => objSettings.Endpoints;

    /// <summary>
    /// The endpoint keys whose self-signed TLS certificate the user has explicitly opted to trust
    /// (REQ-FN-028). Read-only view so the Settings editor can pre-tick its per-endpoint trust
    /// checkboxes; edits persist through <see cref="SaveSettingsAsync"/>.
    /// </summary>
    public IReadOnlyCollection<string> TrustedSelfSignedEndpoints => objSettings.TrustedSelfSignedEndpoints;

    /// <summary>Whether a full detect sweep is currently running.</summary>
    public bool IsSweeping { get; private set; }

    /// <summary>The engine/enumeration error of the last sweep, or <c>null</c> when it succeeded.</summary>
    public string? SweepError { get; private set; }

    /// <summary>Whether the given check is currently being re-checked (single-row spinner).</summary>
    /// <param name="aCheckId">The check id.</param>
    /// <returns><c>true</c> while the row's probe is in flight.</returns>
    public bool IsChecking(string aCheckId)
    {
        lock (objCheckingIds)
        {
            return objCheckingIds.Contains(aCheckId);
        }
    }

    /// <summary>Whether the given check's fix is currently running (Fix buttons disable).</summary>
    /// <param name="aCheckId">The check id.</param>
    /// <returns><c>true</c> while the fix pipeline runs for the check.</returns>
    public bool IsFixing(string aCheckId)
    {
        lock (objFixingIds)
        {
            return objFixingIds.Contains(aCheckId);
        }
    }

    /// <summary>Whether any fix is currently running anywhere on the board.</summary>
    public bool IsAnyFixRunning
    {
        get
        {
            lock (objFixingIds)
            {
                return objFixingIds.Count > 0;
            }
        }
    }

    /// <summary>
    /// The last fix-pipeline run recorded for a check, or <c>null</c> when it never ran.
    /// </summary>
    /// <param name="aCheckId">The check id.</param>
    /// <returns>The recorded run, or <c>null</c>.</returns>
    public BoardFixRun? LastRunFor(string aCheckId)
    {
        lock (objLastRuns)
        {
            return objLastRuns.TryGetValue(aCheckId, out var vRun) ? vRun : null;
        }
    }

    /// <summary>
    /// Builds the first board and starts the initial detect sweep once per circuit.
    /// Safe to call from every page; does nothing on later calls or before roles exist.
    /// </summary>
    /// <returns>A task completing when the sweep has been started (not finished).</returns>
    public Task EnsureInitializedAsync()
    {
        if (objIsInitialized || NeedsSetup)
        {
            return Task.CompletedTask;
        }

        objIsInitialized = true;
        return RescopeAsync(objSettings.Roles, objSettings.SelectedApp, aPersist: false);
    }

    /// <summary>
    /// Re-scopes the board to new roles / app (header selectors, REQ-UI-001): rebuilds the
    /// board without a page reload, optionally persists the choice, and streams a fresh
    /// detect sweep into the rows.
    /// </summary>
    /// <param name="aRoles">The machine roles to scope to.</param>
    /// <param name="aSelectedApp">The app profile, or <c>null</c> for framework-only.</param>
    /// <param name="aPersist">Whether to save the new scope to the settings file.</param>
    /// <returns>A task completing when the sweep has been started.</returns>
    public async Task RescopeAsync(MachineRole aRoles, string? aSelectedApp, bool aPersist = true)
    {
        objIsInitialized = true;
        objSettings.Roles = aRoles;
        objSettings.SelectedApp = aSelectedApp;
        if (aPersist)
        {
            await TrySaveSettingsAsync().ConfigureAwait(false);
        }

        StartSweep();
    }

    /// <summary>
    /// Persists the role-picker choices (REQ-UI-003 Save), clears the first-run flag and
    /// starts the board sweep so the user lands on a live board.
    /// </summary>
    /// <param name="aRoles">The chosen machine roles (including the native-dev variant flag).</param>
    /// <param name="aSelectedApp">The chosen default app, or <c>null</c> for framework-only.</param>
    /// <returns>A task completing when settings are saved and the sweep started.</returns>
    /// <exception cref="IOException">Propagated when the settings file cannot be written.</exception>
    public async Task SaveSetupAsync(MachineRole aRoles, string? aSelectedApp)
    {
        objSettings.Roles = aRoles;
        objSettings.SelectedApp = aSelectedApp;
        await objSettingsStore.SaveAsync(objSettings).ConfigureAwait(false);
        IsFirstRun = false;
        objIsInitialized = true;
        StartSweep();
    }

    /// <summary>
    /// Persists the full Settings surface (REQ-UI-006 Save): roles, selected app AND named endpoints
    /// together, then re-scopes the board WITHOUT a page reload so the next detect sweep probes the
    /// new endpoint values (e.g. the Bridges checks target the updated Mac IP). The settings singleton
    /// is the same instance the checks read live, so the mutation is seen immediately by the sweep.
    /// </summary>
    /// <param name="aRoles">The chosen machine roles (including the native-dev variant flag).</param>
    /// <param name="aSelectedApp">The chosen app profile, or <c>null</c> for framework-only.</param>
    /// <param name="aEndpoints">The endpoint values to persist by name (empty entries should be omitted by the caller).</param>
    /// <param name="aTrustedSelfSignedEndpoints">
    /// The endpoint keys the user explicitly opted to trust an untrusted TLS certificate for
    /// (REQ-FN-028), or <c>null</c> to leave the stored set untouched.
    /// </param>
    /// <returns>A task completing when settings are saved and the re-scope sweep started.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aEndpoints"/> is null.</exception>
    /// <exception cref="IOException">Propagated when the settings file cannot be written.</exception>
    public async Task SaveSettingsAsync(
        MachineRole aRoles,
        string? aSelectedApp,
        IReadOnlyDictionary<string, string> aEndpoints,
        IReadOnlyCollection<string>? aTrustedSelfSignedEndpoints = null)
    {
        ArgumentNullException.ThrowIfNull(aEndpoints);
        objSettings.Roles = aRoles;
        objSettings.SelectedApp = aSelectedApp;
        objSettings.Endpoints = new Dictionary<string, string>(aEndpoints, StringComparer.OrdinalIgnoreCase);
        if (aTrustedSelfSignedEndpoints is not null)
        {
            objSettings.TrustedSelfSignedEndpoints =
                new HashSet<string>(aTrustedSelfSignedEndpoints, StringComparer.OrdinalIgnoreCase);
        }
        await objSettingsStore.SaveAsync(objSettings).ConfigureAwait(false);
        IsFirstRun = false;
        objIsInitialized = true;
        StartSweep();
    }

    /// <summary>
    /// Re-checks the whole board (header "Re-check all"): rebuilds rows to their streaming
    /// skeleton state and runs a fresh parallel detect sweep.
    /// </summary>
    public void RecheckAll() => StartSweep();

    /// <summary>
    /// Re-checks a single row (&lt; 5 s, REQ-NFR-001) and streams the result into the board.
    /// </summary>
    /// <param name="aRow">The row to re-check.</param>
    /// <returns>A task completing when the probe result landed.</returns>
    public async Task RecheckRowAsync(BoardRow aRow)
    {
        ArgumentNullException.ThrowIfNull(aRow);
        if (Board is null)
        {
            return;
        }

        lock (objCheckingIds)
        {
            if (!objCheckingIds.Add(aRow.Check.Id))
            {
                return;
            }
        }

        Changed?.Invoke();
        try
        {
            await objEngine.RecheckRowAsync(Board, aRow).ConfigureAwait(false);
        }
        finally
        {
            lock (objCheckingIds)
            {
                objCheckingIds.Remove(aRow.Check.Id);
            }

            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Runs the consent-gated Detect → Preview → Fix → Re-verify pipeline for one row and
    /// refreshes the row afterwards. Records the run for the detail pane's last-run output.
    /// </summary>
    /// <param name="aRow">The failing row to fix.</param>
    /// <returns>The pipeline outcome (Fixed / Failed / Declined / ManualOnly).</returns>
    public async Task<FixRunResult> FixRowAsync(BoardRow aRow)
    {
        ArgumentNullException.ThrowIfNull(aRow);
        lock (objFixingIds)
        {
            objFixingIds.Add(aRow.Check.Id);
        }

        Changed?.Invoke();
        try
        {
            var vResult = await objFixPipeline.RunAsync(aRow.Check).ConfigureAwait(false);
            if (vResult.Status is FixRunStatus.Fixed or FixRunStatus.Failed)
            {
                lock (objLastRuns)
                {
                    objLastRuns[aRow.Check.Id] =
                        new BoardFixRun(aRow.Check.FixPreview ?? string.Empty, vResult, DateTimeOffset.UtcNow);
                }

                await RecheckRowAsync(aRow).ConfigureAwait(false);
            }

            return vResult;
        }
        finally
        {
            lock (objFixingIds)
            {
                objFixingIds.Remove(aRow.Check.Id);
            }

            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Finds a board row by check id (deep links, /check/{id}).
    /// </summary>
    /// <param name="aCheckId">The check id from the route.</param>
    /// <returns>The row, or <c>null</c> when the id is unknown in the current scope.</returns>
    public BoardRow? FindRow(string? aCheckId)
        => aCheckId is null
            ? null
            : Board?.Rows.FirstOrDefault(
                aRow => string.Equals(aRow.Check.Id, aCheckId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a fix-all run is currently executing (drives the run view + consent routing).</summary>
    public bool IsFixAllRunning { get; private set; }

    /// <summary>The completed fix-all run outcome, or <c>null</c> while running or before any run.</summary>
    public FixAllRunResult? FixAllResult { get; private set; }

    /// <summary>The ordered per-step view models the fix-run screen renders (REQ-UI-004).</summary>
    public IReadOnlyList<FixAllStepView> FixAllSteps => objFixAllSteps;

    /// <summary>The 1-based number of the step currently executing, or 0 before the run starts.</summary>
    public int FixAllCurrentStep { get; private set; }

    /// <summary>The total number of steps in the current fix-all plan.</summary>
    public int FixAllTotalSteps => objFixAllSteps.Count;

    /// <summary>Whether a stop has been requested for the running fix-all (halts after the current step).</summary>
    public bool IsStopRequested => objIsStopRequested;

    /// <summary>Whether there is a queued fix-all plan waiting to start (set by a group "Fix all").</summary>
    public bool HasPendingFixQueue => objFixAllQueue.Count > 0;

    /// <summary>
    /// The rows a "Fix all" would queue for a group (or the whole board when null): in-scope,
    /// failing or warning, and NOT manual-only (manual rows only offer guidance).
    /// </summary>
    /// <param name="aGroup">The group to scope to, or <c>null</c> for the whole board.</param>
    /// <returns>The fixable non-passing rows, in board order.</returns>
    public IReadOnlyList<BoardRow> FixableRows(BoardGroup? aGroup)
    {
        var vRows = aGroup?.Rows ?? Board?.Rows.ToList() ?? new List<BoardRow>();
        return vRows
            .Where(aRow => aRow.IsInScope
                && aRow.Status is CheckStatus.Fail or CheckStatus.Warn
                && !aRow.Check.IsManualOnly)
            .ToList();
    }

    /// <summary>
    /// Queues a group's (or the whole board's) fixable rows for the next fix-all run and clears
    /// any prior run state, so navigating to <c>/fix-run</c> starts a fresh run (REQ-UI-004).
    /// </summary>
    /// <param name="aGroup">The group to queue, or <c>null</c> for every fixable row on the board.</param>
    public void QueueFixAll(BoardGroup? aGroup)
    {
        objFixAllQueue = FixableRows(aGroup).Select(aRow => aRow.Check).ToList();
        objFixAllSteps = new List<FixAllStepView>();
        FixAllResult = null;
        FixAllCurrentStep = 0;
        objIsStopRequested = false;
        Changed?.Invoke();
    }

    /// <summary>
    /// Runs the queued fix-all plan (REQ-UI-004): orders it by dependency, then drives each step
    /// through the consent-gated pipeline via <see cref="FixAllRunner"/>, streaming per-step
    /// status into <see cref="FixAllSteps"/>. Re-checks fixed rows on the board when done.
    /// </summary>
    /// <returns>A task completing when the run finishes (or halts).</returns>
    public async Task RunFixAllAsync()
    {
        if (IsFixAllRunning)
        {
            return;
        }

        var vOrdered = BuildOrderedPlan();
        objFixAllSteps = vOrdered.Select((aStep, aIndex) => new FixAllStepView(aStep.Check, aIndex + 1)).ToList();
        if (objFixAllSteps.Count == 0)
        {
            FixAllResult = new FixAllRunResult(Array.Empty<FixAllStepResult>(), false, null);
            Changed?.Invoke();
            return;
        }

        objIsStopRequested = false;
        FixAllResult = null;
        FixAllCurrentStep = 0;
        IsFixAllRunning = true;
        objFixAllCts = new CancellationTokenSource();
        Changed?.Invoke();
        try
        {
            FixAllResult = await objFixAllRunner
                .RunAsync(vOrdered, FixAllFailurePolicy.StopOnFailure, new FixAllProgress(OnFixAllUpdate), objFixAllCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stop requested — the partial step results already streamed are the record.
        }
        finally
        {
            IsFixAllRunning = false;
            objFixAllCts?.Dispose();
            objFixAllCts = null;
            Changed?.Invoke();
            await RefreshFixedRowsAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Requests the running fix-all stop: declines the pending step and halts before the next.
    /// </summary>
    public void RequestStopFixAll()
    {
        objIsStopRequested = true;
        Consent.Decline();
        objFixAllCts?.Cancel();
        Changed?.Invoke();
    }

    private IReadOnlyList<FixStep> BuildOrderedPlan()
    {
        var vSteps = objFixAllQueue.Select(aCheck => new FixStep(aCheck)).ToList();
        try
        {
            return FixAllPlanner.Order(vSteps);
        }
        catch (InvalidOperationException)
        {
            return vSteps;
        }
    }

    private void OnFixAllUpdate(FixAllStepUpdate aUpdate)
    {
        FixAllCurrentStep = aUpdate.StepNumber;
        var vView = objFixAllSteps.FirstOrDefault(
            aStep => string.Equals(aStep.Check.Id, aUpdate.CheckId, StringComparison.OrdinalIgnoreCase));
        if (vView is not null)
        {
            if (aUpdate.Phase == FixAllStepPhase.Starting)
            {
                vView.IsActive = true;
            }
            else
            {
                vView.IsActive = false;
                vView.Status = aUpdate.Result?.Status;
                vView.Result = aUpdate.Result;
            }
        }

        Changed?.Invoke();
    }

    private async Task RefreshFixedRowsAsync()
    {
        if (Board is null)
        {
            return;
        }

        foreach (var vStep in objFixAllSteps.Where(aStep => aStep.Status == FixAllStepStatus.Fixed))
        {
            var vRow = FindRow(vStep.Check.Id);
            if (vRow is not null)
            {
                await RecheckRowAsync(vRow).ConfigureAwait(false);
            }
        }
    }

    private void StartSweep()
    {
        objSweepCts?.Cancel();
        var vCts = new CancellationTokenSource();
        objSweepCts = vCts;
        SweepError = null;

        try
        {
            var vBoard = objEngine.BuildBoard(objSettings.Roles, objSettings.SelectedApp);
            vBoard.RowChanged += (aSender, aArgs) => Changed?.Invoke();
            Board = vBoard;
        }
        catch (Exception vEx)
        {
            SweepError = vEx.Message;
            IsSweeping = false;
            Changed?.Invoke();
            return;
        }

        IsSweeping = true;
        Changed?.Invoke();
        _ = RunSweepAsync(Board, vCts);
    }

    private async Task RunSweepAsync(CheckBoard aBoard, CancellationTokenSource aCts)
    {
        try
        {
            await objEngine.RunDetectSweepAsync(aBoard, aCancellationToken: aCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer scope — nothing to report.
        }
        catch (Exception vEx)
        {
            if (ReferenceEquals(Board, aBoard))
            {
                SweepError = vEx.Message;
            }
        }
        finally
        {
            if (ReferenceEquals(Board, aBoard))
            {
                IsSweeping = false;
            }

            Changed?.Invoke();
        }
    }

    private async Task TrySaveSettingsAsync()
    {
        try
        {
            await objSettingsStore.SaveAsync(objSettings).ConfigureAwait(false);
        }
        catch (Exception vEx)
        {
            SweepError = $"Could not save settings: {vEx.Message}";
        }
    }

    /// <summary>Synchronous <see cref="IProgress{T}"/> so fix-all step updates apply immediately.</summary>
    private sealed class FixAllProgress : IProgress<FixAllStepUpdate>
    {
        private readonly Action<FixAllStepUpdate> objOnReport;

        /// <summary>Creates the progress sink around a callback.</summary>
        /// <param name="aOnReport">The callback invoked for each streamed update.</param>
        public FixAllProgress(Action<FixAllStepUpdate> aOnReport) => objOnReport = aOnReport;

        /// <inheritdoc />
        public void Report(FixAllStepUpdate aValue) => objOnReport(aValue);
    }
}
