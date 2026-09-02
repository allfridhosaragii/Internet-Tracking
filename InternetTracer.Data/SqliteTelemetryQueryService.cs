namespace InternetTracer.Data;

using Dapper;
using InternetTracer.Core.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class SqliteTelemetryQueryService : ITelemetryServiceApi
{
    private readonly DatabaseFactory _dbFactory;
    private readonly LiveTelemetryBuffer _liveBuffer;
    private readonly MinuteAggregator _minuteAggregator;

    public SqliteTelemetryQueryService(DatabaseFactory dbFactory, LiveTelemetryBuffer liveBuffer, MinuteAggregator minuteAggregator)
    {
        _dbFactory = dbFactory;
        _liveBuffer = liveBuffer;
        _minuteAggregator = minuteAggregator;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        var summary = new DashboardSummary();
        var snapshot = _liveBuffer.GetCurrentSnapshot();
        summary.CurrentSpeed = new SpeedSnapshot { DownloadBytesPerSecond = snapshot.CurrentDownloadBytesPerSec, UploadBytesPerSecond = snapshot.CurrentUploadBytesPerSec };
        
        var todayStart = DateTime.UtcNow.Date.ToString("o");
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).ToString("o");

        using var connection = _dbFactory.CreateConnection();
        
        var unflushedTotals = _minuteAggregator.GetUnflushedTotalVolume();

        // Today's traffic
        var today = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT SUM(download_bytes) as Download, SUM(upload_bytes) as Upload 
            FROM traffic_minute 
            WHERE bucket_utc >= @Start", new { Start = todayStart });
            
        summary.TodayTraffic = new TrafficSnapshot 
        { 
            DownloadBytes = (today?.Download ?? 0) + unflushedTotals.Download, 
            UploadBytes = (today?.Upload ?? 0) + unflushedTotals.Upload
        };

        // Monthly traffic
        var month = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT SUM(download_bytes) as Download, SUM(upload_bytes) as Upload 
            FROM traffic_minute 
            WHERE bucket_utc >= @Start", new { Start = monthStart });

        summary.MonthlyTraffic = new TrafficSnapshot 
        { 
            DownloadBytes = (month?.Download ?? 0) + unflushedTotals.Download, 
            UploadBytes = (month?.Upload ?? 0) + unflushedTotals.Upload
        };

        // Top Apps
        summary.TopApplications = await GetTopApplicationsAsync(DateTime.UtcNow.Date, DateTime.UtcNow, 5);
        summary.Quality = await GetConnectionQualityAsync();
        
        return summary;
    }

    public Task<CurrentSnapshot> GetCurrentSnapshotAsync()
    {
        return Task.FromResult(_liveBuffer.GetCurrentSnapshot());
    }

    public Task<ConnectionQuality> GetConnectionQualityAsync()
    {
        // For phase 1, connection quality is mocked unless ping/latency monitoring is running.
        return Task.FromResult(new ConnectionQuality { Status = "Good", LatencyMs = 15, PacketLossPercentage = 0 });
    }

    public async Task<TrafficTimeline> GetTrafficTimelineAsync(DateTime startUtc, DateTime endUtc, string resolution)
    {
        var totalSecs = (endUtc - startUtc).TotalSeconds;
        if (totalSecs <= 120 && resolution == "1s")
        {
            return _liveBuffer.GetTimeline(startUtc, endUtc);
        }

        using var connection = _dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<dynamic>(@"
            SELECT bucket_utc, SUM(download_bytes) as Download, SUM(upload_bytes) as Upload
            FROM traffic_minute
            WHERE bucket_utc >= @Start AND bucket_utc <= @End
            GROUP BY bucket_utc
            ORDER BY bucket_utc ASC
        ", new { Start = startUtc.ToString("o"), End = endUtc.ToString("o") });

        var timeline = new TrafficTimeline();
        foreach (var row in rows)
        {
            timeline.Points.Add(new TrafficTimelinePoint
            {
                TimestampUtc = DateTime.Parse(row.bucket_utc),
                DownloadBytes = row.Download ?? 0,
                UploadBytes = row.Upload ?? 0
            });
        }
        return timeline;
    }

    public async Task<List<TopUsageEntry>> GetTopApplicationsAsync(DateTime startUtc, DateTime endUtc, int limit)
    {
        using var connection = _dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<TopUsageEntry>(@"
            SELECT 
                application_id as EntityId, 
                application_id as DisplayName, 
                application_id as ProcessName,
                SUM(download_bytes) as DownloadBytes,
                SUM(upload_bytes) as UploadBytes,
                SUM(download_bytes + upload_bytes) as TotalBytes
            FROM traffic_minute
            WHERE bucket_utc >= @Start AND bucket_utc <= @End AND application_id IS NOT NULL
            GROUP BY application_id
        ", new { Start = startUtc.ToString("o"), End = endUtc.ToString("o") });
        
        var dbList = rows.ToList();
        var unflushed = _minuteAggregator.GetUnflushedAppVolumes();

        var merged = new Dictionary<string, TopUsageEntry>();
        foreach (var r in dbList)
        {
            merged[r.EntityId] = r;
        }

        foreach (var un in unflushed)
        {
            if (merged.TryGetValue(un.EntityId, out var existing))
            {
                existing.DownloadBytes += un.DownloadBytes;
                existing.UploadBytes += un.UploadBytes;
                existing.TotalBytes += un.TotalBytes;
            }
            else
            {
                merged[un.EntityId] = un;
            }
        }

        return merged.Values
            .OrderByDescending(x => x.TotalBytes)
            .Take(limit)
            .ToList();
    }

    public Task<List<NetworkUsage>> GetNetworkUsageAsync(DateTime startUtc, DateTime endUtc)
    {
        throw new NotImplementedException();
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
