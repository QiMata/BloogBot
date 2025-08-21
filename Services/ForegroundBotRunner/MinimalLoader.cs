using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices; // for CallConvStdcall / CallConvCdecl

namespace ForegroundBotRunner
{
    public class MinimalLoader
    {
        // Explicitly specify stdcall so it matches native declaration on x86.
        [UnmanagedCallersOnly(EntryPoint = "TestEntry", CallConvs = new[] { typeof(CallConvStdcall) })]
        public static int TestEntry(IntPtr argsPtr, int size)
        {
            try
            {
                // Write a tiny breadcrumb file so native side can see we executed
                TryWrite("testentry_stdcall.txt", "Entered stdcall TestEntry\n");
                // Sleep a little to keep thread alive momentarily
                System.Threading.Thread.Sleep(50);
                return 42; // distinct code
            }
            catch (Exception ex)
            {
                TryWrite("testentry_stdcall.txt", $"Exception: {ex}\n");
                return -1;
            }
        }

        // cdecl variant for diagnostic comparison
        [UnmanagedCallersOnly(EntryPoint = "TestEntryCdecl", CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int TestEntryCdecl(IntPtr argsPtr, int size)
        {
            try
            {
                TryWrite("testentry_cdecl.txt", "Entered cdecl TestEntryCdecl\n");
                System.Threading.Thread.Sleep(50);
                return 43; // distinct code
            }
            catch (Exception ex)
            {
                TryWrite("testentry_cdecl.txt", $"Exception: {ex}\n");
                return -1;
            }
        }

        private static void TryWrite(string fileName, string msg)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
                var path = System.IO.Path.Combine(baseDir, fileName);
                System.IO.File.AppendAllText(path, msg);
            }
            catch { }
        }
    }
}