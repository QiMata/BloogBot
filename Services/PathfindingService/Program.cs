using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PathfindingService
{
    public class Program
    {
        private static class NativeEnvironment
        {
            [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            private static extern int _putenv(string envString);

            public static void Set(string key, string value)
            {
                try
                {
                    _ = _putenv($"{key}={value}");
                }
                catch
                {
                    // Best-effort only; managed environment is still set below.
                }
            }
        }

        public static void Main(string[] args)
        {
            var previousDataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
            var resolvedDataDir = ResolveNavigationDataDirectory(previousDataDir);

            if (!string.IsNullOrEmpty(resolvedDataDir))
            {
                if (!resolvedDataDir.EndsWith(Path.DirectorySeparatorChar))
                {
                    resolvedDataDir += Path.DirectorySeparatorChar;
                }

                Environment.SetEnvironmentVariable("WWOW_DATA_DIR", resolvedDataDir);
                NativeEnvironment.Set("WWOW_DATA_DIR", resolvedDataDir);
                Console.WriteLine($"[PathfindingService] WWOW_DATA_DIR set to: {resolvedDataDir}");
            }
            else
            {
                Console.WriteLine("[PathfindingService] WARNING: Could not find nav data root containing mmaps/maps/vmaps. FindPath may fail.");
                if (!string.IsNullOrWhiteSpace(previousDataDir))
                    Console.WriteLine($"[PathfindingService] Existing WWOW_DATA_DIR was invalid: {previousDataDir}");
            }

            CreateHostBuilder(args)
                .Build()
                .Run();
        }

        private static string? ResolveNavigationDataDirectory(string? existingDataDir)
        {
            var candidates = new List<string>();
            AddCandidate(candidates, existingDataDir);

            var baseDir = NormalizePath(AppContext.BaseDirectory);
            var currentDir = NormalizePath(Directory.GetCurrentDirectory());

            AddCommonOutputCandidates(candidates, baseDir);
            AddCommonOutputCandidates(candidates, currentDir);
            AddCandidate(candidates, @"D:\World of Warcraft");

            foreach (var ancestor in EnumerateAncestors(baseDir))
                AddCommonOutputCandidates(candidates, ancestor);

            if (!string.Equals(baseDir, currentDir, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var ancestor in EnumerateAncestors(currentDir))
                    AddCommonOutputCandidates(candidates, ancestor);
            }

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (HasNavData(candidate))
                    return candidate;
            }

            return null;
        }

        private static void AddCommonOutputCandidates(List<string> candidates, string? root)
        {
            if (string.IsNullOrWhiteSpace(root))
                return;

            AddCandidate(candidates, root);
            AddCandidate(candidates, Path.Combine(root, "Bot", "Debug", "net8.0"));
            AddCandidate(candidates, Path.Combine(root, "Bot", "Debug", "x64"));
            AddCandidate(candidates, Path.Combine(root, "Bot", "Release", "net8.0"));
            AddCandidate(candidates, Path.Combine(root, "Bot", "Release", "x64"));
            AddCandidate(candidates, Path.Combine(root, "Tests", "Bot", "Debug", "net8.0"));
        }

        private static void AddCandidate(List<string> candidates, string? candidate)
        {
            var normalized = NormalizePath(candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
                candidates.Add(normalized);
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path.Trim().Trim('"'));
            }
            catch
            {
                return path.Trim().Trim('"');
            }
        }

        private static IEnumerable<string> EnumerateAncestors(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                yield break;

            DirectoryInfo? current;
            try
            {
                current = new DirectoryInfo(path);
            }
            catch
            {
                yield break;
            }

            while (current != null)
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }

        private static bool HasNavData(string root)
        {
            static bool HasEntries(string path)
            {
                try
                {
                    return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
                }
                catch
                {
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(root))
                return false;

            var mmapsDir = Path.Combine(root, "mmaps");
            var mapsDir = Path.Combine(root, "maps");
            var vmapsDir = Path.Combine(root, "vmaps");

            return HasEntries(mmapsDir)
                && HasEntries(mapsDir)
                && HasEntries(vmapsDir);
        }

        /// <summary>
        /// Launches the PathfindingService as a separate process.
        /// Used by StateManager when the service isn't already running.
        /// </summary>
        public static void LaunchServiceFromCommandLine()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var exePath = Path.Combine(baseDir, "PathfindingService.exe");
                var dllPath = Path.Combine(baseDir, "PathfindingService.dll");

                ProcessStartInfo psi;
                if (File.Exists(exePath))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = baseDir,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                }
                else if (File.Exists(dllPath))
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"\"{dllPath}\"",
                        WorkingDirectory = baseDir,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                }
                else
                {
                    Console.WriteLine($"PathfindingService not found at {exePath} or {dllPath}");
                    return;
                }

                Process.Start(psi);
                Console.WriteLine("PathfindingService launched as separate process.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch PathfindingService: {ex.Message}");
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.AddJsonFile("appsettings.PathfindingService.json", optional: false, reloadOnChange: true);
                    builder.AddJsonFile($"appsettings.PathfindingService.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    builder.AddEnvironmentVariables();
                    if (args != null)
                        builder.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;

                    // Register PathfindingSocketServer as a singleton
                    services.AddSingleton<PathfindingSocketServer>(serviceProvider =>
                    {
                        var logger = serviceProvider.GetRequiredService<ILogger<PathfindingSocketServer>>();

                        var ipAddress = configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
                        var port = int.Parse(configuration["PathfindingService:Port"] ?? "5000");

                        return new PathfindingSocketServer(ipAddress, port, logger);
                    });

                    // Register the hosted service
                    services.AddHostedService<PathfindingServiceWorker>();
                });
    }
}
