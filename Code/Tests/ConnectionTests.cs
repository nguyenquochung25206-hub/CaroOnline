using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace Caro.Tests;

public class ConnectionTests
{
    [Fact]
    public async Task Client_Connects_To_Local_Test_Server()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(TimeSpan.FromSeconds(5));
        using var accepted = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(client.Connected);
        Assert.True(accepted.Connected);
    }

    [Fact]
    public async Task Two_Clients_Can_Connect_To_Local_Test_Server()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var first = new TcpClient();
        using var second = new TcpClient();
        await Task.WhenAll(first.ConnectAsync(IPAddress.Loopback, port), second.ConnectAsync(IPAddress.Loopback, port))
            .WaitAsync(TimeSpan.FromSeconds(5));
        using var accepted1 = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var accepted2 = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(first.Connected && second.Connected);
    }

    [Fact]
    public async Task Connecting_To_Closed_Port_Fails()
    {
        int port;
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        using var client = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(async () =>
            await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Client_Can_Send_Data_And_Server_Can_Read_It()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(TimeSpan.FromSeconds(5));
        using var accepted = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
        byte[] sent = Encoding.UTF8.GetBytes("HELLO\n");
        await client.GetStream().WriteAsync(sent);
        byte[] buffer = new byte[64];
        int count = await accepted.GetStream().ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(count > 0);
        Assert.StartsWith("HELLO", Encoding.UTF8.GetString(buffer, 0, count));
    }
}
