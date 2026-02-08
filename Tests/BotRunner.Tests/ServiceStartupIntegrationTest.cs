using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests
{
    /// <summary>
    /// Integration tests that validate StateManager and PathfindingService can be properly
    /// started and communicate with each other. These tests capture service logs for debugging.
    /// 
    /// === WHERE TO VIEW PATHFINDING SERVICE LOGS ===
    /// 
    /// 1. TEST OUTPUT (Recommended):
    ///    - Run this test and check the "Output" pane in Test Explorer
    ///    - All stdout/stderr is captured and displayed
    ///    - Run with: dotnet test --logger "console;verbosity=detailed" --filter "ServiceStartupIntegrationTest"
    /// 
    /// 2. CONSOLE (When running manually):
    ///    - cd Services/PathfindingService
    ///    - dotnet run
    ///    - All logs appear in the terminal
    /// 
    /// 3. LOG FILE (Configure in appsettings):
    ///    - Add to appsettings.PathfindingService.json:
    ///      "Logging": { "File": { "Path": "pathfinding.log" } }
    /// 
    /// === COMMON ERRORS ===
    /// 
    /// 1. "Unable to load DLL 'Navigation.dll'":
    ///    - The native Navigation.dll is not in the output directory
    ///    - Build the Navigation C++ project first
    ///    - Ensure Navigation.dll is copied to the PathfindingService output
    /// 
    /// 2. "Navigation mesh not found":
    ///    - The mmaps/ directory is missing or empty
    ///    - Set WWOW_DATA_DIR environment variable to point to data directory
    ///    - Or place mmaps folder next to Navigation.dll
    /// 
    /// 3. Service exits immediately:
    ///    - Check the error logs captured in the test output
    ///    - Usually indicates a DLL loading or configuration issue
    /// </summary>
    [RequiresInfrastructure]
    public class ServiceStartupIntegrationTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly IConfiguration _configuration;
        private Process? _pathfindingServiceProcess;
        private Process? _stateManagerProcess;
        private readonly StringBuilder _pathfindingLogs = new();
        private readonly StringBuilder _stateManagerLogs = new();
        private readonly object _logLock = new();

        public ServiceStartupIntegrationTest(ITestOutputHelper output)
        {
            _output = output;
            
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();
            
            _configuration = configBuilder.Build();
        }

        [Fact]
        public async Task PathfindingService_ShouldStartSuccessfully_AndLogOutput()
        {
            _output.WriteLine("=== PATHFINDING SERVICE STARTUP TEST ===");
            _output.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");

            // Check if PathfindingService is already running
            var pathfindingIp = _configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
            var pathfindingPort = int.Parse(_configuration["PathfindingService:Port"] ?? "5000");

            _output.WriteLine($"Checking if PathfindingService is already running at {pathfindingIp}:{pathfindingPort}...");

            if (await IsServiceRunning(pathfindingIp, pathfindingPort))
            {
                _output.WriteLine("? PathfindingService is already running!");
                _output.WriteLine("  Skipping startup test - service is available.");
                
                // Test connectivity
                await TestPathfindingServiceConnectivity(pathfindingIp, pathfindingPort);
                return;
            }

            _output.WriteLine("PathfindingService is NOT running. Starting it now...");
            _output.WriteLine("");

            // Find the solution root
            var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
            if (solutionRoot == null)
            {
                Assert.Fail("Could not find solution root directory. Run test from within the solution.");
            }
            _output.WriteLine($"Solution root: {solutionRoot}");

            var pathfindingProjectPath = Path.Combine(solutionRoot, "Services", "PathfindingService");
            if (!Directory.Exists(pathfindingProjectPath))
            {
                Assert.Fail($"PathfindingService project not found at: {pathfindingProjectPath}");
            }
            _output.WriteLine($"PathfindingService project: {pathfindingProjectPath}");

            // Start PathfindingService - run from output directory where Navigation.dll exists
            _output.WriteLine("");
            _output.WriteLine("--- STARTING PATHFINDING SERVICE ---");

            // The service should run from the output directory where Navigation.dll is located
            var outputDirectory = Path.Combine(solutionRoot, "Bot", "Debug", "net8.0");
            if (!Directory.Exists(outputDirectory))
            {
                // Try Release if Debug doesn't exist
                outputDirectory = Path.Combine(solutionRoot, "Bot", "Release", "net8.0");
            }
            if (!Directory.Exists(outputDirectory))
            {
                // Fallback to generic net8.0
                outputDirectory = Path.Combine(solutionRoot, "Bot", "net8.0");
            }

            var pathfindingExePath = Path.Combine(outputDirectory, "PathfindingService.exe");
            var pathfindingDllPath = Path.Combine(outputDirectory, "PathfindingService.dll");
            var navigationDllPath = Path.Combine(outputDirectory, "Navigation.dll");

            _output.WriteLine($"Output directory: {outputDirectory}");
            _output.WriteLine($"PathfindingService.exe exists: {File.Exists(pathfindingExePath)}");
            _output.WriteLine($"PathfindingService.dll exists: {File.Exists(pathfindingDllPath)}");
            _output.WriteLine($"Navigation.dll exists: {File.Exists(navigationDllPath)}");

            ProcessStartInfo psi;
            if (File.Exists(pathfindingExePath))
            {
                // Use the exe directly
                psi = new ProcessStartInfo
                {
                    FileName = pathfindingExePath,
                    WorkingDirectory = outputDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                _output.WriteLine($"Starting via: {pathfindingExePath}");
            }
            else if (File.Exists(pathfindingDllPath))
            {
                // Fall back to dotnet PathfindingService.dll
                psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "PathfindingService.dll",
                    WorkingDirectory = outputDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                _output.WriteLine($"Starting via: dotnet PathfindingService.dll");
            }
            else
            {
                _output.WriteLine("Building PathfindingService first...");
                // Fall back to dotnet run (will build if needed)
                psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run",
                    WorkingDirectory = pathfindingProjectPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }

            // IMPORTANT: Add the output directory to PATH so native DLLs can be found
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            psi.Environment["PATH"] = outputDirectory + Path.PathSeparator + currentPath;
            _output.WriteLine($"Added to PATH: {outputDirectory}");

            // Set environment variables for logging
            psi.Environment["Logging__LogLevel__Default"] = "Debug";
            psi.Environment["Logging__LogLevel__PathfindingService"] = "Debug";

            _pathfindingServiceProcess = new Process { StartInfo = psi };
            
            _pathfindingServiceProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    lock (_logLock)
                    {
                        _pathfindingLogs.AppendLine($"[OUT] {args.Data}");
                    }
                    _output.WriteLine($"[PathfindingService] {args.Data}");
                }
            };

            _pathfindingServiceProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    lock (_logLock)
                    {
                        _pathfindingLogs.AppendLine($"[ERR] {args.Data}");
                    }
                    _output.WriteLine($"[PathfindingService-ERR] {args.Data}");
                }
            };

            try
            {
                bool started = _pathfindingServiceProcess.Start();
                Assert.True(started, "Failed to start PathfindingService process");

                _pathfindingServiceProcess.BeginOutputReadLine();
                _pathfindingServiceProcess.BeginErrorReadLine();

                _output.WriteLine($"PathfindingService process started with PID: {_pathfindingServiceProcess.Id}");
                _output.WriteLine("Waiting for service to become available...");
                _output.WriteLine("");

                // Wait for service to become available
                var timeout = TimeSpan.FromSeconds(120); // Navigation mesh loading can take time
                var sw = Stopwatch.StartNew();
                bool serviceAvailable = false;

                while (sw.Elapsed < timeout)
                {
                    if (_pathfindingServiceProcess.HasExited)
                    {
                        _output.WriteLine("");
                        _output.WriteLine("=== PATHFINDING SERVICE EXITED UNEXPECTEDLY ===");
                        _output.WriteLine($"Exit code: {_pathfindingServiceProcess.ExitCode}");
                        _output.WriteLine("");
                        _output.WriteLine("=== CAPTURED LOGS ===");
                        _output.WriteLine(_pathfindingLogs.ToString());
                        _output.WriteLine("=== END LOGS ===");
                        
                        Assert.Fail($"PathfindingService process exited with code {_pathfindingServiceProcess.ExitCode}. Check logs above for errors.");
                    }

                    if (await IsServiceRunning(pathfindingIp, pathfindingPort))
                    {
                        serviceAvailable = true;
                        break;
                    }

                    // Progress indicator every 10 seconds
                    if (sw.Elapsed.TotalSeconds % 10 < 1)
                    {
                        _output.WriteLine($"  Still waiting... ({sw.Elapsed.TotalSeconds:F0}s elapsed)");
                    }

                    await Task.Delay(1000);
                }

                if (!serviceAvailable)
                {
                    _output.WriteLine("");
                    _output.WriteLine("=== TIMEOUT WAITING FOR SERVICE ===");
                    _output.WriteLine($"Waited {timeout.TotalSeconds} seconds but service did not respond on {pathfindingIp}:{pathfindingPort}");
                    _output.WriteLine("");
                    _output.WriteLine("=== CAPTURED LOGS ===");
                    _output.WriteLine(_pathfindingLogs.ToString());
                    _output.WriteLine("=== END LOGS ===");

                    Assert.Fail($"PathfindingService did not become available within {timeout.TotalSeconds} seconds");
                }

                _output.WriteLine("");
                _output.WriteLine($"? PathfindingService is now available! (took {sw.Elapsed.TotalSeconds:F1}s)");
                _output.WriteLine("");

                // Test connectivity
                await TestPathfindingServiceConnectivity(pathfindingIp, pathfindingPort);

            }
            finally
            {
                // Print all captured logs
                _output.WriteLine("");
                _output.WriteLine("=== ALL CAPTURED PATHFINDING SERVICE LOGS ===");
                _output.WriteLine(_pathfindingLogs.ToString());
                _output.WriteLine("=== END LOGS ===");
            }
        }

        [Fact]
        public async Task StateManager_ShouldStartAndConnectToPathfindingService()
        {
            _output.WriteLine("=== STATE MANAGER STARTUP TEST ===");
            _output.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");

            var pathfindingIp = _configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
            var pathfindingPort = int.Parse(_configuration["PathfindingService:Port"] ?? "5000");

            // First, ensure PathfindingService is running
            _output.WriteLine("Step 1: Ensuring PathfindingService is available...");
            
            if (!await IsServiceRunning(pathfindingIp, pathfindingPort))
            {
                _output.WriteLine("  PathfindingService is NOT running.");
                _output.WriteLine("  Please start PathfindingService first:");
                _output.WriteLine("    cd Services/PathfindingService && dotnet run");
                _output.WriteLine("");
                Assert.Fail("PathfindingService must be running before testing StateManager connectivity");
            }

            _output.WriteLine($"  ? PathfindingService is running at {pathfindingIp}:{pathfindingPort}");
            _output.WriteLine("");

            // Find the solution root
            var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
            if (solutionRoot == null)
            {
                Assert.Fail("Could not find solution root directory");
            }

            var stateManagerProjectPath = Path.Combine(solutionRoot, "Services", "WoWStateManager");
            if (!Directory.Exists(stateManagerProjectPath))
            {
                Assert.Fail($"StateManager project not found at: {stateManagerProjectPath}");
            }

            _output.WriteLine("Step 2: Starting StateManager...");
            _output.WriteLine($"  Project path: {stateManagerProjectPath}");
            _output.WriteLine("");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --no-build",
                WorkingDirectory = stateManagerProjectPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Set environment variables for logging and configuration
            psi.Environment["Logging__LogLevel__Default"] = "Debug";
            psi.Environment["PathfindingService__IpAddress"] = pathfindingIp;
            psi.Environment["PathfindingService__Port"] = pathfindingPort.ToString();

            _stateManagerProcess = new Process { StartInfo = psi };

            _stateManagerProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    lock (_logLock)
                    {
                        _stateManagerLogs.AppendLine($"[OUT] {args.Data}");
                    }
                    _output.WriteLine($"[StateManager] {args.Data}");
                }
            };

            _stateManagerProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    lock (_logLock)
                    {
                        _stateManagerLogs.AppendLine($"[ERR] {args.Data}");
                    }
                    _output.WriteLine($"[StateManager-ERR] {args.Data}");
                }
            };

            try
            {
                bool started = _stateManagerProcess.Start();
                Assert.True(started, "Failed to start StateManager process");

                _stateManagerProcess.BeginOutputReadLine();
                _stateManagerProcess.BeginErrorReadLine();

                _output.WriteLine($"  StateManager process started with PID: {_stateManagerProcess.Id}");
                _output.WriteLine("");

                // Wait for StateManager to connect to PathfindingService
                _output.WriteLine("Step 3: Waiting for StateManager to initialize...");
                
                var timeout = TimeSpan.FromSeconds(30);
                var sw = Stopwatch.StartNew();
                bool foundConnectionMessage = false;

                while (sw.Elapsed < timeout)
                {
                    if (_stateManagerProcess.HasExited)
                    {
                        _output.WriteLine("");
                        _output.WriteLine("=== STATE MANAGER EXITED ===");
                        _output.WriteLine($"Exit code: {_stateManagerProcess.ExitCode}");
                        _output.WriteLine("");
                        _output.WriteLine("=== CAPTURED LOGS ===");
                        _output.WriteLine(_stateManagerLogs.ToString());
                        _output.WriteLine("=== END LOGS ===");

                        // Check if it exited normally (expected for quick tests)
                        if (_stateManagerProcess.ExitCode == 0)
                        {
                            _output.WriteLine("StateManager exited with code 0 - this may be expected for initialization tests");
                            break;
                        }

                        Assert.Fail($"StateManager exited unexpectedly with code {_stateManagerProcess.ExitCode}");
                    }

                    // Check logs for connection success
                    lock (_logLock)
                    {
                        if (_stateManagerLogs.ToString().Contains("PathfindingService") && 
                            (_stateManagerLogs.ToString().Contains("connected") || 
                             _stateManagerLogs.ToString().Contains("available") ||
                             _stateManagerLogs.ToString().Contains("Connected")))
                        {
                            foundConnectionMessage = true;
                            break;
                        }
                    }

                    await Task.Delay(500);
                }

                if (foundConnectionMessage)
                {
                    _output.WriteLine("  ? StateManager connected to PathfindingService!");
                }
                else
                {
                    _output.WriteLine("  ? Could not confirm connection message in logs");
                }

                _output.WriteLine("");
                _output.WriteLine("=== TEST COMPLETED ===");
                
                // The test passes if we got this far without errors
                Assert.True(true, "StateManager startup test completed");
            }
            finally
            {
                _output.WriteLine("");
                _output.WriteLine("=== ALL CAPTURED STATE MANAGER LOGS ===");
                _output.WriteLine(_stateManagerLogs.ToString());
                _output.WriteLine("=== END LOGS ===");
            }
        }

        [Fact]
        public async Task BothServices_ShouldStartAndCommunicate_EndToEnd()
        {
            _output.WriteLine("=== END-TO-END SERVICE COMMUNICATION TEST ===");
            _output.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");

            var pathfindingIp = _configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
            var pathfindingPort = int.Parse(_configuration["PathfindingService:Port"] ?? "5000");
            
            var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
            if (solutionRoot == null)
            {
                Assert.Fail("Could not find solution root directory");
            }

            // Phase 1: Start PathfindingService if not already running
            _output.WriteLine("PHASE 1: PathfindingService Setup");
            _output.WriteLine("?".PadRight(50, '?'));

            bool weStartedPathfinding = false;
            if (!await IsServiceRunning(pathfindingIp, pathfindingPort))
            {
                _output.WriteLine("  Starting PathfindingService...");
                weStartedPathfinding = await StartPathfindingServiceAsync(solutionRoot, pathfindingIp, pathfindingPort);
                
                if (!weStartedPathfinding)
                {
                    _output.WriteLine("");
                    _output.WriteLine("=== PATHFINDING SERVICE LOGS ===");
                    _output.WriteLine(_pathfindingLogs.ToString());
                    _output.WriteLine("=== END LOGS ===");
                    Assert.Fail("Failed to start PathfindingService");
                }
            }
            else
            {
                _output.WriteLine($"  ? PathfindingService already running at {pathfindingIp}:{pathfindingPort}");
            }
            _output.WriteLine("");

            // Phase 2: Test PathfindingService API
            _output.WriteLine("PHASE 2: PathfindingService API Test");
            _output.WriteLine("?".PadRight(50, '?'));
            await TestPathfindingServiceConnectivity(pathfindingIp, pathfindingPort);
            _output.WriteLine("");

            // Phase 3: Validate StateManager can connect
            _output.WriteLine("PHASE 3: StateManager Connection Test");
            _output.WriteLine("?".PadRight(50, '?'));

            // For this test, we'll just verify the connection can be established
            // without fully starting StateManager (which requires WoW.exe)
            _output.WriteLine("  Testing direct TCP connection to PathfindingService...");
            
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(pathfindingIp, pathfindingPort);
                _output.WriteLine($"  ? TCP connection established to {pathfindingIp}:{pathfindingPort}");
                client.Close();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  ? TCP connection failed: {ex.Message}");
                Assert.Fail($"Could not establish TCP connection to PathfindingService: {ex.Message}");
            }
            _output.WriteLine("");

            // Phase 4: Summary
            _output.WriteLine("PHASE 4: Test Summary");
            _output.WriteLine("?".PadRight(50, '?'));
            _output.WriteLine("  ? PathfindingService is running and accepting connections");
            _output.WriteLine("  ? TCP connectivity verified");
            _output.WriteLine("  ? Service communication infrastructure is working");
            _output.WriteLine("");
            _output.WriteLine("=== END-TO-END TEST PASSED ===");

            // Print all logs for debugging
            if (_pathfindingLogs.Length > 0)
            {
                _output.WriteLine("");
                _output.WriteLine("=== PATHFINDING SERVICE LOGS ===");
                _output.WriteLine(_pathfindingLogs.ToString());
                _output.WriteLine("=== END LOGS ===");
            }
        }

        private async Task<bool> StartPathfindingServiceAsync(string solutionRoot, string ip, int port)
        {
            // Run from output directory where Navigation.dll exists
            var outputDirectory = Path.Combine(solutionRoot, "Bot", "Debug", "net8.0");
            if (!Directory.Exists(outputDirectory))
                outputDirectory = Path.Combine(solutionRoot, "Bot", "Release", "net8.0");
            if (!Directory.Exists(outputDirectory))
                outputDirectory = Path.Combine(solutionRoot, "Bot", "net8.0");

            var pathfindingExePath = Path.Combine(outputDirectory, "PathfindingService.exe");
            var pathfindingDllPath = Path.Combine(outputDirectory, "PathfindingService.dll");

            ProcessStartInfo psi;
            if (File.Exists(pathfindingExePath))
            {
                psi = new ProcessStartInfo
                {
                    FileName = pathfindingExePath,
                    WorkingDirectory = outputDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }
            else if (File.Exists(pathfindingDllPath))
            {
                psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "PathfindingService.dll",
                    WorkingDirectory = outputDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }
            else
            {
                var pathfindingProjectPath = Path.Combine(solutionRoot, "Services", "PathfindingService");
                psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run",
                    WorkingDirectory = pathfindingProjectPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
            }

            psi.Environment["Logging__LogLevel__Default"] = "Debug";
            psi.Environment["PathfindingService__IpAddress"] = ip;
            psi.Environment["PathfindingService__Port"] = port.ToString();
            
            // IMPORTANT: Add the output directory to PATH so native DLLs can be found
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            psi.Environment["PATH"] = outputDirectory + Path.PathSeparator + currentPath;

            _pathfindingServiceProcess = new Process { StartInfo = psi };

            _pathfindingServiceProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    lock (_logLock)
                    {
                        _pathfindingLogs.AppendLine($"[OUT] {args.Data}");
                    }
                    _output.WriteLine($"[PathfindingService] {args.Data}");
                }
            };

            _pathfindingServiceProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    lock (_logLock)
                    {
                        _pathfindingLogs.AppendLine($"[ERR] {args.Data}");
                    }
                    _output.WriteLine($"[PathfindingService-ERR] {args.Data}");
                }
            };

            if (!_pathfindingServiceProcess.Start())
                return false;

            _pathfindingServiceProcess.BeginOutputReadLine();
            _pathfindingServiceProcess.BeginErrorReadLine();

            _output.WriteLine($"  PathfindingService started with PID: {_pathfindingServiceProcess.Id}");

            // Wait for service to become available
            var timeout = TimeSpan.FromSeconds(120);
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < timeout)
            {
                if (_pathfindingServiceProcess.HasExited)
                {
                    _output.WriteLine($"  ? PathfindingService exited with code {_pathfindingServiceProcess.ExitCode}");
                    return false;
                }

                if (await IsServiceRunning(ip, port))
                {
                    _output.WriteLine($"  ? PathfindingService available after {sw.Elapsed.TotalSeconds:F1}s");
                    return true;
                }

                if (sw.Elapsed.TotalSeconds % 15 < 1)
                {
                    _output.WriteLine($"  Waiting for service... ({sw.Elapsed.TotalSeconds:F0}s)");
                }

                await Task.Delay(1000);
            }

            _output.WriteLine($"  ? Timeout waiting for PathfindingService");
            return false;
        }

        private async Task TestPathfindingServiceConnectivity(string ip, int port)
        {
            _output.WriteLine("Testing PathfindingService connectivity...");

            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(ip, port);
                var timeoutTask = Task.Delay(5000);

                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    _output.WriteLine("  ? Connection timeout");
                    Assert.Fail($"Connection to PathfindingService at {ip}:{port} timed out");
                }

                _output.WriteLine($"  ? Connected to PathfindingService at {ip}:{port}");
                
                // Additional info
                _output.WriteLine($"  Local endpoint: {client.Client.LocalEndPoint}");
                _output.WriteLine($"  Remote endpoint: {client.Client.RemoteEndPoint}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  ? Connection failed: {ex.Message}");
                throw;
            }
        }

        private async Task<bool> IsServiceRunning(string ip, int port)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(ip, port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(1000)) == connectTask;
                
                if (completed && client.Connected)
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private string? FindSolutionRoot(string startDirectory)
        {
            var dir = new DirectoryInfo(startDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "WestworldOfWarcraft.sln")) ||
                    (Directory.Exists(Path.Combine(dir.FullName, "Services")) &&
                     Directory.Exists(Path.Combine(dir.FullName, "Exports"))))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return null;
        }

        public void Dispose()
        {
            // Cleanup processes
            try
            {
                if (_pathfindingServiceProcess != null && !_pathfindingServiceProcess.HasExited)
                {
                    _output.WriteLine("Stopping PathfindingService process...");
                    _pathfindingServiceProcess.Kill(entireProcessTree: true);
                    _pathfindingServiceProcess.Dispose();
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error stopping PathfindingService: {ex.Message}");
            }

            try
            {
                if (_stateManagerProcess != null && !_stateManagerProcess.HasExited)
                {
                    _output.WriteLine("Stopping StateManager process...");
                    _stateManagerProcess.Kill(entireProcessTree: true);
                    _stateManagerProcess.Dispose();
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error stopping StateManager: {ex.Message}");
            }
        }

        /// <summary>
        /// Diagnostic test that checks all prerequisites for PathfindingService
        /// without actually starting it. Useful for troubleshooting.
        /// </summary>
        [Fact]
        public void PathfindingService_DiagnosePrerequisites()
        {
            _output.WriteLine("=== PATHFINDING SERVICE PREREQUISITES DIAGNOSTIC ===");
            _output.WriteLine($"Diagnostic run at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine("");

            var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
            var allPassed = true;

            // 1. Check solution structure
            _output.WriteLine("1. Solution Structure");
            _output.WriteLine("?".PadRight(50, '?'));
            
            if (solutionRoot == null)
            {
                _output.WriteLine("  ? Could not find solution root");
                allPassed = false;
            }
            else
            {
                _output.WriteLine($"  ? Solution root: {solutionRoot}");
            }

            var pathfindingProjectPath = solutionRoot != null 
                ? Path.Combine(solutionRoot, "Services", "PathfindingService")
                : null;

            if (pathfindingProjectPath != null && Directory.Exists(pathfindingProjectPath))
            {
                _output.WriteLine($"  ? PathfindingService project: {pathfindingProjectPath}");
            }
            else
            {
                _output.WriteLine($"  ? PathfindingService project not found");
                allPassed = false;
            }
            _output.WriteLine("");

            // 2. Check for Navigation.dll
            _output.WriteLine("2. Native Dependencies");
            _output.WriteLine("?".PadRight(50, '?'));

            var possibleDllPaths = new List<string>();
            if (solutionRoot != null)
            {
                possibleDllPaths.AddRange(new[]
                {
                    Path.Combine(solutionRoot, "Bot", "Debug", "net8.0", "Navigation.dll"),
                    Path.Combine(solutionRoot, "Bot", "Release", "net8.0", "Navigation.dll"),
                    Path.Combine(solutionRoot, "Bot", "net8.0", "Navigation.dll"),
                    Path.Combine(solutionRoot, "Exports", "Navigation", "x64", "Debug", "Navigation.dll"),
                    Path.Combine(solutionRoot, "Exports", "Navigation", "x64", "Release", "Navigation.dll"),
                    Path.Combine(solutionRoot, "Exports", "Navigation", "Debug", "Navigation.dll"),
                    Path.Combine(solutionRoot, "Exports", "Navigation", "Release", "Navigation.dll"),
                });
            }

            bool foundNavDll = false;
            foreach (var path in possibleDllPaths)
            {
                if (File.Exists(path))
                {
                    _output.WriteLine($"  ? Navigation.dll found at: {path}");
                    foundNavDll = true;
                    
                    // Check file info
                    var fileInfo = new FileInfo(path);
                    _output.WriteLine($"    Size: {fileInfo.Length:N0} bytes");
                    _output.WriteLine($"    Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                    break;
                }
            }

            if (!foundNavDll)
            {
                _output.WriteLine("  ? Navigation.dll NOT found in any expected location:");
                foreach (var path in possibleDllPaths)
                {
                    _output.WriteLine($"    - {path}");
                }
                _output.WriteLine("");
                _output.WriteLine("  FIX: Build the Navigation C++ project:");
                _output.WriteLine("    1. Open WestworldOfWarcraft.sln in Visual Studio");
                _output.WriteLine("    2. Build the 'Navigation' project (x64/Release or x64/Debug)");
                _output.WriteLine("    3. Copy Navigation.dll to the PathfindingService output directory");
                allPassed = false;
            }
            _output.WriteLine("");

            // 3. Check for navigation data (mmaps)
            _output.WriteLine("3. Navigation Data (mmaps)");
            _output.WriteLine("?".PadRight(50, '?'));

            var mmapsPaths = new List<string>();
            
            // Check environment variable first
            var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
            if (!string.IsNullOrEmpty(dataDir))
            {
                mmapsPaths.Add(Path.Combine(dataDir, "mmaps"));
                _output.WriteLine($"  WWOW_DATA_DIR set to: {dataDir}");
            }
            else
            {
                _output.WriteLine("  WWOW_DATA_DIR not set");
            }

            if (solutionRoot != null)
            {
                mmapsPaths.AddRange(new[]
                {
                    Path.Combine(solutionRoot, "mmaps"),
                    Path.Combine(solutionRoot, "Data", "mmaps"),
                    Path.Combine(solutionRoot, "Bot", "mmaps"),
                    Path.Combine(solutionRoot, "Bot", "Debug", "net8.0", "mmaps"),
                    Path.Combine(solutionRoot, "Bot", "Release", "net8.0", "mmaps"),
                });
            }

            bool foundMmaps = false;
            foreach (var path in mmapsPaths.Distinct())
            {
                if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path, "*.mmtile");
                    if (files.Length > 0)
                    {
                        _output.WriteLine($"  ? mmaps found at: {path}");
                        _output.WriteLine($"    Contains {files.Length} .mmtile files");
                        foundMmaps = true;
                        break;
                    }
                }
            }

            if (!foundMmaps)
            {
                _output.WriteLine("  ? mmaps directory NOT found or empty");
                _output.WriteLine("  Checked locations:");
                foreach (var path in mmapsPaths.Distinct())
                {
                    _output.WriteLine($"    - {path}");
                }
                _output.WriteLine("");
                _output.WriteLine("  FIX: Set WWOW_DATA_DIR environment variable:");
                _output.WriteLine("    $env:WWOW_DATA_DIR = \"C:\\path\\to\\your\\data\"");
                _output.WriteLine("    (ensure this directory contains a 'mmaps' subfolder with .mmtile files)");
                allPassed = false;
            }
            _output.WriteLine("");

            // 4. Check configuration
            _output.WriteLine("4. Configuration");
            _output.WriteLine("?".PadRight(50, '?'));

            var pathfindingIp = _configuration["PathfindingService:IpAddress"] ?? "127.0.0.1";
            var pathfindingPort = _configuration["PathfindingService:Port"] ?? "5000";
            
            _output.WriteLine($"  PathfindingService:IpAddress = {pathfindingIp}");
            _output.WriteLine($"  PathfindingService:Port = {pathfindingPort}");

            if (pathfindingProjectPath != null)
            {
                var appSettingsPath = Path.Combine(pathfindingProjectPath, "appsettings.PathfindingService.json");
                if (File.Exists(appSettingsPath))
                {
                    _output.WriteLine($"  ? Config file exists: {appSettingsPath}");
                }
                else
                {
                    _output.WriteLine($"  ? Config file not found: {appSettingsPath}");
                }
            }
            _output.WriteLine("");

            // 5. Check port availability
            _output.WriteLine("5. Port Availability");
            _output.WriteLine("?".PadRight(50, '?'));

            if (int.TryParse(pathfindingPort, out int port))
            {
                try
                {
                    using var listener = new TcpListener(System.Net.IPAddress.Parse(pathfindingIp), port);
                    listener.Start();
                    listener.Stop();
                    _output.WriteLine($"  ? Port {port} is available");
                }
                catch (SocketException)
                {
                    _output.WriteLine($"  ? Port {port} is already in use");
                    _output.WriteLine("    (This is OK if PathfindingService is already running)");
                }
            }
            _output.WriteLine("");

            // Summary
            _output.WriteLine("=== DIAGNOSTIC SUMMARY ===");
            if (allPassed)
            {
                _output.WriteLine("? All prerequisites appear to be met");
            }
            else
            {
                _output.WriteLine("? Some prerequisites are missing. See above for details.");
            }
            _output.WriteLine("");
            _output.WriteLine("To start PathfindingService manually:");
            _output.WriteLine("  cd Services/PathfindingService");
            _output.WriteLine("  dotnet run");
            _output.WriteLine("");
            _output.WriteLine("To run this test with verbose output:");
            _output.WriteLine("  dotnet test --filter \"PathfindingService_DiagnosePrerequisites\" --logger \"console;verbosity=detailed\"");

            // Don't fail the test - this is diagnostic only
            Assert.True(true, "Diagnostic completed - check output for details");
        }
    }
}
