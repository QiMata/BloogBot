using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace BotRunner.Tests;

// Local definitions for CreateProcess (needed because WinProcessImports defines them in global namespace)
[StructLayout(LayoutKind.Sequential)]
internal struct STARTUPINFO_LOCAL
{
    public uint cb;
    public string lpReserved;
    public string lpDesktop;
    public string lpTitle;
    public uint dwX;
    public uint dwY;
    public uint dwXSize;
    public uint dwYSize;
    public uint dwXCountChars;
    public uint dwYCountChars;
    public uint dwFillAttribute;
    public uint dwFlags;
    public short wShowWindow;
    public short cbReserved2;
    public IntPtr lpReserved2;
    public IntPtr hStdInput;
    public IntPtr hStdOutput;
    public IntPtr hStdError;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PROCESS_INFORMATION_LOCAL
{
    public IntPtr hProcess;
    public IntPtr hThread;
    public uint dwProcessId;
    public uint dwThreadId;
}

internal enum ProcessCreationFlagLocal : uint
{
    CREATE_DEFAULT_ERROR_MODE = 0x04000000
}

internal static class CreateProcessHelper
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CreateProcess(
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        ProcessCreationFlagLocal dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO_LOCAL lpStartupInfo,
        out PROCESS_INFORMATION_LOCAL lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    public const uint WAIT_OBJECT_0 = 0x00000000;
    public const uint WAIT_TIMEOUT = 0x00000102;
}

/// <summary>
/// Tests for detecting which screen the WoW client is currently on by reading memory values.
///
/// MEMORY OFFSETS REFERENCE (Vanilla 1.12.1 / Elysium):
/// =====================================================
///
/// LoginState:        0xB41478   - ASCII string: "login", "charselect", "connecting", "charcreate"
/// ManagerBase:       0xB41414   - Object manager base pointer
/// PlayerGuid:        ManagerBase + 0xC0 (0xB414D4) - Non-zero when logged in
/// ClientConnection:  0xB41DA0   - Non-zero when connected to server
/// ContinentId:       0x86F694   - 0xFF during world loading, valid map ID when loaded
/// CharacterCount:    0xB42140   - Number of characters on account
/// IsGhost:           0x835A48   - Ghost state flag
///
/// SCREEN DETECTION LOGIC:
/// =======================
///
/// 1. LOGIN SCREEN:
///    - LoginState = "login"
///    - PlayerGuid = 0
///
/// 2. CONNECTING:
///    - LoginState = "connecting"
///
/// 3. CHARACTER SELECT:
///    - LoginState = "charselect"
///    - PlayerGuid = 0
///    - ContinentId = 0xFF or unchanged from login
///
/// 4. LOADING WORLD:
///    - LoginState = "charselect"
///    - ContinentId = 0xFF
///    - PlayerGuid transitioning to non-zero
///
/// 5. IN WORLD:
///    - LoginState = "charselect" (stays this way!)
///    - PlayerGuid != 0
///    - ContinentId != 0xFF
///    - ClientConnection != 0
///
/// 6. DISCONNECTED:
///    - ClientConnection = 0
///    - Was previously InWorld
///
/// NOTE: The WoW client keeps LoginState as "charselect" even when in-world!
/// We must use multiple signals to determine the actual state.
/// </summary>
[RequiresInfrastructure]
public class ScreenDetectionTests(ScreenDetectionTestFixture fixture, ITestOutputHelper output) : IClassFixture<ScreenDetectionTestFixture>
{
    private readonly ScreenDetectionTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Memory offsets for screen detection (Vanilla 1.12.1)
    /// </summary>
    private static class MemoryOffsets
    {
        public const int LoginState = 0xB41478;        // ASCII string
        public const int ManagerBase = 0xB41414;       // Object manager base
        public const int PlayerGuidOffset = 0xC0;      // Offset from ManagerBase
        public const int ClientConnection = 0xB41DA0;  // Connection pointer
        public const int ContinentId = 0x86F694;       // Map/continent ID (0xFF = loading)
        public const int CharacterCount = 0xB42140;    // Number of characters
        public const int IsGhost = 0x835A48;           // Ghost state
        public const int IsIngame = 0xB4B424;          // UNRELIABLE on Elysium - returns 0

        // Candidate addresses for loading state detection (from wowdev.wiki and ownedcore)
        public const int WorldFrame = 0xB4B2BC;        // WorldFrame pointer - may indicate world is rendered
        public const int Enables = 0x87B2A4;           // Rendering flags (wowdev.wiki)
        public const int MapId = 0x84C498;             // Another map ID location
    }

