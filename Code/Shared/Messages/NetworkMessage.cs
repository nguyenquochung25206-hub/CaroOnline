using System;

namespace Caro.Shared.Messages;

public sealed class NetworkMessage
{
    public string Type { get; set; } = "";
    // Mã định danh để phân biệt và đối chiếu message.
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    // Thời điểm tạo message.
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    // Dữ liệu đi kèm message (tùy thuộc vào Type)
    public object? Payload { get; set; }
}
public class CreateRoomPayload
{
    public string PlayerName { get; set; } = "";
    public int BoardSize { get; set; } = 15;
}

public class JoinRoomPayload
{
    public string RoomId { get; set; } = "";
    public string PlayerName { get; set; } = "";
}

public class RoomStatePayload
{
    public string RoomId { get; set; } = "";
    public string PlayerX { get; set; } = "";
    public string PlayerO { get; set; } = "";
    public string CurrentTurn { get; set; } = "X";
    public bool IsPlaying { get; set; } = false;
}

public class MovePayload
{
    public string RoomId { get; set; } = "";
    public int Row { get; set; }
    public int Col { get; set; }
}

public class BoardUpdatedPayload
{
    public string RoomId { get; set; } = "";
    public string[][] Board { get; set; } = Array.Empty<string[]>();
    public int Row { get; set; }
    public int Col { get; set; }
    public string Symbol { get; set; } = "";
    public string CurrentTurn { get; set; } = "";
}

public class ErrorPayload
{
    public string ErrorCode { get; set; } = "";
    public string Message { get; set; } = "";
}

public class GameOverPayload
{
    public string RoomId { get; set; } = "";
    public string WinnerSymbol { get; set; } = "";
    public string WinnerName { get; set; } = "";
    public string Reason { get; set; } = "";
}