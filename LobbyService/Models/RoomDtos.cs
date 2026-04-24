namespace LobbyService.Models
{
    // ── 请求 DTO ──────────────────────────────────────────

    public class CreateRoomRequest
    {
        public string PlayerName { get; set; } = string.Empty;
        public int MaxPlayers { get; set; } = 4;
        public string MapName { get; set; } = "/Game/Maps/Level_01";
        public string DsIp { get; set; } = "127.0.0.1";
        public int DsPort { get; set; } = 7777;
    }

    public class JoinRoomRequest
    {
        public string PlayerName { get; set; } = string.Empty;
    }

    public class LeaveRoomRequest
    {
        public string PlayerName { get; set; } = string.Empty;
    }

    // ── 响应 DTO ──────────────────────────────────────────

    public class RoomDto
    {
        public string RoomId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public string MapName { get; set; } = string.Empty;
        public bool IsStarted { get; set; }
    }

    public class CreateRoomResponse
    {
        public string RoomId { get; set; } = string.Empty;
        public string DsIp { get; set; } = string.Empty;
        public int DsPort { get; set; }
    }

    public class JoinRoomResponse
    {
        public string RoomId { get; set; } = string.Empty;
        public string DsIp { get; set; } = string.Empty;
        public int DsPort { get; set; }
        public string JoinToken { get; set; } = string.Empty;
    }

    public class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
    }

}
