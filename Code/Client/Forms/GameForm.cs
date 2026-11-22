using System;
using System.Drawing;
using System.Windows.Forms;
using Caro.Client.Services;
using CaroGame.Client.Controls;
using CaroGame.Shared.Game;

namespace CaroGame.Client
{
    public class GameForm : Form
    {
        private readonly BoardControl boardControl;
        private readonly Label statusLabel;
        private readonly Label playerInfoLabel;
        private readonly ConnectionService? connectionService;
        private readonly string mySymbol;
        private readonly string playerX;
        private readonly string playerO;

        public GameForm() : this(null, "X", "Người chơi X", "Người chơi O")
        {
        }

        public GameForm(ConnectionService? connectionService,
            string mySymbol, string playerX, string playerO)
        {
            this.connectionService = connectionService;
            this.mySymbol = NormalizeSymbol(mySymbol);
            this.playerX = string.IsNullOrWhiteSpace(playerX) ? "Người chơi X" : playerX.Trim();
            this.playerO = string.IsNullOrWhiteSpace(playerO) ? "Người chơi O" : playerO.Trim();

            Text = $"Caro Online — {this.playerX} (X) vs {this.playerO} (O) — Bạn là {this.mySymbol}";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 780);
            MinimumSize = new Size(600, 650);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            playerInfoLabel = new Label
            {
                Text = $"Trận đấu: {this.playerX} (X)  VS  {this.playerO} (O)   |   Bạn: Quân {this.mySymbol}",
                Dock = DockStyle.Top,
                Height = 38,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 76, 120),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(240, 245, 250)
            };

            bool startsWithMyTurn = this.mySymbol == "X";
            statusLabel = new Label
            {
                Text = connectionService == null
                    ? "Chế độ chơi thử: X đi trước."
                    : (startsWithMyTurn ? "Đến lượt của bạn (X)." : "Đang đợi X đi trước..."),
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = startsWithMyTurn ? Color.FromArgb(25, 135, 84) : Color.DimGray,
                TextAlign = ContentAlignment.MiddleCenter
            };

            boardControl = new BoardControl
            {
                Dock = DockStyle.Fill,
                LocalMode = connectionService == null,
                MySymbol = this.mySymbol == "X" ? Player.X : Player.O
            };
            boardControl.SetOnlineTurn(startsWithMyTurn);

            Controls.Add(boardControl);
            Controls.Add(statusLabel);
            Controls.Add(playerInfoLabel);

