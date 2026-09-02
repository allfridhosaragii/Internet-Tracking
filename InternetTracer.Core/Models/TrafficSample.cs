namespace InternetTracer.Core.Models;

public class TrafficSample
{
    public DateTime TimestampUtc { get; set; }
    public string InterfaceId { get; set; } = string.Empty;
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
}
