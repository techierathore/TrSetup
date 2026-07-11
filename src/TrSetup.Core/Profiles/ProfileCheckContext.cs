using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;
using TrSetup.Core.Settings;

namespace TrSetup.Core.Profiles;

/// <summary>
/// Everything a <see cref="IProfileRequirementHandler"/> needs to construct a real board
/// <see cref="Checks.Check"/> from a <see cref="ProfileRequirement"/> (REQ-FN-021). Threads the
/// exact same collaborators the built-in catalog threads through
/// <see cref="Catalog.CheckCatalog.CreateAllChecks"/> — the process runner choke-point, the P2
/// fixer bundle, the HTTP and system probes, and the live settings accessor.
/// </summary>
public sealed class ProfileCheckContext
{
    /// <summary>
    /// Creates the context.
    /// </summary>
    /// <param name="aProfileName">The owning profile name — the app the built checks scope to (drives <see cref="Checks.Check.Apps"/>).</param>
    /// <param name="aProcessRunner">The single process choke-point detect/fix shells through (REQ-FN-003).</param>
    /// <param name="aFixServices">The P2 fixer bundle (download / config-write / elevation).</param>
    /// <param name="aHttpProbe">The HTTP GET reachability probe (endpoint / appium-head / nuget-feed detect).</param>
    /// <param name="aSystemProbe">The read-only local filesystem/environment probe (env-secret presence).</param>
    /// <param name="aSettingsAccessor">Live accessor for the current settings (endpoints, selected app).</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
    public ProfileCheckContext(
        string aProfileName,
        IProcessRunner aProcessRunner,
        CheckFixServices aFixServices,
        IHttpStatusProbe aHttpProbe,
        ISystemProbe aSystemProbe,
        Func<TrSetupSettings> aSettingsAccessor)
    {
        ProfileName = aProfileName ?? throw new ArgumentNullException(nameof(aProfileName));
        ProcessRunner = aProcessRunner ?? throw new ArgumentNullException(nameof(aProcessRunner));
        FixServices = aFixServices ?? throw new ArgumentNullException(nameof(aFixServices));
        HttpProbe = aHttpProbe ?? throw new ArgumentNullException(nameof(aHttpProbe));
        SystemProbe = aSystemProbe ?? throw new ArgumentNullException(nameof(aSystemProbe));
        SettingsAccessor = aSettingsAccessor ?? throw new ArgumentNullException(nameof(aSettingsAccessor));
    }

    /// <summary>The owning profile name — the app the built checks scope to.</summary>
    public string ProfileName { get; }

    /// <summary>The process choke-point checks probe and fix through.</summary>
    public IProcessRunner ProcessRunner { get; }

    /// <summary>The P2 fixer frameworks bundle (download / config-write / elevation).</summary>
    public CheckFixServices FixServices { get; }

    /// <summary>The HTTP GET reachability probe.</summary>
    public IHttpStatusProbe HttpProbe { get; }

    /// <summary>The read-only local filesystem/environment probe.</summary>
    public ISystemProbe SystemProbe { get; }

    /// <summary>Live accessor for the current settings.</summary>
    public Func<TrSetupSettings> SettingsAccessor { get; }
}
