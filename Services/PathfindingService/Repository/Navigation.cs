using GameData.Core.Models;
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
        // Newly added for physics bridge:
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PhysicsInput
        {
            // MovementInfoUpdate core
            public uint movementFlags;

            // Position & orientation
            public float posX;
            public float posY;
            public float posZ;
            public float facing;

            // Transport
            public ulong transportGuid;
            public float transportOffsetX;
            public float transportOffsetY;
            public float transportOffsetZ;
            public float transportOrientation;

            // Swimming
            public float swimPitch;

            // Falling / jumping
            public uint fallTime;
            public float jumpVerticalSpeed;
            public float jumpCosAngle;
            public float jumpSinAngle;
            public float jumpHorizontalSpeed;

            // Spline elevation
            public float splineElevation;

            // MovementBlockUpdate speeds
            public float walkSpeed;
            public float runSpeed;
            public float runBackSpeed;
            public float swimSpeed;
            public float swimBackSpeed;

            // Current velocity
            public float velX;
            public float velY;
            public float velZ;

            // Collision & world
            public float radius;
            public float height;
            public float gravity;

            // Terrain fallbacks
            public float adtGroundZ;
            public float adtLiquidZ;

            // Context
            public uint mapId;
        }

        // PhysicsOutput.cs
        [StructLayout(LayoutKind.Sequential)]
        public struct PhysicsOutput
        {
            public float newPosX, newPosY, newPosZ;
            public float newVelX, newVelY, newVelZ;
            public uint movementFlags;
        }

        /* ─────────────── Native delegates ─────────────── */

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate XYZ* CalculatePathDelegate(uint mapId, XYZ start, XYZ end,
                                                    bool straightPath, out int length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FreePathArrDelegate(XYZ* pathArr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool LineOfSightDelegate(uint mapId, XYZ from, XYZ to);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr CapsuleOverlapDelegate(uint mapId, XYZ position,
                                                       float radius, float height, out int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FreeNavPolyArrDelegate(IntPtr ptr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate PhysicsOutput StepPhysicsDelegate(ref PhysicsInput input, float dt);

        /* ─────────────── Function pointers ─────────────── */

        private readonly CalculatePathDelegate calculatePath;
        private readonly FreePathArrDelegate freePathArr;
        private readonly LineOfSightDelegate lineOfSight;
        private readonly CapsuleOverlapDelegate capsuleOverlap;
        private readonly FreeNavPolyArrDelegate freeNavPolyArr;
        private readonly StepPhysicsDelegate stepPhysics;

        /* ─────────────── Constructor: bind all exports ─────────────── */

        private readonly AdtGroundZLoader _adtGroundZLoader;

        public Navigation()
        {
            var binFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var dllPath = FindNavigationDll(binFolder);
            
            if (string.IsNullOrEmpty(dllPath))
            {
                throw new FileNotFoundException($"Navigation.dll not found in any of the expected locations. Searched in: {binFolder}");
            }
            
            var mod = WinProcessImports.LoadLibrary(dllPath);

            if (mod == IntPtr.Zero)
            {
                // Get the last Win32 error for more detailed error information
                var lastError = Marshal.GetLastWin32Error();
                throw new FileNotFoundException($"Failed to load Navigation.dll from path: {dllPath}. Win32 Error Code: {lastError} (0x{lastError:X})", dllPath);
            }

            calculatePath = Marshal.GetDelegateForFunctionPointer<CalculatePathDelegate>(
                WinProcessImports.GetProcAddress(mod, "CalculatePath"));
            freePathArr = Marshal.GetDelegateForFunctionPointer<FreePathArrDelegate>(
                WinProcessImports.GetProcAddress(mod, "FreePathArr"));
            lineOfSight = Marshal.GetDelegateForFunctionPointer<LineOfSightDelegate>(
                WinProcessImports.GetProcAddress(mod, "LineOfSight"));
            capsuleOverlap = Marshal.GetDelegateForFunctionPointer<CapsuleOverlapDelegate>(
                WinProcessImports.GetProcAddress(mod, "CapsuleOverlap"));
            freeNavPolyArr = Marshal.GetDelegateForFunctionPointer<FreeNavPolyArrDelegate>(
                WinProcessImports.GetProcAddress(mod, "FreeNavPolyArr"));
            stepPhysics = Marshal.GetDelegateForFunctionPointer<StepPhysicsDelegate>(
                WinProcessImports.GetProcAddress(mod, "StepPhysics"));

            //_adtGroundZLoader = new AdtGroundZLoader([Path.Combine(binFolder, @"Data\terrain.MPQ")]);
        }

        /// <summary>
        /// Finds Navigation.dll in multiple possible locations to handle different build configurations
        /// </summary>
        private static string? FindNavigationDll(string binFolder)
        {
            var possiblePaths = new[]
            {
                // Current directory (most common case)
                Path.Combine(binFolder, "Navigation.dll"),
                
                // Parent directory (in case of platform-specific subdirectories)
                Path.Combine(Path.GetDirectoryName(binFolder)!, "Navigation.dll"),
                
                // Common Debug output directory (from platform-specific x86/x64 to main Debug)
                Path.Combine(binFolder, "..", "..", "..", "Debug", "net8.0", "Navigation.dll"),
                
                // Alternative paths for different nesting levels
                Path.Combine(binFolder, "..", "..", "Debug", "net8.0", "Navigation.dll"),
                Path.Combine(binFolder, "..", "Debug", "net8.0", "Navigation.dll"),
                
                // Direct fallback to known location
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(binFolder)!))!, "Debug", "net8.0", "Navigation.dll")
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    var resolvedPath = Path.GetFullPath(path);
                    if (File.Exists(resolvedPath))
                    {
                        return resolvedPath;
                    }
                }
                catch
                {
                    // Ignore path resolution errors and continue searching
                    continue;
                }
            }

            return null;
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

        public bool IsLineOfSight(uint mapId, Position from, Position to)
        {
            return lineOfSight(mapId, from.ToXYZ(), to.ToXYZ());
        }

        public PhysicsOutput StepPhysics(PhysicsInput input,
            float dt)
        {
            _adtGroundZLoader.TryGetZ((int)input.mapId, input.posX, input.posY, out float adtGroundZ, out float adtLiquidZ);

            input.adtGroundZ = adtGroundZ;
            input.adtLiquidZ = adtLiquidZ;

            return stepPhysics(ref input, dt);
        }
    }
}
