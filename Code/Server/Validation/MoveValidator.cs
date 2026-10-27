using Caro.Shared.Game;
namespace Caro.Server.Validation;
// TODO TV5: Server xác minh đúng phòng, đúng lượt, trong biên, ô trống.
public static class MoveValidator
{
    public static bool IsValid(BoardState board, Move move, out string error)
    { error = "Chưa triển khai"; return false; }
}
