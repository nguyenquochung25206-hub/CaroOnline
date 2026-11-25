using System.Text.Json;
using Caro.Shared.Messages;
using Caro.Shared.Protocol;
using Xunit;

namespace Caro.Tests;

public class ProtocolTests
{
    [Fact]
    public void Encode_Then_Decode_Preserves_Type_And_RequestId()
    {
        var original = new NetworkMessage { Type = "HELLO", RequestId = "req-001", Payload = "hi" };
        var json = MessageCodec.Encode(original);
        var restored = MessageCodec.Decode(json);
        Assert.NotNull(restored);
        Assert.Equal(original.Type, restored.Type);
        Assert.Equal(original.RequestId, restored.RequestId);
    }

    [Fact]
    public void Encode_Produces_Valid_Json()
    {
        var json = MessageCodec.Encode(new NetworkMessage { Type = "PING" });
        using var document = JsonDocument.Parse(json);
        Assert.Equal("PING", document.RootElement.GetProperty("Type").GetString());
    }

    [Fact]
    public void Malformed_Json_Returns_Null()
    {
        var result = MessageCodec.Decode("{not valid json}");

        Assert.Null(result);
    }

    [Fact]
    public void Message_Without_Type_Fails_Validation()
    {
        var message = MessageCodec.Decode("{\"RequestId\":\"req-1\"}");
        Assert.False(MessageValidator.IsValid(message, out var error));
        Assert.Contains("Type", error);
    }

    [Fact]
    public void Message_With_Type_And_RequestId_Passes_Validation()
    {
        var message = new NetworkMessage
        {
            Type = "PING",
            RequestId = "req-001",
             Payload = 1795168800000L
        };

        Assert.True(
            MessageValidator.IsValid(message, out var error),
            error);
    }
    [Fact]
    public void Null_Json_Fails_Validation()
    {
        var message = MessageCodec.Decode("null");
        Assert.False(MessageValidator.IsValid(message, out _));
    }

    [Fact]
    public void Missing_RequestId_Fails_Validation()
    {
        var message = new NetworkMessage
        {
            Type = "PING",
            RequestId = ""
        };

        Assert.False(
            MessageValidator.IsValid(message, out var error));

        Assert.Contains("RequestId", error);
    }

    [Fact]
    public void Empty_Type_Fails_Validation()
    {
        var message = new NetworkMessage
        {
            Type = "   ",
            RequestId = "req-001"
        };

        Assert.False(
            MessageValidator.IsValid(message, out var error));

        Assert.Contains("Type", error);
    }

    [Fact]
    public void Oversized_Raw_Message_Fails_Validation()
    {
        string rawJson = new string('A', 16385);

        Assert.False(
            MessageValidator.IsRawLengthValid(
                rawJson, out var error));

        Assert.Contains("16384", error);
    }
    [Fact]
    public void CreateRoom_With_Required_Fields_Passes_Validation()
    {
        var message = new NetworkMessage
        {
            Type = MessageTypes.CreateRoom,
            Payload = new
            {
                PlayerName = "Thao Vy",
                HostName = "Thao Vy",
                RoomName = "Caro Arena",
                BoardSize = 15
            }
        };

        Assert.True(MessageValidator.IsValid(message, out var error), error);
    }

    [Fact]
    public void CreateRoom_Without_RoomName_Fails_Validation()
    {
        var message = new NetworkMessage
        {
            Type = MessageTypes.CreateRoom,
            Payload = new { PlayerName = "Thao Vy", BoardSize = 15 }
        };

        Assert.False(MessageValidator.IsValid(message, out var error));
        Assert.Contains("RoomName", error);
    }

    [Fact]
    public void JoinRoom_Without_RoomId_Fails_Validation()
    {
        var message = new NetworkMessage
        {
            Type = MessageTypes.JoinRoom,
            Payload = new { RoomId = "", PlayerName = "Guest" }
        };

        Assert.False(MessageValidator.IsValid(message, out var error));
        Assert.Contains("RoomId", error);
    }

    [Fact]
    public void Move_With_Integer_Row_And_Col_Passes_Validation()
    {
        var message = new NetworkMessage
        {
            Type = MessageTypes.Move,
            Payload = new { RoomId = "7788", Row = 7, Col = 7 }
        };

        Assert.True(MessageValidator.IsValid(message, out var error), error);
    }

    [Fact]
    public void Move_Without_Row_Fails_Validation()
    {
        var message = new NetworkMessage
        {
            Type = MessageTypes.Move,
            Payload = new { RoomId = "7788", Col = 7 }
        };

        Assert.False(MessageValidator.IsValid(message, out var error));
        Assert.Contains("Row", error);
    }

    [Fact]
    public void Move_With_String_Row_Fails_Validation()
    {
        var message = new NetworkMessage
        {
            Type = MessageTypes.Move,
            Payload = new { RoomId = "7788", Row = "7", Col = 7 }
        };

        Assert.False(MessageValidator.IsValid(message, out var error));
        Assert.Contains("so nguyen", error);
    }
    
    [Fact]
    public void Chat_Over_500_Characters_Fails_Validation()
    {
        var message = new NetworkMessage
        {
            Type = MessageTypes.Chat,
            Payload = new { RoomId = "7788", Sender = "Vy", Message = new string('A', 501) }
        };

        Assert.False(MessageValidator.IsValid(message, out var error));
        Assert.Contains("500", error);
    }

    [Fact]
    public void BoardUpdated_With_Required_Fields_Passes_Validation()
    {
        var message = new NetworkMessage
        {
            Type = MessageTypes.BoardUpdated,
            Payload = new { Row = 7, Col = 7, Symbol = "X", CurrentTurn = "O" }
        };

        Assert.True(MessageValidator.IsValid(message, out var error), error);
    }

    [Fact]
    public void Ping_With_Numeric_Payload_Passes_Validation()
    {
        var message = new NetworkMessage
        {
            Type = MessageTypes.Ping,
            Payload = 1795168800000L
        };

        Assert.True(MessageValidator.IsValid(message, out var error), error);
    }


    [Fact]
    public void Json_Without_RequestId_Cannot_Be_Decoded()
    {
        var message = MessageCodec.Decode("{\"Type\":\"PING\"}");
        Assert.Null(message);
    }
    [Fact]
    public void Decode_Empty_Json_Returns_Null()
    {
        var result = MessageCodec.Decode("");

        Assert.Null(result);
    }

    [Fact]
    public void Decode_Whitespace_Json_Returns_Null()
    {
        var result = MessageCodec.Decode("   ");

        Assert.Null(result);
    }

    [Fact]
    public void Decode_Invalid_Json_Structure_Returns_Null()
    {
        var result = MessageCodec.Decode("[]");

        Assert.Null(result);
    }

    [Fact]
    public void Raw_Message_At_Maximum_Length_Passes_Validation()
    {
        string rawJson = new string('A', 16384);

        Assert.True(
            MessageValidator.IsRawLengthValid(rawJson, out var error),
            error);
    }

    [Fact]
    public void Oversized_Payload_Fails_Validation()
    {
        var message = new NetworkMessage
        {
            Type = "HELLO",
            RequestId = "req-001",
            Payload = new string('A', 16385)
        };

        Assert.False(
            MessageValidator.IsValid(message, out var error));

        Assert.Contains("Payload", error);
    }
}

