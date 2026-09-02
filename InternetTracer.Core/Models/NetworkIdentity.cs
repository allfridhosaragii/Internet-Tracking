namespace InternetTracer.Core.Models;
using System;

public class NetworkIdentity
{
    public string Id { get; set; } = string.Empty; // The fingerprint hash
    public string? DisplayName { get; set; }
    public string? Ssid { get; set; }
    public string? Bssid { get; set; }
    public string? Gateway { get; set; }
    public DateTime FirstSeenUtc { get; set; }
}
