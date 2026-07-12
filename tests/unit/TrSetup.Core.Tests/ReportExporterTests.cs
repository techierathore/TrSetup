using TrSetup.Core.Checks;
using TrSetup.Core.Engine;
using TrSetup.Core.Reporting;
using TrSetup.Core.Tests.TestDoubles;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-010 report exporter tests: the Markdown and HTML outputs reproduce a fake board
/// (header, groups with counts, per-row status + evidence), files land with the
/// <c>TrSetup-Report-&lt;host&gt;</c> naming, and secret-related rows render presence
/// status only — a secret value smuggled into evidence never reaches either output.
/// </summary>
public sealed class ReportExporterTests
{
    private const string SecretValue = "sk-SUPERSECRET-9f8e7d6c";

    private readonly ReportExporter objExporter = new();

    /// <summary>
    /// Builds a detected two-group board: a passing SDK check and a failing bridge check
    /// (plus an out-of-scope Mac row), optionally adding a secret-related check whose
    /// evidence deliberately tries to leak a secret value.
    /// </summary>
    private static async Task<CheckBoard> BuildDetectedBoardAsync(bool aIncludeSecretCheck = false)
    {
        var vChecks = new List<Check>
        {
            new FakeCheck(
                "wsl.dotnet-sdk",
                MachineRole.AgentHostWsl,
                aDetect: _ => Task.FromResult(CheckResult.Pass("dotnet --version -> 10.0.100")),
                aCategory: "Framework core"),
            new FakeCheck(
                "wsl.winrun-bridge",
                MachineRole.AgentHostWsl,
                aDetect: _ => Task.FromResult(CheckResult.Fail("~/bin/winrun missing from PATH")),
                aCategory: "Bridges"),
            new FakeCheck(
                "mac.xcode",
                MachineRole.DeviceHostMac,
                aCategory: "Framework core")
        };
        if (aIncludeSecretCheck)
        {
            vChecks.Add(new FakeCheck(
                "win.env-secret.appmanager",
                MachineRole.AgentHostWsl,
                aDetect: _ => Task.FromResult(CheckResult.Pass($"AppManagerKey={SecretValue} found in environment")),
                aCategory: "Framework core"));
        }

        var vEngine = new CheckEngine(vChecks);
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);
        return await vEngine.RunDetectSweepAsync(vBoard);
    }

    /// <summary>The Markdown report reproduces the fake board: host header, roles, both groups with counts, per-row status icon+text and evidence, and the out-of-scope row as not-applicable.</summary>
    [Fact]
    public async Task MarkdownReproducesBoard()
    {
        var vBoard = await BuildDetectedBoardAsync();

        var vMarkdown = objExporter.BuildMarkdown(vBoard, "TESTHOST");

        Assert.Contains("# TrSetup Report — TESTHOST", vMarkdown);
        Assert.Contains("**Roles:** AgentHostWsl", vMarkdown);
        Assert.Contains("**Selected app:** (none)", vMarkdown);
        Assert.Contains("## Framework core — ✓ 1 · ⚠ 0 · ✗ 0 · – 1 n/a", vMarkdown);
        Assert.Contains("## Bridges — ✓ 0 · ⚠ 0 · ✗ 1", vMarkdown);
        Assert.Contains("✓ Pass", vMarkdown);
        Assert.Contains("✗ Fail", vMarkdown);
        Assert.Contains("– Not applicable", vMarkdown);
        Assert.Contains("dotnet --version -> 10.0.100", vMarkdown);
        Assert.Contains("~/bin/winrun missing from PATH", vMarkdown);
    }

    /// <summary>The HTML report is a self-contained doc-shell page (theme toggle, inline CSS, no external requests) reproducing the same groups, counts, statuses and evidence as the board.</summary>
    [Fact]
    public async Task HtmlReproducesBoard()
    {
        var vBoard = await BuildDetectedBoardAsync();

        var vHtml = objExporter.BuildHtml(vBoard, "TESTHOST");

        Assert.Contains("<title>TrSetup Report — TESTHOST</title>", vHtml);
        Assert.Contains("id=\"themeToggle\"", vHtml);
        Assert.Contains("--bg:#f4f1e9", vHtml);
        Assert.Contains("Framework core", vHtml);
        Assert.Contains("Bridges", vHtml);
        Assert.Contains("✓ 1 · ⚠ 0 · ✗ 0", vHtml);
        Assert.Contains("dotnet --version -&gt; 10.0.100", vHtml);
        Assert.Contains("~/bin/winrun missing from PATH", vHtml);
        Assert.DoesNotContain("src=\"http", vHtml);
        Assert.DoesNotContain("<link", vHtml);
    }

    /// <summary>A secret-related row whose evidence tries to include the secret value renders presence status only — the value appears in neither the Markdown nor the HTML output.</summary>
    [Fact]
    public async Task SecretValueNeverExported()
    {
        var vBoard = await BuildDetectedBoardAsync(aIncludeSecretCheck: true);

        var vMarkdown = objExporter.BuildMarkdown(vBoard, "TESTHOST");
        var vHtml = objExporter.BuildHtml(vBoard, "TESTHOST");

        Assert.DoesNotContain(SecretValue, vMarkdown);
        Assert.DoesNotContain(SecretValue, vHtml);
        Assert.Contains("Secret present (presence-only", vMarkdown);
        Assert.Contains("Secret present (presence-only", vHtml);
        Assert.Contains("win.env-secret.appmanager", vMarkdown);
    }

    /// <summary>A pending (never-detected) in-scope row exports as Pending with a "not detected yet" note instead of empty evidence.</summary>
    [Fact]
    public void PendingRowExportsAsPending()
    {
        var vChecks = new List<Check> { new FakeCheck("wsl.node", MachineRole.AgentHostWsl, aCategory: "Framework core") };
        var vEngine = new CheckEngine(vChecks);
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);

        var vMarkdown = objExporter.BuildMarkdown(vBoard, "TESTHOST");

        Assert.Contains("… Pending", vMarkdown);
        Assert.Contains("not detected yet", vMarkdown);
    }

    /// <summary>ExportAsync writes both files into the target directory named TrSetup-Report-&lt;host&gt;.md/.html with the machine name as host, and returns their paths.</summary>
    [Fact]
    public async Task ExportWritesBothFiles()
    {
        var vBoard = await BuildDetectedBoardAsync();
        var vDirectory = Path.Combine(Path.GetTempPath(), $"trsetup-report-test-{Guid.NewGuid():N}");
        try
        {
            var vResult = await objExporter.ExportAsync(vBoard, vDirectory);

            Assert.True(File.Exists(vResult.MarkdownPath));
            Assert.True(File.Exists(vResult.HtmlPath));
            Assert.StartsWith("TrSetup-Report-", Path.GetFileName(vResult.MarkdownPath));
            Assert.EndsWith(".md", vResult.MarkdownPath);
            Assert.EndsWith(".html", vResult.HtmlPath);
            var vMarkdown = await File.ReadAllTextAsync(vResult.MarkdownPath);
            Assert.Contains($"# TrSetup Report — {Environment.MachineName}", vMarkdown);
        }
        finally
        {
            if (Directory.Exists(vDirectory))
            {
                Directory.Delete(vDirectory, recursive: true);
            }
        }
    }
}
