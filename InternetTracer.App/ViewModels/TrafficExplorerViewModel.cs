namespace InternetTracer_App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternetTracer.Core.Contracts;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

public partial class TrafficExplorerViewModel : ObservableObject
{
    private readonly ITelemetryServiceApi? _telemetryService;
    private readonly bool _useMockData;

    [ObservableProperty]
    private TimeRangeType _timeRange = TimeRangeType.Last24Hours;

    [ObservableProperty]
    private DateTime _startDate = DateTime.UtcNow.AddDays(-1);

    [ObservableProperty]
    private DateTime _endDate = DateTime.UtcNow;

    public ObservableCollection<TimeRangeOption> TimeRanges { get; } = new()
    {
        new TimeRangeOption("Last Hour", TimeRangeType.LastHour),
        new TimeRangeOption("Last 24 Hours", TimeRangeType.Last24Hours),
        new TimeRangeOption("Last 7 Days", TimeRangeType.Last7Days),
        new TimeRangeOption("Last 30 Days", TimeRangeType.Last30Days)
    };

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private TopUsageEntry? _selectedApplication;

    [ObservableProperty]
    private DashboardLoadState _loadState = DashboardLoadState.Loading;

    [ObservableProperty]
    private TelemetryConnectionState _connectionState = TelemetryConnectionState.Connecting;

    [ObservableProperty]
    private TelemetryFreshnessState _freshnessState = TelemetryFreshnessState.Stale;

    [ObservableProperty]
    private InternetTracer_App.Components.ComponentDataState _explorerDataState = InternetTracer_App.Components.ComponentDataState.Loading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _lastUpdatedText = "waiting for data";

    [ObservableProperty]
    private ApplicationUsage? _selectedAppDetails;

    [ObservableProperty]
    private TrafficTimeline? _trafficTimeline;

    [ObservableProperty]
    private TrafficSnapshot? _totalTraffic;

    public ObservableCollection<TopUsageEntry> ApplicationList { get; } = new();

    public ObservableCollection<NetworkUsage> NetworkList { get; } = new();

    public TrafficExplorerViewModel()
    {
        _useMockData = true;
        _telemetryService = null;
    }

    public TrafficExplorerViewModel(ITelemetryServiceApi telemetryService)
    {
        _telemetryService = telemetryService;
        _useMockData = false;
    }