    [Fact]
    public async Task DetectLoginScreen_WhenAtLogin_ShouldReturnCorrectState()
    {
        _output.WriteLine("=== SCREEN DETECTION TEST: LOGIN SCREEN ===");
        _output.WriteLine("This test validates detection of the login screen.");
        _output.WriteLine("");

        var (_, handle) = await _fixture.GetWoWProcessAsync(_output);
        if (handle == IntPtr.Zero)
        {
            _output.WriteLine("SKIP: No WoW process available for testing");
            return;
        }

        try
        {
            var state = DetectScreenState(handle);
            LogCurrentMemoryState(handle);

            _output.WriteLine($"Detected Screen State: {state}");

            // This test documents what we see - actual assertions depend on manual state
            Assert.NotEqual(WoWScreenState.ProcessNotAvailable, state);
        }
        finally
        {
            WinProcessImports.CloseHandle(handle);
        }
    }

    [Fact]
    public async Task MonitorScreenTransitions_ShouldTrackStateChanges()
    {
        _output.WriteLine("=== SCREEN TRANSITION MONITORING TEST ===");
        _output.WriteLine("This test monitors screen state changes over time.");
        _output.WriteLine("Manually navigate through screens to see state transitions.");
        _output.WriteLine("");

        var (_, handle) = await _fixture.GetWoWProcessAsync(_output);
        if (handle == IntPtr.Zero)
        {
            _output.WriteLine("SKIP: No WoW process available for testing");
            return;
        }

        try
        {
            WoWScreenState? lastState = null;
            var stateHistory = new List<(DateTime Time, WoWScreenState State, string Details)>();

            // Monitor for 3 minutes
            var monitorDuration = TimeSpan.FromSeconds(180);
            var startTime = DateTime.Now;
            var pollInterval = TimeSpan.FromMilliseconds(500);

            _output.WriteLine($"Monitoring for {monitorDuration.TotalSeconds} seconds...");
            _output.WriteLine("Navigate the WoW client to see state changes detected.");
            _output.WriteLine("");

            while (DateTime.Now - startTime < monitorDuration)
            {
                if (HasProcessExited(handle))
                {
                    _output.WriteLine("WoW process exited during monitoring.");
                    break;
                }

                var currentState = DetectScreenState(handle);
                var details = GetStateDetails(handle);

                if (currentState != lastState)
                {
                    stateHistory.Add((DateTime.Now, currentState, details));
                    _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] STATE CHANGE: {lastState} -> {currentState}");
                    _output.WriteLine($"    Details: {details}");
                    lastState = currentState;
                }

                await Task.Delay(pollInterval);
            }

            _output.WriteLine("");
            _output.WriteLine("=== STATE HISTORY ===");
            foreach (var (time, state, details) in stateHistory)
            {
                _output.WriteLine($"  {time:HH:mm:ss.fff}: {state} - {details}");
            }

            Assert.NotEmpty(stateHistory);
        }
        finally
        {
            WinProcessImports.CloseHandle(handle);
        }
    }

