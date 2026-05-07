using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable CA1416 // PrintWindow / GDI+ are Windows-only; tests run on Windows.

namespace Tests.Infrastructure;

/// <summary>
/// Captures a screenshot of any visible top-level window via PrintWindow with
/// PW_RENDERFULLCONTENT. Mirrors the FFXI focus-safe helper at
/// Final Fantasy XI/src/ClientInterop/Memory/WindowCapture.cs so that periodic
/// timeline capture during a live multi-minute test does not steal focus or
/// disturb the bot's input. See e:/repos/.claude/skills/mmo-movement-diagnostics/SKILL.md.
/// </summary>
public static class WindowCapture
{
    private const uint PW_RENDERFULLCONTENT = 0x00000002;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>
    /// Captures the full window and saves it as a PNG file. Falls back to a
    /// desktop region capture if PrintWindow does not yield a usable bitmap.
    /// Returns true if a PNG was written.
    /// </summary>
    public static bool CaptureWindow(nint windowHandle, string outputPath)
    {
        PrepareOutputPath(outputPath);

        if (windowHandle != nint.Zero && TryCaptureWindow(windowHandle, outputPath))
            return true;

        return TryCaptureDesktop(outputPath);
    }

    private static bool TryCaptureWindow(nint windowHandle, string outputPath)
    {
        if (windowHandle == nint.Zero)
            return false;

        if (!NativeMethods.GetWindowRect(windowHandle, out var rect))
            return false;

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
            return false;

        var windowDC = NativeMethods.GetWindowDC(windowHandle);
        if (windowDC == nint.Zero)
            return TryCaptureScreenRegion(rect.Left, rect.Top, width, height, outputPath);

        var memDC = nint.Zero;
        var bitmap = nint.Zero;
        var oldBitmap = nint.Zero;
        try
        {
            memDC = NativeMethods.CreateCompatibleDC(windowDC);
            bitmap = NativeMethods.CreateCompatibleBitmap(windowDC, width, height);
            if (memDC == nint.Zero || bitmap == nint.Zero)
                return TryCaptureScreenRegion(rect.Left, rect.Top, width, height, outputPath);

            oldBitmap = NativeMethods.SelectObject(memDC, bitmap);
            if (!NativeMethods.PrintWindow(windowHandle, memDC, PW_RENDERFULLCONTENT))
                return TryCaptureScreenRegion(rect.Left, rect.Top, width, height, outputPath);

            using var bmp = Image.FromHbitmap(bitmap);
            bmp.Save(outputPath, ImageFormat.Png);
            return true;
        }
        catch
        {
            return TryCaptureScreenRegion(rect.Left, rect.Top, width, height, outputPath);
        }
        finally
        {
            if (oldBitmap != nint.Zero && memDC != nint.Zero)
                NativeMethods.SelectObject(memDC, oldBitmap);
            if (bitmap != nint.Zero)
                NativeMethods.DeleteObject(bitmap);
            if (memDC != nint.Zero)
                NativeMethods.DeleteDC(memDC);
            NativeMethods.ReleaseDC(windowHandle, windowDC);
        }
    }

    private static bool TryCaptureDesktop(string outputPath)
    {
        int left = NativeMethods.GetSystemMetrics(SM_XVIRTUALSCREEN);
        int top = NativeMethods.GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = NativeMethods.GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = NativeMethods.GetSystemMetrics(SM_CYVIRTUALSCREEN);

        if (width <= 0 || height <= 0)
        {
            left = 0;
            top = 0;
            width = NativeMethods.GetSystemMetrics(SM_CXSCREEN);
            height = NativeMethods.GetSystemMetrics(SM_CYSCREEN);
        }

        return TryCaptureScreenRegion(left, top, width, height, outputPath);
    }

    private static bool TryCaptureScreenRegion(int left, int top, int width, int height, string outputPath)
    {
        if (width <= 0 || height <= 0)
            return false;

        try
        {
            using var bmp = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bmp);
            graphics.CopyFromScreen(left, top, 0, 0, new Size(width, height));
            bmp.Save(outputPath, ImageFormat.Png);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void PrepareOutputPath(string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(outputPath))
            File.Delete(outputPath);
    }

    /// <summary>
    /// Enumerates all visible top-level windows belonging to the given process
    /// id. Used by callers that need to filter by class name / title (e.g.
    /// WoW's GxWindowClassD3d) before invoking CaptureWindow.
    /// </summary>
    public static IReadOnlyList<WindowInfo> GetTopLevelWindowsForProcess(int processId)
    {
        var results = new List<WindowInfo>();
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out var windowPid);
            if (windowPid != (uint)processId || !NativeMethods.IsWindowVisible(hWnd))
                return true;
            if (!NativeMethods.GetWindowRect(hWnd, out var rect))
                return true;
            if (rect.Right <= rect.Left || rect.Bottom <= rect.Top)
                return true;

            var titleBuf = new StringBuilder(256);
            var classBuf = new StringBuilder(128);
            NativeMethods.GetWindowText(hWnd, titleBuf, titleBuf.Capacity);
            NativeMethods.GetClassName(hWnd, classBuf, classBuf.Capacity);

            results.Add(new WindowInfo(
                Handle: hWnd,
                Title: titleBuf.ToString(),
                ClassName: classBuf.ToString(),
                Left: rect.Left,
                Top: rect.Top,
                Width: rect.Right - rect.Left,
                Height: rect.Bottom - rect.Top));
            return true;
        }, nint.Zero);
        return results;
    }

    /// <summary>
    /// Picks the WoW client window for the given pid. Prefers
    /// GxWindowClassD3d, then any window titled "World of Warcraft", then any
    /// visible top-level window. Returns nint.Zero if nothing matches.
    /// </summary>
    public static nint FindWoWClientWindow(int processId)
    {
        var windows = GetTopLevelWindowsForProcess(processId);
        var picked = windows
            .Where(w => string.Equals(w.ClassName, "GxWindowClassD3d", StringComparison.Ordinal))
            .Concat(windows.Where(w => string.Equals(w.Title, "World of Warcraft", StringComparison.Ordinal)))
            .Concat(windows)
            .FirstOrDefault();
        return picked.Handle;
    }

    public readonly record struct WindowInfo(
        nint Handle,
        string Title,
        string ClassName,
        int Left,
        int Top,
        int Width,
        int Height);

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PrintWindow(nint hWnd, nint hDC, uint nFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint GetWindowDC(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int ReleaseDC(nint hWnd, nint hDC);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(nint hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(nint hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(nint hWnd, StringBuilder text, int count);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern nint CreateCompatibleDC(nint hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern nint CreateCompatibleBitmap(nint hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern nint SelectObject(nint hdc, nint hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteDC(nint hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(nint hObject);

        public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}

#pragma warning restore CA1416
