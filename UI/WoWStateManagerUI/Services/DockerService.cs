using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace WoWStateManagerUI.Services
{
    public class ContainerInfo
    {
        public string Name { get; set; } = "";
        public string Image { get; set; } = "";
        public string Status { get; set; } = "";
        public string Ports { get; set; } = "";
        public string State { get; set; } = "";  // running, exited, paused, etc.
        public bool IsHealthy { get; set; }
        public string Project { get; set; } = ""; // grouping label (WWoW, WAR, FFXI, etc.)
    }

    /// <summary>
    /// Manages Docker containers via the Docker CLI.
    /// All operations shell out to `docker` — no Docker SDK dependency needed.
    /// </summary>
    public class DockerService
    {
        /// <summary>Map container name prefixes to project groups for display.</summary>
        private static readonly (string Prefix, string Project)[] ProjectMappings =
        [
            ("mangos", "WWoW"),
            ("realmd", "WWoW"),
            ("maria-db", "WWoW"),
            ("pathfinding", "WWoW"),
            ("scene-data", "WWoW"),
            ("war-", "Warhammer"),
            ("ffxi-", "FFXI"),
        ];

        public async Task<List<ContainerInfo>> ListContainersAsync()
        {
            // --all includes stopped containers
            var output = await RunDockerAsync("ps -a --format \"{{.Names}}|{{.Image}}|{{.Status}}|{{.Ports}}|{{.State}}\"");
            var containers = new List<ContainerInfo>();

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 5) continue;

                var name = parts[0].Trim();
                containers.Add(new ContainerInfo
                {
                    Name = name,
                    Image = parts[1].Trim(),
                    Status = parts[2].Trim(),
                    Ports = parts[3].Trim(),
                    State = parts[4].Trim(),
                    IsHealthy = parts[2].Contains("healthy"),
                    Project = ResolveProject(name),
                });
            }

            return containers.OrderBy(c => c.Project).ThenBy(c => c.Name).ToList();
        }

        public async Task<string> StartContainerAsync(string name)
            => await RunDockerAsync($"start {name}");

        public async Task<string> StopContainerAsync(string name)
            => await RunDockerAsync($"stop {name}");

        public async Task<string> RestartContainerAsync(string name)
            => await RunDockerAsync($"restart {name}");

        public async Task<string> GetLogsAsync(string name, int tailLines = 50)
            => await RunDockerAsync($"logs --tail {tailLines} {name}");

        public async Task<bool> TestDockerAvailableAsync()
        {
            try
            {
                var output = await RunDockerAsync("version --format \"{{.Server.Version}}\"");
                return !string.IsNullOrWhiteSpace(output);
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveProject(string containerName)
        {
            foreach (var (prefix, project) in ProjectMappings)
            {
                if (containerName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return project;
            }
            return "Other";
        }

        private static async Task<string> RunDockerAsync(string arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
                return $"ERROR: {stderr.Trim()}";

            return stdout.Trim();
        }
    }
}
