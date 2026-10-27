using System;

namespace Caro.Shared.Messages;

public sealed class NetworkMessage
{
    public string Type { get; set; } = "";
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public object? Payload { get; set; }
}
