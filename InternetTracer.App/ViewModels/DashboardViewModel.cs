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
    private DashboardSummary _summary = new DashboardSummary { TodayTraffic = new InternetTracer.Core.Contracts.TrafficSnapshot(), TopApplications = new System.Collections.Generic.List<InternetTracer.Core.Contracts.TopUsageEntry>() };

    public System.Collections.ObjectModel.ObservableCollection<InternetTracer.Core.Contracts.TopUsageEntry> TopApps { get; } = new();

    [ObservableProperty]
    private CurrentSnapshot _snapshot = new CurrentSnapshot();

    [ObservableProperty]
    private ConnectionQuality _quality = new ConnectionQuality { Status = "Loading..." };

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
    private string _liveIndicatorText = "CONNECTING";

    [ObservableProperty]
    private string _lastUpdatedText = "waiting for data";

    [ObservableProperty]
    private Components.ComponentDataState _dashboardDataState = Components.ComponentDataState.Loading;

    [ObservableProperty]
    private TrafficTimeline? _timeline;

    private CancellationTokenSource? _pollingCts;
    private DateTime _lastSuccessfulPoll = DateTime.MinValue;

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
            Quality = await _telemetryService.GetConnectionQualityAsync();

            TopApps.Clear();
            if (Summary?.TopApplications != null)
            {
                foreach (var app in Summary.TopApplications)
                {
                    TopApps.Add(app);
                }
            }

            var endUtc = DateTime.UtcNow;
            var startUtc = endUtc.AddSeconds(-60);
            Timeline = await _telemetryService.GetTrafficTimelineAsync(startUtc, endUtc, "1s");
            
            // Sync Snapshot with the Timeline's latest point to ensure visual consistency
            if (Timeline?.Points?.Count > 0)
            {
                var last = Timeline.Points.Last();
                var newSnapshot = await _telemetryService.GetCurrentSnapshotAsync();
                Snapshot = new CurrentSnapshot
                {
                    CurrentDownloadBytesPerSec = last.DownloadBytes,
                    CurrentUploadBytesPerSec = last.UploadBytes,
                    ActiveConnections = newSnapshot.ActiveConnections
                };
            }
            else
            {
                Snapshot = await _telemetryService.GetCurrentSnapshotAsync();
            }

            _lastSuccessfulPoll = DateTime.UtcNow;

            LoadState = Summary == null || Snapshot == null ? DashboardLoadState.Empty : DashboardLoadState.Loaded;
            ConnectionState = TelemetryConnectionState.Connected;
            FreshnessState = TelemetryFreshnessState.Live;
            LiveIndicatorText = "LIVE";
            LastUpdatedText = "updated just now";
            
            // Assume attribution is healthy if we have apps, or empty if we don't, but for now we say Healthy
            AttributionHealth = Summary?.TopApplications?.Count > 0 ? AttributionHealthState.Healthy : AttributionHealthState.Unavailable;
            ErrorMessage = string.Empty;

            StartPolling();
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

    private void StartPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts = new CancellationTokenSource();
        _ = PollLiveDataAsync(_pollingCts.Token);
    }

    private async Task PollLiveDataAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, token);
                if (token.IsCancellationRequested) break;
                
                var endUtc = DateTime.UtcNow;
                var startUtc = endUtc.AddSeconds(-60);
                
                var newSummary = await _telemetryService.GetDashboardSummaryAsync();
                Summary = newSummary;

                if (newSummary?.TopApplications != null)
                {
                    // Update TopApps properties in place to prevent UI flickering / item replacement animations in ItemsRepeater
                    for (int i = 0; i < newSummary.TopApplications.Count; i++)
                    {
                        var newApp = newSummary.TopApplications[i];
                        if (i < TopApps.Count)
                        {
                            var existingApp = TopApps[i];
                            // If it's a completely different app in this slot, replace it
                            if (existingApp.EntityId != newApp.EntityId)
                            {
                                TopApps[i] = newApp;
                            }
                            else
                            {
                                // Otherwise mutate properties so INotifyPropertyChanged updates UI bindings
                                existingApp.DownloadBytes = newApp.DownloadBytes;
                                existingApp.UploadBytes = newApp.UploadBytes;
                                existingApp.TotalBytes = newApp.TotalBytes;
                                existingApp.DisplayName = newApp.DisplayName;
                                existingApp.ProcessName = newApp.ProcessName;
                                existingApp.AttributionState = newApp.AttributionState;
                            }
                        }
                        else
                        {
                            TopApps.Add(newApp);
                        }
                    }
                    // Remove any excess items if the list shrunk
                    while (TopApps.Count > newSummary.TopApplications.Count)
                    {
                        TopApps.RemoveAt(TopApps.Count - 1);
                    }
                }

                var newTimeline = await _telemetryService.GetTrafficTimelineAsync(startUtc, endUtc, "1s");
                Timeline = newTimeline;

                if (newTimeline?.Points?.Count > 0)
                {
                    var last = newTimeline.Points.Last();
                    var newSnapshot = await _telemetryService.GetCurrentSnapshotAsync();
                    Snapshot = new CurrentSnapshot
                    {
                        CurrentDownloadBytesPerSec = last.DownloadBytes,
                        CurrentUploadBytesPerSec = last.UploadBytes,
                        ActiveConnections = newSnapshot.ActiveConnections
                    };
                }
                else
                {
                    Snapshot = await _telemetryService.GetCurrentSnapshotAsync();
                }

                _lastSuccessfulPoll = DateTime.UtcNow;

                if (ConnectionState != TelemetryConnectionState.Connected)
                {
                    ConnectionState = TelemetryConnectionState.Connected;
                }
                
                FreshnessState = TelemetryFreshnessState.Live;
                LiveIndicatorText = "LIVE";
                UpdateFreshnessText();
                UpdateComponentState();
            }
            catch (Exception)
            {
                // Silently degrade on transient poll failure, but mark as stale/offline if it persists
                var delta = (DateTime.UtcNow - _lastSuccessfulPoll).TotalSeconds;
                if (delta > 10)
                {
                    ConnectionState = TelemetryConnectionState.Offline;
                    LiveIndicatorText = "OFFLINE";
                }
                else
                {
                    FreshnessState = TelemetryFreshnessState.Stale;
                    LiveIndicatorText = "STALE";
                }
                
                UpdateFreshnessText();
                UpdateComponentState();
                
                if (ConnectionState == TelemetryConnectionState.Offline)
                    break;
            }
        }
    }

    private void UpdateFreshnessText()
    {
        if (_lastSuccessfulPoll == DateTime.MinValue) return;
        var delta = (DateTime.UtcNow - _lastSuccessfulPoll).TotalSeconds;
        if (delta < 2)
            LastUpdatedText = "updated just now";
        else
            LastUpdatedText = $"updated {delta:F1}s ago";
    }

    public void OnNavigatedFrom()
    {
        _pollingCts?.Cancel();
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
