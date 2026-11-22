// TODO TV5: Tạo phòng, tham gia phòng, rời phòng, ghép tối đa 2 người/phòng.
using System;
using System.Collections.Generic;

namespace Caro.Server.Services
{
    public sealed class RoomService
    {
        private const int MaxPlayers = 2;

        // Thông tin một phòng
        private sealed class Room
        {
            public string RoomId { get; private set; }

            public List<string> Players { get; private set; }

            public GameService? Game { get; set; }

            public Room(string roomId)
            {
                RoomId = roomId;
                Players = new List<string>();
                Game = null;
            }
        }

        // Danh sách phòng
        private readonly Dictionary<string, Room> rooms =
            new Dictionary<string, Room>();

        // Người chơi đang thuộc phòng nào
        private readonly Dictionary<string, string> playerRooms =
            new Dictionary<string, string>();

        private readonly object syncLock = new object();

        // Hàm tạo phòng nội bộ
        private bool CreateRoomInternal(
            string playerId,
            out string roomId)
        {
            roomId = Guid.NewGuid().ToString("N").Substring(0, 6);

            Room room = new Room(roomId);

            room.Players.Add(playerId);

            rooms.Add(roomId, room);
            playerRooms.Add(playerId, roomId);

            return true;
        }

        // 1. Tạo phòng
        public bool CreateRoom(
            string playerId,
            out string roomId)
        {
            lock (syncLock)
            {
                roomId = string.Empty;

                if (string.IsNullOrWhiteSpace(playerId))
                    return false;

                if (playerRooms.ContainsKey(playerId))
                    return false;

                return CreateRoomInternal(playerId, out roomId);
            }
        }

        // 2. Tham gia phòng
        public bool JoinRoom(
            string roomId,
            string playerId)
        {
            lock (syncLock)
            {
                if (string.IsNullOrWhiteSpace(roomId) ||
                    string.IsNullOrWhiteSpace(playerId))
                    return false;

                if (!rooms.ContainsKey(roomId))
                    return false;

                if (playerRooms.ContainsKey(playerId))
                    return false;

                Room room = rooms[roomId];

                if (room.Players.Count >= MaxPlayers)
                    return false;

                room.Players.Add(playerId);
                playerRooms.Add(playerId, roomId);

                // Đủ hai người thì bắt đầu trận đấu
                if (room.Players.Count == MaxPlayers)
                {
                    room.Game = new GameService(
                        room.Players[0],
                        room.Players[1]);
                }

                return true;
            }
        }

        // 3. Rời phòng
        public bool LeaveRoom(string playerId)
        {
            lock (syncLock)
            {
                if (string.IsNullOrWhiteSpace(playerId))
                    return false;

                if (!playerRooms.ContainsKey(playerId))
                    return false;

                string roomId = playerRooms[playerId];

                Room room = rooms[roomId];

                room.Players.Remove(playerId);
                playerRooms.Remove(playerId);

                // Hủy trận đấu hiện tại nếu có người rời phòng
                room.Game = null;

                // Xóa phòng nếu không còn người chơi
                if (room.Players.Count == 0)
                {
                    rooms.Remove(roomId);
                }

                return true;
            }
        }

        // 4. Ghép người chơi vào phòng còn chỗ
        public bool MatchPlayer(
            string playerId,
            out string roomId)
        {
            lock (syncLock)
            {
                roomId = string.Empty;

                if (string.IsNullOrWhiteSpace(playerId))
                    return false;

                if (playerRooms.ContainsKey(playerId))
                    return false;

                // Tìm phòng đang chờ người chơi thứ hai
                foreach (KeyValuePair<string, Room> item in rooms)
                {
                    Room room = item.Value;

                    if (room.Players.Count < MaxPlayers)
                    {
                        room.Players.Add(playerId);

                        playerRooms.Add(
                            playerId,
                            room.RoomId);

                        // Khởi tạo trận đấu
                        if (room.Players.Count == MaxPlayers)
                        {
                            room.Game = new GameService(
                                room.Players[0],
                                room.Players[1]);
                        }

                        roomId = room.RoomId;

                        return true;
                    }
                }

                // Không có phòng trống thì tạo phòng mới
                return CreateRoomInternal(playerId, out roomId);
            }
        }

        // 5. Lấy danh sách người chơi trong phòng
        public List<string> GetPlayers(string roomId)
        {
            lock (syncLock)
            {
                if (!rooms.ContainsKey(roomId))
                    return new List<string>();

                return new List<string>(
                    rooms[roomId].Players);
            }
        }

        // 6. Lấy trận đấu trong phòng
        public GameService? GetGame(string roomId)
        {
            lock (syncLock)
            {
                if (!rooms.ContainsKey(roomId))
                    return null;

                return rooms[roomId].Game;
            }
        }

        // 7. Lấy mã phòng của người chơi
        public string? GetRoomId(string playerId)
        {
            lock (syncLock)
            {
                if (!playerRooms.ContainsKey(playerId))
                    return null;

                return playerRooms[playerId];
            }
        }

        // 8. Kiểm tra phòng đã đủ người chưa
        public bool IsRoomFull(string roomId)
        {
            lock (syncLock)
            {
                if (!rooms.ContainsKey(roomId))
                    return false;

                return rooms[roomId].Players.Count >= MaxPlayers;
            }
        }
    }
}


