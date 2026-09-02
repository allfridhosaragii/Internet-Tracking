using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using Microsoft.Extensions.DependencyInjection;
using InternetTracer.Core.Contracts;
using InternetTracer.Ipc;

namespace InternetTracer_App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    
    /// <summary>
    /// Gets the current App instance in use
    /// </summary>
    public new static App Current => (App)Application.Current;

    /// <summary>
    /// Gets the IServiceProvider instance to resolve application services.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
        
        this.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled exception: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine(e.Exception.StackTrace);
            System.IO.File.WriteAllText("p:\\Internet Tracer\\crash.txt", $"{e.Exception.Message}\n{e.Exception.StackTrace}");
            e.Handled = true;
        };
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register IPC Client as Singleton (Production)
        services.AddSingleton<ITelemetryServiceApi, IpcClient>();

        // Use Design Fixture for UI Prototyping and Layout Generation
        // services.AddSingleton<ITelemetryServiceApi, InternetTracer_App.Services.DesignFixtureTelemetryService>();

        // ViewModels will be registered here
        services.AddTransient<InternetTracer_App.ViewModels.DashboardViewModel>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
