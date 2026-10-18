using Caro.Shared.Messages;
namespace Caro.Shared.Protocol;

// TODO TV2: Kiểm tra Type, RequestId, Payload và giới hạn kích thước dữ liệu.
public static class MessageValidator
{
    public static bool IsValid(NetworkMessage? message, out string error)
    {
        if (message is null) { error = "Message rỗng hoặc JSON không hợp lệ."; return false; }
        if (string.IsNullOrWhiteSpace(message.Type)) { error = "Thiếu trường Type."; return false; }
        error = ""; return true;
    }
}
