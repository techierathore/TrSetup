using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles;

/// <summary>
/// Parses the human-readable role-name strings a profile JSON uses (e.g. <c>"AppRunnerMac"</c>,
/// <c>"DeviceHostWindows"</c>) into combined <see cref="MachineRole"/> flags (BRD-35). Kept
/// separate so the JSON stays readable role names instead of raw flag integers.
/// </summary>
internal static class ProfileRoleParser
{
    /// <summary>
    /// Parses a list of role-name strings into combined flags, collecting any unknown names.
    /// </summary>
    /// <param name="aRoleNames">The role-name strings from the profile JSON.</param>
    /// <param name="aErrors">Sink the caller appends "unknown role" messages to.</param>
    /// <param name="aContext">Human context (the requirement id) prefixed to any error.</param>
    /// <returns>The OR-combined roles; <see cref="MachineRole.None"/> when the list is empty.</returns>
    public static MachineRole Parse(IReadOnlyList<string> aRoleNames, IList<string> aErrors, string aContext)
    {
        var vRoles = MachineRole.None;
        if (aRoleNames.Count == 0)
        {
            aErrors.Add($"{aContext}: at least one role is required (BRD-35 — every requirement is role-tagged).");
            return vRoles;
        }

        foreach (var vName in aRoleNames)
        {
            if (Enum.TryParse<MachineRole>(vName?.Trim(), ignoreCase: true, out var vRole) &&
                vRole != MachineRole.None && Enum.IsDefined(vRole))
            {
                vRoles |= vRole;
            }
            else
            {
                aErrors.Add($"{aContext}: unknown role '{vName}' (expected one of {string.Join(", ", KnownRoleNames())}).");
            }
        }

        return vRoles;
    }

    private static IEnumerable<string> KnownRoleNames()
        => Enum.GetValues<MachineRole>().Where(aRole => aRole != MachineRole.None).Select(aRole => aRole.ToString());
}
