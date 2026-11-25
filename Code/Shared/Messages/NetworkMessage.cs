using System;
using System.Text.Json.Serialization;

namespace Caro.Shared.Messages;

public sealed class NetworkMessage
{
    public string Type { get; set; } = "";
    [JsonRequired]
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public object? Payload { get; set; }
}

public sealed class HelloPayload
{
    public string? Name { get; set; }
}

public sealed class WelcomePayload
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = "";

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public sealed class ErrorPayload
{
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class CreateRoomPayload
{
    public string PlayerName { get; set; } = "";
    public string HostName { get; set; } = "";
    public string RoomName { get; set; } = "";
    public int BoardSize { get; set; } = 15;
}

public sealed class RoomCreatedPayload
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string HostName { get; set; } = "";
}

public class JoinRoomPayload
{
    public string RoomId { get; set; } = "";
    public string PlayerName { get; set; } = "";
}

public sealed class RoomJoinedPayload
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string HostName { get; set; } = "";
    public string GuestName { get; set; } = "";
}

public sealed class JoinRoomFailedPayload
{
    public string Reason { get; set; } = "";
}

public sealed class LeaveRoomPayload
{
    public string RoomId { get; set; } = "";
    public string PlayerName { get; set; } = "";
}

public sealed class RoomLeftPayload
{
    public string RoomId { get; set; } = "";
    public string PlayerName { get; set; } = "";
}

public sealed class OpponentLeftPayload
{
    public string RoomId { get; set; } = "";
    public string PlayerName { get; set; } = "";
}

public sealed class StartGameRequestPayload
{
    public string RoomId { get; set; } = "";
    public string PlayerName { get; set; } = "";
}

public sealed class StartGamePayload
{
    public string RoomId { get; set; } = "";
    public string PlayerX { get; set; } = "";
    public string PlayerO { get; set; } = "";
}

public sealed class MovePayload
{
    public string RoomId { get; set; } = "";
    public int Row { get; set; }
    public int Col { get; set; }
    public string PlayerName { get; set; } = "";
}

public sealed class MoveAcceptedPayload
{
    public string RoomId { get; set; } = "";
    public int Row { get; set; }
    public int Col { get; set; }
}

public sealed class MoveRejectedPayload
{
    public string Reason { get; set; } = "";
}

public sealed class BoardUpdatedPayload
{
    public string RoomId { get; set; } = "";
    public int Row { get; set; }
    public int Col { get; set; }
    public string Symbol { get; set; } = "";
    public string CurrentTurn { get; set; } = "";
}

public sealed class GameOverPayload
{
    public string RoomId { get; set; } = "";
    public string WinnerSymbol { get; set; } = "";
    public string WinnerName { get; set; } = "";
    public string Reason { get; set; } = "";
}

public sealed class ChatPayload
{
    public string RoomId { get; set; } = "";
    public string Sender { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime? Timestamp { get; set; }
}

public sealed class ChatBroadcastPayload
{
    public string RoomId { get; set; } = "";
    public string Sender { get; set; } = "";
    public string Content { get; set; } = "";
}
