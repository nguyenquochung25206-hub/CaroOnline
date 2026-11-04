// TODO TV4 + TV5 + TV6: Kiểm thử lượt đi, ô đã đánh, ngoài bàn cờ, thắng và hòa.

using System;
using Xunit;
using CaroGame.Shared.Game;

namespace Caro.Tests
{
    public class GameFlowTests
    {
        // Test 1: Bàn cờ mặc định có kích thước 15x15
        [Fact]
        public void Board_Default_Size_Is_15x15()
        {
            var board = new BoardState();

            Assert.Equal(15, board.Rows);
            Assert.Equal(15, board.Cols);
        }

        // Test 2: Đánh vào ô trống thành công
        [Fact]
        public void Place_Move_On_Empty_Cell_Success()
        {
            var board = new BoardState();
            var move = new Move(0, 0, Player.X);

            bool result = board.TryPlaceMove(move);

            Assert.True(result);
            Assert.Equal(Player.X, board.GetCell(0, 0));
        }

        // Test 3: Không được đánh vào ô đã có quân
        [Fact]
        public void Place_Move_On_Occupied_Cell_Fails()
        {
            var board = new BoardState();

            board.TryPlaceMove(new Move(0, 0, Player.X));
            bool result = board.TryPlaceMove(
                new Move(0, 0, Player.O));

            Assert.False(result);
            Assert.Equal(Player.X, board.GetCell(0, 0));
        }

        // Test 4: Không được đánh ngoài bàn cờ
        [Fact]
        public void Place_Move_Outside_Board_Fails()
        {
            var board = new BoardState();

            bool result = board.TryPlaceMove(
                new Move(20, 20, Player.X));

            Assert.False(result);
        }

        // Test 5: Reset bàn cờ về trạng thái trống
        [Fact]
        public void Reset_Board_Clears_All_Moves()
        {
            var board = new BoardState();

            board.TryPlaceMove(new Move(0, 0, Player.X));
            board.TryPlaceMove(new Move(1, 1, Player.O));

            board.Reset();

            Assert.Equal(Player.None, board.GetCell(0, 0));
            Assert.Equal(Player.None, board.GetCell(1, 1));
        }
    }
}
