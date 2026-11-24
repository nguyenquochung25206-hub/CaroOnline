using Caro.Server.Handlers;
using Caro.Server.Logging;
using Caro.Server.Services;
using Caro.Server.Validation;
using Caro.Shared.Messages;
using Caro.Shared.Protocol;
using CaroGame.Shared.Game;

namespace Caro.Server.Networking;

// Cau noi giua tang mang (ClientSession/ClientManager - TV1) va cac dich vu nghiep vu
// (RoomService, GameService - TV5; ChatHandler, RequestValidator - TV6). (TV1 - Tuan 4)
//
// Nhiem vu:
//   - Nhan message nghiep vu tu ClientSession: CREATE_ROOM, JOIN_ROOM, LEAVE_ROOM,
//     START_GAME, MOVE, CHAT.
//   - Goi dich vu tuong ung va gui phan hoi dung dinh dang ma Client dang cho.
//   - Don phong va bao cho doi thu khi Client roi phong hoac mat ket noi.
//
// Quy uoc dung chung (xem tai lieu API):
//   - PlayerId = ClientSession.Id.ToString() (dang "D"), trung voi ChatHandler.
//   - Chu phong (nguoi dau tien trong phong) choi X, nguoi vao sau choi O.
//   - Server la noi quyet dinh: Client chi hien thi sau khi Server tra loi.
public sealed class MessageRouter
{
    // GameService hien chi ho tro ban co 15x15.
    private const int SupportedBoardSize = 15;

    private const int MaxRoomNameLength = 40;

    private readonly ClientManager _clients;
    private readonly RoomService _rooms;
    private readonly ChatHandler _chat;

    // Khoa bao ve trang thai phong/van co. Moi thao tac doc-ghi RoomService/GameService
    // va _meta deu nam trong khoa nay; viec GUI message thuc hien SAU khi nha khoa.
    private readonly object _gate = new();

    // Thong tin them cua phong ma RoomService khong luu (ten phong, da bat dau van chua).
    private readonly Dictionary<string, RoomMeta> _meta = new();

    private sealed class RoomMeta
    {
        public string RoomName { get; init; } = "";
        public bool Started { get; set; }
    }

    // Mot message cho 1 nguoi nhan, xep hang de gui sau khi nha khoa.
    private readonly record struct Outgoing(string PlayerId, NetworkMessage Message);

    public MessageRouter(ClientManager clients, RoomService rooms, ChatHandler chat)
    {
        _clients = clients ?? throw new ArgumentNullException(nameof(clients));
        _rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
    }

    // Gan router vao ClientManager: nhan message nghiep vu va don phong khi Client roi di.
    public void Attach()
    {
        _clients.MessageReceived = HandleAsync;
        _clients.ClientDisconnected += OnClientDisconnectedAsync;
    }

    // Diem vao cua moi message nghiep vu (HELLO va PING da duoc ClientSession xu ly).
    public async Task HandleAsync(ClientSession session, NetworkMessage message)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(message);

