namespace Caro.Client.Services;

/// <summary>
/// Quản lý cấu hình kết nối phía Client (IP, Port, Tên người chơi) - Nhiệm vụ TV3 (Tuần 4).
/// </summary>
public static class ClientSettings
{
    public const int DefaultPort = 5000;
    public const string DefaultHost = "127.0.0.1";
    public const string DefaultPlayerName = "Player";

    public static string Host => ConnectionService.LoadConfig().LastHost;
    public static int Port => ConnectionService.LoadConfig().LastPort;
    public static string PlayerName => ConnectionService.LoadConfig().LastPlayerName;

    /// <summary>
    /// Lưu cấu hình kết nối gần nhất.
    /// </summary>
    public static void Save(string host, int port, string playerName)
    {
        ConnectionService.SaveConfig(host, port, playerName);
    }

    /// <summary>
    /// Nạp cấu hình kết nối gần nhất.
    /// </summary>
    public static ClientConfig Load() => ConnectionService.LoadConfig();
}
