﻿using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ForegroundBotRunner.Mem.AntiWarden
{
    internal static class WardenDisabler
    {
        private static readonly byte[] pageScanOriginalBytes = [0x8B, 0x45, 0x08, 0x8A, 0x04]; // first 5 bytes of Warden's PageScan function
        private static readonly byte[] memScanOriginalBytes = [0x56, 0x57, 0xFC, 0x8B, 0x54]; // first 5 bytes of Warden's MemScan function

        // different client versions have different function signatures for this hook. the game crashes with an access violation unless you
        // use the right signature here (likely due to stack or register corruption)
        private delegate void DisableWardenVanillaDelegate(nint _);

        private static DisableWardenVanillaDelegate disableWardenVanillaDelegate;
        private static nint wardenPageScanFunPtr = nint.Zero;
        private static nint wardenMemScanFunPtr = nint.Zero;

        // Module scan
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern nint GetModuleHandle(string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern nint GetProcAddress(nint hModule, string procName);

        [StructLayout(LayoutKind.Sequential)]
        private struct MODULEENTRY32
        {
            internal uint dwSize;
            internal uint th32ModuleID;
            internal uint th32ProcessID;
            internal uint GlblcntUsage;
            internal uint ProccntUsage;
            private readonly nint modBaseAddr;
            internal uint modBaseSize;
            private readonly nint hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] internal string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string szExePath;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Module32First(nint hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Module32Next(nint hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void SetLastError(uint dwErrCode);

        private static Module32FirstDelegate module32FirstDelegate;
        private static Detour module32FirstHook;
        private static Module32NextDelegate module32NextDelegate;
        private static Detour module32NextHook;
        private static readonly IList<string> modules =
        [
            "wow.exe",
            "ntdll.dll",
            "kernel32.dll",
            "kernelbase.dll",
            "advapi32.dll",
            "msvcrt.dll",
            "sechost.dll",
            "rpcrt4.dll",
            "sspicli.dll",
            "cryptbase.dll",
            "comctl32.dll",
            "gdi32.dll",
            "user32.dll",
            "lpk.dll",
            "usp10.dll",
            "shell32.dll",
            "shlwapi.dll",
            "wsock32.dll",
            "ws2_32.dll",
            "nsi.dll",
            "opengl32.dll",
            "glu32.dll",
            "ddraw.dll",
            "dciman32.dll",
            "setupapi.dll",
            "cfgmgr32.dll",
            "oleaut32.dll",
            "ole32.dll",
            "devobj.dll",
            "dwmapi.dll",
            "imm32.dll",
            "msctf.dll",
            "divxdecoder.dll",
            "winmm.dll",
            "fmod.dll",
            "msacm32.dll",
            "wininet.dll",
            "api-ms-win-downlevel-user32-l1-1-0.dll",
            "api-ms-win-downlevel-shlwapi-l1-1-0.dll",
            "api-ms-win-downlevel-version-l1-1-0.dll",
            "version.dll",
            "api-ms-win-downlevel-normaliz-l1-1-0.dll",
            "normaliz.dll",
            "iertutil.dll",
            "api-ms-win-downlevel-advapi32-l1-1-0.dll",
            "userenv.dll",
            "profapi.dll",
            "apphelp.dll",
            "acgenral.dll",
            "uxtheme.dll",
            "samcli.dll",
            "sfc.dll",
            "sfc_os.dll",
            "urlmon.dll",
            "api-ms-win-downlevel-ole32-l1-1-0.dll",
            "mpr.dll",
            "acspecfc.dll",
            "mscms.dll",
            "comdlg32.dll",
            "msi.dll",
            "ntmarta.dll",
            "wldap32.dll",
            "d3d9.dll",
            "d3d8thk.dll",
            "nvd3dum.dll",
            "psapi.dll",
            "powrprof.dll",
            "nvscpapi.dll",
            "dsound.dll",
            "clbcatq.dll",
            "mmdevapi.dll",
            "propsys.dll",
            "audioses.dll",
            "wdmaud.drv",
            "ksuser.dll",
            "avrt.dll",
            "msacm32.drv",
            "midimap.dll",
            "mswsock.dll",
            "wshtcpip.dll",
            "secur32.dll",
            "api-ms-win-downlevel-advapi32-l2-1-0.dll",
            "wship6.dll",
            "iphlpapi.dll",
            "winnsi.dll",
            "api-ms-win-downlevel-shlwapi-l2-1-0.dll",
            "dnsapi.dll",
            "rasadhlp.dll",
            "fwpuclnt.dll",
            "comctl32.dll",
            "nlaapi.dll",
            "napinsp.dll",
            "pnrpnsp.dll",
            "winrnr.dll"
        ];

        // we need a 5 byte instruction to use as a valid inline hook option.
        // it gets called when Warden is dynamically added to the WoW process,
        // so we hook here to detour the WardenLoad call to ensure
        // we can detour Warden's various scanning functions before they're
        // called for the first time. the location of this 5 byte instruction
        // depends on which version of the WoW client we're running.
        static internal void Initialize()
        {
            disableWardenVanillaDelegate = DisableWardenInternal;
            var addrToDetour = Marshal.GetFunctionPointerForDelegate(disableWardenVanillaDelegate);

            string[] instructions =
                [
                    "MOV[0xCE8978], EAX",
                    "PUSHFD",
                    "PUSHAD",
                    "PUSH EAX",
                    $"CALL {(uint)addrToDetour}",
                    "POPAD",
                    "POPFD",
                    "JMP 0x006CA233"
                ];

            var wardenLoadDetour = MemoryManager.InjectAssembly("WardenLoadDetour", instructions);
            MemoryManager.InjectAssembly("WardenLoadHook", 0x006CA22E, "JMP " + wardenLoadDetour);
            //InitializeModuleScanHook();
        }

        private static void DisableWardenInternal(nint _)
        {
            Console.WriteLine("[WARDEN] DisableWardenHook called.");
            Console.WriteLine($"[WARDEN] WardenPtr = {_} (0x{_:X})");
            if (_ != nint.Zero)
            {
                var wardenBaseAddr = MemoryManager.ReadIntPtr(_);
                Console.WriteLine($"[WARDEN] DisableWardenHook found WardenBaseAddress = {wardenBaseAddr} (0x{wardenBaseAddr:X})");
                //InitializeWardenPageScanHook(wardenBaseAddr);
                //InitializeWardenMemScanHook(wardenBaseAddr);
            }
        }

        #region InitializeWardenPageScanHook
        private delegate void WardenPageScanDelegate(nint readBase, int readOffset, nint writeTo);

        private static WardenPageScanDelegate wardenPageScanDelegate;
        private static readonly byte[] seed = new byte[4];
        private static readonly byte[] buffer = new byte[20];

        private static void InitializeWardenPageScanHook(nint wardenModuleStart)
        {
            nint pageScanPtr = nint.Zero;

            // in the WotLK client, the PageScan and MemScan functions seem to be loaded into memory at a random offset sometime after the WardenModule base address,
            // and about 6000 bytes later. I spent a bunch of time trying to figure out how to find the address of these functions deterministically, but failed.
            // so instead, we scan 5 bytes of memory 1 byte at a time until we find the function signature. and we start at 6000 and go down because occasionally
            // I've seen the same 5 bytes in memory more in more than one place, but through experimentation, it seems we always want the higher one.

            // TODO [12-3-2022]: this sorta works, but not consistently. need to come back and find a better way of doing this.
            for (var i = 0x10000; i > 0; i--)
            {
                var tempPageScanPtr = nint.Add(wardenModuleStart, i);
                var currentBytes = MemoryManager.ReadBytes(tempPageScanPtr, 5);
                if (currentBytes != null && currentBytes[0] == pageScanOriginalBytes[0])
                {
                    pageScanPtr = tempPageScanPtr;
                    break;
                }
            }

            Console.WriteLine($"[WARDEN] PageScan module found in memory, continuing with hook... {(int)pageScanPtr}");

            wardenPageScanDelegate = WardenPageScanHook;
            var addrToDetour = Marshal.GetFunctionPointerForDelegate(wardenPageScanDelegate);

            var instructions = new[]
            {
                    "MOV EAX, [EBP+8]",
                    "PUSHFD",
                    "PUSHAD",
                    "MOV ECX, ESI",
                    "ADD ECX, EDI",
                    "ADD ECX, 0x1C",
                    "PUSH ECX",
                    "PUSH EDI",
                    "PUSH EAX",
                    $"CALL {(uint)addrToDetour}",
                    "POPAD",
                    "POPFD",
                    "INC EDI",
                    $"JMP {(uint)pageScanPtr + 0xB}"
                };

            var wardenPageScanDetourPtr = MemoryManager.InjectAssembly("WardenPageScanDetour", instructions);
            MemoryManager.InjectAssembly("WardenPageScanHook", (uint)pageScanPtr, "JMP 0x" + wardenPageScanDetourPtr.ToString("X"));

            wardenPageScanFunPtr = pageScanPtr;
            Console.WriteLine($"[WARDEN] PageScan Hooked! WardenModulePtr=0x{wardenModuleStart:X} OriginalPageScanFunPtr=0x{pageScanPtr:X} DetourFunPtr=0x{wardenPageScanDetourPtr:X}");
        }

        private static void WardenPageScanHook(nint readBase, int readOffset, nint writeTo)
        {
            // Logging this to the console lags the client like crazy.
            // Console.WriteLine($"[WARDEN PageScan] BaseAddr: {readBase.ToString("X")}, Offset: {readOffset}");

            var readByteFrom = readBase + readOffset;

            var hacksWithinRange = HackManager.Hacks.Where(h => h.IsWithinScanRange(readByteFrom, 1));

            foreach (var hack in hacksWithinRange)
                Console.WriteLine($"[WARDEN PageScan] Disabling {hack.Name} at {hack.Address:X}");

            foreach (var hack in hacksWithinRange)
                HackManager.DisableHack(hack);

            MemoryManager.WriteByte(writeTo, MemoryManager.ReadByte(readBase + readOffset));

            foreach (var hack in hacksWithinRange)
                HackManager.EnableHack(hack);
        }
        #endregion

        #region InitializeWardenMemScanHook
        private delegate void WardenMemScanDelegate(nint addr, int size, nint bufferStart);

        private static WardenMemScanDelegate wardenMemScanDelegate;

        private static void InitializeWardenMemScanHook(nint wardenModuleStart)
        {
            nint memScanPtr = nint.Zero;
            for (var i = 0x10000; i > 0; i--)
            {
                var tempMemScanPtr = nint.Add(wardenModuleStart, i);
                var currentBytes = MemoryManager.ReadBytes(tempMemScanPtr, 5);
                if (currentBytes != null && currentBytes.SequenceEqual(memScanOriginalBytes))
                {
                    memScanPtr = tempMemScanPtr;
                    break;
                }
            }

            Console.WriteLine($"[WARDEN] MemScan module found in memory, continuing with hook... {(int)memScanPtr}");

            wardenMemScanDelegate = WardenMemScanHook;
            var addrToDetour = Marshal.GetFunctionPointerForDelegate(wardenMemScanDelegate);

            var instructions = new[]
            {
                    "PUSH ESI",
                    "PUSH EDI",
                    "CLD",
                    "MOV EDX, [ESP+20]",
                    "MOV ESI, [ESP+16]",
                    "MOV EAX, [ESP+12]",
                    "MOV ECX, EDX",
                    "MOV EDI, EAX",
                    "PUSHFD",
                    "PUSHAD",
                    "PUSH EDI",
                    "PUSH ECX",
                    "PUSH ESI",
                    $"CALL 0x{(uint)addrToDetour:X}",
                    "POPAD",
                    "POPFD",
                    "POP EDI",
                    "POP ESI",
                    $"JMP 0x{(uint) (memScanPtr + 0x24):X}",
                };

            var wardenMemScanDetourPtr = MemoryManager.InjectAssembly("WardenMemScanDetour", instructions);
            MemoryManager.InjectAssembly("WardenMemScanHook", (uint)memScanPtr, "JMP 0x" + wardenMemScanDetourPtr.ToString("X"));

            wardenMemScanFunPtr = memScanPtr;
            Console.WriteLine($"[WARDEN] MemScan Hooked! WardenModulePtr={wardenModuleStart:X} OriginalMemScanFunPtr=0x{memScanPtr:X} DetourFunPtr=0x{wardenMemScanDetourPtr:X}");
        }

        private static void WardenMemScanHook(nint addr, int size, nint bufferStart)
        {
            // todo: will size ever be 0?
            if (size != 0)
            {
                // Logging this to the console lags the client like crazy
                //Console.WriteLine($"[WARDEN MemoryScan] BaseAddr: {addr.ToString("X")}, Size: {size}");

                var hacksWithinRange = HackManager.Hacks
                    .Where(i => i.Address.ToInt32() <= nint.Add(addr, size).ToInt32() && i.Address.ToInt32() >= addr.ToInt32());

                foreach (var hack in hacksWithinRange)
                    Console.WriteLine($"[WARDEN MemoryScan] Disabling {hack.Name} at {hack.Address:X}");

                foreach (var hack in hacksWithinRange)
                    HackManager.DisableHack(hack);

                MemoryManager.WriteBytes(bufferStart, MemoryManager.ReadBytes(addr, size));

                foreach (var hack in hacksWithinRange)
                    HackManager.EnableHack(hack);
            }
        }
        #endregion

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool Module32FirstDelegate(nint snapshot, ref MODULEENTRY32 module);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool Module32NextDelegate(nint snapshot, ref MODULEENTRY32 module);

        private static void InitializeModuleScanHook()
        {
            var handle = GetModuleHandle("kernel32.dll");
            var firstAddr = GetProcAddress(handle, "Module32First");
            var nextAddr = GetProcAddress(handle, "Module32Next");

            // Registering a delegate pointing to the function we want to detour
            module32FirstDelegate = Marshal.GetDelegateForFunctionPointer<Module32FirstDelegate>(firstAddr);
            module32FirstHook = new Detour(module32FirstDelegate, new Module32FirstDelegate(Module32FirstDetour), "Module32First");

            // Registering a delegate pointing to the function we want to detour
            module32NextDelegate = Marshal.GetDelegateForFunctionPointer<Module32NextDelegate>(nextAddr);
            module32NextHook = new Detour(module32NextDelegate, new Module32NextDelegate(Module32NextDetour), "Module32Next");

            Console.WriteLine($"[WARDEN] ModuleScan Hooked 0x{(int)handle:X} 0x{(int)firstAddr:X} 0x{(int)nextAddr:X}");
        }

        private static readonly HashSet<string> protectedItems = [];

        private static bool Module32FirstDetour(nint snapshot, ref MODULEENTRY32 module)
        {
            Console.WriteLine("[WARDEN ModuleScan] Started");

            // TODO: try to read the Warden packet to see which Module they're scanning for
            //var ptr1 = MemoryManager.ReadIntPtr((IntPtr)WARDEN_PACKET_PTR);
            //var ptr2 = IntPtr.Add(ptr1, 0x634);
            //var ptr3 = MemoryManager.ReadIntPtr(ptr2);

            //seed[0] = MemoryManager.ReadByte(ptr3 + 3);
            //seed[1] = MemoryManager.ReadByte(ptr3 + 4);
            //seed[2] = MemoryManager.ReadByte(ptr3 + 5);
            //seed[3] = MemoryManager.ReadByte(ptr3 + 6);

            //for (var i = 7; i < 27; i++)
            //    buffer[i - 7] = MemoryManager.ReadByte(ptr3 + i);

            module32FirstHook.Remove();
            var ret = Module32First(snapshot, ref module);
            module32FirstHook.Apply();
            return ret;
        }

        private static bool Module32NextDetour(nint snapshot, ref MODULEENTRY32 module)
        {
            module32NextHook.Remove();
            var ret = Module32Next(snapshot, ref module);
            module32NextHook.Apply();

            var moduleName = module.szModule.ToUpper();
            var hmac = new HMACSHA1(seed);
            var dllBytes = Encoding.ASCII.GetBytes(moduleName);
            var dllHash = hmac.ComputeHash(dllBytes);
            var match = buffer.SequenceEqual(dllHash);

            if (match)
                Console.WriteLine($"[WARDEN ModuleScan] Scan detected for {moduleName}, detouring");

            while (!modules.Contains(module.szModule.ToLower()) && ret)
            {
                if (!protectedItems.Contains(module.szModule.ToLower()))
                    protectedItems.Add(module.szModule.ToLower());

                module32NextHook.Remove();
                ret = Module32Next(snapshot, ref module);
                module32NextHook.Apply();
            }
            if (!ret)
            {
                if (!modules.Contains(module.szModule.ToLower()))
                    module = new MODULEENTRY32 { dwSize = 548 };
                SetLastError(18);
            }
            return ret;
        }
    }
}
