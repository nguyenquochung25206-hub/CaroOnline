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
        private bool onlineGameFinished;

        public bool LocalMode { get; set; } = true;

        public Player MySymbol { get; set; } = Player.X;

        public bool IsMyTurn { get; private set; } = true;

        public event Action<Move>? MovePlayed;

        public BoardControl()
        {
            board = new BoardState(15, 15);
            Size = new Size(600, 600);
            BackColor = Color.Bisque;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int margin = 25;
            float cellWidth = (Width - margin * 2) / (float)(board.Cols - 1);
            float cellHeight = (Height - margin * 2) / (float)(board.Rows - 1);

            using (Pen gridPen = new Pen(Color.SaddleBrown, 1))
            {
                for (int row = 0; row < board.Rows; row++)
                {
                    float y = margin + row * cellHeight;
                    g.DrawLine(gridPen, margin, y, Width - margin, y);
                }

                for (int col = 0; col < board.Cols; col++)
                {
                    float x = margin + col * cellWidth;
                    g.DrawLine(gridPen, x, margin, x, Height - margin);
                }
            }

            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Cols; col++)
                {
                    Player player = board.GetCell(row, col);
                    if (player == Player.None) continue;

                    float centerX = margin + col * cellWidth;
                    float centerY = margin + row * cellHeight;
                    float radius = Math.Min(cellWidth, cellHeight) * 0.32f;
                    Color pieceColor = player == Player.X ? Color.Blue : Color.Red;

                    using (Pen piecePen = new Pen(pieceColor, 2.5f))
                    {
                        if (player == Player.X)
                        {
                            g.DrawLine(piecePen, centerX - radius, centerY - radius,
                                centerX + radius, centerY + radius);
                            g.DrawLine(piecePen, centerX - radius, centerY + radius,
                                centerX + radius, centerY - radius);
                        }
                        else
                        {
                            g.DrawEllipse(piecePen, centerX - radius, centerY - radius,
                                radius * 2, radius * 2);
                        }
                    }
                }
            }
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (e.Button != MouseButtons.Left) return;
            if (LocalMode && gameResult != GameResult.Ongoing) return;
            if (!LocalMode && (onlineGameFinished || !IsMyTurn)) return;

            int margin = 25;
            float cellWidth = (Width - margin * 2) / (float)(board.Cols - 1);
            float cellHeight = (Height - margin * 2) / (float)(board.Rows - 1);

            int col = (int)Math.Round((e.X - margin) / cellWidth);
            int row = (int)Math.Round((e.Y - margin) / cellHeight);

            if (!board.IsInside(row, col)) return;

            float centerX = margin + col * cellWidth;
            float centerY = margin + row * cellHeight;
            if (Math.Abs(e.X - centerX) > cellWidth * 0.4f ||
                Math.Abs(e.Y - centerY) > cellHeight * 0.4f)
                return;

            if (board.GetCell(row, col) != Player.None) return;

            Player symbol = LocalMode ? currentPlayer : MySymbol;
            Move move = new Move(row, col, symbol);

            if (LocalMode)
            {
                if (!board.TryPlaceMove(move)) return;

                gameResult = WinChecker.Check(board, move);
                MovePlayed?.Invoke(move);

                if (gameResult == GameResult.Ongoing)
                {
                    currentPlayer = currentPlayer == Player.X ? Player.O : Player.X;
                }

                Invalidate();
                ShowLocalResultIfFinished();
            }
            else
            {
                IsMyTurn = false;
                MovePlayed?.Invoke(move);
                Invalidate();
            }
        }

        public bool ApplyRemoteMove(Move move)
        {
            if (move == null || onlineGameFinished) return false;
            if (!board.TryPlaceMove(move)) return false;

            currentPlayer = move.Player == Player.X ? Player.O : Player.X;
            Invalidate();
            return true;
        }

        public void SetOnlineTurn(bool isMyTurn)
        {
            IsMyTurn = isMyTurn && !onlineGameFinished;
        }

        public void FinishOnlineGame()
        {
            onlineGameFinished = true;
            IsMyTurn = false;
            Invalidate();
        }

        public void ResetBoard()
        {
            board.Reset();
            currentPlayer = Player.X;
            gameResult = GameResult.Ongoing;
            onlineGameFinished = false;
            IsMyTurn = true;
            Invalidate();
        }

        private void ShowLocalResultIfFinished()
        {
            if (gameResult == GameResult.Ongoing) return;

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
