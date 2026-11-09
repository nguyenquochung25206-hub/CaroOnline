// TODO TV4: Vẽ bàn cờ, phát sự kiện ô được chọn, cập nhật X/O.
using System;
using System.Drawing;
using System.Windows.Forms;
using CaroGame.Shared.Game;

namespace CaroGame.Client.Controls
{
    public class BoardControl : UserControl
    {
        private readonly BoardState board;

        private Player currentPlayer = Player.X;
        private GameResult gameResult = GameResult.Ongoing;

        public bool LocalMode { get; set; } = true;

        public event Action<Move> MovePlayed;

        public BoardControl()
        {
            board = new BoardState(15, 15);

            Size = new Size(600, 600);
            BackColor = Color.Bisque;
ẽ
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode =
                System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int margin = 25;

            float cellWidth =
                (Width - margin * 2) / (float)(board.Cols - 1);

            float cellHeight =
                (Height - margin * 2) / (float)(board.Rows - 1);

            using (Pen gridPen = new Pen(Color.SaddleBrown, 1))
            {
                for (int row = 0; row < board.Rows; row++)
                {
                    float y = margin + row * cellHeight;

                    g.DrawLine(gridPen,
                        margin, y, Width - margin, y);
                }

                for (int col = 0; col < board.Cols; col++)
                {
                    float x = margin + col * cellWidth;

                    g.DrawLine(gridPen,
                        x, margin, x, Height - margin);
                }
            }

            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Cols; col++)
                {
                    Player player = board.GetCell(row, col);

                    if (player == Player.None)
                        continue;

                    float centerX = margin + col * cellWidth;
                    float centerY = margin + row * cellHeight;

                    float radius =
                        Math.Min(cellWidth, cellHeight) * 0.32f;

                    Color pieceColor =
                        player == Player.X ? Color.Blue : Color.Red;

                    using (Pen piecePen = new Pen(pieceColor, 2.5f))
                    {
                        if (player == Player.X)
                        {
                            g.DrawLine(piecePen,
                                centerX - radius, centerY - radius,
                                centerX + radius, centerY + radius);

                            g.DrawLine(piecePen,
                                centerX - radius, centerY + radius,
                                centerX + radius, centerY - radius);
                        }
                        else
                        {
                            g.DrawEllipse(piecePen,
                                centerX - radius, centerY - radius,
                                radius * 2, radius * 2);
                        }
                    }
                }
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (e.Button != MouseButtons.Left)
                return;

            if (gameResult != GameResult.Ongoing)
                return;

            int margin = 25;

            float cellWidth =
                (Width - margin * 2) / (float)(board.Cols - 1);

            float cellHeight =
                (Height - margin * 2) / (float)(board.Rows - 1);

            int col = (int)Math.Round((e.X - margin) / cellWidth);
            int row = (int)Math.Round((e.Y - margin) / cellHeight);

            if (!board.IsInside(row, col))
                return;

            float centerX = margin + col * cellWidth;
            float centerY = margin + row * cellHeight;

            if (Math.Abs(e.X - centerX) > cellWidth * 0.4f ||
                Math.Abs(e.Y - centerY) > cellHeight * 0.4f)
                return;

            Move move = new Move(row, col, currentPlayer);

            if (LocalMode)
            {
                if (!board.TryPlaceMove(move))
                    return;

                gameResult = WinChecker.Check(board, move);

                MovePlayed?.Invoke(move);

                if (gameResult == GameResult.Ongoing)
                {
                    currentPlayer = currentPlayer == Player.X
                        ? Player.O
                        : Player.X;
                }

                Invalidate();
                ShowResultIfFinished();
            }
            else
            {
                MovePlayed?.Invoke(move);
            }
        }

        public bool ApplyRemoteMove(Move move)
        {
            if (move == null)
                return false;

            if (gameResult != GameResult.Ongoing)
                return false;

            if (!board.TryPlaceMove(move))
                return false;

            gameResult = WinChecker.Check(board, move);

            if (gameResult == GameResult.Ongoing)
            {
                currentPlayer = move.Player == Player.X
                    ? Player.O
                    : Player.X;
            }

            Invalidate();
            ShowResultIfFinished();

            return true;
        }

        public void ResetBoard()
        {
            board.Reset();
            currentPlayer = Player.X;
            gameResult = GameResult.Ongoing;

            Invalidate();
        }

        private void ShowResultIfFinished()
        {
            if (gameResult == GameResult.Ongoing)
                return;

            string message;

            switch (gameResult)
            {
                case GameResult.XWins:
                    message = "Người chơi X chiến thắng!";
                    break;

                case GameResult.OWins:
                    message = "Người chơi O chiến thắng!";
                    break;

                default:
                    message = "Ván đấu hòa!";
                    break;
            }

            MessageBox.Show(message, "Kết quả",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

