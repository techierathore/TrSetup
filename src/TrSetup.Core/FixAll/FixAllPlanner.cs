namespace TrSetup.Core.FixAll;

/// <summary>
/// Orders a fix-all plan by declared dependencies (REQ-FN-019): a stable topological sort
/// so every step runs after the steps it depends on (Node before Appium, SDK before AVD),
/// with ties broken by the caller's input order.
/// </summary>
public static class FixAllPlanner
{
    /// <summary>
    /// Topologically sorts the steps by their <see cref="FixStep.DependsOn"/> declarations.
    /// Dependencies on ids not present in the plan are ignored (they are assumed already
    /// green or out of scope). The sort is stable: independent steps keep their input order.
    /// </summary>
    /// <param name="aSteps">The unordered plan.</param>
    /// <returns>The steps in a valid dependency execution order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aSteps"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when two steps share the same check id.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the dependencies form a cycle.</exception>
    public static IReadOnlyList<FixStep> Order(IReadOnlyList<FixStep> aSteps)
    {
        ArgumentNullException.ThrowIfNull(aSteps);

        var vRemainingDependencies = BuildDependencyMap(aSteps);
        var vOrdered = new List<FixStep>(aSteps.Count);
        var vPlaced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (vOrdered.Count < aSteps.Count)
        {
            var vReady = aSteps.FirstOrDefault(aStep =>
                !vPlaced.Contains(aStep.Id) &&
                vRemainingDependencies[aStep.Id].All(vPlaced.Contains));
            if (vReady is null)
            {
                var vStuck = aSteps.Where(aStep => !vPlaced.Contains(aStep.Id)).Select(aStep => aStep.Id);
                throw new InvalidOperationException(
                    $"Fix-all plan has a dependency cycle among: {string.Join(", ", vStuck)}");
            }

            vOrdered.Add(vReady);
            vPlaced.Add(vReady.Id);
        }

        return vOrdered;
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildDependencyMap(IReadOnlyList<FixStep> aSteps)
    {
        var vKnownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var vStep in aSteps)
        {
            if (!vKnownIds.Add(vStep.Id))
            {
                throw new ArgumentException($"Duplicate check id in fix-all plan: {vStep.Id}", nameof(aSteps));
            }
        }

        return aSteps.ToDictionary(
            aStep => aStep.Id,
            aStep => (IReadOnlyList<string>)aStep.DependsOn.Where(vKnownIds.Contains).ToList(),
            StringComparer.OrdinalIgnoreCase);
    }
}
