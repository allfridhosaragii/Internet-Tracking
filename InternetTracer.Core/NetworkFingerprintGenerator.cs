namespace InternetTracer.Core;

using System;
using System.Security.Cryptography;
using System.Text;

public static class NetworkFingerprintGenerator
{
    // A network fingerprint identifies a unique network (e.g., "Home Wi-Fi") independent of the physical adapter.
    public static string GenerateHash(string gatewayAddress, string bssid, string ssid)
    {
        // A stable network is defined by its gateway and BSSID. SSID is included as a fallback identifier.
        // We explicitly DO NOT include the interface GUID so that roaming adapters (or swapping laptops on the same dock)
        // correctly identify the same network.
        var rawString = $"{gatewayAddress}|{bssid}|{ssid}".ToLowerInvariant();
        
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawString));
        
        return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16).ToLowerInvariant();
    }
}
