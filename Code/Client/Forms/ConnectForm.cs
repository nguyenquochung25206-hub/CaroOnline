using System.Drawing;
using System.Windows.Forms;
using Caro.Client.Services;

namespace Caro.Client.Forms;

/// <summary>
/// Giao diện nhập địa chỉ IP, cổng (port) và tên người chơi để kết nối tới Caro Server (Nhiệm vụ TV3 - Tuần 1, 4).
/// Hỗ trợ nạp cấu hình gần nhất, danh sách gợi ý Preset nhanh (Localhost/LAN), xác thực dữ liệu đầu vào và báo lỗi trực quan.
/// </summary>
public sealed class ConnectForm : Form
{
    private readonly TextBox _txtHost = new();
    private readonly NumericUpDown _numPort = new();
    private readonly TextBox _txtName = new();
    private readonly ComboBox _cboPresets = new();
    private readonly Label _lblStatus = new();
    private readonly Button _btnConnect = new();
    private readonly Button _btnCancel = new();

    public string ServerHost => _txtHost.Text.Trim();
    public int ServerPort => (int)_numPort.Value;
    public string PlayerName => _txtName.Text.Trim();
    public string StatusMessage => _lblStatus.Text;

    private readonly ConnectionService? _connectionService;

    public ConnectForm(ConnectionService? connectionService = null, string defaultHost = "127.0.0.1", int defaultPort = 5000, string defaultName = "Player1")
    {
        _connectionService = connectionService;

        // Nếu người dùng không chỉ định thông số riêng, tự nạp cấu hình gần nhất đã lưu
        if (defaultHost == "127.0.0.1" && defaultPort == 5000 && defaultName == "Player1")
        {
            var savedConfig = ConnectionService.LoadConfig();
            defaultHost = savedConfig.LastHost;
            defaultPort = savedConfig.LastPort;
            defaultName = savedConfig.LastPlayerName;
        }

        InitializeComponent(defaultHost, defaultPort, defaultName);
    }

