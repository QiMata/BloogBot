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
            static bool HasNavData(string root)
            {
                if (string.IsNullOrWhiteSpace(root))
                    return false;

                return Directory.Exists(Path.Combine(root, "mmaps"))
                    && Directory.Exists(Path.Combine(root, "maps"))
                    && Directory.Exists(Path.Combine(root, "vmaps"));
            }

            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(existingDataDir))
                candidates.Add(existingDataDir);

            var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            candidates.Add(baseDir);
            candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "net8.0")));
            candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "net8.0")));
            candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "net8.0")));
            candidates.Add(Directory.GetCurrentDirectory());
            candidates.Add(@"D:\World of Warcraft");

            try
            {
                var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
                candidates.Add(Path.Combine(repoRoot, "Bot", "Debug", "net8.0"));
                candidates.Add(Path.Combine(repoRoot, "Bot", "Release", "net8.0"));
            }
            catch
            {
                // Ignore path normalization failures and continue probing.
            }

            foreach (var candidate in candidates
                         .Where(c => !string.IsNullOrWhiteSpace(c))
                         .Select(c => c.Trim('"').Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (HasNavData(candidate))
                    return Path.GetFullPath(candidate);
            }

            return null;
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
