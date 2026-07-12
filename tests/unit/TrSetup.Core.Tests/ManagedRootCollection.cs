using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// xUnit collection that serializes every test class which mutates the process-global
/// <see cref="TrSetup.Core.Downloads.TrSetupPaths.RootOverride"/>, so parallel classes never
/// race on that shared static and see each other's managed root.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ManagedRootCollection
{
    /// <summary>The collection name applied to the participating test classes.</summary>
    public const string Name = "TrSetup managed root (serialized)";
}
