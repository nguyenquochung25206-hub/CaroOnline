using System;
using System.Text.Json;
using Caro.Shared.Messages;

namespace Caro.Shared.Protocol;

public static class MessageValidator
{
    private const int MaxMessageLength = 16384;

    private const int MaxChatLength = 500;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool IsValid(NetworkMessage? message, out string error)
    {
        error = "";

        if (message is null)
        {
            error = "Message rong hoac JSON khong hop le.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.Type))
        {
            error = "Thieu truong Type hoac Type khong hop le.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.RequestId))
        {
            error = "Thieu truong RequestId de dinh danh goi tin.";
            return false;
        }

        try
        {
            if (message.Payload != null)
            {
                string payloadJson = JsonSerializer.Serialize(message.Payload, Options);
                if (payloadJson.Length > MaxMessageLength)
                {
                    error = $"Payload vuot qua {MaxMessageLength} ky tu.";
                    return false;
                }
            }
        }
        catch (JsonException)
        {
            error = "Payload khong the chuyen doi thanh JSON.";
            return false;
        }
        catch (NotSupportedException)
        {
            error = "Payload co kieu du lieu khong duoc ho tro.";
            return false;
        }

        return ValidatePayload(message.Type, message, out error);
    }

    public static bool ValidatePayload(
        string type,
        NetworkMessage message,
        out string error)
    {
        error = "";

        if (message == null)
        {
            error = "Message khong duoc null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            error = "Type khong duoc de trong.";
            return false;
        }

        string normalizedType = type.Trim().ToUpperInvariant();

        if (normalizedType == MessageTypes.Ping || normalizedType == MessageTypes.Pong)
        {
            if (!TryGetPayloadElement(message.Payload!, out JsonElement pingPayload) ||
                pingPayload.ValueKind != JsonValueKind.Number ||
                !pingPayload.TryGetInt64(out _))
            {
                error = $"{type} can Payload la timestamp dang so nguyen.";
                return false;
            }

            return true;
        }

        if (message.Payload == null)
        {
            if (normalizedType == MessageTypes.CreateRoom ||
                normalizedType == MessageTypes.JoinRoom ||
                normalizedType == MessageTypes.Move ||
                normalizedType == MessageTypes.Chat ||
                normalizedType == MessageTypes.BoardUpdated ||
                normalizedType == MessageTypes.RoomCreated ||
                normalizedType == MessageTypes.RoomJoined ||
                normalizedType == MessageTypes.StartGame ||
                normalizedType == MessageTypes.LeaveRoom)
            {
                error = $"Payload cua {type} khong duoc de trong.";
                return false;
            }

            return true;
        }

        if (!TryGetPayloadElement(message.Payload, out JsonElement payload))
        {
            error = $"Payload cua {type} khong phai JSON object hop le.";
            return false;
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            error = $"Payload cua {type} phai la mot object JSON.";
            return false;
        }

        switch (type.Trim().ToUpperInvariant())
        {
            case MessageTypes.CreateRoom:
                {
                    if (!TryGetString(payload, "PlayerName", out string playerName) ||
                        string.IsNullOrWhiteSpace(playerName) ||
                        !TryGetString(payload, "RoomName", out string roomName) ||
                        string.IsNullOrWhiteSpace(roomName))
                    {
                        error = "CREATE_ROOM can PlayerName va RoomName khong duoc de trong.";
                        return false;
                    }

                    if (!TryGetInt32(payload, "BoardSize", out int boardSize) ||
                        boardSize <= 0)
                    {
                        error = "CREATE_ROOM BoardSize phai la so nguyen lon hon 0.";
                        return false;
                    }

                    CreateRoomPayload? data = MessageCodec.GetPayload<CreateRoomPayload>(message);
                    if (data == null)
                    {
                        error = "CREATE_ROOM Payload khong dung cau truc.";
                        return false;
                    }

                    return true;
                }

            case MessageTypes.JoinRoom:
                {
                    JoinRoomPayload? data = MessageCodec.GetPayload<JoinRoomPayload>(message);

                    if (data == null || string.IsNullOrWhiteSpace(data.RoomId))
                    {
                        error = "JOIN_ROOM can RoomId khong duoc de trong.";
                        return false;
                    }

                    return true;
                }

            case MessageTypes.Move:
                {
                    if (!TryGetString(payload, "RoomId", out string roomId) ||
                        string.IsNullOrWhiteSpace(roomId))
                    {
                        error = "MOVE can RoomId khong duoc de trong.";
                        return false;
                    }

                    if (!TryGetInt32(payload, "Row", out _) ||
                        !TryGetInt32(payload, "Col", out _))
                    {
                        error = "MOVE can Row va Col la so nguyen.";
                        return false;
                    }

                    MovePayload? data = MessageCodec.GetPayload<MovePayload>(message);
                    if (data == null)
                    {
                        error = "MOVE Payload khong dung cau truc.";
                        return false;
                    }

                    return true;
                }

            case MessageTypes.Chat:
                {
                    ChatPayload? data = MessageCodec.GetPayload<ChatPayload>(message);

                    if (data == null || string.IsNullOrWhiteSpace(data.Message))
                    {
                        error = "CHAT can Message khong duoc de trong.";
                        return false;
                    }

                    if (data.Message.Trim().Length > MaxChatLength)
                    {
                        error = $"CHAT khong duoc vuot qua {MaxChatLength} ky tu.";
                        return false;
                    }

                    return true;
                }

            case MessageTypes.BoardUpdated:
                {
                    BoardUpdatedPayload? data = MessageCodec.GetPayload<BoardUpdatedPayload>(message);

                    if (data == null ||
                        !TryGetInt32(payload, "Row", out _) ||
                        !TryGetInt32(payload, "Col", out _) ||
                        string.IsNullOrWhiteSpace(data.Symbol) ||
                        string.IsNullOrWhiteSpace(data.CurrentTurn))
                    {
                        error = "BOARD_UPDATED can Row, Col, Symbol va CurrentTurn hop le.";
                        return false;
                    }

                    if (data.Symbol != "X" && data.Symbol != "O")
                    {
                        error = "BOARD_UPDATED Symbol chi duoc la X hoac O.";
                        return false;
                    }

                    if (data.CurrentTurn != "X" && data.CurrentTurn != "O")
                    {
                        error = "BOARD_UPDATED CurrentTurn chi duoc la X hoac O.";
                        return false;
                    }

                    return true;
                }

            case MessageTypes.RoomCreated:
                {
                    RoomCreatedPayload? data = MessageCodec.GetPayload<RoomCreatedPayload>(message);
                    if (data == null || string.IsNullOrWhiteSpace(data.RoomId))
                    {
                        error = "ROOM_CREATED can RoomId khong duoc de trong.";
                        return false;
                    }

                    return true;
                }

            case MessageTypes.RoomJoined:
                {
                    RoomJoinedPayload? data = MessageCodec.GetPayload<RoomJoinedPayload>(message);
                    if (data == null || string.IsNullOrWhiteSpace(data.RoomId))
                    {
                        error = "ROOM_JOINED can RoomId khong duoc de trong.";
                        return false;
                    }

                    return true;
                }

            case MessageTypes.StartGame:
            case MessageTypes.LeaveRoom:
                {
                    if (!TryGetString(payload, "RoomId", out string roomId) ||
                        string.IsNullOrWhiteSpace(roomId))
                    {
                        error = $"{type} can RoomId khong duoc de trong.";
                        return false;
                    }

                    return true;
                }

            default:
                return true;
        }
    }

    public static bool IsRawLengthValid(string rawJson, out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            error = "Du lieu tho rong.";
            return false;
        }

        if (rawJson.Length > MaxMessageLength)
        {
            error = $"Goi tin tho vuot qua kich thuoc toi da cho phep ({MaxMessageLength} ky tu).";
            return false;
        }

        return true;
    }

    private static bool TryGetPayloadElement(object payload, out JsonElement element)
    {
        try
        {
            if (payload is JsonElement jsonElement)
            {
                element = jsonElement;
                return true;
            }

            using JsonDocument document =
                JsonDocument.Parse(JsonSerializer.Serialize(payload, Options));

            element = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            element = default;
            return false;
        }
        catch (NotSupportedException)
        {
            element = default;
            return false;
        }
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        value = "";

        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                value = property.Value.GetString() ?? "";
                return true;
            }
        }

        return false;
    }

    private static bool TryGetInt32(JsonElement element, string name, out int value)
    {
        value = 0;

        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.Number &&
                       property.Value.TryGetInt32(out value);
            }
        }

        return false;
    }
}
