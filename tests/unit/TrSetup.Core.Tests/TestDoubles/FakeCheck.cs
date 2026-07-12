using TrSetup.Core.Checks;

namespace TrSetup.Core.Tests.TestDoubles;

/// <summary>
/// Configurable fake implementation of the <see cref="Check"/> contract used to drive
/// contract, pipeline and engine tests without touching the real machine.
/// </summary>
public sealed class FakeCheck : Check
{
    private readonly Func<CancellationToken, Task<CheckResult>> objDetect;
    private readonly Func<CancellationToken, Task<CheckResult>>? objVerify;

    /// <summary>
    /// Creates the fake.
    /// </summary>
    /// <param name="aId">Stable check id.</param>
    /// <param name="aRoles">Roles the check applies to.</param>
    /// <param name="aDetect">Detect behaviour; defaults to an immediate Pass.</param>
    /// <param name="aVerify">Verify behaviour; defaults to the detect behaviour (contract default).</param>
    /// <param name="aFix">Automated fix, or null for manual-only.</param>
    /// <param name="aApps">App profiles the check belongs to; empty = framework-level.</param>
    /// <param name="aCategory">Board category.</param>
    public FakeCheck(
        string aId,
        MachineRole aRoles,
        Func<CancellationToken, Task<CheckResult>>? aDetect = null,
        Func<CancellationToken, Task<CheckResult>>? aVerify = null,
        CheckFix? aFix = null,
        IReadOnlyCollection<string>? aApps = null,
        string aCategory = "Test group")
    {
        Id = aId;
        Roles = aRoles;
        objDetect = aDetect ?? (_ => Task.FromResult(CheckResult.Pass($"{aId} detected OK")));
        objVerify = aVerify;
        FixAsync = aFix;
        Apps = aApps ?? Array.Empty<string>();
        Category = aCategory;
    }

    /// <inheritdoc />
    public override string Id { get; }

    /// <inheritdoc />
    public override string Title => $"Fake check {Id}";

    /// <inheritdoc />
    public override string Category { get; }

    /// <inheritdoc />
    public override MachineRole Roles { get; }

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain =>
        new($"What {Id} is", $"Why {Id} matters", "https://example.test/docs");

    /// <inheritdoc />
    public override IReadOnlyCollection<string> Apps { get; }

    /// <inheritdoc />
    public override string? FixPreview => FixAsync is null ? null : $"fake-fix --install {Id}";

    /// <inheritdoc />
    public override CheckFix? FixAsync { get; }

    /// <summary>How many times <see cref="DetectAsync"/> has run (includes verify re-detects when no verify override).</summary>
    public int DetectCallCount { get; private set; }

    /// <inheritdoc />
    public override Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        DetectCallCount++;
        return objDetect(aCancellationToken);
    }

    /// <inheritdoc />
    public override Task<CheckResult> VerifyAsync(CancellationToken aCancellationToken = default)
        => objVerify is null ? base.VerifyAsync(aCancellationToken) : objVerify(aCancellationToken);
}
