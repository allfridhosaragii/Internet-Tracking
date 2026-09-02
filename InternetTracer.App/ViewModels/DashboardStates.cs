namespace InternetTracer_App.ViewModels;

public enum DashboardLoadState
{
    Loading,
    Loaded,
    Empty,
    Error
}

public enum TelemetryConnectionState
{
    Connecting,
    Connected,
    Offline,
    Error
}

public enum TelemetryFreshnessState
{
    Live,
    Stale
}

public enum AttributionHealthState
{
    Healthy,
    Degraded,
    Unavailable
}
