namespace Caro.Shared.Messages;
public static class MessageTypes
{
    public const string Hello = "HELLO";
    public const string Welcome = "WELCOME";
    public const string Ping = "PING";
    public const string Pong = "PONG";
    public const string Error = "ERROR";
    public const string CreateRoom = "CREATE_ROOM";
    public const string RoomCreated = "ROOM_CREATED";
    public const string JoinRoom = "JOIN_ROOM";
    public const string RoomJoined = "ROOM_JOINED";
    public const string JoinRoomFailed = "JOIN_ROOM_FAILED";
    public const string LeaveRoom = "LEAVE_ROOM";
    public const string RoomLeft = "ROOM_LEFT";
    public const string StartGame = "START_GAME";
    public const string Move = "MOVE";
    public const string MoveAccepted = "MOVE_ACCEPTED";
    public const string MoveRejected = "MOVE_REJECTED";
    public const string BoardUpdated = "BOARD_UPDATED";
    public const string GameOver = "GAME_OVER";
    public const string Chat = "CHAT";
}
