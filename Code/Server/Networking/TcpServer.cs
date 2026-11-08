using System.Net;
using System.Net.Sockets;
using Caro.Server.Services;

namespace Caro.Server.Networking;

// Lang nghe va tiep nhan nhieu Client ket noi qua TCP, tao phien (ClientSession)
// rieng cho tung Client, va dung Server mot cach an toan (TV1 - Tuan 2).
public sealed class TcpServer
{
    // So Client toi da cung luc. Vuot qua se bi tu choi de Server khong qua tai.
    private const int DefaultMaxClients = 200;

    // Thoi gian toi da cho cac Client dong het khi tat Server (mili giay).
    private const int ShutdownWaitMilliseconds = 3000;

    // Dia chi IP ma Server se lang nghe.
    private readonly IPAddress _ipAddress;

    // Cong mang ma Server se su dung.
    private readonly int _port;

    // Doi tuong quan ly cac Client.
    private readonly ClientManager _clientManager;

    // So Client toi da cho phep.
    private readonly int _maxClients;

    // Khoi tao TCP Server voi IP, port va ClientManager.
    // maxClients la tuy chon nen Program.cs cu van bien dich binh thuong.
    public TcpServer(
        IPAddress ipAddress,
        int port,
        ClientManager clientManager,
        int maxClients = DefaultMaxClients)
    {
        _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
        _port = port;
        _clientManager = clientManager
            ?? throw new ArgumentNullException(nameof(clientManager));
        _maxClients = maxClients > 0 ? maxClients : DefaultMaxClients;
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

            // Thong bao dia chi va port dang lang nghe.
            Log($"[SERVER] Listening on {_ipAddress}:{_port}");

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
                    Log($"[SERVER] Accept error: {ex.Message}");
                    await Task.Delay(200);
                    continue;
                }

                // Qua tai thi tu choi Client moi.
                if (_clientManager.ClientCount >= _maxClients)
                {
                    Log($"[SERVER] Rejected a client: server full ({_maxClients}).");
                    CloseQuietly(client);
                    continue;
                }

                ClientSession session;

                try
                {
                    ConfigureSocket(client);

                    // Tao phien lam viec rieng cho Client vua ket noi.
                    // - Log: ghi log.
                    // - RemoveAsync: goi khi Client ngat ket noi.
                    // - DispatchAsync: chuyen message nghiep vu cho TV5 xu ly.
                    session = new ClientSession(
                        client,
                        Log,
                        _clientManager.RemoveAsync,
                        _clientManager.DispatchAsync);
                }
                catch (Exception ex)
                {
                    // Client da ngat ngay luc vua ket noi.
                    Log($"[SERVER] Could not start session: {ex.Message}");
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
            // Dung lang nghe khi Server ket thuc.
            listener.Stop();

            // Dong cac Client dang duoc quan ly.
            _clientManager.CloseAll();

            // Cho cac phien Client don dep xong (toi da vai giay).
            int waited = 0;
            while (_clientManager.ClientCount > 0 &&
                   waited < ShutdownWaitMilliseconds)
            {
                await Task.Delay(100);
                waited += 100;
            }

            // Ghi log Server da dung.
            Log("[SERVER] Stopped.");
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
            Log($"[SERVER] Session {session.RemoteAddress} crashed: {ex.Message}");
            session.Close();
        }
    }

    // NoDelay: gui nuoc di ngay, khong cho gop goi (giam tre).
    // KeepAlive: giup phat hien Client mat mang ma khong dong ket noi.
    private static void ConfigureSocket(TcpClient client)
    {
        client.NoDelay = true;
        client.Client.SetSocketOption(
            SocketOptionLevel.Socket,
            SocketOptionName.KeepAlive,
            true);
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

    // Ghi log kem gio (sau nay co the doi sang ServerLogger).
    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
