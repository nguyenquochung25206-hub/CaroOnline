// TODO TV4: Một nước đi trên bàn cờ.
namespace CaroGame.Shared.Game
{
    public enum Player
    {
        None = 0,
        X = 1,
        O = 2
    }
    public sealed class Move
    {
        public int Row { get; private set; }
        public int Col { get; private set; }
        public Player Player { get; private set; }

        public Move(int row, int col, Player player)
        {
            if (player != Player.X && player != Player.O)
            {
                throw new System.ArgumentException(
                    "Người chơi phải là X hoặc O.",
                    nameof(player));
            }
            Row = row;
            Col = col;
            Player = player;
        }
    }
}

