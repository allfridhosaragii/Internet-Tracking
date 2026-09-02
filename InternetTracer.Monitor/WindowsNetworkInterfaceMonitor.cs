namespace InternetTracer.Monitor;

using InternetTracer.Core.Models;
using System.Collections.Generic;
using System.Net.NetworkInformation;

public class WindowsNetworkInterfaceMonitor : INetworkInterfaceMonitor
{
    public IEnumerable<NetworkInterfaceInfo> GetInterfaces()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();
        var result = new List<NetworkInterfaceInfo>();

        foreach (var ni in interfaces)
        {
            var stats = ni.GetIPStatistics();
            result.Add(new NetworkInterfaceInfo
            {
                Id = ni.Id,
                SystemGuid = ni.Id,
                Name = ni.Name,
                Type = ni.NetworkInterfaceType.ToString(),
                Description = ni.Description,
                OperationalState = ni.OperationalStatus.ToString(),
                BytesReceived = stats.BytesReceived,
                BytesSent = stats.BytesSent
            });
        }

        return result;
    }
}
