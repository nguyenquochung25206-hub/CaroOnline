
using System;
using System.Linq;
using Caro.Server.Services;
using CaroGame.Shared.Game;
using Xunit;

namespace Caro.Tests;

public class RoomServiceTests
{
    // TEST 1: Tao phong thanh cong
    [Fact]
    public void CreateRoom_Success()
    {
        var service = new RoomService();

        bool success = service.CreateRoom("player1", out string roomId);

        Assert.True(success);
        Assert.False(string.IsNullOrWhiteSpace(roomId));
        Assert.Equal(roomId, service.GetRoomId("player1"));
        Assert.Contains("player1", service.GetPlayers(roomId));
    }

    // TEST 2: Khong cho tao phong voi ID rong
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateRoom_InvalidPlayerId_ReturnsFalse(string? playerId)
    {
        var service = new RoomService();

        bool success = service.CreateRoom(playerId!, out string roomId);

        Assert.False(success);
        Assert.Equal(string.Empty, roomId);
    }

    // TEST 3: Mot nguoi khong duoc tao hai phong
    [Fact]
    public void CreateRoom_DuplicatePlayer_ReturnsFalse()
    {
        var service = new RoomService();

        Assert.True(service.CreateRoom("player1", out _));

        bool success = service.CreateRoom("player1", out _);

        Assert.False(success);
    }

    // TEST 4: Nguoi thu hai tham gia phong thanh cong
    [Fact]
    public void JoinRoom_SecondPlayer_Success()
    {
        var service = new RoomService();

        service.CreateRoom("player1", out string roomId);

        bool success = service.JoinRoom(roomId, "player2");

        Assert.True(success);
        Assert.Equal(2, service.GetPlayers(roomId).Count);
        Assert.True(service.IsRoomFull(roomId));
    }

    // TEST 5: Phong chi cho toi da hai nguoi
    [Fact]
    public void JoinRoom_ThirdPlayer_ReturnsFalse()
    {
        var service = new RoomService();

        service.CreateRoom("player1", out string roomId);
        service.JoinRoom(roomId, "player2");

        bool success = service.JoinRoom(roomId, "player3");

        Assert.False(success);
        Assert.Equal(2, service.GetPlayers(roomId).Count);
        Assert.Null(service.GetRoomId("player3"));
    }

    // TEST 6: Khong the tham gia phong khong ton tai
    [Fact]
    public void JoinRoom_NonexistentRoom_ReturnsFalse()
    {
        var service = new RoomService();

        Assert.False(service.JoinRoom("invalid", "player2"));
    }

    // TEST 7: Du hai nguoi thi game duoc tao
    [Fact]
    public void JoinRoom_TwoPlayers_StartsGame()
    {
        var service = new RoomService();

        service.CreateRoom("player1", out string roomId);
        service.JoinRoom(roomId, "player2");

        var game = service.GetGame(roomId);

        Assert.NotNull(game);
        Assert.Equal(Player.X, game.CurrentPlayer);
        Assert.Equal("player1", game.GetPlayerId(Player.X));
        Assert.Equal("player2", game.GetPlayerId(Player.O));
    }

    // TEST 8: Nguoi choi roi phong
    [Fact]
    public void LeaveRoom_RemovesPlayer()
    {
        var service = new RoomService();

        service.CreateRoom("player1", out string roomId);
        service.JoinRoom(roomId, "player2");

        bool success = service.LeaveRoom("player2");

        Assert.True(success);
        Assert.Null(service.GetRoomId("player2"));
        Assert.Single(service.GetPlayers(roomId));
        Assert.Null(service.GetGame(roomId));
    }

    // TEST 9: Xoa phong khi nguoi cuoi cung roi di
    [Fact]
    public void LeaveRoom_LastPlayer_DeletesRoom()
    {
        var service = new RoomService();

        service.CreateRoom("player1", out string roomId);

        Assert.True(service.LeaveRoom("player1"));

        Assert.Empty(service.GetPlayers(roomId));
        Assert.Null(service.GetGame(roomId));
        Assert.Null(service.GetRoomId("player1"));
    }

    // TEST 10: Ghep tran tu dong vao phong con cho
    [Fact]
    public void MatchPlayer_JoinsAvailableRoom()
    {
        var service = new RoomService();

        Assert.True(service.MatchPlayer("player1", out string room1));
        Assert.True(service.MatchPlayer("player2", out string room2));

        Assert.Equal(room1, room2);
        Assert.True(service.IsRoomFull(room1));
        Assert.NotNull(service.GetGame(room1));
    }

    // TEST 11: Phong day thi tao phong moi
    [Fact]
    public void MatchPlayer_FullRoom_CreatesNewRoom()
    {
        var service = new RoomService();

        service.MatchPlayer("player1", out string room1);
        service.MatchPlayer("player2", out _);
        service.MatchPlayer("player3", out string room3);

        Assert.NotEqual(room1, room3);
        Assert.Single(service.GetPlayers(room3));
    }

    // TEST 12: Mot nguoi khong duoc vao hai phong
    [Fact]
    public void JoinRoom_PlayerAlreadyInRoom_ReturnsFalse()
    {
        var service = new RoomService();

        service.CreateRoom("player1", out string room1);
        service.CreateRoom("player2", out string room2);

        Assert.False(service.JoinRoom(room2, "player1"));
        Assert.Equal(room1, service.GetRoomId("player1"));
    }

    // TEST 13: Nguoi khong o trong phong khong the roi
    [Fact]
    public void LeaveRoom_UnknownPlayer_ReturnsFalse()
    {
        var service = new RoomService();

        Assert.False(service.LeaveRoom("unknown"));
    }

    // TEST 14: Phong mot nguoi chua bat dau game
    [Fact]
    public void CreateRoom_OnePlayer_GameNotStarted()
    {
        var service = new RoomService();

        service.CreateRoom("player1", out string roomId);

        Assert.Null(service.GetGame(roomId));
        Assert.False(service.IsRoomFull(roomId));
    }
}
