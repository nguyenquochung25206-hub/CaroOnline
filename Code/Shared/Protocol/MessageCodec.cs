using System;
using System.Text.Json;
using Caro.Shared.Messages;

namespace Caro.Shared.Protocol;

public static class MessageCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Chuyen message thanh JSON va them ky tu xuong dong.
    public static string Encode(NetworkMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        string json = JsonSerializer.Serialize(message, Options);
        return json + "\n";
    }

    // Giai ma JSON, neu loi thi tra ve null.
    public static NetworkMessage? Decode(string json)
    {
        TryDecode(json, out NetworkMessage? message, out _);
        return message;
    }

    // Giai ma JSON va tra ve ly do neu khong thanh cong.
    public static bool TryDecode(
        string json,
        out NetworkMessage? message,
        out string error)
    {
        message = null;
        error = "";

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "JSON rong.";
            return false;
        }

        try
        {
            message = JsonSerializer.Deserialize<NetworkMessage>(
                json.Trim(), Options);

            if (message == null)
            {
                error = "JSON khong tao duoc NetworkMessage.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON khong hop le hoac thieu truong bat buoc: {ex.Message}";
            return false;
        }
        catch (NotSupportedException ex)
        {
            error = $"Kieu du lieu khong duoc ho tro: {ex.Message}";
            return false;
        }
    }

    // Lay Payload va chuyen thanh kieu du lieu mong muon.
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
        catch (NotSupportedException)
        {
            return default;
        }
    }
}
