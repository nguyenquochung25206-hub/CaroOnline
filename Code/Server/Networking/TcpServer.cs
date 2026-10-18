namespace Caro.Server.Networking;
// TODO TV1: TcpListener, accept nhiều client bất đồng bộ, cancellation, đóng tài nguyên.
public sealed class TcpServer(string ip, int port)
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"TODO: chạy TCP server tại {ip}:{port}");
        return Task.CompletedTask;
    }
}