            if (connectionService != null)
            {
                RegisterNetworkEvents();
            }
            else
            {
                AddLocalModeControls();
            }
        }
        private static string NormalizeSymbol(string? symbol)
        {
            return string.Equals(symbol?.Trim(), "O", StringComparison.OrdinalIgnoreCase)
                ? "O" : "X";
        }

        private void AddLocalModeControls()
        {
            Button resetButton = new Button
            {
                Text = "Chơi lại",
                Dock = DockStyle.Bottom,
                Height = 40
            };

            resetButton.Click += (sender, e) =>
            {
                boardControl.ResetBoard();
                statusLabel.Text = "Đã làm mới bàn cờ: X đi trước.";
                statusLabel.ForeColor = Color.FromArgb(25, 135, 84);
            };
            Controls.Add(resetButton);

            boardControl.MovePlayed += move =>
            {
                statusLabel.Text = $"Người vừa đánh: {move.Player} — Hàng: {move.Row}, Cột: {move.Col}";
                statusLabel.ForeColor = Color.DimGray;
            };
        }

        private void RegisterNetworkEvents()
        {
            if (connectionService == null) return;

            boardControl.MovePlayed += async move =>
            {
                bool sent = await connectionService.SendMoveAsync(move.Row, move.Col);
                if (!sent && !IsDisposed)
                {
                    RunOnUiThread(() =>
                    {
                        boardControl.SetOnlineTurn(true);
                        statusLabel.Text = "Không gửi được nước đi. Bạn có thể thử lại.";
                        statusLabel.ForeColor = Color.FromArgb(220, 53, 69);
                    });
                }
            };
            connectionService.MoveAccepted += (row, col) => RunOnUiThread(() =>
            {
                statusLabel.Text = $"Server đã chấp nhận nước đi ({row}, {col}). Đang chờ cập nhật bàn cờ...";
                statusLabel.ForeColor = Color.DimGray;
            });

            connectionService.MoveRejected += reason => RunOnUiThread(() =>
            {
                boardControl.SetOnlineTurn(true);
                statusLabel.Text = $"Nước đi bị từ chối: {reason}";
                statusLabel.ForeColor = Color.FromArgb(220, 53, 69);
            });

            connectionService.BoardUpdated += (row, col, symbol, nextTurn) =>
                RunOnUiThread(() => OnBoardUpdated(row, col, symbol, nextTurn));

            connectionService.GameOver += (winnerSymbol, winnerName, reason) =>
                RunOnUiThread(() => OnGameOver(winnerSymbol, winnerName, reason));

            connectionService.OpponentLeft += opponent => RunOnUiThread(() =>
            {
                boardControl.FinishOnlineGame();
                string message = $"Đối thủ '{opponent}' đã rời trận đấu.";
                statusLabel.Text = message;
                statusLabel.ForeColor = Color.FromArgb(25, 135, 84);
                MessageBox.Show(message, "Trận đấu kết thúc",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        private void OnBoardUpdated(int row, int col, string symbol, string nextTurn)
        {
            if (row >= 0 && col >= 0 &&
                (symbol.Equals("X", StringComparison.OrdinalIgnoreCase) ||
                 symbol.Equals("O", StringComparison.OrdinalIgnoreCase)))
            {
                Player player = symbol.Equals("X", StringComparison.OrdinalIgnoreCase)
                    ? Player.X : Player.O;

                bool applied = boardControl.ApplyRemoteMove(new Move(row, col, player));
                if (!applied)
                {
                    statusLabel.Text = "Không thể áp dụng nước đi Server gửi. Có thể bàn cờ đang lệch trạng thái.";
                    statusLabel.ForeColor = Color.FromArgb(220, 53, 69);
                    return;
                }
            }
            bool isMyTurn = string.Equals(nextTurn, mySymbol, StringComparison.OrdinalIgnoreCase);
            boardControl.SetOnlineTurn(isMyTurn);

            if (isMyTurn)
            {
                statusLabel.Text = $"Đến lượt của bạn (quân {mySymbol})!";
                statusLabel.ForeColor = Color.FromArgb(25, 135, 84);
            }
            else
            {
                statusLabel.Text = $"Đang đợi đối thủ ({nextTurn}) đi...";
                statusLabel.ForeColor = Color.DimGray;
            }
        }

        private void OnGameOver(string winnerSymbol, string winnerName, string reason)
        {
            boardControl.FinishOnlineGame();

            bool isMeWinner = string.Equals(winnerSymbol, mySymbol, StringComparison.OrdinalIgnoreCase);
            string message = isMeWinner
                ? "Chúc mừng! Bạn đã chiến thắng!"
                : $"Rất tiếc! {winnerName} ({winnerSymbol}) đã chiến thắng.";

            if (!string.IsNullOrWhiteSpace(reason))
                message += $"\nLý do: {reason}";

            statusLabel.Text = message;
            statusLabel.ForeColor = isMeWinner
                ? Color.FromArgb(25, 135, 84)
                : Color.FromArgb(220, 53, 69);

            MessageBox.Show(message, "Kết thúc trận đấu",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || Disposing) return;

            if (InvokeRequired)
            {
                try { BeginInvoke(action); }
                catch (InvalidOperationException) { }
            }
            else
            {
                action();
            }
        }
    }
}
