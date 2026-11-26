using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Caro.Client.Networking;

/// <summary>
/// Trạng thái kết nối TCP của Client.
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting
}

/// <summary>
/// Quản lý kết nối TCP Socket cấp thấp (Nhiệm vụ TV3 - Tuần 1, 3):
/// Quản lý kết nối/ngắt kết nối, gửi/nhận dữ liệu dạng newline-delimited JSON string bất đồng bộ,
/// xử lý timeout, phát hiện rớt mạng đột ngột và giải phóng tài nguyên an toàn.
/// </summary>
public sealed class TcpClientService : IDisposable, IAsyncDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _disconnectLock = new();
    private bool _isDisposed;

    public event Action<string>? MessageReceived;
    public event Action? Disconnected;
    public event Action<string?>? DisconnectedWithReason;
    public event Action<Exception>? ErrorOccurred;
    public event Action<ConnectionState>? StateChanged;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public bool IsConnected => State == ConnectionState.Connected && _tcpClient is { Connected: true } && !_isDisposed;
    public string? LastDisconnectReason { get; private set; }

    private void SetState(ConnectionState newState)
    {
        if (State != newState)
        {
            State = newState;
            StateChanged?.Invoke(newState);
        }
    }

    /// <summary>
    /// Kết nối tới TCP server với timeout xác định (mặc định 5000ms).
    /// </summary>
    public async Task ConnectAsync(string host, int port, int timeoutMilliseconds = 5000, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            throw new InvalidOperationException("Client đã được kết nối trước đó.");
        }

        Cleanup();
        SetState(ConnectionState.Connecting);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _tcpClient = new TcpClient
        {
            NoDelay = true // Tắt thuật toán Nagle để giảm độ trễ game
        };

        using var timeoutCts = new CancellationTokenSource(timeoutMilliseconds);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

        try
        {
            // Kết nối có hỗ trợ timeout & cancellation
            await _tcpClient.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);

            _networkStream = _tcpClient.GetStream();
            _reader = new StreamReader(_networkStream, Encoding.UTF8);
            _writer = new StreamWriter(_networkStream, new UTF8Encoding(false))
            {
                AutoFlush = true
            };

            SetState(ConnectionState.Connected);

            // Bắt đầu vòng lặp đọc dữ liệu nền cho phiên kết nối hiện tại
            TcpClient currentClient = _tcpClient;
            StreamReader currentReader = _reader;
            CancellationToken currentToken = _cts.Token;
            _receiveTask = Task.Run(() => ReceiveLoopAsync(currentClient, currentReader, currentToken), CancellationToken.None);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            Cleanup();
            SetState(ConnectionState.Disconnected);
            throw new TimeoutException($"Hết thời gian chờ kết nối tới {host}:{port} ({timeoutMilliseconds}ms).");
        }
        catch (Exception ex)
        {
            Cleanup();
            SetState(ConnectionState.Disconnected);
            ErrorOccurred?.Invoke(ex);
            throw;
        }
    }

    /// <summary>
    /// Gửi một dòng văn bản (kết thúc bằng newline) tới server với timeout an toàn.
    /// </summary>
    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _writer == null)
        {
            throw new InvalidOperationException("Không thể gửi vì chưa kết nối tới server.");
        }

        using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        writeCts.CancelAfter(5000); // 5 giây timeout cho thao tác gửi

        await _sendLock.WaitAsync(writeCts.Token).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(message.AsMemory(), writeCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            Disconnect($"Lỗi khi gửi dữ liệu: {ex.Message}");
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Vòng lặp nhận dữ liệu từ NetworkStream theo từng dòng (newline-delimited).
    /// Nhận client, reader và token riêng biệt để tránh đọc nhầm hoặc ngắt kết nối mới khi kết nối lại nhanh.
    /// </summary>
    private async Task ReceiveLoopAsync(TcpClient client, StreamReader reader, CancellationToken token)
    {
        string? disconnectReason = null;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);

                // Khi server đóng kết nối một cách êm đẹp, ReadLineAsync trả về null
                if (line is null)
                {
                    disconnectReason = "Máy chủ đã đóng kết nối.";
                    break;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    MessageReceived?.Invoke(line);
                }
            }
        }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException)
        {
            disconnectReason = "Client chủ động ngắt kết nối.";
        }
        catch (IOException ex) when (ex.InnerException is SocketException sockEx)
        {
            disconnectReason = $"Mất kết nối tới server ({sockEx.SocketErrorCode}).";
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            disconnectReason = $"Lỗi kết nối: {ex.Message}";
            ErrorOccurred?.Invoke(ex);
        }
        finally
        {
            // Chỉ ngắt nếu kết nối hiện tại vẫn là kết nối của vòng lặp này
            if (ReferenceEquals(_tcpClient, client))
            {
                Disconnect(disconnectReason);
            }
        }
    }

    /// <summary>
    /// Ngắt kết nối và đóng tài nguyên an toàn, có thể truyền lý do ngắt kết nối.
    /// </summary>
    public void Disconnect(string? reason = null)
    {
        bool notify = false;

        lock (_disconnectLock)
        {
            if (State == ConnectionState.Disconnected && _tcpClient == null)
            {
                return;
            }

            SetState(ConnectionState.Disconnecting);
            LastDisconnectReason = reason;

            if (_cts != null && !_cts.IsCancellationRequested)
            {
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException) { }
            }

            bool wasConnected = _tcpClient != null;
            Cleanup();
            SetState(ConnectionState.Disconnected);

            if (wasConnected)
            {
                notify = true;
            }
        }

        if (notify)
        {
            Disconnected?.Invoke();
            DisconnectedWithReason?.Invoke(reason);
        }
    }

    private void Cleanup()
    {
        try { _reader?.Dispose(); } catch { }
        _reader = null;

        try { _writer?.Dispose(); } catch { }
        _writer = null;

        try { _networkStream?.Dispose(); } catch { }
        _networkStream = null;

        try { _tcpClient?.Close(); _tcpClient?.Dispose(); } catch { }
        _tcpClient = null;

        try { _cts?.Dispose(); } catch { }
        _cts = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Disconnect("Client đã bị Dispose.");
        _sendLock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Disconnect("Client đã bị DisposeAsync.");
        if (_receiveTask != null)
        {
            try { await _receiveTask.ConfigureAwait(false); } catch { }
        }
        _sendLock.Dispose();
    }
}
