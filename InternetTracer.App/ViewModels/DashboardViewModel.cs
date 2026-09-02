namespace InternetTracer_App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternetTracer.Core.Contracts;
using System.Threading.Tasks;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ITelemetryServiceApi _telemetryService;

    [ObservableProperty]
    public partial DashboardSummary? Summary { get; set; }

    [ObservableProperty]
    public partial CurrentSnapshot? Snapshot { get; set; }

    [ObservableProperty]
    public partial ConnectionQuality? Quality { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public DashboardViewModel(ITelemetryServiceApi telemetryService)
    {
        _telemetryService = telemetryService;
    }

    [RelayCommand]
    public async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        try
        {
            Summary = await _telemetryService.GetDashboardSummaryAsync();
            Snapshot = await _telemetryService.GetCurrentSnapshotAsync();
            Quality = await _telemetryService.GetConnectionQualityAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
