namespace InternetTracer.Ipc;

using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InternetTracer.Core.Contracts;
using System.Collections.Generic;

public class IpcClient : ITelemetryServiceApi, IDisposable
{
    private const string PipeName = "InternetTracerTelemetryPipe";
    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task ConnectAsync(CancellationToken token = default)
    {
        if (_pipeClient != null && _pipeClient.IsConnected) return;

        await _lock.WaitAsync(token);
        try
        {
            if (_pipeClient != null && _pipeClient.IsConnected) return;

            _pipeClient?.Dispose();
            _pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            
            await _pipeClient.ConnectAsync(5000, token);
            _pipeClient.ReadMode = PipeTransmissionMode.Message;
            
            _reader = new StreamReader(_pipeClient);
            _writer = new StreamWriter(_pipeClient) { AutoFlush = true };
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<T> SendRequestAsync<T>(string operation, object? payload = null)
    {
        await ConnectAsync();

        if (_writer == null || _reader == null)
            throw new InvalidOperationException("Not connected to IPC server.");

        var request = new IpcRequest
        {
            Operation = operation,
            Payload = payload != null ? JsonSerializer.SerializeToElement(payload) : null
        };

        var requestJson = JsonSerializer.Serialize(request);
        
        await _lock.WaitAsync();
        try
        {
            await _writer.WriteLineAsync(requestJson);
            var responseJson = await _reader.ReadLineAsync();
            
            if (responseJson == null)
                throw new IOException("Server disconnected unexpectedly.");

            var response = JsonSerializer.Deserialize<IpcResponse>(responseJson);
            
            if (response == null)
                throw new IOException("Invalid response from server.");

            if (response.StatusCode != 200)
                throw new Exception($"IPC Error: {response.ErrorCode} (HTTP {response.StatusCode})");

            if (response.Payload.HasValue)
                return response.Payload.Value.Deserialize<T>()!;
                
            return default!;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<DashboardSummary> GetDashboardSummaryAsync() => SendRequestAsync<DashboardSummary>("GetDashboardSummary");

    public Task<TrafficTimeline> GetTrafficTimelineAsync(DateTime startUtc, DateTime endUtc, string resolution)
    {
        return SendRequestAsync<TrafficTimeline>("GetTrafficTimeline", new { startUtc, endUtc, resolution });
    }

    public Task<List<TopUsageEntry>> GetTopApplicationsAsync(DateTime startUtc, DateTime endUtc, int limit)
    {
        return SendRequestAsync<List<TopUsageEntry>>("GetTopApplications", new { startUtc, endUtc, limit });
    }

    public Task<List<NetworkUsage>> GetNetworkUsageAsync(DateTime startUtc, DateTime endUtc)
    {
        return SendRequestAsync<List<NetworkUsage>>("GetNetworkUsage", new { startUtc, endUtc });
    }

    public Task<ApplicationUsage> GetApplicationUsageAsync(string applicationId, DateTime startUtc, DateTime endUtc)
    {
         return SendRequestAsync<ApplicationUsage>("GetApplicationUsage", new { applicationId, startUtc, endUtc });
    }

    public async Task<List<ConnectionEvent>> GetConnectionEventsAsync(int limit)
    {
        return await SendRequestAsync<List<ConnectionEvent>>("GetConnectionEvents", new { Limit = limit }) 
               ?? new List<ConnectionEvent>();
    }

    public async Task<CurrentSnapshot> GetCurrentSnapshotAsync()
    {
        return await SendRequestAsync<CurrentSnapshot>("GetCurrentSnapshot", new { }) 
               ?? new CurrentSnapshot();
    }

    public async Task<ConnectionQuality> GetConnectionQualityAsync()
    {
        return await SendRequestAsync<ConnectionQuality>("GetConnectionQuality", new { }) 
               ?? new ConnectionQuality();
    }

    #region K16 Phase 4: Filtering and Search Support

    public async Task<List<string>> GetUniqueApplicationIdsAsync(DateTime startUtc, DateTime endUtc)
    {
        return await SendRequestAsync<List<string>>("GetUniqueApplicationIds", new { startUtc, endUtc })
               ?? new List<string>();
    }

    public async Task<List<string>> GetUniqueNetworkIdsAsync(DateTime startUtc, DateTime endUtc)
    {
        return await SendRequestAsync<List<string>>("GetUniqueNetworkIds", new { startUtc, endUtc })
               ?? new List<string>();
    }

    public Task<List<TopUsageEntry>> GetTopApplicationsFilteredAsync(DateTime startUtc, DateTime endUtc, int limit, string? appId)
    {
        return SendRequestAsync<List<TopUsageEntry>>("GetTopApplicationsFiltered", new { startUtc, endUtc, limit, appId });
    }

    public Task<List<TopUsageEntry>> GetTopApplicationsSortedAsync(DateTime startUtc, DateTime endUtc, int limit, string sortBy, bool descending)
    {
        return SendRequestAsync<List<TopUsageEntry>>("GetTopApplicationsSorted", new { startUtc, endUtc, limit, sortBy, descending });
    }

    public Task<List<NetworkUsage>> GetNetworkUsageFilteredAsync(DateTime startUtc, DateTime endUtc, string? networkId)
    {
        return SendRequestAsync<List<NetworkUsage>>("GetNetworkUsageFiltered", new { startUtc, endUtc, networkId });
    }

    public Task<List<TopUsageEntry>> SearchApplicationsAsync(DateTime startUtc, DateTime endUtc, string searchTerm, int limit)
    {
        return SendRequestAsync<List<TopUsageEntry>>("SearchApplications", new { startUtc, endUtc, searchTerm, limit });
    }

    #endregion

    public void Dispose()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _pipeClient?.Dispose();
    }
}