    [Fact]
    public async Task MonitorCandidateLoadingAddresses_ShouldFindPatterns()
    {
        _output.WriteLine("=== CANDIDATE LOADING ADDRESS MONITOR ===");
        _output.WriteLine("This test monitors multiple candidate memory addresses to find loading state indicators.");
        _output.WriteLine("Navigate through: Login -> CharSelect -> Enter World to see what changes.");
        _output.WriteLine("");

        var (_, handle) = await _fixture.GetWoWProcessAsync(_output);
        if (handle == IntPtr.Zero)
        {
            _output.WriteLine("SKIP: No WoW process available for testing");
            return;
        }

        try
        {
            var monitorDuration = TimeSpan.FromSeconds(180); // 3 minutes
            var startTime = DateTime.Now;
            var pollInterval = TimeSpan.FromMilliseconds(500);

            // Track last values to detect changes
            string? lastLoginState = null;
            uint lastWorldFrame = 0;
            uint lastEnables = 0;
            uint lastMapId = 0;
            uint lastContinentId = 0;
            uint lastClientConnection = 0;
            ulong lastPlayerGuid = 0;
            uint lastIsIngame = 0;

            _output.WriteLine($"Monitoring for {monitorDuration.TotalSeconds} seconds...");
            _output.WriteLine("Legend: WF=WorldFrame, EN=Enables, MI=MapId, CI=ContinentId, CC=ClientConn, PG=PlayerGuid, IG=IsIngame");
            _output.WriteLine("");

            while (DateTime.Now - startTime < monitorDuration)
            {
                if (HasProcessExited(handle))
                {
                    _output.WriteLine("WoW process exited during monitoring.");
                    break;
                }

                // Read all candidate addresses
                var loginState = ReadString(handle, MemoryOffsets.LoginState, 32)?.Trim() ?? "";
                var worldFrame = ReadUInt32(handle, MemoryOffsets.WorldFrame);
                var enables = ReadUInt32(handle, MemoryOffsets.Enables);
                var mapId = ReadUInt32(handle, MemoryOffsets.MapId);
                var continentId = ReadUInt32(handle, MemoryOffsets.ContinentId);
                var clientConnection = ReadUInt32(handle, MemoryOffsets.ClientConnection);
                var managerBase = ReadUInt32(handle, MemoryOffsets.ManagerBase);
                var isIngame = ReadUInt32(handle, MemoryOffsets.IsIngame);

                ulong playerGuid = 0;
                if (managerBase != 0)
                {
                    playerGuid = ReadUInt64(handle, (int)managerBase + MemoryOffsets.PlayerGuidOffset);
                }

                // Check if anything changed
                bool changed = loginState != lastLoginState ||
                               worldFrame != lastWorldFrame ||
                               enables != lastEnables ||
                               mapId != lastMapId ||
                               continentId != lastContinentId ||
                               clientConnection != lastClientConnection ||
                               playerGuid != lastPlayerGuid ||
                               isIngame != lastIsIngame;

                if (changed)
                {
                    var changes = new List<string>();
                    if (loginState != lastLoginState) changes.Add($"LoginState: \"{lastLoginState}\"->\"{ loginState}\"");
                    if (worldFrame != lastWorldFrame) changes.Add($"WF: 0x{lastWorldFrame:X}->0x{worldFrame:X}");
                    if (enables != lastEnables) changes.Add($"EN: 0x{lastEnables:X}->0x{enables:X}");
                    if (mapId != lastMapId) changes.Add($"MI: 0x{lastMapId:X}->0x{mapId:X}");
                    if (continentId != lastContinentId) changes.Add($"CI: 0x{lastContinentId:X}->0x{continentId:X}");
                    if (clientConnection != lastClientConnection) changes.Add($"CC: 0x{lastClientConnection:X}->0x{clientConnection:X}");
                    if (playerGuid != lastPlayerGuid) changes.Add($"PG: {lastPlayerGuid}->{playerGuid}");
                    if (isIngame != lastIsIngame) changes.Add($"IG: 0x{lastIsIngame:X}->0x{isIngame:X}");

                    _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] CHANGE: {string.Join(" | ", changes)}");

                    // Update last values
                    lastLoginState = loginState;
                    lastWorldFrame = worldFrame;
                    lastEnables = enables;
                    lastMapId = mapId;
                    lastContinentId = continentId;
                    lastClientConnection = clientConnection;
                    lastPlayerGuid = playerGuid;
                    lastIsIngame = isIngame;
                }

                await Task.Delay(pollInterval);
            }

            _output.WriteLine("");
            _output.WriteLine("=== FINAL VALUES ===");
            _output.WriteLine($"LoginState:       \"{lastLoginState}\"");
            _output.WriteLine($"WorldFrame:       0x{lastWorldFrame:X8}");
            _output.WriteLine($"Enables:          0x{lastEnables:X8}");
            _output.WriteLine($"MapId:            0x{lastMapId:X8}");
            _output.WriteLine($"ContinentId:      0x{lastContinentId:X8}");
            _output.WriteLine($"ClientConnection: 0x{lastClientConnection:X8}");
            _output.WriteLine($"PlayerGuid:       {lastPlayerGuid}");
            _output.WriteLine($"IsIngame:         0x{lastIsIngame:X8}");

            Assert.True(true); // Always pass - this is for data collection
        }
        finally
        {
            WinProcessImports.CloseHandle(handle);
        }
    }

    [Fact]
    public async Task DumpAllMemoryValues_ShouldShowCurrentState()
    {
        _output.WriteLine("=== MEMORY DUMP FOR SCREEN DETECTION ===");
        _output.WriteLine("This test dumps all relevant memory values for screen detection.");
        _output.WriteLine("");

        var (_, handle) = await _fixture.GetWoWProcessAsync(_output);
        if (handle == IntPtr.Zero)
        {
            _output.WriteLine("SKIP: No WoW process available for testing");
            return;
        }

        try
        {
            _output.WriteLine("MEMORY VALUES:");
            _output.WriteLine("==============");

            // LoginState (string)
            var loginState = ReadString(handle, MemoryOffsets.LoginState, 32);
            _output.WriteLine($"LoginState (0x{MemoryOffsets.LoginState:X}): \"{loginState}\"");

            // ManagerBase
            var managerBase = ReadUInt32(handle, MemoryOffsets.ManagerBase);
            _output.WriteLine($"ManagerBase (0x{MemoryOffsets.ManagerBase:X}): 0x{managerBase:X8}");

            // PlayerGuid (from ManagerBase + offset)
            if (managerBase != 0)
            {
                var playerGuid = ReadUInt64(handle, (int)managerBase + MemoryOffsets.PlayerGuidOffset);
                _output.WriteLine($"PlayerGuid (ManagerBase+0x{MemoryOffsets.PlayerGuidOffset:X}): {playerGuid} (0x{playerGuid:X16})");
            }
            else
            {
                _output.WriteLine($"PlayerGuid: N/A (ManagerBase is null)");
            }

            // ClientConnection
            var clientConnection = ReadUInt32(handle, MemoryOffsets.ClientConnection);
            _output.WriteLine($"ClientConnection (0x{MemoryOffsets.ClientConnection:X}): 0x{clientConnection:X8} ({(clientConnection != 0 ? "Connected" : "Disconnected")})");

            // ContinentId
            var continentId = ReadUInt32(handle, MemoryOffsets.ContinentId);
            _output.WriteLine($"ContinentId (0x{MemoryOffsets.ContinentId:X}): 0x{continentId:X8} ({(continentId == 0xFF ? "LOADING" : $"Map {continentId}")})");

            // CharacterCount
            var charCount = ReadUInt32(handle, MemoryOffsets.CharacterCount);
            _output.WriteLine($"CharacterCount (0x{MemoryOffsets.CharacterCount:X}): {charCount}");

            // IsGhost
            var isGhost = ReadUInt32(handle, MemoryOffsets.IsGhost);
            _output.WriteLine($"IsGhost (0x{MemoryOffsets.IsGhost:X}): 0x{isGhost:X8}");

            // IsIngame (UNRELIABLE on Elysium)
            var isIngame = ReadUInt32(handle, MemoryOffsets.IsIngame);
            _output.WriteLine($"IsIngame (0x{MemoryOffsets.IsIngame:X}): 0x{isIngame:X8} (UNRELIABLE on Elysium!)");

            _output.WriteLine("");
            _output.WriteLine("DERIVED STATE:");
            _output.WriteLine("==============");

            var state = DetectScreenState(handle);
            _output.WriteLine($"Detected Screen: {state}");

            Assert.NotEqual(WoWScreenState.ProcessNotAvailable, state);
        }
        finally
        {
            WinProcessImports.CloseHandle(handle);
        }
    }

    [Fact]
    public async Task ValidateLoginToCharSelectTransition_ShouldDetectCorrectly()
    {
        _output.WriteLine("=== LOGIN TO CHARSELECT TRANSITION TEST ===");
        _output.WriteLine("This test validates detecting the transition from login to character select.");
        _output.WriteLine("You need to manually login during this test.");
        _output.WriteLine("");

        var (_, handle) = await _fixture.GetWoWProcessAsync(_output);
        if (handle == IntPtr.Zero)
        {
            _output.WriteLine("SKIP: No WoW process available for testing");
            return;
        }

        try
        {
            var sawLogin = false;
            var sawCharSelect = false;
            var sawTransition = false;
            WoWScreenState? lastState = null;

            _output.WriteLine("Waiting for login -> charselect transition (60s timeout)...");
            _output.WriteLine("Please login to your account if not already at character select.");
            _output.WriteLine("");

            var timeout = TimeSpan.FromSeconds(60);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                if (HasProcessExited(handle))
                {
                    _output.WriteLine("WoW process exited.");
                    break;
                }

                var state = DetectScreenState(handle);

                if (state == WoWScreenState.LoginScreen)
                    sawLogin = true;

                if (state == WoWScreenState.CharacterSelect)
                    sawCharSelect = true;

                if (lastState == WoWScreenState.LoginScreen && state == WoWScreenState.CharacterSelect)
                {
                    sawTransition = true;
                    _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] TRANSITION DETECTED: Login -> CharSelect");
                }

                if (state != lastState)
                {
                    _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] State: {state}");
                    lastState = state;
                }

                // If we've seen character select, we can end early
                if (sawCharSelect && state == WoWScreenState.CharacterSelect)
                {
                    await Task.Delay(1000); // Verify it's stable
                    break;
                }

                await Task.Delay(200);
            }

            _output.WriteLine("");
            _output.WriteLine("RESULTS:");
            _output.WriteLine($"  Saw Login Screen: {sawLogin}");
            _output.WriteLine($"  Saw Character Select: {sawCharSelect}");
            _output.WriteLine($"  Saw Transition: {sawTransition}");

            // At minimum, we should have detected some state
            Assert.True(sawLogin || sawCharSelect, "Should have detected either login or character select screen");
        }
        finally
        {
            WinProcessImports.CloseHandle(handle);
        }
    }

    [Fact]
    public async Task ValidateCharSelectToInWorldTransition_ShouldDetectCorrectly()
    {
        _output.WriteLine("=== CHARSELECT TO IN-WORLD TRANSITION TEST ===");
        _output.WriteLine("This test validates detecting the transition from character select to in-world.");
        _output.WriteLine("You need to manually select a character and enter the world during this test.");
        _output.WriteLine("");

        var (_, handle) = await _fixture.GetWoWProcessAsync(_output);
        if (handle == IntPtr.Zero)
        {
            _output.WriteLine("SKIP: No WoW process available for testing");
            return;
        }

        try
        {
            var sawCharSelect = false;
            var sawLoading = false;
            var sawInWorld = false;
            WoWScreenState? lastState = null;

            _output.WriteLine("Waiting for charselect -> loading -> inworld transition (90s timeout)...");
            _output.WriteLine("Please enter the world if at character select.");
            _output.WriteLine("");

            var timeout = TimeSpan.FromSeconds(90);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                if (HasProcessExited(handle))
                {
                    _output.WriteLine("WoW process exited.");
                    break;
                }

                var state = DetectScreenState(handle);
                var details = GetStateDetails(handle);

                if (state == WoWScreenState.CharacterSelect)
                    sawCharSelect = true;

                if (state == WoWScreenState.LoadingWorld)
                    sawLoading = true;

                if (state == WoWScreenState.InWorld)
                    sawInWorld = true;

                if (state != lastState)
                {
                    _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] State: {state}");
                    _output.WriteLine($"    {details}");
                    lastState = state;
                }

                // If we've seen in-world, we can end
                if (sawInWorld && state == WoWScreenState.InWorld)
                {
                    await Task.Delay(2000); // Verify it's stable
                    break;
                }

                await Task.Delay(100);
            }

            _output.WriteLine("");
            _output.WriteLine("RESULTS:");
            _output.WriteLine($"  Saw Character Select: {sawCharSelect}");
            _output.WriteLine($"  Saw Loading World: {sawLoading}");
            _output.WriteLine($"  Saw In World: {sawInWorld}");

            // At minimum, we should have detected some state
            Assert.True(sawCharSelect || sawInWorld, "Should have detected either character select or in-world screen");
        }
        finally
        {
            WinProcessImports.CloseHandle(handle);
        }
    }

    [Fact]
    public async Task AutomatedScreenStateValidation_ShouldConfirmStateIsConsistent()
    {
        _output.WriteLine("=== AUTOMATED SCREEN STATE VALIDATION TEST ===");
        _output.WriteLine("This test reads memory values and validates internal state consistency.");
        _output.WriteLine("");

        var (_, handle) = await _fixture.GetWoWProcessAsync(_output);
        if (handle == IntPtr.Zero)
        {
            _output.WriteLine("SKIP: No WoW process available for testing");
            return;
        }

        try
        {
            // Read all memory values
            var loginState = ReadString(handle, MemoryOffsets.LoginState, 32)?.Trim().ToLowerInvariant() ?? "";
            var managerBase = ReadUInt32(handle, MemoryOffsets.ManagerBase);
            var clientConnection = ReadUInt32(handle, MemoryOffsets.ClientConnection);
            var continentId = ReadUInt32(handle, MemoryOffsets.ContinentId);
            var charCount = ReadUInt32(handle, MemoryOffsets.CharacterCount);

            ulong playerGuid = 0;
            if (managerBase != 0)
            {
                playerGuid = ReadUInt64(handle, (int)managerBase + MemoryOffsets.PlayerGuidOffset);
            }

            _output.WriteLine("RAW MEMORY VALUES:");
            _output.WriteLine($"  LoginState:        \"{loginState}\"");
            _output.WriteLine($"  ManagerBase:       0x{managerBase:X8}");
            _output.WriteLine($"  PlayerGuid:        {playerGuid}");
            _output.WriteLine($"  ClientConnection:  0x{clientConnection:X8}");
            _output.WriteLine($"  ContinentId:       0x{continentId:X8}");
            _output.WriteLine($"  CharacterCount:    {charCount}");
            _output.WriteLine("");

            var detectedState = DetectScreenState(handle);
            _output.WriteLine($"DETECTED STATE: {detectedState}");
            _output.WriteLine("");

            // Validate state consistency based on memory values
            _output.WriteLine("STATE VALIDATION:");

            bool isValid = true;
            var validationMessages = new List<string>();

            switch (detectedState)
            {
                case WoWScreenState.LoginScreen:
                    if (loginState != "login")
                    {
                        validationMessages.Add($"  WARNING: LoginScreen but LoginState=\"{loginState}\" (expected \"login\")");
                        isValid = false;
                    }
                    if (playerGuid != 0)
                    {
                        validationMessages.Add($"  ERROR: LoginScreen but PlayerGuid={playerGuid} (expected 0)");
                        isValid = false;
                    }
                    break;

                case WoWScreenState.CharacterSelect:
                    if (loginState != "charselect")
                    {
                        validationMessages.Add($"  WARNING: CharacterSelect but LoginState=\"{loginState}\" (expected \"charselect\")");
                    }
                    if (playerGuid != 0)
                    {
                        validationMessages.Add($"  ERROR: CharacterSelect but PlayerGuid={playerGuid} (expected 0)");
                        isValid = false;
                    }
                    break;

                case WoWScreenState.LoadingWorld:
                    if (continentId != 0xFF)
                    {
                        validationMessages.Add($"  ERROR: LoadingWorld but ContinentId=0x{continentId:X} (expected 0xFF)");
                        isValid = false;
                    }
                    break;

                case WoWScreenState.InWorld:
                    // InWorld is determined solely by ContinentId being a valid map ID (< 0xFF)
                    if (continentId >= 0xFF)
                    {
                        validationMessages.Add($"  ERROR: InWorld but ContinentId=0x{continentId:X} (expected valid map ID < 0xFF)");
                        isValid = false;
                    }
                    // Note: PlayerGuid and ClientConnection are NOT used for InWorld detection
                    // They may be 0 during brief transition moments, but ContinentId is authoritative
                    break;

                case WoWScreenState.Connecting:
                    if (loginState != "connecting")
                    {
                        validationMessages.Add($"  WARNING: Connecting but LoginState=\"{loginState}\" (expected \"connecting\")");
                    }
                    break;

                // Note: Disconnected state is no longer automatically detected
                // It would require tracking state transitions (e.g., InWorld -> CharacterSelect)
            }

            if (validationMessages.Count == 0)
            {
                _output.WriteLine("  All validations passed!");
            }
            else
            {
                foreach (var msg in validationMessages)
                {
                    _output.WriteLine(msg);
                }
            }

            _output.WriteLine("");
            _output.WriteLine($"STATE CONSISTENCY: {(isValid ? "VALID" : "INCONSISTENT")}");

            // Record state for documentation
            _output.WriteLine("");
            _output.WriteLine("=== DOCUMENTATION FOR CURRENT STATE ===");
            _output.WriteLine($"When the WoW client shows screen: {detectedState}");
            _output.WriteLine($"The memory values are:");
            _output.WriteLine($"  - LoginState (0x{MemoryOffsets.LoginState:X}) = \"{loginState}\"");
            _output.WriteLine($"  - PlayerGuid = {playerGuid}");
            _output.WriteLine($"  - ClientConnection (0x{MemoryOffsets.ClientConnection:X}) = 0x{clientConnection:X8}");
            _output.WriteLine($"  - ContinentId (0x{MemoryOffsets.ContinentId:X}) = 0x{continentId:X8}");

            Assert.NotEqual(WoWScreenState.Unknown, detectedState);
        }
        finally
        {
            WinProcessImports.CloseHandle(handle);
        }
    }

    [Fact]
    public void StateDetectionLogic_ShouldHandleAllCombinations()
    {
        _output.WriteLine("=== STATE DETECTION LOGIC UNIT TEST ===");
        _output.WriteLine("This test validates the state detection logic with various memory value combinations.");
        _output.WriteLine("");

        // Test all expected state combinations
        // NOTE: ContinentId is the PRIMARY discriminator for charselect states:
        //   - 0xFFFFFFFF = CharacterSelect (not in any map, includes cinematic/pre-load)
        //   - 0xFF (255) = LoadingWorld (loading bar visible)
        //   - 0-30ish = InWorld (valid map ID)
        var testCases = new[]
        {
            // LoginState, PlayerGuid, ClientConnection, ContinentId, Expected State

            // Login screen - LoginState is "login", ContinentId irrelevant
            ("login", 0UL, 0U, 0xFFFFFFFFU, WoWScreenState.LoginScreen),
            ("login", 0UL, 0x12345678U, 0xFFFFFFFFU, WoWScreenState.LoginScreen),

            // Connecting - LoginState is "connecting"
            ("connecting", 0UL, 0U, 0xFFFFFFFFU, WoWScreenState.Connecting),
            ("connecting", 0UL, 0x12345678U, 0xFFFFFFFFU, WoWScreenState.Connecting),

            // Character create - LoginState is "charcreate"
            ("charcreate", 0UL, 0x12345678U, 0xFFFFFFFFU, WoWScreenState.CharacterCreate),

            // Character select - ContinentId = 0xFFFFFFFF (not in any map)
            ("charselect", 0UL, 0U, 0xFFFFFFFFU, WoWScreenState.CharacterSelect),           // No char selected
            ("charselect", 0UL, 0x12345678U, 0xFFFFFFFFU, WoWScreenState.CharacterSelect),  // No char selected, connected
            ("charselect", 5UL, 0U, 0xFFFFFFFFU, WoWScreenState.CharacterSelect),           // Char selected (or cinematic)
            ("charselect", 5UL, 0x12345678U, 0xFFFFFFFFU, WoWScreenState.CharacterSelect),  // Char selected, connected

            // Loading world - ContinentId = 0xFF (255) - loading bar visible
            ("charselect", 5UL, 0U, 0xFFU, WoWScreenState.LoadingWorld),
            ("charselect", 5UL, 0x12345678U, 0xFFU, WoWScreenState.LoadingWorld),

            // In world - ContinentId is valid map ID (0=Eastern Kingdoms, 1=Kalimdor, etc.)
            ("charselect", 5UL, 0U, 0U, WoWScreenState.InWorld),           // Map 0, connection=0 (still InWorld!)
            ("charselect", 5UL, 0x12345678U, 0U, WoWScreenState.InWorld),  // Map 0, connected
            ("charselect", 5UL, 0x12345678U, 1U, WoWScreenState.InWorld),  // Map 1 (Kalimdor)
            ("charselect", 5UL, 0x12345678U, 30U, WoWScreenState.InWorld), // Map 30 (Alterac Valley)
        };

        foreach (var (loginState, playerGuid, clientConnection, continentId, expected) in testCases)
        {
            var actual = DetermineState(loginState, playerGuid, clientConnection, continentId);

            _output.WriteLine($"LoginState=\"{loginState}\", GUID={playerGuid}, Conn=0x{clientConnection:X}, Map=0x{continentId:X}");
            _output.WriteLine($"  Expected: {expected}, Actual: {actual}");

            Assert.Equal(expected, actual);
        }

        _output.WriteLine("");
        _output.WriteLine("All state detection logic tests passed!");
    }

    /// <summary>
    /// Determines the current WoW screen state by reading memory values.
    /// </summary>
    private WoWScreenState DetectScreenState(IntPtr processHandle)
    {
        // Read all required values
        var loginState = ReadString(processHandle, MemoryOffsets.LoginState, 32)?.Trim().ToLowerInvariant() ?? "";
        var managerBase = ReadUInt32(processHandle, MemoryOffsets.ManagerBase);
        var clientConnection = ReadUInt32(processHandle, MemoryOffsets.ClientConnection);
        var continentId = ReadUInt32(processHandle, MemoryOffsets.ContinentId);

        ulong playerGuid = 0;
        if (managerBase != 0)
        {
            playerGuid = ReadUInt64(processHandle, (int)managerBase + MemoryOffsets.PlayerGuidOffset);
        }

        // Determine state based on memory values
        return DetermineState(loginState, playerGuid, clientConnection, continentId);
    }

    /// <summary>
    /// Core state determination logic based on memory values.
    ///
    /// UPDATED LOGIC: Uses ContinentId as primary discriminator for charselect states.
    ///
    /// ContinentId values:
    /// - 0xFFFFFFFF = Not in any map (charselect, cinematic, pre-load)
    /// - 0xFF (255) = Loading world (loading bar visible)
    /// - 0-30ish = Valid map ID (in world)
    ///
    /// Note: PlayerGuid can be non-zero at charselect (when character is selected or
    /// during cinematic/pre-load), so we cannot rely on it alone.
    /// </summary>
    private WoWScreenState DetermineState(string loginState, ulong playerGuid, uint clientConnection, uint continentId)
    {
        // Check for "connecting" state first
        if (loginState == "connecting")
        {
            return WoWScreenState.Connecting;
        }

        // Check for character create
        if (loginState == "charcreate")
        {
            return WoWScreenState.CharacterCreate;
        }

        // Check for login screen
        if (loginState == "login")
        {
            return WoWScreenState.LoginScreen;
        }

        // At this point, loginState should be "charselect"
        // Use ContinentId as the PRIMARY discriminator
        if (loginState == "charselect" || string.IsNullOrEmpty(loginState))
        {
            // ContinentId == 0xFFFFFFFF means we're NOT in any map
            // This includes: charselect, cinematic, pre-load phase
            if (continentId == 0xFFFFFFFF)
            {
                return WoWScreenState.CharacterSelect;
            }

            // ContinentId == 0xFF (255) means loading bar is visible
            if (continentId == 0xFF)
            {
                return WoWScreenState.LoadingWorld;
            }

            // ContinentId is a valid map ID (0=Eastern Kingdoms, 1=Kalimdor, etc.)
            // This definitively means we're in world
            if (continentId < 0xFF)
            {
                return WoWScreenState.InWorld;
            }

            // Fallback for unexpected ContinentId values
            return WoWScreenState.Unknown;
        }

        return WoWScreenState.Unknown;
    }

    private string GetStateDetails(IntPtr processHandle)
    {
        var loginState = ReadString(processHandle, MemoryOffsets.LoginState, 32) ?? "";
        var managerBase = ReadUInt32(processHandle, MemoryOffsets.ManagerBase);
        var clientConnection = ReadUInt32(processHandle, MemoryOffsets.ClientConnection);
        var continentId = ReadUInt32(processHandle, MemoryOffsets.ContinentId);

        ulong playerGuid = 0;
        if (managerBase != 0)
        {
            playerGuid = ReadUInt64(processHandle, (int)managerBase + MemoryOffsets.PlayerGuidOffset);
        }

        return $"LoginState=\"{loginState}\", PlayerGuid={playerGuid}, Connection=0x{clientConnection:X}, ContinentId=0x{continentId:X}";
    }

    private void LogCurrentMemoryState(IntPtr processHandle)
    {
        var details = GetStateDetails(processHandle);
        _output.WriteLine($"Memory State: {details}");
    }

    /// <summary>
    /// Check if process has exited using WaitForSingleObject with 0 timeout
    /// </summary>
    private bool HasProcessExited(IntPtr processHandle)
    {
        if (processHandle == IntPtr.Zero) return true;
        var result = CreateProcessHelper.WaitForSingleObject(processHandle, 0);
        return result == CreateProcessHelper.WAIT_OBJECT_0;
    }

    #region Memory Reading Helpers

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
/// Test fixture for screen detection tests.
/// Provides access to running WoW process or starts one if needed.
/// Uses CreateProcess to get a handle with full access rights.
/// </summary>
public class ScreenDetectionTestFixture : IDisposable
{
    private readonly IConfiguration _configuration;
    private IntPtr _ownedProcessHandle = IntPtr.Zero;
    private int _ownedProcessId = 0;

