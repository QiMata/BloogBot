using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ForegroundBotRunner.Mem.Hooks
{
    public class SignalEventManager
    {
        // CRITICAL: Delegates use nint (raw pointers) instead of string to prevent
        // CLR marshaling AVs. String marshaling happens BEFORE the method body,
        // so try/catch can't protect it. We marshal manually inside the try block.
        private delegate void SignalEventDelegate(nint eventNamePtr, nint formatPtr, uint firstArgPtr);

        private delegate void SignalEventNoArgsDelegate(nint eventNamePtr);

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

            // Get SafeCallback3 from FastCall.dll for SEH protection.
            // .NET 8 can't catch AccessViolationException — only C++ __try/__except can.
            // SafeCallback3 wraps the managed delegate call with SEH so AVs from stale
            // WoW memory pointers are caught instead of crashing the process.
            //
            // Use NativeLibrary (not GetModuleHandle/GetProcAddress) — the .NET 8
            // hosting layer may load FastCall.dll from a different path than the OS
            // module table reports, causing GetProcAddress to fail on the wrong handle.
            var safeCallback3Addr = NativeLibraryHelper.GetFastCallExport("_SafeCallback3@16", "SafeCallback3");
            DiagLog($"NativeLibrary SafeCallback3 = 0x{(uint)safeCallback3Addr:X8}");

            string[] instructions;
            if (safeCallback3Addr != nint.Zero)
            {
                DiagLog($"SafeCallback3 found at 0x{(uint)safeCallback3Addr:X8} — using SEH-protected detour");
                instructions =
                [
                    "push ebx",
                    "push esi",
                    "call 0x007040D0",
                    "pushfd",
                    "pushad",
                    "mov eax, ebp",
                    "add eax, 0x10",
                    "push eax",                                    // arg3: firstArgPtr
                    "mov eax, [ebp + 0xC]",
                    "push eax",                                    // arg2: format
                    "mov edi, [edi]",
                    "push edi",                                    // arg1: eventName
                    $"push 0x{(uint)addrToDetour:X}",              // parCallbackPtr (managed delegate)
                    $"call 0x{(uint)safeCallback3Addr:X}",         // SafeCallback3 — SEH-protected
                    "popad",
                    "popfd",
                    $"jmp 0x{(uint)(MemoryAddresses.SignalEventFunPtr + 7):X}"
                ];
            }
            else
            {
                DiagLog("WARNING: SafeCallback3 NOT found — falling back to unprotected detour");
                instructions =
                [
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
                    $"call 0x{(uint)addrToDetour:X}",
                    "popad",
                    "popfd",
                    $"jmp 0x{(uint)(MemoryAddresses.SignalEventFunPtr + 7):X}"
                ];
            }

            var signalEventDetour = MemoryManager.InjectAssembly("SignalEventDetour", instructions);
            MemoryManager.InjectAssembly("SignalEventHook", (uint)MemoryAddresses.SignalEventFunPtr, "jmp " + signalEventDetour);

            // Verify hook was written
            var hookBytes = MemoryManager.ReadBytes((nint)MemoryAddresses.SignalEventFunPtr, 5);
            if (hookBytes != null && hookBytes[0] == 0xE9)
                DiagLog($"SignalEvent hook INSTALLED at 0x{MemoryAddresses.SignalEventFunPtr:X8} → detour 0x{(uint)signalEventDetour:X8}");
            else
                DiagLog($"SignalEvent hook FAILED at 0x{MemoryAddresses.SignalEventFunPtr:X8}: first byte=0x{(hookBytes?[0] ?? 0):X2}");
        }

        private static void SignalEventHook(nint eventNamePtr, nint formatPtr, uint firstArgPtr)
        {
            // CRITICAL: This runs on WoW's main thread via native assembly detour.
            // ANY unhandled exception propagates into WoW's native stack → ERROR #132 crash.
            // Delegates use nint params to prevent CLR marshaling AVs (which happen BEFORE
            // the method body and can't be caught by try/catch). We marshal manually here.
            try
            {
                _eventCount++;

                // Manual string marshaling — safe inside try/catch
                string? eventName = eventNamePtr != nint.Zero
                    ? Marshal.PtrToStringAnsi(eventNamePtr)
                    : null;
                string? typesArg = formatPtr != nint.Zero
                    ? Marshal.PtrToStringAnsi(formatPtr)
                    : null;

                if (_eventCount <= 20
                    || (eventName != null && (eventName.StartsWith("UI_ERROR")
                        || eventName.StartsWith("UI_INFO")
                        || eventName.StartsWith("CHAT_MSG_SKILL")
                        || eventName == "CHAT_MSG_SYSTEM"
                        || eventName == "LEARNED_SPELL"
                        || eventName == "UNLEARNED_SPELL")))
                    DiagLog($"EVENT[{_eventCount}]: {eventName} format={typesArg}");

                if (string.IsNullOrEmpty(typesArg) || string.IsNullOrEmpty(eventName))
                {
                    if (!string.IsNullOrEmpty(eventName))
                        OnNewEventSignalEvent(eventName, Array.Empty<object>());
                    return;
                }

                if (firstArgPtr == 0)
                {
                    OnNewEventSignalEvent(eventName, Array.Empty<object>());
                    return;
                }

                var types = typesArg.TrimStart('%').Split('%');
                var list = new object[types.Length];
                for (var i = 0; i < types.Length; i++)
                {
                    var tmpPtr = firstArgPtr + (uint)i * 4;
                    if (types[i] == "s")
                    {
                        var ptr = MemoryManager.ReadInt((nint)tmpPtr);
                        list[i] = (ptr != 0 ? MemoryManager.ReadString(ptr) : null) ?? "";
                    }
                    else if (types[i] == "f")
                    {
                        list[i] = MemoryManager.ReadFloat((nint)tmpPtr);
                    }
                    else if (types[i] == "u")
                    {
                        list[i] = MemoryManager.ReadUint((nint)tmpPtr);
                    }
                    else if (types[i] == "d")
                    {
                        list[i] = MemoryManager.ReadInt((nint)tmpPtr);
                    }
                    else if (types[i] == "b")
                    {
                        list[i] = MemoryManager.ReadInt((nint)tmpPtr) != 0;
                    }
                    else
                    {
                        list[i] = 0;
                    }
                }

                OnNewEventSignalEvent(eventName, list);
            }
            catch (Exception ex)
            {
                try { DiagLog($"SignalEventHook EXCEPTION (swallowed): {ex.GetType().Name}: {ex.Message}"); }
                catch { }
            }
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

            // Get SafeCallback1 from FastCall.dll for SEH protection
            var safeCallback1Addr = NativeLibraryHelper.GetFastCallExport("_SafeCallback1@8", "SafeCallback1");

            string[] instructions;
            if (safeCallback1Addr != nint.Zero)
            {
                DiagLog($"SafeCallback1 found at 0x{(uint)safeCallback1Addr:X8} — using SEH-protected detour");
                instructions =
                [
                    "push esi",
                    "call 0x007040D0",
                    "pushfd",
                    "pushad",
                    "mov edi, [edi]",
                    "push edi",                                    // arg1: eventName
                    $"push 0x{(uint)addrToDetour:X}",              // parCallbackPtr (managed delegate)
                    $"call 0x{(uint)safeCallback1Addr:X}",         // SafeCallback1 — SEH-protected
                    "popad",
                    "popfd",
                    $"jmp 0x{(uint)MemoryAddresses.SignalEventNoParamsFunPtr + 6:X}"
                ];
            }
            else
            {
                DiagLog("WARNING: SafeCallback1 NOT found — falling back to unprotected detour");
                instructions =
                [
                    "push esi",
                    "call 0x007040D0",
                    "pushfd",
                    "pushad",
                    "mov edi, [edi]",
                    "push edi",
                    $"call 0x{(uint)addrToDetour:X}",
                    "popad",
                    "popfd",
                    $"jmp 0x{(uint)MemoryAddresses.SignalEventNoParamsFunPtr + 6:X}"
                ];
            }

            var signalEventNoArgsDetour = MemoryManager.InjectAssembly("SignalEventNoArgsDetour", instructions);
            MemoryManager.InjectAssembly("SignalEventNoArgsHook", (uint)MemoryAddresses.SignalEventNoParamsFunPtr, "jmp " + signalEventNoArgsDetour);

            // Verify hook was written
            var hookBytes = MemoryManager.ReadBytes((nint)MemoryAddresses.SignalEventNoParamsFunPtr, 5);
            if (hookBytes != null && hookBytes[0] == 0xE9)
                DiagLog($"SignalEventNoArgs hook INSTALLED at 0x{MemoryAddresses.SignalEventNoParamsFunPtr:X8} → detour 0x{(uint)signalEventNoArgsDetour:X8}");
            else
                DiagLog($"SignalEventNoArgs hook FAILED at 0x{MemoryAddresses.SignalEventNoParamsFunPtr:X8}: first byte=0x{(hookBytes?[0] ?? 0):X2}");
        }

        private static void SignalEventNoArgsHook(nint eventNamePtr)
        {
            try
            {
                _eventCount++;

                // Manual string marshaling — safe inside try/catch
                string? eventName = eventNamePtr != nint.Zero
                    ? Marshal.PtrToStringAnsi(eventNamePtr)
                    : null;

                if (_eventCount <= 20
                    || eventName == "LEARNED_SPELL"
                    || eventName == "UNLEARNED_SPELL")
                    DiagLog($"EVENT_NOARGS[{_eventCount}]: {eventName}");

                if (!string.IsNullOrEmpty(eventName))
                    OnNewSignalEventNoArgs?.Invoke(eventName);
            }
            catch (Exception ex)
            {
                try { DiagLog($"SignalEventNoArgsHook EXCEPTION (swallowed): {ex.GetType().Name}: {ex.Message}"); }
                catch { }
            }
        }

        internal delegate void SignalEventNoArgsEventHandler(string parEvent, params object[] parArgs);

        internal static event SignalEventNoArgsEventHandler OnNewSignalEventNoArgs;
        #endregion
    }
}
