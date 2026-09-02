namespace InternetTracer.Core.Models;
using System;

public class NetworkSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string InterfaceId { get; set; } = string.Empty;
    public string NetworkId { get; set; } = string.Empty;
    public DateTime ConnectedUtc { get; set; }
    public DateTime? DisconnectedUtc { get; set; }
}
