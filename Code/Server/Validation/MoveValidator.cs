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
                error = "Bàn cờ không tồn tại.";
                return false;
            }

            // 2. Kiểm tra nước đi
            if (move == null)
            {
                error = "Nước đi không hợp lệ.";
                return false;
            }

            // 3. Kiểm tra người chơi
            if (move.Player != Player.X &&
                move.Player != Player.O)
            {
                error = "Người chơi không hợp lệ.";
                return false;
            }

            // 4. Kiểm tra tọa độ
            if (!board.IsInside(move.Row, move.Col))
            {
                error = "Tọa độ nằm ngoài bàn cờ.";
                return false;
            }

            // 5. Kiểm tra ô đã có quân chưa
            if (board.GetCell(move.Row, move.Col) != Player.None)
            {
                error = "Ô này đã có quân.";
                return false;
            }

            return true;
        }
    }
}
