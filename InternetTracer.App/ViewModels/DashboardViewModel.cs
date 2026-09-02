namespace InternetTracer_App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternetTracer.Core.Contracts;
using System.Threading.Tasks;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ITelemetryServiceApi _telemetryService;

#pragma warning disable MVVMTK0045 // WinRT AOT warning
    [ObservableProperty]
    private DashboardSummary? _summary;

    [ObservableProperty]
    private CurrentSnapshot? _snapshot;

    [ObservableProperty]
    private ConnectionQuality? _quality;

    [ObservableProperty]
    private bool _isLoading;
#pragma warning restore MVVMTK0045

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
