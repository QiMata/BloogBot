using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class WoWUIAutomation
{
    // Windows API imports for UI interaction
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern IntPtr GetMessageExtraInfo();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    // Constants for Windows Messages
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_MOUSEMOVE = 0x0200;

    // Input types
    public const int INPUT_MOUSE = 0;
    public const int INPUT_KEYBOARD = 1;

    // Virtual key codes
    public const ushort VK_W = 0x57;
    public const ushort VK_A = 0x41;
    public const ushort VK_S = 0x53;
    public const ushort VK_D = 0x44;
    public const ushort VK_SPACE = 0x20;
    public const ushort VK_ENTER = 0x0D;
    public const ushort VK_ESCAPE = 0x1B;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // UI Automation helper methods
    public static IntPtr FindWoWWindow()
    {
        // Try different window class names for WoW
        var wowWindow = FindWindow("GxWindowClass", null); // Modern WoW
        if (wowWindow == IntPtr.Zero)
        {
            wowWindow = FindWindow("Warcraft III", null); // Classic WoW
        }
        if (wowWindow == IntPtr.Zero)
        {
            wowWindow = FindWindow("WorldWarcraft", null); // Alternative
        }
        return wowWindow;
    }

    public static bool FocusWoWWindow()
    {
        var wowWindow = FindWoWWindow();
        if (wowWindow != IntPtr.Zero)
        {
            SetForegroundWindow(wowWindow);
            return true;
        }
        return false;
    }

    public static void SendKeyPress(ushort virtualKey, bool hold = false)
    {
        const uint KEYEVENTF_KEYUP = 0x0002;
        
        var inputs = new INPUT[hold ? 1 : 2];
        
        // Key down
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = GetMessageExtraInfo()
                }
            }
        };

        if (!hold)
        {
            // Key up
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = virtualKey,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void SendMouseClick(int x, int y, bool rightClick = false)
    {
        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        var inputs = new INPUT[2];
        
        uint downFlag = rightClick ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
        uint upFlag = rightClick ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;

        // Convert screen coordinates to normalized coordinates (0-65535)
        int normalizedX = (x * 65536) / GetSystemMetrics(0); // SM_CXSCREEN
        int normalizedY = (y * 65536) / GetSystemMetrics(1); // SM_CYSCREEN

        inputs[0] = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = normalizedX,
                    dy = normalizedY,
                    mouseData = 0,
                    dwFlags = downFlag | MOUSEEVENTF_ABSOLUTE,
                    time = 0,
                    dwExtraInfo = GetMessageExtraInfo()
                }
            }
        };

        inputs[1] = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = normalizedX,
                    dy = normalizedY,
                    mouseData = 0,
                    dwFlags = upFlag | MOUSEEVENTF_ABSOLUTE,
                    time = 0,
                    dwExtraInfo = GetMessageExtraInfo()
                }
            }
        };

        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    // High-level bot interaction methods
    public static class BotUIActions
    {
        public static bool ClickCharacterCreation()
        {
            if (!FocusWoWWindow()) return false;
            
            // Typical coordinates for character creation button (adjust as needed)
            SendMouseClick(400, 500);
            Thread.Sleep(100);
            return true;
        }

        public static bool CreateRandomCharacter()
        {
            if (!FocusWoWWindow()) return false;
            
            // Random character creation sequence
            SendMouseClick(300, 200); // Race selection
            Thread.Sleep(500);
            SendMouseClick(400, 200); // Class selection  
            Thread.Sleep(500);
            SendMouseClick(500, 600); // Accept/Create button
            Thread.Sleep(1000);
            return true;
        }

        public static bool EnterWorld()
        {
            if (!FocusWoWWindow()) return false;
            
            // Click Enter World button
            SendMouseClick(640, 480); // Typical center-bottom location
            Thread.Sleep(2000);
            return true;
        }

        public static void StartMovementForward()
        {
            if (!FocusWoWWindow()) return;
            SendKeyPress(VK_W, hold: true);
        }

        public static void StopMovementForward()
        {
            if (!FocusWoWWindow()) return;
            SendKeyPress(VK_W, hold: false);
        }

        public static void MoveTowardsDirection(float seconds, bool forward = true)
        {
            if (!FocusWoWWindow()) return;
            
            ushort key = forward ? VK_W : VK_S;
            SendKeyPress(key, hold: true);
            Thread.Sleep((int)(seconds * 1000));
            SendKeyPress(key, hold: false);
        }
    }
}