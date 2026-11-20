
// TV6
using System;

namespace Caro.Server.Validation;

public static class RequestValidator
{
    private const int MaxChatLength = 500;

    // Kiem tra noi dung tin nhan CHAT.
    public static bool ValidateChat(
        string? content,
        out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(content))
        {
            error = "Tin nhan khong duoc de trong.";
            return false;
        }

        string trimmed = content.Trim();

        if (trimmed.Length > MaxChatLength)
        {
            error = "Tin nhan khong duoc vuot qua 500 ky tu.";
            return false;
        }

        foreach (char c in trimmed)
        {
            if (char.IsControl(c))
            {
                error = "Tin nhan chua ky tu khong hop le.";
                return false;
            }
        }

        return true;
    }

    // Kiem tra ma phong.
    public static bool ValidateRoomId(
        string? roomId,
        out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(roomId))
        {
            error = "Ma phong khong duoc de trong.";
            return false;
        }

        return true;
    }
}
