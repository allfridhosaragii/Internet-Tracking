namespace InternetTracer.Ipc;

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InternetTracer.Core.Contracts;

public class IpcServer
{
    private const string PipeName = "InternetTracerTelemetryPipe";
    private readonly ITelemetryServiceApi _apiImplementation;
    private CancellationTokenSource? _cts;

    public IpcServer(ITelemetryServiceApi apiImplementation)
    {
        _apiImplementation = apiImplementation;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => ServerLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    private async Task ServerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Implement strict ACL: Only Interactive User and Admins can connect.
                PipeSecurity pipeSecurity = new PipeSecurity();
                
                var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                pipeSecurity.AddAccessRule(new PipeAccessRule(adminSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
                
                var interactiveSid = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);
                pipeSecurity.AddAccessRule(new PipeAccessRule(interactiveSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
                
                var networkSid = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
                pipeSecurity.AddAccessRule(new PipeAccessRule(networkSid, PipeAccessRights.FullControl, AccessControlType.Deny));
                
                var anonymousSid = new SecurityIdentifier(WellKnownSidType.AnonymousSid, null);
                pipeSecurity.AddAccessRule(new PipeAccessRule(anonymousSid, PipeAccessRights.FullControl, AccessControlType.Deny));

                // Wait, NamedPipeServerStream constructor with PipeSecurity requires ACL configuration.
                // In .NET 6+, NamedPipeServerStreamAcl is used.
                using var pipeServer = NamedPipeServerStreamAcl.Create(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous,
                    0, 0, pipeSecurity);

                await pipeServer.WaitForConnectionAsync(token);

                // Handle connection in background, allowing multiple clients
                _ = Task.Run(() => HandleClientAsync(pipeServer, token), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server loop error: {ex.Message}");
                await Task.Delay(1000, token); // Backoff on error
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipeServer, CancellationToken token)
    {
        using (pipeServer)
        using (var reader = new StreamReader(pipeServer))
        using (var writer = new StreamWriter(pipeServer) { AutoFlush = true })
        {
            try
            {
                while (pipeServer.IsConnected && !token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(token);
                    if (line == null) break;

                    var request = JsonSerializer.Deserialize<IpcRequest>(line);
                    if (request == null) continue;

                    var response = await ProcessRequestAsync(request);
                    
                    var responseJson = JsonSerializer.Serialize(response);
                    await writer.WriteLineAsync(responseJson);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client handling error: {ex.Message}");
            }
        }
    }

    private async Task<IpcResponse> ProcessRequestAsync(IpcRequest request)
    {
        var response = new IpcResponse { RequestId = request.RequestId };
        
        try
        {
            switch (request.Operation)
            {
                case "GetDashboardSummary":
                    var summary = await _apiImplementation.GetDashboardSummaryAsync();
                    response.Payload = JsonSerializer.SerializeToElement(summary);
                    break;
                // Add other operations...
                default:
                    response.StatusCode = 404;
                    response.ErrorCode = "UnknownOperation";
                    break;
            }
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            response.ErrorCode = "InternalServerError";
            // Do not leak stack traces over IPC
        }

        return response;
    }
}
