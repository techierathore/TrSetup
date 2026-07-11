using Serilog;

namespace TrSetup;

/// <summary>
/// The MAUI application shell: creates the single window hosting <see cref="MainPage"/>.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Initializes the application resources.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates the app's single window and hooks its <see cref="Window.Destroying"/> event to flush
    /// and close the Serilog pipeline (REQ-NFR-007). This is the cleanest deterministic shutdown
    /// point for the single-window unpackaged desktop head — it fires when the window is torn down,
    /// guaranteeing buffered file-sink writes reach disk before the process exits.
    /// </summary>
    /// <param name="aActivationState">Platform activation state supplied by MAUI.</param>
    /// <returns>The main window titled TrSetup.</returns>
    protected override Window CreateWindow(IActivationState? aActivationState)
    {
        var vWindow = new Window(new MainPage()) { Title = "TrSetup" };
        vWindow.Destroying += (aSender, aArgs) =>
        {
            Log.Information("TrSetup shutting down");
            Log.CloseAndFlush();
        };
        return vWindow;
    }
}
