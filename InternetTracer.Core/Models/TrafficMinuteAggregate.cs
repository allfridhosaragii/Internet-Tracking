namespace InternetTracer.Core.Models;
using System;

public class TrafficMinuteAggregate
{
    public DateTime BucketUtc { get; set; }
    public string InterfaceId { get; set; } = string.Empty;
    public string? NetworkId { get; set; }
    public string? ApplicationId { get; set; }
    public long DownloadBytes { get; set; }
    public long UploadBytes { get; set; }
    public int SampleCount { get; set; }
    public AttributionState AttributionState { get; set; }
}
