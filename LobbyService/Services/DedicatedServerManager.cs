using LobbyService.Models;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace LobbyService.Services
{
    public class DedicatedServerManager : IDedicatedServerManager
    {
        private readonly ILogger<DedicatedServerManager> _logger;
        private readonly IOptionsMonitor<DedicatedServerOptions> _optionsMonitor;
        private readonly ConcurrentDictionary<string, DedicatedServerRuntime> _runtimes = new();
        private readonly HashSet<int> _reservedPorts = new();
        private readonly object _portLock = new();

        public DedicatedServerManager(
            ILogger<DedicatedServerManager> logger,
            IOptionsMonitor<DedicatedServerOptions> optionsMonitor,
            IHostApplicationLifetime appLifetime)
        {
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            appLifetime.ApplicationStopping.Register(StopAll);
        }

        public async Task<DedicatedServerInstance> StartForRoomAsync(
            string roomId,
            string mapName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new InvalidOperationException("roomId is required.");
            }

            if (_runtimes.ContainsKey(roomId))
            {
                throw new InvalidOperationException($"Dedicated server already exists for room {roomId}.");
            }

            var options = _optionsMonitor.CurrentValue;
            ValidateOptions(options);

            var port = ReservePort(options);
            var resolvedMap = string.IsNullOrWhiteSpace(mapName) ? "/Game/Maps/Level_01" : mapName;
            var arguments = BuildArguments(options.ArgumentsTemplate, roomId, resolvedMap, options.HostIp, port);

            var workingDirectory = string.IsNullOrWhiteSpace(options.WorkingDirectory)
                ? Path.GetDirectoryName(options.ExecutablePath) ?? AppContext.BaseDirectory
                : options.WorkingDirectory;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = options.ExecutablePath,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            try
            {
                if (!process.Start())
                {
                    ReleasePort(port);
                    throw new InvalidOperationException($"Failed to start dedicated server process for room {roomId}.");
                }
            }
            catch (Win32Exception)
            {
                ReleasePort(port);
                process.Dispose();
                throw;
            }
            catch (InvalidOperationException)
            {
                ReleasePort(port);
                process.Dispose();
                throw;
            }

            var runtime = new DedicatedServerRuntime(roomId, options.HostIp, port, process);
            if (!_runtimes.TryAdd(roomId, runtime))
            {
                StopProcess(process, options.KillProcessTreeOnStop);
                ReleasePort(port);
                process.Dispose();
                throw new InvalidOperationException($"Failed to track dedicated server for room {roomId}.");
            }

            process.Exited += (_, _) =>
            {
                if (_runtimes.TryGetValue(roomId, out var current) && ReferenceEquals(current.Process, process))
                {
                    _runtimes.TryRemove(roomId, out _);
                    ReleasePort(current.Port);
                }

                _logger.LogWarning(
                    "[DS] Process exited: room={RoomId}, pid={Pid}, exitCode={ExitCode}",
                    roomId,
                    process.Id,
                    process.ExitCode
                );
            };

            if (options.StartupDelayMs > 0)
            {
                await Task.Delay(options.StartupDelayMs, cancellationToken);
            }

            if (process.HasExited)
            {
                StopForRoom(roomId);
                throw new InvalidOperationException($"Dedicated server exited during startup for room {roomId}.");
            }

            _logger.LogInformation(
                "[DS] Started: room={RoomId}, pid={Pid}, endpoint={Ip}:{Port}, args={Args}",
                roomId,
                process.Id,
                options.HostIp,
                port,
                arguments
            );

            return new DedicatedServerInstance(
                roomId,
                options.HostIp,
                port,
                process.Id,
                DateTime.UtcNow
            );
        }

        public bool IsRoomServerRunning(string roomId)
        {
            if (!_runtimes.TryGetValue(roomId, out var runtime))
            {
                return false;
            }

            return !runtime.Process.HasExited;
        }

        public void StopForRoom(string roomId)
        {
            if (!_runtimes.TryRemove(roomId, out var runtime))
            {
                return;
            }

            var options = _optionsMonitor.CurrentValue;
            StopProcess(runtime.Process, options.KillProcessTreeOnStop);
            ReleasePort(runtime.Port);

            _logger.LogInformation(
                "[DS] Stopped: room={RoomId}, pid={Pid}, endpoint={Ip}:{Port}",
                roomId,
                runtime.Process.Id,
                runtime.Ip,
                runtime.Port
            );
        }

        public void StopAll()
        {
            foreach (var roomId in _runtimes.Keys.ToArray())
            {
                StopForRoom(roomId);
            }
        }

        private static void ValidateOptions(DedicatedServerOptions options)
        {
            if (!options.Enabled)
            {
                throw new InvalidOperationException("Dedicated server orchestration is disabled.");
            }

            if (string.IsNullOrWhiteSpace(options.ExecutablePath))
            {
                throw new InvalidOperationException("DedicatedServer:ExecutablePath is not configured.");
            }

            if (!File.Exists(options.ExecutablePath))
            {
                throw new InvalidOperationException(
                    $"Dedicated server executable not found: {options.ExecutablePath}");
            }

            if (options.PortRangeStart <= 0 || options.PortRangeEnd <= 0 || options.PortRangeStart > options.PortRangeEnd)
            {
                throw new InvalidOperationException("DedicatedServer port range is invalid.");
            }

            if (string.IsNullOrWhiteSpace(options.HostIp) || !IPAddress.TryParse(options.HostIp, out _))
            {
                throw new InvalidOperationException("DedicatedServer:HostIp must be a valid IPv4/IPv6 address.");
            }
        }

        private int ReservePort(DedicatedServerOptions options)
        {
            lock (_portLock)
            {
                for (var port = options.PortRangeStart; port <= options.PortRangeEnd; port++)
                {
                    if (_reservedPorts.Contains(port))
                    {
                        continue;
                    }

                    if (!IsPortAvailable(port))
                    {
                        continue;
                    }

                    _reservedPorts.Add(port);
                    return port;
                }
            }

            throw new InvalidOperationException(
                $"No available port in configured range {options.PortRangeStart}-{options.PortRangeEnd}.");
        }

        private void ReleasePort(int port)
        {
            lock (_portLock)
            {
                _reservedPorts.Remove(port);
            }
        }

        private static bool IsPortAvailable(int port)
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private static string BuildArguments(
            string template,
            string roomId,
            string map,
            string ip,
            int port)
        {
            var value = string.IsNullOrWhiteSpace(template)
                ? "{map} -port={port} -log"
                : template;

            return value
                .Replace("{roomId}", roomId, StringComparison.OrdinalIgnoreCase)
                .Replace("{map}", map, StringComparison.OrdinalIgnoreCase)
                .Replace("{ip}", ip, StringComparison.OrdinalIgnoreCase)
                .Replace("{port}", port.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private void StopProcess(Process process, bool killTree)
        {
            try
            {
                if (process.HasExited)
                {
                    process.Dispose();
                    return;
                }

                process.Kill(killTree);
                process.WaitForExit(2000);
            }
            catch (InvalidOperationException)
            {
                // Process already exited while stopping.
            }
            catch (Win32Exception ex)
            {
                _logger.LogWarning(ex, "[DS] Failed to stop process pid={Pid}", process.Id);
            }
            finally
            {
                process.Dispose();
            }
        }

        private sealed class DedicatedServerRuntime
        {
            public DedicatedServerRuntime(string roomId, string ip, int port, Process process)
            {
                RoomId = roomId;
                Ip = ip;
                Port = port;
                Process = process;
            }

            public string RoomId { get; }
            public string Ip { get; }
            public int Port { get; }
            public Process Process { get; }
        }
    }
}
