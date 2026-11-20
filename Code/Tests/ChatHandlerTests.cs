
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;
using Caro.Server.Handlers;
using Caro.Server.Networking;
using Caro.Server.Services;

namespace Caro.TV6.Tests;

public class ChatHandlerTests
{
    // TEST 1: Khong cho phep ClientSession null
    [Fact]
    public async Task Chat_NullSession_ThrowsArgumentNullException()
    {
        var handler = new ChatHandler(null!, null!);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(null!, "Xin chao"));
    }

    // TEST 2: Nguoi choi chua vao phong khong duoc gui CHAT
    [Fact]
    public async Task Chat_NotInRoom_ReturnsError()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var client = new TcpClient();
            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

            await client.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverClient = await acceptTask;

            var session = new ClientSession(
                serverClient,
                _ => Task.CompletedTask);

            try
            {
                var roomService = new RoomService();
                var clientManager = new ClientManager();
                var handler = new ChatHandler(
                    roomService, clientManager);

                await handler.HandleAsync(session, "Xin chao");

                using var reader = new System.IO.StreamReader(
                    client.GetStream());

                string? response = await reader.ReadLineAsync()
                    .WaitAsync(TimeSpan.FromSeconds(3));

                Assert.NotNull(response);
                Assert.Contains("ERROR", response,
                    StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                session.Close();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    // TEST 3: Tu choi tin nhan CHAT rong
    [Fact]
    public async Task Chat_EmptyMessage_ReturnsError()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var client = new TcpClient();
            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

            await client.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverClient = await acceptTask;

            var session = new ClientSession(
                serverClient,
                _ => Task.CompletedTask);

            try
            {
                var handler = new ChatHandler(
                    new RoomService(),
                    new ClientManager());

                await handler.HandleAsync(session, "");

                using var reader = new System.IO.StreamReader(
                    client.GetStream());

                string? response = await reader.ReadLineAsync()
                    .WaitAsync(TimeSpan.FromSeconds(3));

                Assert.NotNull(response);
                Assert.Contains("ERROR", response,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Tin nhan khong duoc de trong", response);
            }
            finally
            {
                session.Close();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    // TEST 4: Tu choi tin nhan vuot qua 500 ky tu
    [Fact]
    public async Task Chat_TooLongMessage_ReturnsError()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var client = new TcpClient();
            Task<TcpClient> acceptTask =
                listener.AcceptTcpClientAsync();

            await client.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverClient = await acceptTask;

            var session = new ClientSession(
                serverClient,
                _ => Task.CompletedTask);

            try
            {
                var handler = new ChatHandler(
                    new RoomService(),
                    new ClientManager());

                string content = new string('A', 501);

                await handler.HandleAsync(session, content);

                using var reader = new System.IO.StreamReader(
                    client.GetStream());

                string? response = await reader.ReadLineAsync()
                    .WaitAsync(TimeSpan.FromSeconds(3));

                Assert.NotNull(response);
                Assert.Contains("ERROR", response,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains("500 ky tu", response);
            }
            finally
            {
                session.Close();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    // TEST 5: Hai nguoi choi cung phong nhan duoc CHAT
    [Fact]
    public async Task Chat_SameRoom_BroadcastsMessage()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        ClientSession? sessionA = null;
        ClientSession? sessionB = null;

        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var clientA = new TcpClient();
            Task<TcpClient> acceptA = listener.AcceptTcpClientAsync();
            await clientA.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverA = await acceptA;

            using var clientB = new TcpClient();
            Task<TcpClient> acceptB = listener.AcceptTcpClientAsync();
            await clientB.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverB = await acceptB;

            sessionA = new ClientSession(
                serverA, _ => Task.CompletedTask);
            sessionB = new ClientSession(
                serverB, _ => Task.CompletedTask);

            var roomService = new RoomService();
            var clientManager = new ClientManager();

            clientManager.Add(sessionA);
            clientManager.Add(sessionB);

            // Tao phong va cho nguoi choi B tham gia
            Assert.True(roomService.CreateRoom(
                sessionA.Id.ToString(), out string roomId));

            Assert.True(roomService.JoinRoom(
                roomId, sessionB.Id.ToString()));

            sessionA.RoomId = roomId;
            sessionB.RoomId = roomId;

            var handler = new ChatHandler(
                roomService, clientManager);

            // Nguoi choi A gui tin nhan
            await handler.HandleAsync(
                sessionA, "Xin chao nguoi choi B");

            // Nguoi choi B nhan tin nhan qua TCP
            using var reader = new System.IO.StreamReader(
                clientB.GetStream());

            string? response = await reader.ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(3));

            Assert.NotNull(response);
            Assert.Contains("CHAT", response,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "Xin chao nguoi choi B", response);
        }
        finally
        {
            sessionA?.Close();
            sessionB?.Close();
            listener.Stop();
        }
    }

    // TEST 6: Nguoi choi khac phong khong nhan duoc CHAT
    [Fact]
    public async Task Chat_DifferentRoom_DoesNotReceiveMessage()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        ClientSession? sessionA = null;
        ClientSession? sessionB = null;

        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var clientA = new TcpClient();
            Task<TcpClient> acceptA = listener.AcceptTcpClientAsync();
            await clientA.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverA = await acceptA;

            using var clientB = new TcpClient();
            Task<TcpClient> acceptB = listener.AcceptTcpClientAsync();
            await clientB.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverB = await acceptB;

            sessionA = new ClientSession(
                serverA, _ => Task.CompletedTask);
            sessionB = new ClientSession(
                serverB, _ => Task.CompletedTask);

            var roomService = new RoomService();
            var clientManager = new ClientManager();

            clientManager.Add(sessionA);
            clientManager.Add(sessionB);

            // Tao hai phong rieng biet
            Assert.True(roomService.CreateRoom(
                sessionA.Id.ToString(), out string roomA));

            Assert.True(roomService.CreateRoom(
                sessionB.Id.ToString(), out string roomB));

            Assert.NotEqual(roomA, roomB);

            sessionA.RoomId = roomA;
            sessionB.RoomId = roomB;

            var handler = new ChatHandler(
                roomService, clientManager);

            // A gui tin nhan trong phong A
            await handler.HandleAsync(
                sessionA, "Tin nhan chi danh cho phong A");

            // B khong duoc nhan du lieu
            using var reader = new System.IO.StreamReader(
                clientB.GetStream());

            Task<string?> readTask = reader.ReadLineAsync();

            await Assert.ThrowsAsync<TimeoutException>(
                async () => await readTask.WaitAsync(
                    TimeSpan.FromMilliseconds(500)));
        }
        finally
        {
            sessionA?.Close();
            sessionB?.Close();
            listener.Stop();
        }
    }

    // TEST 7: CHAT giu dung ten nguoi gui va noi dung sau Trim
    [Fact]
    public async Task Chat_ValidMessage_PreservesSenderAndTrimmedContent()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        ClientSession? sessionA = null;
        ClientSession? sessionB = null;

        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var clientA = new TcpClient();
            Task<TcpClient> acceptA = listener.AcceptTcpClientAsync();
            await clientA.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverA = await acceptA;

            using var clientB = new TcpClient();
            Task<TcpClient> acceptB = listener.AcceptTcpClientAsync();
            await clientB.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverB = await acceptB;

            sessionA = new ClientSession(
                serverA, _ => Task.CompletedTask);
            sessionB = new ClientSession(
                serverB, _ => Task.CompletedTask);

            var roomService = new RoomService();
            var clientManager = new ClientManager();

            clientManager.Add(sessionA);
            clientManager.Add(sessionB);

            Assert.True(roomService.CreateRoom(
                sessionA.Id.ToString(), out string roomId));

            Assert.True(roomService.JoinRoom(
                roomId, sessionB.Id.ToString()));

            sessionA.RoomId = roomId;
            sessionB.RoomId = roomId;

            var handler = new ChatHandler(
                roomService, clientManager);

            await handler.HandleAsync(
                sessionA, "  Xin chao B  ");

            using var reader = new System.IO.StreamReader(
                clientB.GetStream());

            string? response = await reader.ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(3));

            Assert.NotNull(response);

            using var document =
                System.Text.Json.JsonDocument.Parse(response!);

            var payload = document.RootElement.GetProperty("Payload");

            Assert.Equal(
                sessionA.PlayerName,
                payload.GetProperty("Sender").GetString());

            Assert.Equal(
                "Xin chao B",
                payload.GetProperty("Content").GetString());
        }
        finally
        {
            sessionA?.Close();
            sessionB?.Close();
            listener.Stop();
        }
    }

    // TEST 8: Ma phong cua Client khong khop voi RoomService
    [Fact]
    public async Task Chat_WrongRoomId_ReturnsError()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var client = new TcpClient();
            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

            await client.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverClient = await acceptTask;

            var session = new ClientSession(
                serverClient, _ => Task.CompletedTask);

            try
            {
                var roomService = new RoomService();
                var clientManager = new ClientManager();

                Assert.True(roomService.CreateRoom(
                    session.Id.ToString(), out string roomId));

                // Gia lap Client dang giu ma phong sai
                session.RoomId = roomId + "_SAI";

                var handler = new ChatHandler(
                    roomService, clientManager);

                await handler.HandleAsync(session, "Xin chao");

                using var reader = new System.IO.StreamReader(
                    client.GetStream());

                string? response = await reader.ReadLineAsync()
                    .WaitAsync(TimeSpan.FromSeconds(3));

                Assert.NotNull(response);
                Assert.Contains("ERROR", response,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains(
                    "Ban chua tham gia phong choi", response);
            }
            finally
            {
                session.Close();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    // TEST 9: Tu choi tin nhan co ky tu dieu khien
    [Fact]
    public async Task Chat_ControlCharacter_ReturnsError()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var client = new TcpClient();
            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

            await client.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverClient = await acceptTask;

            var session = new ClientSession(
                serverClient, _ => Task.CompletedTask);

            try
            {
                var handler = new ChatHandler(
                    new RoomService(),
                    new ClientManager());

                await handler.HandleAsync(
                    session, "Xin chao\nNguoi choi B");

                using var reader = new System.IO.StreamReader(
                    client.GetStream());

                string? response = await reader.ReadLineAsync()
                    .WaitAsync(TimeSpan.FromSeconds(3));

                Assert.NotNull(response);
                Assert.Contains("ERROR", response,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains(
                    "ky tu khong hop le", response);
            }
            finally
            {
                session.Close();
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    // TEST 10: Tin nhan dung 500 ky tu duoc gui thanh cong
    [Fact]
    public async Task Chat_Exactly500Characters_BroadcastsSuccessfully()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        ClientSession? sessionA = null;
        ClientSession? sessionB = null;

        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            using var clientA = new TcpClient();
            Task<TcpClient> acceptA = listener.AcceptTcpClientAsync();

            await clientA.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverA = await acceptA;

            using var clientB = new TcpClient();
            Task<TcpClient> acceptB = listener.AcceptTcpClientAsync();

            await clientB.ConnectAsync(IPAddress.Loopback, port);
            using TcpClient serverB = await acceptB;

            sessionA = new ClientSession(
                serverA, _ => Task.CompletedTask);
            sessionB = new ClientSession(
                serverB, _ => Task.CompletedTask);

            var roomService = new RoomService();
            var clientManager = new ClientManager();

            clientManager.Add(sessionA);
            clientManager.Add(sessionB);

            Assert.True(roomService.CreateRoom(
                sessionA.Id.ToString(), out string roomId));

            Assert.True(roomService.JoinRoom(
                roomId, sessionB.Id.ToString()));

            sessionA.RoomId = roomId;
            sessionB.RoomId = roomId;

            var handler = new ChatHandler(
                roomService, clientManager);

            string content = new string('A', 500);

            await handler.HandleAsync(sessionA, content);

            using var reader = new System.IO.StreamReader(
                clientB.GetStream());

            string? response = await reader.ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(3));

            Assert.NotNull(response);
            Assert.Contains("CHAT", response,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(content, response);
        }
        finally
        {
            sessionA?.Close();
            sessionB?.Close();
            listener.Stop();
        }
    }

}
