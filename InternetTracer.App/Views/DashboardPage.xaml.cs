using InternetTracer_App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace InternetTracer_App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();
        
        // Load data on start
        _ = ViewModel.LoadDashboardDataAsync();
    }
}
