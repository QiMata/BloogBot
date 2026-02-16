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
using System.Threading.Tasks;
using System.IO;

namespace ForegroundBotRunner.Statics
{
    public class ObjectManager : IObjectManager
    {
        // Diagnostic logging for debugging (writes to WWoWLogs folder)
        private static readonly string DiagnosticLogPath;
        private static readonly object DiagnosticLogLock = new();
        static ObjectManager()
        {
            string wowDir;
            try { wowDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName) ?? AppContext.BaseDirectory; }
            catch { wowDir = AppContext.BaseDirectory; }
            var logsDir = Path.Combine(wowDir, "WWoWLogs");
            try { Directory.CreateDirectory(logsDir); } catch { }
            DiagnosticLogPath = Path.Combine(logsDir, "object_manager_debug.log");
            try { File.WriteAllText(DiagnosticLogPath, $"=== ObjectManager Debug Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n"); } catch { }
        }
        private static void DiagLog(string message)
        {
            try
            {
                lock (DiagnosticLogLock) { File.AppendAllText(DiagnosticLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); }
            }
            catch { }
        }

        // LUA SCRIPTS
        private const string WandLuaScript = "if IsCurrentAction(72) == nil then CastSpellByName('Shoot') end";
        private const string TurnOffWandLuaScript = "if IsCurrentAction(72) ~= nil then CastSpellByName('Shoot') end";
        private const string AutoAttackLuaScript = "if IsCurrentAction(72) == nil then CastSpellByName('Attack') end";
        private const string TurnOffAutoAttackLuaScript = "if IsCurrentAction(72) ~= nil then CastSpellByName('Attack') end";
        private const int OBJECT_TYPE_OFFSET = 0x14;

        // Vanilla 1.12.1 callback signature: int __thiscall callback(int filter, ulong guid)
        // ThisCall convention: filter comes first, guid second (opposite of non-Vanilla clients)
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate int EnumerateVisibleObjectsCallbackVanilla(int filter, ulong guid);

        public HighGuid PlayerGuid { get; internal set; } = new HighGuid(new byte[4], new byte[4]);
        private volatile bool _ingame1 = true;
        private readonly bool _ingame2 = true;
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
        private readonly EnumerateVisibleObjectsCallbackVanilla CallbackDelegate;
        private readonly nint callbackPtr;
        private readonly IWoWActivitySnapshot _characterState;
        public IEnumerable<IWoWObject> Objects
        {
            get
            {
                lock (_objectsLock)
                {
                    return [.. ObjectsBuffer.Cast<IWoWObject>()]; // safe snapshot
                }
            }
        }
        internal IList<WoWObject> ObjectsBuffer = [];

        private readonly object _objectsLock = new();
        public ObjectManager(IWoWEventHandler eventHandler, IWoWActivitySnapshot parProbe)
        {
            _characterState = parProbe;

            CallbackDelegate = CallbackVanilla;
            callbackPtr = Marshal.GetFunctionPointerForDelegate(CallbackDelegate);

            eventHandler.OnEvent += OnEvent;

            // SIMPLIFIED: Disable the EnumerateVisibleObjects callback loop.
            // We now use static memory addresses only for login detection.
            // The callback loop is still available but not used for critical state detection.
            // Task.Factory.StartNew(async () => await StartEnumeration());

            // Instead, start a simple polling loop that only reads static memory addresses
            Task.Factory.StartNew(async () => await StartSimplePollingLoop());
        }

        public IWoWLocalPlayer Player { get; internal set; }

        public IWoWLocalPet Pet { get; private set; }

        /// <summary>
        /// Event fired when the player first enters the world after login.
        /// This fires from the enumeration thread (inside ThreadSynchronizer) when HasEnteredWorld becomes true.
        /// Subscribers should use this to immediately send snapshots while the player is definitely in-world.
        /// </summary>
        public event EventHandler? OnEnteredWorld;

