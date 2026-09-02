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

    // K16 Phase 4: Filtering and Search Support
    public Task<List<string>> GetUniqueApplicationIdsAsync(DateTime startUtc, DateTime endUtc)
    {
        return Task.FromResult(new List<string>
        {
            "app1",
            "app2",
            "app3",
            "app4"
        });
    }

    public Task<List<string>> GetUniqueNetworkIdsAsync(DateTime startUtc, DateTime endUtc)
    {
        return Task.FromResult(new List<string>
        {
            "net1",
            "net2"
        });
    }

    public Task<List<TopUsageEntry>> GetTopApplicationsFilteredAsync(DateTime startUtc, DateTime endUtc, int limit, string? appId)
    {
        var apps = new List<TopUsageEntry>
        {
            new TopUsageEntry { EntityId = "app1", DisplayName = "msedge.exe", TotalBytes = 1024L * 1024 * 1024 * 15 },
            new TopUsageEntry { EntityId = "app2", DisplayName = "steam.exe", TotalBytes = 1024L * 1024 * 1024 * 12 },
            new TopUsageEntry { EntityId = "app3", DisplayName = "Discord.exe", TotalBytes = 1024L * 1024 * 1024 * 4 },
            new TopUsageEntry { EntityId = "app4", DisplayName = "Spotify.exe", TotalBytes = 1024L * 1024 * 510 }
        };

        if (!string.IsNullOrEmpty(appId))
        {
            apps = apps.Where(a => a.EntityId == appId).ToList();
        }

        return Task.FromResult(apps.Take(limit).ToList());
    }

    public Task<List<TopUsageEntry>> GetTopApplicationsSortedAsync(DateTime startUtc, DateTime endUtc, int limit, string sortBy, bool descending)
    {
        var apps = new List<TopUsageEntry>
        {
            new TopUsageEntry { EntityId = "app1", DisplayName = "msedge.exe", DownloadBytes = 1024L * 1024 * 1024 * 8, UploadBytes = 1024L * 1024 * 50, TotalBytes = 1024L * 1024 * 1024 * 15 },
            new TopUsageEntry { EntityId = "app2", DisplayName = "steam.exe", DownloadBytes = 1024L * 1024 * 1024 * 6, UploadBytes = 1024L * 1024 * 600, TotalBytes = 1024L * 1024 * 1024 * 12 },
            new TopUsageEntry { EntityId = "app3", DisplayName = "Discord.exe", DownloadBytes = 1024L * 1024 * 1024 * 2, UploadBytes = 1024L * 1024 * 200, TotalBytes = 1024L * 1024 * 1024 * 4 },
            new TopUsageEntry { EntityId = "app4", DisplayName = "Spotify.exe", DownloadBytes = 1024L * 1024 * 300, UploadBytes = 1024L * 1024 * 200, TotalBytes = 1024L * 1024 * 510 }
        };

        switch (sortBy.ToLowerInvariant())
        {
            case "totalbytes":
                return Task.FromResult(descending ? apps.OrderByDescending(a => a.TotalBytes).Take(limit).ToList() 
                                                  : apps.OrderBy(a => a.TotalBytes).Take(limit).ToList());
            case "downloadbytes":
                return Task.FromResult(descending ? apps.OrderByDescending(a => a.DownloadBytes).Take(limit).ToList() 
                                                  : apps.OrderBy(a => a.DownloadBytes).Take(limit).ToList());
            case "uploadbytes":
                return Task.FromResult(descending ? apps.OrderByDescending(a => a.UploadBytes).Take(limit).ToList() 
                                                  : apps.OrderBy(a => a.UploadBytes).Take(limit).ToList());
            default: // Name
                return Task.FromResult(descending ? apps.OrderByDescending(a => a.DisplayName).Take(limit).ToList() 
                                                  : apps.OrderBy(a => a.DisplayName).Take(limit).ToList());
        }
    }

    public Task<List<NetworkUsage>> GetNetworkUsageFilteredAsync(DateTime startUtc, DateTime endUtc, string? networkId)
    {
        var networks = new List<NetworkUsage>
        {
            new NetworkUsage { NetworkId = "net1", DisplayName = "Home_WiFi_5G", TotalTraffic = new TrafficSnapshot { DownloadBytes = 1024L * 1024 * 1024 * 30, UploadBytes = 1024L * 1024 * 1024 * 4 } },
            new NetworkUsage { NetworkId = "net2", DisplayName = "Starbucks WiFi", TotalTraffic = new TrafficSnapshot { DownloadBytes = 1024L * 1024 * 1024 * 2, UploadBytes = 1024L * 1024 * 200 } }
        };

        if (!string.IsNullOrEmpty(networkId))
        {
            networks = networks.Where(n => n.NetworkId == networkId).ToList();
        }

        return Task.FromResult(networks);
    }

    public Task<List<TopUsageEntry>> SearchApplicationsAsync(DateTime startUtc, DateTime endUtc, string searchTerm, int limit)
    {
        var apps = new List<TopUsageEntry>
        {
            new TopUsageEntry { EntityId = "app1", DisplayName = "msedge.exe", TotalBytes = 1024L * 1024 * 1024 * 15 },
            new TopUsageEntry { EntityId = "app2", DisplayName = "steam.exe", TotalBytes = 1024L * 1024 * 1024 * 12 },
            new TopUsageEntry { EntityId = "app3", DisplayName = "Discord.exe", TotalBytes = 1024L * 1024 * 1024 * 4 },
            new TopUsageEntry { EntityId = "app4", DisplayName = "Spotify.exe", TotalBytes = 1024L * 1024 * 510 }
        };

        var search = searchTerm.ToLowerInvariant();
        return Task.FromResult(apps
            .Where(a => a.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList());
    }
}
