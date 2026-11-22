using System.Net;
using System.Net.Sockets;
using Caro.Server.Logging;
using Caro.Server.Services;
using Caro.Shared.Messages;

namespace Caro.Server.Networking;

// Lang nghe va tiep nhan nhieu Client ket noi qua TCP, tao phien (ClientSession)
// rieng cho tung Client, va dung Server mot cach an toan (TV1 - Tuan 2 + Tuan 3).
public sealed class TcpServer
{
    // So Client toi da cung luc. Vuot qua se bi tu choi de Server khong qua tai.
    private const int DefaultMaxClients = 200;

    // Thoi gian toi da cho cac Client dong het khi tat Server (mili giay).
    private const int ShutdownWaitMilliseconds = 3000;

    // Thoi gian toi da de bao cho Client biet Server sap tat.
    private static readonly TimeSpan ShutdownNoticeTimeout = TimeSpan.FromSeconds(2);

    // KeepAlive: sau 30 giay khong co du lieu thi thu kiem tra, moi 10 giay mot lan,
    // that bai 3 lan thi coi la mat ket noi (tong khoang 60 giay).
    private const int KeepAliveTimeSeconds = 30;
    private const int KeepAliveIntervalSeconds = 10;
    private const int KeepAliveRetryCount = 3;

    // Dia chi IP ma Server se lang nghe.
    private readonly IPAddress _ipAddress;

    // Cong mang ma Server se su dung.
    private readonly int _port;

    // Doi tuong quan ly cac Client.
    private readonly ClientManager _clientManager;

    // So Client toi da cho phep.
    private readonly int _maxClients;

    // Thoi gian Client duoc phep im lang truoc khi bi ngat ket noi.
    private readonly TimeSpan? _idleTimeout;

    // Khoi tao TCP Server voi IP, port va ClientManager.
    // maxClients, idleTimeout la tuy chon nen Program.cs cu van bien dich binh thuong.
    //  - idleTimeout = null: dung mac dinh (60 giay); TimeSpan.Zero: khong timeout.
    public TcpServer(
        IPAddress ipAddress,
        int port,
        ClientManager clientManager,
        int maxClients = DefaultMaxClients,
        TimeSpan? idleTimeout = null)
    {
        _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
        _port = port;
        _clientManager = clientManager
            ?? throw new ArgumentNullException(nameof(clientManager));
        _maxClients = maxClients > 0 ? maxClients : DefaultMaxClients;
        _idleTimeout = idleTimeout;
    }

    // Khoi dong Server va tiep tuc chay cho den khi co yeu cau dung.
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Tao TCP Listener voi dia chi IP va port da cau hinh.
        var listener = new TcpListener(_ipAddress, _port);