    [RelayCommand]
    private void SelectTimeRange(TimeRangeType range)
    {
        switch (range)
        {
            case TimeRangeType.LastHour:
                StartDate = DateTime.UtcNow.AddHours(-1);
                EndDate = DateTime.UtcNow;
                break;
            case TimeRangeType.Last24Hours:
                StartDate = DateTime.UtcNow.AddHours(-24);
                EndDate = DateTime.UtcNow;
                break;
            case TimeRangeType.Last7Days:
                StartDate = DateTime.UtcNow.AddDays(-7);
                EndDate = DateTime.UtcNow;
                break;
            case TimeRangeType.Last30Days:
                StartDate = DateTime.UtcNow.AddDays(-30);
                EndDate = DateTime.UtcNow;
                break;
        }
        TimeRange = range;
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            LoadState = DashboardLoadState.Loading;
            ConnectionState = TelemetryConnectionState.Connecting;
            UpdateComponentState();

            if (_useMockData)
            {
                await LoadMockDataAsync();
            }
            else
            {
                await LoadRealDataAsync();
            }

            _lastSuccessfulPoll = DateTime.UtcNow;
            LastUpdatedText = "updated just now";

            LoadState = DashboardLoadState.Loaded;
            ConnectionState = TelemetryConnectionState.Connected;
            FreshnessState = TelemetryFreshnessState.Live;
            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            LoadState = DashboardLoadState.Error;
            ConnectionState = TelemetryConnectionState.Error;
            ErrorMessage = ex.Message;
        }
        finally
        {
            UpdateComponentState();
        }
    }

    private async Task LoadMockDataAsync()
    {
        await Task.Delay(500); // Simulate loading

        var mockApps = new[]
        {
            CreateMockApplication("Chrome", "Google Chrome", 2500000000L, 150000000L),
            CreateMockApplication("Firefox", "Mozilla Firefox", 1800000000L, 95000000L),
            CreateMockApplication("Spotify", "Spotify", 1200000000L, 45000000L),
            CreateMockApplication("VSCode", "Visual Studio Code", 850000000L, 32000000L),
            CreateMockApplication("Discord", "Discord", 620000000L, 28000000L),
            CreateMockApplication("Steam", "Steam", 4500000000L, 12000000L),
            CreateMockApplication("Edge", "Microsoft Edge", 980000000L, 55000000L),
            CreateMockApplication("Zoom", "Zoom", 320000000L, 180000000L),
            CreateMockApplication("Slack", "Slack", 280000000L, 15000000L),
            CreateMockApplication("OneDrive", "Microsoft OneDrive", 150000000L, 85000000L)
        };

        ApplicationList.Clear();
        foreach (var app in mockApps)
        {
            ApplicationList.Add(app);
        }

        var mockNetworks = new[]
        {
            CreateMockNetwork("Ethernet", "Intel Ethernet Controller", 8500000000L, 420000000L),
            CreateMockNetwork("WiFi", "Wi-Fi Adapter", 3200000000L, 180000000L)
        };

        NetworkList.Clear();
        foreach (var network in mockNetworks)
        {
            NetworkList.Add(network);
        }

        GenerateMockTimeline();
    }

    private async Task LoadRealDataAsync()
    {
        if (_telemetryService == null)
            throw new InvalidOperationException("Telemetry service not initialized");

        var startUtc = StartDate;
        var endUtc = EndDate;

        var topApps = await _telemetryService.GetTopApplicationsAsync(startUtc, endUtc, 50);
        
        ApplicationList.Clear();
        if (topApps != null)
        {
            foreach (var app in topApps)
            {
                ApplicationList.Add(app);
            }
        }

        var networks = await _telemetryService.GetNetworkUsageAsync(startUtc, endUtc);
        
        NetworkList.Clear();
        if (networks != null)
        {
            foreach (var network in networks)
            {
                NetworkList.Add(network);
            }
        }

        var timeline = await _telemetryService.GetTrafficTimelineAsync(startUtc, endUtc, "1h");
        TrafficTimeline = timeline;

        CalculateTotalTraffic();
    }

    [RelayCommand]
    public async Task LoadApplicationDetailsAsync(string applicationId)
    {
        try
        {
            if (_useMockData)
            {
                SelectedAppDetails = GetMockApplicationDetails(applicationId);
            }
            else
            {
                if (_telemetryService == null)
                    throw new InvalidOperationException("Telemetry service not initialized");

                var startUtc = StartDate;
                var endUtc = EndDate;
                SelectedAppDetails = await _telemetryService.GetApplicationUsageAsync(applicationId, startUtc, endUtc);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load application details: {ex.Message}";
        }
    }

    [RelayCommand]
    public void NavigateBack()
    {
        // Navigation logic would go here
    }

    private void GenerateMockTimeline()
    {
        var points = new System.Collections.Generic.List<TrafficTimelinePoint>();
        var now = DateTime.UtcNow;
        var hourStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);

        for (int i = 0; i < 24; i++)
        {
            var timestamp = hourStart.AddHours(i);
            points.Add(new TrafficTimelinePoint
            {
                TimestampUtc = timestamp,
                DownloadBytes = Random.Shared.Next(50_000000, 200_000000),
                UploadBytes = Random.Shared.Next(10_000000, 50_000000)
            });
        }

        TrafficTimeline = new TrafficTimeline { Points = points };
        CalculateTotalTraffic();
    }

    private void CalculateTotalTraffic()
    {
        long totalDownload = 0;
        long totalUpload = 0;

        foreach (var point in TrafficTimeline?.Points ?? Enumerable.Empty<TrafficTimelinePoint>())
        {
            totalDownload += point.DownloadBytes;
            totalUpload += point.UploadBytes;
        }

        TotalTraffic = new TrafficSnapshot
        {
            DownloadBytes = totalDownload,
            UploadBytes = totalUpload
        };
    }

    private static TopUsageEntry CreateMockApplication(string displayName, string processName, long downloadBytes, long uploadBytes)
    {
        return new TopUsageEntry
        {
            EntityId = Guid.NewGuid().ToString(),
            DisplayName = displayName,
            ProcessName = processName,
            DownloadBytes = downloadBytes,
            UploadBytes = uploadBytes,
            TotalBytes = downloadBytes + uploadBytes,
            AttributionState = "Attributed"
        };
    }

    private static NetworkUsage CreateMockNetwork(string entityId, string displayName, long downloadBytes, long uploadBytes)
    {
        return new NetworkUsage
        {
            NetworkId = entityId,
            DisplayName = displayName,
            TotalTraffic = new TrafficSnapshot
            {
                DownloadBytes = downloadBytes,
                UploadBytes = uploadBytes
            }
        };
    }

    private ApplicationUsage? GetMockApplicationDetails(string applicationId)
    {
        var apps = new Dictionary<string, ApplicationUsage>
        {
            ["chrome"] = new ApplicationUsage
            {
                ApplicationId = "chrome",
                ApplicationName = "Google Chrome",
                ExecutablePath = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
                TotalTraffic = new TrafficSnapshot { DownloadBytes = 2500000000L, UploadBytes = 150000000L },
                Timeline = new TrafficTimeline { Points = GenerateMockTimelinePoints() }
            },
            ["firefox"] = new ApplicationUsage
            {
                ApplicationId = "firefox",
                ApplicationName = "Mozilla Firefox",
                ExecutablePath = "C:\\Program Files\\Mozilla Firefox\\firefox.exe",
                TotalTraffic = new TrafficSnapshot { DownloadBytes = 1800000000L, UploadBytes = 95000000L },
                Timeline = new TrafficTimeline { Points = GenerateMockTimelinePoints() }
            },
            ["spotify"] = new ApplicationUsage
            {
                ApplicationId = "spotify",
                ApplicationName = "Spotify",
                ExecutablePath = "C:\\Program Files\\Spotify\\Setup\\spotify.exe",
                TotalTraffic = new TrafficSnapshot { DownloadBytes = 1200000000L, UploadBytes = 45000000L },
                Timeline = new TrafficTimeline { Points = GenerateMockTimelinePoints() }
            }
        };

        return apps.GetValueOrDefault(applicationId.ToLower());
    }

    private System.Collections.Generic.List<TrafficTimelinePoint> GenerateMockTimelinePoints()
    {
        var points = new System.Collections.Generic.List<TrafficTimelinePoint>();
        var now = DateTime.UtcNow;
        var hourStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);

        for (int i = 0; i < 24; i++)
        {
            var timestamp = hourStart.AddHours(i);
            points.Add(new TrafficTimelinePoint
            {
                TimestampUtc = timestamp,
                DownloadBytes = Random.Shared.Next(50_000000, 200_000000),
                UploadBytes = Random.Shared.Next(10_000000, 50_000000)
            });
        }

        return points;
    }

    private DateTime _lastSuccessfulPoll = DateTime.MinValue;

    private void UpdateComponentState()
    {
        if (ConnectionState == TelemetryConnectionState.Offline)
            ExplorerDataState = InternetTracer_App.Components.ComponentDataState.Offline;
        else if (LoadState == DashboardLoadState.Error)
            ExplorerDataState = InternetTracer_App.Components.ComponentDataState.Error;
        else if (LoadState == DashboardLoadState.Loading)
            ExplorerDataState = InternetTracer_App.Components.ComponentDataState.Loading;
        else if (LoadState == DashboardLoadState.Empty)
            ExplorerDataState = InternetTracer_App.Components.ComponentDataState.Empty;
        else if (FreshnessState == TelemetryFreshnessState.Stale)
            ExplorerDataState = InternetTracer_App.Components.ComponentDataState.Stale;
        else
            ExplorerDataState = InternetTracer_App.Components.ComponentDataState.Normal;
    }

    public void OnNavigatedFrom()
    {
        // Cleanup if needed
    }
}

public enum TimeRangeType
{
    LastHour,
    Last24Hours,
    Last7Days,
    Last30Days,
    Custom
}

public class TimeRangeOption
{
    public string Name { get; }
    public TimeRangeType Value { get; }

    public TimeRangeOption(string name, TimeRangeType value)
    {
        Name = name;
        Value = value;
    }
}
