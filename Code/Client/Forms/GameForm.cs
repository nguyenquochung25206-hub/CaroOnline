// TODO TV4: Ghép BoardControl, lượt hiện tại, tên hai người chơi và trạng thái trận.
using System.Drawing;
using System.Windows.Forms;
using CaroGame.Client.Controls;

namespace CaroGame.Client
{
    public class GameForm : Form
    {
        private readonly BoardControl boardControl;
        private readonly Label statusLabel;

        public GameForm()
        {
            Text = "Caro Game - Test";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(680, 740);

            statusLabel = new Label
            {
                Text = "X đi trước",
                Dock = DockStyle.Top,
                Height = 35,
                TextAlign = ContentAlignment.MiddleCenter
            };

            boardControl = new BoardControl
            {
                Dock = DockStyle.Fill,
                LocalMode = true
            };

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

            boardControl.MovePlayed += move =>
            {
                statusLabel.Text =
                    $"Người vừa đánh: {move.Player}; " +
                    $"Hàng: {move.Row}; Cột: {move.Col}";
            };

            Controls.Add(boardControl);
            Controls.Add(resetButton);
            Controls.Add(statusLabel);
        }
    }
}

