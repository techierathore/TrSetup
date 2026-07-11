namespace TrSetup.Core.Reporting;

/// <summary>
/// The absolute paths of the two files a report export produced (REQ-FN-010):
/// the Markdown report and its self-contained HTML sibling.
/// </summary>
/// <param name="MarkdownPath">Absolute path of the exported <c>TrSetup-Report-&lt;host&gt;.md</c>.</param>
/// <param name="HtmlPath">Absolute path of the exported <c>TrSetup-Report-&lt;host&gt;.html</c>.</param>
public sealed record ReportExportResult(string MarkdownPath, string HtmlPath);
