// TODO TV4: Kiểm tra thắng theo luật nhóm thống nhất (5 liên tiếp, quy tắc chặn nếu áp dụng).
namespace CaroGame.Shared.Game
{
    public enum GameResult
    {
        Ongoing,
        XWins,
        OWins,
        Draw
    }

    public static class WinChecker
    {
        public static GameResult Check(
            BoardState board,
            Move lastMove,
            int winLength = 5)
        {
            if (board == null)
                throw new System.ArgumentNullException(nameof(board));

            if (lastMove == null)
                throw new System.ArgumentNullException(nameof(lastMove));

            if (winLength <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(winLength));

            if (!board.IsInside(lastMove.Row, lastMove.Col))
                throw new System.ArgumentException(
                    "Nước đi nằm ngoài bàn cờ.");

            if (board.GetCell(lastMove.Row, lastMove.Col)
                != lastMove.Player)
            {
                throw new System.ArgumentException(
                    "Nước đi chưa được đặt trên bàn cờ.");
            }

            int[,] directions =
            {
                { 0, 1 },
                { 1, 0 },
                { 1, 1 },
                { 1, -1 }
            };

            for (int i = 0; i < directions.GetLength(0); i++)
            {
                int rowStep = directions[i, 0];
                int colStep = directions[i, 1];

                int count = 1;

                count += CountDirection(
                    board, lastMove, rowStep, colStep);

                count += CountDirection(
                    board, lastMove, -rowStep, -colStep);

                if (count >= winLength)
                {
                    return lastMove.Player == Player.X
                        ? GameResult.XWins
                        : GameResult.OWins;
                }
            }

            if (board.IsFull())
                return GameResult.Draw;

            return GameResult.Ongoing;
        }

        private static int CountDirection(
            BoardState board,
            Move move,
            int rowStep,
            int colStep)
        {
            int count = 0;

            int row = move.Row + rowStep;
            int col = move.Col + colStep;

            while (board.IsInside(row, col)
                && board.GetCell(row, col) == move.Player)
            {
                count++;

                row += rowStep;
                col += colStep;
            }

            return count;
        }
    }
}
