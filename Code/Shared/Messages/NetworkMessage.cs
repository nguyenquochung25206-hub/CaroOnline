using System;
using System.Text.Json.Serialization;

namespace Caro.Shared.Messages;

public sealed class NetworkMessage
{
    public string Type { get; set; } = "";
    // Mã định danh để phân biệt và đối chiếu message.
    [JsonRequired]
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    // Thời điểm tạo message.
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    // Dữ liệu đi kèm message (tùy thuộc vào Type)
    public object? Payload { get; set; }
}
public class CreateRoomPayload
{
    public string PlayerName { get; set; } = "";
    public string HostName { get; set; } = "";
    public string RoomName { get; set; } = "";
    public int BoardSize { get; set; } = 15;
}

public class JoinRoomPayload
{
    public string RoomId { get; set; } = "";
    public string PlayerName { get; set; } = "";
}

public sealed class RoomCreatedPayload
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string HostName { get; set; } = "";
}

public sealed class RoomJoinedPayload
{
    public string RoomId { get; set; } = "";
    public string HostName { get; set; } = "";
    public string GuestName { get; set; } = "";
}

public sealed class MovePayload
{
    public string RoomId { get; set; } = "";
    public int Row { get; set; }
    public int Col { get; set; }

    public string PlayerName { get; set; } = "";
}

public sealed class BoardUpdatedPayload
{
    public string RoomId { get; set; } = "";
    public int Row { get; set; }
    public int Col { get; set; }
    public string Symbol { get; set; } = "";
    public string CurrentTurn { get; set; } = "";
}

public sealed class ChatPayload
{
    public string RoomId { get; set; } = "";
    public string Sender { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime? Timestamp { get; set; }
}

public sealed class ErrorPayload
{
    public string ErrorCode { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class GameOverPayload
{
    public string RoomId { get; set; } = "";
    public string WinnerSymbol { get; set; } = "";
    public string WinnerName { get; set; } = "";
    public string Reason { get; set; } = "";
}