    private void InitializeComponent(string defaultHost, int defaultPort, string defaultName)
    {
        Text = "Cấu Hình & Kết Nối Server — Caro Online";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 360);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Color.FromArgb(245, 247, 250);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            BackColor = Color.FromArgb(24, 76, 120),
            Padding = new Padding(15, 12, 15, 10)
        };

        var lblTitle = new Label
        {
            Text = "Cấu hình kết nối Máy chủ Caro",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(15, 10)
        };

        var lblSubtitle = new Label
        {
            Text = "Chọn cấu hình mẫu hoặc nhập IP, cổng và tên người chơi để kết nối",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(210, 230, 250),
            AutoSize = true,
            Location = new Point(16, 36)
        };

        pnlHeader.Controls.AddRange([lblTitle, lblSubtitle]);

        // Preset Selector (Gợi ý cấu hình nhanh - Tuần 4)
        var lblPreset = new Label
        {
            Text = "Cấu hình mẫu:",
            Location = new Point(25, 82),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(50, 50, 50)
        };

        _cboPresets.Location = new Point(160, 78);
        _cboPresets.Size = new Size(265, 26);
        _cboPresets.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboPresets.Items.AddRange([
            "Tự nhập cấu hình...",
            "Máy chủ cục bộ (127.0.0.1 : 5000)",
            "Mạng nội bộ LAN (0.0.0.0 : 5000)"
        ]);
        _cboPresets.SelectedIndex = 0;
        _cboPresets.SelectedIndexChanged += OnPresetChanged;

        // Host IP
        var lblHost = new Label
        {
            Text = "Địa chỉ IP / Host:",
            Location = new Point(25, 122),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 40, 40)
        };
        _txtHost.Location = new Point(160, 119);
        _txtHost.Size = new Size(265, 26);
        _txtHost.Text = defaultHost;

        // Port
        var lblPort = new Label
        {
            Text = "Cổng (Port):",
            Location = new Point(25, 162),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 40, 40)
        };
        _numPort.Location = new Point(160, 159);
        _numPort.Size = new Size(130, 26);
        _numPort.Minimum = 1;
        _numPort.Maximum = 65535;
        _numPort.Value = defaultPort is >= 1 and <= 65535 ? defaultPort : 5000;

        // Player Name
        var lblName = new Label
        {
            Text = "Tên người chơi:",
            Location = new Point(25, 202),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 40, 40)
        };
        _txtName.Location = new Point(160, 199);
        _txtName.Size = new Size(265, 26);
        _txtName.Text = defaultName;
        _txtName.MaxLength = 20;

        // Status / Error message
        _lblStatus.Location = new Point(25, 238);
        _lblStatus.Size = new Size(400, 42);
        _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
        _lblStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
        _lblStatus.Text = "";

        // Action Buttons
        _btnConnect.Text = "Kết nối";
        _btnConnect.Location = new Point(205, 295);
        _btnConnect.Size = new Size(115, 38);
        _btnConnect.BackColor = Color.FromArgb(24, 119, 242);
        _btnConnect.ForeColor = Color.White;
        _btnConnect.FlatStyle = FlatStyle.Flat;
        _btnConnect.FlatAppearance.BorderSize = 0;
        _btnConnect.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _btnConnect.Cursor = Cursors.Hand;
        _btnConnect.Click += OnConnectClicked;

        _btnCancel.Text = "Hủy bỏ";
        _btnCancel.Location = new Point(330, 295);
        _btnCancel.Size = new Size(95, 38);
        _btnCancel.BackColor = Color.FromArgb(220, 224, 230);
        _btnCancel.ForeColor = Color.FromArgb(50, 50, 50);
        _btnCancel.FlatStyle = FlatStyle.Flat;
        _btnCancel.FlatAppearance.BorderSize = 0;
        _btnCancel.DialogResult = DialogResult.Cancel;
        _btnCancel.Cursor = Cursors.Hand;

        AcceptButton = _btnConnect;
        CancelButton = _btnCancel;

        Controls.AddRange([
            _btnCancel, _btnConnect, _lblStatus,
            _txtName, lblName, _numPort, lblPort, _txtHost, lblHost,
            _cboPresets, lblPreset, pnlHeader
        ]);
    }

    private void OnPresetChanged(object? sender, EventArgs e)
    {
        switch (_cboPresets.SelectedIndex)
        {
            case 1: // Localhost
                _txtHost.Text = "127.0.0.1";
                _numPort.Value = 5000;
                break;
            case 2: // LAN
                _txtHost.Text = "0.0.0.0";
                _numPort.Value = 5000;
                break;
        }
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

        string cleanName = _txtName.Text.Trim();
        if (cleanName.Length > 20)
        {
            _lblStatus.Text = "Tên người chơi không được vượt quá 20 ký tự.";
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
                cleanName
            );

            if (!success)
            {
                SetConnectingState(false);
                _lblStatus.ForeColor = Color.FromArgb(200, 30, 30);
                string detail = string.IsNullOrWhiteSpace(_connectionService.LastError)
                    ? "Không thể kết nối tới server. Vui lòng kiểm tra IP/cổng hoặc đảm bảo Server đang chạy."
                    : $"Lỗi: {_connectionService.LastError}";
                _lblStatus.Text = detail;
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
        _cboPresets.Enabled = !isConnecting;
        _txtHost.Enabled = !isConnecting;
        _numPort.Enabled = !isConnecting;
        _txtName.Enabled = !isConnecting;
        _btnConnect.Text = isConnecting ? "Đang kết nối..." : "Kết nối";
    }

    #region Helper Methods for Unit Testing

    public void SetHost(string host) => _txtHost.Text = host;

    public void SetPort(int port)
    {
        if (port < 1) _numPort.Value = 1;
        else if (port > 65535) _numPort.Value = 65535;
        else _numPort.Value = port;
    }

    public void SetPlayerName(string name) => _txtName.Text = name;

    public void TriggerConnect() => OnConnectClicked(_btnConnect, EventArgs.Empty);

    #endregion
}