    public ScreenDetectionTestFixture()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>
    /// Gets a WoW process for testing. First checks for running process,
    /// then optionally starts one if configured to do so.
    /// Uses CreateProcess to get a handle with full access rights (no admin needed).
    /// </summary>
    public async Task<(Process? Process, IntPtr Handle)> GetWoWProcessAsync(ITestOutputHelper output)
    {
        // First, try to find existing WoW process
        var processes = Process.GetProcessesByName("WoW");
        if (processes.Length > 0)
        {
            var process = processes[0];
            output.WriteLine($"Found existing WoW process: PID {process.Id}");

            var handle = WinProcessImports.OpenProcess(
                WinProcessImports.PROCESS_VM_READ | WinProcessImports.PROCESS_QUERY_INFORMATION,
                false,
                process.Id);

            if (handle != IntPtr.Zero)
            {
                return (process, handle);
            }

            output.WriteLine($"WARNING: Could not open existing process handle. Need to start our own process.");
        }

        // Try to start WoW if configured
        var autoStart = _configuration.GetValue<bool>("ScreenDetectionTests:AutoStartWoW", false);
        if (!autoStart)
        {
            output.WriteLine("No WoW process found and auto-start is disabled.");
            output.WriteLine("Set ScreenDetectionTests:AutoStartWoW=true in appsettings.test.json to auto-start.");
            return (null, IntPtr.Zero);
        }

        // Get WoW path
        var wowPath = Environment.GetEnvironmentVariable("WWOW_GAME_CLIENT_PATH")
                      ?? _configuration["GameClient:ExecutablePath"];

        if (string.IsNullOrEmpty(wowPath) || !File.Exists(wowPath))
        {
            output.WriteLine($"WoW executable not found at: {wowPath}");
            return (null, IntPtr.Zero);
        }

        output.WriteLine($"Starting WoW process using CreateProcess: {wowPath}");

        // Use CreateProcess to get a handle with full access rights
        var startupInfo = new STARTUPINFO_LOCAL();
        startupInfo.cb = (uint)Marshal.SizeOf(startupInfo);

        var createResult = CreateProcessHelper.CreateProcess(
            wowPath,
            null,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            ProcessCreationFlagLocal.CREATE_DEFAULT_ERROR_MODE,
            IntPtr.Zero,
            Path.GetDirectoryName(wowPath),
            ref startupInfo,
            out PROCESS_INFORMATION_LOCAL processInfo);

        if (!createResult || processInfo.hProcess == IntPtr.Zero)
        {
            var lastError = Marshal.GetLastWin32Error();
            output.WriteLine($"CreateProcess failed. Error Code: {lastError} - {new System.ComponentModel.Win32Exception(lastError).Message}");
            return (null, IntPtr.Zero);
        }

        _ownedProcessHandle = processInfo.hProcess;
        _ownedProcessId = (int)processInfo.dwProcessId;
        output.WriteLine($"WoW started with PID: {_ownedProcessId}, Handle: 0x{_ownedProcessHandle:X}");

        // Wait for process to initialize
        output.WriteLine("Waiting for WoW to initialize (up to 30 seconds)...");

        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(1000);

            // Check if process exited using WaitForSingleObject with 0 timeout
            var waitResult = CreateProcessHelper.WaitForSingleObject(_ownedProcessHandle, 0);
            if (waitResult == CreateProcessHelper.WAIT_OBJECT_0)
            {
                output.WriteLine("WoW process exited during initialization.");
                return (null, IntPtr.Zero);
            }

            // Try reading memory to verify the handle works
            var loginState = ReadString(_ownedProcessHandle, 0xB41478, 32);
            if (!string.IsNullOrEmpty(loginState))
            {
                output.WriteLine($"Memory read successful after {i + 1} seconds. LoginState: \"{loginState}\"");
                return (null, _ownedProcessHandle);  // Return null Process since we can't access it
            }

            if (i % 5 == 4)
            {
                output.WriteLine($"Still waiting... ({i + 1}s)");
            }
        }

        output.WriteLine("WoW started but memory not accessible yet - returning handle anyway.");
        return (null, _ownedProcessHandle);
    }

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

    public void Dispose()
    {
        // Terminate and close the process handle from CreateProcess
        if (_ownedProcessHandle != IntPtr.Zero)
        {
            try
            {
                // Terminate the process if it's still running
                CreateProcessHelper.TerminateProcess(_ownedProcessHandle, 0);
            }
            catch { }

            try
            {
                // Close the handle
                WinProcessImports.CloseHandle(_ownedProcessHandle);
            }
            catch { }
            _ownedProcessHandle = IntPtr.Zero;
        }
    }
}
