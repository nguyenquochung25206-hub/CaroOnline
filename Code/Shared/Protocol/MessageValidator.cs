using System;
using System.Text;
using System.Text.Json;
using Caro.Shared.Messages;

namespace Caro.Shared.Protocol;

public static class MessageValidator
{
    // Giới hạn kích thước theo quy ước chung của dự án
    private const int MaxMessageLength = 64 * 1024;
    private const int MaxPayloadLength = 16384;
    private const int MaxChatLength = 500;
    private const int BoardSize = 15;

    /// <summary>
    /// Kiểm tra message Client gửi lên Server.
    /// Chỉ sử dụng để kiểm tra chiều Client -> Server.
    /// </summary>
    public static bool IsValid(NetworkMessage? message, out string error)
    {
        error = "";

        if (message == null)
        {
            error = "Message không được null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.Type))
        {
            error = "Thiếu Type.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.RequestId))
        {
            error = "Thiếu RequestId.";
            return false;
        }

        // Kiểm tra kích thước Payload
        if (message.Payload != null)
        {
            try
            {
                string payloadJson = JsonSerializer.Serialize(message.Payload);

                if (payloadJson.Length > MaxPayloadLength)
                {
                    error = $"Payload vượt quá {MaxPayloadLength} ký tự.";
                    return false;
                }
            }
            catch (Exception)
            {
                error = "Không thể đọc Payload.";
                return false;
            }
        }

        return ValidatePayload(message, out error);
    }

    /// <summary>
    /// Kiểm tra kích thước tối đa của một dòng JSON.
    /// Giới hạn 64 KB tính theo số byte UTF-8.
    /// </summary>
    public static bool IsRawLengthValid(string line, out string error)
    {
        error = "";

        if (line == null)
        {
            error = "Dòng JSON không được null.";
            return false;
        }

        int length = Encoding.UTF8.GetByteCount(line);

        if (length > MaxMessageLength)
        {
            error = $"Message vượt quá {MaxMessageLength} byte.";
            return false;
        }

        return true;
    }

    private static bool ValidatePayload(
        NetworkMessage message,
        out string error)
    {
        error = "";

        switch (message.Type)
        {
            case MessageTypes.Hello:
                // HELLO có thể không có Payload.
                // Nếu có Name thì kiểm tra kiểu dữ liệu.
                if (message.Payload == null)
                    return true;

                if (!TryGetPayloadElement(message, out JsonElement hello))
                {
                    error = "Payload HELLO không hợp lệ.";
                    return false;
                }

                if (hello.ValueKind != JsonValueKind.Object)
                {
                    error = "Payload HELLO phải là object.";
                    return false;
                }

                if (TryGetProperty(hello, "Name", out JsonElement name)
                    && name.ValueKind != JsonValueKind.String)
                {
                    error = "Name phải là chuỗi.";
                    return false;
                }

                return true;

            case MessageTypes.Ping:
                // PING bắt buộc có Payload là số nguyên Unix milliseconds.
                if (!TryGetPayloadElement(message, out JsonElement ping))
                {
                    error = "PING thiếu Payload.";
                    return false;
                }

                if (ping.ValueKind != JsonValueKind.Number
                    || !ping.TryGetInt64(out _))
                {
                    error = "Payload PING phải là số nguyên Unix milliseconds.";
                    return false;
                }

                return true;

            case MessageTypes.CreateRoom:
                if (!TryGetPayloadElement(message, out JsonElement createRoom)
                    || createRoom.ValueKind != JsonValueKind.Object)
                {
                    error = "CREATE_ROOM thiếu Payload.";
                    return false;
                }

                if (!TryGetString(createRoom, "PlayerName", out string playerName)
                    || string.IsNullOrWhiteSpace(playerName))
                {
                    error = "CREATE_ROOM thiếu PlayerName.";
                    return false;
                }

                if (!TryGetString(createRoom, "HostName", out string hostName)
                    || string.IsNullOrWhiteSpace(hostName))
                {
                    error = "CREATE_ROOM thiếu HostName.";
                    return false;
                }

                if (!TryGetString(createRoom, "RoomName", out string roomName)
                    || string.IsNullOrWhiteSpace(roomName))
                {
                    error = "CREATE_ROOM thiếu RoomName.";
                    return false;
                }

                if (!TryGetInt32(createRoom, "BoardSize", out int boardSize)
                    || boardSize != BoardSize)
                {
                    error = "BoardSize phải bằng 15.";
                    return false;
                }

                return true;

            case MessageTypes.JoinRoom:
                if (!TryGetPayloadElement(message, out JsonElement joinRoom)
                    || joinRoom.ValueKind != JsonValueKind.Object)
                {
                    error = "JOIN_ROOM thiếu Payload.";
                    return false;
                }

                if (!HasNonEmptyString(joinRoom, "RoomId"))
                {
                    error = "JOIN_ROOM thiếu RoomId.";
                    return false;
                }

                if (!HasNonEmptyString(joinRoom, "PlayerName"))
                {
                    error = "JOIN_ROOM thiếu PlayerName.";
                    return false;
                }

                return true;

            case MessageTypes.LeaveRoom:
                if (!TryGetPayloadElement(message, out JsonElement leaveRoom)
                    || leaveRoom.ValueKind != JsonValueKind.Object)
                {
                    error = "LEAVE_ROOM thiếu Payload.";
                    return false;
                }

                if (!HasNonEmptyString(leaveRoom, "RoomId"))
                {
                    error = "LEAVE_ROOM thiếu RoomId.";
                    return false;
                }

                if (!HasNonEmptyString(leaveRoom, "PlayerName"))
                {
                    error = "LEAVE_ROOM thiếu PlayerName.";
                    return false;
                }

                return true;

            case MessageTypes.StartGame:
                if (!TryGetPayloadElement(message, out JsonElement startGame)
                    || startGame.ValueKind != JsonValueKind.Object)
                {
                    error = "START_GAME thiếu Payload.";
                    return false;
                }

                if (!HasNonEmptyString(startGame, "RoomId"))
                {
                    error = "START_GAME thiếu RoomId.";
                    return false;
                }

                if (!HasNonEmptyString(startGame, "PlayerName"))
                {
                    error = "START_GAME thiếu PlayerName.";
                    return false;
                }

                return true;

            case MessageTypes.Move:
                if (!TryGetPayloadElement(message, out JsonElement move)
                    || move.ValueKind != JsonValueKind.Object)
                {
                    error = "MOVE thiếu Payload.";
                    return false;
                }

                if (!HasNonEmptyString(move, "RoomId"))
                {
                    error = "MOVE thiếu RoomId.";
                    return false;
                }

                if (!HasNonEmptyString(move, "PlayerName"))
                {
                    error = "MOVE thiếu PlayerName.";
                    return false;
                }

                if (!TryGetInt32(move, "Row", out int row))
                {
                    error = "Row phải là số nguyên.";
                    return false;
                }

                if (!TryGetInt32(move, "Col", out int col))
                {
                    error = "Col phải là số nguyên.";
                    return false;
                }

                if (row < 0 || row >= BoardSize
                    || col < 0 || col >= BoardSize)
                {
                    error = "Tọa độ Row và Col phải nằm trong khoảng 0 đến 14.";
                    return false;
                }

                return true;

            case MessageTypes.Chat:
                if (!TryGetPayloadElement(message, out JsonElement chat)
                    || chat.ValueKind != JsonValueKind.Object)
                {
                    error = "CHAT thiếu Payload.";
                    return false;
                }

                if (!HasNonEmptyString(chat, "RoomId"))
                {
                    error = "CHAT thiếu RoomId.";
                    return false;
                }

                if (!HasNonEmptyString(chat, "Sender"))
                {
                    error = "CHAT thiếu Sender.";
                    return false;
                }

                if (!TryGetString(chat, "Message", out string chatMessage)
                    || string.IsNullOrWhiteSpace(chatMessage))
                {
                    error = "CHAT thiếu Message.";
                    return false;
                }

                if (chatMessage.Length > MaxChatLength)
                {
                    error = $"Tin nhắn CHAT không được vượt quá {MaxChatLength} ký tự.";
                    return false;
                }

                return true;
            default:
                error = $"Message Type không được hỗ trợ từ Client: {message.Type}.";
                return false;
        }
    }
    private static bool TryGetPayloadElement(
    NetworkMessage message,
    out JsonElement element)
    {
        element = default;
        if (message.Payload == null)
            return false;
        try
        {
            if (message.Payload is JsonElement jsonElement)
            {
                element = jsonElement;
                return element.ValueKind != JsonValueKind.Null
                && element.ValueKind != JsonValueKind.Undefined;
            }
            string json = JsonSerializer.Serialize(message.Payload);
            using JsonDocument document = JsonDocument.Parse(json);
            element = document.RootElement.Clone();
            return element.ValueKind != JsonValueKind.Null
            && element.ValueKind != JsonValueKind.Undefined;
        }
        catch (Exception)
        {
            return false;
        }
    }
    private static bool TryGetProperty(
    JsonElement element,
    string propertyName,
    out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(
            property.Name,
            propertyName,
            StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        return false;
    }
    private static bool TryGetString(
    JsonElement element,
    string propertyName,
    out string value)
    {
        value = "";
        if (!TryGetProperty(element, propertyName, out JsonElement property))
            return false;
        if (property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString() ?? "";
        return true;
    }
    private static bool TryGetInt32(
    JsonElement element,
    string propertyName,
    out int value)
    {
        value = 0;
        if (!TryGetProperty(element, propertyName, out JsonElement property))
            return false;
        return property.ValueKind == JsonValueKind.Number
        && property.TryGetInt32(out value);
    }
    private static bool HasNonEmptyString(
    JsonElement element,
    string propertyName)
    {
        return TryGetString(element, propertyName, out string value)
        && !string.IsNullOrWhiteSpace(value);
    }
}
