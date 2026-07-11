using System.Text.Json;
using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles;

/// <summary>
/// Parses and schema-validates a <c>trsetup-profile.json</c> document (REQ-FN-021). Every
/// structural problem — missing <c>name</c>/<c>id</c>/<c>title</c>, unknown <c>type</c>,
/// unparseable role, duplicate id within the document, missing type-specific param — is collected
/// and thrown as one <see cref="ProfileValidationException"/>; a malformed document never yields a
/// partially-loaded profile.
/// </summary>
internal static class ProfileJsonReader
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Parses one profile document.
    /// </summary>
    /// <param name="aJson">The raw JSON text.</param>
    /// <param name="aSource">Human source label (path or profile name) used in error messages.</param>
    /// <returns>The validated profile.</returns>
    /// <exception cref="ProfileValidationException">Thrown when the document is malformed or fails validation.</exception>
    public static TrSetupProfile Read(string aJson, string aSource)
    {
        ProfileDocument? vDocument;
        try
        {
            vDocument = JsonSerializer.Deserialize<ProfileDocument>(aJson, ReadOptions);
        }
        catch (JsonException vEx)
        {
            throw new ProfileValidationException(aSource, new[] { $"malformed JSON: {vEx.Message}" });
        }

        if (vDocument is null)
        {
            throw new ProfileValidationException(aSource, new[] { "document is empty (parsed to null)." });
        }

        var vErrors = new List<string>();
        if (string.IsNullOrWhiteSpace(vDocument.Name))
        {
            vErrors.Add("top-level 'name' is required.");
        }

        var vRequirements = ParseRequirements(vDocument, vErrors);
        if (vErrors.Count > 0)
        {
            throw new ProfileValidationException(vDocument.Name ?? aSource, vErrors);
        }

        return new TrSetupProfile(vDocument.Name!, vRequirements);
    }

    private static List<ProfileRequirement> ParseRequirements(ProfileDocument aDocument, List<string> aErrors)
    {
        var vRequirements = new List<ProfileRequirement>();
        var vSeenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var vEntries = aDocument.Requirements ?? new List<RequirementEntry>();
        for (var vIndex = 0; vIndex < vEntries.Count; vIndex++)
        {
            var vEntry = vEntries[vIndex];
            var vContext = string.IsNullOrWhiteSpace(vEntry.Id) ? $"requirement[{vIndex}]" : $"requirement '{vEntry.Id}'";
            var vRequirement = ParseEntry(vEntry, vContext, aErrors);
            if (vRequirement is null)
            {
                continue;
            }

            if (!vSeenIds.Add(vRequirement.Id))
            {
                aErrors.Add($"{vContext}: duplicate id within the document.");
                continue;
            }

            vRequirements.Add(vRequirement);
        }

        return vRequirements;
    }

    private static ProfileRequirement? ParseEntry(RequirementEntry aEntry, string aContext, List<string> aErrors)
    {
        var vErrorsBefore = aErrors.Count;
        if (string.IsNullOrWhiteSpace(aEntry.Id))
        {
            aErrors.Add($"{aContext}: 'id' is required.");
        }

        if (string.IsNullOrWhiteSpace(aEntry.Title))
        {
            aErrors.Add($"{aContext}: 'title' is required.");
        }

        var vTypeKnown = false;
        if (string.IsNullOrWhiteSpace(aEntry.Type))
        {
            aErrors.Add($"{aContext}: 'type' is required.");
        }
        else if (!ProfileRequirementTypes.IsKnown(aEntry.Type))
        {
            aErrors.Add($"{aContext}: unknown type '{aEntry.Type}' (expected one of {string.Join(", ", ProfileRequirementTypes.All)}).");
        }
        else
        {
            vTypeKnown = true;
        }

        var vRoles = ProfileRoleParser.Parse(aEntry.Roles ?? new List<string>(), aErrors, aContext);
        var vSeverity = ParseSeverity(aEntry.Severity, aContext, aErrors);
        var vParams = BuildParams(aEntry.Params);
        if (vTypeKnown)
        {
            ProfileRequirementParamRules.Validate(aEntry.Type!, vParams, aErrors, aContext);
        }

        if (aErrors.Count > vErrorsBefore)
        {
            return null;
        }

        return new ProfileRequirement(aEntry.Type!, aEntry.Id!, aEntry.Title!, vRoles, vSeverity, vParams);
    }

    private static CheckSeverity ParseSeverity(string? aSeverity, string aContext, List<string> aErrors)
    {
        if (string.IsNullOrWhiteSpace(aSeverity))
        {
            return CheckSeverity.Required;
        }

        if (Enum.TryParse<CheckSeverity>(aSeverity.Trim(), ignoreCase: true, out var vSeverity) && Enum.IsDefined(vSeverity))
        {
            return vSeverity;
        }

        aErrors.Add($"{aContext}: unknown severity '{aSeverity}' (expected Required, Recommended or Optional).");
        return CheckSeverity.Required;
    }

    private static IReadOnlyDictionary<string, string> BuildParams(Dictionary<string, JsonElement>? aParams)
    {
        var vResult = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (aParams is null)
        {
            return vResult;
        }

        foreach (var vPair in aParams)
        {
            vResult[vPair.Key] = vPair.Value.ValueKind == JsonValueKind.String
                ? vPair.Value.GetString() ?? string.Empty
                : vPair.Value.GetRawText();
        }

        return vResult;
    }

    private sealed class ProfileDocument
    {
        public string? Name { get; set; }

        public List<RequirementEntry>? Requirements { get; set; }
    }

    private sealed class RequirementEntry
    {
        public string? Type { get; set; }

        public string? Id { get; set; }

        public string? Title { get; set; }

        public List<string>? Roles { get; set; }

        public string? Severity { get; set; }

        public Dictionary<string, JsonElement>? Params { get; set; }
    }
}
