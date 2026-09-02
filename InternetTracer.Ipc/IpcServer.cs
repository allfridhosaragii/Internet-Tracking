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
    private readonly SecurityIdentifier _authorizedClientSid;
    private CancellationTokenSource? _cts;

    public IpcServer(ITelemetryServiceApi apiImplementation, SecurityIdentifier authorizedClientSid)
    {
        _apiImplementation = apiImplementation;
        _authorizedClientSid = authorizedClientSid ?? throw new ArgumentNullException(nameof(authorizedClientSid));
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
                // Implement strict ACL: Only the specific authorized user and Admins can connect.
                PipeSecurity pipeSecurity = new PipeSecurity();
                
                var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                pipeSecurity.AddAccessRule(new PipeAccessRule(adminSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
                
                // Allow the explicitly authorized client
                pipeSecurity.AddAccessRule(new PipeAccessRule(_authorizedClientSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
                
                var networkSid = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
                pipeSecurity.AddAccessRule(new PipeAccessRule(networkSid, PipeAccessRights.FullControl, AccessControlType.Deny));
                
                var anonymousSid = new SecurityIdentifier(WellKnownSidType.AnonymousSid, null);
                pipeSecurity.AddAccessRule(new PipeAccessRule(anonymousSid, PipeAccessRights.FullControl, AccessControlType.Deny));

                // In .NET 6+, NamedPipeServerStreamAcl is used.
                using var pipeServer = NamedPipeServerStreamAcl.Create(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous,
                    0, 0, pipeSecurity);

                await pipeServer.WaitForConnectionAsync(token);

                bool isAuthorized = false;
                pipeServer.RunAsClient(() =>
                {
                    var clientIdentity = WindowsIdentity.GetCurrent();
                    if (clientIdentity.User != null && clientIdentity.User.Equals(_authorizedClientSid))
                    {
                        isAuthorized = true;
                    }
                    else if (clientIdentity.User != null && clientIdentity.Owner != null)
                    {
                        // Fallback check if user is a member of BuiltinAdministrators
                        var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                        var principal = new WindowsPrincipal(clientIdentity);
                        if (principal.IsInRole(adminSid))
                        {
                            isAuthorized = true;
                        }
                    }
                });

                if (!isAuthorized)
                {
                    Console.WriteLine("Unauthorized client rejected. Dropping connection.");
                    pipeServer.Disconnect();
                    continue;
                }

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
                case "GetCurrentSnapshot":
                    var snapshot = await _apiImplementation.GetCurrentSnapshotAsync();
                    response.Payload = JsonSerializer.SerializeToElement(snapshot);
                    break;
                case "GetConnectionQuality":
                    var quality = await _apiImplementation.GetConnectionQualityAsync();
                    response.Payload = JsonSerializer.SerializeToElement(quality);
                    break;
                // Note: Other parameterized methods (GetTrafficTimeline, etc.) require payload parsing
                // which will be fully implemented when the SQLite aggregation query layer is done.
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
