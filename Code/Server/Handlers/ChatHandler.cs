//tv6
using System;
using System.Threading.Tasks;
using Caro.Server.Networking;
using Caro.Server.Services;
using Caro.Server.Validation;
using Caro.Shared.Messages;

namespace Caro.Server.Handlers;

public sealed class ChatHandler
{
    private readonly RoomService _roomService;
    private readonly ClientManager _clientManager;

    public ChatHandler(
        RoomService roomService,
        ClientManager clientManager)
    {
        _roomService = roomService;
        _clientManager = clientManager;
    }

    public async Task HandleAsync(
        ClientSession session,
        string content)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        // Kiem tra noi dung CHAT bang RequestValidator
        if (!RequestValidator.ValidateChat(
            content, out string error))
        {
            await session.SendErrorAsync(error);
            return;
        }

        content = content.Trim();

        // Kiem tra nguoi gui dang o trong phong
        string? roomId = _roomService.GetRoomId(
            session.Id.ToString());

        if (string.IsNullOrEmpty(roomId) ||
            session.RoomId != roomId)
        {
            await session.SendErrorAsync(
                "Ban chua tham gia phong choi.");
            return;
        }

        // Tao thong diep CHAT
        var chatMessage = new NetworkMessage
        {
            Type = MessageTypes.Chat,
            Payload = new
            {
                RoomId = roomId,
                Sender = session.PlayerName,
                Content = content
            }
        };

        // Gui tin nhan den cac Client trong cung phong
        await _clientManager.BroadcastToRoomAsync(
            roomId,
            chatMessage);
    }
}
