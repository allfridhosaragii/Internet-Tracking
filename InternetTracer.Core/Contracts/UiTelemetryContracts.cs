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

public class TopUsageEntry : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private string _entityId = string.Empty;
    public string EntityId 
    { 
        get => _entityId; 
        set { if (_entityId != value) { _entityId = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(EntityId))); } } 
    }

    private string _displayName = string.Empty;
    public string DisplayName 
    { 
        get => _displayName; 
        set { if (_displayName != value) { _displayName = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(DisplayName))); } } 
    }

    private string _processName = string.Empty;
    public string ProcessName 
    { 
        get => _processName; 
        set { if (_processName != value) { _processName = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ProcessName))); } } 
    }

    private long _downloadBytes;
    public long DownloadBytes 
    { 
        get => _downloadBytes; 
        set { if (_downloadBytes != value) { _downloadBytes = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(DownloadBytes))); } } 
    }

    private long _uploadBytes;
    public long UploadBytes 
    { 
        get => _uploadBytes; 
        set { if (_uploadBytes != value) { _uploadBytes = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(UploadBytes))); } } 
    }

    private long _totalBytes;
    public long TotalBytes 
    { 
        get => _totalBytes; 
        set { if (_totalBytes != value) { _totalBytes = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TotalBytes))); } } 
    }

    private string _attributionState = "Attributed";
    public string AttributionState 
    { 
        get => _attributionState; 
        set { if (_attributionState != value) { _attributionState = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AttributionState))); } } 
    }
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