        try
        {
            // Bat dau lang nghe ket noi tu Client.
            listener.Start();

            // Thong bao dia chi, port va cau hinh timeout.
            ServerLogger.Info($"[SERVER] Listening on {_ipAddress}:{_port}");
            ServerLogger.Info($"[SERVER] Idle timeout: {DescribeIdleTimeout()}. Log folder: {ServerLogger.LogDirectory}");

            // Khi token bi huy, dung Listener de thoat khoi viec cho ket noi.
            using CancellationTokenRegistration registration =
                cancellationToken.Register(() =>
                {
                    listener.Stop();
                });

            // Lap cho den khi nhan duoc yeu cau dung Server.
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    // Cho Client moi ket noi den Server.
                    // Ham bat dong bo nen khong can chan luong xu ly.
                    client = await listener.AcceptTcpClientAsync(
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Thoat vong lap khi Server bi huy.
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Listener da bi dung.
                    break;
                }
                catch (SocketException ex)
                {
                    // Loi socket do dung Listener thi thoat.
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Loi accept tam thoi: ghi log va tiep tuc nhan Client khac,
                    // khong de 1 loi lam Server ngung hoat dong.
                    ServerLogger.Error("[SERVER] Accept error", ex);
                    await Task.Delay(200);
                    continue;
                }

                // Qua tai thi tu choi Client moi.
                if (_clientManager.ClientCount >= _maxClients)
                {
                    ServerLogger.Warn($"[SERVER] Rejected a client: server full ({_maxClients}).");
                    CloseQuietly(client);
                    continue;
                }

                ClientSession session;

                try
                {
                    ConfigureSocket(client);

                    // Tao phien lam viec rieng cho Client vua ket noi.
                    // - RemoveAsync: goi khi Client ngat ket noi.
                    // - DispatchAsync: chuyen message nghiep vu cho TV5 xu ly.
                    // - _idleTimeout: Client im lang qua lau thi bi ngat.
                    session = new ClientSession(
                        client,
                        _clientManager.RemoveAsync,
                        _clientManager.DispatchAsync,
                        _idleTimeout);
                }
                catch (Exception ex)
                {
                    // Client da ngat ngay luc vua ket noi.
                    ServerLogger.Error("[SERVER] Could not start session", ex);
                    CloseQuietly(client);
                    continue;
                }

                // Them Client vao danh sach quan ly.
                _clientManager.Add(session);

                // Chay xu ly Client rieng de Server tiep tuc nhan ket noi moi.
                _ = Task.Run(() => RunSessionAsync(session));
            }
        }
        finally
        {
            // Ngung nhan ket noi moi.
            listener.Stop();

            // Bao cho Client biet Server sap tat, roi dong tung Client.
            await NotifyShutdownAsync();
            _clientManager.CloseAll();

            // Cho cac phien Client don dep xong (toi da vai giay).
            int waited = 0;
            while (_clientManager.ClientCount > 0 &&
                   waited < ShutdownWaitMilliseconds)
            {
                await Task.Delay(100);
                waited += 100;
            }

            if (_clientManager.ClientCount > 0)
                ServerLogger.Warn($"[SERVER] {_clientManager.ClientCount} client(s) did not close in time.");

            // Ghi log Server da dung.
            ServerLogger.Info("[SERVER] Stopped.");
        }
    }

    // Chay 1 phien Client; moi loi duoc bat de khong lam sap Server.
    private async Task RunSessionAsync(ClientSession session)
    {
        try
        {
            await session.RunAsync();
        }
        catch (Exception ex)
        {
            ServerLogger.Error($"[SERVER] Session {session.RemoteAddress} crashed", ex);
            session.Close(DisconnectReason.Error);
        }
    }

    // Gui thong bao "Server sap tat" cho moi Client (toi da vai giay, khong chan viec tat Server).
    private async Task NotifyShutdownAsync()
    {
        if (_clientManager.ClientCount == 0)
            return;

        try
        {
            var message = new NetworkMessage
            {
                Type = MessageTypes.Error,
                Payload = new { message = "Server is shutting down" }
            };

            await _clientManager.BroadcastAsync(message)
                .WaitAsync(ShutdownNoticeTimeout);
        }
        catch (Exception ex)
        {
            ServerLogger.Warn($"[SERVER] Could not notify clients before shutdown: {ex.Message}");
        }
    }

    // NoDelay: gui nuoc di ngay, khong cho gop goi (giam tre).
    // KeepAlive: phat hien Client mat mang (rut day, tat Wi-Fi) ma khong dong ket noi.
    private static void ConfigureSocket(TcpClient client)
    {
        client.NoDelay = true;

        Socket socket = client.Client;
        socket.SetSocketOption(
            SocketOptionLevel.Socket,
            SocketOptionName.KeepAlive,
            true);

        try
        {
            // Mac dinh Windows chi kiem tra sau ~2 gio, qua lau; chinh ngan lai.
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, KeepAliveTimeSeconds);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, KeepAliveIntervalSeconds);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, KeepAliveRetryCount);
        }
        catch (Exception)
        {
            // He dieu hanh khong ho tro chinh KeepAlive: van con idle timeout cua ClientSession.
        }
    }

    // Mo ta cau hinh timeout de ghi log.
    private string DescribeIdleTimeout()
    {
        TimeSpan timeout = _idleTimeout ?? ClientSession.DefaultIdleTimeout;

        return timeout > TimeSpan.Zero
            ? $"{timeout.TotalSeconds:0} seconds"
            : "disabled";
    }

    // Dong ket noi ma khong nem loi.
    private static void CloseQuietly(TcpClient client)
    {
        try
        {
            client.Close();
        }
        catch (Exception)
        {
        }
    }
}
