using System.Diagnostics;

namespace PathfindingService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args)
                .Build()
                .Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    builder.AddEnvironmentVariables();
                    if (args != null)
                        builder.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<PathfindingServiceWorker>();
                });

        /// <summary>
        /// Launches the PathfindingService as an external process with debugging support
        /// </summary>
        public static void LaunchServiceFromCommandLine()
        {
            LaunchServiceFromCommandLine(enableEnhancedDebugging: false);
        }

        /// <summary>
        /// Launches the PathfindingService as an external process with configurable debugging support
        /// </summary>
        /// <param name="enableEnhancedDebugging">If true, uses enhanced debugging with colored console and detailed logging</param>
        public static void LaunchServiceFromCommandLine(bool enableEnhancedDebugging)
        {
            if (enableEnhancedDebugging)
            {
                LaunchServiceWithEnhancedDebugging();
                return;
            }

            try
            {
                // Determine the correct path to the PathfindingService.exe (not .dll for external launch)
                var botOutputPath = AppDomain.CurrentDomain.BaseDirectory;
                var pathfindingServicePath = Path.Combine(botOutputPath, "PathfindingService.exe");

                // Ensure the file exists before trying to launch it
                if (!File.Exists(pathfindingServicePath))
                {
                    throw new FileNotFoundException($"PathfindingService.exe not found at: {pathfindingServicePath}");
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"cd /d \"{botOutputPath}\" && echo Starting PathfindingService for debugging... && echo Working Directory: {botOutputPath} && echo Service Path: {pathfindingServicePath} && echo. && \"{pathfindingServicePath}\" && echo. && echo PathfindingService has exited. Press any key to close this window... && pause > nul\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal,
                    WorkingDirectory = botOutputPath
                };

                Process.Start(processInfo);
                Console.WriteLine($"PathfindingService has been launched with debugging console from: {botOutputPath}");
                Console.WriteLine("The console window will remain open for debugging purposes and show detailed startup information.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch PathfindingService: {ex.Message}");
            }
        }

        /// <summary>
        /// Launches the PathfindingService with enhanced debugging and logging
        /// </summary>
        public static void LaunchServiceWithEnhancedDebugging()
        {
            try
            {
                // Determine the correct path to the PathfindingService.exe
                var botOutputPath = AppDomain.CurrentDomain.BaseDirectory;
                var pathfindingServicePath = Path.Combine(botOutputPath, "PathfindingService.exe");

                // Ensure the file exists before trying to launch it
                if (!File.Exists(pathfindingServicePath))
                {
                    throw new FileNotFoundException($"PathfindingService.exe not found at: {pathfindingServicePath}");
                }

                // Create a batch file for enhanced debugging
                var batchContent = $@"@echo off
title PathfindingService Debug Console
color 0A
echo ===============================================
echo  PathfindingService Debug Launch
echo ===============================================
echo.
echo Working Directory: {botOutputPath}
echo Service Path: {pathfindingServicePath}
echo Timestamp: %date% %time%
echo.
cd /d ""{botOutputPath}""
echo Starting PathfindingService...
echo.
""{pathfindingServicePath}""
echo.
echo ===============================================
echo  PathfindingService has exited
echo ===============================================
echo Exit Code: %ERRORLEVEL%
echo Timestamp: %date% %time%
echo.
echo Press any key to close this debugging window...
pause > nul
";

                var batchPath = Path.Combine(Path.GetTempPath(), "PathfindingService_Debug.bat");
                File.WriteAllText(batchPath, batchContent);

                var processInfo = new ProcessStartInfo
                {
                    FileName = batchPath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Process.Start(processInfo);
                Console.WriteLine($"PathfindingService has been launched with enhanced debugging console.");
                Console.WriteLine($"Debug batch file created at: {batchPath}");
                Console.WriteLine("The console window will remain open with detailed logging and error information.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch PathfindingService with enhanced debugging: {ex.Message}");
            }
        }
    }
}
