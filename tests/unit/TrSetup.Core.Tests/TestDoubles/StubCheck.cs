using TrSetup.Core.Checks;

namespace TrSetup.Core.Tests.TestDoubles;

/// <summary>
/// A configurable board check for engine/catalog tests: id, role scope and detect behavior
/// are injected, everything else is a fixed minimal contract implementation.
/// </summary>
public sealed class StubCheck : Check
{
    private readonly string objId;
    private readonly MachineRole objRoles;
    private readonly Func<CancellationToken, Task<CheckResult>> objDetect;

    /// <summary>
    /// Creates the stub.
    /// </summary>
    /// <param name="aId">The stable id the test asserts on.</param>
    /// <param name="aDetect">The detect behavior (may pass, fail, throw, hang, or ignore the token).</param>
    /// <param name="aRoles">The role scope; defaults to <see cref="MachineRole.AgentHostWsl"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aId"/> or <paramref name="aDetect"/> is null.</exception>
    public StubCheck(
        string aId,
        Func<CancellationToken, Task<CheckResult>> aDetect,
        MachineRole aRoles = MachineRole.AgentHostWsl)
    {
        objId = aId ?? throw new ArgumentNullException(nameof(aId));
        objDetect = aDetect ?? throw new ArgumentNullException(nameof(aDetect));
        objRoles = aRoles;
    }

    /// <inheritdoc />
    public override string Id => objId;

    /// <inheritdoc />
    public override string Title => $"Stub check {objId}";

    /// <inheritdoc />
    public override string Category => "Test";

    /// <inheritdoc />
    public override MachineRole Roles => objRoles;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new("A test stub.", "Unit tests only.");

    /// <inheritdoc />
    public override Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
        => objDetect(aCancellationToken);

    /// <summary>
    /// Creates a stub whose detect ignores its <see cref="CancellationToken"/> entirely and
    /// never completes — the misbehaving-probe shape behind the REQ-UI-001 stuck-"Pending" hang.
    /// </summary>
    /// <param name="aId">The stable id the test asserts on.</param>
    /// <param name="aRoles">The role scope; defaults to <see cref="MachineRole.AgentHostWsl"/>.</param>
    /// <returns>The hanging stub.</returns>
    public static StubCheck Hanging(string aId, MachineRole aRoles = MachineRole.AgentHostWsl)
        => new(aId, _ => new TaskCompletionSource<CheckResult>().Task, aRoles);
}
