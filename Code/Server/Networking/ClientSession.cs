using System.Net.Sockets;
using System.Text;
using Caro.Server.Logging;
using Caro.Shared.Messages;
using Caro.Shared.Protocol;

namespace Caro.Server.Networking;

// Ly do phien Client bi dong. TV5 (RoomService) co the doc ClientSession.CloseReason
// trong su kien ClientDisconnected de biet doi thu thoat binh thuong hay mat mang.
public enum DisconnectReason
{
    // Client chu dong dong ket noi (thoat game, tat ung dung).
    ClientClosed,

    // Server chu dong dong (Server tat, goi Close()).
    ClosedByServer,

    // Client khong gui gi qua thoi gian cho phep.
    Timeout,

    // Mat mang, bi reset, gui/nhan that bai.
    NetworkError,

    // Client gui du lieu sai nghiem trong (dong qua dai...).
    InvalidData,

    // Loi khong mong doi phia Server.
    Error
}

// Quan ly phien ket noi cua tung Client (TV1 - Tuan 2 + Tuan 3).
//  - Doc message JSON theo tung dong (newline-delimited), co gioi han do dai.
//  - Tu xu ly HELLO -> WELCOME va PING -> PONG.
//  - Cac message khac chuyen cho ClientManager.MessageReceived de TV5 xu ly.
//  - Gui RIENG an toan khi nhieu luong cung gui (vi du broadcast nuoc di).
//  - Tuan 3: timeout khi Client im lang, ghi ly do ngat ket noi, giai phong tai nguyen.
public sealed class ClientSession
{
    // Thoi gian cho mac dinh khi Client khong gui gi. Client (TV3) can gui PING dinh ky
    // (vi du 15 giay/lan) de khong bi coi la mat ket noi.
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(60);

    // Gioi han 1 dong JSON (byte). Dong dai hon se bi coi la du lieu xau.
    private const int MaxLineBytes = 64 * 1024;

    // Kich thuoc bo dem doc du lieu tu socket.
    private const int ReadBufferSize = 4096;

    // Do dai toi da cua ten nguoi choi.
    private const int MaxNameLength = 20;

