namespace TrSetup.Core.Profiles;

/// <summary>
/// Thrown when a <c>trsetup-profile.json</c> fails schema validation (REQ-FN-021): a missing
/// required field, an unknown requirement <c>type</c>, an unparseable role, a duplicate id, or a
/// missing type-specific parameter. Loading NEVER silently skips an invalid requirement — the
/// whole load fails loudly with every error collected.
/// </summary>
public sealed class ProfileValidationException : Exception
{
    /// <summary>
    /// Creates the exception from a set of collected validation errors.
    /// </summary>
    /// <param name="aProfileName">The profile whose load failed (or the source path when the name is unknown).</param>
    /// <param name="aErrors">Every validation error found, in order.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aErrors"/> is null.</exception>
    public ProfileValidationException(string aProfileName, IReadOnlyList<string> aErrors)
        : base(BuildMessage(aProfileName, aErrors))
    {
        ProfileName = aProfileName;
        Errors = aErrors ?? throw new ArgumentNullException(nameof(aErrors));
    }

    /// <summary>The profile (or source path) whose load failed validation.</summary>
    public string ProfileName { get; }

    /// <summary>Every validation error found during the failed load.</summary>
    public IReadOnlyList<string> Errors { get; }

    private static string BuildMessage(string aProfileName, IReadOnlyList<string> aErrors)
    {
        var vErrors = aErrors ?? Array.Empty<string>();
        return $"Profile '{aProfileName}' failed validation with {vErrors.Count} error(s):" +
            Environment.NewLine + string.Join(Environment.NewLine, vErrors.Select(aError => "  - " + aError));
    }
}
