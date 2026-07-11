namespace TrSetup.Core.ConfigWriting;

/// <summary>
/// The comment syntax of a config file TrSetup writes managed marker blocks into
/// (REQ-FN-018): a line prefix and an optional closing suffix.
/// </summary>
/// <param name="Prefix">Text that opens a comment line (e.g. <c>#</c>, <c>;</c>, <c>//</c>, <c>&lt;!--</c>).</param>
/// <param name="Suffix">Text that closes the comment, or empty for line comments (e.g. <c>--&gt;</c> for XML).</param>
public sealed record CommentSyntax(string Prefix, string Suffix)
{
    /// <summary>Hash line comments — shell profiles (<c>.bashrc</c>), <c>.wslconfig</c>-style INI, YAML, PowerShell <c>.ps1</c>.</summary>
    public static CommentSyntax Hash { get; } = new("#", string.Empty);

    /// <summary>Semicolon line comments — classic INI files.</summary>
    public static CommentSyntax Semicolon { get; } = new(";", string.Empty);

    /// <summary>Double-slash line comments — JSONC, C-like config files.</summary>
    public static CommentSyntax DoubleSlash { get; } = new("//", string.Empty);

    /// <summary>XML block comments — plists, csproj, XML config.</summary>
    public static CommentSyntax Xml { get; } = new("<!--", "-->");

    /// <summary>
    /// Renders one marker line in this comment syntax.
    /// </summary>
    /// <param name="aMarkerText">The marker text to wrap in the comment.</param>
    /// <returns>The complete comment line, e.g. <c># &gt;&gt;&gt; TrSetup ... &gt;&gt;&gt;</c>.</returns>
    public string RenderMarker(string aMarkerText)
        => Suffix.Length == 0 ? $"{Prefix} {aMarkerText}" : $"{Prefix} {aMarkerText} {Suffix}";
}
