using Caro.Shared.Game;
namespace Caro.Server.Validation;
// TODO TV5: Server xác minh đúng phòng, đúng lượt, trong biên, ô trống.
public static class MoveValidator
{
    public static bool IsValid(BoardState board, Move move, out string error)
    { error = "Chưa triển khai"; return false; }
             // 1. Kiem tra ban co
            if (board == null)
            {
                error = "Ban co khong ton tai.";
                return false;
            }

            // 2. Kiem tra nuoc di
            if (move == null)
            {
                error = "Nuoc di khong hop le.";
                return false;
            }

            // 3. Kiem tra ma nguoi choi
            if (string.IsNullOrWhiteSpace(move.PlayerId))
            {
                error = "Ma nguoi choi khong hop le.";
                return false;
            }

            // 4. Kiem tra toa do nam trong ban co
            if (move.Row < 0 || move.Row >= BoardState.Size ||
                move.Col < 0 || move.Col >= BoardState.Size)
            {
                error = "Toa do nam ngoai ban co.";
                return false;
            }

            // 5. Kiem tra o da co quan hay chua
            if (board.Cells[move.Row, move.Col] != null)
            {
                error = "O nay da co quan.";
                return false;
            }

            // Tat ca dieu kien deu hop le
            return true;
        }
    }
}   
}
