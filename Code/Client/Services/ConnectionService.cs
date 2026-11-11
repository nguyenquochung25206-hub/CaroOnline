using System.Diagnostics;
using System.Text.Json;
using Caro.Client.Networking;
using Caro.Shared.Messages;
using Caro.Shared.Protocol;

namespace Caro.Client.Services;

/// <summary>
/// Đại diện cho thông tin phòng chơi hiện tại của Client.
/// </summary>
public sealed class RoomInfo
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string HostPlayer { get; set; } = "";
    public string? GuestPlayer { get; set; }
    public bool IsHost { get; set; }
    public string MySymbol { get; set; } = "X"; // Host là X, Guest là O
    public bool IsGameStarted { get; set; }
}

/// <summary>
/// Service cấp ứng dụng quản lý trạng thái kết nối, mã hóa/giải mã NetworkMessage và giao tiếp với Server.
/// Đảm nhiệm: Kết nối TCP, Đo Ping/Pong, Tạo/Vào/Rời phòng, Chat và bắt đầu trận đấu (Nhiệm vụ TV3 - Tuần 1 & Tuần 2).
/// </summary>
public sealed class ConnectionService : IDisposable
{
    private readonly TcpClientService _tcpClientService;
    private readonly Stopwatch _pingStopwatch = new();
    private string? _pendingPingId;
    private bool _isDisposed;

    public bool IsConnected => _tcpClientService.IsConnected;
    public string? Host { get; private set; }
    public int Port { get; private set; }
    public string PlayerName { get; private set; } = "Player";

    // Trạng thái phòng hiện tại
    public RoomInfo? CurrentRoom { get; private set; }
    public bool IsInRoom => CurrentRoom != null;

    // Sự kiện kết nối & nhật ký
    public event Action<bool, string>? ConnectionStateChanged;
    public event Action<NetworkMessage>? MessageReceived;
    public event Action<string, bool>? LogGenerated;
    public event Action<long>? PingUpdated;

    // Sự kiện phòng chơi (Tuần 2)
    public event Action<RoomInfo>? RoomCreated;
    public event Action<RoomInfo>? RoomJoined;
    public event Action<string>? JoinRoomFailed;
    public event Action<string>? RoomLeft;
    public event Action<string, string>? PlayerJoinedRoom; // (roomId, playerName)
    public event Action<string, string>? PlayerLeftRoom;   // (roomId, playerName)
    public event Action<string, string>? ChatMessageReceived; // (sender, content)
    public event Action<string>? GameStarted;              // (roomId)

    public ConnectionService()
    {
        _tcpClientService = new TcpClientService();
        _tcpClientService.MessageReceived += OnRawMessageReceived;
        _tcpClientService.Disconnected += OnDisconnected;
        _tcpClientService.ErrorOccurred += OnErrorOccurred;
    }

