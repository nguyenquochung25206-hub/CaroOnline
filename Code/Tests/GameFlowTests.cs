// TODO TV4 + TV5 + TV6: Kiểm thử lượt đi, ô đã đánh, ngoài bàn cờ, thắng và hòa.
using System;
using Xunit;
using CaroGame.Shared.Game;

public class GameFlowTests
{
    // TEST 1: Bàn cờ mặc định 15x15
    [Fact]
    public void Board_Default_Size()
    {
        var board = new BoardState();

        Assert.Equal(15, board.Rows);
        Assert.Equal(15, board.Cols);
    }

    // TEST 2: Đánh vào ô trống thành công
    [Fact]
    public void Place_Move_Success()
    {
        var board = new BoardState();
        var move = new Move(0, 0, Player.X);

        Assert.True(board.TryPlaceMove(move));
        Assert.Equal(Player.X, board.GetCell(0, 0));
    }

    // TEST 3: Không được đánh vào ô đã có quân
    [Fact]
    public void Place_Occupied_Cell_Fails()
    {
        var board = new BoardState();

        board.TryPlaceMove(new Move(0, 0, Player.X));

        Assert.False(board.TryPlaceMove(
            new Move(0, 0, Player.O)));
    }

    // TEST 4: Không được đánh ngoài bàn cờ
    [Fact]
    public void Place_Outside_Board_Fails()
    {
        var board = new BoardState();

        Assert.False(board.TryPlaceMove(
            new Move(20, 20, Player.X)));
    }

    // TEST 5: Reset bàn cờ
    [Fact]
    public void Reset_Board_Clears_Moves()
    {
        var board = new BoardState();

        board.TryPlaceMove(new Move(0, 0, Player.X));
        board.Reset();

        Assert.Equal(Player.None, board.GetCell(0, 0));
    }

    // TEST 6: X thắng theo hàng ngang
    [Fact]
    public void X_Wins_Horizontal()
    {
        var board = new BoardState();
        Move lastMove = null;

        for (int col = 0; col < 5; col++)
        {
            lastMove = new Move(0, col, Player.X);
            board.TryPlaceMove(lastMove);
        }

        Assert.Equal(GameResult.XWins,
            WinChecker.Check(board, lastMove));
    }

    // TEST 7: O thắng theo hàng dọc
    [Fact]
    public void O_Wins_Vertical()
    {
        var board = new BoardState();
        Move lastMove = null;

        for (int row = 0; row < 5; row++)
        {
            lastMove = new Move(row, 0, Player.O);
            board.TryPlaceMove(lastMove);
        }

        Assert.Equal(GameResult.OWins,
            WinChecker.Check(board, lastMove));
    }

    // TEST 8: X thắng theo đường chéo
    [Fact]
    public void X_Wins_Diagonal()
    {
        var board = new BoardState();
        Move lastMove = null;

        for (int i = 0; i < 5; i++)
        {
            lastMove = new Move(i, i, Player.X);
            board.TryPlaceMove(lastMove);
        }

        Assert.Equal(GameResult.XWins,
            WinChecker.Check(board, lastMove));
    }

    // TEST 9: Chưa đủ 5 quân thì chưa thắng
    [Fact]
    public void Less_Than_Five_No_Win()
    {
        var board = new BoardState();
        Move lastMove = null;

        for (int col = 0; col < 4; col++)
        {
            lastMove = new Move(0, col, Player.X);
            board.TryPlaceMove(lastMove);
        }

        Assert.Equal(GameResult.Ongoing,
            WinChecker.Check(board, lastMove));
    }

    // TEST 10: Bàn cờ đầy và không có 5 quân
    // liên tiếp thì hòa
    [Fact]
    public void Full_Board_Draw()
    {
        var board = new BoardState(3, 3);
        Move lastMove = null;

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                Player player = (row + col) % 2 == 0
                    ? Player.X : Player.O;

                lastMove = new Move(row, col, player);
                board.TryPlaceMove(lastMove);
            }
        }

        Assert.True(board.IsFull());
        Assert.Equal(GameResult.Draw,
            WinChecker.Check(board, lastMove));
    }
}
