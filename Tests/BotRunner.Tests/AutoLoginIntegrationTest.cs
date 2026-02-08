using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using GameData.Core.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinImports;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests;

/// <summary>
/// End-to-end integration test for auto-login functionality.
///
/// This test validates:
/// 1. WoW client launches successfully
/// 2. Bot automatically logs in with credentials from StateManager
/// 3. Bot selects character and enters world
/// 4. Bot stays connected and in-world for at least 60 seconds
/// 5. Screen state correctly detects InWorld (ContinentId &lt; 0xFF)
/// 6. If disconnected, bot automatically reconnects
///
/// Prerequisites:
/// - Mangos server running with SOAP enabled on port 7878
/// - MySQL database with characters, realmd databases accessible
/// - WoW.exe client installed and path configured in appsettings.test.json
/// - Loader.dll built (run Setup-InjectionDlls.ps1)
/// - StateManager running (or auto-started by test)
///
/// To run: dotnet test --filter "FullyQualifiedName~AutoLogin" --logger "console;verbosity=detailed"
/// </summary>
[RequiresInfrastructure]
public class AutoLoginIntegrationTest : IClassFixture<AutoLoginTestFixture>
{
    private readonly AutoLoginTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AutoLoginIntegrationTest(AutoLoginTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Memory offsets for screen detection (Vanilla 1.12.1)
    /// </summary>
    private static class MemoryOffsets
    {
        public const int LoginState = 0xB41478;        // ASCII string: "login", "charselect", "connecting"
        public const int ManagerBase = 0xB41414;       // Object manager base pointer
        public const int PlayerGuidOffset = 0xC0;      // Offset from ManagerBase
        public const int ClientConnection = 0xB41DA0;  // Connection pointer
        public const int ContinentId = 0x86F694;       // Map ID (0xFF = loading, 0xFFFFFFFF = not in map)
        public const int CharacterCount = 0xB42140;    // Number of characters
    }

    [Fact]
    [Trait("Category", "AutoLogin")]
    public async Task AutoLogin_ShouldLoginAndStayConnectedFor60Seconds()
    {
        _output.WriteLine("=== AUTO-LOGIN INTEGRATION TEST ===");
        _output.WriteLine("This test validates the complete auto-login flow.");
        _output.WriteLine("");

        // Step 1: Validate prerequisites
        _output.WriteLine("1. VALIDATING PREREQUISITES");

        var (success, errorMessage) = await _fixture.ValidatePrerequisitesAsync(_output);
        if (!success)
        {
            _output.WriteLine($"   SKIP: {errorMessage}");
            Assert.Fail($"Prerequisites not met: {errorMessage}");
            return;
        }
        _output.WriteLine("   All prerequisites validated.");
        _output.WriteLine("");

        // Step 2: Ensure StateManager is running
        _output.WriteLine("2. ENSURING STATEMANAGER IS RUNNING");
        await _fixture.EnsureStateManagerRunningAsync(_output);
        _output.WriteLine("");

        // Step 3: Launch WoW with injection
        _output.WriteLine("3. LAUNCHING WOW WITH BOT INJECTION");
        var (processHandle, processId) = await _fixture.LaunchWoWWithInjectionAsync(_output);

        if (processHandle == IntPtr.Zero)
        {
            _output.WriteLine("   FAIL: Could not launch WoW with injection");
            Assert.Fail("Failed to launch WoW with injection");
            return;
        }

        _output.WriteLine($"   WoW launched with PID: {processId}");
        _output.WriteLine("");

        try
        {
            // Step 4: Monitor login flow and verify state transitions
            _output.WriteLine("4. MONITORING AUTO-LOGIN FLOW");

            var stateHistory = new List<(DateTime Time, WoWScreenState State, string Details)>();
            var startTime = DateTime.Now;
            var inWorldTime = DateTime.MinValue;
            var testDuration = TimeSpan.FromMinutes(3); // 3 minute total timeout
            var inWorldRequiredDuration = TimeSpan.FromSeconds(60);

            bool sawLoginScreen = false;
            bool sawConnecting = false;
            bool sawCharSelect = false;
            bool sawLoadingWorld = false;
            bool sawInWorld = false;
            bool stayedInWorld60Seconds = false;

            WoWScreenState? lastState = null;
            var pollInterval = TimeSpan.FromMilliseconds(500);

            _output.WriteLine("   Monitoring screen state transitions...");
            _output.WriteLine("");

            while (DateTime.Now - startTime < testDuration)
            {
                // Check if process exited
                if (HasProcessExited(processHandle))
                {
                    _output.WriteLine("   ERROR: WoW process exited unexpectedly");
                    break;
                }

                // Read current screen state
                var (state, details) = DetectScreenState(processHandle);

                // Track which states we've seen
                switch (state)
                {
                    case WoWScreenState.LoginScreen:
                        sawLoginScreen = true;
                        break;
                    case WoWScreenState.Connecting:
                        sawConnecting = true;
                        break;
                    case WoWScreenState.CharacterSelect:
                        sawCharSelect = true;
                        break;
                    case WoWScreenState.LoadingWorld:
                        sawLoadingWorld = true;
                        break;
                    case WoWScreenState.InWorld:
                        sawInWorld = true;
                        if (inWorldTime == DateTime.MinValue)
                        {
                            inWorldTime = DateTime.Now;
                            _output.WriteLine($"   [{DateTime.Now:HH:mm:ss.fff}] ENTERED WORLD - Starting 60 second timer");
                        }
                        break;
                }

                // Log state transitions
                if (state != lastState)
                {
                    stateHistory.Add((DateTime.Now, state, details));
                    _output.WriteLine($"   [{DateTime.Now:HH:mm:ss.fff}] STATE: {lastState} -> {state}");
                    _output.WriteLine($"      Details: {details}");
                    lastState = state;
                }

                // Check if we've been in world for 60 seconds
                if (state == WoWScreenState.InWorld && inWorldTime != DateTime.MinValue)
                {
                    var timeInWorld = DateTime.Now - inWorldTime;
                    if (timeInWorld >= inWorldRequiredDuration)
                    {
                        stayedInWorld60Seconds = true;
                        _output.WriteLine($"   [{DateTime.Now:HH:mm:ss.fff}] SUCCESS: Stayed in world for {timeInWorld.TotalSeconds:F1} seconds");
                        break;
                    }

                    // Log progress every 10 seconds
                    if ((int)timeInWorld.TotalSeconds % 10 == 0 && timeInWorld.TotalSeconds > 0)
                    {
                        // Only log once per 10-second interval
                        if (stateHistory.LastOrDefault().Time.Second != DateTime.Now.Second)
                        {
                            _output.WriteLine($"   [{DateTime.Now:HH:mm:ss.fff}] In world for {timeInWorld.TotalSeconds:F0} seconds...");
                        }
                    }
                }
                else if (sawInWorld && state != WoWScreenState.InWorld && state != WoWScreenState.LoadingWorld)
                {
                    // We were in world but now we're not (disconnected or logged out)
                    _output.WriteLine($"   [{DateTime.Now:HH:mm:ss.fff}] LEFT WORLD (was in world, now {state}) - Testing reconnect...");
                    inWorldTime = DateTime.MinValue; // Reset timer for reconnect test
                }

                await Task.Delay(pollInterval);
            }

            _output.WriteLine("");

            // Step 5: Verify diagnostic logs
            _output.WriteLine("5. CHECKING DIAGNOSTIC LOGS");
            var (logSuccess, characterName, snapshotReceived) = await CheckDiagnosticLogsAsync();
            _output.WriteLine($"   Character name from logs: {characterName ?? "(not found)"}");
            _output.WriteLine($"   Snapshot sent to StateManager: {snapshotReceived}");
            _output.WriteLine("");

            // Step 6: Report results
            _output.WriteLine("6. TEST RESULTS");
            _output.WriteLine("   State Transitions Observed:");
            _output.WriteLine($"      - Login Screen: {(sawLoginScreen ? "YES" : "no")}");
            _output.WriteLine($"      - Connecting: {(sawConnecting ? "YES" : "no")}");
            _output.WriteLine($"      - Character Select: {(sawCharSelect ? "YES" : "no")}");
            _output.WriteLine($"      - Loading World: {(sawLoadingWorld ? "YES" : "no")}");
            _output.WriteLine($"      - In World: {(sawInWorld ? "YES" : "no")}");
            _output.WriteLine($"      - Stayed 60s in World: {(stayedInWorld60Seconds ? "YES" : "no")}");
            _output.WriteLine("");

            _output.WriteLine("   State History:");
            foreach (var (time, state, details) in stateHistory.TakeLast(20))
            {
                _output.WriteLine($"      {time:HH:mm:ss.fff}: {state}");
            }
            _output.WriteLine("");

            // Assertions
            Assert.True(sawInWorld, "Bot should have entered the world");
            Assert.True(stayedInWorld60Seconds, "Bot should have stayed in world for at least 60 seconds");

            if (!string.IsNullOrEmpty(characterName))
            {
                _output.WriteLine($"   SUCCESS: Character '{characterName}' stayed in world for 60+ seconds");
            }
        }
        finally
        {
            // Cleanup: terminate WoW process
            _output.WriteLine("");
            _output.WriteLine("7. CLEANUP");
            _fixture.CleanupProcess(processHandle, processId);
            _output.WriteLine("   WoW process terminated");
        }

        _output.WriteLine("");
        _output.WriteLine("=== TEST COMPLETE ===");
    }

    [Fact]
    [Trait("Category", "AutoLogin")]
    public async Task AutoLogin_ShouldReconnectAfterDisconnect()
    {
        _output.WriteLine("=== AUTO-RECONNECT TEST ===");
        _output.WriteLine("This test validates that the bot reconnects after being disconnected.");
        _output.WriteLine("NOTE: This test requires manual intervention to disconnect the client.");
        _output.WriteLine("");

        // Step 1: Validate prerequisites
        var (success, errorMessage) = await _fixture.ValidatePrerequisitesAsync(_output);
        if (!success)
        {
            _output.WriteLine($"SKIP: {errorMessage}");
            return;
        }

        // Step 2: Ensure StateManager is running
        await _fixture.EnsureStateManagerRunningAsync(_output);

        // Step 3: Launch WoW with injection
        var (processHandle, processId) = await _fixture.LaunchWoWWithInjectionAsync(_output);

        if (processHandle == IntPtr.Zero)
        {
            Assert.Fail("Failed to launch WoW with injection");
            return;
        }

        try
        {
            var testDuration = TimeSpan.FromMinutes(5); // 5 minute timeout for reconnect test
            var startTime = DateTime.Now;

            int enteredWorldCount = 0;
            DateTime? firstInWorldTime = null;
            DateTime? secondInWorldTime = null;
            WoWScreenState? lastState = null;
            bool wasInWorld = false;
            bool wasDisconnected = false;
            bool reconnected = false;

            _output.WriteLine("Monitoring for disconnect/reconnect cycle...");
            _output.WriteLine("If the bot enters world, manually disconnect to test reconnection.");
            _output.WriteLine("");

            while (DateTime.Now - startTime < testDuration)
            {
                if (HasProcessExited(processHandle))
                {
                    _output.WriteLine("WoW process exited");
                    break;
                }

                var (state, details) = DetectScreenState(processHandle);

                if (state != lastState)
                {
                    _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] State: {lastState} -> {state}");
                    lastState = state;
                }

                // Track world entry/exit
                if (state == WoWScreenState.InWorld)
                {
                    if (!wasInWorld)
                    {
                        enteredWorldCount++;
                        if (enteredWorldCount == 1)
                        {
                            firstInWorldTime = DateTime.Now;
                            _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] FIRST WORLD ENTRY - disconnect now to test reconnect");
                        }
                        else if (enteredWorldCount == 2 && wasDisconnected)
                        {
                            secondInWorldTime = DateTime.Now;
                            reconnected = true;
                            _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RECONNECTED! Bot successfully re-entered world.");

                            // Stay a bit longer to confirm stable connection
                            await Task.Delay(5000);
                            break;
                        }
                        wasInWorld = true;
                    }
                }
                else if (wasInWorld && state == WoWScreenState.LoginScreen)
                {
                    wasDisconnected = true;
                    wasInWorld = false;
                    _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] DISCONNECTED - watching for reconnect...");
                }
                else if (state != WoWScreenState.InWorld)
                {
                    wasInWorld = false;
                }

                await Task.Delay(500);
            }

