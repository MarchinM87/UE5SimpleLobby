namespace LobbyService.Models
{
    /// <summary>
    /// 房间数据模型（服务器内部存储）
    /// </summary>
    public class GameRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public int MaxPlayers { get; set; } = 4;
        public string MapName { get; set; } = string.Empty;
        public string DsIp { get; set; } = string.Empty;
        public int DsPort { get; set; } = 7777;
        public int? DsProcessId { get; set; }
        public DateTime? DsStartedAtUtc { get; set; }
        public List<string> Players { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsStarted { get; set; } = false;
    }
}
