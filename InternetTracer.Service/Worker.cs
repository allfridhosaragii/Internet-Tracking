namespace InternetTracer.Service;

using InternetTracer.Data;
using InternetTracer.Monitor;
using InternetTracer.Core.Models;
using InternetTracer.Ipc;
using System.Collections.Concurrent;
using System.Diagnostics;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly WindowsNetworkInterfaceMonitor _interfaceMonitor;
    private readonly TrafficDeltaCalculator _deltaCalculator;
    private readonly MinuteAggregator _minuteAggregator;
    private readonly IpcServer _ipcServer;
    private EtwKernelTraceMonitor? _etwMonitor;

    private readonly ConcurrentDictionary<int, (long rx, long tx)> _pidAccumulators = new();

    public Worker(
        ILogger<Worker> logger, 
        WindowsNetworkInterfaceMonitor interfaceMonitor,
        TrafficDeltaCalculator deltaCalculator, 
        MinuteAggregator minuteAggregator,
        IpcServer ipcServer)
    {
        _logger = logger;
        _interfaceMonitor = interfaceMonitor;
        _deltaCalculator = deltaCalculator;
        _minuteAggregator = minuteAggregator;
        _ipcServer = ipcServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InternetTracer Service starting...");

        _ipcServer.Start();
        
        try
        {
            _etwMonitor = new EtwKernelTraceMonitor((pid, rx, tx) => 
            {
                _pidAccumulators.AddOrUpdate(pid, (rx, tx), (k, existing) => (existing.rx + rx, existing.tx + tx));
            });
            _etwMonitor.Start();
            _logger.LogInformation("ETW Kernel Monitor started.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ETW Monitor. Running in degraded mode.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
            
            try 
            {
                var interfaces = _interfaceMonitor.GetInterfaces();
                var deltas = _deltaCalculator.CalculateDeltas(interfaces, DateTime.UtcNow);
                
                // For Phase 1, we just take the highest traffic PID and attribute everything. 
                // A true implementation matches ETW sums to interface totals proportionally.
                // We'll extract the PIDs, sum them up, and map to ProcessNames.
                
                var pidsToProcess = _pidAccumulators.Keys.ToList();
                var processTraffic = new Dictionary<string, (long rx, long tx)>();
                
                foreach (var pid in pidsToProcess)
                {
                    if (_pidAccumulators.TryRemove(pid, out var traffic))
                    {
                        var name = GetProcessNameSafely(pid);
                        if (!processTraffic.ContainsKey(name)) processTraffic[name] = (0, 0);
                        processTraffic[name] = (processTraffic[name].rx + traffic.rx, processTraffic[name].tx + traffic.tx);
                    }
                }

                foreach (var delta in deltas)
                {
                    // Default to unattributed if no process traffic
                    if (!processTraffic.Any())
                    {
                         _minuteAggregator.AddSample(new TrafficSample 
                         {
                             TimestampUtc = DateTime.UtcNow,
                             InterfaceId = delta.InterfaceId,
                             BytesReceived = delta.BytesReceived,
                             BytesSent = delta.BytesSent
                         });
                         continue;
                    }

                    // Simple proportional attribution
                    var totalEtwRx = processTraffic.Values.Sum(x => x.rx) + 1; // +1 to avoid div by zero
                    var totalEtwTx = processTraffic.Values.Sum(x => x.tx) + 1;
                    
                    foreach (var pt in processTraffic)
                    {
                        var appRx = (long)((pt.Value.rx / (double)totalEtwRx) * delta.BytesReceived);
                        var appTx = (long)((pt.Value.tx / (double)totalEtwTx) * delta.BytesSent);
                        
                        if (appRx > 0 || appTx > 0)
                        {
                            _minuteAggregator.AddSample(new TrafficSample 
                            {
                                TimestampUtc = DateTime.UtcNow,
                                InterfaceId = delta.InterfaceId,
                                ApplicationId = pt.Key,
                                BytesReceived = appRx,
                                BytesSent = appTx
                            });
                        }
                    }
                }
                
                // Flush memory to SQLite for items older than 2 minutes
                await _minuteAggregator.FlushOlderThanAsync(DateTime.UtcNow.AddMinutes(-2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in collection loop");
            }
        }
        
        _etwMonitor?.Dispose();
        _logger.LogInformation("InternetTracer Service stopped.");
    }

    private string GetProcessNameSafely(int pid)
    {
        if (pid == 0) return "System Idle Process";
        if (pid == 4) return "System";
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch
        {
            return $"Unknown (PID {pid})";
        }
    }
}
