using System.Text.Json;
using Caro.Shared.Messages;

namespace Caro.Shared.Protocol;

// TODO TV2: Mỗi message một dòng JSON (newline-delimited JSON); không giả định một lần Read = một message.
public static class MessageCodec
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    public static string Encode(NetworkMessage message) => JsonSerializer.Serialize(message, Options);
    public static NetworkMessage? Decode(string json) => JsonSerializer.Deserialize<NetworkMessage>(json, Options);
}
