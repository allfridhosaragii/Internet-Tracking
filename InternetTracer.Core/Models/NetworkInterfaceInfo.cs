namespace InternetTracer.Core.Models;

public class NetworkInterfaceInfo
{
    public string Id { get; set; } = string.Empty;
    public string SystemGuid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OperationalState { get; set; } = string.Empty;
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
}