        // These filter from ObjectsBuffer (which is populated by EnumerateVisibleObjects callback)
        public IEnumerable<IWoWGameObject> GameObjects => Objects.OfType<IWoWGameObject>();
        public IEnumerable<IWoWUnit> Units => Objects.OfType<IWoWUnit>();
        public IEnumerable<IWoWPlayer> Players => Objects.OfType<IWoWPlayer>();
        public IEnumerable<IWoWItem> Items => Objects.OfType<IWoWItem>();
        public IEnumerable<IWoWContainer> Containers => Objects.OfType<IWoWContainer>();
        public ulong StarTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Star, true);
        public ulong CircleTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Circle, true);
        public ulong DiamondTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Diamond, true);
        public ulong TriangleTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Triangle, true);
        public ulong MoonTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Moon, true);
        public ulong SquareTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Square, true);
        public ulong CrossTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Cross, true);
        public ulong SkullTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Skull, true);

        // Note: Memory offset 0xB4B424 (IsIngame) returns 0 on this WoW client version (Elysium).
        // Note: Functions.GetPlayerGuid() returns an object manager index (e.g., 5), not the actual GUID.
        // Reading the player GUID directly from memory: [ManagerBase] + PlayerGuidOffset
        private static int _guidLogCount = 0;

        // Cached player GUID - updated via ThreadSynchronizer to ensure main thread context
        // Functions.GetPlayerGuid() only works reliably from the main WoW thread
        private static ulong _cachedPlayerGuid = 0;
        private static readonly object _guidCacheLock = new();
        private ulong GetPlayerGuidFromMemory()
        {
            try
            {
                var managerPtr = MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase);
                var guid = managerPtr == nint.Zero ? 0UL : MemoryManager.ReadUlong(nint.Add(managerPtr, (int)Offsets.ObjectManager.PlayerGuid));

                // Enhanced diagnostic logging - log first 20 reads, then every 50th read
                _guidLogCount++;
                if (_guidLogCount <= 20 || (_guidLogCount % 50 == 0))
                {
                    // Also read additional state for comprehensive diagnostics
                    var continentId = MemoryManager.ReadUint(Offsets.Map.ContinentId);
                    var clientConn = MemoryManager.ReadIntPtr(Offsets.Connection.ClientConnection);
                    var loginStateStr = "(unknown)";
                    try { loginStateStr = MemoryManager.ReadString(Offsets.CharacterScreen.LoginState); } catch { }

                    DiagLog($"GetPlayerGuidFromMemory[{_guidLogCount}]: " +
                        $"ManagerBase=0x{Offsets.ObjectManager.ManagerBase:X}, " +
                        $"managerPtr=0x{managerPtr:X}, " +
                        $"PlayerGuidOffset=0x{Offsets.ObjectManager.PlayerGuid:X}, " +
                        $"GUID={guid}, " +
                        $"ContinentId=0x{continentId:X} ({(continentId == 0xFF ? "LOADING" : "Ready")}), " +
                        $"ClientConn=0x{clientConn:X} ({(clientConn != nint.Zero ? "Connected" : "Disconnected")}), " +
                        $"LoginState=\"{loginStateStr}\"");
                }

                if (managerPtr == nint.Zero) return 0;
                return guid;
            }
            catch (Exception ex)
            {
                if (_guidLogCount <= 20)
                {
                    DiagLog($"GetPlayerGuidFromMemory EXCEPTION: {ex.Message}");
                }
                return 0;
            }
        }
        // CRITICAL: Use Functions.GetPlayerGuid() instead of memory reads!
        // GetPlayerGuidFromMemory() returns 0 most of the time because ManagerBase pointer is null.
        // Functions.GetPlayerGuid() calls the WoW function which works correctly.
        // Note: The returned value (e.g., 5) is an object manager index, but non-zero means logged in.
        // IMPORTANT: GetPlayerGuid() only works reliably from the main WoW thread!
        // We cache the result to avoid synchronization overhead on every check.
        // Counter for IsLoggedIn calls to limit logging
        private static int _isLoggedInCallCount = 0;

        /// <summary>
        /// Flag to pause ThreadSynchronizer-based native calls during the critical EnterWorld phase.
        /// When true, IsLoggedIn and other checks will use memory-only detection.
        /// This prevents WM_USER messages from interfering with the login handshake.
        /// Set by ForegroundBotWorker before clicking EnterWorld, cleared after world load or disconnect.
        /// </summary>
        public static volatile bool PauseNativeCallsDuringWorldEntry = false;

        public bool IsLoggedIn
        {
            get
            {
                _isLoggedInCallCount++;

                // Fast path: if we have a cached GUID, we're logged in
                lock (_guidCacheLock)
                {
                    if (_cachedPlayerGuid != 0) return true;
                }

                // During world entry phase, use memory-only to avoid ThreadSynchronizer interference
                if (PauseNativeCallsDuringWorldEntry)
                {
                    // Memory-only detection: read GUID from memory
                    var memGuid = GetPlayerGuidFromMemory();
                    if (memGuid != 0)
                    {
                        lock (_guidCacheLock)
                        {
                            _cachedPlayerGuid = memGuid;
                            if (_isLoggedInCallCount <= 10)
                            {
                                DiagLog($"IsLoggedIn[MemoryOnly]: Cached GUID={memGuid} from memory (PauseNative={PauseNativeCallsDuringWorldEntry})");
                            }
                        }
                        return true;
                    }
                    return false;
                }

                // Slow path: check via main thread and cache the result (Mode 2+)
                try
                {
                    var guid = ThreadSynchronizer.RunOnMainThread(() => Functions.GetPlayerGuid());
                    if (guid != 0)
                    {
                        lock (_guidCacheLock)
                        {
                            _cachedPlayerGuid = guid;
                            DiagLog($"IsLoggedIn: Cached GUID={guid} via ThreadSynchronizer");
                        }
                        return true;
                    }
                    else
                    {
                        // Log GUID=0 result periodically (first 10, then every 20th call)
                        if (_isLoggedInCallCount <= 10 || _isLoggedInCallCount % 20 == 0)
                        {
                            DiagLog($"IsLoggedIn[{_isLoggedInCallCount}]: GetPlayerGuid returned 0 (not logged in)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    DiagLog($"IsLoggedIn: ThreadSynchronizer error: {ex.Message}");
                }

                return false;
            }
        }

        /// <summary>
        /// Clears the cached player GUID. Call this when logout is detected.
        /// </summary>
        internal static void ClearCachedGuid()
        {
            lock (_guidCacheLock)
            {
                if (_cachedPlayerGuid != 0)
                {
                    DiagLog($"ClearCachedGuid: Clearing cached GUID={_cachedPlayerGuid}");
                    _cachedPlayerGuid = 0;
                }
            }
        }

        /// <summary>
        /// Updates the cached player GUID. Call this when a valid GUID is detected.
        /// </summary>
        internal static void UpdateCachedGuid(ulong guid)
        {
            if (guid == 0) return;
            lock (_guidCacheLock)
            {
                if (_cachedPlayerGuid != guid)
                {
                    DiagLog($"UpdateCachedGuid: Updating cached GUID from {_cachedPlayerGuid} to {guid}");
                    _cachedPlayerGuid = guid;
                }
            }
        }

        // Volatile to ensure visibility across threads (enumeration thread sets, main loop reads)
        private volatile bool _hasEnteredWorld;
        public bool HasEnteredWorld
        {
            get => _hasEnteredWorld;
            set => _hasEnteredWorld = value;
        }

        /// <summary>
        /// Returns true if the world is currently loading (ContinentID = 0xFF)
        /// </summary>
        public bool IsLoadingWorld => MemoryManager.ReadUint(Offsets.Map.ContinentId) == 0xFF;

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
        public WoWScreenState GetCurrentScreenState()
        {
            try
            {
                var loginStateStr = MemoryManager.ReadString(Offsets.CharacterScreen.LoginState)?.Trim().ToLowerInvariant() ?? "";
                var continentId = MemoryManager.ReadUint(Offsets.Map.ContinentId);

                // Check for "connecting" state first
                if (loginStateStr == "connecting")
                {
                    return WoWScreenState.Connecting;
                }

                // Check for character create
                if (loginStateStr == "charcreate")
                {
                    return WoWScreenState.CharacterCreate;
                }

                // Check for login screen
                if (loginStateStr == "login")
                {
                    return WoWScreenState.LoginScreen;
                }

                // At this point, loginState should be "charselect"
                // Use ContinentId as the PRIMARY discriminator
                if (loginStateStr == "charselect" || string.IsNullOrEmpty(loginStateStr))
                {
                    // ContinentId == 0xFFFFFFFF means we're NOT in any map.
                    // This can mean charselect OR continent transition (e.g. zeppelin crossing).
                    // Distinguish: if we've already entered the world, it's a continent transition.
                    if (continentId == 0xFFFFFFFF)
                    {
                        if (HasEnteredWorld)
                        {
                            return WoWScreenState.LoadingWorld;
                        }
                        return WoWScreenState.CharacterSelect;
                    }

                    // ContinentId == 0xFF (255) means loading bar is visible
                    if (continentId == 0xFF)
                    {
                        return WoWScreenState.LoadingWorld;
                    }

                    // Any other ContinentId value is a valid map ID - we're in world
                    // Map IDs: 0=Eastern Kingdoms, 1=Kalimdor, 36=Deadmines, 329=Stratholme, 533=Naxxramas, etc.
                    // Dungeons/raids can have IDs > 255, so we can't just check < 0xFF
                    return WoWScreenState.InWorld;
                }

                return WoWScreenState.Unknown;
            }
            catch (Exception ex)
            {
                DiagLog($"GetCurrentScreenState EXCEPTION: {ex.Message}");
                return WoWScreenState.Unknown;
            }
        }

        /// <summary>
        /// Returns true if the client has an active connection to the server.
        /// ClientConnection pointer is null when disconnected.
        /// </summary>
        public bool IsConnected => MemoryManager.ReadIntPtr(Offsets.Connection.ClientConnection) != nint.Zero;

        /// <summary>
        /// Robust check for whether we're fully in the game world.
        /// Checks multiple signals to avoid stale state issues.
        /// </summary>
        public bool IsInWorld =>
            IsLoggedIn &&
            !IsLoadingWorld &&
            Player != null &&
            HasEnteredWorld;

        private static int _antiAfkLogCounter = 0;
        public void AntiAfk()
        {
            var tickCount = Environment.TickCount;
            MemoryManager.WriteInt(MemoryAddresses.LastHardwareAction, tickCount);

            // Log every 20th call (every 10 seconds at 500ms intervals) to verify it's working
            if (++_antiAfkLogCounter % 20 == 1)
            {
                var readBack = MemoryManager.ReadInt((nint)MemoryAddresses.LastHardwareAction);
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Documents", "BloogBot", "antiafk_log.txt");
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                    var line = $"[{DateTime.Now:HH:mm:ss}] AntiAfk: wrote {tickCount}, readback={readBack}, match={tickCount == readBack}\n";
                    File.AppendAllText(logPath, line);
                }
                catch { }
            }
        }

        public string ZoneText
        {
            // this is weird and throws an exception right after entering world,
            // so we catch and ignore the exception to avoid console noise
            get
            {
                try
                {
                    var ptr = MemoryManager.ReadIntPtr(MemoryAddresses.ZoneTextPtr);
                    return MemoryManager.ReadString(ptr);
                }
                catch (Exception)
                {
                    return "";
                }
            }
        }

        public string MinimapZoneText
        {
            // this is weird and throws an exception right after entering world,
            // so we catch and ignore the exception to avoid console noise
            get
            {
                try
                {
                    var ptr = MemoryManager.ReadIntPtr(MemoryAddresses.MinimapZoneTextPtr);
                    return MemoryManager.ReadString(ptr);
                }
                catch (Exception)
                {
                    return "";
                }
            }
        }

        public string ServerName
        {
            // this is weird and throws an exception right after entering world,
            // so we catch and ignore the exception to avoid console noise
            get
            {
                try
                {
                    // not exactly sure how this works. seems to return a string like "Endless\WoW.exe" or "Karazhan\WoW.exe"
                    var fullName = MemoryManager.ReadString(MemoryAddresses.ServerName);
                    return fullName.Split('\\').First();
                }
                catch (Exception)
                {
                    return "";
                }
            }
        }

        public IEnumerable<IWoWPlayer> PartyMembers
        {
            get
            {
                var partyMembers = new List<IWoWPlayer>() { Player };

                var partyMember1 = (WoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party1Guid);
                if (partyMember1 != null)
                    partyMembers.Add(partyMember1);

                var partyMember2 = (WoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party2Guid);
                if (partyMember2 != null)
                    partyMembers.Add(partyMember2);

                var partyMember3 = (WoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party3Guid);
                if (partyMember3 != null)
                    partyMembers.Add(partyMember3);

                var partyMember4 = (WoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party4Guid);
                if (partyMember4 != null)
                    partyMembers.Add(partyMember4);

                return partyMembers;
            }
        }

        public IWoWPlayer PartyLeader => Players.FirstOrDefault(p => p.Guid == PartyLeaderGuid);

        public ulong PartyLeaderGuid => MemoryManager.ReadUlong(MemoryAddresses.PartyLeaderGuid);
        public ulong Party1Guid => MemoryManager.ReadUlong(MemoryAddresses.Party1Guid);
        public ulong Party2Guid => MemoryManager.ReadUlong(MemoryAddresses.Party2Guid);
        public ulong Party3Guid => MemoryManager.ReadUlong(MemoryAddresses.Party3Guid);
        public ulong Party4Guid => MemoryManager.ReadUlong(MemoryAddresses.Party4Guid);

        public IEnumerable<IWoWUnit> CasterAggressors =>
            Aggressors
                .Where(u => u.ManaPercent > 0);

        public IEnumerable<IWoWUnit> MeleeAggressors =>
            Aggressors
                .Where(u => u.ManaPercent <= 0);

        public IEnumerable<IWoWUnit> Aggressors =>
            Hostiles
                .Where(u => u.IsInCombat || u.IsFleeing);
        //.Where(u =>
        //    u.TargetGuid == Pet?.Guid || 
        //    u.IsFleeing ||
        //    PartyMembers.Any(x => u.TargetGuid == x.Guid));            

        public IEnumerable<IWoWUnit> Hostiles =>
            Units
                .Where(u => u.Health > 0)
                .Where(u =>
                    u.UnitReaction == UnitReaction.Hated ||
                    u.UnitReaction == UnitReaction.Hostile ||
                    u.UnitReaction == UnitReaction.Unfriendly ||
                    u.UnitReaction == UnitReaction.Neutral);

        // https://vanilla-wow.fandom.com/wiki/API_GetTalentInfo
        // tab index is 1, 2 or 3
        // talentIndex is counter left to right, top to bottom, starting at 1
        public static sbyte GetTalentRank(int tabIndex, int talentIndex)
        {
            var results = Functions.LuaCallWithResult($"{{0}}, {{1}}, {{2}}, {{3}}, {{4}} = GetTalentInfo({tabIndex},{talentIndex})");

            if (results.Length == 5)
                return Convert.ToSByte(results[4]);

            return -1;
        }

        public static void PickupInventoryItem(int inventorySlot)
        {
            Functions.LuaCall($"PickupInventoryItem({inventorySlot})");
        }

        public void DeleteCursorItem()
        {
            Functions.LuaCall("DeleteCursorItem()");
        }

        public void SendChatMessage(string chatMessage)
        {
            Functions.LuaCall($"SendChatMessage(\"{chatMessage}\")");
        }

        public void SetRaidTarget(IWoWUnit target, TargetMarker targetMarker)
        {
            SetTarget(target.Guid);
            Functions.LuaCall($"SetRaidTarget('target', {targetMarker})");
        }

        public void SetTarget(ulong guid)
        {
            Functions.SetTarget(guid);
        }
        public void AcceptGroupInvite()
        {
            ThreadSynchronizer.RunOnMainThread(() =>
            {
                Functions.LuaCall($"StaticPopup1Button1:Click()");
                Functions.LuaCall($"AcceptGroup()");
            });
        }

        public void EquipCursorItem()
        {
            Functions.LuaCall("AutoEquipCursorItem()");
        }

        public void ConfirmItemEquip()
        {
            Functions.LuaCall($"AutoEquipCursorItem()");
            Functions.LuaCall($"StaticPopup1Button1:Click()");
        }
        public void EnterWorld(ulong characterGuid)
        {
            var charCount = MaxCharacterCount;
            DiagLog($"EnterWorld: charCount={charCount}, characterGuid={characterGuid}");

            // Capture initial state
            LoginStateMonitor.CaptureSnapshot("EnterWorld_START");

            if (charCount <= 0)
            {
                DiagLog("EnterWorld: ABORT - no characters");
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
                        Functions.LuaCall("GlueDialogButton1:Click()");
                        return;
                    }
                    else
                    {
                        // Informational message like "Character list retrieved" - just dismiss and continue
                        DiagLog($"EnterWorld: Informational dialog - dismissing and continuing");
                        Functions.LuaCall("GlueDialogButton1:Click()");
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
                    var buttonCheck = Functions.LuaCallWithResult(
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
            Functions.LuaCall("CharSelectEnterWorldButton:Click()");

            // Capture state immediately after click
            LoginStateMonitor.CaptureSnapshot("EnterWorld_POST_CLICK");

            DiagLog("EnterWorld: Button click completed");
        }
        public void DefaultServerLogin(string accountName, string password)
        {
            if (LoginState != LoginStates.login) return;
            Functions.LuaCall($"DefaultServerLogin('{accountName}', '{password}');");
        }

        public string GlueDialogText => Functions.LuaCallWithResult("{0} = GlueDialogText:GetText()")[0];

        public static int MaxCharacterCount => MemoryManager.ReadInt(0x00B42140);
        public static void ResetLogin()
        {
            Functions.LuaCall("arg1 = 'ESCAPE' GlueDialog_OnKeyDown()");
            Functions.LuaCall("if RealmListCancelButton ~= nil then if RealmListCancelButton:IsVisible() then RealmListCancelButton:Click(); end end ");
        }

        public void JoinBattleGroundQueue()
        {
            string enabled = Functions.LuaCallWithResult("{0} = BattlefieldFrameGroupJoinButton:IsEnabled()")[0];

            if (enabled == "1")
                Functions.LuaCall("BattlefieldFrameGroupJoinButton:Click()");
            else
                Functions.LuaCall("BattlefieldFrameJoinButton:Click()");
        }
        public int GetItemCount(string parItemName)
        {
            var totalCount = 0;
            for (var i = 0; i < 5; i++)
            {
                int slots;
                if (i == 0)
                {
                    slots = 16;
                }
                else
                {
                    var iAdjusted = i - 1;
                    var bag = GetExtraBag(iAdjusted);
                    if (bag == null) continue;
                    slots = bag.NumOfSlots;
                }

                for (var k = 0; k <= slots; k++)
                {
                    var item = GetItem(i, k);
                    if (item?.Info.Name == parItemName) totalCount += (int)item.StackCount;
                }
            }
            return totalCount;
        }

        public int GetItemCount(int itemId)
        {
            var totalCount = 0;
            for (var i = 0; i < 5; i++)
            {
                int slots;
                if (i == 0)
                {
                    slots = 16;
                }
                else
                {
                    var iAdjusted = i - 1;
                    var bag = GetExtraBag(iAdjusted);
                    if (bag == null) continue;
                    slots = bag.NumOfSlots;
                }

                for (var k = 0; k <= slots; k++)
                {
                    var item = GetItem(i, k);
                    if (item?.ItemId == itemId) totalCount += (int)item.StackCount;
                }
            }
            return totalCount;
        }

        public IList<IWoWItem> GetAllItems()
        {
            var items = new List<IWoWItem>();
            for (int bag = 0; bag < 5; bag++)
            {
                var container = GetExtraBag(bag - 1);
                if (bag != 0 && container == null)
                {
                    continue;
                }

                for (int slot = 0; slot < (bag == 0 ? 16 : container.NumOfSlots); slot++)
                {
                    var item = GetItem(bag, slot);
                    if (item == null)
                    {
                        continue;
                    }

                    items.Add(item);
                }
            }

            return items;
        }

        public int CountFreeSlots(bool parCountSpecialSlots)
        {
            var freeSlots = 0;
            for (var i = 0; i < 16; i++)
            {
                var tmpSlotGuid = GetBackpackItemGuid(i);
                if (tmpSlotGuid == 0) freeSlots++;
            }
            var bagGuids = new List<ulong>();
            for (var i = 0; i < 4; i++)
                bagGuids.Add(MemoryManager.ReadUlong(nint.Add(MemoryAddresses.LocalPlayerFirstExtraBag, i * 8)));

            var tmpItems = Containers
                .Where(i => i.NumOfSlots != 0 && bagGuids.Contains(i.Guid)).ToList();

            foreach (var bag in tmpItems)
            {
                if ((bag.Info.Name.Contains("Quiver") || bag.Info.Name.Contains("Ammo") || bag.Info.Name.Contains("Shot") ||
                     bag.Info.Name.Contains("Herb") || bag.Info.Name.Contains("Soul")) && !parCountSpecialSlots) continue;

                for (var i = 1; i < bag.NumOfSlots; i++)
                {
                    var tmpSlotGuid = bag.GetItemGuid(i);
                    if (tmpSlotGuid == 0) freeSlots++;
                }
            }
            return freeSlots;
        }

        public static int EmptyBagSlots
        {
            get
            {
                var bagGuids = new List<ulong>();
                for (var i = 0; i < 4; i++)
                    bagGuids.Add(MemoryManager.ReadUlong(nint.Add(MemoryAddresses.LocalPlayerFirstExtraBag, i * 8)));

                return bagGuids.Count(b => b == 0);
            }
        }

        ILoginScreen IObjectManager.LoginScreen => throw new NotImplementedException();

        IRealmSelectScreen IObjectManager.RealmSelectScreen => throw new NotImplementedException();

        ICharacterSelectScreen IObjectManager.CharacterSelectScreen => throw new NotImplementedException();

        IGossipFrame IObjectManager.GossipFrame => throw new NotImplementedException();

        ILootFrame IObjectManager.LootFrame => throw new NotImplementedException();

        IMerchantFrame IObjectManager.MerchantFrame => throw new NotImplementedException();

        ICraftFrame IObjectManager.CraftFrame => throw new NotImplementedException();

        IQuestFrame IObjectManager.QuestFrame => throw new NotImplementedException();

        IQuestGreetingFrame IObjectManager.QuestGreetingFrame => throw new NotImplementedException();

        ITaxiFrame IObjectManager.TaxiFrame => throw new NotImplementedException();

        ITradeFrame IObjectManager.TradeFrame => throw new NotImplementedException();

        ITrainerFrame IObjectManager.TrainerFrame => throw new NotImplementedException();

        ITalentFrame IObjectManager.TalentFrame => throw new NotImplementedException();

        public List<CharacterSelect> CharacterSelects => throw new NotImplementedException();

        public uint GetBagId(ulong itemGuid)
        {
            var totalCount = 0;
            for (var i = 0; i < 5; i++)
            {
                int slots;
                if (i == 0)
                {
                    slots = 16;
                }
                else
                {
                    var iAdjusted = i - 1;
                    var bag = GetExtraBag(iAdjusted);
                    if (bag == null) continue;
                    slots = bag.NumOfSlots;
                }

                for (var k = 0; k < slots; k++)
                {
                    var item = GetItem(i, k);
                    if (item?.Guid == itemGuid) return (uint)i;
                }
            }
            return (uint)totalCount;
        }

        public uint GetSlotId(ulong itemGuid)
        {
            var totalCount = 0;
            for (var i = 0; i < 5; i++)
            {
                int slots;
                if (i == 0)
                {
                    slots = 16;
                }
                else
                {
                    var iAdjusted = i - 1;
                    var bag = GetExtraBag(iAdjusted);
                    if (bag == null) continue;
                    slots = bag.NumOfSlots;
                }

                for (var k = 0; k < slots; k++)
                {
                    var item = GetItem(i, k);
                    if (item?.Guid == itemGuid) return (uint)k + 1;
                }
            }
            return (uint)totalCount;
        }

        public IWoWItem GetEquippedItem(EquipSlot slot)
        {
            var guid = GetEquippedItemGuid(slot);
            if (guid == 0) return null;
            return Items.FirstOrDefault(i => i.Guid == guid);
        }
        public IEnumerable<IWoWItem> GetEquippedItems()
        {
            IWoWItem headItem = GetEquippedItem(EquipSlot.Head);
            IWoWItem neckItem = GetEquippedItem(EquipSlot.Neck);
            IWoWItem shoulderItem = GetEquippedItem(EquipSlot.Shoulders);
            IWoWItem backItem = GetEquippedItem(EquipSlot.Back);
            IWoWItem chestItem = GetEquippedItem(EquipSlot.Chest);
            IWoWItem shirtItem = GetEquippedItem(EquipSlot.Shirt);
            IWoWItem tabardItem = GetEquippedItem(EquipSlot.Tabard);
            IWoWItem wristItem = GetEquippedItem(EquipSlot.Wrist);
            IWoWItem handsItem = GetEquippedItem(EquipSlot.Hands);
            IWoWItem waistItem = GetEquippedItem(EquipSlot.Waist);
            IWoWItem legsItem = GetEquippedItem(EquipSlot.Legs);
            IWoWItem feetItem = GetEquippedItem(EquipSlot.Feet);
            IWoWItem finger1Item = GetEquippedItem(EquipSlot.Finger1);
            IWoWItem finger2Item = GetEquippedItem(EquipSlot.Finger2);
            IWoWItem trinket1Item = GetEquippedItem(EquipSlot.Trinket1);
            IWoWItem trinket2Item = GetEquippedItem(EquipSlot.Trinket2);
            IWoWItem mainHandItem = GetEquippedItem(EquipSlot.MainHand);
            IWoWItem offHandItem = GetEquippedItem(EquipSlot.OffHand);
            IWoWItem rangedItem = GetEquippedItem(EquipSlot.Ranged);

            List<IWoWItem> list =
            [
                .. headItem != null ? new List<IWoWItem> { headItem } : [],
                .. neckItem != null ? new List<IWoWItem> { neckItem } : [],
                .. shoulderItem != null ? new List<IWoWItem> { shoulderItem } : [],
                .. backItem != null ? new List<IWoWItem> { backItem } : [],
                .. chestItem != null ? new List<IWoWItem> { chestItem } : [],
                .. shirtItem != null ? new List<IWoWItem> { shirtItem } : [],
                .. tabardItem != null ? new List<IWoWItem> { tabardItem } : [],
                .. wristItem != null ? new List<IWoWItem> { wristItem } : [],
                .. handsItem != null ? new List<IWoWItem> { handsItem } : [],
                .. waistItem != null ? new List<IWoWItem> { waistItem } : [],
                .. legsItem != null ? new List<IWoWItem> { legsItem } : [],
                .. feetItem != null ? new List<IWoWItem> { feetItem } : [],
                .. finger1Item != null ? new List<IWoWItem> { finger1Item } : [],
                .. finger2Item != null ? new List<IWoWItem> { finger2Item } : [],
                .. trinket1Item != null ? new List<IWoWItem> { trinket1Item } : [],
                .. trinket2Item != null ? new List<IWoWItem> { trinket2Item } : [],
                .. mainHandItem != null ? new List<IWoWItem> { mainHandItem } : [],
                .. offHandItem != null ? new List<IWoWItem> { offHandItem } : [],
                .. rangedItem != null ? new List<IWoWItem> { rangedItem } : [],
            ];
            return list;
        }

        private IWoWContainer GetExtraBag(int parSlot)
        {
            if (parSlot > 3 || parSlot < 0) return null;
            var bagGuid = MemoryManager.ReadUlong(nint.Add(MemoryAddresses.LocalPlayerFirstExtraBag, parSlot * 8));
            return bagGuid == 0 ? null : Containers.FirstOrDefault(i => i.Guid == bagGuid);
        }

        public IWoWItem GetItem(int parBag, int parSlot)
        {
            parBag += 1;
            switch (parBag)
            {
                case 1:
                    ulong itemGuid = 0;
                    if (parSlot < 16 && parSlot >= 0)
                        itemGuid = GetBackpackItemGuid(parSlot);
                    return itemGuid == 0 ? null : Items.FirstOrDefault(i => i.Guid == itemGuid);

                case 2:
                case 3:
                case 4:
                case 5:
                    var tmpBag = GetExtraBag(parBag - 2);
                    if (tmpBag == null) return null;
                    var tmpItemGuid = tmpBag.GetItemGuid(parSlot);
                    if (tmpItemGuid == 0) return null;
                    return Items.FirstOrDefault(i => i.Guid == tmpItemGuid);

                default:
                    return null;
            }
        }

        private void OnEvent(object sender, OnEventArgs args)
        {
            // Note: CURSOR_UPDATE previously checked memory offset 0xB4B424, but it returns 0 on this WoW client.
            // For now, we rely on DISCONNECTED_FROM_SERVER to detect logout instead.
            if (args.EventName == "DISCONNECTED_FROM_SERVER")
            {
                _ingame1 = false;
                ClearCachedGuid(); // Clear cached GUID on disconnect
                return;
            }
            if (args.EventName != "UNIT_MODEL_CHANGED" &&
                args.EventName != "UPDATE_SELECTED_CHARACTER" &&
                args.EventName != "VARIABLES_LOADED") return;
            _ingame1 = true;
        }

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
        private bool _prevIsConnected = true;
        private LoginStates _prevLoginState = LoginStates.login;
        private uint _prevContinentId = 0xFFFFFFFF;
        private volatile bool _isContinentTransition;

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
                    _isContinentTransition = isContinentTransition;

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
                        // Get player GUID via ThreadSynchronizer to ensure main thread context
                        // Functions.GetPlayerGuid() only works reliably from the main WoW thread
                        try
                        {
                            // Get player GUID using ThreadSynchronizer for thread-safe access
                            ulong playerGuid = ThreadSynchronizer.RunOnMainThread(() => Functions.GetPlayerGuid());
                            if (playerGuid == 0)
                            {
                                Player = null;
                                continue;
                            }

                            // Update the cached GUID for fast IsLoggedIn checks
                            UpdateCachedGuid(playerGuid);

                            byte[] playerGuidParts = BitConverter.GetBytes(playerGuid);
                            PlayerGuid = new HighGuid(playerGuidParts[0..4], playerGuidParts[4..8]);

                            // Get player object pointer using Functions.GetObjectPtr
                            // IMPORTANT: GetObjectPtr calls a WoW native function, NOT a memory read!
                            // It MUST be called from the main thread via ThreadSynchronizer.
                            var playerObject = ThreadSynchronizer.RunOnMainThread(() => Functions.GetObjectPtr(PlayerGuid.FullGuid));

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
                                            var result = Functions.LuaCallWithResult("{0} = UnitName('player')");
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

        /// <summary>
        /// LEGACY: Original enumeration loop using EnumerateVisibleObjects callback.
        /// Kept for reference but not used - replaced by StartSimplePollingLoop().
        /// </summary>
        internal async Task StartEnumeration()
        {
            while (true)
            {
                try
                {
                    EnumerateVisibleObjects();
                    await Task.Delay(500);
                }
                catch (Exception e)
                {
                    Log.Error($"[OBJECT MANAGER] {e}");
                }
            }
        }

        private void EnumerateVisibleObjects()
        {
            ThreadSynchronizer.RunOnMainThread(() =>
            {
                try
                {
                    if (!IsLoggedIn)
                    {
                        return;
                    }
                    // Use memory read instead of Functions.GetPlayerGuid() which returns an index
                    ulong playerGuid = GetPlayerGuidFromMemory();
                    byte[] playerGuidParts = BitConverter.GetBytes(playerGuid);
                    PlayerGuid = new HighGuid(playerGuidParts[0..4], playerGuidParts[4..8]);

                    if (PlayerGuid.FullGuid == 0)
                    {
                        Player = null;
                        return;
                    }
                    var playerObject = Functions.GetObjectPtr(PlayerGuid.FullGuid);
                    if (playerObject == nint.Zero)
                    {
                        Player = null;
                        return;
                    }

                    lock (_objectsLock)
                    {
                        ObjectsBuffer.Clear();
                        Functions.EnumerateVisibleObjects(callbackPtr, 0);
                    }

                    if (Player != null)
                    {
                        try
                        {
                            var petFound = false;

                            foreach (var unit in Units)
                            {
                                if (unit.SummonedByGuid == Player?.Guid)
                                {
                                    Pet = new LocalPet(((WoWObject)unit).Pointer, unit.HighGuid, unit.ObjectType);
                                    petFound = true;
                                }
                            }

                            if (!petFound)
                                Pet = null;

                            RefreshSpells();
                            RefreshSkills();
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[OBJECT MANAGER] Post-enum error: {ex.Message}");
                        }
                    }

                    UpdateProbe();
                }
                catch (Exception ex)
                {
                    Log.Error($"[OBJECT MANAGER] EnumerateVisibleObjects error: {ex.Message}");
                }
            });
        }

        public void RefreshSpells()
        {
            if (Player is not LocalPlayer localPlayer) return;

            localPlayer.PlayerSpells.Clear();
            for (var i = 0; i < 1024; i++)
            {
                var currentSpellId = MemoryManager.ReadInt(MemoryAddresses.LocalPlayerSpellsBase + 4 * i);
                if (currentSpellId == 0) break;

                var spellsBasePtr = MemoryManager.ReadIntPtr(0x00C0D788);
                if (spellsBasePtr == nint.Zero) continue;

                var spellPtr = MemoryManager.ReadIntPtr(spellsBasePtr + currentSpellId * 4);
                if (spellPtr == nint.Zero) continue;

                var spellNamePtr = MemoryManager.ReadIntPtr(spellPtr + 0x1E0);
                if (spellNamePtr == nint.Zero) continue;

                var name = MemoryManager.ReadString(spellNamePtr);
                if (string.IsNullOrEmpty(name)) continue;

                if (localPlayer.PlayerSpells.TryGetValue(name, out int[]? value))
                    localPlayer.PlayerSpells[name] = [.. value, currentSpellId];
                else
                    localPlayer.PlayerSpells.Add(name, [currentSpellId]);
            }
        }

        public void RefreshSkills()
        {
            if (Player is not LocalPlayer localPlayer) return;

            localPlayer.PlayerSkills.Clear();
            var skillPtr1 = MemoryManager.ReadIntPtr(nint.Add(localPlayer.Pointer, 8));
            if (skillPtr1 == nint.Zero) return;

            var skillPtr2 = nint.Add(skillPtr1, 0xB38);

            var maxSkills = MemoryManager.ReadInt(0x00B700B4);
            // Sanity check to prevent infinite loops
            if (maxSkills < 0 || maxSkills > 1000) return;

            for (var i = 0; i < maxSkills + 12; i++)
            {
                var curPointer = nint.Add(skillPtr2, i * 12);

                var id = (Skills)MemoryManager.ReadShort(curPointer);
                if (!Enum.IsDefined(typeof(Skills), id))
                {
                    continue;
                }

                localPlayer.PlayerSkills.Add((short)id);
            }
        }
        // EnumerateVisibleObjects callback for Vanilla 1.12.1: ThisCall with (filter, guid)
        // Parameter order is swapped compared to non-Vanilla clients
        private int CallbackVanilla(int filter, ulong guid)
        {
            return CallbackInternal(guid, filter);  // Swap back to (guid, filter) for internal use
        }

        private int CallbackInternal(ulong guid, int filter)
        {
            try
            {
                if (guid == 0)
                {
                    return 0;
                }

                var pointer = Functions.GetObjectPtr(guid);

                if (pointer == nint.Zero)
                {
                    return 1; // Continue enumeration
                }

                var objectType = (WoWObjectType)MemoryManager.ReadInt(nint.Add(pointer, OBJECT_TYPE_OFFSET));

                byte[] guidParts = BitConverter.GetBytes(guid);
                // Note: On private servers, low GUIDs like 5 are perfectly valid
                HighGuid highGuid = new(guidParts[0..4], guidParts[4..8]);  // Fixed: was [0..3], now [0..4] for 4 bytes

                switch (objectType)
                {
                    case WoWObjectType.Container:
                        ObjectsBuffer.Add(new WoWContainer(pointer, highGuid, objectType));
                        break;
                    case WoWObjectType.Item:
                        ObjectsBuffer.Add(new WoWItem(pointer, highGuid, objectType));
                        break;
                    case WoWObjectType.Player:
                        // GetPlayerGuid() returns the low GUID (e.g., 5) which is valid on private servers
                        var ourPlayerGuid = Functions.GetPlayerGuid();
                        var ourPlayerPtr = Functions.GetObjectPtr(ourPlayerGuid);
                        var isLocalPlayer = (pointer == ourPlayerPtr);
                        if (isLocalPlayer)
                        {
                            var player = new LocalPlayer(pointer, highGuid, objectType);
                            Player = player;
                            ObjectsBuffer.Add(player);
                        }
                        else
                        {
                            ObjectsBuffer.Add(new WoWPlayer(pointer, highGuid, objectType));
                        }
                        break;
                    case WoWObjectType.GameObj:
                        ObjectsBuffer.Add(new WoWGameObject(pointer, highGuid, objectType));
                        break;
                    case WoWObjectType.Unit:
                        ObjectsBuffer.Add(new WoWUnit(pointer, highGuid, objectType));
                        break;
                }

                return 1;
            }
            catch (Exception e)
            {
                Log.Error($"OBJECT MANAGER: CallbackInternal => {e.Message} {e.StackTrace}");
                return 1; // Continue enumeration even on error
            }
        }

        private void UpdateProbe()
        {
            try
            {
                if (IsLoggedIn && Player != null)
                {
                    // Track if this is our first time entering the world (to fire event)
                    bool justEnteredWorld = !HasEnteredWorld;

                    // Mark that we've successfully entered the world
                    if (justEnteredWorld)
                    {
                        HasEnteredWorld = true;
                    }

                    // Update snapshot with character info for StateManager
                    var playerName = Player.Name ?? "";

                    // Lua fallback: Name cache may not be populated on first login
                    if (string.IsNullOrEmpty(playerName))
                    {
                        try
                        {
                            var result = Functions.LuaCallWithResult("{0} = UnitName('player')");
                            if (result != null && result.Length > 0 && !string.IsNullOrEmpty(result[0]))
                            {
                                playerName = result[0];
                            }
                        }
                        catch
                        {
                            // Lua name fallback failed, continue with empty name
                        }
                    }

                    _characterState.CharacterName = playerName;
                    _characterState.Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    // Fire event when first entering world (AFTER setting character name in snapshot)
                    // This allows subscribers to send the snapshot immediately while we're definitely in-world
                    if (justEnteredWorld)
                    {
                        try
                        {
                            OnEnteredWorld?.Invoke(this, EventArgs.Empty);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[OBJECT MANAGER] OnEnteredWorld event handler error: {ex.Message}");
                        }
                    }

                    //_characterState.Guid = playerGuid;
                    //_characterState.Zone = MinimapZoneText;
                    //_characterState.InParty = int.Parse(Functions.LuaCallWithResult("{0} = GetNumPartyMembers()")[0]) > 0;
                    //_characterState.InRaid = int.Parse(Functions.LuaCallWithResult("{0} = GetNumRaidMembers()")[0]) > 0;
                    //_characterState.MapId = (int)MapId;
                    //_characterState.Race = Enum.GetValues(typeof(Race)).Cast<Race>().Where(x => x.GetDescription() == Player.Race).First();
                    //_characterState.Facing = Player.Facing;
                    //_characterState.Position = new Vector3(Player.Position.X, Player.Position.Y, Player.Position.Z);

                    IWoWItem headItem = GetEquippedItem(EquipSlot.Head);
                    IWoWItem neckItem = GetEquippedItem(EquipSlot.Neck);
                    IWoWItem shoulderItem = GetEquippedItem(EquipSlot.Shoulders);
                    IWoWItem backItem = GetEquippedItem(EquipSlot.Back);
                    IWoWItem chestItem = GetEquippedItem(EquipSlot.Chest);
                    IWoWItem shirtItem = GetEquippedItem(EquipSlot.Shirt);
                    IWoWItem tabardItem = GetEquippedItem(EquipSlot.Tabard);
                    IWoWItem wristItem = GetEquippedItem(EquipSlot.Wrist);
                    IWoWItem handsItem = GetEquippedItem(EquipSlot.Hands);
                    IWoWItem waistItem = GetEquippedItem(EquipSlot.Waist);
                    IWoWItem legsItem = GetEquippedItem(EquipSlot.Legs);
                    IWoWItem feetItem = GetEquippedItem(EquipSlot.Feet);
                    IWoWItem finger1Item = GetEquippedItem(EquipSlot.Finger1);
                    IWoWItem finger2Item = GetEquippedItem(EquipSlot.Finger2);
                    IWoWItem trinket1Item = GetEquippedItem(EquipSlot.Trinket1);
                    IWoWItem trinket2Item = GetEquippedItem(EquipSlot.Trinket2);
                    IWoWItem mainHandItem = GetEquippedItem(EquipSlot.MainHand);
                    IWoWItem offHandItem = GetEquippedItem(EquipSlot.OffHand);
                    IWoWItem rangedItem = GetEquippedItem(EquipSlot.Ranged);

                    //if (headItem != null)
                    //{
                    //    _characterState.HeadItem = headItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.HeadItem = 0;
                    //}
                    //if (neckItem != null)
                    //{
                    //    _characterState.NeckItem = neckItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.NeckItem = 0;
                    //}
                    //if (shoulderItem != null)
                    //{
                    //    _characterState.ShoulderItem = shoulderItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.ShoulderItem = 0;
                    //}
                    //if (backItem != null)
                    //{
                    //    _characterState.BackItem = backItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.BackItem = 0;
                    //}
                    //if (chestItem != null)
                    //{
                    //    _characterState.ChestItem = chestItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.ChestItem = 0;
                    //}
                    //if (shirtItem != null)
                    //{
                    //    _characterState.ShirtItem = shirtItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.ShirtItem = 0;
                    //}
                    //if (tabardItem != null)
                    //{
                    //    _characterState.TabardItem = tabardItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.TabardItem = 0;
                    //}
                    //if (wristItem != null)
                    //{
                    //    _characterState.WristsItem = wristItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.WristsItem = 0;
                    //}
                    //if (handsItem != null)
                    //{
                    //    _characterState.HandsItem = handsItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.HandsItem = 0;
                    //}
                    //if (waistItem != null)
                    //{
                    //    _characterState.WaistItem = waistItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.WaistItem = 0;
                    //}
                    //if (legsItem != null)
                    //{
                    //    _characterState.LegsItem = legsItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.LegsItem = 0;
                    //}
                    //if (feetItem != null)
                    //{
                    //    _characterState.FeetItem = feetItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.FeetItem = 0;
                    //}
                    //if (finger1Item != null)
                    //{
                    //    _characterState.Finger1Item = finger1Item.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.Finger1Item = 0;
                    //}
                    //if (finger2Item != null)
                    //{
                    //    _characterState.Finger2Item = finger2Item.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.Finger2Item = 0;
                    //}
                    //if (trinket1Item != null)
                    //{
                    //    _characterState.Trinket1Item = trinket1Item.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.Trinket1Item = 0;
                    //}
                    //if (trinket2Item != null)
                    //{
                    //    _characterState.Trinket2Item = trinket2Item.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.Trinket2Item = 0;
                    //}
                    //if (mainHandItem != null)
                    //{
                    //    _characterState.MainHandItem = mainHandItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.MainHandItem = 0;
                    //}
                    //if (offHandItem != null)
                    //{
                    //    _characterState.OffHandItem = offHandItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.OffHandItem = 0;
                    //}
                    //if (rangedItem != null)
                    //{
                    //    _characterState.RangedItem = rangedItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.RangedItem = 0;
                    //}
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[OBJECT MANAGER]{ex.Message} {ex.StackTrace}");
            }
        }

        public void LeaveGroup()
        {
            Functions.LuaCall("LeaveParty()");
        }

        public void ResetInstances()
        {
            Functions.LuaCall("ResetInstances()");
        }

        public void PickupMacro(uint v)
        {
            Functions.LuaCall($"PickupMacro({v})");
        }

        public void PlaceAction(uint v)
        {
            Functions.LuaCall($"PlaceAction({v})");
        }

        public void ConvertToRaid()
        {
            Functions.LuaCall("ConvertToRaid()");
        }

        public static void InviteToGroup(string characterName)
        {
            Functions.LuaCall($"InviteByName('{characterName}')");
        }
        public ulong GetBackpackItemGuid(int slot) => MemoryManager.ReadUlong(((LocalPlayer)Player).GetDescriptorPtr() + (MemoryAddresses.LocalPlayer_BackpackFirstItemOffset + slot * 8));

        public ulong GetEquippedItemGuid(EquipSlot slot) => MemoryManager.ReadUlong(nint.Add(((LocalPlayer)Player).Pointer, MemoryAddresses.LocalPlayer_EquipmentFirstItemOffset + ((int)slot - 1) * 0x8));


        public void StartMeleeAttack()
        {
            if (!Player.IsCasting && (Player.Class == Class.Warlock || Player.Class == Class.Mage || Player.Class == Class.Priest))
            {
                Functions.LuaCall(WandLuaScript);
            }
            else if (Player.Class != Class.Hunter)
            {
                Functions.LuaCall(AutoAttackLuaScript);
            }
        }

        public void DoEmote(Emote emote)
        {
            throw new NotImplementedException();
        }

        public void DoEmote(TextEmote emote)
        {
            throw new NotImplementedException();
        }

        public uint GetManaCost(string healingTouch)
        {
            throw new NotImplementedException();
        }

        public void StartRangedAttack()
        {
            throw new NotImplementedException();
        }

        public void StopAttack()
        {
            throw new NotImplementedException();
        }

        public bool IsSpellReady(string spellName)
        {
            throw new NotImplementedException();
        }

        public void CastSpell(string spellName, int rank = -1, bool castOnSelf = false)
        {
            throw new NotImplementedException();
        }

        public void CastSpell(uint spellId, int rank = -1, bool castOnSelf = false)
        {
            throw new NotImplementedException();
        }

        public void StartWandAttack()
        {
            throw new NotImplementedException();
        }

        public void MoveToward(Position position, float facing)
        {
            SetFacing(facing);
            StartMovement(ControlBits.Front);
        }

        public void StopCasting()
        {
            throw new NotImplementedException();
        }

        public void CastSpell(int spellId, int rank = -1, bool castOnSelf = false)
        {
            throw new NotImplementedException();
        }

        public bool CanCastSpell(int spellId, ulong targetGuid)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyCollection<uint> KnownSpellIds
        {
            get
            {
                if (Player is not LocalPlayer lp) return [];
                return lp.PlayerSpells.Values.SelectMany(v => v).Select(id => (uint)id).ToArray();
            }
        }

        public void UseItem(int bagId, int slotId, ulong targetGuid = 0)
        {
            throw new NotImplementedException();
        }

        public IWoWItem GetContainedItem(int bagSlot, int slotId)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IWoWItem> GetContainedItems()
        {
            throw new NotImplementedException();
        }

        public uint GetBagGuid(EquipSlot equipSlot)
        {
            throw new NotImplementedException();
        }

        public void PickupContainedItem(int bagSlot, int slotId, int quantity)
        {
            throw new NotImplementedException();
        }

        public void PlaceItemInContainer(int bagSlot, int slotId)
        {
            throw new NotImplementedException();
        }

        public void DestroyItemInContainer(int bagSlot, int slotId, int quantity = -1)
        {
            throw new NotImplementedException();
        }

        public void Logout()
        {
            throw new NotImplementedException();
        }

        public void SplitStack(int bag, int slot, int quantity, int destinationBag, int destinationSlot)
        {
            throw new NotImplementedException();
        }

        public void EquipItem(int bagSlot, int slotId, EquipSlot? equipSlot = null)
        {
            throw new NotImplementedException();
        }

        public void UnequipItem(EquipSlot slot)
        {
            throw new NotImplementedException();
        }

        public void AcceptResurrect()
        {
            throw new NotImplementedException();
        }

        public void Initialize(IWoWActivitySnapshot parProbe)
        {
            throw new NotImplementedException();
        }

        public sbyte GetTalentRank(uint tabIndex, uint talentIndex)
        {
            throw new NotImplementedException();
        }

        public void PickupInventoryItem(uint inventorySlot)
        {
            throw new NotImplementedException();
        }

        public uint GetItemCount(uint itemId)
        {
            throw new NotImplementedException();
        }

        public void UseContainerItem(int v1, int v2)
        {
            throw new NotImplementedException();
        }

        public void PickupContainerItem(uint v1, uint v2)
        {
            throw new NotImplementedException();
        }

        public IWoWUnit GetTarget(IWoWUnit woWUnit)
        {
            throw new NotImplementedException();
        }

        public void InviteToGroup(ulong guid)
        {
            // Find the player name by GUID from enumerated objects
            var player = Players.FirstOrDefault(p => p.Guid == guid);
            if (player != null)
                Functions.LuaCall($"InviteByName('{player.Name}')");
        }

        public void InviteByName(string characterName)
        {
            Functions.LuaCall($"InviteByName('{characterName}')");
        }

        public void KickPlayer(ulong guid)
        {
            var player = Players.FirstOrDefault(p => p.Guid == guid);
            if (player != null)
                Functions.LuaCall($"UninviteByName('{player.Name}')");
        }

        public void DeclineGroupInvite()
        {
            ThreadSynchronizer.RunOnMainThread(() =>
            {
                Functions.LuaCall("DeclineGroup()");
                Functions.LuaCall("StaticPopup1Button2:Click()");
            });
        }

        public void DisbandGroup()
        {
            // In vanilla, leader leaving disbands the group
            Functions.LuaCall("LeaveParty()");
        }

        public bool HasPendingGroupInvite()
        {
            var result = Functions.LuaCallWithResult("{0} = StaticPopup1:IsVisible() and StaticPopup1.which == 'PARTY_INVITE'");
            return result.Length > 0 && result[0] == "1";
        }

        public bool HasLootRollWindow(int itemId)
        {
            // Check if a loot roll frame is visible for this item
            var result = Functions.LuaCallWithResult(
                $"{{0}} = 0; for i=1,4 do local f = getglobal('GroupLootFrame'..i); if f and f:IsVisible() then {{0}} = 1 end end");
            return result.Length > 0 && result[0] == "1";
        }

        public void LootPass(int itemId)
        {
            // Pass on loot roll (rollID is 1-based, we approximate by clicking pass on visible frame)
            Functions.LuaCall("for i=1,4 do local f = getglobal('GroupLootFrame'..i); if f and f:IsVisible() then local b = getglobal('GroupLootFrame'..i..'PassButton'); if b then b:Click() end end end");
        }

        public void LootRollGreed(int itemId)
        {
            Functions.LuaCall("for i=1,4 do local f = getglobal('GroupLootFrame'..i); if f and f:IsVisible() then local b = getglobal('GroupLootFrame'..i..'GreedButton'); if b then b:Click() end end end");
        }

        public void LootRollNeed(int itemId)
        {
            Functions.LuaCall("for i=1,4 do local f = getglobal('GroupLootFrame'..i); if f and f:IsVisible() then local b = getglobal('GroupLootFrame'..i..'NeedButton'); if b then b:Click() end end end");
        }

        public void AssignLoot(int itemId, ulong playerGuid)
        {
            // Master looter: give loot to specific player
            var player = Players.FirstOrDefault(p => p.Guid == playerGuid);
            if (player != null)
            {
                Functions.LuaCall($"for i=1,GetNumLootItems() do GiveMasterLoot(i, 1) end");
            }
        }

        public void SetGroupLoot(GroupLootSetting setting)
        {
            // 0=FFA, 1=RoundRobin, 2=MasterLooter, 3=GroupLoot, 4=NeedBeforeGreed
            Functions.LuaCall($"SetLootMethod('group')");
        }

        public void PromoteLootManager(ulong playerGuid)
        {
            var player = Players.FirstOrDefault(p => p.Guid == playerGuid);
            if (player != null)
                Functions.LuaCall($"SetLootMethod('master', '{player.Name}')");
        }

        public void PromoteAssistant(ulong playerGuid)
        {
            var player = Players.FirstOrDefault(p => p.Guid == playerGuid);
            if (player != null)
                Functions.LuaCall($"PromoteToAssistant('{player.Name}')");
        }

        public void PromoteLeader(ulong playerGuid)
        {
            var player = Players.FirstOrDefault(p => p.Guid == playerGuid);
            if (player != null)
                Functions.LuaCall($"PromoteToLeader('{player.Name}')");
        }
        public void SetFacing(float facing)
        {
            Functions.SetFacing(nint.Add(((LocalPlayer)Player).Pointer, MemoryAddresses.LocalPlayer_SetFacingOffset), facing);
            Functions.SendMovementUpdate(((LocalPlayer)Player).Pointer, (int)Opcode.MSG_MOVE_SET_FACING);
        }
        // the client will NOT send a packet to the server if a key is already pressed, so you're safe to spam this
        public void StartMovement(ControlBits bits)
        {
            if (bits == ControlBits.Nothing)
                return;

            Functions.SetControlBit((int)bits, 1, Environment.TickCount);
        }

        public void StopAllMovement()
        {
            // Always clear all movement control bits unconditionally.
            // MovementFlags can read 0x0 when opposing directions cancel out,
            // but the underlying control bits are still set.
            var bits = ControlBits.Front | ControlBits.Back | ControlBits.Left | ControlBits.Right | ControlBits.StrafeLeft | ControlBits.StrafeRight;
            StopMovement(bits);
        }

        public void StopMovement(ControlBits bits)
        {
            if (bits == ControlBits.Nothing)
                return;

            Functions.SetControlBit((int)bits, 0, Environment.TickCount);
        }

        public void Jump()
        {
            // Vanilla 1.12.1 has no dedicated jump function (JumpFunPtr = 0).
            // SetControlBit(Jump) doesn't work for impulse actions.
            // Simulate spacebar press via PostMessage to trigger the jump.
            ThreadSynchronizer.SimulateSpacebarPress();
        }

        public static void Stand() => Functions.LuaCall("DoEmote(\"STAND\")");

        public void ReleaseCorpse() => Functions.ReleaseCorpse(((LocalPlayer)Player).Pointer);

        public void RetrieveCorpse() => Functions.RetrieveCorpse();

        // ── Smooth turning state (ported from reference LocalPlayer.cs) ──
        private readonly Random _facingRandom = new();
        private bool _turning;
        private int _totalTurns;
        private int _turnCount;
        private float _amountPerTurn;
        private Position? _turningToward;

        /// <summary>
        /// Smoothly turns the player to face the target position.
        /// Splits the turn into 2-5 steps for human-like behavior.
        /// Call repeatedly (e.g. every tick) until the player is facing the target.
        /// </summary>
        public void Face(Position pos)
        {
            if (pos == null || Player == null) return;

            // Correct negative facing (client bug)
            if (Player.Facing < 0)
            {
                SetFacing((float)(Math.PI * 2) + Player.Facing);
                return;
            }

            // If we're already turning toward a different position, reset
            if (_turning && pos != _turningToward)
            {
                ResetFacingState();
                return;
            }

            // Already facing the target - nothing to do
            if (!_turning && Player.IsFacing(pos))
                return;

            if (!_turning)
            {
                var requiredFacing = Player.GetFacingForPosition(pos);
                float amountToTurn;
                if (requiredFacing > Player.Facing)
                {
                    if (requiredFacing - Player.Facing > Math.PI)
                        amountToTurn = -((float)(Math.PI * 2) - requiredFacing + Player.Facing);
                    else
                        amountToTurn = requiredFacing - Player.Facing;
                }
                else
                {
                    if (Player.Facing - requiredFacing > Math.PI)
                        amountToTurn = (float)(Math.PI * 2) - Player.Facing + requiredFacing;
                    else
                        amountToTurn = requiredFacing - Player.Facing;
                }

                // Small turn - just snap to target
                if (Math.Abs(amountToTurn) < 0.05)
                {
                    SetFacing(requiredFacing);
                    ResetFacingState();
                    return;
                }

                _turning = true;
                _turningToward = pos;
                _totalTurns = _facingRandom.Next(2, 5);
                _amountPerTurn = amountToTurn / _totalTurns;
            }

            if (_turning)
            {
                if (_turnCount < _totalTurns - 1)
                {
                    var twoPi = (float)(Math.PI * 2);
                    var newFacing = Player.Facing + _amountPerTurn;

                    if (newFacing < 0)
                        newFacing = twoPi + _amountPerTurn + Player.Facing;
                    else if (newFacing > twoPi)
                        newFacing = _amountPerTurn - (twoPi - Player.Facing);

                    SetFacing(newFacing);
                    _turnCount++;
                }
                else
                {
                    SetFacing(Player.GetFacingForPosition(pos));
                    ResetFacingState();
                }
            }
        }

        private void ResetFacingState()
        {
            _turning = false;
            _totalTurns = 0;
            _turnCount = 0;
            _amountPerTurn = 0;
            _turningToward = null;
            StopMovement(ControlBits.StrafeLeft);
            StopMovement(ControlBits.StrafeRight);
        }

        public void MoveToward(Position pos)
        {
            Face(pos);
            StartMovement(ControlBits.Front);
        }

        public void Turn180()
        {
            var newFacing = Player.Facing + Math.PI;
            if (newFacing > Math.PI * 2)
                newFacing -= Math.PI * 2;
            SetFacing((float)newFacing);
        }

    }
}
