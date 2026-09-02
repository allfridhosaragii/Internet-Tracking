namespace InternetTracer.Core.Models;
using System;

public enum IdentityConfidence
{
    Low,
    Medium,
    High
}

public class NetworkIdentity
{
    public string Id { get; set; } = string.Empty; // The fingerprint hash
    public IdentityConfidence Confidence { get; set; }
    public string FallbackReason { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Ssid { get; set; }
    public string? Bssid { get; set; }
    public string? Gateway { get; set; }
    public DateTime FirstSeenUtc { get; set; }
}
