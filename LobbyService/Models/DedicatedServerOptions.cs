namespace LobbyService.Models
{
    public class DedicatedServerOptions
    {
        public const string SectionName = "DedicatedServer";

        public bool Enabled { get; set; } = false;
        public string ExecutablePath { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string HostIp { get; set; } = "127.0.0.1";
        public int PortRangeStart { get; set; } = 7777;
        public int PortRangeEnd { get; set; } = 7877;
        public string ArgumentsTemplate { get; set; } = "{map} -port={port} -log";
        public int StartupDelayMs { get; set; } = 1500;
        public bool KillProcessTreeOnStop { get; set; } = true;
    }
}
