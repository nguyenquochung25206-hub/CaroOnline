using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Caro.Server.Handlers;
using Caro.Server.Networking;
using Caro.Server.Services;
using Caro.Shared.Messages;
using Caro.Shared.Protocol;
using Xunit;

namespace Caro.TV6.Tests;

/// <summary>
/// Kiểm thử luồng message đi qua MessageRouter bằng TCP loopback.
/// Không khởi chạy WinForms và không sửa các lớp nghiệp vụ của thành viên khác.
/// </summary>
public sealed class MessageRouterTests
{
    [Fact]
    public async Task CreateRoom_ReturnsRoomCreatedWithSixCharacterRoomId()
    {
        await using var env = await TestEnvironment.CreateAsync(1);

        await env.Router.HandleAsync(env.Sessions[0], Request(
            MessageTypes.CreateRoom, new { RoomName = "Phong test", BoardSize = 15 }));

        NetworkMessage response = await env.ReadAsync(0);
        Assert.Equal(MessageTypes.RoomCreated, response.Type);
        string roomId = PayloadString(response, "RoomId");
        Assert.Equal(6, roomId.Length);
        Assert.Equal(roomId, roomId.ToLowerInvariant());
        Assert.Equal(roomId, env.Sessions[0].RoomId);
    }

    [Fact]
    public async Task JoinRoom_ValidRoom_NotifiesBothPlayers()
    {
        await using var env = await TestEnvironment.CreateAsync(2);
        string roomId = await env.CreateRoomAsync(0);

        await env.Router.HandleAsync(env.Sessions[1], Request(
            MessageTypes.JoinRoom, new { RoomId = roomId }));

        NetworkMessage guestResponse = await env.ReadAsync(1);
        NetworkMessage hostNotification = await env.ReadAsync(0);

        Assert.Equal(MessageTypes.RoomJoined, guestResponse.Type);
        Assert.Equal(MessageTypes.RoomJoined, hostNotification.Type);
        Assert.Equal(roomId, env.Sessions[1].RoomId);
        Assert.Equal(roomId, PayloadString(guestResponse, "RoomId"));
    }

    [Fact]
    public async Task JoinRoom_InvalidRoom_ReturnsJoinRoomFailed()
    {
        await using var env = await TestEnvironment.CreateAsync(1);

        await env.Router.HandleAsync(env.Sessions[0], Request(
            MessageTypes.JoinRoom, new { RoomId = "not-found" }));

        NetworkMessage response = await env.ReadAsync(0);
        Assert.Equal(MessageTypes.JoinRoomFailed, response.Type);
    }

    [Fact]
    public async Task StartGame_WithoutSecondPlayer_ReturnsError()
    {
        await using var env = await TestEnvironment.CreateAsync(1);
        await env.CreateRoomAsync(0);

        await env.Router.HandleAsync(env.Sessions[0], Request(MessageTypes.StartGame, new { }));

        NetworkMessage response = await env.ReadAsync(0);
        Assert.Equal(MessageTypes.Error, response.Type);
    }

    [Fact]
    public async Task StartGame_OnlyHostCanStart()
    {
        await using var env = await TestEnvironment.CreateAsync(2);
        string roomId = await env.CreateRoomAsync(0);
        await env.JoinRoomAsync(1, roomId);

        await env.Router.HandleAsync(env.Sessions[1], Request(MessageTypes.StartGame, new { }));

        NetworkMessage response = await env.ReadAsync(1);
        Assert.Equal(MessageTypes.Error, response.Type);
    }

    [Fact]
    public async Task StartGame_WithTwoPlayers_NotifiesBothPlayers()
    {
        await using var env = await TestEnvironment.CreateAsync(2);
        string roomId = await env.CreateRoomAsync(0);
        await env.JoinRoomAsync(1, roomId);

        await env.Router.HandleAsync(env.Sessions[0], Request(MessageTypes.StartGame, new { }));

        NetworkMessage hostResponse = await env.ReadAsync(0);
        NetworkMessage guestNotification = await env.ReadAsync(1);
        Assert.Equal(MessageTypes.StartGame, hostResponse.Type);
        Assert.Equal(MessageTypes.StartGame, guestNotification.Type);
        Assert.Equal(roomId, PayloadString(hostResponse, "RoomId"));
    }

    [Fact]
    public async Task Move_BeforeGameStarted_ReturnsMoveRejected()
    {
        await using var env = await TestEnvironment.CreateAsync(2);
        string roomId = await env.CreateRoomAsync(0);
        await env.JoinRoomAsync(1, roomId);

        await env.Router.HandleAsync(env.Sessions[0], Request(
            MessageTypes.Move, new { RoomId = roomId, Row = 7, Col = 7 }));

        NetworkMessage response = await env.ReadAsync(0);
        Assert.Equal(MessageTypes.MoveRejected, response.Type);
    }

    [Fact]
    public async Task Move_AfterStart_BroadcastsBoardUpdatedToBothPlayers()
    {
        await using var env = await TestEnvironment.CreateAsync(2);
        string roomId = await env.CreateRoomAsync(0);
        await env.JoinRoomAsync(1, roomId);
        await env.StartGameAsync(0);

        await env.Router.HandleAsync(env.Sessions[0], Request(
            MessageTypes.Move, new { RoomId = roomId, Row = 7, Col = 7 }));

        NetworkMessage hostAccepted = await env.ReadAsync(0);
        NetworkMessage hostBoard = await env.ReadAsync(0);
        NetworkMessage guestBoard = await env.ReadAsync(1);

        Assert.Equal(MessageTypes.MoveAccepted, hostAccepted.Type);
        Assert.Equal(MessageTypes.BoardUpdated, hostBoard.Type);
        Assert.Equal(MessageTypes.BoardUpdated, guestBoard.Type);
        Assert.Equal("X", PayloadString(hostBoard, "Symbol"));
        Assert.Equal(7, PayloadInt(hostBoard, "Row"));
        Assert.Equal(7, PayloadInt(hostBoard, "Col"));
    }

