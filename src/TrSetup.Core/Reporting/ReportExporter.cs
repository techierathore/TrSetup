using System.Net;
using System.Text;
using TrSetup.Core.Checks;
using TrSetup.Core.Engine;

namespace TrSetup.Core.Reporting;

/// <summary>
/// Renders the full check board as a shareable report (REQ-FN-010): Markdown plus a
/// self-contained HTML sibling in the shared doc-shell style, written as
/// <c>TrSetup-Report-&lt;host&gt;.md</c> / <c>.html</c>. Secret-free by construction:
/// checks never emit secret values (ADR-008), and as a defensive layer any row whose
/// check id/title marks it secret-related renders presence status only — its raw
/// evidence is never included.
/// </summary>
public sealed class ReportExporter
{
    /// <summary>
    /// Renders the board as a Markdown report: host/roles/app/timestamp header, one section
    /// per group with ✓/⚠/✗ counts, and per row the status icon+text, severity, last-detect
    /// time and the evidence / last-run output (presence-only for secret-related rows).
    /// </summary>
    /// <param name="aBoard">The board to render (typically after a detect sweep).</param>
    /// <param name="aHostName">The machine name shown in the report header.</param>
    /// <returns>The complete Markdown document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aBoard"/> or <paramref name="aHostName"/> is null.</exception>
    public string BuildMarkdown(CheckBoard aBoard, string aHostName)
    {
        ArgumentNullException.ThrowIfNull(aBoard);
        ArgumentNullException.ThrowIfNull(aHostName);

        var vSb = new StringBuilder();
        vSb.AppendLine($"# TrSetup Report — {aHostName}");
        vSb.AppendLine();
        vSb.AppendLine($"- **Host:** {aHostName}");
        vSb.AppendLine($"- **Roles:** {FormatRoles(aBoard.Roles)}");
        vSb.AppendLine($"- **Selected app:** {aBoard.SelectedApp ?? "(none)"}");
        vSb.AppendLine($"- **Generated:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        foreach (var vGroup in aBoard.Groups)
        {
            vSb.AppendLine();
            vSb.AppendLine($"## {vGroup.Name} — {FormatCounts(vGroup)}");
            foreach (var vRow in vGroup.Rows)
            {
                AppendMarkdownRow(vSb, vRow);
            }
        }

        return vSb.ToString();
    }

    /// <summary>
    /// Renders the board as a single self-contained HTML report in the shared doc-shell
    /// style (warm-light/soft-dark themes with persisted toggle, copy buttons, inline TOC);
    /// no external requests — all CSS/JS is inlined. Content mirrors
    /// <see cref="BuildMarkdown"/>, including the secret presence-only scrub.
    /// </summary>
    /// <param name="aBoard">The board to render (typically after a detect sweep).</param>
    /// <param name="aHostName">The machine name shown in the report header.</param>
    /// <returns>The complete HTML document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aBoard"/> or <paramref name="aHostName"/> is null.</exception>
    public string BuildHtml(CheckBoard aBoard, string aHostName)
    {
        ArgumentNullException.ThrowIfNull(aBoard);
        ArgumentNullException.ThrowIfNull(aHostName);

        var vTitle = $"TrSetup Report — {WebUtility.HtmlEncode(aHostName)}";
        var vHasSidebar = aBoard.Groups.Count > 6;
        var vSb = new StringBuilder();
        vSb.AppendLine("<!doctype html>");
        vSb.AppendLine("<html lang=\"en\">");
        vSb.AppendLine("<head>");
        vSb.AppendLine("<meta charset=\"utf-8\">");
        vSb.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        vSb.AppendLine($"<title>{vTitle}</title>");
        vSb.AppendLine($"<script>{ReportHtmlShell.HeadThemeScript}</script>");
        vSb.AppendLine($"<style>{ReportHtmlShell.Css}</style>");
        vSb.AppendLine("</head>");
        vSb.AppendLine("<body>");
        vSb.AppendLine("<button id=\"themeToggle\" class=\"theme-toggle\" title=\"Toggle light / dark\">☾ Dark</button>");
        vSb.AppendLine($"<div class=\"layout{(vHasSidebar ? string.Empty : " no-toc")}\">");
        if (vHasSidebar)
        {
            AppendHtmlSidebar(vSb, aBoard, vTitle);
        }

        AppendHtmlMain(vSb, aBoard, aHostName, vTitle);
        vSb.AppendLine("</div>");
        vSb.AppendLine($"<script>{ReportHtmlShell.BodyScript}</script>");
        vSb.AppendLine("</body>");
        vSb.AppendLine("</html>");
        return vSb.ToString();
    }

    /// <summary>
    /// Writes the Markdown and HTML reports for this machine into a directory (created when
    /// missing) as <c>TrSetup-Report-&lt;host&gt;.md</c> / <c>.html</c>, using
    /// <see cref="Environment.MachineName"/> as the host.
    /// </summary>
    /// <param name="aBoard">The board to export (typically after a detect sweep).</param>
    /// <param name="aDirectory">Target directory for the two files.</param>
    /// <param name="aCancellationToken">Cancels the file writes.</param>
    /// <returns>The absolute paths of the two written files.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aBoard"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="aDirectory"/> is null or whitespace.</exception>
    public async Task<ReportExportResult> ExportAsync(
        CheckBoard aBoard,
        string aDirectory,
        CancellationToken aCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aBoard);
        ArgumentException.ThrowIfNullOrWhiteSpace(aDirectory);

        var vHost = Environment.MachineName;
        var vSafeHost = SanitizeForFileName(vHost);
        Directory.CreateDirectory(aDirectory);
        var vMarkdownPath = Path.GetFullPath(Path.Combine(aDirectory, $"TrSetup-Report-{vSafeHost}.md"));
        var vHtmlPath = Path.GetFullPath(Path.Combine(aDirectory, $"TrSetup-Report-{vSafeHost}.html"));
        await File.WriteAllTextAsync(vMarkdownPath, BuildMarkdown(aBoard, vHost), aCancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(vHtmlPath, BuildHtml(aBoard, vHost), aCancellationToken).ConfigureAwait(false);
        return new ReportExportResult(vMarkdownPath, vHtmlPath);
    }

    private static void AppendMarkdownRow(StringBuilder aSb, BoardRow aRow)
    {
        aSb.AppendLine();
        aSb.AppendLine($"### {StatusIcon(aRow.Status)} {aRow.Check.Title}");
        aSb.AppendLine();
        aSb.AppendLine($"- **Check id:** `{aRow.Check.Id}`");
        aSb.AppendLine($"- **Status:** {StatusIcon(aRow.Status)} {StatusText(aRow.Status)}");
        aSb.AppendLine($"- **Severity:** {aRow.Check.Severity}");
        aSb.AppendLine($"- **Last detected:** {FormatLastDetected(aRow)}");
        aSb.AppendLine();
        aSb.AppendLine("**Evidence / last-run output:**");
        aSb.AppendLine();
        aSb.AppendLine("```text");
        aSb.AppendLine(FenceSafe(RenderEvidence(aRow)));
        aSb.AppendLine("```");
    }

    private static void AppendHtmlSidebar(StringBuilder aSb, CheckBoard aBoard, string aTitle)
    {
        aSb.AppendLine("<nav class=\"side\">");
        aSb.AppendLine($"<h1>{aTitle}</h1>");
        aSb.AppendLine($"<div class=\"sub\">Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC</div>");
        aSb.AppendLine("<div class=\"group\">Contents</div>");
        aSb.AppendLine("<ol>");
        foreach (var vGroup in aBoard.Groups)
        {
            aSb.AppendLine($"<li><a href=\"#{Slug(vGroup.Name)}\">{WebUtility.HtmlEncode(vGroup.Name)}</a></li>");
        }

        aSb.AppendLine("</ol>");
        aSb.AppendLine("</nav>");
    }

    private static void AppendHtmlMain(StringBuilder aSb, CheckBoard aBoard, string aHostName, string aTitle)
    {
        aSb.AppendLine("<main>");
        aSb.AppendLine($"<h1>{aTitle}</h1>");
        aSb.AppendLine(
            $"<div class=\"subtitle\">Roles: {WebUtility.HtmlEncode(FormatRoles(aBoard.Roles))} · " +
            $"Selected app: {WebUtility.HtmlEncode(aBoard.SelectedApp ?? "(none)")} · " +
            $"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC · " +
            $"Host: {WebUtility.HtmlEncode(aHostName)}</div>");
        if (aBoard.Groups.Count >= 2)
        {
            AppendHtmlInlineToc(aSb, aBoard);
        }

        foreach (var vGroup in aBoard.Groups)
        {
            aSb.AppendLine(
                $"<h2 id=\"{Slug(vGroup.Name)}\">{WebUtility.HtmlEncode(vGroup.Name)}" +
                $"<span class=\"counts\">{FormatCounts(vGroup)}</span></h2>");
            foreach (var vRow in vGroup.Rows)
            {
                AppendHtmlRow(aSb, vRow);
            }
        }

        aSb.AppendLine("</main>");
    }

    private static void AppendHtmlInlineToc(StringBuilder aSb, CheckBoard aBoard)
    {
        aSb.AppendLine("<div class=\"toc-inline\">");
        aSb.AppendLine("<div>Contents</div>");
        aSb.AppendLine("<ol>");
        foreach (var vGroup in aBoard.Groups)
        {
            aSb.AppendLine(
                $"<li><a href=\"#{Slug(vGroup.Name)}\">{WebUtility.HtmlEncode(vGroup.Name)}</a>" +
                $" — {FormatCounts(vGroup)}</li>");
        }

        aSb.AppendLine("</ol>");
        aSb.AppendLine("</div>");
    }

    private static void AppendHtmlRow(StringBuilder aSb, BoardRow aRow)
    {
        var vStatusClass = StatusCssClass(aRow.Status);
        aSb.AppendLine(
            $"<h3 id=\"{Slug(aRow.Check.Id)}\"><span class=\"{vStatusClass}\">{StatusIcon(aRow.Status)}</span> " +
            $"{WebUtility.HtmlEncode(aRow.Check.Title)}</h3>");
        aSb.AppendLine(
            $"<div class=\"row-meta\">Status: <span class=\"{vStatusClass}\">{StatusIcon(aRow.Status)} {StatusText(aRow.Status)}</span>" +
            $" · Check id: <code>{WebUtility.HtmlEncode(aRow.Check.Id)}</code>" +
            $" · Severity: {aRow.Check.Severity}" +
            $" · Last detected: {WebUtility.HtmlEncode(FormatLastDetected(aRow))}</div>");
        aSb.AppendLine($"<pre><code>{WebUtility.HtmlEncode(RenderEvidence(aRow))}</code></pre>");
    }

    private static string RenderEvidence(BoardRow aRow)
    {
        if (IsSecretRelated(aRow.Check))
        {
            return ScrubbedSecretEvidence(aRow.Status);
        }

        if (aRow.Status is null)
        {
            return "(not detected yet — run a detect sweep before exporting)";
        }

        return string.IsNullOrWhiteSpace(aRow.Evidence) ? "(no evidence captured)" : aRow.Evidence;
    }

    private static bool IsSecretRelated(Check aCheck)
        => aCheck.Id.Contains("secret", StringComparison.OrdinalIgnoreCase)
           || aCheck.Title.Contains("secret", StringComparison.OrdinalIgnoreCase);

    private static string ScrubbedSecretEvidence(CheckStatus? aStatus) => aStatus switch
    {
        CheckStatus.Pass => "Secret present (presence-only — the value is never rendered; ADR-008).",
        CheckStatus.Warn => "Secret present but degraded (presence-only — the value is never rendered; ADR-008).",
        CheckStatus.Fail => "Secret missing or not detected (presence-only — the value is never rendered; ADR-008).",
        CheckStatus.NotApplicable => "Out of scope on this machine (presence-only secret row; ADR-008).",
        _ => "(not detected yet — presence-only secret row; ADR-008)"
    };

    private static string StatusIcon(CheckStatus? aStatus) => aStatus switch
    {
        CheckStatus.Pass => "✓",
        CheckStatus.Warn => "⚠",
        CheckStatus.Fail => "✗",
        CheckStatus.NotApplicable => "–",
        _ => "…"
    };

    private static string StatusText(CheckStatus? aStatus) => aStatus switch
    {
        CheckStatus.Pass => "Pass",
        CheckStatus.Warn => "Warn",
        CheckStatus.Fail => "Fail",
        CheckStatus.NotApplicable => "Not applicable",
        _ => "Pending"
    };

    private static string StatusCssClass(CheckStatus? aStatus) => aStatus switch
    {
        CheckStatus.Pass => "status-pass",
        CheckStatus.Warn => "status-warn",
        CheckStatus.Fail => "status-fail",
        _ => "status-na"
    };

    private static string FormatCounts(BoardGroup aGroup)
        => $"✓ {aGroup.PassCount} · ⚠ {aGroup.WarnCount} · ✗ {aGroup.FailCount}" +
           (aGroup.NotApplicableCount > 0 ? $" · – {aGroup.NotApplicableCount} n/a" : string.Empty);

    private static string FormatRoles(MachineRole aRoles)
        => aRoles == MachineRole.None ? "(none selected)" : aRoles.ToString();

    private static string FormatLastDetected(BoardRow aRow)
        => aRow.LastDetectedAt is null ? "never" : $"{aRow.LastDetectedAt:yyyy-MM-dd HH:mm:ss} UTC";

    private static string FenceSafe(string aText)
        => aText.Replace("```", "`` `", StringComparison.Ordinal);

    private static string SanitizeForFileName(string aHost)
    {
        var vInvalid = Path.GetInvalidFileNameChars();
        var vChars = aHost.Select(aChar => vInvalid.Contains(aChar) || char.IsWhiteSpace(aChar) ? '-' : aChar);
        var vSafe = new string(vChars.ToArray()).Trim('-');
        return vSafe.Length == 0 ? "host" : vSafe;
    }

    private static string Slug(string aText)
    {
        var vSb = new StringBuilder(aText.Length);
        var vLastWasDash = true;
        foreach (var vChar in aText.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(vChar))
            {
                vSb.Append(vChar);
                vLastWasDash = false;
            }
            else if (!vLastWasDash)
            {
                vSb.Append('-');
                vLastWasDash = true;
            }
        }

        var vSlug = vSb.ToString().Trim('-');
        return vSlug.Length == 0 ? "section" : vSlug;
    }
}
