using System.Drawing;
using System.Windows.Forms;
using Caro.Client.Services;

namespace Caro.Client.Forms;

/// <summary>
/// Giao diện Sảnh chờ & Phòng đấu (Lobby & Room Waiting Form) - Nhiệm vụ TV3:
/// Cho phép tạo phòng, tham gia phòng theo mã, hiển thị thông tin 2 đối thủ (X/O),
/// trò chuyện (chat) trong phòng và bắt đầu ván đấu.
/// </summary>
public sealed class LobbyForm : Form
{
    private readonly ConnectionService _connectionService;

    // View panels: Chuyển đổi giữa chế độ Sảnh (Lobby) và Phòng chờ (Room)
    private readonly Panel _pnlHeader = new();
    private readonly Panel _pnlLobbyView = new();
    private readonly Panel _pnlRoomView = new();

    // Controls trong Header
    private readonly Label _lblTitle = new();
    private readonly Label _lblPlayerBadge = new();

    // Controls chế độ Sảnh (Lobby Mode)
    private readonly TextBox _txtCreateRoomName = new();
    private readonly Button _btnCreateRoom = new();
    private readonly TextBox _txtJoinRoomId = new();
    private readonly Button _btnJoinRoom = new();
    private readonly Label _lblLobbyStatus = new();

    // Controls chế độ Phòng chờ (Room Mode)
    private readonly Label _lblRoomNumber = new();
    private readonly Button _btnCopyRoomId = new();
    private readonly Label _lblRoomName = new();
    private readonly Panel _pnlPlayerX = new();
    private readonly Panel _pnlPlayerO = new();
    private readonly Label _lblPlayerXName = new();
    private readonly Label _lblPlayerOName = new();
    private readonly Label _lblPlayerXRole = new();
    private readonly Label _lblPlayerORole = new();
    private readonly Label _lblMatchStatus = new();
    private readonly Button _btnStartGame = new();
    private readonly Button _btnLeaveRoom = new();

    // Controls khung Chat phòng
    private readonly RichTextBox _rtbChat = new();
    private readonly TextBox _txtChatMessage = new();
    private readonly Button _btnSendChat = new();

