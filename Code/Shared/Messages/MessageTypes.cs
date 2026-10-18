namespace Caro.Shared.Messages;

// TODO TV2: Bổ sung và thống nhất tên message giữa Client/Server.
public static class MessageTypes
{
    public const string Hello = "HELLO";
    public const string Welcome = "WELCOME";
    public const string Ping = "PING";
    public const string Pong = "PONG";
    public const string Error = "ERROR";
    public const string CreateRoom = "CREATE_ROOM";
    public const string JoinRoom = "JOIN_ROOM";
    public const string RoomJoined = "ROOM_JOINED";
    public const string StartGame = "START_GAME";
    public const string Move = "MOVE";
    public const string MoveAccepted = "MOVE_ACCEPTED";
    public const string BoardUpdated = "BOARD_UPDATED";
    public const string GameOver = "GAME_OVER";
    public const string Chat = "CHAT";
    public const string LeaveRoom = "LEAVE_ROOM";
}
