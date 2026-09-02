using InternetTracer.Core.Contracts;
using InternetTracer.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace InternetTracer.Data;

public class LiveTelemetryBuffer
{
    private readonly int _maxSeconds;
    private readonly ConcurrentQueue<TrafficTimelinePoint> _timeline = new();
    
    private readonly object _lock = new();
    private CurrentSnapshot _currentSnapshot = new();

    public LiveTelemetryBuffer(int maxSeconds = 60)
    {
        _maxSeconds = maxSeconds;
    }

    public void AddSnapshot(TrafficSample sample)
    {
        lock (_lock)
        {
            // Update the live snapshot with the latest 1-second data
            _currentSnapshot = new CurrentSnapshot
            {
                CurrentDownloadBytesPerSec = sample.BytesReceived,
                CurrentUploadBytesPerSec = sample.BytesSent,
                ActiveConnections = _currentSnapshot.ActiveConnections // preserve if we track it later
            };

            // Maintain the 60-second sliding window
            _timeline.Enqueue(new TrafficTimelinePoint
            {
                TimestampUtc = sample.TimestampUtc,
                DownloadBytes = sample.BytesReceived,
                UploadBytes = sample.BytesSent
            });

            // Prune old entries
            var cutoff = DateTime.UtcNow.AddSeconds(-_maxSeconds);
            while (_timeline.TryPeek(out var oldest) && oldest.TimestampUtc < cutoff)
            {
                _timeline.TryDequeue(out _);
            }
        }
    }

    public CurrentSnapshot GetCurrentSnapshot()
    {
        lock (_lock)
        {
            return new CurrentSnapshot
            {
                CurrentDownloadBytesPerSec = _currentSnapshot.CurrentDownloadBytesPerSec,
                CurrentUploadBytesPerSec = _currentSnapshot.CurrentUploadBytesPerSec,
                ActiveConnections = _currentSnapshot.ActiveConnections
            };
        }
    }

    public TrafficTimeline GetTimeline(DateTime startUtc, DateTime endUtc)
    {
        var result = new TrafficTimeline();
        lock (_lock)
        {
            result.Points.AddRange(_timeline.Where(p => p.TimestampUtc >= startUtc && p.TimestampUtc <= endUtc));
        }
        return result;
    }
}
