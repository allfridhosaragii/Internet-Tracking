using InternetTracer.Monitor;
using InternetTracer.Core.Models;

Console.WriteLine("Internet Tracer - Console Tester");
Console.WriteLine("Monitoring interface traffic deltas...");

INetworkInterfaceMonitor monitor = new WindowsNetworkInterfaceMonitor();
var previousStats = new Dictionary<string, NetworkInterfaceInfo>();

while (true)
{
    var currentInterfaces = monitor.GetInterfaces();
    
    foreach (var ni in currentInterfaces)
    {
        if (ni.OperationalState != "Up") continue;
        
        if (previousStats.TryGetValue(ni.Id, out var previous))
        {
            long rxDelta = ni.BytesReceived - previous.BytesReceived;
            long txDelta = ni.BytesSent - previous.BytesSent;
            
            if (rxDelta > 0 || txDelta > 0)
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {ni.Name}");
                Console.WriteLine($"  ↓ {(rxDelta / 1024.0):F2} KB/s   ↑ {(txDelta / 1024.0):F2} KB/s");
            }
        }
        
        previousStats[ni.Id] = ni;
    }
    
    Thread.Sleep(1000);
}
