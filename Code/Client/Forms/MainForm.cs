using System.Drawing;
using System.Windows.Forms;
using Caro.Client.Services;
using Caro.Shared.Messages;

namespace Caro.Client.Forms;

/// <summary>
/// Giao diện chính của Caro Client - Trách nhiệm TV3:
/// Quản lý cấu hình IP/Port, kết nối/ngắt kết nối tới Server, đo độ trễ Ping/Pong, hiển thị trạng thái và bảng Log giao thức.
/// </summary>
public sealed class MainForm : Form
{
    private readonly ConnectionService _connectionService;

    // Controls trên thanh kết nối
    private readonly TextBox _txtHost = new();
    private readonly NumericUpDown _numPort = new();
    private readonly TextBox _txtName = new();
    private readonly Button _btnConnect = new();
    private readonly Button _btnDisconnect = new();
    private readonly Button _btnPing = new();
    private readonly Button _btnOpenConnectDialog = new();

    // Controls trạng thái
    private readonly Panel _pnlStatusIndicator = new();
    private readonly Label _lblStatusText = new();

    // Controls tương tác phòng / game (Sẵn sàng kết nối với TV4 & TV5)
    private readonly GroupBox _grpActions = new();
    private readonly Button _btnCreateRoom = new();
    private readonly Button _btnJoinRoom = new();
    private readonly TextBox _txtRoomId = new();
    private readonly Label _lblRoomHint = new();

    // Controls bảng log giao thức mạng
    private readonly RichTextBox _rtbLogs = new();
    private readonly Button _btnClearLogs = new();
    private readonly CheckBox _chkAutoScroll = new();

    // Thanh trạng thái dưới cùng (StatusStrip)
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ToolStripStatusLabel _pingLabel = new();
    private readonly ToolStripStatusLabel _playerLabel = new();

    public MainForm()
    {
        _connectionService = new ConnectionService();

        InitializeComponent();
        RegisterEvents();
        UpdateUIState(false);
    }

    private void InitializeComponent()
    {
        Text = "Caro Online Client — v1.0 [TCP Network]";
        Width = 960;
        Height = 680;
        MinimumSize = new Size(800, 550);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        BackColor = Color.FromArgb(240, 242, 245);

        // 1. Thanh kết nối phía trên (Top Panel)
        var pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 115,
            BackColor = Color.White,
            Padding = new Padding(12)
        };

