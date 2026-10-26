using System.Collections.Concurrent;
using Caro.Server.Networking;

namespace Caro.Server.Services;

public sealed class ClientManager
{
    // Luu danh sach Client dang ket noi.
    // ConcurrentDictionary ho tro nhieu tac vu truy cap dong thoi an toan hon.
    // Key la ID cua Client, Value la doi tuong ClientSession.
    private readonly ConcurrentDictionary<Guid, ClientSession> _clients
        = new();

    // Tra ve tong so Client hien dang duoc quan ly.
    public int ClientCount => _clients.Count;

    // Them mot Client moi vao danh sach quan ly.
    public void Add(ClientSession session)
    {
        // Them Client theo ID rieng.
        _clients.TryAdd(session.Id, session);

        // Ghi log so luong Client sau khi them.
        Console.WriteLine(
            $"[CLIENT] Added {session.RemoteAddress}. " +
            $"Total clients: {ClientCount}");
    }

    // Xoa Client khoi danh sach khi Client ngat ket noi.
    public Task RemoveAsync(ClientSession session)
    {
        // Xoa Client dua tren ID cua Client.
        _clients.TryRemove(session.Id, out _);

        // Ghi log so luong Client sau khi xoa.
        Console.WriteLine(
            $"[CLIENT] Removed {session.RemoteAddress}. " +
            $"Total clients: {ClientCount}");

        // Tra ve Task da hoan thanh de co the goi bang await.
        return Task.CompletedTask;
    }

    // Dong tat ca cac Client dang duoc quan ly.
    public void CloseAll()
    {
        // Duyet qua danh sach Client hien tai.
        foreach (ClientSession session in _clients.Values)
        {
            // Dong ket noi cua tung Client.
            session.Close();
        }
    }
}