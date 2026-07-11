namespace TrSetup.Core.Checks;

/// <summary>
/// How important a check is for the roles it applies to; drives ordering and how
/// prominently a failure is surfaced on the board.
/// </summary>
public enum CheckSeverity
{
    /// <summary>The environment does not work without this item.</summary>
    Required = 0,

    /// <summary>Strongly advised; some workflows degrade without it.</summary>
    Recommended = 1,

    /// <summary>Nice to have; absence never blocks a workflow.</summary>
    Optional = 2
}
