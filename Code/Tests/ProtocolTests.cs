using System.Text.Json;
using Caro.Shared.Messages;
using Caro.Shared.Protocol;
using Xunit;

namespace Caro.Tests;

// Kiểm thử dựa trên schema hiện có của TV2; cần chạy lại khi TV2 cập nhật protocol.
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
    public void Malformed_Json_Throws_JsonException()
    {
        Assert.Throws<JsonException>(() => MessageCodec.Decode("{not valid json}"));
    }

    [Fact]
    public void Message_Without_Type_Fails_Validation()
    {
        var message = MessageCodec.Decode("{\"RequestId\":\"req-1\"}");
        Assert.False(MessageValidator.IsValid(message, out var error));
        Assert.Contains("Type", error);
    }

    [Fact]
    public void Message_With_Type_Passes_Current_Validation()
    {
        var message = new NetworkMessage { Type = "PING" };
        Assert.True(MessageValidator.IsValid(message, out var error), error);
    }

    [Fact]
    public void Null_Json_Fails_Validation()
    {
        var message = MessageCodec.Decode("null");
        Assert.False(MessageValidator.IsValid(message, out _));
    }
}
