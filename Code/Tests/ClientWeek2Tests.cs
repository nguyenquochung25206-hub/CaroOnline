using System.Net;
using System.Net.Sockets;
using System.Text;
using Caro.Client.Forms;
using Caro.Client.Services;
using Caro.Shared.Messages;
using Caro.Shared.Protocol;
using Xunit;

namespace Caro.Tests;

/// <summary>
/// Test Script Tuần 2: Kiểm thử tạo phòng, vào phòng, rời phòng, bắt đầu trận và đồng bộ message (TC_065 - TC_071).
/// </summary>
public class ClientWeek2Tests
{
    private static async Task<(TcpListener listener, int port, Task<TcpClient> clientTask)> StartMockServerAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var clientTask = listener.AcceptTcpClientAsync();
        return (listener, port, clientTask);
    }

    // TC_065: LobbyForm - Tạo phòng
    [Fact]
    public async Task TC_065_LobbyForm_CreateRoom()
    {
        var (listener, port, clientTask) = await StartMockServerAsync();
        using (listener)
        {
            using var connectionService = new ConnectionService();
            await connectionService.ConnectAsync("127.0.0.1", port, "HostPlayer");

            using var accepted = await clientTask.WaitAsync(TimeSpan.FromSeconds(5));
            using var lobbyForm = new LobbyForm(connectionService);

            RoomInfo? createdRoom = null;
            connectionService.RoomCreated += room => createdRoom = room;

            bool success = await connectionService.CreateRoomAsync("Caro Arena 1");

            Assert.True(success);
            Assert.True(connectionService.IsInRoom);
            Assert.NotNull(connectionService.CurrentRoom);
            Assert.Equal("Caro Arena 1", connectionService.CurrentRoom.RoomName);
            Assert.Equal("HostPlayer", connectionService.CurrentRoom.HostPlayer);
            Assert.True(connectionService.CurrentRoom.IsHost);
            Assert.Equal("X", connectionService.CurrentRoom.MySymbol);
            Assert.NotNull(createdRoom);
        }
    }

    // TC_066: LobbyForm - Tham gia phòng hợp lệ
    [Fact]
    public async Task TC_066_LobbyForm_JoinRoom_Valid()
    {
        var (listener, port, clientTask) = await StartMockServerAsync();
        using (listener)
        {
            using var connectionService = new ConnectionService();
            await connectionService.ConnectAsync("127.0.0.1", port, "GuestPlayer");

            using var accepted = await clientTask.WaitAsync(TimeSpan.FromSeconds(5));
            using var writer = new StreamWriter(accepted.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };

            var joinTask = connectionService.JoinRoomAsync("1234");

            var roomJoinedMsg = new NetworkMessage
            {
                Type = MessageTypes.RoomJoined,
                Payload = new
                {
                    RoomId = "1234",
                    HostName = "RoomHost",
                    GuestName = "GuestPlayer"
                }
            };
            await writer.WriteLineAsync(MessageCodec.Encode(roomJoinedMsg).TrimEnd());

            await joinTask;
            await Task.Delay(150);

            Assert.True(connectionService.IsInRoom);
            Assert.NotNull(connectionService.CurrentRoom);
            Assert.Equal("1234", connectionService.CurrentRoom.RoomId);
            Assert.Equal("RoomHost", connectionService.CurrentRoom.HostPlayer);
            Assert.Equal("GuestPlayer", connectionService.CurrentRoom.GuestPlayer);
            Assert.False(connectionService.CurrentRoom.IsHost);
            Assert.Equal("O", connectionService.CurrentRoom.MySymbol);
        }
    }

    // TC_067: LobbyForm - Tham gia phòng không tồn tại
    [Fact]
    public async Task TC_067_LobbyForm_JoinRoom_NotFound()
    {
        var (listener, port, clientTask) = await StartMockServerAsync();
        using (listener)
        {
            using var connectionService = new ConnectionService();
            await connectionService.ConnectAsync("127.0.0.1", port, "GuestPlayer");

            using var accepted = await clientTask.WaitAsync(TimeSpan.FromSeconds(5));
            using var writer = new StreamWriter(accepted.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };

            string? failureReason = null;
            var failTcs = new TaskCompletionSource<string>();
            connectionService.JoinRoomFailed += reason =>
            {
                failureReason = reason;
                failTcs.TrySetResult(reason);
            };

            await connectionService.JoinRoomAsync("9999");

            var failMsg = new NetworkMessage
            {
                Type = MessageTypes.JoinRoomFailed,
                Payload = new { Message = "Phòng 9999 không tồn tại hoặc đã đầy." }
            };
            await writer.WriteLineAsync(MessageCodec.Encode(failMsg).TrimEnd());

            string receivedReason = await failTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Contains("không tồn tại", receivedReason);
            Assert.False(connectionService.IsInRoom);
            Assert.Null(connectionService.CurrentRoom);
        }
    }

    // TC_068: LobbyForm - Phòng đã đủ 2 người
    [Fact]
    public async Task TC_068_LobbyForm_RoomFull_ReadyForGame()
    {
        var (listener, port, clientTask) = await StartMockServerAsync();
        using (listener)
        {
            using var connectionService = new ConnectionService();
            await connectionService.ConnectAsync("127.0.0.1", port, "HostUser");

            using var accepted = await clientTask.WaitAsync(TimeSpan.FromSeconds(5));
            using var writer = new StreamWriter(accepted.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };

            using var lobbyForm = new LobbyForm(connectionService);
            await connectionService.CreateRoomAsync("Battle Room");

            var joinedMsg = new NetworkMessage
            {
                Type = MessageTypes.RoomJoined,
                Payload = new
                {
                    RoomId = connectionService.CurrentRoom!.RoomId,
                    HostName = "HostUser",
                    GuestName = "Challenger"
                }
            };
            await writer.WriteLineAsync(MessageCodec.Encode(joinedMsg).TrimEnd());
            await Task.Delay(150);

            Assert.NotNull(connectionService.CurrentRoom.GuestPlayer);
            Assert.Equal("Challenger", connectionService.CurrentRoom.GuestPlayer);
            Assert.True(lobbyForm.IsStartGameButtonEnabled);
            Assert.Contains("sẵn sàng", lobbyForm.CurrentMatchStatus);
        }
    }

    // TC_069: LobbyForm - Rời phòng
    [Fact]
    public async Task TC_069_LobbyForm_LeaveRoom()
    {
        var (listener, port, clientTask) = await StartMockServerAsync();
        using (listener)
        {
            using var connectionService = new ConnectionService();
            await connectionService.ConnectAsync("127.0.0.1", port, "PlayerA");

            using var accepted = await clientTask.WaitAsync(TimeSpan.FromSeconds(5));

            await connectionService.CreateRoomAsync("Temporary Room");
            Assert.True(connectionService.IsInRoom);

            string? leftRoomId = null;
            connectionService.RoomLeft += id => leftRoomId = id;

            bool leaveSuccess = await connectionService.LeaveRoomAsync();

            Assert.True(leaveSuccess);
            Assert.False(connectionService.IsInRoom);
            Assert.Null(connectionService.CurrentRoom);
            Assert.NotNull(leftRoomId);
        }
    }

    // TC_070: LobbyForm / MainForm - Bắt đầu ván và mở bàn cờ
    [Fact]
    public async Task TC_070_LobbyForm_MainForm_StartGame()
    {
        var (listener, port, clientTask) = await StartMockServerAsync();
        using (listener)
        {
            using var connectionService = new ConnectionService();
            await connectionService.ConnectAsync("127.0.0.1", port, "HostUser");

            using var accepted = await clientTask.WaitAsync(TimeSpan.FromSeconds(5));
            using var writer = new StreamWriter(accepted.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };

            await connectionService.CreateRoomAsync("Game Room");

            string? startedRoomId = null;
            var startTcs = new TaskCompletionSource<string>();
            connectionService.GameStarted += roomId =>
            {
                startedRoomId = roomId;
                startTcs.TrySetResult(roomId);
            };

            await connectionService.StartGameAsync();

            string resRoomId = await startTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(connectionService.CurrentRoom!.IsGameStarted);
            Assert.Equal(connectionService.CurrentRoom.RoomId, resRoomId);
        }
    }

    // TC_071: ConnectionService - Cập nhật giao diện theo message Server
    // Kịch bản: Nhận lần lượt ROOM_CREATED, ROOM_JOINED, START_GAME
    [Fact]
    public async Task TC_071_ConnectionService_UpdateUIFromIncomingMessages_SequentialFlow()
    {
        var (listener, port, clientTask) = await StartMockServerAsync();
        using (listener)
        {
            using var connectionService = new ConnectionService();
            await connectionService.ConnectAsync("127.0.0.1", port, "HostUser");

            using var accepted = await clientTask.WaitAsync(TimeSpan.FromSeconds(5));
            using var writer = new StreamWriter(accepted.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };

            var receivedEvents = new List<string>();

            var roomCreatedTcs = new TaskCompletionSource<RoomInfo>();
            connectionService.RoomCreated += room =>
            {
                receivedEvents.Add(MessageTypes.RoomCreated);
                roomCreatedTcs.TrySetResult(room);
            };

            var roomJoinedTcs = new TaskCompletionSource<RoomInfo>();
            connectionService.RoomJoined += room =>
            {
                receivedEvents.Add(MessageTypes.RoomJoined);
                roomJoinedTcs.TrySetResult(room);
            };

            var gameStartedTcs = new TaskCompletionSource<string>();
            connectionService.GameStarted += roomId =>
            {
                receivedEvents.Add(MessageTypes.StartGame);
                gameStartedTcs.TrySetResult(roomId);
            };

            // 1. Nhận message ROOM_CREATED từ Server
            var roomCreatedMsg = new NetworkMessage
            {
                Type = MessageTypes.RoomCreated,
                Payload = new
                {
                    RoomId = "5555",
                    RoomName = "Phòng của HostUser",
                    HostName = "HostUser"
                }
            };
            await writer.WriteLineAsync(MessageCodec.Encode(roomCreatedMsg).TrimEnd());

            var createdRoom = await roomCreatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(createdRoom);
            Assert.Equal("5555", createdRoom.RoomId);
            Assert.Equal("HostUser", createdRoom.HostPlayer);
            Assert.True(createdRoom.IsHost);
            Assert.Equal("X", createdRoom.MySymbol);
            Assert.True(connectionService.IsInRoom);

            // 2. Nhận tiếp message ROOM_JOINED từ Server (Đối thủ vào phòng)
            var roomJoinedMsg = new NetworkMessage
            {
                Type = MessageTypes.RoomJoined,
                Payload = new
                {
                    RoomId = "5555",
                    HostName = "HostUser",
                    GuestName = "GuestUser"
                }
            };
            await writer.WriteLineAsync(MessageCodec.Encode(roomJoinedMsg).TrimEnd());

            var joinedRoom = await roomJoinedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(joinedRoom);
            Assert.Equal("GuestUser", joinedRoom.GuestPlayer);
            Assert.Equal("HostUser", joinedRoom.HostPlayer);

            // 3. Nhận tiếp message START_GAME từ Server (Bắt đầu ván đấu)
            var startMsg = new NetworkMessage
            {
                Type = MessageTypes.StartGame,
                Payload = new { RoomId = "5555" }
            };
            await writer.WriteLineAsync(MessageCodec.Encode(startMsg).TrimEnd());

            string startedRoomId = await gameStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("5555", startedRoomId);
            Assert.True(connectionService.CurrentRoom!.IsGameStarted);

            // Kiểm tra thứ tự nhận lần lượt đúng 100%
            Assert.Equal(3, receivedEvents.Count);
            Assert.Equal(MessageTypes.RoomCreated, receivedEvents[0]);
            Assert.Equal(MessageTypes.RoomJoined, receivedEvents[1]);
            Assert.Equal(MessageTypes.StartGame, receivedEvents[2]);
        }
    }
}