            _output.WriteLine("");
            _output.WriteLine("RESULTS:");
            _output.WriteLine($"  Times entered world: {enteredWorldCount}");
            _output.WriteLine($"  First entry: {firstInWorldTime?.ToString("HH:mm:ss") ?? "N/A"}");
            _output.WriteLine($"  Was disconnected: {wasDisconnected}");
            _output.WriteLine($"  Reconnected: {reconnected}");
            _output.WriteLine($"  Second entry: {secondInWorldTime?.ToString("HH:mm:ss") ?? "N/A"}");

            // Only fail if we never entered world
            Assert.True(enteredWorldCount >= 1, "Bot should have entered world at least once");

            if (wasDisconnected)
            {
                Assert.True(reconnected, "Bot should have reconnected after disconnect");
            }
            else
            {
                _output.WriteLine("NOTE: Disconnect was not detected. To fully test reconnection, manually disconnect during the test.");
            }
        }
        finally
        {
            _fixture.CleanupProcess(processHandle, processId);
        }
    }

    [Fact]
    [Trait("Category", "AutoLogin")]
    public async Task ScreenStateDetection_ShouldCorrectlyIdentifyInWorld()
    {
        _output.WriteLine("=== SCREEN STATE DETECTION VALIDATION ===");
        _output.WriteLine("This test validates that ContinentId-based screen detection works correctly.");
        _output.WriteLine("");

        var (success, errorMessage) = await _fixture.ValidatePrerequisitesAsync(_output);
        if (!success)
        {
            _output.WriteLine($"SKIP: {errorMessage}");
            return;
        }

        await _fixture.EnsureStateManagerRunningAsync(_output);

        var (processHandle, processId) = await _fixture.LaunchWoWWithInjectionAsync(_output);

        if (processHandle == IntPtr.Zero)
        {
            Assert.Fail("Failed to launch WoW");
            return;
        }

        try
        {
            var testDuration = TimeSpan.FromMinutes(2);
            var startTime = DateTime.Now;
            WoWScreenState? lastState = null;

            _output.WriteLine("Monitoring screen states and validating ContinentId values...");
            _output.WriteLine("Expected: InWorld when ContinentId < 0xFF (e.g., 0x0 for Eastern Kingdoms)");
            _output.WriteLine("");

            bool sawValidInWorld = false;
            uint inWorldContinentId = 0;

            while (DateTime.Now - startTime < testDuration)
            {
                if (HasProcessExited(processHandle))
                {
                    break;
                }

                var (state, details) = DetectScreenState(processHandle);
                var continentId = ReadUInt32(processHandle, MemoryOffsets.ContinentId);

                if (state != lastState)
                {
                    _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] State={state}, ContinentId=0x{continentId:X}");
                    lastState = state;

                    // Validate the detection logic
                    if (state == WoWScreenState.InWorld)
                    {
                        Assert.True(continentId < 0xFF, $"InWorld should have ContinentId < 0xFF, got 0x{continentId:X}");
                        sawValidInWorld = true;
                        inWorldContinentId = continentId;
                        _output.WriteLine($"   VALIDATED: InWorld with ContinentId=0x{continentId:X} (valid map ID)");
                    }
                    else if (state == WoWScreenState.LoadingWorld)
                    {
                        Assert.Equal(0xFFU, continentId);
                        _output.WriteLine($"   VALIDATED: LoadingWorld with ContinentId=0xFF");
                    }
                    else if (state == WoWScreenState.CharacterSelect)
                    {
                        Assert.Equal(0xFFFFFFFFU, continentId);
                        _output.WriteLine($"   VALIDATED: CharacterSelect with ContinentId=0xFFFFFFFF");
                    }
                }

                // Stop once we've validated InWorld
                if (sawValidInWorld)
                {
                    await Task.Delay(5000); // Stay a bit to confirm
                    break;
                }

                await Task.Delay(500);
            }

            _output.WriteLine("");
            _output.WriteLine($"RESULT: InWorld detection {(sawValidInWorld ? "VALIDATED" : "NOT TESTED")}");
            if (sawValidInWorld)
            {
                _output.WriteLine($"   ContinentId when in world: 0x{inWorldContinentId:X}");
            }

            Assert.True(sawValidInWorld, "Should have seen valid InWorld state with ContinentId < 0xFF");
        }
        finally
        {
            _fixture.CleanupProcess(processHandle, processId);
        }
    }

    /// <summary>
    /// Detects the current WoW screen state by reading memory values.
    /// Uses ContinentId as the primary discriminator for charselect states.
    /// </summary>
    private (WoWScreenState State, string Details) DetectScreenState(IntPtr processHandle)
    {
        var loginState = ReadString(processHandle, MemoryOffsets.LoginState, 32)?.Trim().ToLowerInvariant() ?? "";
        var managerBase = ReadUInt32(processHandle, MemoryOffsets.ManagerBase);
        var clientConnection = ReadUInt32(processHandle, MemoryOffsets.ClientConnection);
        var continentId = ReadUInt32(processHandle, MemoryOffsets.ContinentId);

        ulong playerGuid = 0;
        if (managerBase != 0)
        {
            playerGuid = ReadUInt64(processHandle, (int)managerBase + MemoryOffsets.PlayerGuidOffset);
        }

        var details = $"LoginState=\"{loginState}\", ContinentId=0x{continentId:X}, PlayerGuid={playerGuid}, Conn=0x{clientConnection:X}";

        // Use the same detection logic as ObjectManager.GetCurrentScreenState()
        if (loginState == "connecting")
        {
            return (WoWScreenState.Connecting, details);
        }

        if (loginState == "charcreate")
        {
            return (WoWScreenState.CharacterCreate, details);
        }

        if (loginState == "login")
        {
            return (WoWScreenState.LoginScreen, details);
        }

        // At this point, loginState should be "charselect"
        // Use ContinentId as the PRIMARY discriminator
        if (loginState == "charselect" || string.IsNullOrEmpty(loginState))
        {
            // ContinentId == 0xFFFFFFFF means we're NOT in any map
            if (continentId == 0xFFFFFFFF)
            {
                return (WoWScreenState.CharacterSelect, details);
            }

            // ContinentId == 0xFF (255) means loading bar is visible
            if (continentId == 0xFF)
            {
                return (WoWScreenState.LoadingWorld, details);
            }

            // ContinentId is a valid map ID (0=Eastern Kingdoms, 1=Kalimdor, etc.)
            if (continentId < 0xFF)
            {
                return (WoWScreenState.InWorld, details);
            }
        }

        return (WoWScreenState.Unknown, details);
    }

    /// <summary>
    /// Checks the diagnostic log files for evidence of successful login.
    /// </summary>
    private async Task<(bool Success, string? CharacterName, bool SnapshotSent)> CheckDiagnosticLogsAsync()
    {
        var wowDir = Environment.GetEnvironmentVariable("WWOW_GAME_CLIENT_PATH");
        if (string.IsNullOrEmpty(wowDir))
        {
            wowDir = _fixture.Configuration["GameClient:ExecutablePath"];
        }

        if (string.IsNullOrEmpty(wowDir))
        {
            return (false, null, false);
        }

        wowDir = Path.GetDirectoryName(wowDir);
        var logsDir = Path.Combine(wowDir!, "WWoWLogs");

        string? characterName = null;
        bool snapshotSent = false;

        // Check foreground_bot_debug.log
        var foregroundLog = Path.Combine(logsDir, "foreground_bot_debug.log");
        if (File.Exists(foregroundLog))
        {
            try
            {
                var content = await File.ReadAllTextAsync(foregroundLog);

                // Look for character name in snapshot
                var snapshotMatch = Regex.Match(content, @"FIRST snapshot for Account=.*?, Character=(\w+)");
                if (snapshotMatch.Success)
                {
                    characterName = snapshotMatch.Groups[1].Value;
                    snapshotSent = true;
                }

                // Also check for OnPlayerEnteredWorld
                if (content.Contains("OnPlayerEnteredWorld EVENT FIRED"))
                {
                    _output.WriteLine("   Found: OnPlayerEnteredWorld event fired");
                }

                // Look for ScreenState=InWorld
                if (content.Contains("ScreenState=InWorld"))
                {
                    _output.WriteLine("   Found: ScreenState=InWorld logged");
                }
            }
            catch (IOException)
            {
                // File may be in use
            }
        }

        // Check object_manager_debug.log
        var objectManagerLog = Path.Combine(logsDir, "object_manager_debug.log");
        if (File.Exists(objectManagerLog))
        {
            try
            {
                var content = await File.ReadAllTextAsync(objectManagerLog);

                if (content.Contains("ENTERED_WORLD"))
                {
                    _output.WriteLine("   Found: ENTERED_WORLD in ObjectManager log");
                }

                // Extract player name if found
                var playerMatch = Regex.Match(content, @"ENTERED_WORLD! Player=(\w+)");
                if (playerMatch.Success && string.IsNullOrEmpty(characterName))
                {
                    characterName = playerMatch.Groups[1].Value;
                }
            }
            catch (IOException)
            {
                // File may be in use
            }
        }

        return (characterName != null || snapshotSent, characterName, snapshotSent);
    }

    private bool HasProcessExited(IntPtr processHandle)
    {
        if (processHandle == IntPtr.Zero) return true;
        var result = WaitForSingleObject(processHandle, 0);
        return result == WAIT_OBJECT_0;
    }

    #region P/Invoke and Memory Reading

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
    private const uint WAIT_OBJECT_0 = 0x00000000;

    private string? ReadString(IntPtr processHandle, int address, int maxLength)
    {
        var buffer = new byte[maxLength];
        if (WinProcessImports.ReadProcessMemory(processHandle, (IntPtr)address, buffer, buffer.Length, out _))
        {
            var nullIndex = Array.IndexOf(buffer, (byte)0);
            if (nullIndex >= 0)
            {
                return Encoding.ASCII.GetString(buffer, 0, nullIndex);
            }
            return Encoding.ASCII.GetString(buffer);
        }
        return null;
    }

    private uint ReadUInt32(IntPtr processHandle, int address)
    {
        var buffer = new byte[4];
        if (WinProcessImports.ReadProcessMemory(processHandle, (IntPtr)address, buffer, 4, out _))
        {
            return BitConverter.ToUInt32(buffer, 0);
        }
        return 0;
    }

    private ulong ReadUInt64(IntPtr processHandle, int address)
    {
        var buffer = new byte[8];
        if (WinProcessImports.ReadProcessMemory(processHandle, (IntPtr)address, buffer, 8, out _))
        {
            return BitConverter.ToUInt64(buffer, 0);
        }
        return 0;
    }

    #endregion
}

