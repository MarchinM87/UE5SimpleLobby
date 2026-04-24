using LobbyService.Models;
using System.Collections.Concurrent;

namespace LobbyService.Services
{
    /// <summary>
    /// 内存房间存储（单例）
    /// 后期可替换为 Redis 或 DB
    /// </summary>
    public class RoomStore
    {
        private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

        public IEnumerable<GameRoom> GetAll()
            => _rooms.Values;

        public GameRoom? Get(string roomId)
            => _rooms.TryGetValue(roomId, out var room) ? room : null;

        public void Add(GameRoom room)
            => _rooms[room.RoomId] = room;

        public bool Remove(string roomId)
            => _rooms.TryRemove(roomId, out _);

        public int Count
            => _rooms.Count;
    }
}