        switch (message.Type.Trim().ToUpperInvariant())
        {
            case MessageTypes.CreateRoom:
                await HandleCreateRoomAsync(session, message).ConfigureAwait(false);
                break;

            case MessageTypes.JoinRoom:
                await HandleJoinRoomAsync(session, message).ConfigureAwait(false);
                break;

            case MessageTypes.LeaveRoom:
                await HandleLeaveRoomAsync(session, message).ConfigureAwait(false);
                break;

            case MessageTypes.StartGame:
                await HandleStartGameAsync(session, message).ConfigureAwait(false);
                break;

            case MessageTypes.Move:
                await HandleMoveAsync(session, message).ConfigureAwait(false);
                break;

            case MessageTypes.Chat:
                await HandleChatAsync(session, message).ConfigureAwait(false);
                break;

            case MessageTypes.Pong:
                // PONG tu Client khong can xu ly.
                break;

            default:
                await session.SendErrorAsync(
                    $"Unsupported message type: {message.Type}",
                    message.RequestId,
                    "UNSUPPORTED_TYPE").ConfigureAwait(false);
                break;
        }
    }

    // ------------------------------------------------------------------ CREATE_ROOM

    private async Task HandleCreateRoomAsync(ClientSession session, NetworkMessage request)
    {
        RoomRequestPayload? payload = MessageCodec.GetPayload<RoomRequestPayload>(request);

        if (payload == null)
        {
            await session.SendErrorAsync("Payload CREATE_ROOM không hợp lệ.", request.RequestId, "INVALID_PAYLOAD")
                .ConfigureAwait(false);
            return;
        }

        if (payload.BoardSize.HasValue && payload.BoardSize.Value != SupportedBoardSize)
        {
            await session.SendErrorAsync(
                $"Server chỉ hỗ trợ bàn cờ {SupportedBoardSize}x{SupportedBoardSize}.",
                request.RequestId,
                "UNSUPPORTED_BOARD_SIZE").ConfigureAwait(false);
            return;
        }

        string playerId = PlayerId(session);
        string roomName = CleanRoomName(payload.RoomName, session.PlayerName);
        string roomId = "";
        string? failure = null;
        var outbox = new List<Outgoing>();

        lock (_gate)
        {
            if (!_rooms.CreateRoom(playerId, out roomId))
            {
                failure = "Bạn đang ở trong một phòng. Hãy rời phòng trước khi tạo phòng mới.";
            }
            else
            {
                _meta[roomId] = new RoomMeta { RoomName = roomName };
                session.RoomId = roomId;

                outbox.Add(new Outgoing(playerId, Reply(request, MessageTypes.RoomCreated, new
                {
                    RoomId = roomId,
                    RoomName = roomName,
                    HostName = session.PlayerName
                })));
            }
        }

        if (failure != null)
        {
            await session.SendErrorAsync(failure, request.RequestId, "ALREADY_IN_ROOM").ConfigureAwait(false);
            return;
        }

        ServerLogger.Info($"[ROOM] {session.PlayerName} created room {roomId}");
        await FlushAsync(outbox).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ JOIN_ROOM

    private async Task HandleJoinRoomAsync(ClientSession session, NetworkMessage request)
    {
        RoomRequestPayload? payload = MessageCodec.GetPayload<RoomRequestPayload>(request);

        // Ma phong do Server sinh ra la chu thuong; chap nhan ca khi nguoi dung go chu hoa.
        string roomId = (payload?.RoomId ?? "").Trim().ToLowerInvariant();

        if (!RequestValidator.ValidateRoomId(roomId, out string idError))
        {
            await session.SendAsync(Reply(request, MessageTypes.JoinRoomFailed, new { Reason = idError }))
                .ConfigureAwait(false);
            return;
        }

        string playerId = PlayerId(session);
        string? failure = null;
        string hostName = "";
        string roomName = "";
        var outbox = new List<Outgoing>();

        lock (_gate)
        {
            List<string> players = _rooms.GetPlayers(roomId);
            ClientSession? host = players.Count > 0 ? _clients.FindByPlayerId(players[0]) : null;

            if (_rooms.GetRoomId(playerId) != null)
            {
                failure = "Bạn đang ở trong một phòng. Hãy rời phòng trước khi vào phòng khác.";
            }
            else if (players.Count == 0)
            {
                failure = $"Không tìm thấy phòng #{roomId}.";
            }
            else if (players.Count >= 2)
            {
                failure = "Phòng đã đủ 2 người chơi.";
            }
            else if (host == null)
            {
                failure = "Phòng không còn hoạt động.";
            }
            else if (string.Equals(host.PlayerName, session.PlayerName, StringComparison.OrdinalIgnoreCase))
            {
                // Client phan biet chu phong/khach bang ten nen hai nguoi khong duoc trung ten.
                failure = "Tên người chơi trùng với chủ phòng. Hãy đổi tên rồi vào lại.";
            }
            else if (!_rooms.JoinRoom(roomId, playerId))
            {
                failure = "Không thể vào phòng này.";
            }
            else
            {
                session.RoomId = roomId;
                hostName = host.PlayerName;
                roomName = _meta.TryGetValue(roomId, out RoomMeta? meta) ? meta.RoomName : "";

                // Ca chu phong lan nguoi vao deu nhan ROOM_JOINED.
                var payloadOut = new
                {
                    RoomId = roomId,
                    RoomName = roomName,
                    HostName = hostName,
                    GuestName = session.PlayerName
                };

                outbox.Add(new Outgoing(playerId, Reply(request, MessageTypes.RoomJoined, payloadOut)));
                outbox.Add(new Outgoing(players[0], Notify(MessageTypes.RoomJoined, payloadOut)));
            }
        }

        if (failure != null)
        {
            await session.SendAsync(Reply(request, MessageTypes.JoinRoomFailed, new { Reason = failure }))
                .ConfigureAwait(false);
            return;
        }

        ServerLogger.Info($"[ROOM] {session.PlayerName} joined room {roomId} (host {hostName})");
        await FlushAsync(outbox).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ LEAVE_ROOM

    private async Task HandleLeaveRoomAsync(ClientSession session, NetworkMessage request)
    {
        var outbox = new List<Outgoing>();
        bool left;

        lock (_gate)
        {
            left = LeaveInternal(session, request, outbox);
        }

        if (!left)
        {
            await session.SendErrorAsync("Bạn chưa ở trong phòng nào.", request.RequestId, "NOT_IN_ROOM")
                .ConfigureAwait(false);
            return;
        }

        ServerLogger.Info($"[ROOM] {session.PlayerName} left the room");
        await FlushAsync(outbox).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ START_GAME

    private async Task HandleStartGameAsync(ClientSession session, NetworkMessage request)
    {
        string playerId = PlayerId(session);
        string? failure = null;
        string roomId = "";
        var outbox = new List<Outgoing>();

        lock (_gate)
        {
            string? currentRoom = _rooms.GetRoomId(playerId);

            if (currentRoom == null)
            {
                failure = "Bạn chưa ở trong phòng nào.";
            }
            else
            {
                roomId = currentRoom;
                List<string> players = _rooms.GetPlayers(roomId);
                GameService? game = _rooms.GetGame(roomId);
                RoomMeta? meta = _meta.TryGetValue(roomId, out RoomMeta? found) ? found : null;

                if (players.Count == 0 || players[0] != playerId)
                {
                    failure = "Chỉ chủ phòng mới được bắt đầu ván cờ.";
                }
                else if (game == null || players.Count < 2)
                {
                    failure = "Cần đủ 2 người chơi để bắt đầu.";
                }
                else if (game.IsGameOver)
                {
                    // Chua co API reset van trong RoomService (de xuat TV5: ResetGame).
                    failure = "Ván cờ trước đã kết thúc. Hãy rời phòng và tạo phòng mới để chơi lại.";
                }
                else if (meta == null || meta.Started)
                {
                    failure = "Ván cờ đã bắt đầu.";
                }
                else
                {
                    meta.Started = true;

                    ClientSession? guest = _clients.FindByPlayerId(players[1]);

                    var payloadOut = new
                    {
                        RoomId = roomId,
                        PlayerX = session.PlayerName,
                        PlayerO = guest?.PlayerName ?? ""
                    };

                    outbox.Add(new Outgoing(players[0], Reply(request, MessageTypes.StartGame, payloadOut)));
                    outbox.Add(new Outgoing(players[1], Notify(MessageTypes.StartGame, payloadOut)));
                }
            }
        }

        if (failure != null)
        {
            await session.SendErrorAsync(failure, request.RequestId, "CANNOT_START").ConfigureAwait(false);
            return;
        }

        ServerLogger.Info($"[GAME] Room {roomId} started");
        await FlushAsync(outbox).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ MOVE

    private async Task HandleMoveAsync(ClientSession session, NetworkMessage request)
    {
        MoveRequestPayload? payload = MessageCodec.GetPayload<MoveRequestPayload>(request);

        if (payload == null || !payload.Row.HasValue || !payload.Col.HasValue)
        {
            await RejectMoveAsync(session, request, "Dữ liệu nước đi không hợp lệ.").ConfigureAwait(false);
            return;
        }

        int row = payload.Row.Value;
        int col = payload.Col.Value;
        string playerId = PlayerId(session);
        string? rejectReason = null;
        string roomId = "";
        var outbox = new List<Outgoing>();

        lock (_gate)
        {
            string? currentRoom = _rooms.GetRoomId(playerId);

            if (currentRoom == null)
            {
                rejectReason = "Bạn chưa ở trong phòng nào.";
            }
            else
            {
                roomId = currentRoom;

                // Chong gian lan: khong chap nhan MOVE gan voi ma phong khac phong cua nguoi gui.
                string claimed = (payload.RoomId ?? "").Trim();
                RoomMeta? meta = _meta.TryGetValue(roomId, out RoomMeta? found) ? found : null;
                GameService? game = _rooms.GetGame(roomId);

                if (claimed.Length > 0 && !string.Equals(claimed, roomId, StringComparison.OrdinalIgnoreCase))
                {
                    rejectReason = "Mã phòng không khớp với phòng của bạn.";
                }
                else if (meta == null || !meta.Started || game == null)
                {
                    rejectReason = "Ván cờ chưa bắt đầu.";
                }
                else
                {
                    Player mover = game.CurrentPlayer;

                    if (!game.MakeMove(playerId, row, col, out string moveError))
                    {
                        rejectReason = moveError;
                    }
                    else
                    {
                        // Khi MakeMove tra true, chuoi "error" chi la thong tin (thang/hoa), khong phai loi.
                        BuildMoveMessages(request, roomId, playerId, row, col, mover, game, outbox);
                    }
                }
            }
        }

        if (rejectReason != null)
        {
            await RejectMoveAsync(session, request, rejectReason).ConfigureAwait(false);
            return;
        }

        await FlushAsync(outbox).ConfigureAwait(false);
    }

    // Tao MOVE_ACCEPTED, BOARD_UPDATED (va GAME_OVER neu ket thuc). Goi trong khoa.
    private void BuildMoveMessages(
        NetworkMessage request,
        string roomId,
        string moverId,
        int row,
        int col,
        Player mover,
        GameService game,
        List<Outgoing> outbox)
    {
        string symbol = mover == Player.X ? "X" : "O";
        string nextTurn = mover == Player.X ? "O" : "X";

        List<string> players = _rooms.GetPlayers(roomId);

        outbox.Add(new Outgoing(moverId, Reply(request, MessageTypes.MoveAccepted, new
        {
            RoomId = roomId,
            Row = row,
            Col = col
        })));

        var boardUpdated = new
        {
            RoomId = roomId,
            Row = row,
            Col = col,
            Symbol = symbol,
            CurrentTurn = nextTurn
        };

        foreach (string id in players)
        {
            outbox.Add(new Outgoing(id, Notify(MessageTypes.BoardUpdated, boardUpdated)));
        }

        if (!game.IsGameOver)
            return;

        string winnerSymbol = "";
        string winnerName = "";
        string reason = "Hòa - bàn cờ đã đầy";

        if (game.Result == GameResult.XWins || game.Result == GameResult.OWins)
        {
            winnerSymbol = game.Result == GameResult.XWins ? "X" : "O";
            string? winnerId = game.GetPlayerId(game.Result == GameResult.XWins ? Player.X : Player.O);
            winnerName = winnerId == null ? "" : (_clients.FindByPlayerId(winnerId)?.PlayerName ?? "");
            reason = "5 quân liên tiếp";
        }

        var gameOver = new
        {
            RoomId = roomId,
            WinnerSymbol = winnerSymbol,
            WinnerName = winnerName,
            Reason = reason
        };

        foreach (string id in players)
        {
            outbox.Add(new Outgoing(id, Notify(MessageTypes.GameOver, gameOver)));
        }

        ServerLogger.Info($"[GAME] Room {roomId} finished: {(winnerSymbol.Length == 0 ? "draw" : winnerSymbol + " wins")}");
    }

    private static Task RejectMoveAsync(ClientSession session, NetworkMessage request, string reason)
    {
        return session.SendAsync(Reply(request, MessageTypes.MoveRejected, new { Reason = reason }));
    }

    // ------------------------------------------------------------------ CHAT

    private async Task HandleChatAsync(ClientSession session, NetworkMessage request)
    {
        ChatRequestPayload? payload = MessageCodec.GetPayload<ChatRequestPayload>(request);
        string text = payload?.Message ?? payload?.Content ?? payload?.Text ?? "";

        // ChatHandler (TV6) tu kiem tra noi dung, kiem tra nguoi gui dang trong phong va gui cho ca phong.
        await _chat.HandleAsync(session, text).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ Mat ket noi

    private async Task OnClientDisconnectedAsync(ClientSession session)
    {
        var outbox = new List<Outgoing>();
        bool left;

        lock (_gate)
        {
            left = LeaveInternal(session, null, outbox);
        }

        if (!left)
            return;

        ServerLogger.Info($"[ROOM] {session.PlayerName} disconnected ({session.CloseReason}), room cleaned up");
        await FlushAsync(outbox).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ Ham noi bo

    // Cho 1 nguoi roi phong (tu nguyen hoac mat ket noi). PHAI goi trong khoa _gate.
    //   - request != null: roi phong tu nguyen, gui ROOM_LEFT xac nhan cho nguoi roi.
    //   - Neu chu phong roi di: dong phong, dua nguoi con lai ve sanh.
    //   - Neu khach roi di: phong van con chu phong, van co (neu dang choi) bi huy.
    // Tra ve false neu nguoi do khong o phong nao.
    private bool LeaveInternal(ClientSession session, NetworkMessage? request, List<Outgoing> outbox)
    {
        string playerId = PlayerId(session);
        string? roomId = _rooms.GetRoomId(playerId);

        if (roomId == null)
        {
            session.RoomId = null;
            return false;
        }

        List<string> before = _rooms.GetPlayers(roomId);
        bool wasHost = before.Count > 0 && before[0] == playerId;
        RoomMeta? meta = _meta.TryGetValue(roomId, out RoomMeta? found) ? found : null;
        bool wasPlaying = meta != null && meta.Started;
        string leaverName = session.PlayerName;

        _rooms.LeaveRoom(playerId);
        session.RoomId = null;

        if (request != null)
        {
            outbox.Add(new Outgoing(playerId, Reply(request, MessageTypes.RoomLeft, new
            {
                RoomId = roomId,
                PlayerName = leaverName
            })));
        }

        List<string> remaining = _rooms.GetPlayers(roomId);

        if (remaining.Count == 0)
        {
            _meta.Remove(roomId);
            return true;
        }

        string otherId = remaining[0];
        ClientSession? other = _clients.FindByPlayerId(otherId);
        string otherName = other?.PlayerName ?? "";

        if (wasHost)
        {
            // Chu phong roi di: dong phong va dua nguoi con lai ve sanh.
            _rooms.LeaveRoom(otherId);
            if (other != null)
                other.RoomId = null;

            _meta.Remove(roomId);

            if (wasPlaying)
            {
                outbox.Add(new Outgoing(otherId, Notify(MessageTypes.OpponentLeft, new
                {
                    RoomId = roomId,
                    PlayerName = leaverName
                })));
            }

            // ROOM_LEFT mang ten chinh nguoi nhan: Client hieu la minh da roi phong.
            outbox.Add(new Outgoing(otherId, Notify(MessageTypes.RoomLeft, new
            {
                RoomId = roomId,
                PlayerName = otherName
            })));
        }
        else
        {
            // Khach roi di: chu phong o lai cho nguoi moi, van co dang choi bi huy.
            if (meta != null)
                meta.Started = false;

            if (wasPlaying)
            {
                outbox.Add(new Outgoing(otherId, Notify(MessageTypes.OpponentLeft, new
                {
                    RoomId = roomId,
                    PlayerName = leaverName
                })));
            }

            outbox.Add(new Outgoing(otherId, Notify(MessageTypes.RoomLeft, new
            {
                RoomId = roomId,
                PlayerName = leaverName
            })));
        }

        return true;
    }

    // Gui cac message da xep hang. Moi nguoi nhan nhan theo dung thu tu; cac nguoi nhan gui song song.
    private async Task FlushAsync(List<Outgoing> outbox)
    {
        if (outbox.Count == 0)
            return;

        IEnumerable<Task> tasks = outbox
            .GroupBy(item => item.PlayerId)
            .Select(group => SendInOrderAsync(group.Key, group.Select(item => item.Message).ToList()));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task SendInOrderAsync(string playerId, List<NetworkMessage> messages)
    {
        if (!Guid.TryParse(playerId, out Guid clientId))
            return;

        foreach (NetworkMessage message in messages)
        {
            await _clients.SendToAsync(clientId, message).ConfigureAwait(false);
        }
    }

    private static string PlayerId(ClientSession session)
    {
        return session.Id.ToString();
    }

    // Phan hoi cho dung yeu cau (giu nguyen RequestId de Client doi chieu).
    private static NetworkMessage Reply(NetworkMessage request, string type, object payload)
    {
        return new NetworkMessage
        {
            Type = type,
            RequestId = request.RequestId,
            Payload = payload
        };
    }

    // Thong bao chu dong cho nguoi khac (RequestId moi).
    private static NetworkMessage Notify(string type, object payload)
    {
        return new NetworkMessage
        {
            Type = type,
            Payload = payload
        };
    }

    // Bo ky tu dieu khien, cat do dai; de trong thi dat ten mac dinh theo chu phong.
    private static string CleanRoomName(string? raw, string hostName)
    {
        string cleaned = "";

        if (!string.IsNullOrWhiteSpace(raw))
        {
            cleaned = new string(raw.Trim().Where(c => !char.IsControl(c)).ToArray()).Trim();

            if (cleaned.Length > MaxRoomNameLength)
                cleaned = cleaned[..MaxRoomNameLength];
        }

        return cleaned.Length > 0 ? cleaned : $"Phòng của {hostName}";
    }
}

// Cac lop du lieu doc payload. Dat internal (khong phai private long nhau) de System.Text.Json
// khoi tao duoc; ten truong khong phan biet hoa thuong nen khop voi payload cua Client.
internal sealed class RoomRequestPayload
{
    public string? RoomId { get; set; }
    public string? RoomName { get; set; }
    public string? PlayerName { get; set; }
    public int? BoardSize { get; set; }
}

internal sealed class MoveRequestPayload
{
    public string? RoomId { get; set; }
    public int? Row { get; set; }
    public int? Col { get; set; }
    public string? PlayerName { get; set; }
}

internal sealed class ChatRequestPayload
{
    public string? Message { get; set; }
    public string? Content { get; set; }
    public string? Text { get; set; }
}
