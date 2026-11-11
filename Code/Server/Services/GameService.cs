// TODO TV5: Điều phối lượt đi, kiểm tra nước đi và kết thúc trận.

using System;

namespace Caro.Server.Services
{
    public sealed class GameService
    {
        private const int BoardSize = 15;
        private const int WinCount = 5;

        private char[,] board;
        private char currentTurn;
        private bool isGameOver;

        public GameService()
        {
            board = new char[BoardSize, BoardSize];
            currentTurn = 'X';
            isGameOver = false;
        }

        // Lấy lượt chơi hiện tại
        public char GetCurrentTurn()
        {
            return currentTurn;
        }

        // Kiểm tra trạng thái trận đấu
        public bool IsGameOver()
        {
            return isGameOver;
        }

        // Xử lý nước đi
        public bool MakeMove(int row, int col, char player,
                             out string message)
        {
            message = "";

            // Kiểm tra trận đấu đã kết thúc
            if (isGameOver)
            {
                message = "Trận đấu đã kết thúc.";
                return false;
            }

            // Kiểm tra người chơi hợp lệ
            if (player != 'X' && player != 'O')
            {
                message = "Người chơi không hợp lệ.";
                return false;
            }

            // Kiểm tra đúng lượt
            if (player != currentTurn)
            {
                message = "Chưa đến lượt của bạn.";
                return false;
            }

            // Kiểm tra tọa độ
            if (row < 0 || row >= BoardSize ||
                col < 0 || col >= BoardSize)
            {
                message = "Vị trí đánh không hợp lệ.";
                return false;
            }

            // Kiểm tra ô đã có quân cờ
            if (board[row, col] != '\0')
            {
                message = "Ô này đã có quân cờ.";
                return false;
            }

            // Cập nhật bàn cờ
            board[row, col] = player;

            // Kiểm tra thắng
            if (CheckWin(row, col, player))
            {
                isGameOver = true;
                message = "Người chơi " + player + " đã chiến thắng!";
                return true;
            }

            // Kiểm tra hòa
            if (CheckDraw())
            {
                isGameOver = true;
                message = "Trận đấu hòa!";
                return true;
            }

            // Chuyển lượt
            currentTurn = currentTurn == 'X' ? 'O' : 'X';

            message = "Nước đi hợp lệ.";
            return true;
        }

        // Kiểm tra chiến thắng
        private bool CheckWin(int row, int col, char player)
        {
            int[,] directions =
            {
                { 0, 1 },   // Ngang
                { 1, 0 },   // Dọc
                { 1, 1 },   // Chéo chính
                { 1, -1 }   // Chéo phụ
            };

            for (int i = 0; i < directions.GetLength(0); i++)
            {
                int count = 1;

                count += CountDirection(
                    row, col,
                    directions[i, 0],
                    directions[i, 1],
                    player);

                count += CountDirection(
                    row, col,
                    -directions[i, 0],
                    -directions[i, 1],
                    player);

                if (count >= WinCount)
                    return true;
            }

            return false;
        }

        // Đếm quân cờ liên tiếp
        private int CountDirection(
            int row, int col,
            int rowDirection,
            int colDirection,
            char player)
        {
            int count = 0;

            int r = row + rowDirection;
            int c = col + colDirection;

            while (r >= 0 && r < BoardSize &&
                   c >= 0 && c < BoardSize &&
                   board[r, c] == player)
            {
                count++;
                r += rowDirection;
                c += colDirection;
            }

            return count;
        }

        // Kiểm tra bàn cờ hòa
        private bool CheckDraw()
        {
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    if (board[row, col] == '\0')
                        return false;
                }
            }

            return true;
        }

        // Lấy dữ liệu bàn cờ
        public char[,] GetBoard()
        {
            return (char[,])board.Clone();
        }

        // Khởi động lại trận đấu
        public void ResetGame()
        {
            board = new char[BoardSize, BoardSize];
            currentTurn = 'X';
            isGameOver = false;
        }
    }
}