    // Gui qua lau (Client treo / mang cham) thi dong ket noi de khong lam
    // ket cac luong khac dang broadcast.
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);

    private static readonly UTF8Encoding Utf8 = new(false);

    // Ket noi TCP va luong du lieu cua Client nay.
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;

    // Ham goi khi Client ngat ket noi de xoa khoi ClientManager.
    private readonly Func<ClientSession, Task> _onDisconnected;

    // Ham chuyen message (khong phai HELLO/PING) cho noi xu ly nghiep vu.
    private readonly Func<ClientSession, NetworkMessage, Task>? _onMessage;

    // Thoi gian cho toi da giua 2 message cua Client (Infinite = khong gioi han).
    private readonly TimeSpan _idleTimeout;

    // Moi luc chi cho 1 tac vu ghi len socket de cac message khong bi tron lan.
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // Huy cac thao tac doc/gui dang cho khi dong phien.
    private readonly CancellationTokenSource _cts = new();

    // Bo dem doc: du lieu tho vua nhan + phan dong dang ghep do.
    private readonly byte[] _readBuffer = new byte[ReadBufferSize];
    private readonly MemoryStream _lineBuffer = new();
    private int _readPos;
    private int _readLen;

    // 0 = dang mo, 1 = da dong. Dung Interlocked de an toan khi nhieu luong.
    private int _closed;

    // ID duy nhat cua phien Client.
    public Guid Id { get; }

    // Dia chi va port cua Client (luu san de log duoc ca sau khi socket dong).
    public string RemoteAddress { get; }

    // Ten nguoi choi, lay tu HELLO. Mac dinh la Player-xxxx.
    public string PlayerName { get; private set; }

    // Ma phong Client dang o (null neu chua vao phong).
    // RoomService (TV5) gan khi vao/roi phong de ClientManager broadcast theo phong.
    public string? RoomId { get; set; }

    // Thoi diem ket noi va thoi diem nhan message gan nhat.
    public DateTime ConnectedAtUtc { get; }
    public DateTime LastActivityUtc { get; private set; }

    // Ly do phien bi dong (chi co y nghia sau khi IsClosed = true).
    public DisconnectReason CloseReason { get; private set; } = DisconnectReason.ClientClosed;

    // Phien da dong chua.
    public bool IsClosed => Volatile.Read(ref _closed) == 1;

    // Khoi tao phien Client.
    //  - onMessage: tuy chon (null thi message nghiep vu bi bao loi).
    //  - idleTimeout: null = dung DefaultIdleTimeout (60 giay); TimeSpan.Zero = khong timeout.
    public ClientSession(
        TcpClient client,
        Func<ClientSession, Task> onDisconnected,
        Func<ClientSession, NetworkMessage, Task>? onMessage = null,
        TimeSpan? idleTimeout = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _onDisconnected = onDisconnected
            ?? throw new ArgumentNullException(nameof(onDisconnected));
        _onMessage = onMessage;

        TimeSpan timeout = idleTimeout ?? DefaultIdleTimeout;
        _idleTimeout = timeout > TimeSpan.Zero ? timeout : Timeout.InfiniteTimeSpan;

        _stream = client.GetStream();

        Id = Guid.NewGuid();
        PlayerName = $"Player-{Id.ToString("N")[..4]}";
        RemoteAddress = GetRemoteAddress(client);
        ConnectedAtUtc = DateTime.UtcNow;
        LastActivityUtc = ConnectedAtUtc;
    }

    // Doc va xu ly message trong suot thoi gian Client ket noi.
    public async Task RunAsync()
    {
        DisconnectReason reason = DisconnectReason.ClientClosed;
        string detail = "client closed the connection";

        try
        {
            ServerLogger.ClientConnected(RemoteAddress);

            while (!IsClosed)
            {
                // Doc 1 dong JSON; null nghia la Client da dong ket noi.
                string? line = await ReadLineWithIdleTimeoutAsync()
                    .ConfigureAwait(false);

                if (line == null)
                    break;

                // Bo qua dong trong.
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                LastActivityUtc = DateTime.UtcNow;

                // Moi message duoc xu ly lan luot nen thu tu nuoc di luon dung.
                await ProcessLineAsync(line).ConfigureAwait(false);
            }
        }
        catch (TimeoutException)
        {
            // Client im lang qua lau: co the da mat mang ma Server khong nhan duoc tin hieu dong.
            reason = DisconnectReason.Timeout;
            detail = $"no data for {_idleTimeout.TotalSeconds:0} seconds";
            await SendErrorAsync("Disconnected: idle timeout").ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Phien bi dong boi noi khac (Server tat, goi Close()).
            reason = DisconnectReason.ClosedByServer;
            detail = "session cancelled";
        }
        catch (InvalidDataException ex)
        {
            // Dong qua dai: bao loi roi ngat Client.
            reason = DisconnectReason.InvalidData;
            detail = ex.Message;
            await SendErrorAsync(ex.Message).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            // Client tat dot ngot / bi reset ket noi.
            reason = DisconnectReason.NetworkError;
            detail = $"network error: {ex.Message}";
        }
        catch (SocketException ex)
        {
            reason = DisconnectReason.NetworkError;
            detail = $"socket error: {ex.Message}";
        }
        catch (ObjectDisposedException)
        {
            reason = DisconnectReason.ClosedByServer;
            detail = "connection closed";
        }
        catch (Exception ex)
        {
            // Bat moi loi con lai de mot Client loi khong lam sap Server.
            reason = DisconnectReason.Error;
            detail = $"unexpected error: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            // Dong ket noi Client (neu da dong truoc do thi giu ly do dong dau tien).
            Close(reason);

            // Bao cho ClientManager biet de xoa khoi danh sach (va bao cho RoomService).
            try
            {
                await _onDisconnected(this).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error in disconnect handler for {RemoteAddress}", ex);
            }

            string text = CloseReason == reason
                ? $"{CloseReason}: {detail}"
                : CloseReason.ToString();

            bool abnormal = CloseReason is DisconnectReason.Timeout
                or DisconnectReason.NetworkError
                or DisconnectReason.InvalidData
                or DisconnectReason.Error;

            ServerLogger.ClientDisconnected(RemoteAddress, PlayerName, text, abnormal);

            // Giai phong cac doi tuong con lai cua phien.
            ReleaseResources();
        }
    }

    // Gui rieng 1 message cho Client nay. Khong nem loi: tra ve false neu gui that bai.
    public Task<bool> SendAsync(NetworkMessage message)
    {
        if (message == null)
            return Task.FromResult(false);

        // MessageCodec.Encode da them '\n' o cuoi de lam dau phan cach message.
        byte[] data = Utf8.GetBytes(MessageCodec.Encode(message));
        return SendEncodedAsync(data);
    }

    // Gui message ERROR cho Client nay (TV5 co the dung de bao loi).
    public Task<bool> SendErrorAsync(string text, string? requestId = null)
    {
        var message = new NetworkMessage
        {
            Type = MessageTypes.Error,
            Payload = new { message = text }
        };

        if (!string.IsNullOrWhiteSpace(requestId))
            message.RequestId = requestId;

        return SendAsync(message);
    }

    // Gui du lieu da ma hoa san. ClientManager dung ham nay de broadcast
    // (ma hoa 1 lan, gui cho nhieu Client).
    internal async Task<bool> SendEncodedAsync(byte[] data)
    {
        if (IsClosed)
            return false;

        bool locked = false;

        try
        {
            // Tong thoi gian cho khoa + ghi du lieu khong qua SendTimeout.
            using var timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            timeoutCts.CancelAfter(SendTimeout);

            await _sendLock.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            locked = true;

            if (IsClosed)
                return false;

            await _stream.WriteAsync(data, timeoutCts.Token)
                .ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException)
        {
            // Het gio hoac phien da bi dong. Co the da ghi do dang nen phai dong han.
            if (!IsClosed)
                ServerLogger.Warn($"Send timeout to {RemoteAddress}, closing session.");

            Close(DisconnectReason.NetworkError);
            return false;
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is SocketException ||
            ex is ObjectDisposedException ||
            ex is InvalidOperationException)
        {
            if (!IsClosed)
                ServerLogger.Warn($"Send failed to {RemoteAddress}: {ex.Message}");

            Close(DisconnectReason.NetworkError);
            return false;
        }
        finally
        {
            if (locked)
                ReleaseSendLock();
        }
    }

    // Server chu dong dong ket noi Client nay (vi du kick, Server tat).
    public void Close()
    {
        Close(DisconnectReason.ClosedByServer);
    }

    // Dong ket noi Client voi ly do cu the. Co the goi nhieu lan va tu nhieu luong;
    // chi lan dau tien co tac dung va ly do dau tien duoc giu lai.
    public void Close(DisconnectReason reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
            return;

        CloseReason = reason;

        try
        {
            // Huy cac thao tac doc/gui dang cho.
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            // Dong socket va luong du lieu (TcpClient.Close giai phong ca NetworkStream).
            _client.Close();
        }
        catch (Exception)
        {
            // Bo qua loi phat sinh trong qua trinh dong socket.
        }
    }

    // Giai phong cac doi tuong con lai sau khi phien ket thuc. Ham chi duoc goi mot lan,
    // cuoi RunAsync. Cac luong khac con gui do se nhan ObjectDisposedException va tu bo cuoc.
    private void ReleaseResources()
    {
        try
        {
            _cts.Dispose();
        }
        catch (Exception)
        {
        }

        try
        {
            _lineBuffer.Dispose();
        }
        catch (Exception)
        {
        }

        try
        {
            _sendLock.Dispose();
        }
        catch (Exception)
        {
        }
    }

    // Tra khoa gui; khong loi neu khoa da bi giai phong.
    private void ReleaseSendLock()
    {
        try
        {
            _sendLock.Release();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SemaphoreFullException)
        {
        }
    }

    // Doc 1 dong nhung neu Client im lang qua _idleTimeout thi nem TimeoutException.
    private async Task<string?> ReadLineWithIdleTimeoutAsync()
    {
        if (_idleTimeout == Timeout.InfiniteTimeSpan)
            return await ReadLineAsync(_cts.Token).ConfigureAwait(false);

        using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        idleCts.CancelAfter(_idleTimeout);

        try
        {
            return await ReadLineAsync(idleCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!_cts.IsCancellationRequested)
        {
            // Bi huy boi dong ho timeout chu khong phai do phien bi dong.
            throw new TimeoutException("Client idle timeout.");
        }
    }

    // Doc mot dong (ket thuc bang '\n') tu socket.
    // TCP la luong byte: 1 lan doc co the chua nua dong hoac nhieu dong,
    // nen phai tu ghep dong thay vi gia dinh 1 lan doc = 1 message.
    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            // Con du lieu trong bo dem thi thu tach dong truoc.
            if (TryTakeLine(out string? line))
                return line;

            int read = await _stream
                .ReadAsync(_readBuffer.AsMemory(), cancellationToken)
                .ConfigureAwait(false);

            // 0 byte nghia la phia kia da dong ket noi.
            if (read == 0)
                return null;

            _readPos = 0;
            _readLen = read;
        }
    }

    // Tach 1 dong hoan chinh tu bo dem. Tra ve false neu chua du 1 dong.
    // (Tach ra ham rieng vi Span khong duoc dung truc tiep trong ham async.)
    private bool TryTakeLine(out string? line)
    {
        line = null;

        if (_readPos >= _readLen)
            return false;

        ReadOnlySpan<byte> pending =
            _readBuffer.AsSpan(_readPos, _readLen - _readPos);

        int newlineIndex = pending.IndexOf((byte)'\n');
        int take = newlineIndex >= 0 ? newlineIndex : pending.Length;

        // Chong Client gui mot dong vo han de ngon het bo nho Server.
        if (_lineBuffer.Length + take > MaxLineBytes)
            throw new InvalidDataException("Message too long.");

        _lineBuffer.Write(pending.Slice(0, take));
        _readPos += take;

        // Chua gap '\n': doi them du lieu.
        if (newlineIndex < 0)
            return false;

        // Bo qua ky tu '\n' va tra ve dong da ghep xong.
        _readPos++;

        line = Utf8
            .GetString(_lineBuffer.GetBuffer(), 0, (int)_lineBuffer.Length)
            .TrimEnd('\r');

        _lineBuffer.SetLength(0);
        return true;
    }

    // Phan tich 1 dong JSON va chon hanh dong phu hop.
    private async Task ProcessLineAsync(string line)
    {
        if (!MessageValidator.IsRawLengthValid(line, out string rawError))
        {
            ServerLogger.InvalidMessage(RemoteAddress, rawError);
            await SendErrorAsync(rawError).ConfigureAwait(false);
            return;
        }

        NetworkMessage? message = MessageCodec.Decode(line);

        if (message == null)
        {
            ServerLogger.InvalidMessage(RemoteAddress, "invalid JSON");
            await SendErrorAsync("Invalid JSON message").ConfigureAwait(false);
            return;
        }

        if (!MessageValidator.IsValid(message, out string error))
        {
            ServerLogger.InvalidMessage(RemoteAddress, error);
            await SendErrorAsync(error, message.RequestId).ConfigureAwait(false);
            return;
        }

        switch (message.Type.Trim().ToUpperInvariant())
        {
            case MessageTypes.Hello:
                await HandleHelloAsync(message).ConfigureAwait(false);
                break;

            case MessageTypes.Ping:
                // Tra lai dung RequestId va payload de Client do do tre.
                await SendAsync(new NetworkMessage
                {
                    Type = MessageTypes.Pong,
                    RequestId = message.RequestId,
                    Payload = message.Payload
                }).ConfigureAwait(false);
                break;

            default:
                await ForwardToHandlerAsync(message).ConfigureAwait(false);
                break;
        }
    }

    // HELLO: luu ten nguoi choi va tra ve WELCOME.
    private async Task HandleHelloAsync(NetworkMessage message)
    {
        HelloPayload? payload = MessageCodec.GetPayload<HelloPayload>(message);

        string name = SanitizeName(payload?.Name);
        if (name.Length == 0)
            name = SanitizeName(payload?.PlayerName);

        if (name.Length > 0)
            PlayerName = name;

        ServerLogger.Info($"HELLO from {RemoteAddress} as '{PlayerName}'");

        await SendAsync(new NetworkMessage
        {
            Type = MessageTypes.Welcome,
            RequestId = message.RequestId,
            Payload = new
            {
                clientId = Id.ToString(),
                playerName = PlayerName,
                message = "Connected to Caro Server"
            }
        }).ConfigureAwait(false);
    }

    // Chuyen message nghiep vu (phong, nuoc di, chat...) cho noi xu ly.
    private async Task ForwardToHandlerAsync(NetworkMessage message)
    {
        if (_onMessage == null)
        {
            await SendErrorAsync("Unknown message type", message.RequestId)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            await _onMessage(this, message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Loi xu ly 1 message khong duoc lam mat ket noi cua Client.
            ServerLogger.Error($"Error handling {message.Type} from {RemoteAddress}", ex);
            await SendErrorAsync("Server error while handling request", message.RequestId)
                .ConfigureAwait(false);
        }
    }

    // Bo ky tu dieu khien, cat khoang trang va gioi han do dai ten.
    private static string SanitizeName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var builder = new StringBuilder();

        foreach (char c in raw.Trim())
        {
            if (char.IsControl(c))
                continue;

            builder.Append(c);

            if (builder.Length >= MaxNameLength)
                break;
        }

        return builder.ToString().Trim();
    }

    // Lay dia chi Client luc vua ket noi (sau khi dong socket khong lay duoc nua).
    private static string GetRemoteAddress(TcpClient client)
    {
        try
        {
            return client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
        }
        catch (Exception)
        {
            return "Unknown";
        }
    }
}

// Du lieu trong payload cua HELLO. Client hien gui { "Name": "..." }.
internal sealed class HelloPayload
{
    public string? Name { get; set; }
    public string? PlayerName { get; set; }
}
