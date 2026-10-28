using System.Diagnostics;
using Caro.Client.Networking;
using Caro.Shared.Messages;
using Caro.Shared.Protocol;

namespace Caro.Client.Services;

/// <summary>
/// Service cấp ứng dụng quản lý trạng thái kết nối, mã hóa/giải mã NetworkMessage và giao tiếp với Server.
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

    public event Action<bool, string>? ConnectionStateChanged;
    public event Action<NetworkMessage>? MessageReceived;
    public event Action<string, bool>? LogGenerated;
    public event Action<long>? PingUpdated;

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
    /// Gửi một NetworkMessage tới server sau khi mã hóa JSON.
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

        Log($"[PING] Đang gửi yêu cầu kiểm tra độ trễ...", isError: false);
        return await SendMessageAsync(pingMsg).ConfigureAwait(false);
    }

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

            // Xử lý gói tin PONG nếu có
            if (message.Type == MessageTypes.Pong)
            {
                _pingStopwatch.Stop();
                long latency = _pingStopwatch.ElapsedMilliseconds;
                PingUpdated?.Invoke(latency);
                Log($"[PONG] Độ trễ phản hồi: {latency} ms", isError: false);
            }

            MessageReceived?.Invoke(message);
        }
        catch (Exception ex)
        {
            Log($"Lỗi xử lý tin nhắn đến: {ex.Message}", isError: true);
        }
    }

    private void OnDisconnected()
    {
        Log("Đã ngắt kết nối với máy chủ.", isError: false);
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