    [Fact]
    public async Task Chat_InSameRoom_IsDeliveredToBothPlayers()
    {
        await using var env = await TestEnvironment.CreateAsync(2);
        string roomId = await env.CreateRoomAsync(0);
        await env.JoinRoomAsync(1, roomId);

        await env.Router.HandleAsync(env.Sessions[0], Request(
            MessageTypes.Chat, new { Message = "Xin chao" }));

        NetworkMessage hostMessage = await env.ReadAsync(0);
        NetworkMessage guestMessage = await env.ReadAsync(1);

        Assert.Equal(MessageTypes.Chat, hostMessage.Type);
        Assert.Equal(MessageTypes.Chat, guestMessage.Type);
        Assert.Equal("Xin chao", PayloadString(guestMessage, "Content"));
        Assert.Equal(roomId, PayloadString(guestMessage, "RoomId"));
    }

    private static NetworkMessage Request(string type, object payload) => new()
    {
        Type = type,
        RequestId = Guid.NewGuid().ToString("N"),
        Payload = payload
    };

    private static string PayloadString(NetworkMessage message, string propertyName)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(message.Payload));
        return document.RootElement.GetProperty(propertyName).GetString() ?? string.Empty;
    }

    private static int PayloadInt(NetworkMessage message, string propertyName)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(message.Payload));
        return document.RootElement.GetProperty(propertyName).GetInt32();
    }

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly List<TcpClient> _clientSockets;
        private readonly List<StreamReader> _readers;

        public ClientSession[] Sessions { get; }
        public MessageRouter Router { get; }

        private TestEnvironment(
            TcpListener listener,
            List<TcpClient> clientSockets,
            List<StreamReader> readers,
            ClientSession[] sessions,
            MessageRouter router)
        {
            _listener = listener;
            _clientSockets = clientSockets;
            _readers = readers;
            Sessions = sessions;
            Router = router;
        }

        public static async Task<TestEnvironment> CreateAsync(int clientCount)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var clientSockets = new List<TcpClient>();
            var readers = new List<StreamReader>();
            var sessions = new List<ClientSession>();

            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                for (int i = 0; i < clientCount; i++)
                {
                    var client = new TcpClient();
                    Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
                    await client.ConnectAsync(IPAddress.Loopback, port);
                    TcpClient serverSocket = await acceptTask.WaitAsync(TimeSpan.FromSeconds(3));

                    clientSockets.Add(client);
                    readers.Add(new StreamReader(client.GetStream()));
                    sessions.Add(new ClientSession(serverSocket, _ => Task.CompletedTask));
                }

                var manager = new ClientManager();
                foreach (ClientSession session in sessions)
                    manager.Add(session);

                var rooms = new RoomService();
                var chat = new ChatHandler(rooms, manager);
                var router = new MessageRouter(manager, rooms, chat);
                router.Attach();

                return new TestEnvironment(listener, clientSockets, readers, sessions.ToArray(), router);
            }
            catch
            {
                foreach (ClientSession session in sessions)
                    session.Close();
                foreach (TcpClient client in clientSockets)
                    client.Dispose();
                foreach (StreamReader reader in readers)
                    reader.Dispose();
                listener.Stop();
                throw;
            }
        }

        public async Task<NetworkMessage> ReadAsync(int clientIndex)
        {
            string? line = await _readers[clientIndex].ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(string.IsNullOrWhiteSpace(line), "Server không gửi phản hồi.");
            NetworkMessage? message = MessageCodec.Decode(line!);
            Assert.NotNull(message);
            return message!;
        }

        public async Task<string> CreateRoomAsync(int index)
        {
            await Router.HandleAsync(Sessions[index], Request(
                MessageTypes.CreateRoom, new { RoomName = "Test room", BoardSize = 15 }));
            NetworkMessage response = await ReadAsync(index);
            Assert.Equal(MessageTypes.RoomCreated, response.Type);
            return PayloadString(response, "RoomId");
        }

        public async Task JoinRoomAsync(int index, string roomId)
        {
            await Router.HandleAsync(Sessions[index], Request(
                MessageTypes.JoinRoom, new { RoomId = roomId }));
            Assert.Equal(MessageTypes.RoomJoined, (await ReadAsync(index)).Type);
            // Chủ phòng cũng nhận thông báo ROOM_JOINED.
            int hostIndex = index == 0 ? 1 : 0;
            Assert.Equal(MessageTypes.RoomJoined, (await ReadAsync(hostIndex)).Type);
        }

        public async Task StartGameAsync(int hostIndex)
        {
            await Router.HandleAsync(Sessions[hostIndex], Request(MessageTypes.StartGame, new { }));
            Assert.Equal(MessageTypes.StartGame, (await ReadAsync(hostIndex)).Type);
            int guestIndex = hostIndex == 0 ? 1 : 0;
            Assert.Equal(MessageTypes.StartGame, (await ReadAsync(guestIndex)).Type);
        }

        public ValueTask DisposeAsync()
        {
            foreach (ClientSession session in Sessions)
                session.Close();
            foreach (StreamReader reader in _readers)
                reader.Dispose();
            foreach (TcpClient client in _clientSockets)
                client.Dispose();
            _listener.Stop();
            return ValueTask.CompletedTask;
        }
    }
}
