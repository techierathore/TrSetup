using TrSetup.Core.Profiles.Handlers;

namespace TrSetup.Core.Profiles;

/// <summary>
/// Maps a requirement <c>type</c> to the <see cref="IProfileRequirementHandler"/> that builds its
/// board check (REQ-FN-021). <see cref="CreateDefault"/> composes the presence-style handler set
/// (Cluster A); the heavy types are registered by Cluster C at the clearly-marked seam below.
/// </summary>
public sealed class ProfileRequirementHandlerRegistry
{
    private readonly Dictionary<string, IProfileRequirementHandler> objByType = new(StringComparer.Ordinal);

    /// <summary>Creates an empty registry. Prefer <see cref="CreateDefault"/> for the built-in handler set.</summary>
    public ProfileRequirementHandlerRegistry()
    {
    }

    /// <summary>The requirement types this registry can build checks for.</summary>
    public IReadOnlyCollection<string> RegisteredTypes => objByType.Keys.ToList();

    /// <summary>
    /// Composes the built-in handler set.
    /// </summary>
    /// <returns>A registry with the presence-style handlers (Cluster A) registered.</returns>
    public static ProfileRequirementHandlerRegistry CreateDefault()
    {
        var vRegistry = new ProfileRequirementHandlerRegistry();

        // Presence-style handlers (Cluster A):
        vRegistry.Register(new SdkRequirementHandler());
        vRegistry.Register(new WorkloadRequirementHandler());
        vRegistry.Register(new CliToolRequirementHandler());
        vRegistry.Register(new EndpointRequirementHandler());
        vRegistry.Register(new NugetFeedRequirementHandler());
        vRegistry.Register(new EnvSecretRequirementHandler());
        vRegistry.Register(new AppiumHeadRequirementHandler());

        // Cluster C (P3) heavy types (REQ-FN-025/026/029) — stateless handlers; collaborators arrive via CreateCheck's context:
        vRegistry.Register(new ServiceRequirementHandler());         // type: service
        vRegistry.Register(new RuntimeInstallRequirementHandler());  // type: runtime-install
        vRegistry.Register(new DiskSpaceRequirementHandler());       // type: disk-space

        return vRegistry;
    }

    /// <summary>
    /// Registers (or replaces) the handler for its <see cref="IProfileRequirementHandler.Type"/>.
    /// </summary>
    /// <param name="aHandler">The handler to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aHandler"/> is null.</exception>
    public void Register(IProfileRequirementHandler aHandler)
    {
        ArgumentNullException.ThrowIfNull(aHandler);
        objByType[aHandler.Type] = aHandler;
    }

    /// <summary>
    /// Looks up the handler for a requirement type.
    /// </summary>
    /// <param name="aType">The requirement type string.</param>
    /// <returns>The handler, or <c>null</c> when no handler is registered for that type.</returns>
    public IProfileRequirementHandler? Find(string aType)
        => objByType.TryGetValue(aType, out var vHandler) ? vHandler : null;
}
