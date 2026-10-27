using System.Text.Json;
using Caro.Shared.Messages;

namespace Caro.Shared.Protocol;

public static class MessageValidator
{
    private const int MaxMessageLength = 16384;

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
            error = "Thieu truong Type hoac truong Type khong hop le.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message.RequestId))
        {
            error = "Thieu truong RequestId de dinh danh goi tin.";
            return false;
        }

        if (message.Payload != null)
        {
            string payloadString = JsonSerializer.Serialize(message.Payload);

            if (payloadString.Length > MaxMessageLength)
            {
                error = $"Kich thuoc Payload vuot qua gioi han cho phep ({MaxMessageLength} ky tu).";
                return false;
            }
        }

        return true;
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
}
