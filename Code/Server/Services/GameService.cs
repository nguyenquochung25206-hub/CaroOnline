// TODO TV5: Điều phối lượt đi, kiểm tra nước đi và kết thúc trận.

using System;
using Caro.Server.Validation;

namespace Caro.Server.Services
{
    public sealed class GameService
    {
        private readonly string[,] board;

        private readonly string playerX;
        private readonly string playerO;

        private string currentPlayerId;
        private string? winnerId;

        private bool isGameOver;

        public GameService(string playerX, string playerO)
        {
            if (string.IsNullOrWhiteSpace(playerX) ||
                string.IsNullOrWhiteSpace(playerO) ||
                playerX == playerO)
            {
                throw new ArgumentException(
                    "Thong tin nguoi choi khong hop le.");
            }

            this.playerX = playerX;
            this.playerO = playerO;

            board = new string[
                MoveValidator.BoardSize,
                MoveValidator.BoardSize
            ];

            currentPlayerId = playerX;
            winnerId = null;
            isGameOver = false;
        }

        public string GetCurrentPlayer()
        {
            return currentPlayerId;
        }

        public string GetWinner()
        {
            return winnerId;
        }

        public bool IsGameOver()
        {
            return isGameOver;
        }

        // Xử lý nước đi
        public bool MakeMove(
            string playerId,
            int row,
            int col,
            out string error)
        {
            error = string.Empty;

            if (isGameOver)
            {
                error = "Tran dau da ket thuc.";
                return false;
            }

            if (playerId != playerX && playerId != playerO)
            {
                error = "Nguoi choi khong thuoc tran dau.";
                return false;
            }

            if (playerId != currentPlayerId)
            {
                error = "Chua den luot cua ban.";
                return false;
            }

            if (!MoveValidator.IsValid(
                board, row, col, playerId, out error))
            {
                return false;
            }

            // Cập nhật bàn cờ
            board[row, col] = playerId;

            // Kiểm tra chiến thắng
            if (CheckWin(row, col, playerId))
            {
                winnerId = playerId;
                isGameOver = true;
                error = "Nguoi choi " + playerId +
                        " da chien thang.";
                return true;
            }

            // Kiểm tra hòa
            if (CheckDraw())
            {
                isGameOver = true;
                error = "Tran dau hoa.";
                return true;
            }

            // Chuyển lượt
            currentPlayerId =
                currentPlayerId == playerX ? playerO : playerX;

            error = "Nuoc di hop le.";
            return true;
        }

        // Kiểm tra thắng
        private bool CheckWin(int row, int col, string playerId)
        {
            int[,] directions =
            {
                { 0, 1 },
                { 1, 0 },
                { 1, 1 },
                { 1, -1 }
            };

            for (int i = 0; i < 4; i++)
            {
                int count = 1;

                count += CountDirection(
                    row, col,
                    directions[i, 0],
                    directions[i, 1],
                    playerId);

                count += CountDirection(
                    row, col,
                    -directions[i, 0],
                    -directions[i, 1],
                    playerId);

                if (count >= 5)
                    return true;
            }

            return false;
        }

        // Đếm quân liên tiếp
        private int CountDirection(
            int row,
            int col,
            int rowDirection,
            int colDirection,
            string playerId)
        {
            int count = 0;

            int r = row + rowDirection;
            int c = col + colDirection;

            while (r >= 0 && r < MoveValidator.BoardSize &&
                   c >= 0 && c < MoveValidator.BoardSize &&
                   board[r, c] == playerId)
            {
                count++;
                r += rowDirection;
                c += colDirection;
            }

            return count;
        }

        // Kiểm tra hòa
        private bool CheckDraw()
        {
            for (int r = 0; r < MoveValidator.BoardSize; r++)
            {
                for (int c = 0; c < MoveValidator.BoardSize; c++)
                {
                    if (board[r, c] == null)
                        return false;
                }
            }

            return true;
        }

        // Lấy dữ liệu bàn cờ
        public string[,] GetBoard()
        {
            return (string[,])board.Clone();
        }
    }
}


