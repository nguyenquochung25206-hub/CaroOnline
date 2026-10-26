using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Caro.Server.Networking;

public sealed class ClientSession
{
    // Luu ket noi TCP cua Client nay.
    private readonly TcpClient _client;

    // Ham ghi log su kien.
    private readonly Action<string> _log;

    // Ham goi khi Client ngat ket noi de xoa khoi ClientManager.
    private readonly Func<ClientSession, Task> _onDisconnected;

    // Dam bao khong co nhieu thao tac gui du lieu cung luc.
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // Tao ID duy nhat cho moi phien Client.
    private readonly Guid _id = Guid.NewGuid();

    // Danh dau phien Client da dong hay chua.
    private bool _closed;

    // Cho phep cac lop khac lay ID cua Client.
    public Guid Id => _id;

    // Lay dia chi va port cua Client dang ket noi.
    public string RemoteAddress =>
        _client.Client.RemoteEndPoint?.ToString() ?? "Unknown";

    // Khoi tao phien Client voi ket noi va cac ham xu ly can thiet.
    public ClientSession(
        TcpClient client,
        Action<string> log,
        Func<ClientSession, Task> onDisconnected)
    {
        _client = client;
        _log = log;
        _onDisconnected = onDisconnected;
    }

    // Doc va xu ly message trong suot thoi gian Client ket noi.
    public async Task RunAsync()
    {
        try
        {
            // Lay luong du lieu TCP cua Client.
            using NetworkStream stream = _client.GetStream();

            // Doc du lieu dang text UTF-8 theo tung dong.
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(false));

            // Tao cong cu gui du lieu cho Client.
            using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(false))
            {
                AutoFlush = true,
                NewLine = "\n"
            };

            // Ghi log khi Client ket noi.
            _log($"Client connected: {RemoteAddress}");

            // Lap doc message cho den khi Client dong ket noi.
            while (!_closed)
            {
                // Doc mot dong JSON tu Client.
                string? line = await reader.ReadLineAsync();

                // Neu nhan null thi Client da dong ket noi.
                if (line == null)
                    break;

                // Chuyen message den ham xu ly.
                await HandleMessageAsync(line, writer);
            }
        }
        catch (IOException ex)
        {
            // Ghi log loi doc hoac ghi du lieu mang.
            _log($"Network error {RemoteAddress}: {ex.Message}");
        }
        catch (SocketException ex)
        {
            // Ghi log loi socket.
            _log($"Socket error {RemoteAddress}: {ex.Message}");
        }
        catch (ObjectDisposedException)
        {
            // Ket noi da bi dong nen khong can xu ly tiep.
        }
        catch (JsonException)
        {
            // Ghi log khi du lieu JSON khong hop le.
            _log($"Invalid JSON from {RemoteAddress}");
        }
        finally
        {
            // Dong ket noi Client.
            Close();

            // Bao cho ClientManager biet Client da ngat ket noi.
            await _onDisconnected(this);

            // Ghi log sau khi Client ngat ket noi.
            _log($"Client disconnected: {RemoteAddress}");
        }
    }

    // Phan tich message JSON va chon hanh dong phu hop.
    private async Task HandleMessageAsync(
        string json,
        StreamWriter writer)
    {
        // Phan tich chuoi JSON thanh doi tuong co the doc duoc.
        using JsonDocument document = JsonDocument.Parse(json);

        // Lay phan goc cua JSON.
        JsonElement root = document.RootElement;

        // Kiem tra message co truong type hay khong.
        if (!root.TryGetProperty("type", out JsonElement typeElement))
        {
            // Tra loi neu message thieu loai.
            await SendAsync(writer, new
            {
                type = "ERROR",
                message = "Missing message type"
            });

            return;
        }

        // Lay gia tri loai message.
        string? type = typeElement.GetString();

        // Xu ly theo loai message nhan duoc.
        switch (type?.ToUpperInvariant())
        {
            case "HELLO":
                // Client chao Server, Server tra ve WELCOME.
                await SendAsync(writer, new
                {
                    type = "WELCOME",
                    clientId = _id.ToString(),
                    message = "Connected to Caro Server"
                });
                break;

            case "PING":
                // Client gui PING, Server phan hoi PONG.
                await SendAsync(writer, new
                {
                    type = "PONG"
                });
                break;

            default:
                // Tra loi neu Server chua ho tro loai message nay.
                await SendAsync(writer, new
                {
                    type = "ERROR",
                    message = "Unknown message type"
                });
                break;
        }
    }

    // Ma hoa doi tuong thanh JSON va gui cho Client.
    private async Task SendAsync(
        StreamWriter writer,
        object message)
    {
        // Chuyen doi tuong message thanh chuoi JSON.
        string json = JsonSerializer.Serialize(message);

        // Cho phep tung tac vu gui du lieu lan luot.
        await _sendLock.WaitAsync();

        try
        {
            // Chi gui khi ket noi chua dong.
            if (!_closed)
                await writer.WriteLineAsync(json);
        }
        finally
        {
            // Giai phong khoa gui du lieu.
            _sendLock.Release();
        }
    }

    // Dong ket noi Client.
    public void Close()
    {
        // Neu da dong thi khong can dong lai.
        if (_closed)
            return;

        // Danh dau Client da dong.
        _closed = true;

        try
        {
            // Dong socket va ket thuc ket noi TCP.
            _client.Close();
        }
        catch (SocketException)
        {
            // Bo qua loi phat sinh trong qua trinh dong socket.
        }
    }
}