/// <summary>
/// Test fixture for auto-login integration tests.
/// Manages WoW process lifecycle and StateManager coordination.
/// </summary>
public class AutoLoginTestFixture : IDisposable
{
    public IConfiguration Configuration { get; }

    private Process? _stateManagerProcess;
    private IntPtr _wowProcessHandle = IntPtr.Zero;
    private int _wowProcessId = 0;

    public AutoLoginTestFixture()
    {
        Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>
    /// Validates all prerequisites for the auto-login test.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> ValidatePrerequisitesAsync(ITestOutputHelper output)
    {
        // Check WoW path
        var wowPath = Environment.GetEnvironmentVariable("WWOW_GAME_CLIENT_PATH")
                      ?? Configuration["GameClient:ExecutablePath"];

        if (string.IsNullOrEmpty(wowPath) || !File.Exists(wowPath))
        {
            return (false, $"WoW.exe not found at: {wowPath}");
        }
        output.WriteLine($"   WoW.exe: {wowPath}");

        // Check Loader.dll path
        var loaderPath = Configuration["LoaderDllPath"];
        if (!string.IsNullOrEmpty(loaderPath) && !Path.IsPathRooted(loaderPath))
        {
            var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
            if (solutionRoot != null)
            {
                loaderPath = Path.Combine(solutionRoot, loaderPath);
            }
        }

        if (string.IsNullOrEmpty(loaderPath) || !File.Exists(loaderPath))
        {
            return (false, $"Loader.dll not found at: {loaderPath}. Run Setup-InjectionDlls.ps1");
        }
        output.WriteLine($"   Loader.dll: {loaderPath}");

        // Check ForegroundBotRunner.dll
        var loaderDir = Path.GetDirectoryName(loaderPath);
        var foregroundBotPath = Path.Combine(loaderDir!, "ForegroundBotRunner.dll");
        if (!File.Exists(foregroundBotPath))
        {
            return (false, $"ForegroundBotRunner.dll not found at: {foregroundBotPath}");
        }
        output.WriteLine($"   ForegroundBotRunner.dll: {foregroundBotPath}");

        // Check StateManager port
        var stateManagerPort = Configuration["CharacterStateListener:Port"] ?? "5002";
        output.WriteLine($"   StateManager port: {stateManagerPort}");

        return (true, null);
    }

    /// <summary>
    /// Ensures StateManager is running, starting it if necessary.
    /// </summary>
    public async Task EnsureStateManagerRunningAsync(ITestOutputHelper output)
    {
        var stateManagerIp = Configuration["CharacterStateListener:IpAddress"] ?? "127.0.0.1";
        var stateManagerPort = int.Parse(Configuration["CharacterStateListener:Port"] ?? "5002");

        // Check if StateManager is already running
        if (await IsPortOpenAsync(stateManagerIp, stateManagerPort))
        {
            output.WriteLine($"   StateManager already running at {stateManagerIp}:{stateManagerPort}");
            return;
        }

        output.WriteLine($"   Starting StateManager...");

        var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
        if (solutionRoot == null)
        {
            throw new InvalidOperationException("Could not find solution root");
        }

        var projectPath = Path.Combine("Services", "WoWStateManager", "WoWStateManager.csproj");
        var fullProjectPath = Path.Combine(solutionRoot, projectPath);

        if (!File.Exists(fullProjectPath))
        {
            throw new InvalidOperationException($"StateManager project not found at {fullProjectPath}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" -c Debug --no-launch-profile",
            WorkingDirectory = solutionRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.Environment["CharacterStateListener:IpAddress"] = stateManagerIp;
        psi.Environment["CharacterStateListener:Port"] = stateManagerPort.ToString();

        _stateManagerProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _stateManagerProcess.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                output.WriteLine($"   [StateManager] {e.Data}");
        };
        _stateManagerProcess.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                output.WriteLine($"   [StateManager-ERR] {e.Data}");
        };

        _stateManagerProcess.Start();
        _stateManagerProcess.BeginOutputReadLine();
        _stateManagerProcess.BeginErrorReadLine();

        // Wait for StateManager to start
        var timeout = TimeSpan.FromSeconds(60);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (await IsPortOpenAsync(stateManagerIp, stateManagerPort))
            {
                output.WriteLine($"   StateManager started successfully");
                return;
            }
            await Task.Delay(500);
        }

        throw new TimeoutException($"StateManager did not start within {timeout.TotalSeconds} seconds");
    }

