//tv6
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Caro.Client.Controls;

public sealed class ChatControl : UserControl
{
    private readonly ListBox messageList;
    private readonly TextBox messageInput;
    private readonly Button sendButton;

    public event Action<string>? MessageSent;

    public ChatControl()
    {
        Dock = DockStyle.Fill;

        messageList = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true
        };

        messageInput = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Nhập tin nhắn...",
            MaxLength = 500
        };

        sendButton = new Button
        {
            Text = "Gửi",
            Dock = DockStyle.Fill,
            Width = 65
        };

        var inputPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            ColumnCount = 2
        };

        inputPanel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100));
        inputPanel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 65));

        inputPanel.Controls.Add(messageInput, 0, 0);
        inputPanel.Controls.Add(sendButton, 1, 0);

        Controls.Add(messageList);
        Controls.Add(inputPanel);

        sendButton.Click += (s, e) => SendMessage();

        messageInput.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        };
    }

    private void SendMessage()
    {
        string message = messageInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(message))
            return;

        MessageSent?.Invoke(message);
        messageInput.Clear();
    }

    public void AddMessage(string sender, string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() =>
                AddMessage(sender, message)));
            return;
        }

        messageList.Items.Add($"{sender}: {message}");
        messageList.TopIndex = messageList.Items.Count - 1;
    }
}
