using System.Collections.Concurrent;
using System.Text;
using Caro.Server.Networking;
using Caro.Shared.Messages;
using Caro.Shared.Protocol;

namespace Caro.Server.Services;

// Quan ly danh sach cac Client dang ket noi (TV1 - Tuan 2):
//  - Them, xoa, tim Client, dong tat ca Client.
//  - Gui RIENG cho 1 Client, gui cho nhieu Client, broadcast toan Server hoac theo phong.
//  - Chuyen message nghiep vu cho TV5 (MessageReceived) va bao khi Client ngat ket noi.
public sealed class ClientManager
{
    private static readonly UTF8Encoding Utf8 = new(false);

    // Luu danh sach Client dang ket noi.
    // ConcurrentDictionary an toan khi nhieu luong them/xoa/doc cung luc.
    // Key la ID cua Client, Value la doi tuong ClientSession.
    private readonly ConcurrentDictionary<Guid, ClientSession> _clients
        = new();

    // TV5 gan ham xu ly message (CREATE_ROOM, JOIN_ROOM, MOVE, CHAT, LEAVE_ROOM...).
    // Vi du trong Program.cs:
    //   clientManager.MessageReceived = (session, message) => handler.HandleAsync(session, message);
    public Func<ClientSession, NetworkMessage, Task>? MessageReceived { get; set; }

    // Xay ra sau khi Client bi xoa khoi danh sach (mat ket noi hoac thoat).
    // RoomService (TV5) dang ky de don phong va bao cho doi thu.
    public event Func<ClientSession, Task>? ClientDisconnected;

    // Tra ve tong so Client hien dang duoc quan ly.
    public int ClientCount => _clients.Count;

    // Them mot Client moi vao danh sach quan ly.
    public void Add(ClientSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Them Client theo ID rieng.
        if (!_clients.TryAdd(session.Id, session))
            return;

        // Ghi log so luong Client sau khi them.
        Console.WriteLine(
            $"[CLIENT] Added {session.RemoteAddress}. " +
            $"Total clients: {ClientCount}");
    }

    // Xoa Client khoi danh sach khi Client ngat ket noi.
    public async Task RemoveAsync(ClientSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Xoa Client dua tren ID. Neu da bi xoa roi thi khong lam gi them.
        if (!_clients.TryRemove(session.Id, out _))
            return;

        // Ghi log so luong Client sau khi xoa.
        Console.WriteLine(
            $"[CLIENT] Removed {session.RemoteAddress}. " +
            $"Total clients: {ClientCount}");

        // Bao cho cac noi dang ky (RoomService...) biet Client da roi di.
        await RaiseDisconnectedAsync(session).ConfigureAwait(false);
    }

    // ----- Tim Client -----

    // Lay Client theo ID.
    public bool TryGet(Guid clientId, out ClientSession? session)
    {
        return _clients.TryGetValue(clientId, out session);
    }

    // Tim Client theo ten nguoi choi (khong phan biet hoa thuong). Khong thay thi tra null.
    public ClientSession? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return _clients.Values.FirstOrDefault(s =>
            string.Equals(s.PlayerName, name, StringComparison.OrdinalIgnoreCase));
    }

    // Ban sao danh sach Client hien tai (an toan de duyet).
    public IReadOnlyList<ClientSession> GetAll()
    {
        return _clients.Values.ToList();
    }

    // ----- Gui rieng -----

    // Gui rieng cho 1 Client theo ID. Tra ve false neu khong thay Client hoac gui loi.
    public Task<bool> SendToAsync(Guid clientId, NetworkMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_clients.TryGetValue(clientId, out ClientSession? session))
            return Task.FromResult(false);

        return session.SendAsync(message);
    }

    // ----- Gui nhieu / broadcast -----

    // Gui cho tat ca Client dang ket noi (co the loai tru 1 Client, vi du nguoi gui).
    // Tra ve so Client gui thanh cong.
    public Task<int> BroadcastAsync(
        NetworkMessage message,
        Guid? exceptClientId = null)
    {
        IEnumerable<ClientSession> targets = _clients.Values
            .Where(s => s.Id != exceptClientId);

        return SendToSessionsAsync(targets, message);
    }

    // Gui cho danh sach Client cu the (vi du 2 nguoi trong cung 1 phong).
    // Client khong ton tai se bi bo qua. Tra ve so Client gui thanh cong.
    public Task<int> SendToManyAsync(
        IEnumerable<Guid> clientIds,
        NetworkMessage message,
        Guid? exceptClientId = null)
    {
        ArgumentNullException.ThrowIfNull(clientIds);

        var targets = new List<ClientSession>();

        foreach (Guid id in clientIds.Distinct())
        {
            if (id == exceptClientId)
                continue;

            if (_clients.TryGetValue(id, out ClientSession? session))
                targets.Add(session);
        }

        return SendToSessionsAsync(targets, message);
    }

    // Gui cho tat ca Client dang o trong 1 phong (dua vao ClientSession.RoomId).
    // Client o phong khac se KHONG nhan duoc message.
    public Task<int> BroadcastToRoomAsync(
        string roomId,
        NetworkMessage message,
        Guid? exceptClientId = null)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return Task.FromResult(0);

        IEnumerable<ClientSession> targets = _clients.Values
            .Where(s => s.RoomId == roomId && s.Id != exceptClientId);

        return SendToSessionsAsync(targets, message);
    }

    // Dong tat ca cac Client dang duoc quan ly.
    public void CloseAll()
    {
        // Duyet qua ban sao danh sach Client hien tai.
        foreach (ClientSession session in _clients.Values)
        {
            // Dong ket noi cua tung Client.
            session.Close();
        }
    }

    // ----- Ham noi bo -----

    // Duoc ClientSession goi khi nhan message nghiep vu.
    internal Task DispatchAsync(ClientSession session, NetworkMessage message)
    {
        Func<ClientSession, NetworkMessage, Task>? handler = MessageReceived;

        // Chua co ai xu ly thi bao loi lai cho Client.
        if (handler == null)
        {
            return session.SendErrorAsync("Unknown message type", message.RequestId);
        }

        return handler(session, message);
    }

    // Ma hoa message 1 LAN roi gui song song cho tung Client.
    // Mot Client cham hoac loi khong lam cham/chan cac Client con lai.
    private static async Task<int> SendToSessionsAsync(
        IEnumerable<ClientSession> targets,
        NetworkMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // ToList de bat dau gui ngay cho tat ca Client.
        List<ClientSession> sessions = targets.ToList();

        if (sessions.Count == 0)
            return 0;

        byte[] data = Utf8.GetBytes(MessageCodec.Encode(message));

        List<Task<bool>> tasks = sessions
            .Select(s => s.SendEncodedAsync(data))
            .ToList();

        bool[] results = await Task.WhenAll(tasks).ConfigureAwait(false);

        return results.Count(ok => ok);
    }

    // Goi lan luot tung noi dang ky ClientDisconnected; loi cua 1 noi khong anh huong noi khac.
    private async Task RaiseDisconnectedAsync(ClientSession session)
    {
        Func<ClientSession, Task>? handlers = ClientDisconnected;

        if (handlers == null)
            return;

        foreach (Delegate d in handlers.GetInvocationList())
        {
            try
            {
                await ((Func<ClientSession, Task>)d)(session).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[CLIENT] Disconnect handler error: {ex.Message}");
            }
        }
    }
}