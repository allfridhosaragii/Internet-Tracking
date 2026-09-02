namespace InternetTracer.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using InternetTracer.Data;
using InternetTracer.Core.Models;
using Dapper;

public class MinuteAggregatorTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseFactory _dbFactory;

    public MinuteAggregatorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid()}.db");
        _dbFactory = new DatabaseFactory(_dbPath);

        // Run migrations
        var engine = new SchemaMigrationEngine(_dbFactory);
        engine.Migrate();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* Ignore */ }
        }
    }

    [Fact]
    public async Task Aggregation_Conservaton_SumsCorrectly()
    {
        var aggregator = new MinuteAggregator(_dbFactory);
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Add 60 samples in the same minute
        for (int i = 0; i < 60; i++)
        {
            aggregator.AddSample(new TrafficSample
            {
                TimestampUtc = baseTime.AddSeconds(i),
                InterfaceId = "eth0",
                ApplicationId = null, // Unattributed
                BytesReceived = 100,
                BytesSent = 50
            });
        }

        // Flush items older than 12:02:00 UTC
        await aggregator.FlushOlderThanAsync(baseTime.AddMinutes(2));

        // Verify in DB
        using var conn = _dbFactory.CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT download_bytes, upload_bytes, sample_count FROM traffic_minute WHERE interface_id = 'eth0'"
        );

        Assert.NotNull(row);
        Assert.Equal(60 * 100, (long)row.download_bytes);
        Assert.Equal(60 * 50, (long)row.upload_bytes);
        Assert.Equal(60, (long)row.sample_count);
    }

    [Fact]
    public async Task Flush_DoesNotFlush_ActiveMinute()
    {
        var aggregator = new MinuteAggregator(_dbFactory);
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 10, DateTimeKind.Utc);

        aggregator.AddSample(new TrafficSample
        {
            TimestampUtc = baseTime,
            InterfaceId = "eth0",
            BytesReceived = 100,
            BytesSent = 50
        });

        // Flush items older than 12:00:30 (same minute)
        await aggregator.FlushOlderThanAsync(baseTime.AddSeconds(20));

        // Verify DB is empty because 12:00:00 bucket is not older than 12:00:30 (they truncate to 12:00:00). Wait, Flush threshold truncates:
        // var safeToFlushThreshold = new DateTime(currentUtc... Minute, 0);
        // Current UTC = 12:00:30 -> threshold = 12:00:00. Keys < 12:00:00 will be flushed.
        // The bucket is 12:00:00. So it is not strictly less than 12:00:00.

        using var conn = _dbFactory.CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM traffic_minute");
        Assert.Equal(0, count);

        // Now flush with current time 12:01:00. Threshold = 12:01:00. Bucket 12:00:00 < 12:01:00.
        await aggregator.FlushOlderThanAsync(baseTime.AddSeconds(50));
        count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM traffic_minute");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Attribution_Correctness_EnforcedInDatabase()
    {
        var aggregator = new MinuteAggregator(_dbFactory);
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // 100 bytes total, 80 attributed, 20 unattributed
        aggregator.AddSample(new TrafficSample
        {
            TimestampUtc = baseTime,
            InterfaceId = "eth0",
            ApplicationId = "chrome.exe",
            BytesReceived = 80,
            BytesSent = 40
        });

        aggregator.AddSample(new TrafficSample
        {
            TimestampUtc = baseTime,
            InterfaceId = "eth0",
            ApplicationId = null,
            BytesReceived = 20,
            BytesSent = 10
        });

        await aggregator.FlushOlderThanAsync(baseTime.AddMinutes(2));

        using var conn = _dbFactory.CreateConnection();
        var rows = await conn.QueryAsync<dynamic>(
            "SELECT application_id, download_bytes FROM traffic_minute"
        );

        Assert.Equal(2, rows.AsList().Count);

        long sumDownload = 0;
        foreach (var r in rows)
        {
            sumDownload += (long)r.download_bytes;
        }

        Assert.Equal(100, sumDownload);
    }
}
