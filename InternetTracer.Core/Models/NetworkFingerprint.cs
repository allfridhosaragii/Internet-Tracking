namespace InternetTracer.Core.Models;

public class NetworkFingerprint
{
    public string NetworkId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string? Ssid { get; set; }
    public string? Bssid { get; set; }
    public string? Gateway { get; set; }
    public string? Subnet { get; set; }
    public string? InterfaceGuid { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}
