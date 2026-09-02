namespace InternetTracer_App.Services;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InternetTracer.Core.Contracts;

/// <summary>
/// A strict design fixture that provides hyper-realistic mock telemetry exclusively for UI prototyping and design.
/// This prevents the UI from relying on database schemas or silent fallback logic.
/// </summary>
public class DesignFixtureTelemetryService : ITelemetryServiceApi
{
    public Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        return Task.FromResult(new DashboardSummary
        {
            CurrentSpeed = new SpeedSnapshot { DownloadBytesPerSecond = 1024 * 1024 * 12, UploadBytesPerSecond = 1024 * 500 },
            TodayTraffic = new TrafficSnapshot { DownloadBytes = 1024L * 1024 * 1024 * 42, UploadBytes = 1024L * 1024 * 1024 * 5 },
            MonthlyTraffic = new TrafficSnapshot { DownloadBytes = 1024L * 1024 * 1024 * 142, UploadBytes = 1024L * 1024 * 1024 * 15 }
        });
    }

    public Task<CurrentSnapshot> GetCurrentSnapshotAsync()
    {
        return Task.FromResult(new CurrentSnapshot
        {
            CurrentDownloadBytesPerSec = 1024 * 1024 * 12,
            CurrentUploadBytesPerSec = 1024 * 500,
            ActiveConnections = 12
        });
    }

    public Task<ConnectionQuality> GetConnectionQualityAsync()
    {
        return Task.FromResult(new ConnectionQuality
        {
            PacketLossPercentage = 0.05,
            LatencyMs = 24,
            Status = "Excellent"
        });
    }

    public Task<TrafficTimeline> GetTrafficTimelineAsync(DateTime startUtc, DateTime endUtc, string resolution)
    {
        var timeline = new TrafficTimeline
        {
            Points = new List<TrafficTimelinePoint>()
        };

        // Generate synthetic wave data for charts
        var current = startUtc;
        var step = TimeSpan.FromMinutes(1);
        var random = new Random(42);

        while (current < endUtc)
        {
            timeline.Points.Add(new TrafficTimelinePoint
            {
                TimestampUtc = current,
                DownloadBytes = (long)(Math.Max(0, Math.Sin(current.Ticks / 10000000.0) * 5000000 + 5000000) + random.Next(1000000)),
                UploadBytes = (long)(Math.Max(0, Math.Cos(current.Ticks / 10000000.0) * 1000000 + 1000000) + random.Next(200000))
            });
            current += step;
        }

        return Task.FromResult(timeline);
    }

    public Task<List<TopUsageEntry>> GetTopApplicationsAsync(DateTime startUtc, DateTime endUtc, int limit)
    {
        return Task.FromResult(new List<TopUsageEntry>
        {
            new TopUsageEntry { EntityId = "app1", DisplayName = "msedge.exe", TotalBytes = 1024L * 1024 * 1024 * 15 },
            new TopUsageEntry { EntityId = "app2", DisplayName = "steam.exe", TotalBytes = 1024L * 1024 * 1024 * 12 },
            new TopUsageEntry { EntityId = "app3", DisplayName = "Discord.exe", TotalBytes = 1024L * 1024 * 1024 * 4 },
            new TopUsageEntry { EntityId = "app4", DisplayName = "Spotify.exe", TotalBytes = 1024L * 1024 * 510 }
        });
    }

    public Task<List<NetworkUsage>> GetNetworkUsageAsync(DateTime startUtc, DateTime endUtc)
    {
        return Task.FromResult(new List<NetworkUsage>
        {
            new NetworkUsage { NetworkId = "net1", DisplayName = "Home_WiFi_5G", TotalTraffic = new TrafficSnapshot { DownloadBytes = 1024L * 1024 * 1024 * 30, UploadBytes = 1024L * 1024 * 1024 * 4 } },
            new NetworkUsage { NetworkId = "net2", DisplayName = "Starbucks WiFi", TotalTraffic = new TrafficSnapshot { DownloadBytes = 1024L * 1024 * 1024 * 2, UploadBytes = 1024L * 1024 * 200 } }
        });
    }

    public Task<ApplicationUsage> GetApplicationUsageAsync(string applicationId, DateTime startUtc, DateTime endUtc)
    {
        throw new NotImplementedException();
    }

    public Task<List<ConnectionEvent>> GetConnectionEventsAsync(int limit)
    {
        throw new NotImplementedException();
    }
}