        var lblAppTitle = new Label
        {
            Text = "CARO ONLINE — BẢNG ĐIỀU KHIỂN KẾT NỐI",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 76, 120),
            AutoSize = true,
            Location = new Point(14, 10)
        };

        // Panel trạng thái (Status badge)
        _pnlStatusIndicator.Size = new Size(14, 14);
        _pnlStatusIndicator.Location = new Point(480, 14);
        _pnlStatusIndicator.BackColor = Color.FromArgb(220, 53, 69); // Đỏ mặc định

        _lblStatusText.Text = "Chưa kết nối";
        _lblStatusText.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _lblStatusText.ForeColor = Color.FromArgb(100, 100, 100);
        _lblStatusText.AutoSize = true;
        _lblStatusText.Location = new Point(500, 12);

        // Dòng nhập liệu IP, Port, Tên
        var lblHost = new Label { Text = "IP / Host:", AutoSize = true, Location = new Point(14, 45), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtHost.Text = "127.0.0.1";
        _txtHost.Size = new Size(130, 25);
        _txtHost.Location = new Point(80, 42);

        var lblPort = new Label { Text = "Port:", AutoSize = true, Location = new Point(220, 45), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _numPort.Minimum = 1;
        _numPort.Maximum = 65535;
        _numPort.Value = 8888;
        _numPort.Size = new Size(80, 25);
        _numPort.Location = new Point(260, 42);

        var lblName = new Label { Text = "Tên:", AutoSize = true, Location = new Point(350, 45), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtName.Text = "Player_" + Random.Shared.Next(100, 999);
        _txtName.Size = new Size(110, 25);
        _txtName.Location = new Point(385, 42);

        // Các nút bấm kết nối
        _btnConnect.Text = "Kết nối";
        _btnConnect.Location = new Point(510, 40);
        _btnConnect.Size = new Size(95, 30);
        _btnConnect.BackColor = Color.FromArgb(13, 110, 253);
        _btnConnect.ForeColor = Color.White;
        _btnConnect.FlatStyle = FlatStyle.Flat;
        _btnConnect.FlatAppearance.BorderSize = 0;
        _btnConnect.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _btnConnect.Cursor = Cursors.Hand;
        _btnConnect.Click += OnConnectClicked;

        _btnDisconnect.Text = "Ngắt kết nối";
        _btnDisconnect.Location = new Point(615, 40);
        _btnDisconnect.Size = new Size(100, 30);
        _btnDisconnect.BackColor = Color.FromArgb(220, 53, 69);
        _btnDisconnect.ForeColor = Color.White;
        _btnDisconnect.FlatStyle = FlatStyle.Flat;
        _btnDisconnect.FlatAppearance.BorderSize = 0;
        _btnDisconnect.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _btnDisconnect.Cursor = Cursors.Hand;
        _btnDisconnect.Click += OnDisconnectClicked;

        _btnPing.Text = "Gửi Ping";
        _btnPing.Location = new Point(725, 40);
        _btnPing.Size = new Size(85, 30);
        _btnPing.BackColor = Color.FromArgb(108, 117, 125);
        _btnPing.ForeColor = Color.White;
        _btnPing.FlatStyle = FlatStyle.Flat;
        _btnPing.FlatAppearance.BorderSize = 0;
        _btnPing.Cursor = Cursors.Hand;
        _btnPing.Click += OnPingClicked;

        _btnOpenConnectDialog.Text = "Cửa sổ kết nối...";
        _btnOpenConnectDialog.Location = new Point(820, 40);
        _btnOpenConnectDialog.Size = new Size(115, 30);
        _btnOpenConnectDialog.BackColor = Color.FromArgb(230, 235, 240);
        _btnOpenConnectDialog.FlatStyle = FlatStyle.Flat;
        _btnOpenConnectDialog.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 205);
        _btnOpenConnectDialog.Cursor = Cursors.Hand;
        _btnOpenConnectDialog.Click += OnOpenConnectDialogClicked;

        pnlTop.Controls.AddRange([
            lblAppTitle, _pnlStatusIndicator, _lblStatusText,
            lblHost, _txtHost, lblPort, _numPort, lblName, _txtName,
            _btnConnect, _btnDisconnect, _btnPing, _btnOpenConnectDialog
        ]);

        // 2. Khu vực chính giữa (Chia làm 2 phần: Chức năng phòng & Log giao thức)
        var pnlMain = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12)
        };

        // Group phòng chơi (Tích hợp cùng TV4/TV5)
        _grpActions.Text = "Sảnh & Phòng đấu (Lobby / Rooms)";
        _grpActions.Dock = DockStyle.Top;
        _grpActions.Height = 85;
        _grpActions.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

        _btnCreateRoom.Text = "Tạo phòng mới";
        _btnCreateRoom.Location = new Point(15, 30);
        _btnCreateRoom.Size = new Size(130, 34);
        _btnCreateRoom.BackColor = Color.FromArgb(25, 135, 84);
        _btnCreateRoom.ForeColor = Color.White;
        _btnCreateRoom.FlatStyle = FlatStyle.Flat;
        _btnCreateRoom.FlatAppearance.BorderSize = 0;
        _btnCreateRoom.Cursor = Cursors.Hand;
        _btnCreateRoom.Click += OnCreateRoomClicked;

        var lblRoom = new Label { Text = "Mã phòng:", Location = new Point(170, 37), AutoSize = true };
        _txtRoomId.Location = new Point(245, 34);
        _txtRoomId.Size = new Size(110, 25);
        _txtRoomId.PlaceholderText = "VD: 101";

        _btnJoinRoom.Text = "Vào phòng";
        _btnJoinRoom.Location = new Point(365, 30);
        _btnJoinRoom.Size = new Size(100, 34);
        _btnJoinRoom.BackColor = Color.FromArgb(13, 110, 253);
        _btnJoinRoom.ForeColor = Color.White;
        _btnJoinRoom.FlatStyle = FlatStyle.Flat;
        _btnJoinRoom.FlatAppearance.BorderSize = 0;
        _btnJoinRoom.Cursor = Cursors.Hand;
        _btnJoinRoom.Click += OnJoinRoomClicked;

        _lblRoomHint.Text = "(Sẵn sàng chuyển tiếp cho TV4/TV5 khi hoàn tất logic phòng)";
        _lblRoomHint.ForeColor = Color.Gray;
        _lblRoomHint.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
        _lblRoomHint.AutoSize = true;
        _lblRoomHint.Location = new Point(480, 39);

        _grpActions.Controls.AddRange([_btnCreateRoom, lblRoom, _txtRoomId, _btnJoinRoom, _lblRoomHint]);

        // Group hiển thị Log mạng & giao thức
        var grpLogs = new GroupBox
        {
            Text = "Nhật ký truyền thông mạng (Protocol & Network Logs)",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Padding = new Padding(10)
        };

        var pnlLogHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 35
        };

        _btnClearLogs.Text = "Xóa nhật ký";
        _btnClearLogs.Location = new Point(5, 4);
        _btnClearLogs.Size = new Size(95, 26);
        _btnClearLogs.Click += (_, _) => _rtbLogs.Clear();

        _chkAutoScroll.Text = "Tự động cuộn xuống cuối";
        _chkAutoScroll.Checked = true;
        _chkAutoScroll.AutoSize = true;
        _chkAutoScroll.Location = new Point(115, 8);
        _chkAutoScroll.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);

        pnlLogHeader.Controls.AddRange([_btnClearLogs, _chkAutoScroll]);

        _rtbLogs.Dock = DockStyle.Fill;
        _rtbLogs.ReadOnly = true;
        _rtbLogs.BackColor = Color.FromArgb(30, 30, 30);
        _rtbLogs.ForeColor = Color.FromArgb(220, 220, 220);
        _rtbLogs.Font = new Font("Consolas", 9.5f, FontStyle.Regular);

        grpLogs.Controls.Add(_rtbLogs);
        grpLogs.Controls.Add(pnlLogHeader);

        pnlMain.Controls.Add(grpLogs);
        pnlMain.Controls.Add(_grpActions);

        // 3. StatusStrip phía dưới
        _statusLabel.Text = "Trạng thái: Chưa kết nối";
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        _pingLabel.Text = "Ping: -- ms";
        _pingLabel.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _pingLabel.Padding = new Padding(10, 0, 10, 0);

        _playerLabel.Text = "Người chơi: " + _txtName.Text;
        _playerLabel.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _playerLabel.Padding = new Padding(10, 0, 10, 0);

        _statusStrip.Items.AddRange([_statusLabel, _pingLabel, _playerLabel]);

        // Ghép vào Form
        Controls.Add(pnlMain);
        Controls.Add(pnlTop);
        Controls.Add(_statusStrip);
    }

    private void RegisterEvents()
    {
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _connectionService.LogGenerated += OnLogGenerated;
        _connectionService.PingUpdated += OnPingUpdated;
        _connectionService.MessageReceived += OnMessageReceived;

        FormClosing += (_, _) =>
        {
            _connectionService.Dispose();
        };
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        string host = _txtHost.Text.Trim();
        int port = (int)_numPort.Value;
        string name = _txtName.Text.Trim();

        _btnConnect.Enabled = false;
        _btnConnect.Text = "Đang kết nối...";

        bool success = await _connectionService.ConnectAsync(host, port, name);
        if (!success)
        {
            _btnConnect.Enabled = true;
            _btnConnect.Text = "Kết nối";
        }
    }

    private async void OnDisconnectClicked(object? sender, EventArgs e)
    {
        await _connectionService.DisconnectAsync();
    }

    private async void OnPingClicked(object? sender, EventArgs e)
    {
        if (_connectionService.IsConnected)
        {
            await _connectionService.SendPingAsync();
        }
    }

    private void OnOpenConnectDialogClicked(object? sender, EventArgs e)
    {
        using var dlg = new ConnectForm(_connectionService, _txtHost.Text, (int)_numPort.Value, _txtName.Text);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _txtHost.Text = dlg.ServerHost;
            _numPort.Value = dlg.ServerPort;
            _txtName.Text = dlg.PlayerName;
        }
    }

    private async void OnCreateRoomClicked(object? sender, EventArgs e)
    {
        if (!_connectionService.IsConnected) return;

        var createRoomMsg = new NetworkMessage
        {
            Type = MessageTypes.CreateRoom,
            Payload = new { HostName = _connectionService.PlayerName }
        };
        await _connectionService.SendMessageAsync(createRoomMsg);
    }

    private async void OnJoinRoomClicked(object? sender, EventArgs e)
    {
        if (!_connectionService.IsConnected) return;

        string roomId = _txtRoomId.Text.Trim();
        if (string.IsNullOrWhiteSpace(roomId))
        {
            MessageBox.Show("Vui lòng nhập mã phòng cần tham gia.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var joinRoomMsg = new NetworkMessage
        {
            Type = MessageTypes.JoinRoom,
            Payload = new { RoomId = roomId, PlayerName = _connectionService.PlayerName }
        };
        await _connectionService.SendMessageAsync(joinRoomMsg);
    }

    private void OnConnectionStateChanged(bool isConnected, string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnConnectionStateChanged(isConnected, status));
            return;
        }

        UpdateUIState(isConnected);
        _lblStatusText.Text = status;
        _statusLabel.Text = $"Trạng thái: {status}";
    }

    private void OnPingUpdated(long latency)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnPingUpdated(latency));
            return;
        }

        _pingLabel.Text = $"Ping: {latency} ms";
    }

    private void OnMessageReceived(NetworkMessage message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnMessageReceived(message));
            return;
        }

        // Thông báo phản hồi nếu nhận WELCOME
        if (message.Type == MessageTypes.Welcome)
        {
            AppendLog($"[HỆ THỐNG] Máy chủ đã chào đón người chơi {_connectionService.PlayerName}!", Color.FromArgb(75, 181, 67));
        }
    }

    private void OnLogGenerated(string logText, bool isError)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnLogGenerated(logText, isError));
            return;
        }

        Color color = isError ? Color.FromArgb(255, 99, 71) : Color.FromArgb(200, 230, 255);
        AppendLog(logText, color);
    }

    private void AppendLog(string text, Color color)
    {
        _rtbLogs.SelectionStart = _rtbLogs.TextLength;
        _rtbLogs.SelectionLength = 0;
        _rtbLogs.SelectionColor = color;
        _rtbLogs.AppendText(text + Environment.NewLine);
        _rtbLogs.SelectionColor = _rtbLogs.ForeColor;

        if (_chkAutoScroll.Checked)
        {
            _rtbLogs.ScrollToCaret();
        }
    }

    private void UpdateUIState(bool isConnected)
    {
        _btnConnect.Enabled = !isConnected;
        _btnConnect.Text = "Kết nối";
        _btnDisconnect.Enabled = isConnected;
        _btnPing.Enabled = isConnected;
        _txtHost.Enabled = !isConnected;
        _numPort.Enabled = !isConnected;
        _txtName.Enabled = !isConnected;

        _pnlStatusIndicator.BackColor = isConnected ? Color.FromArgb(40, 167, 69) : Color.FromArgb(220, 53, 69);
        _grpActions.Enabled = isConnected;

        _playerLabel.Text = "Người chơi: " + _txtName.Text;
        if (!isConnected)
        {
            _pingLabel.Text = "Ping: -- ms";
        }
    }
}
