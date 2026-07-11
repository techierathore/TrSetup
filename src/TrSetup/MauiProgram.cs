using Microsoft.Extensions.Logging;
using Serilog;
using TrBlazeUI.Components.Toast;
using TrBlazeUI.Primitives.Extensions;
using TrSetup.Core.Catalog;
using TrSetup.Core.Engine;
using TrSetup.Core.Processes;
using TrSetup.Core.Reporting;
using TrSetup.Core.Settings;
using TrSetupUI.Services;

namespace TrSetup;

/// <summary>
/// Configures and builds the MAUI Blazor Hybrid head. The head is deliberately thin
/// (ADR-001): it hosts the TrSetupUI RCL in a BlazorWebView and registers nothing beyond
/// what the shared screens need.
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Creates and configures the MAUI application.
    /// </summary>
    /// <returns>The configured <see cref="MauiApp"/> instance.</returns>
    public static MauiApp CreateMauiApp()
    {
        // Serilog file logging (REQ-NFR-007 / BRD-55). A daily rolling file sink lives under the
        // platform per-user app-data folder so operators can retrieve diagnostics without console
        // access; a Debug sink mirrors events into the IDE Output window during development. The
        // shared TrSetupUI / TrSetup.Core libraries keep logging only through Microsoft.Extensions
        // .Logging — AddSerilog (below) redirects that ILogger<T> pipeline into this sink, so no
        // Serilog reference leaks into the libraries.
        var vLogDirectory = Path.Combine(FileSystem.AppDataDirectory, "logs");
        Directory.CreateDirectory(vLogDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(vLogDirectory, "trsetup-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug()
            .CreateLogger();

        RegisterUnhandledExceptionLogging();

        Log.Information(
            "TrSetup starting (version {AppVersion}, build {AppBuild})",
            AppInfo.Current.VersionString,
            AppInfo.Current.BuildString);

        var vBuilder = MauiApp.CreateBuilder();
        vBuilder
            .UseMauiApp<App>()
            .ConfigureFonts(aFonts =>
            {
                aFonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Route the Microsoft.Extensions.Logging pipeline (used by every shared library) into
        // Serilog. dispose: true lets the host flush/close the logger with the DI container.
        vBuilder.Logging.ClearProviders();
        vBuilder.Logging.AddSerilog(dispose: true);

        vBuilder.Services.AddMauiBlazorWebView();

        // Same engine + per-view board/consent UI state the Blazor Server head registers, so the
        // shared TrSetupUI RCL (MainLayout → BoardState) resolves identically on the MAUI head
        // (REQ-FN-030 wiring; mirrors TrSetup.Web/Program.cs).
        vBuilder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
        vBuilder.Services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        vBuilder.Services.AddSingleton(aServices =>
            aServices.GetRequiredService<ISettingsStore>().LoadAsync().GetAwaiter().GetResult());
        vBuilder.Services.AddSingleton(aServices =>
        {
            var vRunner = aServices.GetRequiredService<IProcessRunner>();
            var vLoad = aServices.GetRequiredService<SettingsLoadResult>();
            var vChecks = CheckCatalog.CreateAllChecks(vRunner, () => vLoad.Settings);
            return new CheckEngine(vChecks, aServices.GetRequiredService<ILogger<CheckEngine>>());
        });
        vBuilder.Services.AddSingleton<ReportExporter>();
        vBuilder.Services.AddTrBlazeUIPrimitives();
        vBuilder.Services.AddScoped<ToastService>();
        vBuilder.Services.AddScoped<BoardState>();

#if DEBUG
        vBuilder.Services.AddBlazorWebViewDeveloperTools();
        // Debug-window output is already provided by Serilog's WriteTo.Debug sink above, so no
        // separate Logging.AddDebug() is registered (it would duplicate every event).
#endif

        return vBuilder.Build();
    }

    /// <summary>
    /// Wires last-chance logging for exceptions that escape the normal handling path so that a
    /// crash still leaves a fatal record in the rolling log file. Covers both synchronous
    /// AppDomain-level failures and faulted <see cref="Task"/>s whose exceptions are never observed.
    /// </summary>
    private static void RegisterUnhandledExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (aSender, aArgs) =>
        {
            Log.Fatal(
                aArgs.ExceptionObject as Exception,
                "Unhandled AppDomain exception (terminating: {IsTerminating})",
                aArgs.IsTerminating);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (aSender, aArgs) =>
        {
            Log.Fatal(aArgs.Exception, "Unobserved Task exception");
            aArgs.SetObserved();
        };
    }
}
