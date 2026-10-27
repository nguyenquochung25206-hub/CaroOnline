namespace Caro.Server.Configuration;
// TODO TV1: Đọc IP/port từ cấu hình hoặc tham số; kiểm tra port hợp lệ.
public sealed class ServerOptions { public string Ip { get; set; } = "0.0.0.0"; public int Port { get; set; } = 5000; }
