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
        RequestId = "req-001"
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
    
    // TEST 7: RequestId rong
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

    // TEST 8: Type chi chua khoang trang
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

    // TEST 9: Goi tin vuot gioi han
    [Fact]
    public void Oversized_Raw_Message_Fails_Validation()
    {
        string rawJson = new string('A', 16385);

        Assert.False(
            MessageValidator.IsRawLengthValid(
                rawJson, out var error));

        Assert.Contains("16384", error);
    }

}
