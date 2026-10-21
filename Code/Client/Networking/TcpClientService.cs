using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Caro.Client.Networking;

/// <summary>
/// Quản lý kết nối TCP Socket cấp thấp: kết nối, ngắt kết nối, gửi/nhận dữ liệu dạng newline-delimited string bất đồng bộ.
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
    private bool _isDisposed;

    public event Action<string>? MessageReceived;
    public event Action? Disconnected;
    public event Action<Exception>? ErrorOccurred;

    public bool IsConnected => _tcpClient is { Connected: true } && !_isDisposed;

    /// <summary>
    /// Kết nối tới TCP server với timeout xác định.
    /// </summary>
    public async Task ConnectAsync(string host, int port, int timeoutMilliseconds = 5000, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            throw new InvalidOperationException("Client đã được kết nối trước đó.");
        }

        Cleanup();

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

            // Bắt đầu vòng lặp đọc dữ liệu nền
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), CancellationToken.None);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            Cleanup();
            throw new TimeoutException($"Hết thời gian chờ kết nối tới {host}:{port} ({timeoutMilliseconds}ms).");
        }
        catch (Exception ex)
        {
            Cleanup();
            ErrorOccurred?.Invoke(ex);
            throw;
        }
    }

    /// <summary>
    /// Gửi một dòng văn bản (kết thúc bằng newline) tới server.
    /// </summary>
    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _writer == null)
        {
            throw new InvalidOperationException("Không thể gửi vì chưa kết nối tới server.");
        }

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(message.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            Disconnect();
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Vòng lặp nhận dữ liệu từ NetworkStream theo từng dòng (newline-delimited).
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                // Khi server đóng kết nối, ReadLineAsync trả về null
                if (line is null)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    MessageReceived?.Invoke(line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Hủy kết nối bình thường
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            // Kết nối bị ngắt từ phía server hoặc mạng
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
        }
        finally
        {
            Disconnect();
        }
    }

    /// <summary>
    /// Ngắt kết nối và đóng tài nguyên an toàn.
    /// </summary>
    public void Disconnect()
    {
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

        if (wasConnected)
        {
            Disconnected?.Invoke();
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
        Disconnect();
        _sendLock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Disconnect();
        if (_receiveTask != null)
        {
            try { await _receiveTask.ConfigureAwait(false); } catch { }
        }
        _sendLock.Dispose();
    }
}
