using System.Net;
using System.Net.Sockets;
using System.Text;
using Caro.Client.Forms;
using Caro.Client.Networking;
using Caro.Client.Services;
using Caro.Shared.Messages;
using Caro.Shared.Protocol;
using Xunit;

namespace Caro.Tests;

/// <summary>
/// Test Script Tuần 1: Kiểm thử kết nối, form cấu hình, timeout và đo ping (TC_020 - TC_026).
/// </summary>
public class ClientWeek1Tests
{
    // TC_020: ConnectForm - Kết nối thành công tới Server
    [Fact]
    public async Task TC_020_ConnectForm_ConnectSuccess()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync();
            using var reader = new StreamReader(accepted.GetStream(), Encoding.UTF8);
            string? helloLine = await reader.ReadLineAsync();
            return helloLine;
        });

        using var connectionService = new ConnectionService();
        using var form = new ConnectForm(connectionService, "127.0.0.1", port, "TestPlayer");

        bool connected = await connectionService.ConnectAsync("127.0.0.1", port, "TestPlayer");
        string? receivedHello = await serverTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(connected);
        Assert.True(connectionService.IsConnected);
        Assert.NotNull(receivedHello);
        Assert.Contains(MessageTypes.Hello, receivedHello);
    }

    // TC_021: ConnectForm - Bỏ trống IP
    [Fact]
    public void TC_021_ConnectForm_EmptyIP()
    {
        using var connectionService = new ConnectionService();
        using var form = new ConnectForm(connectionService, "", 8888, "TestPlayer");

        form.SetHost("");
        form.TriggerConnect();

        Assert.Contains("IP", form.StatusMessage);
        Assert.False(connectionService.IsConnected);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await connectionService.ConnectAsync("", 8888, "TestPlayer"));
    }

    // TC_022: ConnectForm - Port ngoài khoảng cho phép
    [Fact]
    public async Task TC_022_ConnectForm_InvalidPort()
    {
        using var connectionService = new ConnectionService();
        using var form = new ConnectForm(connectionService, "127.0.0.1", 8888, "TestPlayer");

        form.SetPort(70000);
        Assert.True(form.ServerPort <= 65535);

        form.SetPort(-5);
        Assert.True(form.ServerPort >= 1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await connectionService.ConnectAsync("127.0.0.1", 0, "TestPlayer"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await connectionService.ConnectAsync("127.0.0.1", 70000, "TestPlayer"));
    }

    // TC_023: ConnectForm - Server chưa chạy hoặc sai port
    [Fact]
    public async Task TC_023_ConnectForm_ServerNotRunningOrWrongPort()
    {
        int unusedPort;
        using (var temp = new TcpListener(IPAddress.Loopback, 0))
        {
            temp.Start();
            unusedPort = ((IPEndPoint)temp.LocalEndpoint).Port;
        }

        using var connectionService = new ConnectionService();
        bool success = await connectionService.ConnectAsync("127.0.0.1", unusedPort, "TestPlayer");

        Assert.False(success);
        Assert.False(connectionService.IsConnected);
    }

    // TC_024: ConnectForm / TcpClientService - Giao diện không bị treo khi chờ kết nối
    [Fact]
    public async Task TC_024_ConnectForm_TcpClientService_NoUIFreezeOnTimeout()
    {
        using var tcpService = new TcpClientService();

        int unusedPort;
        using (var temp = new TcpListener(IPAddress.Loopback, 0))
        {
            temp.Start();
            unusedPort = ((IPEndPoint)temp.LocalEndpoint).Port;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await tcpService.ConnectAsync("127.0.0.1", unusedPort, timeoutMilliseconds: 800);
        });

        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < 3000);
    }

    // TC_025: MainForm - Ngắt kết nối chủ động
    [Fact]
    public async Task TC_025_MainForm_ActiveDisconnect()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync();
            byte[] buf = new byte[128];
            while (await accepted.GetStream().ReadAsync(buf) > 0) { }
        });

        using var mainForm = new MainForm();
        bool connected = await mainForm.ConnectionService.ConnectAsync("127.0.0.1", port, "TestPlayer");
        Assert.True(connected);
        Assert.True(mainForm.ConnectionService.IsConnected);

        mainForm.TriggerDisconnect();
        await Task.Delay(100);

        Assert.False(mainForm.ConnectionService.IsConnected);
    }

    // TC_026: MainForm / ConnectionService - Đo độ trễ PING
    [Fact]
    public async Task TC_026_MainForm_ConnectionService_PingLatency()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync();
            using var reader = new StreamReader(accepted.GetStream(), Encoding.UTF8);
            using var writer = new StreamWriter(accepted.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };

            while (true)
            {
                string? line = await reader.ReadLineAsync();
                if (line == null) break;

                var msg = MessageCodec.Decode(line);
                if (msg?.Type == MessageTypes.Ping)
                {
                    var pong = new NetworkMessage { Type = MessageTypes.Pong, RequestId = msg.RequestId };
                    await writer.WriteLineAsync(MessageCodec.Encode(pong).TrimEnd());
                    break;
                }
            }
        });

        using var mainForm = new MainForm();
        await mainForm.ConnectionService.ConnectAsync("127.0.0.1", port, "TestPlayer");

        long reportedLatency = -1;
        var latencyTcs = new TaskCompletionSource<long>();

        mainForm.ConnectionService.PingUpdated += (latency) =>
        {
            reportedLatency = latency;
            latencyTcs.TrySetResult(latency);
        };

        bool pingSent = await mainForm.ConnectionService.SendPingAsync();
        Assert.True(pingSent);

        long receivedLatency = await latencyTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(receivedLatency >= 0);
        Assert.Equal(reportedLatency, receivedLatency);
    }
}
