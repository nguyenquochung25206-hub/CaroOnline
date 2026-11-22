
// TODO TV5: Đọc IP/port từ cấu hình hoặc tham số; kiểm tra port hợp lệ.
using System;
using System.Net;

namespace Caro.Server.Configuration;

/// <summary>
/// Cấu hình chạy Server: địa chỉ IP và cổng lắng nghe.
/// Mặc định 0.0.0.0:5000. Có thể đọc từ tham số dòng lệnh:
///     dotnet run -- [ip] [port]
/// </summary>
public sealed class ServerOptions
{
    public const string DefaultIp = "0.0.0.0";
    public const int DefaultPort = 5000;
    public const int MinPort = 1;
    public const int MaxPort = 65535;

    public const string InvalidIpMessage = "Invalid IP address.";
    public const string InvalidPortMessage = "Port must be between 1 and 65535.";

    private string _ip = DefaultIp;
    private int _port = DefaultPort;

    /// <summary>Địa chỉ IP lắng nghe. Gán giá trị sai định dạng sẽ ném ArgumentException.</summary>
    public string Ip
    {
        get => _ip;
        set
        {
            if (!IsValidIp(value))
                throw new ArgumentException(InvalidIpMessage, nameof(value));

            _ip = value;
        }
    }

    /// <summary>Cổng lắng nghe. Gán ngoài 1–65535 sẽ ném ArgumentOutOfRangeException.</summary>
    public int Port
    {
        get => _port;
        set
        {
            if (!IsValidPort(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, InvalidPortMessage);

            _port = value;
        }
    }

    public static bool IsValidPort(int port) => port >= MinPort && port <= MaxPort;

    public static bool IsValidIp(string? ip) =>
        !string.IsNullOrWhiteSpace(ip) && IPAddress.TryParse(ip, out _);

    /// <summary>
    /// Đọc cấu hình từ tham số dòng lệnh: args[0] = IP, args[1] = port (cả hai đều tùy chọn).
    /// Trả false kèm thông báo lỗi nếu tham số sai; không ném exception.
    /// </summary>
    public static bool TryParse(string[] args, out ServerOptions options, out string error)
    {
        options = new ServerOptions();
        error = string.Empty;

        if (args == null || args.Length == 0)
            return true;

        if (!IsValidIp(args[0]))
        {
            error = InvalidIpMessage;
            return false;
        }
        options.Ip = args[0];

        if (args.Length >= 2)
        {
            if (!int.TryParse(args[1], out int port) || !IsValidPort(port))
            {
                error = InvalidPortMessage;
                return false;
            }
            options.Port = port;
        }

        return true;
    }
}
