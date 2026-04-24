using LobbyService.Models;
using LobbyService.Services;
using Microsoft.AspNetCore.Mvc;

namespace LobbyService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoomsController : ControllerBase
    {
        private readonly RoomStore _store;
        private readonly IDedicatedServerManager _dedicatedServerManager;
        private readonly ILogger<RoomsController> _logger;

        public RoomsController(
            RoomStore store,
            IDedicatedServerManager dedicatedServerManager,
            ILogger<RoomsController> logger)
        {
            _store = store;
            _dedicatedServerManager = dedicatedServerManager;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────
        // GET /api/rooms
        // 列举所有未开始的房间
        // ─────────────────────────────────────────────────
        [HttpGet]
        public ActionResult<List<RoomDto>> GetRooms()
        {
            var rooms = _store.GetAll()
                .Where(r => !r.IsStarted)
                .Select(r => new RoomDto
                {
                    RoomId = r.RoomId,
                    OwnerName = r.OwnerName,
                    PlayerCount = r.Players.Count,
                    MaxPlayers = r.MaxPlayers,
                    MapName = r.MapName,
                    IsStarted = r.IsStarted
                })
                .ToList();

            return Ok(rooms);
        }

        // ─────────────────────────────────────────────────
        // POST /api/rooms
        // 创建房间（同时拉起 Dedicated Server）
        // ─────────────────────────────────────────────────
        [HttpPost]
        public async Task<ActionResult<CreateRoomResponse>> CreateRoom([FromBody] CreateRoomRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.PlayerName))
            {
                return BadRequest(new ErrorResponse { Error = "PlayerName is required" });
            }
            if (req.MaxPlayers <= 0)
            {
                return BadRequest(new ErrorResponse { Error = "MaxPlayers must be greater than 0" });
            }

            var roomId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            DedicatedServerInstance server;
            try
            {
                server = await _dedicatedServerManager.StartForRoomAsync(
                    roomId,
                    req.MapName,
                    HttpContext.RequestAborted
                );
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "[Room] Failed to start DS for room {RoomId}", roomId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    Error = ex.Message
                });
            }

            var room = new GameRoom
            {
                RoomId = roomId,
                OwnerName = req.PlayerName,
                MaxPlayers = req.MaxPlayers,
                MapName = req.MapName,
                DsIp = server.Ip,
                DsPort = server.Port,
                DsProcessId = server.ProcessId,
                DsStartedAtUtc = server.StartedAtUtc,
                Players = new List<string> { req.PlayerName },
                CreatedAt = DateTime.UtcNow
            };

            _store.Add(room);

            _logger.LogInformation(
                "[Room] Created: {RoomId} by {Owner} DS={Ip}:{Port} PID={Pid}",
                roomId, req.PlayerName, room.DsIp, room.DsPort, room.DsProcessId
            );

            return Ok(new CreateRoomResponse
            {
                RoomId = roomId,
                DsIp = room.DsIp,
                DsPort = room.DsPort
            });
        }

        // ─────────────────────────────────────────────────
        // POST /api/rooms/{roomId}/join
        // 加入房间
        // ─────────────────────────────────────────────────
        [HttpPost("{roomId}/join")]
        public ActionResult<JoinRoomResponse> JoinRoom(
            string roomId, [FromBody] JoinRoomRequest req)
        {
            var room = _store.Get(roomId);
            if (room == null)
            {
                return NotFound(new ErrorResponse { Error = "Room not found" });
            }

            if (room.IsStarted)
            {
                return BadRequest(new ErrorResponse { Error = "Room already started" });
            }
            if (string.IsNullOrWhiteSpace(req.PlayerName))
            {
                return BadRequest(new ErrorResponse { Error = "PlayerName is required" });
            }
            if (!_dedicatedServerManager.IsRoomServerRunning(roomId))
            {
                _store.Remove(roomId);
                return BadRequest(new ErrorResponse { Error = "Room server is unavailable" });
            }

            if (room.Players.Count >= room.MaxPlayers)
            {
                return BadRequest(new ErrorResponse { Error = "Room is full" });
            }

            if (!room.Players.Contains(req.PlayerName))
            {
                room.Players.Add(req.PlayerName);
            }

            var token = Guid.NewGuid().ToString("N");

            _logger.LogInformation(
                "[Room] Joined: {RoomId} by {Player}",
                roomId, req.PlayerName
            );

            return Ok(new JoinRoomResponse
            {
                RoomId = roomId,
                DsIp = room.DsIp,
                DsPort = room.DsPort,
                JoinToken = token
            });
        }

        // ─────────────────────────────────────────────────
        // POST /api/rooms/{roomId}/leave
        // 离开房间
        // ─────────────────────────────────────────────────
        [HttpPost("{roomId}/leave")]
        public ActionResult LeaveRoom(
            string roomId, [FromBody] LeaveRoomRequest req)
        {
            var room = _store.Get(roomId);
            if (room == null)
            {
                return NotFound(new ErrorResponse { Error = "Room not found" });
            }

            room.Players.Remove(req.PlayerName);

            _logger.LogInformation(
                "[Room] Left: {RoomId} by {Player}, remaining={Count}",
                roomId, req.PlayerName, room.Players.Count
            );

            // 房间为空则销毁
            if (room.Players.Count == 0)
            {
                _store.Remove(roomId);
                _dedicatedServerManager.StopForRoom(roomId);
                _logger.LogInformation("[Room] Destroyed: {RoomId} (empty)", roomId);
            }

            return Ok();
        }

        // ─────────────────────────────────────────────────
        // DELETE /api/rooms/{roomId}
        // 强制销毁房间（房主使用）
        // ─────────────────────────────────────────────────
        [HttpDelete("{roomId}")]
        public ActionResult DestroyRoom(string roomId)
        {
            if (!_store.Remove(roomId))
            {
                return NotFound(new ErrorResponse { Error = "Room not found" });
            }

            _dedicatedServerManager.StopForRoom(roomId);
            _logger.LogInformation("[Room] Force destroyed: {RoomId}", roomId);
            return Ok();
        }
    }
}
