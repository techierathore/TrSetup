namespace TrSetup;

/// <summary>
/// The single native page of the head: a BlazorWebView hosting the TrSetupUI RCL router.
/// </summary>
public partial class MainPage : ContentPage
{
    /// <summary>
    /// Initializes the page and its BlazorWebView.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();

#if DEBUG
        // AUTOMATION-ID MIRROR GATE (REQ-FN-030 follow-up). index.debug.html is index.html plus the
        // automation-ids.js tag, which mirrors data-testid -> aria-placeholder so XCUITest/Appium
        // can locate Blazor controls inside the WKWebView. That attribute is ARIA-legal only on
        // textbox/searchbox/combobox/spinbutton, so RELEASE deliberately stays on the clean
        // index.html and never ships the mirror. Full rationale: TrSetup.csproj, same banner name.
        // Assigned here rather than in XAML so the shipping document remains the declared default.
        blazorWebView.HostPage = "wwwroot/index.debug.html";
#endif
    }
}
