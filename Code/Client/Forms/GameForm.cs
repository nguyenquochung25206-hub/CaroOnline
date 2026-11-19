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
        private readonly ConnectionService? _connectionService;
        private readonly string _mySymbol;
        private readonly string _playerX;
        private readonly string _playerO;

        public GameForm() : this(null, "X", "Người chơi X", "Người chơi O") { }

        public GameForm(ConnectionService? connectionService, string mySymbol, string playerX, string playerO)
        {
            _connectionService = connectionService;
            _mySymbol = string.IsNullOrWhiteSpace(mySymbol) ? "X" : mySymbol.Trim().ToUpperInvariant();
            _playerX = string.IsNullOrWhiteSpace(playerX) ? "Người chơi X" : playerX.Trim();
            _playerO = string.IsNullOrWhiteSpace(playerO) ? "Người chơi O" : playerO.Trim();

            Text = $"Caro Online — [{_playerX} (X) vs {_playerO} (O)] — Bạn là quân {_mySymbol}";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 780);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            playerInfoLabel = new Label
            {
                Text = $"Trận đấu: {_playerX} (X)  VS  {_playerO} (O)   |   Bạn: Quân {_mySymbol}",
                Dock = DockStyle.Top,
                Height = 35,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 76, 120),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(240, 245, 250)
            };

            statusLabel = new Label
            {
                Text = (_mySymbol == "X") ? "Ván cờ bắt đầu: Đến lượt của bạn (Quân X)!" : "Ván cờ bắt đầu: Đang đợi Quân X đi trước...",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = (_mySymbol == "X") ? Color.FromArgb(25, 135, 84) : Color.FromArgb(100, 100, 100),
                TextAlign = ContentAlignment.MiddleCenter
            };

            boardControl = new BoardControl
            {
                Dock = DockStyle.Fill,
                LocalMode = (_connectionService == null) // Chơi online thì LocalMode = false
            };

            Controls.Add(boardControl);
            Controls.Add(statusLabel);
            Controls.Add(playerInfoLabel);

            if (_connectionService != null)
            {
                RegisterNetworkEvents();
            }
            else
            {
                // Local mode offline testing
                Button resetButton = new Button
                {
                    Text = "Chơi lại",
                    Dock = DockStyle.Bottom,
                    Height = 40
                };
                resetButton.Click += (sender, e) =>
                {
                    boardControl.ResetBoard();
                    statusLabel.Text = "Đã làm mới bàn cờ: X đi trước";
                };
                Controls.Add(resetButton);

                boardControl.MovePlayed += move =>
                {
                    statusLabel.Text = $"Người vừa đánh: {move.Player}; Hàng: {move.Row}; Cột: {move.Col}";
                };
            }
        }

        private void RegisterNetworkEvents()
        {
            if (_connectionService == null) return;

            // Người dùng click trên bàn cờ -> gửi nước đi lên server
            boardControl.MovePlayed += async (move) =>
            {
                await _connectionService.SendMoveAsync(move.Row, move.Col);
            };

            // Server phản hồi nước đi hợp lệ
            _connectionService.MoveAccepted += (row, col) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(() => {
                        statusLabel.Text = $"Nước đi ({row}, {col}) hợp lệ! Đang đợi đối thủ...";
                        statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
                    });
                    return;
                }
                statusLabel.Text = $"Nước đi ({row}, {col}) hợp lệ! Đang đợi đối thủ...";
                statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
            };

            // Server từ chối nước đi
            _connectionService.MoveRejected += (reason) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(() => {
                        statusLabel.Text = $"Nước đi bị từ chối: {reason}";
                        statusLabel.ForeColor = Color.FromArgb(220, 53, 69);
                    });
                    return;
                }
                statusLabel.Text = $"Nước đi bị từ chối: {reason}";
                statusLabel.ForeColor = Color.FromArgb(220, 53, 69);
            };

            // Server cập nhật bàn cờ (BOARD_UPDATED)
            _connectionService.BoardUpdated += (row, col, symbol, nextTurn) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(() => OnBoardUpdated(row, col, symbol, nextTurn));
                    return;
                }
                OnBoardUpdated(row, col, symbol, nextTurn);
            };

            // Server thông báo kết thúc ván (GAME_OVER)
            _connectionService.GameOver += (winnerSymbol, winnerName, reason) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(() => OnGameOver(winnerSymbol, winnerName, reason));
                    return;
                }
                OnGameOver(winnerSymbol, winnerName, reason);
            };

            // Đối thủ thoát (OPPONENT_LEFT)
            _connectionService.OpponentLeft += (opponent) =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(() => {
                        statusLabel.Text = $"Đối thủ '{opponent}' đã rời phòng! Bạn giành chiến thắng.";
                        statusLabel.ForeColor = Color.FromArgb(25, 135, 84);
                        MessageBox.Show($"Đối thủ '{opponent}' đã rời trận đấu. Bạn chiến thắng!", "Trận đấu kết thúc", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });
                    return;
                }
                statusLabel.Text = $"Đối thủ '{opponent}' đã rời phòng! Bạn giành chiến thắng.";
                statusLabel.ForeColor = Color.FromArgb(25, 135, 84);
                MessageBox.Show($"Đối thủ '{opponent}' đã rời trận đấu. Bạn chiến thắng!", "Trận đấu kết thúc", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        private void OnBoardUpdated(int row, int col, string symbol, string nextTurn)
        {
            if (row >= 0 && col >= 0)
            {
                var player = symbol.Equals("X", StringComparison.OrdinalIgnoreCase) ? Player.X : Player.O;
                boardControl.ApplyRemoteMove(new Move(row, col, player));
            }

            bool isMyTurn = string.Equals(nextTurn, _mySymbol, StringComparison.OrdinalIgnoreCase);
            if (isMyTurn)
            {
                statusLabel.Text = $"Đến lượt của bạn (Quân {_mySymbol})!";
                statusLabel.ForeColor = Color.FromArgb(25, 135, 84);
            }
            else
            {
                statusLabel.Text = $"Đang đợi đối thủ ({nextTurn}) suy nghĩ...";
                statusLabel.ForeColor = Color.FromArgb(100, 100, 100);
            }
        }

        private void OnGameOver(string winnerSymbol, string winnerName, string reason)
        {
            bool isMeWinner = string.Equals(winnerSymbol, _mySymbol, StringComparison.OrdinalIgnoreCase);
            string msg = isMeWinner ? "Chúc mừng! Bạn đã CHIẾN THẮNG!" : $"Rất tiếc! Người chơi {winnerName} ({winnerSymbol}) đã thắng.";
            if (!string.IsNullOrEmpty(reason)) msg += $"\n({reason})";

            statusLabel.Text = msg;
            statusLabel.ForeColor = isMeWinner ? Color.FromArgb(25, 135, 84) : Color.FromArgb(220, 53, 69);
            MessageBox.Show(msg, "Kết thúc trận đấu", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
