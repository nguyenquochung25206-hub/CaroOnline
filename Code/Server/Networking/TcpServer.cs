using System.Net;
using System.Net.Sockets;
using Caro.Server.Services;

namespace Caro.Server.Networking;

public sealed class TcpServer
{
    // Dia chi IP ma Server se lang nghe.
    private readonly IPAddress _ipAddress;

    // Cong mang ma Server se su dung.
    private readonly int _port;

    // Doi tuong quan ly cac Client.
    private readonly ClientManager _clientManager;

    // Doi tuong lang nghe ket noi TCP.
    private TcpListener? _listener;

    // Khoi tao TCP Server voi IP, port va ClientManager.
    public TcpServer(
        IPAddress ipAddress,
        int port,
        ClientManager clientManager)
    {
        _ipAddress = ipAddress;
        _port = port;
        _clientManager = clientManager;
    }

    // Khoi dong Server va tiep tuc chay cho den khi co yeu cau dung.
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Tao TCP Listener voi dia chi IP va port da cau hinh.
        _listener = new TcpListener(_ipAddress, _port);

        try
        {
            // Bat dau lang nghe ket noi tu Client.
            _listener.Start();

            // Thong bao dia chi va port dang lang nghe.
            Console.WriteLine(
                $"[SERVER] Listening on {_ipAddress}:{_port}");

            // Khi token bi huy, dung Listener de thoat khoi viec cho ket noi.
            using CancellationTokenRegistration registration =
                cancellationToken.Register(() =>
                {
                    _listener.Stop();
                });

            // Lap cho den khi nhan duoc yeu cau dung Server.
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    // Cho Client moi ket noi den Server.
                    // Ham bat dong bo nen khong can chan luong xu ly.
                    client = await _listener.AcceptTcpClientAsync(
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Thoat vong lap khi Server bi huy.
                    break;
                }
                catch (SocketException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    // Bo qua loi socket xuat hien do dung Listener.
                    break;
                }

                // Tao phien lam viec rieng cho Client vua ket noi.
                var session = new ClientSession(
                    client,
                    Console.WriteLine,
                    _clientManager.RemoveAsync);

                // Them Client vao danh sach quan ly.
                _clientManager.Add(session);

                // Chay xu ly Client rieng de Server tiep tuc nhan ket noi moi.
                _ = Task.Run(() => session.RunAsync());
            }
        }
        finally
        {
            // Dung lang nghe khi Server ket thuc.
            _listener.Stop();

            // Dong cac Client dang duoc quan ly.
            _clientManager.CloseAll();

            // Ghi log Server da dung.
            Console.WriteLine("[SERVER] Stopped.");
        }
    }
}