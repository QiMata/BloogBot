using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System.Runtime.InteropServices;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace ForegroundBotRunner.Statics
{
    public partial class ObjectManager
    {

        public LoginStates LoginState
        {
            get
            {
                try
                {
                    var loginStateStr = MemoryManager.ReadString(Offsets.CharacterScreen.LoginState);
                    if (string.IsNullOrWhiteSpace(loginStateStr))
                    {
                        return LoginStates.login; // Default to login screen if state is empty
                    }
                    if (Enum.TryParse<LoginStates>(loginStateStr, out var state))
                    {
                        return state;
                    }
                    return LoginStates.login; // Default to login if parsing fails
                }
                catch
                {
                    return LoginStates.login; // Default to login on error
                }
            }
        }


        /// <summary>
        /// Flag to pause ThreadSynchronizer-based native calls during the critical EnterWorld phase.
        /// When true, IsLoggedIn and other checks will use memory-only detection.
        /// This prevents WM_USER messages from interfering with the login handshake.
        /// Set by ForegroundBotWorker before clicking EnterWorld, cleared after world load or disconnect.
        /// </summary>


        /// <summary>
        /// Flag to pause ThreadSynchronizer-based native calls during the critical EnterWorld phase.
        /// When true, IsLoggedIn and other checks will use memory-only detection.
        /// This prevents WM_USER messages from interfering with the login handshake.
        /// Set by ForegroundBotWorker before clicking EnterWorld, cleared after world load or disconnect.
        /// </summary>
        public static volatile bool PauseNativeCallsDuringWorldEntry = false;


        private static DateTime? _enterWorldStartedAt;

        /// <summary>
        /// Tracks the last observed screen state and the timestamp of the last transition.
        /// When the screen state changes, Lua calls are suppressed for ScreenTransitionCooldown
        /// to let WoW's UI animations complete before sending commands. Without this, Lua calls
        /// during screen transitions cause ACCESS_VIOLATION crashes.
        /// </summary>


        /// <summary>
        /// Tracks the last observed screen state and the timestamp of the last transition.
        /// When the screen state changes, Lua calls are suppressed for ScreenTransitionCooldown
        /// to let WoW's UI animations complete before sending commands. Without this, Lua calls
        /// during screen transitions cause ACCESS_VIOLATION crashes.
        /// </summary>


        /// <summary>
        /// Tracks the last observed screen state and the timestamp of the last transition.
        /// When the screen state changes, Lua calls are suppressed for ScreenTransitionCooldown
        /// to let WoW's UI animations complete before sending commands. Without this, Lua calls
        /// during screen transitions cause ACCESS_VIOLATION crashes.
        /// </summary>


        /// <summary>
        /// Tracks the last observed screen state and the timestamp of the last transition.
        /// When the screen state changes, Lua calls are suppressed for ScreenTransitionCooldown
        /// to let WoW's UI animations complete before sending commands. Without this, Lua calls
        /// during screen transitions cause ACCESS_VIOLATION crashes.
        /// </summary>
        private static WoWScreenState _lastObservedScreen = WoWScreenState.Unknown;


        private static DateTime _lastScreenTransitionAt = DateTime.MinValue;


        private static readonly TimeSpan ScreenTransitionCooldown = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Returns true if a screen transition recently occurred and Lua calls should be deferred.
        /// Called by screen handlers before issuing Lua commands.
        /// </summary>


        /// <summary>
        /// Returns true if a screen transition recently occurred and Lua calls should be deferred.
        /// Called by screen handlers before issuing Lua commands.
        /// </summary>


        /// <summary>
        /// Returns true if a screen transition recently occurred and Lua calls should be deferred.
        /// Called by screen handlers before issuing Lua commands.
        /// </summary>


        /// <summary>
        /// Returns true if a screen transition recently occurred and Lua calls should be deferred.
        /// Called by screen handlers before issuing Lua commands.
        /// </summary>
        public static bool IsInScreenTransitionCooldown
        {
            get
            {
                if (_lastScreenTransitionAt == DateTime.MinValue) return false;
                return DateTime.UtcNow - _lastScreenTransitionAt < ScreenTransitionCooldown;
            }
        }


        public bool HasEnteredWorld
        {
            get => _hasEnteredWorld;
            set => _hasEnteredWorld = value;
        }

        /// <summary>
        /// Returns true if the world is currently loading (ContinentID = 0xFF)
        /// </summary>


        /// <summary>
        /// Returns true if the world is currently loading (ContinentID = 0xFF)
        /// </summary>


        /// <summary>
        /// Returns true if the world is currently loading (ContinentID = 0xFF)
        /// </summary>


        /// <summary>
        /// Returns true if the world is currently loading (ContinentID = 0xFF)
        /// </summary>
        public bool IsLoadingWorld => MemoryManager.ReadUint(Offsets.Map.ContinentId) == 0xFF;

        /// <summary>
        /// Gets the current ContinentId from memory.
        /// Used for screen state detection.
        /// </summary>


        /// <summary>
        /// Gets the current ContinentId from memory.
        /// Used for screen state detection.
        /// </summary>


        /// <summary>
        /// Gets the current ContinentId from memory.
        /// Used for screen state detection.
        /// </summary>


        /// <summary>
        /// Gets the current ContinentId from memory.
        /// Used for screen state detection.
        /// </summary>
        public uint ContinentId => MemoryManager.ReadUint(Offsets.Map.ContinentId);

        /// <summary>
        /// Determines the current WoW screen state using ContinentId as the primary discriminator.
        ///
        /// Detection Logic (from ScreenDetectionTests):
        /// - LoginState == "login" → LoginScreen
        /// - LoginState == "connecting" → Connecting
        /// - LoginState == "charcreate" → CharacterCreate
        /// - LoginState == "charselect" with ContinentId:
        ///   - ContinentId == 0xFFFFFFFF → CharacterSelect (not in any map)
        ///   - ContinentId == 0xFF (255) → LoadingWorld (loading bar visible)
        ///   - ContinentId < 0xFF → InWorld (valid map ID: 0=Eastern Kingdoms, 1=Kalimdor, etc.)
        /// </summary>


        /// <summary>
        /// Determines the current WoW screen state using ContinentId as the primary discriminator.
        ///
        /// Detection Logic (from ScreenDetectionTests):
        /// - LoginState == "login" → LoginScreen
        /// - LoginState == "connecting" → Connecting
        /// - LoginState == "charcreate" → CharacterCreate
        /// - LoginState == "charselect" with ContinentId:
        ///   - ContinentId == 0xFFFFFFFF → CharacterSelect (not in any map)
        ///   - ContinentId == 0xFF (255) → LoadingWorld (loading bar visible)
        ///   - ContinentId < 0xFF → InWorld (valid map ID: 0=Eastern Kingdoms, 1=Kalimdor, etc.)
        /// </summary>


        /// <summary>
        /// Determines the current WoW screen state using ContinentId as the primary discriminator.
        ///
        /// Detection Logic (from ScreenDetectionTests):
        /// - LoginState == "login" → LoginScreen
        /// - LoginState == "connecting" → Connecting
        /// - LoginState == "charcreate" → CharacterCreate
        /// - LoginState == "charselect" with ContinentId:
        ///   - ContinentId == 0xFFFFFFFF → CharacterSelect (not in any map)
        ///   - ContinentId == 0xFF (255) → LoadingWorld (loading bar visible)
        ///   - ContinentId < 0xFF → InWorld (valid map ID: 0=Eastern Kingdoms, 1=Kalimdor, etc.)
        /// </summary>


        /// <summary>
        /// Determines the current WoW screen state using ContinentId as the primary discriminator.
        ///
        /// Detection Logic (from ScreenDetectionTests):
        /// - LoginState == "login" → LoginScreen
        /// - LoginState == "connecting" → Connecting
        /// - LoginState == "charcreate" → CharacterCreate
        /// - LoginState == "charselect" with ContinentId:
        ///   - ContinentId == 0xFFFFFFFF → CharacterSelect (not in any map)
        ///   - ContinentId == 0xFF (255) → LoadingWorld (loading bar visible)
        ///   - ContinentId < 0xFF → InWorld (valid map ID: 0=Eastern Kingdoms, 1=Kalimdor, etc.)
        /// </summary>
        public WoWScreenState GetCurrentScreenState()
        {
            try
            {
                var loginStateStr = MemoryManager.ReadString(Offsets.CharacterScreen.LoginState)?.Trim().ToLowerInvariant() ?? "";
                var continentId = MemoryManager.ReadUint(Offsets.Map.ContinentId);

                // Check for "connecting" state first
                if (loginStateStr == "connecting")
                {
                    return TrackScreenTransition(WoWScreenState.Connecting);
                }

                // Check for character create
                if (loginStateStr == "charcreate")
                {
                    return TrackScreenTransition(WoWScreenState.CharacterCreate);
                }

                // Check for login screen
                if (loginStateStr == "login")
                {
                    return TrackScreenTransition(WoWScreenState.LoginScreen);
                }

                // At this point, loginState should be "charselect"
                // Use ContinentId as the PRIMARY discriminator
                // WoW 1.12.1 also uses: "movie" (cinematic), "options" (settings), "patchdownload", "credits"
                // Treat these as LoginScreen since they're pre-authentication screens
                if (loginStateStr == "movie" || loginStateStr == "options" || loginStateStr == "patchdownload" || loginStateStr == "credits")
                {
                    return TrackScreenTransition(WoWScreenState.LoginScreen);
                }

                // "realmwizard" is the first-time realm setup screen (choose realm type, language).
                // It appears after authentication but before the character list loads.
                // Treat as CharacterSelect so BotRunnerService triggers realm selection logic.
                if (loginStateStr == "realmwizard")
                {
                    return TrackScreenTransition(WoWScreenState.CharacterSelect);
                }

                // Empty loginState means WoW.exe just launched and memory isn't initialized yet.
                // This is the login screen — NOT charselect. Without this guard, the bot thinks
                // it's past login and never sends credentials.
                if (string.IsNullOrEmpty(loginStateStr))
                {
                    return TrackScreenTransition(WoWScreenState.LoginScreen);
                }

                if (loginStateStr == "charselect")
                {
                    // ContinentId == 0xFFFFFFFF means we're NOT in any map.
                    // This can mean charselect OR continent transition (e.g. zeppelin crossing).
                    // Distinguish: if we've already entered the world, it's a continent transition.
                    if (continentId == 0xFFFFFFFF)
                    {
                        if (HasEnteredWorld)
                        {
                            return TrackScreenTransition(WoWScreenState.LoadingWorld);
                        }
                        return TrackScreenTransition(WoWScreenState.CharacterSelect);
                    }

                    // ContinentId == 0xFF (255) means loading bar is visible
                    if (continentId == 0xFF)
                    {
                        return TrackScreenTransition(WoWScreenState.LoadingWorld);
                    }

                    // If we haven't entered the world yet, continentId may be stale
                    // (leftover from a previous WoW session or WoW.exe startup defaults).
                    // Treat as CharacterSelect until HasEnteredWorld confirms we're actually in-world.
                    if (!HasEnteredWorld)
                    {
                        return TrackScreenTransition(WoWScreenState.CharacterSelect);
                    }

                    // Any other ContinentId value is a valid map ID - we're in world
                    // Map IDs: 0=Eastern Kingdoms, 1=Kalimdor, 36=Deadmines, 329=Stratholme, 533=Naxxramas, etc.
                    // Dungeons/raids can have IDs > 255, so we can't just check < 0xFF
                    return TrackScreenTransition(WoWScreenState.InWorld);
                }

                DiagLog($"GetCurrentScreenState UNRECOGNIZED loginState='{loginStateStr}' continentId=0x{continentId:X8}");
                return TrackScreenTransition(WoWScreenState.Unknown);
            }
            catch (Exception ex)
            {
                DiagLog($"GetCurrentScreenState EXCEPTION: {ex.Message}");
                return TrackScreenTransition(WoWScreenState.Unknown);
            }
        }

        /// <summary>
        /// Tracks screen state transitions and records when transitions occur,
        /// so screen handlers can wait for animations to complete.
        /// </summary>


        /// <summary>
        /// Tracks screen state transitions and records when transitions occur,
        /// so screen handlers can wait for animations to complete.
        /// </summary>


        /// <summary>
        /// Tracks screen state transitions and records when transitions occur,
        /// so screen handlers can wait for animations to complete.
        /// </summary>


        /// <summary>
        /// Tracks screen state transitions and records when transitions occur,
        /// so screen handlers can wait for animations to complete.
        /// </summary>
        private static WoWScreenState TrackScreenTransition(WoWScreenState newState)
        {
            if (newState != _lastObservedScreen && _lastObservedScreen != WoWScreenState.Unknown)
            {
                DiagLog($"[ScreenTransition] {_lastObservedScreen} → {newState} — cooldown {ScreenTransitionCooldown.TotalSeconds:F1}s");
                _lastScreenTransitionAt = DateTime.UtcNow;
            }
            _lastObservedScreen = newState;
            return newState;
        }

        /// <summary>
        /// Returns true if the client has an active connection to the server.
        /// ClientConnection pointer is null when disconnected.
        /// </summary>


        /// <summary>
        /// Returns true if the client has an active connection to the server.
        /// ClientConnection pointer is null when disconnected.
        /// </summary>


        /// <summary>
        /// Returns true if the client has an active connection to the server.
        /// ClientConnection pointer is null when disconnected.
        /// </summary>


        /// <summary>
        /// Returns true if the client has an active connection to the server.
        /// ClientConnection pointer is null when disconnected.
        /// </summary>
        public bool IsConnected => MemoryManager.ReadIntPtr(Offsets.Connection.ClientConnection) != nint.Zero;

        /// <summary>
        /// Robust check for whether we're fully in the game world.
        /// Checks multiple signals to avoid stale state issues.
        /// </summary>


        /// <summary>
        /// Robust check for whether we're fully in the game world.
        /// Checks multiple signals to avoid stale state issues.
        /// </summary>


        /// <summary>
        /// Robust check for whether we're fully in the game world.
        /// Checks multiple signals to avoid stale state issues.
        /// </summary>


        /// <summary>
        /// Robust check for whether we're fully in the game world.
        /// Checks multiple signals to avoid stale state issues.
        /// </summary>
        public bool IsInWorld =>
            IsLoggedIn &&
            !IsLoadingWorld &&
            Player != null &&
            HasEnteredWorld;


        public void EnterWorld(ulong characterGuid)
        {
            // Guard 1: If the player GUID is already non-zero (detected from memory),
            // the world entry handshake succeeded. Don't re-trigger — let the polling
            // loop's normal player detection path set HasEnteredWorld on the next tick.
            if (IsLoggedIn)
            {
                DiagLog("EnterWorld: SKIP - IsLoggedIn=true (GUID detected, world entry in progress)");
                // Ensure the pause flag is cleared so polling loop can detect the player
                PauseNativeCallsDuringWorldEntry = false;
                _enterWorldStartedAt = null;
                return;
            }

            // Guard 2: Re-entry during active world entry handshake.
            // BotRunnerService calls this every 100ms because HasEnteredWorld stays false.
            if (PauseNativeCallsDuringWorldEntry)
            {
                // Allow retry after 30 seconds (in case enter-world silently failed)
                if (_enterWorldStartedAt.HasValue && (DateTime.UtcNow - _enterWorldStartedAt.Value).TotalSeconds > 30)
                {
                    DiagLog("EnterWorld: TIMEOUT - resetting world entry guard after 30s");
                    PauseNativeCallsDuringWorldEntry = false;
                    _enterWorldStartedAt = null;
                }
                else
                {
                    DiagLog("EnterWorld: SKIP - already in world entry process");
                    return;
                }
            }

            _enterWorldStartedAt = DateTime.UtcNow;

            // Wait for screen transition animation to complete before issuing Lua calls.
            if (IsInScreenTransitionCooldown)
            {
                DiagLog("EnterWorld: SKIP — screen transition cooldown active");
                return;
            }

            // Pause ThreadSynchronizer WM_USER messages during world server handshake
            // to prevent disconnect. Cleared when InWorld state is detected in polling loop.
            PauseNativeCallsDuringWorldEntry = true;

            var charCount = MaxCharacterCount;
            DiagLog($"EnterWorld: charCount={charCount}, characterGuid={characterGuid}");

            // Capture initial state
            LoginStateMonitor.CaptureSnapshot("EnterWorld_START");

            if (charCount <= 0)
            {
                DiagLog("EnterWorld: ABORT - no characters");
                // Don't leave the pause flag set when aborting — allows polling loop to proceed
                PauseNativeCallsDuringWorldEntry = false;
                _enterWorldStartedAt = null;
                return;
            }

            // Check for error dialog first - only abort on actual errors, not informational messages
            try
            {
                var dialogText = GlueDialogText;
                if (!string.IsNullOrEmpty(dialogText))
                {
                    DiagLog($"EnterWorld: Dialog detected: '{dialogText}'");
                    LoginStateMonitor.Log($"Dialog detected: '{dialogText}'");

                    // Only treat as error if it's an actual error message
                    var lowerText = dialogText.ToLowerInvariant();
                    bool isError = lowerText.Contains("failed") ||
                                   lowerText.Contains("error") ||
                                   lowerText.Contains("disconnect") ||
                                   lowerText.Contains("unable") ||
                                   lowerText.Contains("invalid");

                    if (isError)
                    {
                        DiagLog($"EnterWorld: ERROR - dismissing dialog and aborting");
                        LoginStateMonitor.CaptureSnapshot("EnterWorld_ERROR_DIALOG");
                        MainThreadLuaCall("GlueDialogButton1:Click()");
                        PauseNativeCallsDuringWorldEntry = false;
                        _enterWorldStartedAt = null;
                        return;
                    }
                    else
                    {
                        // Informational message like "Character list retrieved" - just dismiss and continue
                        DiagLog($"EnterWorld: Informational dialog - dismissing and continuing");
                        MainThreadLuaCall("GlueDialogButton1:Click()");
                        System.Threading.Thread.Sleep(200); // Brief delay after dismissing
                    }
                }
            }
            catch { /* GlueDialogText may throw if no dialog */ }

            // SIMPLIFIED: Match original BloogBot-Questing approach
            // Just wait for the button to be visible and click it - no EnterWorld() function call
            // The original uses: Wait.For("CharacterScreenAnim", 1000) before clicking

            // Wait for button to be visible (poll with delay like original bot)
            bool buttonVisible = false;
            for (int i = 0; i < 10; i++) // Try for 5 seconds max
            {
                try
                {
                    var buttonCheck = MainThreadLuaCallWithResult(
                        "{0} = CharSelectEnterWorldButton and CharSelectEnterWorldButton:IsVisible() and '1' or '0'");
                    if (buttonCheck.Length > 0 && buttonCheck[0] == "1")
                    {
                        buttonVisible = true;
                        DiagLog($"EnterWorld: Button visible after {(i+1)*500}ms");
                        break;
                    }
                }
                catch { }
                System.Threading.Thread.Sleep(500);
            }

            if (!buttonVisible)
            {
                DiagLog("EnterWorld: Button not visible after 5s - aborting");
                LoginStateMonitor.CaptureSnapshot("EnterWorld_BUTTON_NOT_VISIBLE");
                PauseNativeCallsDuringWorldEntry = false;
                _enterWorldStartedAt = null;
                return;
            }

            // Capture state before button click
            LoginStateMonitor.CaptureSnapshot("EnterWorld_PRE_CLICK");

            // Wait 1 second after button visible (like original Wait.For("CharacterScreenAnim", 1000))
            DiagLog("EnterWorld: Waiting 1s after button visible (matching original timing)");
            System.Threading.Thread.Sleep(1000);

            // Just click the button - this is all the original bot does
            DiagLog("EnterWorld: Clicking CharSelectEnterWorldButton (original approach)");
            LoginStateMonitor.Log("Clicking CharSelectEnterWorldButton NOW");
            MainThreadLuaCall("CharSelectEnterWorldButton:Click()");

            // Capture state immediately after click
            LoginStateMonitor.CaptureSnapshot("EnterWorld_POST_CLICK");

            DiagLog("EnterWorld: Button click completed");
        }


        public void DefaultServerLogin(string accountName, string password)
        {
            if (LoginState != LoginStates.login) return;
            // Use UI-based login: set text fields + click Login button.
            // The C++ DefaultServerLogin() function silently fails when called via
            // injected Lua at startup — the client's UI state prerequisites aren't met.
            // Setting the text fields and clicking the button simulates manual user input
            // and reliably initiates the SRP6 auth handshake.
            MainThreadLuaCall(
                $"AccountLoginAccountEdit:SetText('{accountName}');" +
                $"AccountLoginPasswordEdit:SetText('{password}');" +
                "AccountLogin_Login();");
        }



        public string GlueDialogText => MainThreadLuaCallWithResult("{0} = GlueDialogText:GetText()")[0];

        /// <summary>
        /// Dismisses error GlueDialogs (e.g. "Disconnected from server", "Login failed").
        /// Clicks GlueDialogButton1 if the dialog is visible AND contains an error message.
        /// IMPORTANT: Does NOT dismiss the "Success!" dialog — that's part of the auth handshake.
        /// Clicking "Cancel" on the Success dialog while m_netState is in a connected state
        /// causes ERROR #134 (m_netState == NS_INITIALIZED assertion failure).
        /// </summary>


        /// <summary>
        /// Dismisses error GlueDialogs (e.g. "Disconnected from server", "Login failed").
        /// Clicks GlueDialogButton1 if the dialog is visible AND contains an error message.
        /// IMPORTANT: Does NOT dismiss the "Success!" dialog — that's part of the auth handshake.
        /// Clicking "Cancel" on the Success dialog while m_netState is in a connected state
        /// causes ERROR #134 (m_netState == NS_INITIALIZED assertion failure).
        /// </summary>


        /// <summary>
        /// Dismisses error GlueDialogs (e.g. "Disconnected from server", "Login failed").
        /// Clicks GlueDialogButton1 if the dialog is visible AND contains an error message.
        /// IMPORTANT: Does NOT dismiss the "Success!" dialog — that's part of the auth handshake.
        /// Clicking "Cancel" on the Success dialog while m_netState is in a connected state
        /// causes ERROR #134 (m_netState == NS_INITIALIZED assertion failure).
        /// </summary>


        /// <summary>
        /// Dismisses error GlueDialogs (e.g. "Disconnected from server", "Login failed").
        /// Clicks GlueDialogButton1 if the dialog is visible AND contains an error message.
        /// IMPORTANT: Does NOT dismiss the "Success!" dialog — that's part of the auth handshake.
        /// Clicking "Cancel" on the Success dialog while m_netState is in a connected state
        /// causes ERROR #134 (m_netState == NS_INITIALIZED assertion failure).
        /// </summary>
        public static void DismissGlueDialog()
        {
            try
            {
                // Only dismiss dialogs that contain error/disconnect text.
                // The "Success!" dialog must NOT be dismissed — the client handles it internally.
                MainThreadLuaCall(
                    "if GlueDialog and GlueDialog:IsVisible() then " +
                    "  local text = GlueDialogText and GlueDialogText:GetText() or '' " +
                    "  if text and (string.find(text, 'Disconnected') or string.find(text, 'failed') " +
                    "    or string.find(text, 'error') or string.find(text, 'Error') " +
                    "    or string.find(text, 'unable') or string.find(text, 'Unable') " +
                    "    or string.find(text, 'timeout') or string.find(text, 'Timeout') " +
                    "    or string.find(text, 'closed') or string.find(text, 'Closed')) then " +
                    "    GlueDialogButton1:Click() " +
                    "  end " +
                    "end");
            }
            catch { /* GlueDialog may not exist or Lua not ready */ }
        }



        public static int MaxCharacterCount => MemoryManager.ReadInt(0x00B42140);


        public static void ResetLogin()
        {
            MainThreadLuaCall("arg1 = 'ESCAPE' GlueDialog_OnKeyDown()");
            MainThreadLuaCall("if RealmListCancelButton ~= nil then if RealmListCancelButton:IsVisible() then RealmListCancelButton:Click(); end end ");
        }


        /// <summary>
        /// SIMPLIFIED: Simple polling loop that only reads static memory addresses for login detection.
        /// Does NOT use EnumerateVisibleObjects callback - only memory reads.
        /// NOTE: This runs on a background thread. For WoW API calls that need the main thread,
        /// we use ThreadSynchronizer. However, for initial detection we just use memory reads
        /// which work from any thread.
        /// </summary>
        // Track consecutive failed login checks to debounce reset (avoid resetting on brief GUID=0 glitches)


        /// <summary>
        /// SIMPLIFIED: Simple polling loop that only reads static memory addresses for login detection.
        /// Does NOT use EnumerateVisibleObjects callback - only memory reads.
        /// NOTE: This runs on a background thread. For WoW API calls that need the main thread,
        /// we use ThreadSynchronizer. However, for initial detection we just use memory reads
        /// which work from any thread.
        /// </summary>
        // Track consecutive failed login checks to debounce reset (avoid resetting on brief GUID=0 glitches)
        private int _consecutiveLoggedOutCount = 0;


        private const int LOGOUT_DEBOUNCE_THRESHOLD = 10; // Require 10 consecutive checks (~5 seconds)

        // Track previous state for change detection (for LoginStateMonitor)


        // Track previous state for change detection (for LoginStateMonitor)


        // Track previous state for change detection (for LoginStateMonitor)


        // Track previous state for change detection (for LoginStateMonitor)
        private bool _prevIsConnected = true;


        private LoginStates _prevLoginState = LoginStates.login;


        private uint _prevContinentId = 0xFFFFFFFF;


        private volatile bool _isContinentTransition;

        /// <summary>
        /// True when the client is in a continent/map transition (ContinentId == 0xFF or 0xFFFFFFFF
        /// while HasEnteredWorld). Object pointers are invalid during this period — do NOT access
        /// WoWObject properties. Checked by MovementRecorder and snapshot builders to avoid crashes.
        /// </summary>


        /// <summary>
        /// True when the client is in a continent/map transition (ContinentId == 0xFF or 0xFFFFFFFF
        /// while HasEnteredWorld). Object pointers are invalid during this period — do NOT access
        /// WoWObject properties. Checked by MovementRecorder and snapshot builders to avoid crashes.
        /// </summary>


        /// <summary>
        /// True when the client is in a continent/map transition (ContinentId == 0xFF or 0xFFFFFFFF
        /// while HasEnteredWorld). Object pointers are invalid during this period — do NOT access
        /// WoWObject properties. Checked by MovementRecorder and snapshot builders to avoid crashes.
        /// </summary>


        /// <summary>
        /// True when the client is in a continent/map transition (ContinentId == 0xFF or 0xFFFFFFFF
        /// while HasEnteredWorld). Object pointers are invalid during this period — do NOT access
        /// WoWObject properties. Checked by MovementRecorder and snapshot builders to avoid crashes.
        /// </summary>
        public bool IsContinentTransition => _isContinentTransition;



        internal async Task StartSimplePollingLoop()
        {
            DiagLog("StartSimplePollingLoop: Starting simplified polling");
            LoginStateMonitor.CaptureSnapshot("PollingLoop_START");
            int loopCount = 0;

            while (true)
            {
                try
                {
                    loopCount++;

                    // Read static memory addresses only - no callback, no synchronization needed
                    var isLoggedIn = IsLoggedIn;
                    var isLoadingWorld = IsLoadingWorld;
                    var isConnected = IsConnected;
                    var loginState = LoginState;
                    var continentId = ContinentId;

                    // Detect state changes and capture snapshots
                    if (isConnected != _prevIsConnected)
                    {
                        DiagLog($"STATE CHANGE: IsConnected {_prevIsConnected} -> {isConnected}");
                        LoginStateMonitor.CaptureSnapshot($"ConnectionChanged_{(isConnected ? "CONNECTED" : "DISCONNECTED")}");
                        _prevIsConnected = isConnected;
                    }
                    if (loginState != _prevLoginState)
                    {
                        DiagLog($"STATE CHANGE: LoginState {_prevLoginState} -> {loginState}");
                        LoginStateMonitor.CaptureSnapshot($"LoginStateChanged_{loginState}");
                        _prevLoginState = loginState;
                    }
                    if (continentId != _prevContinentId)
                    {
                        DiagLog($"STATE CHANGE: ContinentId 0x{_prevContinentId:X} -> 0x{continentId:X}");
                        LoginStateMonitor.CaptureSnapshot($"ContinentChanged_0x{continentId:X}");
                        _prevContinentId = continentId;
                    }

                    // Capture periodic snapshot every 10 seconds during login/charselect phase
                    if (loopCount % 20 == 1 && loginState != LoginStates.login && !HasEnteredWorld)
                    {
                        LoginStateMonitor.CaptureSnapshot($"Periodic_{loopCount}");
                    }

                    // Log state every 20 iterations (every 10 seconds)
                    if (loopCount % 20 == 1)
                    {
                        DiagLog($"SimplePolling[{loopCount}]: IsLoggedIn={isLoggedIn}, IsLoadingWorld={isLoadingWorld}, IsConnected={isConnected}, LoginState={loginState}, HasEnteredWorld={HasEnteredWorld}, Player={(Player != null ? Player.Name : "(null)")}, LogoutCount={_consecutiveLoggedOutCount}");
                    }

                    // Detect charselect state reset: if LoginState is charselect AND HasEnteredWorld is true AND NOT logged in,
                    // the player logged out (on servers where events don't fire)
                    // IMPORTANT: Use debounce to avoid resetting on brief GUID=0 glitches
                    // IMPORTANT: Skip during continent transitions (ContinentId == 0xFFFFFFFF while HasEnteredWorld)
                    // — zeppelin/boat crossings temporarily clear the GUID and ContinentId
                    bool isContinentTransition = HasEnteredWorld && (continentId == 0xFFFFFFFF || continentId == 0xFF);

                    // Detect instance/map transitions: map ID changed to a new valid map.
                    bool mapChanged = _prevContinentId != 0xFFFFFFFF && continentId != _prevContinentId
                                      && continentId != 0xFF && continentId != 0xFFFFFFFF;
                    if (mapChanged && HasEnteredWorld)
                    {
                        CrashTrace($"MAP_CHANGE: {_prevContinentId} → {continentId} — pausing native calls");
                        isContinentTransition = true;
                        Mem.ThreadSynchronizer.Paused = true;
                    }

                    _isContinentTransition = isContinentTransition;

                    // Pause ThreadSynchronizer during any transition to prevent WndProc
                    // from executing Lua calls while WoW's internal state is unstable.
                    if (isContinentTransition)
                    {
                        Mem.ThreadSynchronizer.Paused = true;
                        CrashTrace($"TRANSITION: contId=0x{continentId:X} paused=true logged={isLoggedIn} loading={isLoadingWorld}");
                    }
                    else if (Mem.ThreadSynchronizer.Paused && !isContinentTransition && isLoggedIn && !isLoadingWorld)
                    {
                        CrashTrace("TRANSITION_END: resuming native calls");
                        Mem.ThreadSynchronizer.Paused = false;
                    }

                    // When a continent transition starts, immediately clear stale objects.
                    // Object pointers become invalid during map changes — any access to
                    // stale WoWObject.Pointer would hit unmapped memory and crash (.NET 8
                    // does not catch AccessViolationException).
                    if (isContinentTransition)
                    {
                        lock (_objectsLock)
                        {
                            if (ObjectsBuffer.Count > 0)
                            {
                                DiagLog($"SimplePolling: Continent transition — clearing {ObjectsBuffer.Count} stale objects");
                                ObjectsBuffer.Clear();
                            }
                        }
                        Player = null;
                        Pet = null;

                        if (loopCount % 4 == 0)
                        {
                            DiagLog($"SimplePolling: Continent transition detected (ContinentId=0x{continentId:X}), skipping enumeration and logout detection");
                        }
                    }
                    if (loginState == LoginStates.charselect && HasEnteredWorld && !isLoggedIn && !isContinentTransition)
                    {
                        _consecutiveLoggedOutCount++;
                        if (_consecutiveLoggedOutCount >= LOGOUT_DEBOUNCE_THRESHOLD)
                        {
                            DiagLog($"SimplePolling: CHARSELECT RESET after {_consecutiveLoggedOutCount} consecutive checks - resetting state");
                            HasEnteredWorld = false;
                            Player = null;
                            Pet = null;
                            lock (_objectsLock) { ObjectsBuffer.Clear(); }
                            ClearCachedGuid(); // Clear the cached GUID on logout
                            Mem.ThreadSynchronizer.ResetObjMgrValidState(); // Allow Lua calls at charselect
                            _consecutiveLoggedOutCount = 0;
                        }
                        else if (_consecutiveLoggedOutCount == 1)
                        {
                            DiagLog($"SimplePolling: Detected logout condition, waiting for debounce (1/{LOGOUT_DEBOUNCE_THRESHOLD})");
                        }
                    }
                    else
                    {
                        // Reset the logout counter if we're logged in or not at charselect
                        if (_consecutiveLoggedOutCount > 0 && isLoggedIn)
                        {
                            DiagLog($"SimplePolling: Logout condition cleared (was at {_consecutiveLoggedOutCount}), IsLoggedIn=true");
                        }
                        _consecutiveLoggedOutCount = 0;
                    }

                    // OBJECT ENUMERATION: When already in world, run full EnumerateVisibleObjects
                    // to populate ObjectsBuffer with nearby units, players, items, game objects.
                    // This is the normal steady-state path after initial world entry.
                    // Skip during continent transitions — object manager is invalid while map is changing.
                    if (HasEnteredWorld && !PauseNativeCallsDuringWorldEntry && !isContinentTransition)
                    {
                        try
                        {
                            EnumerateVisibleObjects();

                            if (loopCount % 20 == 1)
                            {
                                DiagLog($"SimplePolling[{loopCount}]: Enumerated {ObjectsBuffer.Count} objects (Player={Player?.Name})");
                            }
                        }
                        catch (Exception ex)
                        {
                            DiagLog($"SimplePolling enumeration error: {ex.Message}");
                        }
                    }
                    // INITIAL PLAYER DETECTION: First time entering world after login.
                    // If logged in (GUID != 0) and not loading, attempt to populate player.
                    // Once HasEnteredWorld is set, the enumeration branch above takes over.
                    // Skip native calls during world entry phase to avoid ThreadSynchronizer interference
                    else if (isLoggedIn && !isLoadingWorld && !PauseNativeCallsDuringWorldEntry)
                    {
                        try
                        {
                            // Try memory-first path: if we already have a GUID from memory reads
                            // (set during PauseNativeCallsDuringWorldEntry phase), use it directly.
                            // The ThreadSynchronizer path can fail if WoW's message loop is busy
                            // during world entry transition.
                            ulong playerGuid = GetPlayerGuidFromMemory();
                            if (playerGuid == 0)
                            {
                                // Fallback: try via ThreadSynchronizer (main thread native call)
                                playerGuid = ThreadSynchronizer.RunOnMainThread(() => Functions.GetPlayerGuid());
                            }
                            if (playerGuid == 0)
                            {
                                Player = null;
                                continue;
                            }

                            // Update the cached GUID for fast IsLoggedIn checks
                            UpdateCachedGuid(playerGuid);

                            byte[] playerGuidParts = BitConverter.GetBytes(playerGuid);
                            PlayerGuid = new HighGuid(playerGuidParts[0..4], playerGuidParts[4..8]);

                            // Try to get the player object pointer.
                            // First attempt via ThreadSynchronizer (native function call).
                            // If that fails, try walking the object list in memory directly.
                            var playerObject = ThreadSynchronizer.RunOnMainThread(() => Functions.GetObjectPtr(PlayerGuid.FullGuid));
                            if (playerObject == nint.Zero)
                            {
                                // Memory fallback: walk the object manager linked list to find the player
                                playerObject = GetObjectPtrFromMemory(playerGuid);
                                if (playerObject != nint.Zero && (loopCount <= 100 || loopCount % 50 == 0))
                                {
                                    DiagLog($"SimplePolling[{loopCount}]: GetObjectPtrFromMemory(GUID={playerGuid}) fallback returned 0x{playerObject:X}");
                                }
                            }

                            // Log the pointer lookup on first few attempts
                            if (loopCount <= 100 || loopCount % 50 == 0)
                            {
                                DiagLog($"SimplePolling[{loopCount}]: GetObjectPtr(GUID={playerGuid}) returned 0x{playerObject:X}");
                            }

                            if (playerObject == nint.Zero)
                            {
                                // Player GUID is valid but object pointer is null - still loading object data
                                if (loopCount % 20 == 1)
                                {
                                    DiagLog($"SimplePolling[{loopCount}]: GUID={playerGuid} valid but playerObject=null, waiting for object data");
                                }
                                continue;
                            }

                            // Create or update the player object
                            if (Player == null || ((WoWObject)Player).Pointer != playerObject)
                            {
                                Player = new LocalPlayer(playerObject, PlayerGuid, WoWObjectType.Player);
                                DiagLog($"SimplePolling: Created LocalPlayer at 0x{playerObject:X}, GUID={playerGuid}");
                            }

                            // Check if this is first time in world - fire event
                            if (!HasEnteredWorld && Player != null)
                            {
                                HasEnteredWorld = true;

                                // Get player name - try direct memory first, then Lua fallback via main thread
                                var playerName = Player.Name ?? "";

                                // Log even if name is empty (will try Lua fallback later)
                                DiagLog($"SimplePolling: ENTERED_WORLD! Player={playerName} (empty={string.IsNullOrEmpty(playerName)})");

                                if (string.IsNullOrEmpty(playerName))
                                {
                                    // Try Lua fallback on main thread
                                    ThreadSynchronizer.RunOnMainThread(() =>
                                    {
                                        try
                                        {
                                            var result = MainThreadLuaCallWithResult("{0} = UnitName('player')");
                                            if (result != null && result.Length > 0 && !string.IsNullOrEmpty(result[0]))
                                            {
                                                playerName = result[0];
                                                DiagLog($"SimplePolling: Lua fallback got player name: {playerName}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            DiagLog($"SimplePolling: Lua name fallback error: {ex.Message}");
                                        }

                                        // Update snapshot with character name
                                        _characterState.CharacterName = playerName;
                                        _characterState.Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                    });
                                }
                                else
                                {
                                    // Update snapshot directly (without Lua fallback in Mode 2 or when name is available)
                                    _characterState.CharacterName = playerName;
                                    _characterState.Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                }

                                // Fire the OnEnteredWorld event
                                try
                                {
                                    OnEnteredWorld?.Invoke(this, EventArgs.Empty);
                                }
                                catch (Exception ex)
                                {
                                    DiagLog($"SimplePolling: OnEnteredWorld event error: {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            DiagLog($"SimplePolling player detection error: {ex.Message}");
                        }
                    }
                    // WORLD ENTRY COMPLETION DETECTION (during PauseNativeCallsDuringWorldEntry):
                    // Both branches above are skipped when PauseNativeCallsDuringWorldEntry is true.
                    // But GetPlayerGuidFromMemory() (called by IsLoggedIn) can detect the GUID
                    // becoming non-zero purely from memory reads. When this happens, the world
                    // entry has succeeded — clear the flag so the normal player detection path
                    // (above) can run on the next iteration with native calls.
                    else if (PauseNativeCallsDuringWorldEntry && isLoggedIn && !isLoadingWorld)
                    {
                        DiagLog($"SimplePolling[{loopCount}]: World entry detected via memory GUID during pause phase — clearing PauseNativeCallsDuringWorldEntry");
                        PauseNativeCallsDuringWorldEntry = false;
                        _enterWorldStartedAt = null;
                    }
                    await Task.Delay(500);
                }
                catch (Exception e)
                {
                    DiagLog($"SimplePolling EXCEPTION: {e.Message}");
                    Log.Error($"[OBJECT MANAGER] SimplePolling: {e}");
                    await Task.Delay(1000);
                }
            }
        }
    }
}
