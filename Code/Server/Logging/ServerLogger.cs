using System.Text;

namespace Caro.Server.Logging;

// Muc do cua dong log.
public enum ServerLogLevel
{
    Info,
    Warn,
    Error
}

// Ghi log cho Server (TV1 - Tuan 3):
//  - Moi dong log co thoi gian va muc do (INFO / WARN / ERROR).
//  - Ghi ra console (co mau) va ghi them vao file logs/server-yyyyMMdd.log.
//  - An toan khi nhieu luong cung ghi (dung lock).
//  - KHONG ghi mat khau hay noi dung message cua nguoi choi; chi ghi su kien
//    (ket noi, ngat ket noi, loi, timeout). Ten nguoi choi duoc lam sach truoc khi ghi.
public static class ServerLogger
{
    // Gioi han do dai 1 dong log de Client khong the lam phinh file log.
    private const int MaxMessageLength = 500;

    // Khoa dung chung cho console va file.
    private static readonly object Sync = new();

    private static readonly UTF8Encoding Utf8 = new(false);

    // Mac dinh ghi vao thu muc logs canh file chay cua Server (nam trong bin/, khong bi commit).
    private static string _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

    private static bool _writeToFile = true;

    // Thu muc dang chua file log.
    public static string LogDirectory
    {
        get
        {
            lock (Sync)
            {
                return _logDirectory;
            }
        }
    }

    // Doi thu muc log hoac tat ghi file (tuy chon, khong goi thi dung mac dinh).
    public static void Configure(string? logDirectory = null, bool writeToFile = true)
    {
        lock (Sync)
        {
            if (!string.IsNullOrWhiteSpace(logDirectory))
                _logDirectory = logDirectory;

            _writeToFile = writeToFile;
        }
    }

    // Ghi log thong tin.
    public static void Info(string message)
    {
        Write(ServerLogLevel.Info, message);
    }

    // Ghi log canh bao (vi du Client timeout, message sai).
    public static void Warn(string message)
    {
        Write(ServerLogLevel.Warn, message);
    }

    // Ghi log loi, co the kem exception (chi ghi loai loi va thong bao, khong ghi stack trace).
    public static void Error(string message, Exception? exception = null)
    {
        string text = exception == null
            ? message
            : $"{message} ({exception.GetType().Name}: {exception.Message})";

        Write(ServerLogLevel.Error, text);
    }

    // Client moi ket noi.
    public static void ClientConnected(string remoteAddress)
    {
        Info($"Client connected: {remoteAddress}");
    }

    // Client ngat ket noi. abnormal = true neu ngat bat thuong (timeout, mat mang, du lieu sai).
    public static void ClientDisconnected(
        string remoteAddress,
        string playerName,
        string reason,
        bool abnormal)
    {
        string text = $"Client disconnected: {remoteAddress} ({playerName}) - {reason}";

        if (abnormal)
            Warn(text);
        else
            Info(text);
    }

    // Message khong hop le. Chi ghi ly do, KHONG ghi noi dung message.
    public static void InvalidMessage(string remoteAddress, string reason)
    {
        Warn($"Invalid message from {remoteAddress}: {reason}");
    }

    // Ham ghi chung: tao dong log roi ghi ra console va file.
    private static void Write(ServerLogLevel level, string message)
    {
        DateTime now = DateTime.Now;
        string line = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{LevelText(level)}] {Sanitize(message)}";

        lock (Sync)
        {
            WriteConsole(level, line);

            if (_writeToFile)
                WriteFile(now, line);
        }
    }

    private static void WriteConsole(ServerLogLevel level, string line)
    {
        try
        {
            Console.ForegroundColor = level switch
            {
                ServerLogLevel.Warn => ConsoleColor.Yellow,
                ServerLogLevel.Error => ConsoleColor.Red,
                _ => ConsoleColor.Gray
            };

            Console.WriteLine(line);
        }
        catch (Exception)
        {
            // Console bi dong/chuyen huong: bo qua, khong lam Server loi.
        }
        finally
        {
            try
            {
                Console.ResetColor();
            }
            catch (Exception)
            {
            }
        }
    }

    // Moi ngay mot file log. Loi ghi file (day o dia, het quyen) khong lam Server sap.
    private static void WriteFile(DateTime now, string line)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);

            string path = Path.Combine(_logDirectory, $"server-{now:yyyyMMdd}.log");
            File.AppendAllText(path, line + Environment.NewLine, Utf8);
        }
        catch (Exception ex)
        {
            // Tat ghi file de khong bao loi lien tuc; van con log tren console.
            _writeToFile = false;

            try
            {
                Console.WriteLine($"[LOGGER] Cannot write log file, file logging disabled: {ex.Message}");
            }
            catch (Exception)
            {
            }
        }
    }

    // Doi ky tu xuong dong/dieu khien thanh khoang trang (chong gia mao dong log)
    // va cat bot neu qua dai.
    private static string Sanitize(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return "";

        var builder = new StringBuilder(Math.Min(message.Length, MaxMessageLength));

        foreach (char c in message)
        {
            builder.Append(char.IsControl(c) ? ' ' : c);

            if (builder.Length >= MaxMessageLength)
            {
                builder.Append("...");
                break;
            }
        }

        return builder.ToString();
    }

    private static string LevelText(ServerLogLevel level)
    {
        return level switch
        {
            ServerLogLevel.Warn => "WARN ",
            ServerLogLevel.Error => "ERROR",
            _ => "INFO "
        };
    }
}
