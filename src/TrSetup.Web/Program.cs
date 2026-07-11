// TrSetup.Web — Blazor Server head (thin host, REQ-FN-011). Maps the TrSetupUI RCL and
// nothing else: Kestrel binds http://localhost:5999 (falling back to a free ephemeral port
// when 5999 is taken), prints the real URL, then best-effort opens the Windows browser via
// mirrored networking (harmless no-op on native Linux/Mac).
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using TrBlazeUI.Components.Toast;
using TrBlazeUI.Primitives.Extensions;
using TrSetup.Core.Catalog;
using TrSetup.Core.Engine;
using TrSetup.Core.Processes;
using TrSetup.Core.Reporting;
using TrSetup.Core.Settings;
using TrSetup.Web.Components;
using TrSetupUI.Services;

const int PreferredPort = 5999;

var vBuilder = WebApplication.CreateBuilder(args);

// Serve framework/RCL static web assets (blazor.web.js, _content/*) when running from build
// output in any environment — without this a plain `dotnet run` (Production) 404s them.
StaticWebAssetsLoader.UseStaticWebAssets(vBuilder.Environment, vBuilder.Configuration);

vBuilder.Services.AddRazorComponents().AddInteractiveServerComponents();
vBuilder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
vBuilder.Services.AddSingleton<ISettingsStore, JsonSettingsStore>();
vBuilder.Services.AddSingleton(aServices =>
{
    var vStore = aServices.GetRequiredService<ISettingsStore>();
    var vSettings = vStore.LoadAsync().GetAwaiter().GetResult();
    return vSettings;
});
vBuilder.Services.AddSingleton(aServices =>
{
    var vRunner = aServices.GetRequiredService<IProcessRunner>();
    var vLoad = aServices.GetRequiredService<SettingsLoadResult>();
    var vChecks = CheckCatalog.CreateAllChecks(vRunner, () => vLoad.Settings);
    return new CheckEngine(vChecks, aServices.GetRequiredService<ILogger<CheckEngine>>());
});
vBuilder.Services.AddSingleton<ReportExporter>();

// TrBlazeUI primitives + Toast, and the per-circuit board/consent UI state (REQ-UI-*).
vBuilder.Services.AddTrBlazeUIPrimitives();
vBuilder.Services.AddScoped<ToastService>();
vBuilder.Services.AddScoped<BoardState>();

// REQ-FN-011: bind localhost:5999; when taken, port 0 lets Kestrel pick a free ephemeral one
// (Kestrel forbids ListenLocalhost(0), so the fallback binds 127.0.0.1:0 explicitly).
var vPort = IsPortFree(PreferredPort) ? PreferredPort : 0;
vBuilder.WebHost.ConfigureKestrel(aOptions =>
{
    if (vPort == 0)
    {
        aOptions.Listen(IPAddress.Loopback, 0);
    }
    else
    {
        aOptions.ListenLocalhost(vPort);
    }
});

var vApp = vBuilder.Build();
vApp.UseStaticFiles();
vApp.UseAntiforgery();
vApp.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(TrSetupUI.Routes).Assembly);

vApp.Start();
var vUrl = (vApp.Urls.FirstOrDefault() ?? $"http://localhost:{vPort}")
    .Replace("127.0.0.1", "localhost", StringComparison.Ordinal);
Console.WriteLine($"TrSetup board is running at {vUrl}");
// TrSetup.Web is retained only as the headless UI smoke host (REQ-FN-034): the verify/smoke boot
// sets TRSETUP_NO_BROWSER=1 so it never pops a Windows browser window on every launch. Left unset,
// it still opens the browser for a human running the board locally.
if (!string.Equals(Environment.GetEnvironmentVariable("TRSETUP_NO_BROWSER"), "1", StringComparison.Ordinal))
{
    OpenBrowserBestEffort(vUrl);
}
vApp.WaitForShutdown();
return;

/// <summary>
/// Whether a TCP port is free to bind on loopback (probe-bind and release).
/// </summary>
/// <param name="aPort">The port to probe.</param>
/// <returns><c>true</c> when the port can be bound.</returns>
static bool IsPortFree(int aPort)
{
    try
    {
        var vListener = new TcpListener(IPAddress.Loopback, aPort);
        vListener.Start();
        vListener.Stop();
        return true;
    }
    catch (SocketException)
    {
        return false;
    }
}

/// <summary>
/// Best-effort browser launch: Windows (or WSL with mirrored networking) opens the Windows
/// browser via <c>cmd.exe /c start</c>; macOS uses <c>open</c>; other Linux tries
/// <c>xdg-open</c>. Any failure is swallowed — the printed URL is the contract.
/// </summary>
/// <param name="aUrl">The URL to open.</param>
static void OpenBrowserBestEffort(string aUrl)
{
    try
    {
        if (OperatingSystem.IsWindows() || IsWsl())
        {
            // `start` treats the first quoted arg as a window title — pass an empty one.
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c start \"\" \"{aUrl}\"") { UseShellExecute = false });
            return;
        }

        var vOpener = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
        Process.Start(new ProcessStartInfo(vOpener, aUrl) { UseShellExecute = false });
    }
    catch (Exception vEx) when (vEx is not OutOfMemoryException)
    {
        // No browser available (headless / native Linux without an opener) — harmless no-op.
    }
}

/// <summary>
/// Whether the process runs inside WSL (kernel identifies as Microsoft), where
/// <c>cmd.exe</c> reaches the Windows browser through mirrored networking.
/// </summary>
/// <returns><c>true</c> when running under WSL.</returns>
static bool IsWsl()
{
    try
    {
        return OperatingSystem.IsLinux()
               && File.Exists("/proc/version")
               && File.ReadAllText("/proc/version").Contains("microsoft", StringComparison.OrdinalIgnoreCase);
    }
    catch (IOException)
    {
        return false;
    }
}
