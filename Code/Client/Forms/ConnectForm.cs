using System.Drawing;
using System.Windows.Forms;
using Caro.Client.Services;

namespace Caro.Client.Forms;

/// <summary>
/// Giao diện nhập địa chỉ IP, cổng (port) và tên người chơi để kết nối tới Caro Server.
/// </summary>
public sealed class ConnectForm : Form
{
    private readonly TextBox _txtHost = new();
    private readonly NumericUpDown _numPort = new();
    private readonly TextBox _txtName = new();
    private readonly Label _lblStatus = new();
    private readonly Button _btnConnect = new();
    private readonly Button _btnCancel = new();

    public string ServerHost => _txtHost.Text.Trim();
    public int ServerPort => (int)_numPort.Value;
    public string PlayerName => _txtName.Text.Trim();

    private readonly ConnectionService? _connectionService;

    public ConnectForm(ConnectionService? connectionService = null, string defaultHost = "127.0.0.1", int defaultPort = 8888, string defaultName = "Player1")
    {
        _connectionService = connectionService;

        InitializeComponent(defaultHost, defaultPort, defaultName);
    }

    private void InitializeComponent(string defaultHost, int defaultPort, string defaultName)
    {
        Text = "Kết nối máy chủ - Caro Online";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 310);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Color.FromArgb(245, 247, 250);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.FromArgb(24, 76, 120),
            Padding = new Padding(15, 12, 15, 10)
        };

        var lblTitle = new Label
        {
            Text = "Cấu hình kết nối Server",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(15, 10)
        };

        var lblSubtitle = new Label
        {
            Text = "Nhập IP, cổng và tên người chơi của bạn để bắt đầu",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(210, 230, 250),
            AutoSize = true,
            Location = new Point(16, 35)
        };

        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSubtitle);

        // Group Body
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 15, 20, 15)
        };

        // Host IP
        var lblHost = new Label
        {
            Text = "Địa chỉ IP / Host:",
            Location = new Point(25, 80),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 40, 40)
        };
        _txtHost.Location = new Point(150, 77);
        _txtHost.Size = new Size(230, 26);
        _txtHost.Text = defaultHost;

        // Port
        var lblPort = new Label
        {
            Text = "Cổng (Port):",
            Location = new Point(25, 118),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 40, 40)
        };
        _numPort.Location = new Point(150, 115);
        _numPort.Size = new Size(120, 26);
        _numPort.Minimum = 1;
        _numPort.Maximum = 65535;
        _numPort.Value = defaultPort is >= 1 and <= 65535 ? defaultPort : 8888;

        // Player Name
        var lblName = new Label
        {
            Text = "Tên người chơi:",
            Location = new Point(25, 156),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 40, 40)
        };
        _txtName.Location = new Point(150, 153);
        _txtName.Size = new Size(230, 26);
        _txtName.Text = defaultName;
        _txtName.MaxLength = 20;

        // Status / Error message
        _lblStatus.Location = new Point(25, 192);
        _lblStatus.Size = new Size(355, 38);
        _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
        _lblStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
        _lblStatus.Text = "";

        // Action Buttons
        _btnConnect.Text = "Kết nối";
        _btnConnect.Location = new Point(180, 245);
        _btnConnect.Size = new Size(105, 36);
        _btnConnect.BackColor = Color.FromArgb(24, 119, 242);
        _btnConnect.ForeColor = Color.White;
        _btnConnect.FlatStyle = FlatStyle.Flat;
        _btnConnect.FlatAppearance.BorderSize = 0;
        _btnConnect.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _btnConnect.Cursor = Cursors.Hand;
        _btnConnect.Click += OnConnectClicked;

        _btnCancel.Text = "Hủy bỏ";
        _btnCancel.Location = new Point(295, 245);
        _btnCancel.Size = new Size(85, 36);
        _btnCancel.BackColor = Color.FromArgb(220, 224, 230);
        _btnCancel.ForeColor = Color.FromArgb(50, 50, 50);
        _btnCancel.FlatStyle = FlatStyle.Flat;
        _btnCancel.FlatAppearance.BorderSize = 0;
        _btnCancel.DialogResult = DialogResult.Cancel;
        _btnCancel.Cursor = Cursors.Hand;

        AcceptButton = _btnConnect;
        CancelButton = _btnCancel;

        Controls.Add(_btnCancel);
        Controls.Add(_btnConnect);
        Controls.Add(_lblStatus);
        Controls.Add(_txtName);
        Controls.Add(lblName);
        Controls.Add(_numPort);
        Controls.Add(lblPort);
        Controls.Add(_txtHost);
        Controls.Add(lblHost);
        Controls.Add(pnlHeader);
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);

        if (string.IsNullOrWhiteSpace(_txtHost.Text))
        {
            _lblStatus.Text = "Vui lòng nhập địa chỉ IP hoặc Hostname.";
            _txtHost.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            _lblStatus.Text = "Vui lòng nhập tên người chơi (tối đa 20 ký tự).";
            _txtName.Focus();
            return;
        }

        // Nếu có truyền ConnectionService, thử kết nối trực tiếp
        if (_connectionService != null)
        {
            SetConnectingState(true);
            _lblStatus.ForeColor = Color.FromArgb(24, 76, 120);
            _lblStatus.Text = $"Đang kết nối tới {_txtHost.Text.Trim()}:{_numPort.Value}...";

            bool success = await _connectionService.ConnectAsync(
                _txtHost.Text.Trim(),
                (int)_numPort.Value,
                _txtName.Text.Trim()
            );

            if (!success)
            {
                SetConnectingState(false);
                _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
                _lblStatus.Text = "Không thể kết nối tới server. Vui lòng kiểm tra IP/cổng.";
                return;
            }
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void SetConnectingState(bool isConnecting)
    {
        _btnConnect.Enabled = !isConnecting;
        _btnCancel.Enabled = !isConnecting;
        _txtHost.Enabled = !isConnecting;
        _numPort.Enabled = !isConnecting;
        _txtName.Enabled = !isConnecting;
        _btnConnect.Text = isConnecting ? "Đang kết nối..." : "Kết nối";
    }
}
