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
    private readonly ITelemetryServiceApi _telemetryService;
    private DateTime _lastSuccessfulPoll = DateTime.MinValue;

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

    // Filter/Sort State
    [ObservableProperty]
    private string? _selectedFilterApplicationId;

    [ObservableProperty]
    private string? _selectedFilterNetworkId;

    [ObservableProperty]
    private string _sortBy = "TotalBytes";

    [ObservableProperty]
    private bool _sortDescending = true;

    public ObservableCollection<string> UniqueApplicationIds { get; } = new();

    public ObservableCollection<string> UniqueNetworkIds { get; } = new();

    /// <summary>
    /// Property for sort field selection in UI
    /// </summary>
    // Note: _sortBy is defined via [ObservableProperty] attribute above
    public System.Collections.ObjectModel.ObservableCollection<string> AvailableSortFields { get; } = new()
    {
        "TotalBytes",
        "DownloadBytes", 
        "UploadBytes",
        "DisplayName"
    };

    public string SortDirectionGlyph => SortDescending ? "\u2193" : "\u2191";  // Down/Up arrows

    /// <summary>
    /// Constructor for production use (requires IPC client injection).
    /// Mock data support removed entirely - violates trust principle if left in production builds.
    /// </summary>
    public TrafficExplorerViewModel(ITelemetryServiceApi telemetryService)
    {
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
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
            UpdateComponentState();

            await LoadRealDataAsync();

            _lastSuccessfulPoll = DateTime.UtcNow;
            LastUpdatedText = "updated just now";

            LoadState = ApplicationList.Count == 0 ? DashboardLoadState.Empty : DashboardLoadState.Loaded;
            ConnectionState = TelemetryConnectionState.Connected;
            FreshnessState = TelemetryFreshnessState.Live;
            ErrorMessage = string.Empty;
        }
        catch (System.IO.IOException)
        {
            // IPC connection failed
            LoadState = DashboardLoadState.Error;
            ConnectionState = TelemetryConnectionState.Offline;
            ErrorMessage = "Internet Tracer service is unavailable.";
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

    private async Task LoadRealDataAsync(CancellationToken? cancellationToken = null)
    {
        var startUtc = StartDate;
        var endUtc = EndDate;

        // Load unique IDs for filters
        await LoadFilterOptionsAsync(startUtc, endUtc);

        // Get top applications with filters applied
        var appId = SelectedFilterApplicationId;
        var apps = await _telemetryService.GetTopApplicationsFilteredAsync(startUtc, endUtc, 50, appId);
        
        // Apply sorting
        var sortField = SortBy.ToLowerInvariant();
        var descending = SortDescending;
        
        if (sortField == "totalbytes")
            apps = descending ? apps.OrderByDescending(a => a.TotalBytes).ToList() 
                             : apps.OrderBy(a => a.TotalBytes).ToList();
        else if (sortField == "downloadbytes")
            apps = descending ? apps.OrderByDescending(a => a.DownloadBytes).ToList() 
                             : apps.OrderBy(a => a.DownloadBytes).ToList();
        else if (sortField == "uploadbytes")
            apps = descending ? apps.OrderByDescending(a => a.UploadBytes).ToList() 
                             : apps.OrderBy(a => a.UploadBytes).ToList();
        else // Name
            apps = descending ? apps.OrderByDescending(a => a.DisplayName).ToList() 
                             : apps.OrderBy(a => a.DisplayName).ToList();

        ApplicationList.Clear();
        foreach (var app in apps)
        {
            ApplicationList.Add(app);
        }

        // Get network usage filtered by network selection
        var networkId = SelectedFilterNetworkId;
        var networks = await _telemetryService.GetNetworkUsageFilteredAsync(startUtc, endUtc, networkId);
        
        NetworkList.Clear();
        foreach (var network in networks)
        {
            NetworkList.Add(network);
        }

        // Get timeline chart data with adaptive resolution
        var resolution = DetermineHistoricalResolution(startUtc, endUtc);
        var timeline = await _telemetryService.GetTrafficTimelineAsync(startUtc, endUtc, resolution);
        
        TrafficTimeline = timeline;
        CalculateTotalTraffic();
    }

    /// <summary>
    /// Determines optimal resolution based on time range size.
    /// Matches persisted minute-level granularity with reasonable limits.
    /// </summary>
    private static string DetermineHistoricalResolution(DateTime start, DateTime end)
    {
        var duration = end - start;
        
        if (duration.TotalMinutes <= 1440)        // Up to 24 hours
            return "1m";                          // Use 1-minute buckets (max 1440 points)
        else if (duration.TotalHours <= 168)     // Up to 7 days  
            return "1h";                          // Aggregate to hourly (max 168 points)
        else if (duration.TotalDays <= 30)       // Up to 30 days
            return "1h";                          // Hourly buckets (max 720 points)
        else if (duration.TotalDays <= 90)       // Up to 90 days
            return "4h";                          // 4-hour buckets (max 540 points)
        else                                      // 90+ days
            return "1d";                          // Daily buckets (max 365 points per day)
    }

    [RelayCommand]
    public async Task LoadApplicationDetailsAsync(string applicationId)
    {
        try
        {
            if (_telemetryService == null)
                throw new InvalidOperationException("Telemetry service not initialized");

            var startUtc = StartDate;
            var endUtc = EndDate;
            SelectedAppDetails = await _telemetryService.GetApplicationUsageAsync(applicationId, startUtc, endUtc);
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

    /// <summary>
    /// Loads unique application/network IDs for filter dropdowns.
    /// Called when loading data or changing time range.
    /// </summary>
    private async Task LoadFilterOptionsAsync(DateTime startUtc, DateTime endUtc)
    {
        try
        {
            // Get unique application IDs
            var appIds = await _telemetryService.GetUniqueApplicationIdsAsync(startUtc, endUtc);
            UniqueApplicationIds.Clear();
            
            if (appIds.Any())
            {
                UniqueApplicationIds.Insert(0, "All Applications");
                
                foreach (var id in appIds)
                {
                    UniqueApplicationIds.Add(id);
                }
            }
            else
            {
                UniqueApplicationIds.Add("All Applications");
            }

            // Get unique network IDs
            var netIds = await _telemetryService.GetUniqueNetworkIdsAsync(startUtc, endUtc);
            UniqueNetworkIds.Clear();
            
            if (netIds.Any())
            {
                UniqueNetworkIds.Insert(0, "All Networks");
                
                foreach (var id in netIds)
                {
                    UniqueNetworkIds.Add(id);
                }
            }
            else
            {
                UniqueNetworkIds.Add("All Networks");
            }
        }
        catch (Exception ex)
        {
            // If filter options fail, use safe defaults
            ErrorMessage = $"Failed to load filter options: {ex.Message}";
            UniqueApplicationIds.Clear();
            UniqueNetworkIds.Clear();
            UniqueApplicationIds.Add("All Applications");
            UniqueNetworkIds.Add("All Networks");
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedFilterApplicationId = null;
        SelectedFilterNetworkId = null;
        
        // Reset UI selections to "All"
        if (UniqueApplicationIds.Contains("All Applications"))
        {
            SelectedFilterApplicationId = null;
        }
        
        if (UniqueNetworkIds.Contains("All Networks"))
        {
            SelectedFilterNetworkId = null;
        }
        
        SortBy = "TotalBytes";
        SortDescending = true;
    }

    [RelayCommand]
    private void ToggleSortDirection()
    {
        SortDescending = !SortDescending;
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
