using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Serilog;

namespace ForegroundBotRunner.Mem
{
    static public class ThreadSynchronizer
    {
        [DllImport("user32.dll")]
        private static extern nint SetWindowLong(nint hWnd, int nIndex, nint dwNewLong);

        [DllImport("user32.dll")]
        private static extern int CallWindowProc(nint lpPrevWndFunc, nint hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(nint handle, out int processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(nint hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(nint hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

        [DllImport("user32.dll")]
        private static extern int SendMessage(
            int hWnd,
            uint Msg,
            int wParam,
            int lParam
        );

        [DllImport("user32.dll")]
        private static extern bool PostMessage(
            int hWnd,
            uint Msg,
            int wParam,
            int lParam
        );

        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const int VK_SPACE = 0x20;

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        private delegate int WindowProc(nint hWnd, int Msg, int wParam, int lParam);

        private static readonly Queue<Action> actionQueue = new();
        private static readonly Queue<(Delegate function, ManualResetEventSlim signal, object[] resultHolder)> delegateQueue = new();
        private const int GWL_WNDPROC = -4;
        private const int WM_USER = 0x0400;
        private static readonly nint oldCallback;
        private static readonly WindowProc newCallback;
        private static int windowHandle;
        private static readonly object _queueLock = new();

        // Set to true to disable the window hook (for Warden testing)
        // When disabled, RunOnMainThread will execute directly (WARNING: may cause threading issues)
        private static readonly bool DISABLE_WINDOW_HOOK = false; // Re-enabled: thread safety required for Lua calls

        private static volatile bool _hookInstalled = false;

        /// <summary>
        /// When true, WndProc will NOT execute queued actions/delegates. Callers of
        /// RunOnMainThread will timeout. Set this during map/instance transitions to
        /// prevent Lua calls from crashing WoW when its internal state is unstable.
        /// </summary>
        public static volatile bool Paused = false;

        /// <summary>True once the object manager has been valid at least once (i.e., we entered world).</summary>
        private static volatile bool _objMgrWasValid = false;

        static ThreadSynchronizer()
        {
            if (DISABLE_WINDOW_HOOK)
            {
                DiagLogStatic("ThreadSynchronizer INIT: WINDOW HOOK DISABLED (Warden test mode)");
                DiagLogStatic("WARNING: RunOnMainThread will execute directly - threading may be unsafe!");
                return;
            }

            EnumWindows(FindWindowProc, nint.Zero);
            newCallback = WndProc;
            oldCallback = SetWindowLong(windowHandle, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(newCallback));
            _hookInstalled = true;

            // Log initialization result
            DiagLogStatic($"ThreadSynchronizer INIT: windowHandle=0x{windowHandle:X}, oldCallback=0x{oldCallback:X}");
            if (windowHandle == 0)
            {
                DiagLogStatic("WARNING: Window handle not found! WM_USER messages will not be delivered.");
            }
        }

        static public void RunOnMainThread(Action action)
        {
            // If hook is disabled, just run directly (WARNING: threading unsafe!)
            if (DISABLE_WINDOW_HOOK || !_hookInstalled)
            {
                action();
                return;
            }

            if (GetCurrentThreadId() == Process.GetCurrentProcess().Threads[0].Id)
            {
                action();
                return;
            }
            // Wrap the Action as a Func<int> so it goes through the delegate queue
            // with signal-based wait. This ensures the action completes before we return,
            // matching the synchronous contract even with PostMessage delivery.
            RunOnMainThread<int>(() => { action(); return 0; });
        }

        // Diagnostic logging counter
        private static int _timeoutCount = 0;
        private static int _successCount = 0;

        static public T RunOnMainThread<T>(Func<T> function)
        {
            // If hook is disabled, just run directly (WARNING: threading unsafe!)
            if (DISABLE_WINDOW_HOOK || !_hookInstalled)
            {
                return function();
            }

            if (GetCurrentThreadId() == Process.GetCurrentProcess().Threads[0].Id)
                return function();

            // Use a signal to wait for the result to be ready
            var signal = new ManualResetEventSlim(false);
            var resultHolder = new object[1]; // Array to hold the result (allows passing by reference)

            lock (_queueLock)
            {
                delegateQueue.Enqueue((function, signal, resultHolder));
            }
            SendUserMessage();

            // Wait for the main thread to process our request (with timeout to prevent hangs)
            if (!signal.Wait(TimeSpan.FromSeconds(5)))
            {
                _timeoutCount++;
                Log.Error($"[THREAD] Timeout waiting for main thread (timeout #{_timeoutCount}, success #{_successCount})");
                DiagLogStatic($"TIMEOUT #{_timeoutCount} waiting for main thread (windowHandle=0x{windowHandle:X})");
                return default!;
            }

            _successCount++;
            signal.Dispose();

            // If the delegate threw on the main thread, resultHolder[0] is null.
            // Unboxing null to a value type (e.g. int) would throw NullReferenceException.
            if (resultHolder[0] is T typed)
                return typed;
            return default!;
        }

        // Simple diagnostic logging to file
        private static void DiagLogStatic(string message)
        {
            try
            {
                string wowDir;
                try { wowDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? AppContext.BaseDirectory; }
                catch { wowDir = AppContext.BaseDirectory; }
                var logsDir = Path.Combine(wowDir, "WWoWLogs");
                try { Directory.CreateDirectory(logsDir); } catch { }
                var logPath = Path.Combine(logsDir, "thread_synchronizer_debug.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
            catch { }
        }

        /// <summary>
        /// Crash-safe log: writes to D:/World of Warcraft/WWoWLogs/crash_trace.log with immediate flush.
        /// Use this to trace what happens right before a native ACCESS_VIOLATION kills the process.
        /// </summary>
        private static void CrashTrace(string message)
        {
            try
            {
                var logPath = Path.Combine("D:\\World of Warcraft\\WWoWLogs", "crash_trace.log");
                try { Directory.CreateDirectory(Path.GetDirectoryName(logPath)!); } catch { }
                using var sw = new StreamWriter(logPath, true);
                sw.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                sw.Flush();
            }
            catch { }
        }

        [HandleProcessCorruptedStateExceptions]
        private static int WndProc(nint hWnd, int msg, int wParam, int lParam)
        {
            try
            {
                if (msg != WM_USER) return CallWindowProc(oldCallback, hWnd, msg, wParam, lParam);

                // Real-time ManagerBase + ContinentId check — catches transitions between ObjectManager polls.
                // During login, _objMgrWasValid is false so Lua calls proceed (Lua works at charselect).
                // Once we've been in-world, a null ManagerBase or transitional ContinentId means unsafe state.
                bool managerBaseValid = MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase) != nint.Zero;
                if (managerBaseValid)
                    _objMgrWasValid = true;

                uint continentId = MemoryManager.ReadUint(Offsets.Map.ContinentId);
                bool inTransition = _objMgrWasValid && (continentId == 0xFFFFFFFF || continentId == 0xFF);

                bool shouldBlock = Paused || (_objMgrWasValid && !managerBaseValid) || inTransition;
                if (shouldBlock)
                {
                    DrainQueues($"paused={Paused} objMgrNull={!managerBaseValid} contId=0x{continentId:X}");
                    return 0;
                }

                while (actionQueue.Count > 0)
                {
                    try
                    {
                        actionQueue.Dequeue()?.Invoke();
                    }
                    catch (AccessViolationException)
                    {
                        CrashTrace("WndProc: ACCESS_VIOLATION in action — auto-pausing");
                        Paused = true;
                        DrainQueues("ACCESS_VIOLATION in action");
                        return 0;
                    }
                }

                // Process delegate queue with proper signaling
                while (true)
                {
                    (Delegate function, ManualResetEventSlim signal, object[] resultHolder) item;
                    lock (_queueLock)
                    {
                        if (delegateQueue.Count == 0)
                            break;
                        item = delegateQueue.Dequeue();
                    }

                    try
                    {
                        item.resultHolder[0] = item.function?.DynamicInvoke()!;
                    }
                    catch (AccessViolationException)
                    {
                        CrashTrace("WndProc: ACCESS_VIOLATION in delegate — auto-pausing");
                        Paused = true;
                        item.resultHolder[0] = null!;
                        item.signal.Set();
                        DrainQueues("ACCESS_VIOLATION in delegate");
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        // DynamicInvoke wraps exceptions in TargetInvocationException
                        if (ex.InnerException is AccessViolationException)
                        {
                            CrashTrace("WndProc: ACCESS_VIOLATION (wrapped) in delegate — auto-pausing");
                            Paused = true;
                            item.resultHolder[0] = null!;
                            item.signal.Set();
                            DrainQueues("ACCESS_VIOLATION wrapped in delegate");
                            return 0;
                        }
                        Log.Error($"[THREAD] Error invoking delegate: {ex.Message}");
                        item.resultHolder[0] = null!;
                    }
                    finally
                    {
                        item.signal.Set(); // Signal that result is ready
                    }
                }
                return 0;
            }
            catch (AccessViolationException)
            {
                CrashTrace("WndProc: ACCESS_VIOLATION (outer) — auto-pausing");
                Paused = true;
                DrainQueues("ACCESS_VIOLATION outer");
                return 0;
            }
            catch (Exception e)
            {
                Log.Error($"[THREAD]{e.Message} {e.StackTrace}");
            }

            return CallWindowProc(oldCallback, hWnd, msg, wParam, lParam);
        }

        /// <summary>Drains all queued work, signaling delegates with null results.</summary>
        private static void DrainQueues(string reason)
        {
            CrashTrace($"WndProc: BLOCKED ({reason}) — dropping {actionQueue.Count}+{delegateQueue.Count}");
            while (actionQueue.Count > 0)
                actionQueue.Dequeue();
            lock (_queueLock)
            {
                while (delegateQueue.Count > 0)
                {
                    var item = delegateQueue.Dequeue();
                    item.resultHolder[0] = null!;
                    item.signal.Set();
                }
            }
        }

        private static bool FindWindowProc(nint hWnd, nint lParam)
        {
            GetWindowThreadProcessId(hWnd, out int procId);
            if (procId != Environment.ProcessId) return true;
            if (!IsWindowVisible(hWnd)) return true;
            var l = GetWindowTextLength(hWnd);
            if (l == 0) return true;
            var builder = new StringBuilder(l + 1);
            GetWindowText(hWnd, builder, builder.Capacity);
            if (builder.ToString() == "World of Warcraft")
                windowHandle = (int)hWnd;
            return true;
        }

        // PostMessage instead of SendMessage: PostMessage puts WM_USER in the regular message queue,
        // processed only during GetMessage/PeekMessage in WoW's main game loop — between frames when
        // game state is stable. SendMessage dispatches via the "sent message" queue which can fire
        // during nested SendMessage/PeekMessage calls inside packet processing, potentially executing
        // our Lua calls while WoW is mid-transfer-teardown (before ContinentId even changes).
        private static void SendUserMessage() => PostMessage(windowHandle, WM_USER, 0, 0);

        /// <summary>
        /// Simulates a spacebar press+release to trigger a jump.
        /// Uses PostMessage to send WM_KEYDOWN/WM_KEYUP to WoW's window.
        /// </summary>
        public static void SimulateSpacebarPress()
        {
            if (windowHandle == 0) return;
            PostMessage(windowHandle, WM_KEYDOWN, VK_SPACE, 0x00390001); // scancode 0x39 for space
            PostMessage(windowHandle, WM_KEYUP, VK_SPACE, unchecked((int)0xC0390001)); // release with bit 31+30 set
        }
    }
}
