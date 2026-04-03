using System;
using System.IO;
using System.Runtime.InteropServices;
using ForegroundBotRunner.Diagnostics;

namespace ForegroundBotRunner.Mem.Hooks
{
    /// <summary>
    /// Resolves native exports from FastCall.dll for use in assembly code caves.
    /// Uses multiple strategies to find the correct function pointers.
    /// </summary>
    internal static class NativeLibraryHelper
    {
        private static nint _fastCallHandle;
        private static readonly object _lock = new();

        private static readonly string DiagnosticLogPath;
        private static readonly object DiagLogLock = new();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern nint GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern nint GetProcAddress(nint hModule, string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetModuleFileNameW(nint hModule, char[] lpFilename, int nSize);

        static NativeLibraryHelper()
        {
            DiagnosticLogPath = RecordingFileArtifactGate.ResolveWoWLogsPath("native_library_helper.log");
            if (!string.IsNullOrWhiteSpace(DiagnosticLogPath))
            {
                try
                {
                    File.WriteAllText(DiagnosticLogPath,
                        $"=== NativeLibraryHelper Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                }
                catch { }
            }
        }

        /// <summary>
        /// Get a function pointer for an export from FastCall.dll.
        /// Tries multiple resolution strategies: NativeLibrary, kernel32 GetProcAddress,
        /// PE export table parsing.
        /// </summary>
        internal static nint GetFastCallExport(string decoratedName, string undecoratedName)
        {
            try
            {
                var handle = GetFastCallHandle();
                if (handle == nint.Zero)
                    return nint.Zero;

                // Strategy 1: NativeLibrary.TryGetExport (decorated)
                if (NativeLibrary.TryGetExport(handle, decoratedName, out var addr) && addr != nint.Zero)
                {
                    DiagLog($"[NativeLib] '{decoratedName}' = 0x{(uint)addr:X8} ✓");
                    return addr;
                }

                // Strategy 2: NativeLibrary.TryGetExport (undecorated)
                if (NativeLibrary.TryGetExport(handle, undecoratedName, out addr) && addr != nint.Zero)
                {
                    DiagLog($"[NativeLib] '{undecoratedName}' = 0x{(uint)addr:X8} ✓");
                    return addr;
                }

                // Strategy 3: kernel32 GetProcAddress (decorated) — uses OS module table
                var k32Handle = GetModuleHandle("FastCall.dll");
                if (k32Handle != nint.Zero)
                {
                    addr = GetProcAddress(k32Handle, decoratedName);
                    if (addr != nint.Zero)
                    {
                        DiagLog($"[kernel32] '{decoratedName}' = 0x{(uint)addr:X8} ✓ (handle=0x{(uint)k32Handle:X8})");
                        return addr;
                    }
                    addr = GetProcAddress(k32Handle, undecoratedName);
                    if (addr != nint.Zero)
                    {
                        DiagLog($"[kernel32] '{undecoratedName}' = 0x{(uint)addr:X8} ✓ (handle=0x{(uint)k32Handle:X8})");
                        return addr;
                    }
                    DiagLog($"[kernel32] GetProcAddress failed for both names on handle 0x{(uint)k32Handle:X8}");
                }

                // Strategy 4: Parse PE export table directly from the loaded module
                addr = FindExportInPE(handle, decoratedName, undecoratedName);
                if (addr != nint.Zero)
                {
                    DiagLog($"[PE-parse] Found = 0x{(uint)addr:X8} ✓");
                    return addr;
                }

                // Log all available exports for debugging
                DiagLog($"ALL STRATEGIES FAILED for '{decoratedName}'/'{undecoratedName}'");
                LogAllExports(handle);
                if (k32Handle != nint.Zero && k32Handle != handle)
                    LogAllExports(k32Handle);

                return nint.Zero;
            }
            catch (Exception ex)
            {
                DiagLog($"GetFastCallExport EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                return nint.Zero;
            }
        }

        private static nint GetFastCallHandle()
        {
            if (_fastCallHandle != nint.Zero)
                return _fastCallHandle;

            lock (_lock)
            {
                if (_fastCallHandle != nint.Zero)
                    return _fastCallHandle;

                try
                {
                    _fastCallHandle = NativeLibrary.Load("FastCall.dll");
                    DiagLog($"NativeLibrary.Load('FastCall.dll') = 0x{(uint)_fastCallHandle:X8}");
                    LogModulePath(_fastCallHandle, "NativeLibrary");
                }
                catch (DllNotFoundException ex)
                {
                    DiagLog($"NativeLibrary.Load FAILED: {ex.Message}");
                    // Fallback to explicit path
                    try
                    {
                        var asmDir = Path.GetDirectoryName(typeof(NativeLibraryHelper).Assembly.Location);
                        if (asmDir != null)
                        {
                            var explicitPath = Path.Combine(asmDir, "FastCall.dll");
                            DiagLog($"Trying explicit: {explicitPath} (exists={File.Exists(explicitPath)})");
                            _fastCallHandle = NativeLibrary.Load(explicitPath);
                            DiagLog($"Explicit load = 0x{(uint)_fastCallHandle:X8}");
                        }
                    }
                    catch (Exception ex2) { DiagLog($"Explicit load failed: {ex2.Message}"); }
                }

                // Also log the kernel32 handle for comparison
                var k32 = GetModuleHandle("FastCall.dll");
                DiagLog($"kernel32.GetModuleHandle = 0x{(uint)k32:X8}");
                if (k32 != nint.Zero)
                    LogModulePath(k32, "kernel32");

                // Test a known-good export to verify the DLL has exports at all
                TestKnownExport();

                return _fastCallHandle;
            }
        }

        private static void TestKnownExport()
        {
            // EnumerateVisibleObjects is used by ObjectManager and MUST exist
            var names = new[] { "_EnumerateVisibleObjects@12", "EnumerateVisibleObjects",
                                "_LuaCall@8", "LuaCall",
                                "_SafeCallback1@8", "_SafeCallback3@16" };
            foreach (var name in names)
            {
                var found1 = NativeLibrary.TryGetExport(_fastCallHandle, name, out var a1);
                var a2 = GetProcAddress(_fastCallHandle, name);
                var k32 = GetModuleHandle("FastCall.dll");
                var a3 = k32 != nint.Zero ? GetProcAddress(k32, name) : nint.Zero;
                DiagLog($"  Export '{name}': NativeLib={found1}(0x{(uint)a1:X8}) GetProcAddr_NL=0x{(uint)a2:X8} GetProcAddr_K32=0x{(uint)a3:X8}");
            }
        }

        /// <summary>
        /// Parse PE export directory directly from the loaded module in memory.
        /// </summary>
        private static unsafe nint FindExportInPE(nint moduleBase, string name1, string name2)
        {
            try
            {
                // PE Header: DOS header → PE header → Optional header → Data directories
                var dosHeader = (byte*)moduleBase;
                if (dosHeader[0] != 0x4D || dosHeader[1] != 0x5A) // MZ check
                    return nint.Zero;

                int peOffset = *(int*)(dosHeader + 0x3C);
                var peHeader = dosHeader + peOffset;
                if (peHeader[0] != 0x50 || peHeader[1] != 0x45) // PE check
                    return nint.Zero;

                // Export directory RVA is at optional header offset 0x60 (32-bit PE)
                var optionalHeader = peHeader + 0x18;
                int exportDirRva = *(int*)(optionalHeader + 0x60);
                int exportDirSize = *(int*)(optionalHeader + 0x64);

                if (exportDirRva == 0)
                {
                    DiagLog("[PE-parse] No export directory");
                    return nint.Zero;
                }

                var exportDir = dosHeader + exportDirRva;
                int numberOfNames = *(int*)(exportDir + 0x18);
                int addressTableRva = *(int*)(exportDir + 0x1C);
                int namePointersRva = *(int*)(exportDir + 0x20);
                int ordinalTableRva = *(int*)(exportDir + 0x24);

                DiagLog($"[PE-parse] Export dir at RVA 0x{exportDirRva:X}, {numberOfNames} named exports");

                var namePointers = (int*)(dosHeader + namePointersRva);
                var ordinals = (ushort*)(dosHeader + ordinalTableRva);
                var addresses = (int*)(dosHeader + addressTableRva);

                for (int i = 0; i < numberOfNames; i++)
                {
                    var namePtr = dosHeader + namePointers[i];
                    var exportName = Marshal.PtrToStringAnsi((nint)namePtr) ?? "";

                    if (exportName == name1 || exportName == name2)
                    {
                        int funcRva = addresses[ordinals[i]];
                        var funcAddr = (nint)(dosHeader + funcRva);
                        DiagLog($"[PE-parse] Found '{exportName}' at ordinal {ordinals[i]}, RVA=0x{funcRva:X}, addr=0x{(uint)funcAddr:X8}");
                        return funcAddr;
                    }
                }

                // Log first 10 export names for debugging
                DiagLog("[PE-parse] Export names (first 10):");
                for (int i = 0; i < Math.Min(numberOfNames, 10); i++)
                {
                    var namePtr = dosHeader + namePointers[i];
                    var exportName = Marshal.PtrToStringAnsi((nint)namePtr) ?? "<null>";
                    DiagLog($"  [{i}] {exportName}");
                }

                return nint.Zero;
            }
            catch (Exception ex)
            {
                DiagLog($"[PE-parse] Exception: {ex.GetType().Name}: {ex.Message}");
                return nint.Zero;
            }
        }

        private static void LogAllExports(nint handle)
        {
            // Just test the SafeCallback names with GetLastError
            var names = new[] { "_SafeCallback1@8", "_SafeCallback3@16", "SafeCallback1", "SafeCallback3" };
            foreach (var name in names)
            {
                Marshal.SetLastPInvokeError(0);
                var addr = GetProcAddress(handle, name);
                var err = Marshal.GetLastPInvokeError();
                DiagLog($"  GetProcAddress(0x{(uint)handle:X8}, '{name}') = 0x{(uint)addr:X8}, lastErr={err}");
            }
        }

        private static void LogModulePath(nint handle, string source)
        {
            try
            {
                var buffer = new char[1024];
                var len = GetModuleFileNameW(handle, buffer, buffer.Length);
                if (len > 0)
                    DiagLog($"  [{source}] Path: {new string(buffer, 0, len)}");
            }
            catch { }
        }

        private static void DiagLog(string message)
        {
            if (string.IsNullOrWhiteSpace(DiagnosticLogPath))
            {
                return;
            }

            try
            {
                lock (DiagLogLock)
                {
                    File.AppendAllText(DiagnosticLogPath,
                        $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                }
            }
            catch { }
        }
    }
}