    /// <summary>
    /// Launches WoW.exe and injects the bot DLL.
    /// </summary>
    public async Task<(IntPtr ProcessHandle, int ProcessId)> LaunchWoWWithInjectionAsync(ITestOutputHelper output)
    {
        var wowPath = Environment.GetEnvironmentVariable("WWOW_GAME_CLIENT_PATH")
                      ?? Configuration["GameClient:ExecutablePath"];

        if (string.IsNullOrEmpty(wowPath) || !File.Exists(wowPath))
        {
            return (IntPtr.Zero, 0);
        }

        // Start WoW process
        var psi = new ProcessStartInfo
        {
            FileName = wowPath,
            WorkingDirectory = Path.GetDirectoryName(wowPath),
            UseShellExecute = false
        };

        var process = Process.Start(psi);
        if (process == null)
        {
            output.WriteLine("   Failed to start WoW.exe");
            return (IntPtr.Zero, 0);
        }

        _wowProcessId = process.Id;
        output.WriteLine($"   WoW.exe started with PID: {_wowProcessId}");

        // Wait for process to initialize
        output.WriteLine("   Waiting for WoW to initialize...");
        await WoWProcessDetector.WaitForProcessReadyAsync(
            process,
            TimeSpan.FromSeconds(60),
            waitForLoginScreen: true,
            logger: msg => output.WriteLine($"   [WoWDetector] {msg}"));

        // Open process handle for memory reading
        _wowProcessHandle = WinProcessImports.OpenProcess(
            WinProcessImports.PROCESS_VM_READ | WinProcessImports.PROCESS_QUERY_INFORMATION,
            false,
            _wowProcessId);

        if (_wowProcessHandle == IntPtr.Zero)
        {
            output.WriteLine("   WARNING: Could not open process handle for memory reading");
        }

        // Inject the DLL
        var loaderPath = Configuration["LoaderDllPath"];
        if (!string.IsNullOrEmpty(loaderPath) && !Path.IsPathRooted(loaderPath))
        {
            var solutionRoot = FindSolutionRoot(Directory.GetCurrentDirectory());
            if (solutionRoot != null)
            {
                loaderPath = Path.Combine(solutionRoot, loaderPath);
            }
        }

        if (!string.IsNullOrEmpty(loaderPath) && File.Exists(loaderPath))
        {
            output.WriteLine($"   Injecting {loaderPath}...");

            var injectionSuccess = WinProcessImports.SafeInjection.InjectDllSafely(
                _wowProcessId,
                loaderPath,
                out var errorMessage);

            if (injectionSuccess)
            {
                output.WriteLine("   DLL injection successful");
            }
            else
            {
                output.WriteLine($"   DLL injection failed: {errorMessage}");

                // Don't fail - architecture mismatch is expected with 32-bit WoW
                if (errorMessage?.Contains("Architecture mismatch") == true)
                {
                    output.WriteLine("   NOTE: Architecture mismatch is expected with 32-bit WoW client");
                }
            }
        }

        // Give the injected DLL time to initialize
        await Task.Delay(3000);

        return (_wowProcessHandle, _wowProcessId);
    }

    /// <summary>
    /// Cleans up the WoW process.
    /// </summary>
    public void CleanupProcess(IntPtr processHandle, int processId)
    {
        if (processId > 0)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.Kill();
            }
            catch { }
        }

        if (processHandle != IntPtr.Zero)
        {
            WinProcessImports.CloseHandle(processHandle);
        }

        _wowProcessHandle = IntPtr.Zero;
        _wowProcessId = 0;
    }

    private static async Task<bool> IsPortOpenAsync(string ip, int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
            var connectTask = client.ConnectAsync(ip, port);
            await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, cts.Token));
            return connectTask.IsCompletedSuccessfully && client.Connected;
        }
        catch { return false; }
    }

    private static string? FindSolutionRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WestworldOfWarcraft.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, "Exports")) ||
                Directory.Exists(Path.Combine(dir.FullName, "Services")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }

    public void Dispose()
    {
        // Stop StateManager
        if (_stateManagerProcess != null && !_stateManagerProcess.HasExited)
        {
            try
            {
                _stateManagerProcess.Kill(true);
            }
            catch { }
            _stateManagerProcess.Dispose();
        }

        // Cleanup WoW process
        CleanupProcess(_wowProcessHandle, _wowProcessId);
    }
}
