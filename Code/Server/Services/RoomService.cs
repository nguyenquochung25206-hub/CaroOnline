// TODO TV5: Tạo phòng, tham gia phòng, rời phòng, ghép tối đa 2 người/phòng.

using System;
using System.Collections.Generic;

namespace Caro.Server.Services
{
    public sealed class RoomService
    {
        private const int MaxPlayers = 2;

        // Danh sách phòng và người chơi
        private readonly Dictionary<string, List<string>> rooms
            = new Dictionary<string, List<string>>();

        private readonly Dictionary<string, string> playerRooms
            = new Dictionary<string, string>();

        private readonly object syncLock = new object();

        // 1. Tạo phòng
        public bool CreateRoom(string playerId, out string roomId)
        {
            lock (syncLock)
            {
                roomId = string.Empty;

                if (string.IsNullOrWhiteSpace(playerId))
                    return false;

                if (playerRooms.ContainsKey(playerId))
                    return false;

                roomId = Guid.NewGuid().ToString("N").Substring(0, 6);

                rooms.Add(roomId, new List<string> { playerId });
                playerRooms.Add(playerId, roomId);

                return true;
            }
        }

        // 2. Tham gia phòng
        public bool JoinRoom(string roomId, string playerId)
        {
            lock (syncLock)
            {
                if (string.IsNullOrWhiteSpace(playerId))
                    return false;

                if (!rooms.ContainsKey(roomId))
                    return false;

                if (playerRooms.ContainsKey(playerId))
                    return false;

                List<string> players = rooms[roomId];

                if (players.Count >= MaxPlayers)
                    return false;

                players.Add(playerId);
                playerRooms.Add(playerId, roomId);

                return true;
            }
        }

        // 3. Rời phòng
        public bool LeaveRoom(string playerId)
        {
            lock (syncLock)
            {
                if (!playerRooms.ContainsKey(playerId))
                    return false;

                string roomId = playerRooms[playerId];

                rooms[roomId].Remove(playerId);
                playerRooms.Remove(playerId);

                if (rooms[roomId].Count == 0)
                    rooms.Remove(roomId);

                return true;
            }
        }

        // 4. Ghép người chơi vào phòng
        public bool MatchPlayer(string playerId, out string roomId)
        {
            lock (syncLock)
            {
                roomId = string.Empty;

                if (string.IsNullOrWhiteSpace(playerId))
                    return false;

                if (playerRooms.ContainsKey(playerId))
                    return false;

                foreach (var room in rooms)
                {
                    if (room.Value.Count < MaxPlayers)
                    {
                        room.Value.Add(playerId);
                        playerRooms.Add(playerId, room.Key);

                        roomId = room.Key;
                        return true;
                    }
                }

                return CreateRoom(playerId, out roomId);
            }
        }

        // 5. Lấy danh sách người chơi
        public List<string> GetPlayers(string roomId)
        {
            lock (syncLock)
            {
                if (!rooms.ContainsKey(roomId))
                    return new List<string>();

                return new List<string>(rooms[roomId]);
            }
        }
    }
}

