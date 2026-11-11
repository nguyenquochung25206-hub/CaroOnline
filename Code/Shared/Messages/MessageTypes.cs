namespace Caro.Shared.Messages;
public static class MessageTypes
{
    // Kết nối client và server
    public const string Hello = "HELLO";
    public const string Welcome = "WELCOME";
    public const string Ping = "PING";
    public const string Pong = "PONG";
    public const string Error = "ERROR";
    // Tạo phòng chơi
    public const string CreateRoom = "CREATE_ROOM";
    public const string RoomCreated = "ROOM_CREATED";
    // Tham gia phòng chơi
    public const string JoinRoom = "JOIN_ROOM";
    public const string RoomJoined = "ROOM_JOINED";
    public const string JoinRoomFailed = "JOIN_ROOM_FAILED";
    // Rời phòng
    public const string LeaveRoom = "LEAVE_ROOM";
    public const string RoomLeft = "ROOM_LEFT";
    public const string OpponentLeft = "OPPONENT_LEFT";
    // Bắt đầu trận đấu
    public const string StartGame = "START_GAME";
    // Gửi và xác nhận nước đi
    public const string Move = "MOVE";
    public const string MoveAccepted = "MOVE_ACCEPTED";
    public const string MoveRejected = "MOVE_REJECTED";
    // Cập nhật bàn cờ và thông báo kết quả
    public const string BoardUpdated = "BOARD_UPDATED";
    public const string GameOver = "GAME_OVER";
    // Tin nhắn trong phòng
    public const string Chat = "CHAT";
}
