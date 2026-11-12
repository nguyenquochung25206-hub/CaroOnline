using System;
using CaroGame.Shared.Game;

namespace Caro.Server.Validation
{
    public static class MoveValidator
    {
        public static bool IsValid(BoardState board, Move move, out string error)
        {
            error = string.Empty;

            // 1. Kiểm tra bàn cờ có tồn tại không
            if (board == null)
            {
                error = "Ban co khong ton tai.";
                return false;
            }

            // 2. Kiểm tra nước đi có tồn tại không
            if (move == null)
            {
                error = "Nuoc di khong hop le.";
                return false;
            }

            // 3. Kiểm tra mã người chơi
            if (string.IsNullOrWhiteSpace(move.PlayerId))
            {
                error = "Ma nguoi choi khong hop le.";
                return false;
            }

            // 4. Kiểm tra tọa độ nằm trong bàn cờ
            if (move.Row < 0 || move.Row >= BoardState.Size ||
                move.Col < 0 || move.Col >= BoardState.Size)
            {
                error = "Toa do nam ngoai ban co.";
                return false;
            }

            // 5. Kiểm tra ô cờ đã có người đánh chưa
            if (board.Cells[move.Row, move.Col] != null)
            {
                error = "O nay da co quan.";
                return false;
            }

            // Tất cả điều kiện đều hợp lệ
            return true;
        }
    }
}
