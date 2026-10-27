using System.Collections.Generic;
using System.Text.Json;
using Caro.Shared.Messages;

namespace Caro.Shared.Protocol;

public static class MessageCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
    public static string Encode(NetworkMessage message)
    {
        string json = JsonSerializer.Serialize(message, Options);
        return json + "\n";
    }
    public static NetworkMessage? Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<NetworkMessage>(json.Trim(), Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
    public static T? GetPayload<T>(NetworkMessage message)
    {
        if (message == null || message.Payload == null)
            return default;

        try
        {
            if (message.Payload is JsonElement jsonElement)
            {
                return jsonElement.Deserialize<T>(Options);
            }

            string rawJson = JsonSerializer.Serialize(message.Payload, Options);

            return JsonSerializer.Deserialize<T>(rawJson, Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
    public static List<string> ExtractMessages(ref string dataBuffer)
    {
        var messages = new List<string>();
        int newlineIndex;

        while ((newlineIndex = dataBuffer.IndexOf('\n')) >= 0)
        {
          
            string singleJson = dataBuffer.Substring(0, newlineIndex);

            if (!string.IsNullOrWhiteSpace(singleJson))
            {
                messages.Add(singleJson);
            }

            dataBuffer = dataBuffer.Substring(newlineIndex + 1);
        }

        return messages;
    }
}