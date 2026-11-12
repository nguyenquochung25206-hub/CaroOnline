using CaroGame.Shared.Game;

namespace Caro.Server.Validation
{
    public static class MoveValidator
    {
        public static bool IsValid(
            BoardState board,
            Move move,
            out string error)
        {
            error = string.Empty;

            // 1. Kiểm tra bàn cờ
            if (board == null)
            {
                error = "Ban co khong ton tai.";
                return false;
            }

            // 2. Kiểm tra nước đi
            if (move == null)
            {
                error = "Nuoc di khong hop le.";
                return false;
            }

            // 3. Kiểm tra người chơi
            if (move.Player != Player.X &&
                move.Player != Player.O)
            {
                error = "Nguoi choi khong hop le.";
                return false;
            }

            // 4. Kiểm tra tọa độ
            if (!board.IsInside(move.Row, move.Col))
            {
                error = "Toa do nam ngoai ban co.";
                return false;
            }

            // 5. Kiểm tra ô đã có quân chưa
            if (board.GetCell(move.Row, move.Col) != Player.None)
            {
                error = "O nay da co quan.";
                return false;
            }

            return true;
        }
    }
}
