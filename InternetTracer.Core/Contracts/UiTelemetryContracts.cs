namespace InternetTracer.Core.Contracts;

using System;
using System.Collections.Generic;

public class DashboardSummary
{
    public SpeedSnapshot CurrentSpeed { get; set; } = new();
    public TrafficSnapshot TodayTraffic { get; set; } = new();
    public TrafficSnapshot MonthlyTraffic { get; set; } = new();
    public ConnectionQuality Quality { get; set; } = new();
    public List<TopUsageEntry> TopApplications { get; set; } = new();
}

public class SpeedSnapshot
{
    public long DownloadBytesPerSecond { get; set; }
    public long UploadBytesPerSecond { get; set; }
}

public class TrafficSnapshot
{
    public long DownloadBytes { get; set; }
    public long UploadBytes { get; set; }
    public long TotalBytes => DownloadBytes + UploadBytes;
}

public class ConnectionQuality
{
    public string Status { get; set; } = "Unknown";
    public long LatencyMs { get; set; }
    public double PacketLossPercentage { get; set; }
}

public class CurrentSnapshot
{
    public long CurrentDownloadBytesPerSec { get; set; }
    public long CurrentUploadBytesPerSec { get; set; }
    public int ActiveConnections { get; set; }
}

public class TopUsageEntry
{
    public string EntityId { get; set; } = string.Empty; // App ID or Network ID
    public string DisplayName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public long DownloadBytes { get; set; }
    public long UploadBytes { get; set; }
    public long TotalBytes { get; set; }
    public string AttributionState { get; set; } = "Attributed";
}

public class TrafficTimeline
{
    public List<TrafficTimelinePoint> Points { get; set; } = new();
}

public class TrafficTimelinePoint
{
    public DateTime TimestampUtc { get; set; }
    public long DownloadBytes { get; set; }
    public long UploadBytes { get; set; }
}

public class ApplicationUsage
{
    public string ApplicationId { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public TrafficSnapshot TotalTraffic { get; set; } = new();
    public TrafficTimeline Timeline { get; set; } = new();
}

public class NetworkUsage
{
    public string NetworkId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TrafficSnapshot TotalTraffic { get; set; } = new();
}

public class ConnectionEvent
{
    public DateTime TimestampUtc { get; set; }
    public string EventType { get; set; } = string.Empty; // e.g., Connected, Disconnected
    public string NetworkId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
