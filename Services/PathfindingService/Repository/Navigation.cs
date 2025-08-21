using GameData.Core.Models;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Runtime.InteropServices;
using Position = GameData.Core.Models.Position;

namespace PathfindingService.Repository
{
    public unsafe class Navigation
    {
        /* ─────────────── Structs ─────────────── */
        [StructLayout(LayoutKind.Sequential)]
        public struct NavPoly
        {
            public ulong RefId;
            public uint Area;
            public uint Flags;
            public uint VertCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public XYZ[] Verts;
        }

        // Must match native PhysicsInput (see native PhysicsBridge.h)
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PhysicsInput
        {
            public uint movementFlags;
            public float posX, posY, posZ, facing;
            public ulong transportGuid;
            public float transportOffsetX, transportOffsetY, transportOffsetZ, transportOrientation;
            public float swimPitch;
            public uint fallTime;
            public float jumpVerticalSpeed, jumpCosAngle, jumpSinAngle, jumpHorizontalSpeed;
            public float splineElevation;
            public float walkSpeed, runSpeed, runBackSpeed, swimSpeed, swimBackSpeed;
            public float velX, velY, velZ;
            public float radius, height, gravity;
            public float adtGroundZ, adtLiquidZ;
            public uint mapId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PhysicsOutput
        {
            public float newPosX, newPosY, newPosZ;
            public float newVelX, newVelY, newVelZ;
            public uint movementFlags;
        }

        /* ─────────────── Native delegates ─────────────── */
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate XYZ* CalculatePathDelegate(uint mapId, XYZ start, XYZ end, bool straightPath, out int length);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FreePathArrDelegate(XYZ* pathArr);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate bool LineOfSightDelegate(uint mapId, XYZ from, XYZ to);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr CapsuleOverlapDelegate(uint mapId, XYZ position, float radius, float height, out int count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FreeNavPolyArrDelegate(IntPtr ptr);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate PhysicsOutput StepPhysicsDelegate(ref PhysicsInput input, float dt);

        /* ─────────────── Function pointers ─────────────── */
        private readonly CalculatePathDelegate calculatePath;
        private readonly FreePathArrDelegate freePathArr;
        private readonly LineOfSightDelegate lineOfSight;
        private readonly CapsuleOverlapDelegate capsuleOverlap;
        private readonly FreeNavPolyArrDelegate freeNavPolyArr;
        private readonly StepPhysicsDelegate stepPhysics;

        private readonly AdtGroundZLoader _adtGroundZLoader; // currently unused (lazy terrain loading)

        public Navigation(IConfiguration configuration)
        {
            // Try to get DLL path from environment variable first, then fallback to configuration
            var dllPath = Environment.GetEnvironmentVariable("NAVIGATION_DLL_PATH");
            
            if (string.IsNullOrEmpty(dllPath))
            {
                // Read from configuration
                dllPath = configuration["Navigation:DllPath"];
            }

            if (string.IsNullOrEmpty(dllPath))
            {
                throw new InvalidOperationException(
                    "Navigation DLL path not found. Set either NAVIGATION_DLL_PATH environment variable or Navigation:DllPath in configuration.");
            }

            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException($"Navigation.dll not found at path: {dllPath}");
            }

            Console.WriteLine($"[Navigation] Loading Navigation.dll from: {dllPath}");

            // Validate architecture before attempting to load (prevents cryptic 193/1114)
            ValidateArchitecture(dllPath);

            var mod = WinProcessImports.LoadLibrary(dllPath);
            if (mod == IntPtr.Zero)
            {
                var lastError = Marshal.GetLastWin32Error();
                throw new FileNotFoundException($"Failed to load Navigation.dll from path: {dllPath}. Win32 Error Code: {lastError} (0x{lastError:X}). This typically indicates DllMain failure or missing dependency.", dllPath);
            }

            calculatePath   = GetExport<CalculatePathDelegate>(mod, "CalculatePath");
            freePathArr     = GetExport<FreePathArrDelegate>(mod, "FreePathArr");
            lineOfSight     = GetExport<LineOfSightDelegate>(mod, "LineOfSight");
            capsuleOverlap  = GetExport<CapsuleOverlapDelegate>(mod, "CapsuleOverlap");
            freeNavPolyArr  = GetExport<FreeNavPolyArrDelegate>(mod, "FreeNavPolyArr");
            stepPhysics     = GetExport<StepPhysicsDelegate>(mod, "StepPhysics");

            Console.WriteLine("[Navigation] Successfully loaded all exports from Navigation.dll");
        }

        private static T GetExport<T>(IntPtr module, string name) where T : Delegate
        {
            var proc = WinProcessImports.GetProcAddress(module, name);
            if (proc == IntPtr.Zero)
                throw new EntryPointNotFoundException($"Export '{name}' not found in Navigation.dll");
            return Marshal.GetDelegateForFunctionPointer<T>(proc);
        }

        private static void ValidateArchitecture(string dllPath)
        {
            try
            {
                using var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var br = new BinaryReader(fs);
                if (br.ReadUInt16() != 0x5A4D) return; // MZ
                fs.Seek(0x3C, SeekOrigin.Begin);
                var peOffset = br.ReadInt32();
                fs.Seek(peOffset, SeekOrigin.Begin);
                if (br.ReadUInt32() != 0x4550) return; // PE00
                var machine = br.ReadUInt16();
                bool proc64 = Environment.Is64BitProcess;
                bool dll64 = machine == 0x8664;
                if (proc64 != dll64)
                    throw new BadImageFormatException($"Architecture mismatch: process {(proc64 ? "x64" : "x86")} vs DLL {(dll64 ? "x64" : "x86")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Navigation] Architecture validation warning: {ex.Message}");
            }
        }

        public Position[] CalculatePath(uint mapId, Position start, Position end, bool straightPath)
        {
            var ptr = calculatePath(mapId, start.ToXYZ(), end.ToXYZ(), straightPath, out int len);
            var path = new Position[len];
            for (int i = 0; i < len; ++i)
                path[i] = new Position(ptr[i]);
            freePathArr(ptr);
            return path;
        }

        public bool IsLineOfSight(uint mapId, Position from, Position to) => lineOfSight(mapId, from.ToXYZ(), to.ToXYZ());

        public PhysicsOutput StepPhysics(PhysicsInput input, float dt)
        {
            float adtGroundZ = 0f, adtLiquidZ = 0f;
            _adtGroundZLoader?.TryGetZ((int)input.mapId, input.posX, input.posY, out adtGroundZ, out adtLiquidZ);
            input.adtGroundZ = adtGroundZ;
            input.adtLiquidZ = adtLiquidZ;
            return stepPhysics(ref input, dt);
        }
    }
}
