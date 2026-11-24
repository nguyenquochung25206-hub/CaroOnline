using System.Net;
using System.Net.Sockets;
using Caro.Server.Configuration;
using Caro.Server.Handlers;
using Caro.Server.Logging;
using Caro.Server.Networking;
using Caro.Server.Services;

namespace Caro.Server;

internal class Program
{
    // Ham Main la diem bat dau khi chay ung dung Server.
    //   dotnet run -- [ip] [port]      (mac dinh 0.0.0.0 5000)
    private static async Task Main(string[] args)
    {
        // Doc IP/port tu tham so dong lenh (ServerOptions cua TV5 kiem tra dinh dang).
        if (!ServerOptions.TryParse(args, out ServerOptions options, out string error))
        {
            Console.WriteLine(error);
            return;
        }

        IPAddress ipAddress = IPAddress.Parse(options.Ip);

        // Tao doi tuong quan ly danh sach Client dang ket noi.
        var clientManager = new ClientManager();

        // Cac dich vu nghiep vu: phong choi / van co (TV5) va chat (TV6).
        var roomService = new RoomService();
        var chatHandler = new ChatHandler(roomService, clientManager);

        // Noi tang mang voi cac dich vu: nhan CREATE_ROOM, JOIN_ROOM, MOVE, CHAT...
        // va don phong khi Client mat ket noi.
        var router = new MessageRouter(clientManager, roomService, chatHandler);
        router.Attach();

        // Tao TCP Server voi IP, port va bo quan ly Client.
        var server = new TcpServer(
            ipAddress,
            options.Port,
            clientManager);

        // Tao token de yeu cau Server dung mot cach co kiem soat.
        using var cts = new CancellationTokenSource();

        // Lang nghe su kien nhan Ctrl+C tu nguoi dung.
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            // Ngan chuong trinh ket thuc dot ngot.
            eventArgs.Cancel = true;

            // Gui yeu cau dung Server.
            cts.Cancel();
        };

        // Hien thi thong bao khi bat dau chuong trinh.
        Console.WriteLine("Caro Online Server");
        Console.WriteLine("Press Ctrl+C to stop the server.");

        try
        {
            // Khoi dong Server va cho den khi Server dung.
            await server.StartAsync(cts.Token);
        }
        catch (SocketException ex)
        {
            // Hien thi loi lien quan den socket, vi du port da duoc su dung.
            Console.WriteLine($"Server socket error: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            // Thong bao khi Server bi yeu cau dung.
            Console.WriteLine("Server cancelled.");
        }
        catch (Exception ex)
        {
            // Loi bat ngo: ghi log va dat ma thoat khac 0 de nguoi chay biet.
            ServerLogger.Error("[SERVER] Fatal error", ex);
            Environment.ExitCode = 1;
        }
    }
}
