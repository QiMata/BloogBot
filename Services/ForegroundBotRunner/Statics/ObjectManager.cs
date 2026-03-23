using ForegroundBotRunner.Frames;
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
    public partial class ObjectManager : IObjectManager
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

        /// <summary>Crash-safe trace log for diagnosing ACCESS_VIOLATION during map transitions.
        /// Uses cached DiagnosticLogPath directory to avoid Process.GetCurrentProcess() calls.</summary>
        private static void CrashTrace(string message)
        {
            try
            {
                var logPath = Path.Combine(Path.GetDirectoryName(DiagnosticLogPath)!, "crash_trace.log");
                using var sw = new StreamWriter(logPath, true);
                sw.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ObjMgr] {message}");
                sw.Flush();
            }
            catch { }
        }

        // LUA SCRIPTS


        // LUA SCRIPTS


        /// <summary>
        /// Wraps Functions.LuaCall in ThreadSynchronizer.RunOnMainThread.
        /// All Lua calls MUST execute on WoW's main thread — calling from a background
        /// thread (e.g., BotRunnerService) silently fails.
        /// </summary>


        /// <summary>
        /// Wraps Functions.LuaCall in ThreadSynchronizer.RunOnMainThread.
        /// All Lua calls MUST execute on WoW's main thread — calling from a background
        /// thread (e.g., BotRunnerService) silently fails.
        /// </summary>
        internal static void MainThreadLuaCall(string lua) =>
            ThreadSynchronizer.RunOnMainThread(() => Functions.LuaCall(lua));

        /// <summary>
        /// Wraps Functions.LuaCallWithResult in ThreadSynchronizer.RunOnMainThread.
        /// Returns the Lua result array; blocks until the main thread processes the call.
        /// </summary>


        /// <summary>
        /// Wraps Functions.LuaCallWithResult in ThreadSynchronizer.RunOnMainThread.
        /// Returns the Lua result array; blocks until the main thread processes the call.
        /// </summary>


        /// <summary>
        /// Wraps Functions.LuaCallWithResult in ThreadSynchronizer.RunOnMainThread.
        /// Returns the Lua result array; blocks until the main thread processes the call.
        /// </summary>


        /// <summary>
        /// Wraps Functions.LuaCallWithResult in ThreadSynchronizer.RunOnMainThread.
        /// Returns the Lua result array; blocks until the main thread processes the call.
        /// </summary>
        private static string[] MainThreadLuaCallWithResult(string lua) =>
            ThreadSynchronizer.RunOnMainThread(() => Functions.LuaCallWithResult(lua));

        // Login screen implementations for BotRunnerService integration


        // Login screen implementations for BotRunnerService integration


        // Login screen implementations for BotRunnerService integration


        // Login screen implementations for BotRunnerService integration
        private readonly FgLoginScreen _fgLoginScreen;


        private readonly FgRealmSelectScreen _fgRealmSelectScreen;


        private readonly FgCharacterSelectScreen _fgCharacterSelectScreen;

        private readonly FgGossipFrame _fgGossipFrame;

        private readonly ILootFrame _fgLootFrame;

        private readonly FgMerchantFrame _fgMerchantFrame;

        private readonly FgCraftFrame _fgCraftFrame;

        private readonly FgQuestFrame _fgQuestFrame;

        private readonly FgTaxiFrame _fgTaxiFrame;

        private readonly FgTrainerFrame _fgTrainerFrame;

        private readonly FgTalentFrame _fgTalentFrame;



        public ObjectManager(IWoWEventHandler eventHandler, IWoWActivitySnapshot parProbe)
        {
            EventHandler = eventHandler;
            _characterState = parProbe;

            _fgLoginScreen = new FgLoginScreen(
                () => GetCurrentScreenState(),
                (user, pass) => DefaultServerLogin(user, pass),
                () => ResetLogin(),
                () => DismissGlueDialog());
            _fgRealmSelectScreen = new FgRealmSelectScreen(
                () => GetCurrentScreenState(),
                () => MaxCharacterCount,
                lua => MainThreadLuaCall(lua));
            _fgCharacterSelectScreen = new FgCharacterSelectScreen(
                () => GetCurrentScreenState(),
                () => MaxCharacterCount,
                lua => MainThreadLuaCall(lua));
            _fgGossipFrame = new FgGossipFrame(
                lua => MainThreadLuaCall(lua),
                lua => MainThreadLuaCallWithResult(lua),
                () => GetActiveNpcInteractionGuid());
            _fgLootFrame = new FgLootFrame(
                lua => MainThreadLuaCall(lua),
                lua => MainThreadLuaCallWithResult(lua));
            _fgMerchantFrame = new FgMerchantFrame(
                lua => MainThreadLuaCall(lua),
                lua => MainThreadLuaCallWithResult(lua),
                (bag, slot) => GetContainedItem(bag, slot),
                () => GetContainedItems(),
                slot => GetEquippedItem(slot),
                () => GetActiveNpcInteractionGuid());
            _fgCraftFrame = new FgCraftFrame(
                lua => MainThreadLuaCall(lua),
                lua => MainThreadLuaCallWithResult(lua));
            _fgQuestFrame = new FgQuestFrame(
                lua => MainThreadLuaCall(lua),
                lua => MainThreadLuaCallWithResult(lua),
                () => GetActiveNpcInteractionGuid());
            _fgTaxiFrame = new FgTaxiFrame(
                lua => MainThreadLuaCall(lua),
                lua => MainThreadLuaCallWithResult(lua));
            _fgTrainerFrame = new FgTrainerFrame(
                lua => MainThreadLuaCall(lua),
                lua => MainThreadLuaCallWithResult(lua));
            _fgTalentFrame = new FgTalentFrame(
                lua => MainThreadLuaCall(lua),
                lua => MainThreadLuaCallWithResult(lua),
                spellId => GetSpellNameFromDb(spellId),
                spellName => GetSpellIdsByName(spellName));

            CallbackDelegate = CallbackVanilla;
            callbackPtr = Marshal.GetFunctionPointerForDelegate(CallbackDelegate);

            eventHandler.OnEvent += OnEvent;

            // Start a simple polling loop that only reads static memory addresses
            Task.Factory.StartNew(async () => await StartSimplePollingLoop());
        }


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
        /// <summary>
        /// Walk the WoW object manager linked list in memory to find an object by GUID.
        /// Does NOT require ThreadSynchronizer — pure memory reads, safe from any thread.
        /// Used as a fallback when Functions.GetObjectPtr() via ThreadSynchronizer fails
        /// (e.g., during world entry when WoW's message loop isn't processing WM_USER yet).
        /// </summary>

        /// <summary>
        /// Walk the WoW object manager linked list in memory to find an object by GUID.
        /// Does NOT require ThreadSynchronizer — pure memory reads, safe from any thread.
        /// Used as a fallback when Functions.GetObjectPtr() via ThreadSynchronizer fails
        /// (e.g., during world entry when WoW's message loop isn't processing WM_USER yet).
        /// </summary>


        // Counter for IsLoggedIn calls to limit logging


        // Counter for IsLoggedIn calls to limit logging
        private static int _isLoggedInCallCount = 0;

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


        // Volatile to ensure visibility across threads (enumeration thread sets, main loop reads)


        // Volatile to ensure visibility across threads (enumeration thread sets, main loop reads)
        private volatile bool _hasEnteredWorld;



        private static int _antiAfkLogCounter = 0;


        /// <summary>
        /// Maps spell name → list of spell IDs from the client spell DB (0x00C0D788 pointer chain).
        /// Built once (non-null DB pointer required). Used to translate LEARNED_SPELL arg1 names
        /// into IDs. Different ranks share a name (e.g. Deflection ranks 1-5 = IDs 16462-16466).
        /// </summary>


        /// <summary>
        /// Maps spell name → list of spell IDs from the client spell DB (0x00C0D788 pointer chain).
        /// Built once (non-null DB pointer required). Used to translate LEARNED_SPELL arg1 names
        /// into IDs. Different ranks share a name (e.g. Deflection ranks 1-5 = IDs 16462-16466).
        /// </summary>
        private Dictionary<string, List<uint>> _spellNameToIds = new(StringComparer.OrdinalIgnoreCase);
    }
}