    public LobbyForm(ConnectionService connectionService)
    {
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));

        InitializeComponent();
        RegisterEvents();
        UpdateViewMode();
    }

    private void InitializeComponent()
    {
        Text = "Sảnh Chờ & Phòng Thi Đấu — Caro Online";
        Width = 860;
        Height = 650;
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Color.FromArgb(243, 244, 246);

        // 1. Header Bar
        _pnlHeader.Dock = DockStyle.Top;
        _pnlHeader.Height = 60;
        _pnlHeader.BackColor = Color.FromArgb(24, 76, 120);
        _pnlHeader.Padding = new Padding(15, 12, 15, 12);

        _lblTitle.Text = "SẢNH PHÒNG CHƠI CARO";
        _lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _lblTitle.ForeColor = Color.White;
        _lblTitle.AutoSize = true;
        _lblTitle.Location = new Point(15, 17);

        _lblPlayerBadge.Text = $"Người chơi: {_connectionService.PlayerName}";
        _lblPlayerBadge.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _lblPlayerBadge.ForeColor = Color.FromArgb(220, 240, 255);
        _lblPlayerBadge.AutoSize = true;
        _lblPlayerBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _lblPlayerBadge.Location = new Point(Width - 250, 20);

        _pnlHeader.Controls.Add(_lblTitle);
        _pnlHeader.Controls.Add(_lblPlayerBadge);

        // 2. Chế độ Sảnh (Lobby View Panel)
        InitializeLobbyView();

        // 3. Chế độ Phòng chờ (Room View Panel)
        InitializeRoomView();

        Controls.Add(_pnlRoomView);
        Controls.Add(_pnlLobbyView);
        Controls.Add(_pnlHeader);
    }

    private void InitializeLobbyView()
    {
        _pnlLobbyView.Dock = DockStyle.Fill;
        _pnlLobbyView.Padding = new Padding(30, 25, 30, 25);

        var grpCreate = new GroupBox
        {
            Text = "Tạo Phòng Mới",
            Location = new Point(30, 25),
            Size = new Size(370, 210),
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(15)
        };

        var lblRoomNameHint = new Label
        {
            Text = "Tên phòng hiển thị:",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Location = new Point(15, 35),
            AutoSize = true
        };

        _txtCreateRoomName.Location = new Point(15, 60);
        _txtCreateRoomName.Size = new Size(335, 26);
        _txtCreateRoomName.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        _txtCreateRoomName.Text = $"Phòng của {_connectionService.PlayerName}";

        _btnCreateRoom.Text = "Tạo Phòng Ngay";
        _btnCreateRoom.Location = new Point(15, 105);
        _btnCreateRoom.Size = new Size(335, 42);
        _btnCreateRoom.BackColor = Color.FromArgb(25, 135, 84);
        _btnCreateRoom.ForeColor = Color.White;
        _btnCreateRoom.FlatStyle = FlatStyle.Flat;
        _btnCreateRoom.FlatAppearance.BorderSize = 0;
        _btnCreateRoom.Cursor = Cursors.Hand;
        _btnCreateRoom.Click += OnCreateRoomClicked;

        var lblCreateHint = new Label
        {
            Text = "* Bạn sẽ làm Chủ phòng (Quân X) và chờ đối thủ vào.",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(15, 155),
            AutoSize = true
        };

        grpCreate.Controls.AddRange([lblRoomNameHint, _txtCreateRoomName, _btnCreateRoom, lblCreateHint]);

        // Group Vào phòng bằng mã
        var grpJoin = new GroupBox
        {
            Text = "Tham Gia Phòng Có Sẵn",
            Location = new Point(430, 25),
            Size = new Size(370, 210),
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(15)
        };

        var lblJoinHint = new Label
        {
            Text = "Nhập Mã phòng (Room ID):",
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Location = new Point(15, 35),
            AutoSize = true
        };

        _txtJoinRoomId.Location = new Point(15, 60);
        _txtJoinRoomId.Size = new Size(335, 26);
        _txtJoinRoomId.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _txtJoinRoomId.PlaceholderText = "Ví dụ: 101, 8888, ...";

        _btnJoinRoom.Text = "Vào Phòng";
        _btnJoinRoom.Location = new Point(15, 105);
        _btnJoinRoom.Size = new Size(335, 42);
        _btnJoinRoom.BackColor = Color.FromArgb(13, 110, 253);
        _btnJoinRoom.ForeColor = Color.White;
        _btnJoinRoom.FlatStyle = FlatStyle.Flat;
        _btnJoinRoom.FlatAppearance.BorderSize = 0;
        _btnJoinRoom.Cursor = Cursors.Hand;
        _btnJoinRoom.Click += OnJoinRoomClicked;

        var lblJoinNote = new Label
        {
            Text = "* Người vào phòng sau sẽ là Khách (Quân O).",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(15, 155),
            AutoSize = true
        };

        grpJoin.Controls.AddRange([lblJoinHint, _txtJoinRoomId, _btnJoinRoom, lblJoinNote]);

        // Banner thông tin sảnh
        var pnlInfo = new Panel
        {
            Location = new Point(30, 260),
            Size = new Size(770, 220),
            BackColor = Color.White,
            Padding = new Padding(20)
        };

        var lblInfoTitle = new Label
        {
            Text = "HƯỚNG DẪN THAM GIA TRẬN ĐẤU (TUẦN 2)",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 76, 120),
            AutoSize = true,
            Location = new Point(20, 15)
        };

        var lblInfoContent = new Label
        {
            Text = "1. Bấm 'Tạo Phòng Ngay' để tự tạo phòng mới, sau đó gửi Mã phòng cho bạn bè.\n" +
                   "2. Hoặc người chơi thứ hai nhập Mã phòng ở ô bên phải để kết nối vào cùng phòng.\n" +
                   "3. Khi đủ 2 người, Chủ phòng nhấn 'Bắt đầu ván đấu' để vào bàn cờ Caro 15x15.\n" +
                   "4. Quân X đi trước, quân O đi sau; hai bên nhìn thấy nước đi của nhau theo thời gian thực.",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(51, 65, 85),
            Location = new Point(20, 48),
            Size = new Size(720, 100)
        };

        _lblLobbyStatus.Location = new Point(20, 160);
        _lblLobbyStatus.AutoSize = true;
        _lblLobbyStatus.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _lblLobbyStatus.ForeColor = Color.FromArgb(25, 135, 84);
        _lblLobbyStatus.Text = "Hệ thống sẵn sàng: Đã kết nối máy chủ!";

        pnlInfo.Controls.AddRange([lblInfoTitle, lblInfoContent, _lblLobbyStatus]);

        _pnlLobbyView.Controls.AddRange([grpCreate, grpJoin, pnlInfo]);
    }

    private void InitializeRoomView()
    {
        _pnlRoomView.Dock = DockStyle.Fill;
        _pnlRoomView.Padding = new Padding(20);

        // Header thông tin phòng
        var pnlRoomHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.White,
            Padding = new Padding(15, 10, 15, 10)
        };

        _lblRoomNumber.Text = "MÃ PHÒNG: #----";
        _lblRoomNumber.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        _lblRoomNumber.ForeColor = Color.FromArgb(24, 76, 120);
        _lblRoomNumber.AutoSize = true;
        _lblRoomNumber.Location = new Point(15, 10);

        _btnCopyRoomId.Text = "Sao chép mã";
        _btnCopyRoomId.Size = new Size(100, 28);
        _btnCopyRoomId.Location = new Point(240, 11);
        _btnCopyRoomId.BackColor = Color.FromArgb(230, 235, 245);
        _btnCopyRoomId.FlatStyle = FlatStyle.Flat;
        _btnCopyRoomId.FlatAppearance.BorderSize = 0;
        _btnCopyRoomId.Cursor = Cursors.Hand;
        _btnCopyRoomId.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _btnCopyRoomId.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_connectionService.CurrentRoom?.RoomId))
            {
                Clipboard.SetText(_connectionService.CurrentRoom.RoomId);
                MessageBox.Show($"Đã sao chép mã phòng: {_connectionService.CurrentRoom.RoomId}", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        _lblRoomName.Text = "Tên phòng: ---";
        _lblRoomName.Font = new Font("Segoe UI", 9f, FontStyle.Italic);
        _lblRoomName.ForeColor = Color.FromArgb(100, 116, 139);
        _lblRoomName.AutoSize = true;
        _lblRoomName.Location = new Point(16, 40);

        _btnLeaveRoom.Text = "Rời Phòng";
        _btnLeaveRoom.Size = new Size(110, 36);
        _btnLeaveRoom.Location = new Point(680, 16);
        _btnLeaveRoom.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnLeaveRoom.BackColor = Color.FromArgb(220, 53, 69);
        _btnLeaveRoom.ForeColor = Color.White;
        _btnLeaveRoom.FlatStyle = FlatStyle.Flat;
        _btnLeaveRoom.FlatAppearance.BorderSize = 0;
        _btnLeaveRoom.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _btnLeaveRoom.Cursor = Cursors.Hand;
        _btnLeaveRoom.Click += OnLeaveRoomClicked;

        pnlRoomHeader.Controls.AddRange([_lblRoomNumber, _btnCopyRoomId, _lblRoomName, _btnLeaveRoom]);

        // Panel Body: Chia làm 2 cột: Cột trái (Người chơi & Trạng thái), Cột phải (Khung Chat)
        var pnlRoomBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 15, 0, 0)
        };

        // Cột trái: Thông tin 2 người chơi
        var pnlPlayers = new Panel
        {
            Location = new Point(0, 15),
            Size = new Size(460, 450),
            BackColor = Color.Transparent
        };

        // Card Người chơi X (Chủ phòng)
        _pnlPlayerX.Location = new Point(0, 0);
        _pnlPlayerX.Size = new Size(460, 100);
        _pnlPlayerX.BackColor = Color.White;
        _pnlPlayerX.Padding = new Padding(15);

        var lblIconX = new Label
        {
            Text = "X",
            Font = new Font("Segoe UI", 24f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 53, 69),
            Size = new Size(50, 60),
            Location = new Point(15, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lblPlayerXName.Text = "Người chơi X (Host)";
        _lblPlayerXName.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _lblPlayerXName.ForeColor = Color.FromArgb(30, 41, 59);
        _lblPlayerXName.Location = new Point(75, 22);
        _lblPlayerXName.AutoSize = true;

        _lblPlayerXRole.Text = "Chủ phòng • Quân X (Đi trước)";
        _lblPlayerXRole.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _lblPlayerXRole.ForeColor = Color.FromArgb(100, 116, 139);
        _lblPlayerXRole.Location = new Point(76, 50);
        _lblPlayerXRole.AutoSize = true;

        _pnlPlayerX.Controls.AddRange([lblIconX, _lblPlayerXName, _lblPlayerXRole]);

        // Card Người chơi O (Khách)
        _pnlPlayerO.Location = new Point(0, 115);
        _pnlPlayerO.Size = new Size(460, 100);
        _pnlPlayerO.BackColor = Color.White;
        _pnlPlayerO.Padding = new Padding(15);

        var lblIconO = new Label
        {
            Text = "O",
            Font = new Font("Segoe UI", 24f, FontStyle.Bold),
            ForeColor = Color.FromArgb(13, 110, 253),
            Size = new Size(50, 60),
            Location = new Point(15, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lblPlayerOName.Text = "Đang chờ đối thủ vào...";
        _lblPlayerOName.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _lblPlayerOName.ForeColor = Color.FromArgb(148, 163, 184);
        _lblPlayerOName.Location = new Point(75, 22);
        _lblPlayerOName.AutoSize = true;

        _lblPlayerORole.Text = "Khách tham gia • Quân O (Đi sau)";
        _lblPlayerORole.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _lblPlayerORole.ForeColor = Color.FromArgb(100, 116, 139);
        _lblPlayerORole.Location = new Point(76, 50);
        _lblPlayerORole.AutoSize = true;

        _pnlPlayerO.Controls.AddRange([lblIconO, _lblPlayerOName, _lblPlayerORole]);

        // Panel trạng thái ghép phòng
        var pnlMatch = new Panel
        {
            Location = new Point(0, 230),
            Size = new Size(460, 140),
            BackColor = Color.White,
            Padding = new Padding(15)
        };

        _lblMatchStatus.Text = "Trạng thái: Đang chờ người chơi thứ 2 tham gia phòng...";
        _lblMatchStatus.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _lblMatchStatus.ForeColor = Color.FromArgb(217, 119, 6);
        _lblMatchStatus.Location = new Point(15, 18);
        _lblMatchStatus.Size = new Size(430, 45);

        _btnStartGame.Text = "BẮT ĐẦU TRẬN ĐẤU";
        _btnStartGame.Location = new Point(15, 75);
        _btnStartGame.Size = new Size(430, 45);
        _btnStartGame.BackColor = Color.FromArgb(25, 135, 84);
        _btnStartGame.ForeColor = Color.White;
        _btnStartGame.FlatStyle = FlatStyle.Flat;
        _btnStartGame.FlatAppearance.BorderSize = 0;
        _btnStartGame.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        _btnStartGame.Cursor = Cursors.Hand;
        _btnStartGame.Enabled = false;
        _btnStartGame.Click += OnStartGameClicked;

        pnlMatch.Controls.AddRange([_lblMatchStatus, _btnStartGame]);

        pnlPlayers.Controls.AddRange([_pnlPlayerX, _pnlPlayerO, pnlMatch]);

        // Cột phải: Chat phòng thi đấu
        var grpChat = new GroupBox
        {
            Text = "Trò Chuyện Trong Phòng (Room Chat)",
            Location = new Point(480, 15),
            Size = new Size(335, 430),
            Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(10)
        };

        _rtbChat.Location = new Point(10, 25);
        _rtbChat.Size = new Size(315, 345);
        _rtbChat.ReadOnly = true;
        _rtbChat.BackColor = Color.FromArgb(248, 250, 252);
        _rtbChat.BorderStyle = BorderStyle.None;
        _rtbChat.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

        _txtChatMessage.Location = new Point(10, 385);
        _txtChatMessage.Size = new Size(230, 26);
        _txtChatMessage.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        _txtChatMessage.PlaceholderText = "Nhập tin nhắn...";
        _txtChatMessage.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _btnSendChat.PerformClick();
            }
        };

        _btnSendChat.Text = "Gửi";
        _btnSendChat.Location = new Point(245, 383);
        _btnSendChat.Size = new Size(80, 30);
        _btnSendChat.BackColor = Color.FromArgb(13, 110, 253);
        _btnSendChat.ForeColor = Color.White;
        _btnSendChat.FlatStyle = FlatStyle.Flat;
        _btnSendChat.FlatAppearance.BorderSize = 0;
        _btnSendChat.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _btnSendChat.Cursor = Cursors.Hand;
        _btnSendChat.Click += OnSendChatClicked;

        grpChat.Controls.AddRange([_rtbChat, _txtChatMessage, _btnSendChat]);

        pnlRoomBody.Controls.AddRange([pnlPlayers, grpChat]);

        _pnlRoomView.Controls.AddRange([pnlRoomBody, pnlRoomHeader]);
    }

    private void RegisterEvents()
    {
        _connectionService.RoomCreated += OnRoomCreated;
        _connectionService.RoomJoined += OnRoomJoined;
        _connectionService.JoinRoomFailed += OnJoinRoomFailed;
        _connectionService.RoomLeft += OnRoomLeft;
        _connectionService.PlayerJoinedRoom += OnPlayerJoinedRoom;
        _connectionService.PlayerLeftRoom += OnPlayerLeftRoom;
        _connectionService.ChatMessageReceived += OnChatMessageReceived;
        _connectionService.GameStarted += OnGameStarted;
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;

        FormClosing += async (s, e) =>
        {
            if (_connectionService.IsInRoom)
            {
                await _connectionService.LeaveRoomAsync();
            }
        };
    }

    private async void OnCreateRoomClicked(object? sender, EventArgs e)
    {
        _btnCreateRoom.Enabled = false;
        _btnCreateRoom.Text = "Đang tạo phòng...";
        _lblLobbyStatus.Text = "Đang gửi yêu cầu tạo phòng tới máy chủ...";

        bool success = await _connectionService.CreateRoomAsync(_txtCreateRoomName.Text.Trim());
        _btnCreateRoom.Enabled = true;
        _btnCreateRoom.Text = "Tạo Phòng Ngay";

        if (success)
        {
            UpdateViewMode();
        }
        else
        {
            _lblLobbyStatus.Text = "Không thể tạo phòng. Vui lòng kiểm tra kết nối.";
        }
    }

    private async void OnJoinRoomClicked(object? sender, EventArgs e)
    {
        string roomId = _txtJoinRoomId.Text.Trim();
        if (string.IsNullOrWhiteSpace(roomId))
        {
            MessageBox.Show("Vui lòng nhập Mã phòng cần tham gia.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtJoinRoomId.Focus();
            return;
        }

        _btnJoinRoom.Enabled = false;
        _btnJoinRoom.Text = "Đang vào...";
        _lblLobbyStatus.Text = $"Đang tham gia phòng #{roomId}...";

        bool success = await _connectionService.JoinRoomAsync(roomId);
        _btnJoinRoom.Enabled = true;
        _btnJoinRoom.Text = "Vào Phòng";

        if (success)
        {
            UpdateViewMode();
        }
    }

    private async void OnLeaveRoomClicked(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show("Bạn có chắc chắn muốn rời phòng này?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm == DialogResult.Yes)
        {
            await _connectionService.LeaveRoomAsync();
            UpdateViewMode();
        }
    }

    private async void OnStartGameClicked(object? sender, EventArgs e)
    {
        _btnStartGame.Enabled = false;
        _btnStartGame.Text = "Đang bắt đầu...";

        await _connectionService.StartGameAsync();
    }

    private async void OnSendChatClicked(object? sender, EventArgs e)
    {
        string text = _txtChatMessage.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _txtChatMessage.Clear();
        await _connectionService.SendChatMessageAsync(text);
    }

    #region Event Handlers from ConnectionService

    private void OnRoomCreated(RoomInfo room)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnRoomCreated(room));
            return;
        }

        UpdateViewMode();
        AppendChatSystem($"Bạn đã tạo phòng #{room.RoomId}. Đang đợi người chơi thứ hai...");
    }

    private void OnRoomJoined(RoomInfo room)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnRoomJoined(room));
            return;
        }

        UpdateViewMode();
        AppendChatSystem($"Bạn đã tham gia phòng #{room.RoomId} thành công!");
    }

    private void OnJoinRoomFailed(string reason)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnJoinRoomFailed(reason));
            return;
        }

        _lblLobbyStatus.Text = $"Lỗi: {reason}";
        MessageBox.Show($"Không thể tham gia phòng: {reason}", "Lỗi tham gia phòng", MessageBoxButtons.OK, MessageBoxIcon.Error);
        UpdateViewMode();
    }

    private void OnRoomLeft(string roomId)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnRoomLeft(roomId));
            return;
        }

        _rtbChat.Clear();
        UpdateViewMode();
        _lblLobbyStatus.Text = $"Đã rời khỏi phòng #{roomId}.";
    }

    private void OnPlayerJoinedRoom(string roomId, string guestName)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnPlayerJoinedRoom(roomId, guestName));
            return;
        }

        if (_connectionService.CurrentRoom != null)
        {
            _connectionService.CurrentRoom.GuestPlayer = guestName;
        }
        UpdateRoomDetails();
        AppendChatSystem($"Người chơi '{guestName}' đã tham gia phòng thi đấu!");
    }

    private void OnPlayerLeftRoom(string roomId, string playerName)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnPlayerLeftRoom(roomId, playerName));
            return;
        }

        if (_connectionService.CurrentRoom != null && _connectionService.CurrentRoom.GuestPlayer == playerName)
        {
            _connectionService.CurrentRoom.GuestPlayer = null;
        }
        UpdateRoomDetails();
        AppendChatSystem($"Người chơi '{playerName}' đã rời khỏi phòng.");
    }

    private void OnChatMessageReceived(string sender, string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnChatMessageReceived(sender, message));
            return;
        }

        bool isMe = (sender == _connectionService.PlayerName);
        Color color = isMe ? Color.FromArgb(13, 110, 253) : Color.FromArgb(25, 135, 84);
        string prefix = isMe ? "Bạn" : sender;

        _rtbChat.SelectionStart = _rtbChat.TextLength;
        _rtbChat.SelectionLength = 0;
        _rtbChat.SelectionColor = color;
        _rtbChat.SelectionFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        _rtbChat.AppendText($"[{prefix} - {DateTime.Now:HH:mm}]: ");

        _rtbChat.SelectionColor = Color.FromArgb(30, 41, 59);
        _rtbChat.SelectionFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        _rtbChat.AppendText(message + Environment.NewLine);
        _rtbChat.ScrollToCaret();
    }

    private void OnGameStarted(string roomId)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnGameStarted(roomId));
            return;
        }

        _lblMatchStatus.Text = "Trận đấu đang diễn ra!";
        _lblMatchStatus.ForeColor = Color.FromArgb(25, 135, 84);
        _btnStartGame.Text = "Trận Đấu Đã Bắt Đầu";
        _btnStartGame.Enabled = false;

        AppendChatSystem("Trận đấu chính thức bắt đầu! Chúc hai người chơi thi đấu tốt!");
        MessageBox.Show($"Ván cờ tại phòng #{roomId} đã chính thức bắt đầu!\n(Quân X đi trước, quân O đi sau)", "Bắt đầu trận đấu", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnConnectionStateChanged(bool isConnected, string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnConnectionStateChanged(isConnected, status));
            return;
        }

        if (!isConnected)
        {
            UpdateViewMode();
            _lblLobbyStatus.Text = "Mất kết nối với máy chủ!";
            _lblLobbyStatus.ForeColor = Color.FromArgb(220, 53, 69);
        }
    }

    #endregion

    private void UpdateViewMode()
    {
        bool inRoom = _connectionService.IsInRoom;
        _pnlLobbyView.Visible = !inRoom;
        _pnlRoomView.Visible = inRoom;

        if (inRoom)
        {
            UpdateRoomDetails();
        }
    }

    private void UpdateRoomDetails()
    {
        var room = _connectionService.CurrentRoom;
        if (room == null) return;

        _lblRoomNumber.Text = $"MÃ PHÒNG: #{room.RoomId}";
        _lblRoomName.Text = $"Tên phòng: {room.RoomName} • Bạn là: Quân {(room.IsHost ? "X (Chủ phòng)" : "O (Khách)")}";

        // Người chơi X
        _lblPlayerXName.Text = string.IsNullOrEmpty(room.HostPlayer) ? "Chủ phòng" : room.HostPlayer;
        if (room.HostPlayer == _connectionService.PlayerName)
        {
            _lblPlayerXName.Text += " (Bạn)";
        }

        // Người chơi O
        if (string.IsNullOrEmpty(room.GuestPlayer))
        {
            _lblPlayerOName.Text = "Đang chờ đối thủ vào...";
            _lblPlayerOName.ForeColor = Color.FromArgb(148, 163, 184);

            _lblMatchStatus.Text = "Trạng thái: Đang đợi người chơi thứ 2 tham gia...";
            _lblMatchStatus.ForeColor = Color.FromArgb(217, 119, 6);
            _btnStartGame.Enabled = false;
            _btnStartGame.Text = "Đang Đợi Người Chơi Thứ 2";
        }
        else
        {
            _lblPlayerOName.Text = room.GuestPlayer;
            if (room.GuestPlayer == _connectionService.PlayerName)
            {
                _lblPlayerOName.Text += " (Bạn)";
            }
            _lblPlayerOName.ForeColor = Color.FromArgb(30, 41, 59);

            _lblMatchStatus.Text = "Trạng thái: Đã đủ 2 người chơi! Phòng đã sẵn sàng!";
            _lblMatchStatus.ForeColor = Color.FromArgb(25, 135, 84);

            if (room.IsHost)
            {
                _btnStartGame.Enabled = true;
                _btnStartGame.Text = "BẮT ĐẦU TRẬN ĐẤU (CHỦ PHÒNG)";
            }
            else
            {
                _btnStartGame.Enabled = false;
                _btnStartGame.Text = "Đang Đợi Chủ Phòng Bắt Đầu...";
            }
        }
    }

    private void AppendChatSystem(string message)
    {
        _rtbChat.SelectionStart = _rtbChat.TextLength;
        _rtbChat.SelectionLength = 0;
        _rtbChat.SelectionColor = Color.FromArgb(217, 119, 6);
        _rtbChat.SelectionFont = new Font("Segoe UI", 8.5f, FontStyle.Italic);
        _rtbChat.AppendText($"[Hệ thống]: {message}{Environment.NewLine}");
        _rtbChat.ScrollToCaret();
    }
}
