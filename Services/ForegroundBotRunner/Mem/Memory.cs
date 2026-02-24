using Binarysharp.Assemblers.Fasm;
using GameData.Core.Models;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;
using System;

namespace ForegroundBotRunner.Mem
{
    public static unsafe class MemoryManager
    {
        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtect(nint address, int size, uint newProtect, out uint oldProtect);

        [Flags]
        public enum Protection
        {
            PAGE_NOACCESS = 0x01,
            PAGE_READONLY = 0x02,
            PAGE_READWRITE = 0x04,
            PAGE_WRITECOPY = 0x08,
            PAGE_EXECUTE = 0x10,
            PAGE_EXECUTE_READ = 0x20,
            PAGE_EXECUTE_READWRITE = 0x40,
            PAGE_EXECUTE_WRITECOPY = 0x80,
            PAGE_GUARD = 0x100,
            PAGE_NOCACHE = 0x200,
            PAGE_WRITECOMBINE = 0x400
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern nint GetModuleHandle(string? lpModuleName);

        // Use GetModuleHandle(null) which returns the base address of the main module
        // This works when injected without requiring process handle access
        public static readonly nint imageBase = GetModuleHandle(null);
        private static readonly FasmNet fasm = new();

        [HandleProcessCorruptedStateExceptions]
        static internal byte ReadByte(nint address, bool isRelative = false)
        {
            if (address == nint.Zero)
                return 0;

            if (isRelative)
                address = imageBase + (int)address;

            try
            {
                return *(byte*)address;
            }
            catch (AccessViolationException)
            {
                Log.Error("Access Violation on " + address.ToString("X") + " with type Byte");
                return default;
            }
            catch (Exception e)
            {
                Log.Error($"[MEMORY]{e.Message}{e.InnerException.StackTrace}");
                return default;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        static public short ReadShort(nint address, bool isRelative = false)
        {
            if (address == nint.Zero)
                return 0;

            if (isRelative)
                address = imageBase + (int)address;

            try
            {
                return *(short*)address;
            }
            catch (AccessViolationException)
            {
                Log.Error("Access Violation on " + address.ToString("X") + " with type Short");
                return default;
            }
            catch (Exception e)
            {
                Log.Error($"[MEMORY]{e.Message}{e.InnerException.StackTrace}");
                return default;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        static public int ReadInt(nint address, bool isRelative = false)
        {
            if (address == nint.Zero)
                return 0;

            if (isRelative)
                address = imageBase + (int)address;

            try
            {
                return *(int*)address;
            }
            catch (AccessViolationException)
            {
                Log.Error("Access Violation on " + address.ToString("X") + " with type Int");
                return default;
            }
            catch (Exception e)
            {
                Log.Error($"[MEMORY]{e.Message}{e.InnerException.StackTrace}");
                return default;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        static public uint ReadUint(nint address, bool isRelative = false)
        {
            if (address == nint.Zero)
                return 0;

            if (isRelative)
                address = imageBase + (int)address;

            try
            {
                return *(uint*)address;
            }
            catch (AccessViolationException)
            {
                Log.Error("Access Violation on " + address.ToString("X") + " with type Uint");
                return default;
            }
            catch (Exception e)
            {
                Log.Error($"[MEMORY]{e.Message}{e.InnerException.StackTrace}");
                return default;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        static public ulong ReadUlong(nint address, bool isRelative = false)
        {
            if (address == nint.Zero)
                return 0;

            if (isRelative)
                address = imageBase + (int)address;

            try
            {
                return *(ulong*)address;
            }
            catch (AccessViolationException)
            {
                Log.Error("Access Violation on " + address.ToString("X") + " with type Ulong");
                return default;
            }
            catch (Exception e)
            {
                Log.Error($"[MEMORY]{e.Message}{e.InnerException.StackTrace}");
                return default;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        static public nint ReadIntPtr(nint address, bool isRelative = false)
        {
            if (address == nint.Zero)
                return nint.Zero;

            if (isRelative)
                address = imageBase + (int)address;

            try
            {
                return *(nint*)address;
            }
            catch (AccessViolationException)
            {
                Log.Error("Access Violation on " + address.ToString("X") + " with type IntPtr");
                return default;
            }
            catch (Exception e)
            {
                Log.Error($"[MEMORY]{e.Message}{e.InnerException.StackTrace}");
                return default;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        static public float ReadFloat(nint address, bool isRelative = false)
        {
            if (address == nint.Zero)
                return 0;

            if (isRelative)
                address = imageBase + (int)address;

            try
            {
                return *(float*)address;
            }
            catch (AccessViolationException)
            {
                Log.Error("Access Violation on " + address.ToString("X") + " with type Float");
                return default;
            }
            catch (Exception e)
            {
                Log.Error($"[MEMORY]{e.Message}{e.InnerException.StackTrace}");
                return default;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        static public string ReadString(nint address, int size = 512, bool isRelative = false)
        {
            if (address == nint.Zero)
                return null;

            if (isRelative)
                address = imageBase + (int)address;

            try
            {
                var buffer = ReadBytes(address, size);
                if (buffer.Length == 0)
                    return default;

                var ret = Encoding.ASCII.GetString(buffer);

                if (ret.IndexOf('\0') != -1)
                    ret = ret.Remove(ret.IndexOf('\0'));

                return ret;
            }
            catch (AccessViolationException)
            {
                Log.Error("Access Violation on " + address.ToString("X") + " with type string");
                return default;
            }
            catch (Exception e)
            {
                Log.Error($"[MEMORY]{e.Message}{e.InnerException.StackTrace}");
                return "";
            }
        }

        [HandleProcessCorruptedStateExceptions]
        static public byte[] ReadBytes(nint address, int count, bool isRelative = false)
        {
            if (address == nint.Zero)
                return null;

            if (isRelative)
                address = imageBase + (int)address;

            try
            {
                var ret = new byte[count];
                var ptr = (byte*)address;

                for (var i = 0; i < count; i++)
                    ret[i] = ptr[i];

                return ret;
            }
            catch (NullReferenceException)
            {
                return default;
            }
            catch (AccessViolationException)
            {
                return default;
            }
            catch (Exception e)
            {
                Log.Error($"[MEMORY]{e.Message}{e.InnerException.StackTrace}");
                return default;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        static public ItemCacheEntry ReadItemCacheEntry(nint address)
        {
            if (address == nint.Zero)
                return null;

            try
            {
                return new ItemCacheEntry(address);
            }
            catch (AccessViolationException)
            {
                Log.Error("Access Violation on " + address.ToString("X") + " with type ItemCacheEntry");
                return default;
            }
            catch (Exception e)
            {
                Log.Error($"[MEMORY]{e.Message}{e.InnerException.StackTrace}");
                return default;
            }
        }

        static internal void WriteByte(nint address, byte value) => Marshal.StructureToPtr(value, address, false);

        static internal void WriteInt(nint address, int value) => Marshal.StructureToPtr(value, address, false);

        static internal void WriteFloat(nint address, float value) => *(float*)address = value;

        // In-process memory write: VirtualProtect to make writable, then direct copy.
        // WriteProcessMemory via OpenProcess fails in .NET 8 injected context — use direct writes instead.
        static internal void WriteBytes(nint address, byte[] bytes)
        {
            if (address == nint.Zero || bytes.Length == 0)
                return;

            // Make the target page writable + executable
            VirtualProtect(address, bytes.Length, (uint)Protection.PAGE_EXECUTE_READWRITE, out uint oldProtect);

            // Direct in-process memory write (no OpenProcess/WriteProcessMemory needed)
            Marshal.Copy(bytes, 0, address, bytes.Length);
        }

        static internal nint InjectAssembly(string hackName, string[] instructions)
        {
            // first get the assembly as bytes for the allocated area before overwriting the memory
            fasm.Clear();
            fasm.AddLine("use32");
            foreach (var x in instructions)
                fasm.AddLine(x);

            var byteCode = new byte[0];
            try
            {
                byteCode = fasm.Assemble();
            }
            catch (FasmAssemblerException ex)
            {
                Log.Error("[FASM] Assemble failed for {Name}: {Error}", hackName, ex.Message);
            }

            var start = Marshal.AllocHGlobal(byteCode.Length);
            fasm.Clear();
            fasm.AddLine("use32");
            foreach (var x in instructions)
                fasm.AddLine(x);
            byteCode = fasm.Assemble(start);

            var hack = new Hack(hackName, start, byteCode);
            HackManager.AddHack(hack);

            return start;
        }

        static internal void InjectAssembly(string hackName, uint ptr, string instructions)
        {
            fasm.Clear();
            fasm.AddLine("use32");
            fasm.AddLine(instructions);
            var start = new nint(ptr);
            var byteCode = fasm.Assemble(start);

            var hack = new Hack(hackName, start, byteCode);
            HackManager.AddHack(hack);
        }
    }
}
