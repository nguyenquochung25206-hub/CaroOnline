namespace Caro.Client.Forms;
// TODO TV3: WinForms GUI; nhập IP/port/tên, connect/disconnect, trạng thái, log, nút ping và phòng chơi.
public sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "Caro Online"; Width = 900; Height = 650;
        Controls.Add(new Label { Text = "Caro Online — TODO: xây dựng giao diện", AutoSize = true, Left = 20, Top = 20 });
    }
}
