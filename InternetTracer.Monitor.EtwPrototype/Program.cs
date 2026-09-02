using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Linq;

namespace InternetTracer.Monitor.EtwPrototype;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("ETW Attribution Coverage & Performance Prototype");
        
        if (!(TraceEventSession.IsElevated() ?? false))
        {
            Console.WriteLine("Error: Please run as Administrator.");
            return;
        }

        using var session = new TraceEventSession("InternetTracer-Etw-Prototype");
        
        Console.CancelKeyPress += (s, e) => 
        {
            session.Dispose();
            e.Cancel = true;
        };

        session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
        
        var parser = session.Source.Kernel;

        var etwRx = 0L;
        var etwTx = 0L;
        var unattributedRx = 0L;
        var unattributedTx = 0L;

        parser.TcpIpRecv += data => 
        {
            System.Threading.Interlocked.Add(ref etwRx, data.size);
            if (data.ProcessID == 0) System.Threading.Interlocked.Add(ref unattributedRx, data.size);
        };
        
        parser.TcpIpSend += data => 
        {
            System.Threading.Interlocked.Add(ref etwTx, data.size);
            if (data.ProcessID == 0) System.Threading.Interlocked.Add(ref unattributedTx, data.size);
        };

        parser.UdpIpRecv += data =>
        {
             System.Threading.Interlocked.Add(ref etwRx, data.size);
             if (data.ProcessID == 0) System.Threading.Interlocked.Add(ref unattributedRx, data.size);
        };

        parser.UdpIpSend += data =>
        {
             System.Threading.Interlocked.Add(ref etwTx, data.size);
             if (data.ProcessID == 0) System.Threading.Interlocked.Add(ref unattributedTx, data.size);
        };

        Task.Run(async () => 
        {
            var initialInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Select(i => i.GetIPStatistics())
                .ToList();
                
            long initialRx = initialInterfaces.Sum(i => i.BytesReceived);
            long initialTx = initialInterfaces.Sum(i => i.BytesSent);

            while (true)
            {
                await Task.Delay(5000);
                
                var currentInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Select(i => i.GetIPStatistics())
                    .ToList();
                    
                long currentRx = currentInterfaces.Sum(i => i.BytesReceived);
                long currentTx = currentInterfaces.Sum(i => i.BytesSent);
                
                long interfaceRxDelta = currentRx - initialRx;
                long interfaceTxDelta = currentTx - initialTx;
                
                long currentEtwRx = System.Threading.Interlocked.Exchange(ref etwRx, 0);
                long currentEtwTx = System.Threading.Interlocked.Exchange(ref etwTx, 0);
                long currentUnattrRx = System.Threading.Interlocked.Exchange(ref unattributedRx, 0);
                long currentUnattrTx = System.Threading.Interlocked.Exchange(ref unattributedTx, 0);

                initialRx = currentRx;
                initialTx = currentTx;
                
                double rxCoverage = interfaceRxDelta > 0 ? (currentEtwRx / (double)interfaceRxDelta) * 100 : 100;
                double txCoverage = interfaceTxDelta > 0 ? (currentEtwTx / (double)interfaceTxDelta) * 100 : 100;
                
                long attributedRx = currentEtwRx - currentUnattrRx;
                
                Console.WriteLine($"[5s Window] Interface RX: {interfaceRxDelta / 1024.0:F2} KB | ETW RX: {currentEtwRx / 1024.0:F2} KB (Coverage: {rxCoverage:F1}%) | Attributed: {attributedRx / 1024.0:F2} KB");
                
                using (var process = Process.GetCurrentProcess())
                {
                    Console.WriteLine($"            Memory: {process.WorkingSet64 / 1024 / 1024} MB");
                }
            }
        });

        Console.WriteLine("Listening to Kernel Network events. Press Ctrl+C to stop.");
        session.Source.Process();
    }
}
