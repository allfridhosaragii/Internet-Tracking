namespace InternetTracer.Core;

using System;
using System.Security.Cryptography;
using System.Text;
using System.Net.NetworkInformation;

public static class NetworkFingerprintGenerator
{
    // A network fingerprint identifies a unique network independent of the physical adapter.
    public static string GenerateHash(NetworkInterfaceType type, string gatewayAddress, string? bssid, string? ssid, string interfaceId)
    {
        string rawString;

        if (type == NetworkInterfaceType.Wireless80211 && !string.IsNullOrWhiteSpace(bssid))
        {
            // Level 1 Confidence: Wi-Fi with BSSID and SSID (Highest Confidence, survives DHCP changes)
            rawString = $"{gatewayAddress}|{bssid}|{ssid ?? "UnknownSSID"}";
        }
        else if (!string.IsNullOrWhiteSpace(gatewayAddress) && gatewayAddress != "0.0.0.0")
        {
            // Level 2 Confidence: Ethernet or Wi-Fi without BSSID. Bound to Gateway IP.
            // If the user moves to a different network with the exact same gateway IP (e.g. 192.168.1.1),
            // it will be treated as the same network. This is a known limitation of Gateway-only tracking.
            rawString = $"{type}|{gatewayAddress}";
        }
        else
        {
            // Level 3 Confidence: Captive Portal / No Gateway / VPN Virtual Adapter.
            // Fallback to the interface ID (meaning network identity == interface identity).
            rawString = $"fallback|{interfaceId}";
        }

        var rawLower = rawString.ToLowerInvariant();
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawLower));
        
        return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16).ToLowerInvariant();
    }
}
