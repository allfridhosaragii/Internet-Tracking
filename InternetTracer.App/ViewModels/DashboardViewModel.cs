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
    private DashboardLoadState _loadState = DashboardLoadState.Loading;

    [ObservableProperty]
    private TelemetryConnectionState _connectionState = TelemetryConnectionState.Connecting;

    [ObservableProperty]
    private TelemetryFreshnessState _freshnessState = TelemetryFreshnessState.Stale;

    [ObservableProperty]
    private AttributionHealthState _attributionHealth = AttributionHealthState.Unavailable;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _lastUpdatedText = string.Empty;

    [ObservableProperty]
    private Components.ComponentDataState _dashboardDataState = Components.ComponentDataState.Loading;

#pragma warning restore MVVMTK0045

    public DashboardViewModel(ITelemetryServiceApi telemetryService)
    {
        _telemetryService = telemetryService;
    }

    [RelayCommand]
    public async Task LoadDashboardDataAsync()
    {
        try
        {
            LoadState = DashboardLoadState.Loading;
            ConnectionState = TelemetryConnectionState.Connecting;
            UpdateComponentState();

            Summary = await _telemetryService.GetDashboardSummaryAsync();
            Snapshot = await _telemetryService.GetCurrentSnapshotAsync();
            Quality = await _telemetryService.GetConnectionQualityAsync();

            LoadState = Summary == null || Snapshot == null ? DashboardLoadState.Empty : DashboardLoadState.Loaded;
            ConnectionState = TelemetryConnectionState.Connected;
            FreshnessState = TelemetryFreshnessState.Live;
            LastUpdatedText = "Live";
            
            // Assume attribution is healthy if we have apps, or empty if we don't, but for now we say Healthy
            AttributionHealth = Summary?.TopApplications?.Count > 0 ? AttributionHealthState.Healthy : AttributionHealthState.Unavailable;
            ErrorMessage = string.Empty;
        }
        catch (System.IO.IOException)
        {
            // IPC connection failed
            ConnectionState = TelemetryConnectionState.Offline;
            LoadState = DashboardLoadState.Error;
            ErrorMessage = "Internet Tracer is running, but the telemetry service is unavailable.";
        }
        catch (Exception ex)
        {
            ConnectionState = TelemetryConnectionState.Error;
            LoadState = DashboardLoadState.Error;
            ErrorMessage = ex.Message;
        }
        finally
        {
            UpdateComponentState();
        }
    }

    private void UpdateComponentState()
    {
        if (ConnectionState == TelemetryConnectionState.Offline)
            DashboardDataState = Components.ComponentDataState.Offline;
        else if (LoadState == DashboardLoadState.Error)
            DashboardDataState = Components.ComponentDataState.Error;
        else if (LoadState == DashboardLoadState.Loading)
            DashboardDataState = Components.ComponentDataState.Loading;
        else if (LoadState == DashboardLoadState.Empty)
            DashboardDataState = Components.ComponentDataState.Empty;
        else if (FreshnessState == TelemetryFreshnessState.Stale)
            DashboardDataState = Components.ComponentDataState.Stale;
        else
            DashboardDataState = Components.ComponentDataState.Normal;
    }
}
