using Caro.Server.Networking;

Console.WriteLine("Caro Online Server — skeleton");
var server = new TcpServer("0.0.0.0", 5000);
await server.StartAsync();
