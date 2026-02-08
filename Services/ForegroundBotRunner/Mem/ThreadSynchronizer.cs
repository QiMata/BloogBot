using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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

        private static bool _hookInstalled = false;

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
            actionQueue.Enqueue(action);
            SendUserMessage();
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
            return (T)resultHolder[0]!;
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

        private static int WndProc(nint hWnd, int msg, int wParam, int lParam)
        {
            try
            {
                if (msg != WM_USER) return CallWindowProc(oldCallback, hWnd, msg, wParam, lParam);

                while (actionQueue.Count > 0)
                    actionQueue.Dequeue()?.Invoke();

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
                    catch (Exception ex)
                    {
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
            catch (Exception e)
            {
                Log.Error($"[THREAD]{e.Message} {e.StackTrace}");
            }

            return CallWindowProc(oldCallback, hWnd, msg, wParam, lParam);
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

        private static void SendUserMessage() => SendMessage(windowHandle, WM_USER, 0, 0);

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
