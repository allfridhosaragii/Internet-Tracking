namespace InternetTracer.Data;

using InternetTracer.Core.Models;
using Dapper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class MinuteAggregator
{
    private readonly DatabaseFactory _dbFactory;
    private readonly ConcurrentDictionary<DateTime, ConcurrentBag<TrafficSample>> _buckets = new();

    public MinuteAggregator(DatabaseFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public void AddSample(TrafficSample sample)
    {
        // Truncate to minute
        var bucketTime = new DateTime(sample.TimestampUtc.Year, sample.TimestampUtc.Month, sample.TimestampUtc.Day, sample.TimestampUtc.Hour, sample.TimestampUtc.Minute, 0, DateTimeKind.Utc);
        
        var bag = _buckets.GetOrAdd(bucketTime, _ => new ConcurrentBag<TrafficSample>());
        bag.Add(sample);
    }

    // Flushes all buckets that are strictly older than the specified current UTC time.
    public async Task FlushOlderThanAsync(DateTime currentUtc)
    {
        // Safe to flush if the minute has entirely passed
        var safeToFlushThreshold = new DateTime(currentUtc.Year, currentUtc.Month, currentUtc.Day, currentUtc.Hour, currentUtc.Minute, 0, DateTimeKind.Utc);

        var keysToFlush = _buckets.Keys.Where(k => k < safeToFlushThreshold).ToList();

        if (!keysToFlush.Any()) return;

        var aggregates = new List<TrafficMinuteAggregate>();

        foreach (var key in keysToFlush)
        {
            if (_buckets.TryRemove(key, out var bag))
            {
                var samples = bag.ToList();
                if (!samples.Any()) continue;

                var groups = samples.GroupBy(s => s.InterfaceId);
                foreach (var g in groups)
                {
                    aggregates.Add(new TrafficMinuteAggregate
                    {
                        BucketUtc = key,
                        InterfaceId = g.Key,
                        DownloadBytes = g.Sum(x => x.BytesReceived),
                        UploadBytes = g.Sum(x => x.BytesSent),
                        SampleCount = g.Count(),
                        AttributionState = AttributionState.Unattributed
                    });
                }
            }
        }

        if (!aggregates.Any()) return;

        using var connection = _dbFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var agg in aggregates)
        {
            await connection.ExecuteAsync(@"
                INSERT INTO traffic_minute (bucket_utc, interface_id, download_bytes, upload_bytes, sample_count, attribution_state)
                VALUES (@BucketUtc, @InterfaceId, @DownloadBytes, @UploadBytes, @SampleCount, @AttributionState)
                ON CONFLICT(bucket_utc, interface_id) DO UPDATE SET
                    download_bytes = traffic_minute.download_bytes + @DownloadBytes,
                    upload_bytes = traffic_minute.upload_bytes + @UploadBytes,
                    sample_count = traffic_minute.sample_count + @SampleCount;
            ", new 
            {
                BucketUtc = agg.BucketUtc.ToString("o"), 
                agg.InterfaceId,
                agg.DownloadBytes,
                agg.UploadBytes,
                agg.SampleCount,
                AttributionState = (int)agg.AttributionState
            }, transaction);
        }

        transaction.Commit();
    }
}
