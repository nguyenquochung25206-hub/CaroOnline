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
        private readonly Button returnToLobbyButton;
        private readonly ConnectionService? connectionService;

        private readonly string mySymbol;
        private readonly string playerX;
        private readonly string playerO;

        private bool eventsSubscribed;
        private bool gameFinished;
        private bool leavingRoom;

        public GameForm()
            : this(null, "X", "Người chơi X", "Người chơi O")
        {
        }

        public GameForm(
            ConnectionService? connectionService,
            string mySymbol,
            string playerX,
            string playerO)
        {
            this.connectionService = connectionService;
            this.mySymbol = NormalizeSymbol(mySymbol);

            this.playerX = string.IsNullOrWhiteSpace(playerX)
                ? "Người chơi X" : playerX.Trim();

            this.playerO = string.IsNullOrWhiteSpace(playerO)
                ? "Người chơi O" : playerO.Trim();

            Text = $"Caro Online — {this.playerX} (X) vs {this.playerO} (O)";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 780);
            MinimumSize = new Size(600, 650);
            Font = new Font("Segoe UI", 9.5f);

            playerInfoLabel = new Label
            {
                Text = $"Trận đấu: {this.playerX} (X) VS {this.playerO} (O) | Bạn: {this.mySymbol}",
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
                    : startsWithMyTurn
                        ? "Đến lượt của bạn (X)."
                        : "Đang đợi X đi trước...",

                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = startsWithMyTurn
                    ? Color.FromArgb(25, 135, 84)
                    : Color.DimGray,
                TextAlign = ContentAlignment.MiddleCenter
            };

            boardControl = new BoardControl
            {
                Dock = DockStyle.Fill,
                LocalMode = connectionService == null,
                MySymbol = this.mySymbol == "X"
                    ? Player.X : Player.O
            };

            boardControl.SetOnlineTurn(startsWithMyTurn);

            returnToLobbyButton = new Button
            {
                Text = "Quay về sảnh",
                Dock = DockStyle.Bottom,
                Height = 42,
                Visible = false
            };

            returnToLobbyButton.Click += OnReturnToLobbyClicked;

            Controls.Add(boardControl);
            Controls.Add(statusLabel);
            Controls.Add(playerInfoLabel);
            Controls.Add(returnToLobbyButton);

            FormClosed += OnFormClosedAsync;

            if (connectionService != null)
                Subscribe();
            else
                AddLocalModeControls();
        }

        private static string NormalizeSymbol(string? symbol)
        {
            return string.Equals(
                symbol?.Trim(), "O",
                StringComparison.OrdinalIgnoreCase) ? "O" : "X";
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
                statusLabel.Text =
                    $"Người vừa đánh: {move.Player} — Hàng: {move.Row}, Cột: {move.Col}";
            };
        }

        private void Subscribe()
        {
            if (connectionService == null || eventsSubscribed) return;

            boardControl.MovePlayed += OnMovePlayed;

            connectionService.MoveAccepted += OnMoveAccepted;
            connectionService.MoveRejected += OnMoveRejected;
            connectionService.BoardUpdated += OnBoardUpdatedFromServer;
            connectionService.GameOver += OnGameOverFromServer;
            connectionService.OpponentLeft += OnOpponentLeft;
            connectionService.ConnectionLost += OnConnectionLost;

            eventsSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (connectionService == null || !eventsSubscribed) return;

            boardControl.MovePlayed -= OnMovePlayed;

            connectionService.MoveAccepted -= OnMoveAccepted;
            connectionService.MoveRejected -= OnMoveRejected;
            connectionService.BoardUpdated -= OnBoardUpdatedFromServer;
            connectionService.GameOver -= OnGameOverFromServer;
            connectionService.OpponentLeft -= OnOpponentLeft;
            connectionService.ConnectionLost -= OnConnectionLost;

            eventsSubscribed = false;
        }

        private async void OnMovePlayed(Move move)
        {
            if (connectionService == null || gameFinished) return;

            bool sent = await connectionService.SendMoveAsync(
                move.Row, move.Col);

            if (!sent && !IsDisposed && !gameFinished)
            {
                RunOnUiThread(() =>
                {
                    boardControl.SetOnlineTurn(true);
                    statusLabel.Text =
                        "Không gửi được nước đi. Vui lòng kiểm tra kết nối.";
                    statusLabel.ForeColor = Color.FromArgb(220, 53, 69);
                });
            }
        }

        private void OnMoveAccepted(int row, int col)
        {
            RunOnUiThread(() =>
            {
                statusLabel.Text =
                    $"Server đã chấp nhận nước đi ({row}, {col}).";
            });
        }

        private void OnMoveRejected(string reason)
        {
            RunOnUiThread(() =>
            {
                if (gameFinished) return;

                boardControl.SetOnlineTurn(true);
                statusLabel.Text = $"Nước đi bị từ chối: {reason}";
                statusLabel.ForeColor = Color.FromArgb(220, 53, 69);
            });
        }

        private void OnBoardUpdatedFromServer(
            int row, int col, string symbol, string nextTurn)
        {
            RunOnUiThread(() =>
                OnBoardUpdated(row, col, symbol, nextTurn));
        }

        private void OnGameOverFromServer(
            string winnerSymbol, string winnerName, string reason)
        {
            RunOnUiThread(() =>
                OnGameOver(winnerSymbol, winnerName, reason));
        }

        private void OnOpponentLeft(string opponent)
        {
            RunOnUiThread(() =>
            {
                if (gameFinished) return;

                FinishGame(
                    $"Đối thủ '{opponent}' đã rời trận đấu.",
                    Color.FromArgb(25, 135, 84),
                    "Đối thủ đã rời trận đấu");
            });
        }

        private void OnConnectionLost(string reason)
        {
            RunOnUiThread(() =>
            {
                if (gameFinished) return;

                string message = "Mất kết nối với máy chủ. Bàn cờ đã được khóa.";

                if (!string.IsNullOrWhiteSpace(reason))
                    message += $"\nLý do: {reason}";

                FinishGame(
                    message,
                    Color.FromArgb(220, 53, 69),
                    "Mất kết nối");
            });
        }

        private void OnBoardUpdated(
            int row, int col, string symbol, string nextTurn)
        {
            if (gameFinished) return;

            if (row >= 0 && col >= 0 &&
                (symbol.Equals("X", StringComparison.OrdinalIgnoreCase) ||
                 symbol.Equals("O", StringComparison.OrdinalIgnoreCase)))
            {
                Player player = symbol.Equals(
                    "X", StringComparison.OrdinalIgnoreCase)
                    ? Player.X : Player.O;

                bool applied = boardControl.ApplyRemoteMove(
                    new Move(row, col, player));

                if (!applied)
                {
                    statusLabel.Text =
                        "Không thể áp dụng nước đi Server gửi. Bàn cờ có thể đã lệch trạng thái.";

                    statusLabel.ForeColor = Color.FromArgb(220, 53, 69);
                    boardControl.FinishOnlineGame();
                    gameFinished = true;
                    return;
                }
            }

            bool isMyTurn = string.Equals(
                nextTurn, mySymbol,
                StringComparison.OrdinalIgnoreCase);

            boardControl.SetOnlineTurn(isMyTurn);

            statusLabel.Text = isMyTurn
                ? $"Đến lượt của bạn (quân {mySymbol})!"
                : $"Đang đợi đối thủ ({nextTurn}) đi...";

            statusLabel.ForeColor = isMyTurn
                ? Color.FromArgb(25, 135, 84)
                : Color.DimGray;
        }

        private void OnGameOver(
            string winnerSymbol, string winnerName, string reason)
        {
            bool isDraw = string.IsNullOrWhiteSpace(winnerSymbol);

            bool isMeWinner = !isDraw &&
                string.Equals(
                    winnerSymbol, mySymbol,
                    StringComparison.OrdinalIgnoreCase);

            string message = isDraw
                ? "Ván cờ kết thúc với kết quả HÒA."
                : isMeWinner
                    ? "Chúc mừng! Bạn đã chiến thắng!"
                    : $"Rất tiếc! {NormalizeWinnerName(winnerName, winnerSymbol)} ({winnerSymbol}) đã chiến thắng.";

            if (!string.IsNullOrWhiteSpace(reason))
                message += $"\nLý do: {reason}";

            FinishGame(
                message,
                isDraw || isMeWinner
                    ? Color.FromArgb(25, 135, 84)
                    : Color.FromArgb(220, 53, 69),
                "Kết thúc trận đấu");
        }

        private string NormalizeWinnerName(
            string winnerName, string winnerSymbol)
        {
            if (!string.IsNullOrWhiteSpace(winnerName))
                return winnerName.Trim();

            return winnerSymbol == "X" ? playerX : playerO;
        }

        private void FinishGame(
            string message, Color color, string title)
        {
            if (gameFinished) return;

            gameFinished = true;
            boardControl.FinishOnlineGame();

            statusLabel.Text = message;
            statusLabel.ForeColor = color;
            returnToLobbyButton.Visible = connectionService != null;

            MessageBox.Show(
                message, title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnReturnToLobbyClicked(
            object? sender, EventArgs e)
        {
            Close();
        }

        private async void OnFormClosedAsync(
            object? sender, FormClosedEventArgs e)
        {
            Unsubscribe();

            if (connectionService != null &&
                connectionService.IsInRoom &&
                !leavingRoom)
            {
                leavingRoom = true;

                try
                {
                    await connectionService.LeaveRoomAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Không thể rời phòng khi đóng GameForm: {ex.Message}");
                }
            }
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || Disposing) return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(action);
                }
                catch (InvalidOperationException)
                {
                }
            }
            else
            {
                action();
            }
        }
    }
}
