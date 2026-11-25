
using Caro.Shared.Messages;
using Caro.Shared.Protocol;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using System;
using System.Text.Json;
using Xunit;

namespace Caro.Shared.Tests;

public class ProtocolTests
{
    private static NetworkMessage CreateMessage(
        string type,
        object? payload = null)
    {
        return new NetworkMessage
        {
            Type = type,
            RequestId = Guid.NewGuid().ToString("N"),
            Payload = payload
        };
    }

    // 1. Kiểm tra Encode tạo JSON có xuống dòng
    [Fact]
    public void Encode_ShouldAppendNewLine()
    {
        var message = CreateMessage(MessageTypes.Ping, 123456L);

        string result = MessageCodec.Encode(message);

        Assert.EndsWith("\n", result);
    }

    // 2. Kiểm tra Decode JSON hợp lệ
    [Fact]
    public void Decode_ShouldReturnMessage_WhenJsonIsValid()
    {
        string json =
            """{"Type":"PING","RequestId":"123","Timestamp":"2026-01-01T00:00:00Z","Payload":123456}""";

        NetworkMessage? result = MessageCodec.Decode(json);

        Assert.NotNull(result);
        Assert.Equal(MessageTypes.Ping, result.Type);
        Assert.Equal("123", result.RequestId);
    }

    // 3. JSON không hợp lệ
    [Fact]
    public void Decode_ShouldReturnNull_WhenJsonIsInvalid()
    {
        string json = "{invalid json}";

        NetworkMessage? result = MessageCodec.Decode(json);

        Assert.Null(result);
    }

    // 4. JSON thiếu RequestId
    [Fact]
    public void Decode_ShouldReturnNull_WhenRequestIdIsMissing()
    {
        string json =
            """{"Type":"PING","Timestamp":"2026-01-01T00:00:00Z","Payload":123456}""";

        NetworkMessage? result = MessageCodec.Decode(json);

        Assert.Null(result);
    }