    /// <summary>
    /// Thực hiện kết nối tới Server, lưu lại thông tin và gửi gói HELLO ban đầu.
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int port, string playerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Địa chỉ IP/Host không được để trống.", nameof(host));
        }

        if (port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Cổng port phải nằm trong khoảng 1 - 65535.");
        }

        Host = host.Trim();
        Port = port;
        PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();

        Log($"Đang kết nối tới server {Host}:{Port}...", isError: false);
        ConnectionStateChanged?.Invoke(false, "Đang kết nối...");

        try
        {
            await _tcpClientService.ConnectAsync(Host, Port, timeoutMilliseconds: 5000, cancellationToken).ConfigureAwait(false);

            Log($"Kết nối thành công tới {Host}:{Port}!", isError: false);
            ConnectionStateChanged?.Invoke(true, $"Đã kết nối tới {Host}:{Port}");

            // Gửi thông điệp HELLO tự giới thiệu client
            await SendHelloAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"Lỗi kết nối: {ex.Message}", isError: true);
            ConnectionStateChanged?.Invoke(false, $"Lỗi: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Ngắt kết nối khỏi server một cách chủ động.
    /// </summary>
    public Task DisconnectAsync()
    {
        Log("Đang ngắt kết nối...", isError: false);
        _tcpClientService.Disconnect();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gửi một NetworkMessage tới server sau khi mã hóa JSON (newline-delimited).
    /// </summary>
    public async Task<bool> SendMessageAsync(NetworkMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            Log("Không thể gửi tin nhắn vì chưa kết nối.", isError: true);
            return false;
        }

        if (!MessageValidator.IsValid(message, out var validationError))
        {
            Log($"Tin nhắn không hợp lệ: {validationError}", isError: true);
            return false;
        }

        try
        {
            string json = MessageCodec.Encode(message).TrimEnd('\r', '\n');
            await _tcpClientService.SendAsync(json, cancellationToken).ConfigureAwait(false);
            Log($"[GỬI] {message.Type} (ID: {message.RequestId[..Math.Min(8, message.RequestId.Length)]})", isError: false);
            return true;
        }
        catch (Exception ex)
        {
            Log($"Lỗi khi gửi thông điệp ({message.Type}): {ex.Message}", isError: true);
            return false;
        }
    }

    /// <summary>
    /// Gửi thông điệp HELLO khi vừa kết nối.
    /// </summary>
    public async Task<bool> SendHelloAsync()
    {
        var helloMsg = new NetworkMessage
        {
            Type = MessageTypes.Hello,
            Payload = new { Name = PlayerName }
        };
        return await SendMessageAsync(helloMsg).ConfigureAwait(false);
    }

    /// <summary>
    /// Gửi gói tin PING để kiểm tra độ trễ mạng (latency).
    /// </summary>
    public async Task<bool> SendPingAsync()
    {
        if (!IsConnected) return false;

        var pingMsg = new NetworkMessage
        {
            Type = MessageTypes.Ping,
            Payload = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _pendingPingId = pingMsg.RequestId;
        _pingStopwatch.Restart();

        Log("[PING] Đang gửi yêu cầu kiểm tra độ trễ...", isError: false);
        return await SendMessageAsync(pingMsg).ConfigureAwait(false);
    }

    #region Room Management Methods (Tuần 2)

    /// <summary>
    /// Gửi yêu cầu tạo phòng mới lên máy chủ.
    /// </summary>
    public async Task<bool> CreateRoomAsync(string roomName = "")
    {
        if (!IsConnected)
        {
            Log("Chưa kết nối tới server, không thể tạo phòng.", isError: true);
            return false;
        }

        string generatedRoomId = Random.Shared.Next(1000, 9999).ToString();
        string finalRoomName = string.IsNullOrWhiteSpace(roomName) ? $"Phòng của {PlayerName}" : roomName.Trim();

        var msg = new NetworkMessage
        {
            Type = MessageTypes.CreateRoom,
            Payload = new
            {
                RoomId = generatedRoomId,
                RoomName = finalRoomName,
                HostName = PlayerName
            }
        };

        // Giả lập lưu tạm phòng cho Host trước khi server phản hồi
        CurrentRoom = new RoomInfo
        {
            RoomId = generatedRoomId,
            RoomName = finalRoomName,
            HostPlayer = PlayerName,
            IsHost = true,
            MySymbol = "X"
        };

        Log($"[TẠO PHÒNG] Đang yêu cầu tạo phòng '{finalRoomName}' (Mã: {generatedRoomId})...", isError: false);
        bool sent = await SendMessageAsync(msg).ConfigureAwait(false);
        if (sent)
        {
            RoomCreated?.Invoke(CurrentRoom);
        }
        return sent;
    }

    /// <summary>
    /// Gửi yêu cầu tham gia phòng có sẵn bằng mã phòng.
    /// </summary>
    public async Task<bool> JoinRoomAsync(string roomId)
    {
        if (!IsConnected)
        {
            Log("Chưa kết nối tới server, không thể vào phòng.", isError: true);
            return false;
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            Log("Mã phòng không được để trống.", isError: true);
            return false;
        }

        string cleanRoomId = roomId.Trim();

        var msg = new NetworkMessage
        {
            Type = MessageTypes.JoinRoom,
            Payload = new
            {
                RoomId = cleanRoomId,
                PlayerName = PlayerName
            }
        };

        // Cập nhật trạng thái phòng Client đang tham gia (Guest - O)
        CurrentRoom = new RoomInfo
        {
            RoomId = cleanRoomId,
            RoomName = $"Phòng #{cleanRoomId}",
            GuestPlayer = PlayerName,
            IsHost = false,
            MySymbol = "O"
        };

        Log($"[VÀO PHÒNG] Đang gửi yêu cầu vào phòng #{cleanRoomId}...", isError: false);
        return await SendMessageAsync(msg).ConfigureAwait(false);
    }

    /// <summary>
    /// Gửi thông điệp rời phòng hiện tại và dọn dẹp trạng thái phòng.
    /// </summary>
    public async Task<bool> LeaveRoomAsync()
    {
        if (CurrentRoom == null) return false;

        string roomId = CurrentRoom.RoomId;
        var msg = new NetworkMessage
        {
            Type = MessageTypes.LeaveRoom,
            Payload = new
            {
                RoomId = roomId,
                PlayerName = PlayerName
            }
        };

        Log($"[RỜI PHÒNG] Đang rời phòng #{roomId}...", isError: false);
        bool sent = await SendMessageAsync(msg).ConfigureAwait(false);

        CurrentRoom = null;
        RoomLeft?.Invoke(roomId);
        return sent;
    }

    /// <summary>
    /// Chủ phòng gửi yêu cầu bắt đầu trận đấu khi đối thủ đã sẵn sàng.
    /// </summary>
    public async Task<bool> StartGameAsync()
    {
        if (CurrentRoom == null)
        {
            Log("Bạn chưa vào phòng nào để bắt đầu trận đấu.", isError: true);
            return false;
        }

        var msg = new NetworkMessage
        {
            Type = MessageTypes.StartGame,
            Payload = new
            {
                RoomId = CurrentRoom.RoomId,
                PlayerName = PlayerName
            }
        };

        CurrentRoom.IsGameStarted = true;
        Log($"[BẮT ĐẦU TRẬN ĐẤU] Gửi yêu cầu khởi động ván cờ phòng #{CurrentRoom.RoomId}...", isError: false);
        bool sent = await SendMessageAsync(msg).ConfigureAwait(false);
        if (sent)
        {
            GameStarted?.Invoke(CurrentRoom.RoomId);
        }
        return sent;
    }

    /// <summary>
    /// Gửi tin nhắn trò chuyện (chat) trong phòng thi đấu.
    /// </summary>
    public async Task<bool> SendChatMessageAsync(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;

        var msg = new NetworkMessage
        {
            Type = MessageTypes.Chat,
            Payload = new
            {
                RoomId = CurrentRoom?.RoomId ?? "",
                Sender = PlayerName,
                Message = content.Trim(),
                Timestamp = DateTime.UtcNow
            }
        };

        // Kích hoạt ngay sự kiện chat cho giao diện cục bộ
        ChatMessageReceived?.Invoke(PlayerName, content.Trim());
        return await SendMessageAsync(msg).ConfigureAwait(false);
    }

    #endregion

    private void OnRawMessageReceived(string rawLine)
    {
        try
        {
            var message = MessageCodec.Decode(rawLine);
            if (message == null)
            {
                Log($"[NHẬN] Dữ liệu không thể giải mã JSON: {rawLine}", isError: true);
                return;
            }

            Log($"[NHẬN] {message.Type} (ID: {message.RequestId[..Math.Min(8, message.RequestId.Length)]})", isError: false);

            HandleIncomingMessage(message);
            MessageReceived?.Invoke(message);
        }
        catch (Exception ex)
        {
            Log($"Lỗi xử lý tin nhắn đến: {ex.Message}", isError: true);
        }
    }

    /// <summary>
    /// Xử lý logic nghiệp vụ phòng và thông điệp nhận từ Server.
    /// </summary>
    private void HandleIncomingMessage(NetworkMessage message)
    {
        switch (message.Type)
        {
            case MessageTypes.Pong:
                _pingStopwatch.Stop();
                long latency = _pingStopwatch.ElapsedMilliseconds;
                PingUpdated?.Invoke(latency);
                Log($"[PONG] Độ trễ phản hồi: {latency} ms", isError: false);
                break;

            case MessageTypes.RoomCreated:
                HandleRoomCreatedMessage(message);
                break;

            case MessageTypes.RoomJoined:
                HandleRoomJoinedMessage(message);
                break;

            case MessageTypes.JoinRoomFailed:
                string failReason = ExtractPropertyString(message.Payload, "reason", "message", "error") ?? "Không thể vào phòng.";
                Log($"[VÀO PHÒNG THẤT BẠI] {failReason}", isError: true);
                CurrentRoom = null;
                JoinRoomFailed?.Invoke(failReason);
                break;

            case MessageTypes.RoomLeft:
                string leftRoomId = ExtractPropertyString(message.Payload, "roomId") ?? CurrentRoom?.RoomId ?? "";
                string leftPlayer = ExtractPropertyString(message.Payload, "playerName", "player") ?? "";
                if (leftPlayer == PlayerName || string.IsNullOrEmpty(leftPlayer))
                {
                    CurrentRoom = null;
                    RoomLeft?.Invoke(leftRoomId);
                }
                else
                {
                    if (CurrentRoom != null && CurrentRoom.GuestPlayer == leftPlayer)
                    {
                        CurrentRoom.GuestPlayer = null;
                    }
                    PlayerLeftRoom?.Invoke(leftRoomId, leftPlayer);
                    Log($"[PHÒNG] Người chơi {leftPlayer} đã rời khỏi phòng.", isError: false);
                }
                break;

            case MessageTypes.StartGame:
                string startRoomId = ExtractPropertyString(message.Payload, "roomId") ?? CurrentRoom?.RoomId ?? "";
                if (CurrentRoom != null) CurrentRoom.IsGameStarted = true;
                Log($"[TRẬN ĐẤU] Ván cờ tại phòng #{startRoomId} chính thức bắt đầu!", isError: false);
                GameStarted?.Invoke(startRoomId);
                break;

            case MessageTypes.Chat:
                string sender = ExtractPropertyString(message.Payload, "sender", "playerName", "from") ?? "Đối thủ";
                string content = ExtractPropertyString(message.Payload, "message", "content", "text") ?? "";
                if (!string.IsNullOrEmpty(content) && sender != PlayerName)
                {
                    ChatMessageReceived?.Invoke(sender, content);
                }
                break;

            case MessageTypes.Error:
                string errMsg = ExtractPropertyString(message.Payload, "message", "error") ?? "Lỗi từ server.";
                Log($"[MÁY CHỦ BÁO LỖI] {errMsg}", isError: true);
                break;
        }
    }

    private void HandleRoomCreatedMessage(NetworkMessage message)
    {
        string roomId = ExtractPropertyString(message.Payload, "roomId", "id") ?? CurrentRoom?.RoomId ?? "101";
        string roomName = ExtractPropertyString(message.Payload, "roomName", "name") ?? CurrentRoom?.RoomName ?? $"Phòng #{roomId}";
        string host = ExtractPropertyString(message.Payload, "hostName", "host") ?? PlayerName;

        CurrentRoom = new RoomInfo
        {
            RoomId = roomId,
            RoomName = roomName,
            HostPlayer = host,
            IsHost = (host == PlayerName),
            MySymbol = (host == PlayerName) ? "X" : "O"
        };

        Log($"[PHÒNG ĐÃ TẠO] Phòng #{roomId} ({roomName}) do {host} làm chủ phòng.", isError: false);
        RoomCreated?.Invoke(CurrentRoom);
    }

    private void HandleRoomJoinedMessage(NetworkMessage message)
    {
        string roomId = ExtractPropertyString(message.Payload, "roomId", "id") ?? CurrentRoom?.RoomId ?? "";
        string host = ExtractPropertyString(message.Payload, "hostName", "host") ?? (CurrentRoom?.IsHost == true ? PlayerName : "Chủ phòng");
        string guest = ExtractPropertyString(message.Payload, "guestName", "guest", "playerName") ?? (CurrentRoom?.IsHost == false ? PlayerName : "Người chơi 2");

        if (CurrentRoom == null)
        {
            CurrentRoom = new RoomInfo
            {
                RoomId = roomId,
                RoomName = $"Phòng #{roomId}",
                HostPlayer = host,
                GuestPlayer = guest,
                IsHost = (host == PlayerName),
                MySymbol = (host == PlayerName) ? "X" : "O"
            };
        }
        else
        {
            if (CurrentRoom.IsHost)
            {
                CurrentRoom.GuestPlayer = guest;
            }
            else
            {
                CurrentRoom.HostPlayer = host;
                CurrentRoom.GuestPlayer = PlayerName;
            }
        }

        Log($"[VÀO PHÒNG THÀNH CÔNG] Phòng #{roomId}: {host} (X) vs {guest} (O)", isError: false);
        RoomJoined?.Invoke(CurrentRoom);
        PlayerJoinedRoom?.Invoke(roomId, guest);
    }

    /// <summary>
    /// Trích xuất giá trị chuỗi từ Payload linh hoạt (JsonElement hoặc object).
    /// </summary>
    private static string? ExtractPropertyString(object? payload, params string[] propertyNames)
    {
        if (payload is null) return null;

        if (payload is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var propName in propertyNames)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    if (string.Equals(prop.Name, propName, StringComparison.OrdinalIgnoreCase))
                    {
                        return prop.Value.GetString();
                    }
                }
            }
        }
        else
        {
            try
            {
                string json = JsonSerializer.Serialize(payload);
                using var doc = JsonDocument.Parse(json);
                foreach (var propName in propertyNames)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (string.Equals(prop.Name, propName, StringComparison.OrdinalIgnoreCase))
                        {
                            return prop.Value.GetString();
                        }
                    }
                }
            }
            catch { }
        }

        return null;
    }

    private void OnDisconnected()
    {
        Log("Đã ngắt kết nối với máy chủ.", isError: false);
        CurrentRoom = null;
        ConnectionStateChanged?.Invoke(false, "Đã ngắt kết nối");
    }

    private void OnErrorOccurred(Exception ex)
    {
        Log($"Lỗi mạng: {ex.Message}", isError: true);
    }

    private void Log(string message, bool isError)
    {
        LogGenerated?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}", isError);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _tcpClientService.Dispose();
    }
}
