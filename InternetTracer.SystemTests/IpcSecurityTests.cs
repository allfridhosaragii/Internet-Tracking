namespace InternetTracer.SystemTests;

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;
using InternetTracer.Ipc;

public class IpcSecurityTests
{
    private const string PipeName = "InternetTracerTelemetryPipe";

    public static async Task RunAllTestsAsync()
    {
        Console.WriteLine("--- STARTING IPC RED TEAM SUITE ---");
        
        await TestAuthorizedConnectionAsync();
        await TestInvalidJsonAsync();
        await TestEmptyPayloadAsync();
        await TestOversizedFrameAsync();
        
        Console.WriteLine("--- IPC RED TEAM SUITE COMPLETED ---");
    }

    private static async Task TestAuthorizedConnectionAsync()
    {
        Console.WriteLine("Test: Authorized Connection");
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipeClient.ConnectAsync(2000);

            using var writer = new StreamWriter(pipeClient) { AutoFlush = true };
            using var reader = new StreamReader(pipeClient);

            var req = new IpcRequest { RequestId = "test-1", Operation = "GetDashboardSummary" };
            await writer.WriteLineAsync(JsonSerializer.Serialize(req));
            
            var responseJson = await reader.ReadLineAsync();
            Console.WriteLine($"  [PASS] Authorized connection succeeded. Response: {responseJson?.Substring(0, Math.Min(responseJson?.Length ?? 0, 50))}...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [FAIL] Authorized connection failed: {ex.Message}");
        }
    }

    private static async Task TestInvalidJsonAsync()
    {
        Console.WriteLine("Test: Invalid JSON");
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipeClient.ConnectAsync(2000);

            using var writer = new StreamWriter(pipeClient) { AutoFlush = true };
            using var reader = new StreamReader(pipeClient);

            await writer.WriteLineAsync("{ invalid_json: ");
            
            // Wait for response or disconnect
            var response = await reader.ReadLineAsync();
            if (response == null)
            {
                Console.WriteLine("  [PASS] Server dropped connection on invalid JSON.");
            }
            else
            {
                Console.WriteLine($"  [FAIL] Server responded instead of dropping/handling gracefully: {response}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [FAIL] Exception during test: {ex.Message}");
        }
    }

    private static async Task TestEmptyPayloadAsync()
    {
        Console.WriteLine("Test: Empty Payload");
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipeClient.ConnectAsync(2000);

            using var writer = new StreamWriter(pipeClient) { AutoFlush = true };
            using var reader = new StreamReader(pipeClient);

            await writer.WriteLineAsync(""); // Empty line
            
            var response = await reader.ReadLineAsync();
            if (response == null)
            {
                Console.WriteLine("  [PASS] Server dropped connection on empty payload.");
            }
            else
            {
                Console.WriteLine($"  [FAIL] Server responded: {response}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [FAIL] Exception during test: {ex.Message}");
        }
    }

    private static async Task TestOversizedFrameAsync()
    {
        Console.WriteLine("Test: Oversized Frame (10MB)");
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipeClient.ConnectAsync(2000);

            using var writer = new StreamWriter(pipeClient) { AutoFlush = true };
            using var reader = new StreamReader(pipeClient);

            string hugeString = new string('A', 10 * 1024 * 1024);
            var req = new IpcRequest { RequestId = "test-oversized", Operation = "Unknown", Payload = JsonSerializer.SerializeToElement(hugeString) };
            
            await writer.WriteLineAsync(JsonSerializer.Serialize(req));
            
            var response = await reader.ReadLineAsync();
            if (response == null) {
                Console.WriteLine($"  [PASS] Server dropped the connection when sending oversized frame.");
            } else {
                Console.WriteLine($"  [NOT PROVEN/FAIL] Server processed oversized frame! Response: {response.Substring(0, Math.Min(response.Length, 50))}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [PASS] Server rejected oversized frame or threw appropriately: {ex.Message}");
        }
    }
}
