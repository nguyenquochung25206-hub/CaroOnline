// TODO TV4: Trạng thái bàn cờ; thống nhất kích thước và quy ước ô trống.
namespace CaroGame.Shared.Game
{
    public sealed class BoardState
    {
        private readonly Player[,] cells;

        public int Rows { get; private set; }
        public int Cols { get; private set; }

        public BoardState(int rows = 15, int cols = 15)
        {
            if (rows <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(rows));

            if (cols <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(cols));

            Rows = rows;
            Cols = cols;

            cells = new Player[rows, cols];
            Reset();
        }

        public bool IsInside(int row, int col)
        {
            return row >= 0 && row < Rows
                && col >= 0 && col < Cols;
        }

        public Player GetCell(int row, int col)
        {
            if (!IsInside(row, col))
                throw new System.ArgumentOutOfRangeException(
                    "Tọa độ nằm ngoài bàn cờ.");

            return cells[row, col];
        }

        public bool TryPlaceMove(Move move)
        {
            if (move == null)
                return false;

            if (!IsInside(move.Row, move.Col))
                return false;

            if (cells[move.Row, move.Col] != Player.None)
                return false;

            cells[move.Row, move.Col] = move.Player;
            return true;
        }

        public bool IsFull()
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    if (cells[row, col] == Player.None)
                        return false;
                }
            }

            return true;
        }

        public void Reset()
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    cells[row, col] = Player.None;
                }
            }
        }
    }
}
