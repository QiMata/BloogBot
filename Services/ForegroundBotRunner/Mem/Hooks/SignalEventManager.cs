using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ForegroundBotRunner.Mem.Hooks
{
    public class SignalEventManager
    {
        private delegate void SignalEventDelegate(string eventName, string format, uint firstArgPtr);

        private delegate void SignalEventNoArgsDelegate(string eventName);

        // Diagnostic logging path
        private static readonly string DiagnosticLogPath;
        private static readonly object DiagnosticLogLock = new();
        private static int _eventCount = 0;

        private static volatile bool _hooksInitialized;

        static SignalEventManager()
        {
            string wowDir;
            try
            {
                wowDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName) ?? AppContext.BaseDirectory;
            }
            catch
            {
                wowDir = AppContext.BaseDirectory;
            }
            var logsDir = Path.Combine(wowDir, "WWoWLogs");
            try { Directory.CreateDirectory(logsDir); } catch { }
            DiagnosticLogPath = Path.Combine(logsDir, "signal_event_manager.log");
            try { File.WriteAllText(DiagnosticLogPath, $"=== SignalEventManager Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n"); } catch { }

            // DEFERRED: Hooks inject assembly into WoW's event functions, which interferes
            // with the world server handshake if done before world entry.
            DiagLog("SignalEventManager static constructor COMPLETED (hooks DEFERRED)");
        }

        /// <summary>
        /// Initialize the assembly hooks into WoW's signal event system.
        /// Must be called AFTER the player has successfully entered the world.
        /// </summary>
        public static void InitializeHooks()
        {
            if (_hooksInitialized)
                return;

            DiagLog("InitializeHooks STARTING");

            try
            {
                InitializeSignalEventHook();
            }
            catch (Exception ex)
            {
                DiagLog($"InitializeSignalEventHook FAILED: {ex.Message}");
            }
            try
            {
                InitializeSignalEventHookNoArgs();
            }
            catch (Exception ex)
            {
                DiagLog($"InitializeSignalEventHookNoArgs FAILED: {ex.Message}");
            }

            _hooksInitialized = true;
            DiagLog("InitializeHooks COMPLETED");
        }

        private static void DiagLog(string message)
        {
            try
            {
                lock (DiagnosticLogLock)
                {
                    var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
                    File.AppendAllText(DiagnosticLogPath, line);
                }
            }
            catch { }
        }

        #region InitializeSignalEventHook
        private static SignalEventDelegate signalEventDelegate;

        private static void InitializeSignalEventHook()
        {
            signalEventDelegate = new SignalEventDelegate(SignalEventHook);
            var addrToDetour = Marshal.GetFunctionPointerForDelegate(signalEventDelegate);

            var instructions = new[]
            {
                "push ebx",
                "push esi",
                "call 0x007040D0",
                "pushfd",
                "pushad",
                "mov eax, ebp",
                "add eax, 0x10",
                "push eax",
                "mov eax, [ebp + 0xC]",
                "push eax",
                "mov edi, [edi]",
                "push edi",
                $"call 0x{(uint) addrToDetour:X}",
                "popad",
                "popfd",
                $"jmp 0x{(uint) (MemoryAddresses.SignalEventFunPtr + 7):X}"
            };
            var signalEventDetour = MemoryManager.InjectAssembly("SignalEventDetour", instructions);
            MemoryManager.InjectAssembly("SignalEventHook", (uint)MemoryAddresses.SignalEventFunPtr, "jmp " + signalEventDetour);

            // Verify hook was written
            var hookBytes = MemoryManager.ReadBytes((nint)MemoryAddresses.SignalEventFunPtr, 5);
            if (hookBytes != null && hookBytes[0] == 0xE9)
                DiagLog($"SignalEvent hook INSTALLED at 0x{MemoryAddresses.SignalEventFunPtr:X8} → detour 0x{(uint)signalEventDetour:X8}");
            else
                DiagLog($"SignalEvent hook FAILED at 0x{MemoryAddresses.SignalEventFunPtr:X8}: first byte=0x{(hookBytes?[0] ?? 0):X2}");
        }

        private static void SignalEventHook(string eventName, string typesArg, uint firstArgPtr)
        {
            _eventCount++;
            // Log first 20 events unconditionally, then only log interesting events (errors, skills, chat)
            if (_eventCount <= 20
                || eventName.StartsWith("UI_ERROR")
                || eventName.StartsWith("UI_INFO")
                || eventName.StartsWith("CHAT_MSG_SKILL")
                || eventName == "CHAT_MSG_SYSTEM")
                DiagLog($"EVENT[{_eventCount}]: {eventName} format={typesArg}");

            var types = typesArg.TrimStart('%').Split('%');
            var list = new object[types.Length];
            for (var i = 0; i < types.Length; i++)
            {
                var tmpPtr = firstArgPtr + (uint)i * 4;
                if (types[i] == "s")
                {
                    var ptr = MemoryManager.ReadInt((nint)tmpPtr);
                    var str = MemoryManager.ReadString(ptr);
                    list[i] = str;
                }
                else if (types[i] == "f")
                {
                    var val = MemoryManager.ReadFloat((nint)tmpPtr);
                    list[i] = val;
                }
                else if (types[i] == "u")
                {
                    var val = MemoryManager.ReadUint((nint)tmpPtr);
                    list[i] = val;
                }
                else if (types[i] == "d")
                {
                    var val = MemoryManager.ReadInt((nint)tmpPtr);
                    list[i] = val;
                }
                else if (types[i] == "b")
                {
                    var val = MemoryManager.ReadInt((nint)tmpPtr);
                    list[i] = Convert.ToBoolean(val);
                }
            }

            OnNewEventSignalEvent(eventName, list);
        }

        static internal void OnNewEventSignalEvent(string parEvent, params object[] parList) =>
            OnNewSignalEvent?.Invoke(parEvent, parList);

        internal delegate void SignalEventEventHandler(string parEvent, params object[] parArgs);

        internal static event SignalEventEventHandler OnNewSignalEvent;
        #endregion

        #region InitializeSignalEventHookNoArgs
        private static SignalEventNoArgsDelegate signalEventNoArgsDelegate;

        private static void InitializeSignalEventHookNoArgs()
        {
            signalEventNoArgsDelegate = new SignalEventNoArgsDelegate(SignalEventNoArgsHook);
            var addrToDetour = Marshal.GetFunctionPointerForDelegate(signalEventNoArgsDelegate);

            var instructions = new[]
            {
                "push esi",
                "call 0x007040D0",
                "pushfd",
                "pushad",
                "mov edi, [edi]",
                "push edi",
                $"call 0x{(uint) addrToDetour:X}",
                "popad",
                "popfd",
                $"jmp 0x{(uint) MemoryAddresses.SignalEventNoParamsFunPtr + 6:X}"
            };
            var signalEventNoArgsDetour = MemoryManager.InjectAssembly("SignalEventNoArgsDetour", instructions);
            MemoryManager.InjectAssembly("SignalEventNoArgsHook", (uint)MemoryAddresses.SignalEventNoParamsFunPtr, "jmp " + signalEventNoArgsDetour);

            // Verify hook was written
            var hookBytes = MemoryManager.ReadBytes((nint)MemoryAddresses.SignalEventNoParamsFunPtr, 5);
            if (hookBytes != null && hookBytes[0] == 0xE9)
                DiagLog($"SignalEventNoArgs hook INSTALLED at 0x{MemoryAddresses.SignalEventNoParamsFunPtr:X8} → detour 0x{(uint)signalEventNoArgsDetour:X8}");
            else
                DiagLog($"SignalEventNoArgs hook FAILED at 0x{MemoryAddresses.SignalEventNoParamsFunPtr:X8}: first byte=0x{(hookBytes?[0] ?? 0):X2}");
        }

        private static void SignalEventNoArgsHook(string eventName)
        {
            _eventCount++;
            if (_eventCount <= 20)
                DiagLog($"EVENT_NOARGS[{_eventCount}]: {eventName}");

            OnNewSignalEventNoArgs?.Invoke(eventName);
        }

        internal delegate void SignalEventNoArgsEventHandler(string parEvent, params object[] parArgs);

        internal static event SignalEventNoArgsEventHandler OnNewSignalEventNoArgs;
        #endregion
    }
}
