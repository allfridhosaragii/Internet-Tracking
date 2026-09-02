namespace InternetTracer.Monitor;

using InternetTracer.Core.Models;
using System.Collections.Generic;

public interface INetworkInterfaceMonitor
{
    IEnumerable<NetworkInterfaceInfo> GetInterfaces();
}
