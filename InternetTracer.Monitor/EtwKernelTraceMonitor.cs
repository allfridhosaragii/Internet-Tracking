namespace InternetTracer.Monitor;

using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Threading.Tasks;

public class EtwKernelTraceMonitor : IDisposable
{
    private TraceEventSession? _session;
    private readonly Action<int, long, long> _onTrafficObserved; // pid, rx, tx

    public EtwKernelTraceMonitor(Action<int, long, long> onTrafficObserved)
    {
        _onTrafficObserved = onTrafficObserved;
    }

    public void Start()
    {
        if (!(TraceEventSession.IsElevated() ?? false))
        {
            throw new UnauthorizedAccessException("ETW Kernel trace requires Administrator/LocalSystem privileges.");
        }

        _session = new TraceEventSession("InternetTracer-Service-Etw");
        _session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

        var parser = _session.Source.Kernel;

        parser.TcpIpRecv += data => 
        {
            _onTrafficObserved(data.ProcessID, data.size, 0);
        };
        
        parser.TcpIpSend += data => 
        {
            _onTrafficObserved(data.ProcessID, 0, data.size);
        };

        parser.UdpIpRecv += data =>
        {
            _onTrafficObserved(data.ProcessID, data.size, 0);
        };

        parser.UdpIpSend += data =>
        {
            _onTrafficObserved(data.ProcessID, 0, data.size);
        };

        Task.Run(() => 
        {
            try
            {
                _session.Source.Process();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ETW Session Error: {ex.Message}");
            }
        });
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
