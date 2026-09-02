namespace InternetTracer.Ipc;

using System;
using System.Text.Json;

public class IpcRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string Version { get; set; } = "1.0";
    public string Operation { get; set; } = string.Empty;
    public JsonElement? Payload { get; set; }
}

public class IpcResponse
{
    public string RequestId { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public int StatusCode { get; set; } = 200;
    public string? ErrorCode { get; set; }
    public JsonElement? Payload { get; set; }
}
