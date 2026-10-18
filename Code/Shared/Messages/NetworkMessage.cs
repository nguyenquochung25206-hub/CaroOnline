namespace Caro.Shared.Messages;

// TODO TV2: Chốt schema message dùng chung. Giữ schema tương thích Client/Server.
public sealed class NetworkMessage
{
    public string Type { get; set; } = "";
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public object? Payload { get; set; }
}