    // 5. Encode không nhận message null
    [Fact]
    public void Encode_ShouldThrow_WhenMessageIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => MessageCodec.Encode(null!));
    }

    // 6. Message hợp lệ
    [Fact]
    public void IsValid_ShouldReturnTrue_WhenMessageIsValid()
    {
        var message = new NetworkMessage
        {
            Type = MessageTypes.Ping,
            RequestId = "req-001",
            Payload = JsonSerializer.SerializeToElement(1795168800000L)
        };

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.True(result, error);
    }


    // 7. Message null
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenMessageIsNull()
    {
        bool result = MessageValidator.IsValid(null, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 8. Thiếu Type
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenTypeIsMissing()
    {
        var message = CreateMessage("");

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 9. Thiếu RequestId
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenRequestIdIsMissing()
    {
        var message = CreateMessage(MessageTypes.Ping, 123456L);
        message.RequestId = "";

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 10. Dòng JSON đúng giới hạn 64 KB
    [Fact]
    public void IsRawLengthValid_ShouldReturnTrue_At64KB()
    {
        string line = new string('a', 64 * 1024);

        bool result = MessageValidator.IsRawLengthValid(line, out string error);

        Assert.True(result);
        Assert.Empty(error);
    }

    // 11. Dòng JSON vượt giới hạn 64 KB
    [Fact]
    public void IsRawLengthValid_ShouldReturnFalse_Over64KB()
    {
        string line = new string('a', (64 * 1024) + 1);

        bool result = MessageValidator.IsRawLengthValid(line, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 12. Dòng JSON null
    [Fact]
    public void IsRawLengthValid_ShouldReturnFalse_WhenLineIsNull()
    {
        bool result = MessageValidator.IsRawLengthValid(null!, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 13. Payload vượt quá 16384 ký tự
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenPayloadIsTooLarge()
    {
        var message = CreateMessage(
            MessageTypes.Hello,
            new { Name = new string('a', 16385) });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 14. CREATE_ROOM hợp lệ
    [Fact]
    public void IsValid_ShouldReturnTrue_WhenCreateRoomIsValid()
    {
        var message = CreateMessage(
            MessageTypes.CreateRoom,
            new CreateRoomPayload
            {
                PlayerName = "An",
                HostName = "An",
                RoomName = "Phong 1",
                BoardSize = 15
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.True(result);
        Assert.Empty(error);
    }

    // 15. CREATE_ROOM thiếu RoomName
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenCreateRoomRoomNameIsMissing()
    {
        var message = CreateMessage(
            MessageTypes.CreateRoom,
            new
            {
                PlayerName = "An",
                HostName = "An",
                BoardSize = 15
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 16. CREATE_ROOM có BoardSize khác 15
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenBoardSizeIsNot15()
    {
        var message = CreateMessage(
            MessageTypes.CreateRoom,
            new CreateRoomPayload
            {
                PlayerName = "An",
                HostName = "An",
                RoomName = "Phong 1",
                BoardSize = 10
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 17. JOIN_ROOM hợp lệ
    [Fact]
    public void IsValid_ShouldReturnTrue_WhenJoinRoomIsValid()
    {
        var message = CreateMessage(
            MessageTypes.JoinRoom,
            new JoinRoomPayload
            {
                RoomId = "abcdef",
                PlayerName = "Binh"
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.True(result);
        Assert.Empty(error);
    }

    // 18. JOIN_ROOM thiếu RoomId
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenJoinRoomIdIsMissing()
    {
        var message = CreateMessage(
            MessageTypes.JoinRoom,
            new
            {
                PlayerName = "Binh"
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 19. JOIN_ROOM thiếu PlayerName
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenJoinRoomPlayerNameIsMissing()
    {
        var message = CreateMessage(
            MessageTypes.JoinRoom,
            new
            {
                RoomId = "abcdef"
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 20. START_GAME hợp lệ
    [Fact]
    public void IsValid_ShouldReturnTrue_WhenStartGameIsValid()
    {
        var message = CreateMessage(
            MessageTypes.StartGame,
            new
            {
                RoomId = "abcdef",
                PlayerName = "An"
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.True(result);
        Assert.Empty(error);
    }

    // 21. MOVE hợp lệ
    [Fact]
    public void IsValid_ShouldReturnTrue_WhenMoveIsValid()
    {
        var message = CreateMessage(
            MessageTypes.Move,
            new MovePayload
            {
                RoomId = "abcdef",
                PlayerName = "An",
                Row = 5,
                Col = 7
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.True(result);
        Assert.Empty(error);
    }

    // 22. MOVE thiếu Row
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenMoveRowIsMissing()
    {
        var message = CreateMessage(
            MessageTypes.Move,
            new
            {
                RoomId = "abcdef",
                PlayerName = "An",
                Col = 7
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 23. MOVE có Row không phải số nguyên
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenMoveRowIsNotInteger()
    {
        var message = CreateMessage(
            MessageTypes.Move,
            new
            {
                RoomId = "abcdef",
                PlayerName = "An",
                Row = "abc",
                Col = 7
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 24. MOVE có tọa độ nhỏ hơn 0
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenMoveCoordinateIsNegative()
    {
        var message = CreateMessage(
            MessageTypes.Move,
            new MovePayload
            {
                RoomId = "abcdef",
                PlayerName = "An",
                Row = -1,
                Col = 7
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 25. MOVE có tọa độ lớn hơn 14
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenMoveCoordinateIsTooLarge()
    {
        var message = CreateMessage(
            MessageTypes.Move,
            new MovePayload
            {
                RoomId = "abcdef",
                PlayerName = "An",
                Row = 15,
                Col = 7
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 26. CHAT hợp lệ, dùng trường Message
    [Fact]
    public void IsValid_ShouldReturnTrue_WhenChatIsValid()
    {
        var message = CreateMessage(
            MessageTypes.Chat,
            new ChatPayload
            {
                RoomId = "abcdef",
                Sender = "An",
                Message = "Xin chao"
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.True(result);
        Assert.Empty(error);
    }

    // 27. CHAT vượt quá 500 ký tự
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenChatIsTooLong()
    {
        var message = CreateMessage(
            MessageTypes.Chat,
            new ChatPayload
            {
                RoomId = "abcdef",
                Sender = "An",
                Message = new string('a', 501)
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 28. CHAT không được chỉ dùng Content thay cho Message
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenChatUsesContentInsteadOfMessage()
    {
        var message = CreateMessage(
            MessageTypes.Chat,
            new
            {
                RoomId = "abcdef",
                Sender = "An",
                Content = "Xin chao"
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 29. CHAT thiếu nội dung
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenChatMessageIsEmpty()
    {
        var message = CreateMessage(
            MessageTypes.Chat,
            new ChatPayload
            {
                RoomId = "abcdef",
                Sender = "An",
                Message = ""
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 30. LEAVE_ROOM hợp lệ
    [Fact]
    public void IsValid_ShouldReturnTrue_WhenLeaveRoomIsValid()
    {
        var message = CreateMessage(
            MessageTypes.LeaveRoom,
            new LeaveRoomPayload
            {
                RoomId = "abcdef",
                PlayerName = "An"
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.True(result);
        Assert.Empty(error);
    }

    // 31. PING có Payload là chuỗi thì không hợp lệ
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenPingPayloadIsString()
    {
        var message = CreateMessage(
            MessageTypes.Ping,
            "123456");

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 32. PING không có Payload
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenPingPayloadIsMissing()
    {
        var message = CreateMessage(MessageTypes.Ping);

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 33. Message từ Server gửi xuống Client không được chấp nhận
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenMessageTypeIsServerToClient()
    {
        var message = CreateMessage(
            MessageTypes.Welcome,
            new WelcomePayload
            {
                ClientId = "123",
                PlayerName = "An",
                Message = "Welcome"
            });

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 34. Message Type không tồn tại
    [Fact]
    public void IsValid_ShouldReturnFalse_WhenMessageTypeIsUnknown()
    {
        var message = CreateMessage("UNKNOWN_TYPE");

        bool result = MessageValidator.IsValid(message, out string error);

        Assert.False(result);
        Assert.NotEmpty(error);
    }

    // 35. GetPayload chuyển đổi Payload thành kiểu mong muốn
    [Fact]
    public void GetPayload_ShouldReturnCorrectPayload()
    {
        var message = CreateMessage(
            MessageTypes.JoinRoom,
            new
            {
                RoomId = "abcdef",
                PlayerName = "An"
            });

        JoinRoomPayload? result =
            MessageCodec.GetPayload<JoinRoomPayload>(message);

        Assert.NotNull(result);
        Assert.Equal("abcdef", result.RoomId);
        Assert.Equal("An", result.PlayerName);
    }

    // 36. GetPayload trả về null khi Payload không đúng kiểu
    [Fact]
    public void GetPayload_ShouldReturnDefault_WhenPayloadIsInvalid()
    {
        var message = CreateMessage(
            MessageTypes.JoinRoom,
            new
            {
                RoomId = 123,
                PlayerName = true
            });

        JoinRoomPayload? result =
            MessageCodec.GetPayload<JoinRoomPayload>(message);

        Assert.Null(result);
    }

    // 37. GetPayload trả về default khi Payload null
    [Fact]
    public void GetPayload_ShouldReturnDefault_WhenPayloadIsNull()
    {
        var message = CreateMessage(MessageTypes.JoinRoom);

        JoinRoomPayload? result =
            MessageCodec.GetPayload<JoinRoomPayload>(message);

        Assert.Null(result);
    }

    // 38. WELCOME dùng tên trường camelCase khi serialize
    [Fact]
    public void WelcomePayload_ShouldUseCamelCaseProperties()
    {
        var payload = new WelcomePayload
        {
            ClientId = "123",
            PlayerName = "An",
            Message = "Welcome"
        };

        string json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"clientId\"", json);
        Assert.Contains("\"playerName\"", json);
        Assert.Contains("\"message\"", json);
    }

    // 39. ERROR dùng tên trường camelCase khi serialize
    [Fact]
    public void ErrorPayload_ShouldUseCamelCaseProperties()
    {
        var payload = new ErrorPayload
        {
            ErrorCode = "INVALID",
            Message = "Loi du lieu"
        };

        string json = JsonSerializer.Serialize(payload);

        Assert.Contains("\"errorCode\"", json);
        Assert.Contains("\"message\"", json);
    }
}
