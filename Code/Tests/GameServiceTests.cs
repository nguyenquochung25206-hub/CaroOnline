
using Xunit;
using Caro.Server.Services;
using CaroGame.Shared.Game;

namespace Caro.Tests;

public class GameServiceTests
{
    // TEST 1: X luôn được đi trước
    [Fact]
    public void NewGame_XPlaysFirst()
    {
        var game = new GameService("playerX", "playerO");

        Assert.Equal(Player.X, game.CurrentPlayer);
        Assert.Equal("playerX", game.CurrentPlayerId);
        Assert.False(game.IsGameOver);
    }

    // TEST 2: X đánh xong thì chuyển sang O
    [Fact]
    public void ValidMove_ChangesTurn()
    {
        var game = new GameService("playerX", "playerO");

        bool success = game.MakeMove(
            "playerX", 0, 0, out string error);

        Assert.True(success, error);
        Assert.Equal(Player.O, game.CurrentPlayer);
        Assert.Equal(Player.X, game.Board.GetCell(0, 0));
    }

    // TEST 3: Không được đánh hai lượt liên tiếp
    [Fact]
    public void SamePlayer_CannotMoveTwice()
    {
        var game = new GameService("playerX", "playerO");

        Assert.True(game.MakeMove(
            "playerX", 0, 0, out _));

        bool success = game.MakeMove(
            "playerX", 0, 1, out _);

        Assert.False(success);
        Assert.Equal(Player.O, game.CurrentPlayer);
    }

    // TEST 4: Không được đánh vào ô đã có quân
    [Fact]
    public void OccupiedCell_IsRejected()
    {
        var game = new GameService("playerX", "playerO");

        Assert.True(game.MakeMove(
            "playerX", 0, 0, out _));

        bool success = game.MakeMove(
            "playerO", 0, 0, out _);

        Assert.False(success);
        Assert.Equal(Player.O, game.CurrentPlayer);
    }

    // TEST 5: Không được đánh ngoài bàn cờ
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(15, 0)]
    [InlineData(0, 15)]
    public void OutOfBounds_IsRejected(int row, int col)
    {
        var game = new GameService("playerX", "playerO");

        bool success = game.MakeMove(
            "playerX", row, col, out _);

        Assert.False(success);
        Assert.Equal(Player.X, game.CurrentPlayer);
    }

    // TEST 6: Người ngoài trận không được đánh
    [Fact]
    public void UnknownPlayer_IsRejected()
    {
        var game = new GameService("playerX", "playerO");

        bool success = game.MakeMove(
            "stranger", 0, 0, out _);

        Assert.False(success);
        Assert.Equal(Player.X, game.CurrentPlayer);
    }

    // TEST 7: X thắng khi có 5 quân liên tiếp
    [Fact]
    public void FiveInRow_XWins()
    {
        var game = new GameService("playerX", "playerO");

        for (int col = 0; col < 4; col++)
        {
            Assert.True(game.MakeMove(
                "playerX", 0, col, out _));

            Assert.True(game.MakeMove(
                "playerO", 1, col, out _));
        }

        bool success = game.MakeMove(
            "playerX", 0, 4, out _);

        Assert.True(success);
        Assert.True(game.IsGameOver);
        Assert.Equal(GameResult.XWins, game.Result);
    }

    // TEST 8: Không được đánh sau khi trận kết thúc
    [Fact]
    public void GameOver_RejectsFurtherMoves()
    {
        var game = new GameService("playerX", "playerO");

        for (int col = 0; col < 4; col++)
        {
            game.MakeMove("playerX", 0, col, out _);
            game.MakeMove("playerO", 1, col, out _);
        }

        game.MakeMove("playerX", 0, 4, out _);

        bool success = game.MakeMove(
            "playerO", 2, 2, out _);

        Assert.False(success);
        Assert.Equal(Player.None,
            game.Board.GetCell(2, 2));
    }

    // TEST 9: Không cho hai người chơi trùng ID
    [Fact]
    public void DuplicatePlayerIds_ThrowsException()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new GameService("same", "same"));
    }

    // TEST 10: Lấy đúng ID của X và O
    [Fact]
    public void GetPlayerId_ReturnsCorrectId()
    {
        var game = new GameService("playerX", "playerO");

        Assert.Equal("playerX",
            game.GetPlayerId(Player.X));

        Assert.Equal("playerO",
            game.GetPlayerId(Player.O));

        Assert.Null(game.GetPlayerId(Player.None));
    }
}
