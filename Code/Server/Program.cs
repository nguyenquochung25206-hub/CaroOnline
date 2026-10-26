using System.Net;
using System.Net.Sockets;
using Caro.Server.Networking;
using Caro.Server.Services;

namespace Caro.Server;

internal class Program
{
    // Ham Main la diem bat dau khi chay ung dung Server.
    private static async Task Main(string[] args)
    {
        // Neu nguoi dung truyen IP khi chay chuong trinh thi su dung IP do.
        // Neu khong truyen thi Server lang nghe tren tat ca dia chi IPv4.
        string ipText = args.Length > 0 ? args[0] : "0.0.0.0";

        // Port mac dinh cua Server la 5000.
        int port = 5000;

        // Neu co tham so port thi kiem tra port co hop le hay khong.
        if (args.Length > 1 &&
            (!int.TryParse(args[1], out port) ||
             port < 1 || port > 65535))
        {
            Console.WriteLine("Port must be between 1 and 65535.");
            return;
        }

        // Kiem tra chuoi IP co dung dinh dang dia chi IP hay khong.
        if (!IPAddress.TryParse(ipText, out IPAddress? ipAddress))
        {
            Console.WriteLine("Invalid IP address.");
            return;
        }

        // Tao doi tuong quan ly danh sach Client dang ket noi.
        var clientManager = new ClientManager();

        // Tao TCP Server voi IP, port va bo quan ly Client.
        var server = new TcpServer(
            ipAddress,
            port,
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
    }
}