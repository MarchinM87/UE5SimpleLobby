namespace LobbyService.Services
{
    public sealed record DedicatedServerInstance(
        string RoomId,
        string Ip,
        int Port,
        int ProcessId,
        DateTime StartedAtUtc
    );

    public interface IDedicatedServerManager
    {
        Task<DedicatedServerInstance> StartForRoomAsync(
            string roomId,
            string mapName,
            CancellationToken cancellationToken = default
        );

        bool IsRoomServerRunning(string roomId);
        void StopForRoom(string roomId);
        void StopAll();
    }
}
