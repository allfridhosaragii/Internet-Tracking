namespace InternetTracer.Tests;

using InternetTracer.Monitor;
using InternetTracer.Data;
using InternetTracer.Core.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Dapper;
using Microsoft.Data.Sqlite;

public class TelemetryIntegrityTests : IDisposable
{
    private readonly DatabaseFactory _dbFactory;
    private readonly string _dbPath;

    public TelemetryIntegrityTests()
    {
        _dbPath = $"test_db_{Guid.NewGuid()}.sqlite";
        _dbFactory = new DatabaseFactory(_dbPath);
        
        var migrator = new SchemaMigrationEngine(_dbFactory);
        migrator.Migrate();
    }

    [Fact]
    public async Task Aggregator_MaintainsExactByteConservation()
    {
        // 1. Arrange
        var calculator = new TrafficDeltaCalculator();
        var aggregator = new MinuteAggregator(_dbFactory);
        
        var baseTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        
        long totalSimulatedRx = 0;
        long totalSimulatedTx = 0;
        
        long currentRxCounter = 1000;
        long currentTxCounter = 2000;

        // Simulate a 5-minute period (300 seconds)
        for (int i = 0; i < 300; i++)
        {
            var currentTime = baseTime.AddSeconds(i);
            
            long rxIncrease = 100 + (i % 50); // Variable traffic
            long txIncrease = 50 + (i % 25);
            
            currentRxCounter += rxIncrease;
            currentTxCounter += txIncrease;

            // Generate raw state
            var state = new NetworkInterfaceInfo 
            { 
                Id = "test-interface", 
                BytesReceived = currentRxCounter, 
                BytesSent = currentTxCounter 
            };

            // Calculate delta
            var deltas = calculator.CalculateDeltas(new[] { state }, currentTime).ToList();
            
            foreach (var delta in deltas)
            {
                totalSimulatedRx += delta.BytesReceived;
                totalSimulatedTx += delta.BytesSent;
                aggregator.AddSample(delta);
            }
        }

        // 2. Act
        // Flush everything older than 12:10 (which covers all 300 seconds up to 12:04:59)
        await aggregator.FlushOlderThanAsync(baseTime.AddMinutes(10));

        // 3. Assert
        using var conn = _dbFactory.CreateConnection();
        var dbAggregates = conn.Query<TrafficMinuteAggregate>("SELECT download_bytes as DownloadBytes, upload_bytes as UploadBytes FROM traffic_minute").ToList();
        
        long dbTotalRx = dbAggregates.Sum(x => x.DownloadBytes);
        long dbTotalTx = dbAggregates.Sum(x => x.UploadBytes);

        // Core telemetry integrity check. Not a single byte should be lost.
        Assert.Equal(totalSimulatedRx, dbTotalRx);
        Assert.Equal(totalSimulatedTx, dbTotalTx);
    }

    [Fact]
    public async Task Aggregator_RespectsExactMinuteBoundaries()
    {
        var aggregator = new MinuteAggregator(_dbFactory);
        
        var time120059 = new DateTime(2025, 1, 1, 12, 0, 59, DateTimeKind.Utc);
        var time120100 = new DateTime(2025, 1, 1, 12, 1, 0, DateTimeKind.Utc);
        
        var sample1 = new TrafficSample { TimestampUtc = time120059, InterfaceId = "if1", BytesReceived = 100 };
        var sample2 = new TrafficSample { TimestampUtc = time120100, InterfaceId = "if1", BytesReceived = 200 };
        
        aggregator.AddSample(sample1);
        aggregator.AddSample(sample2);
        
        // Flush up to 12:02 (safe to flush 12:00 and 12:01 buckets)
        await aggregator.FlushOlderThanAsync(new DateTime(2025, 1, 1, 12, 2, 0, DateTimeKind.Utc));
        
        using var conn = _dbFactory.CreateConnection();
        var aggregates = conn.Query<TrafficMinuteAggregate>("SELECT bucket_utc as BucketUtc, download_bytes as DownloadBytes FROM traffic_minute ORDER BY bucket_utc").ToList();
        
        Assert.Equal(2, aggregates.Count);
        
        // 12:00:59 belongs in the 12:00 bucket
        Assert.Equal(new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), aggregates[0].BucketUtc.ToUniversalTime());
        Assert.Equal(100, aggregates[0].DownloadBytes);
        
        // 12:01:00 belongs in the 12:01 bucket
        Assert.Equal(new DateTime(2025, 1, 1, 12, 1, 0, DateTimeKind.Utc), aggregates[1].BucketUtc.ToUniversalTime());
        Assert.Equal(200, aggregates[1].DownloadBytes);
    }

    public void Dispose()
    {
        // Need to clear pool before file can be deleted on windows
        SqliteConnection.ClearAllPools();
        
        if (System.IO.File.Exists(_dbPath))
        {
            System.IO.File.Delete(_dbPath);
        }
    }
}
