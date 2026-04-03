using GameData.Core.Models;
using System.Runtime.InteropServices;
using GameData.Core.Enums;
using System;
using System.IO;
using System.Linq; // Access MovementFlags for sanitization
using System.Collections.Generic;

namespace PathfindingService.Repository
{
    public class Physics
    {
        private const string DLL_NAME = "Navigation";
        private static readonly string[] NativeLibraryFileNames = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["Navigation.dll", "Navigation"]
            : ["libNavigation.so", "Navigation"];
        private static bool _dllLoaded = false;
        private static bool _mapsPreloaded = false;
        private static readonly object _loadLock = new object();
        private static List<uint> _preloadedMapIds = [];

        public static IReadOnlyList<uint> PreloadedMapIds => _preloadedMapIds;

        // ===============================
        // NATIVE LIBRARY HELPER
        // ===============================
        
        /// <summary>
        /// Ensures the native Navigation library is loaded before any P/Invoke calls.
        /// Call this at the start of the application to get better error messages.
        /// </summary>
        public static void EnsureNativeLibraryLoaded()
        {
            lock (_loadLock)
            {
                if (_dllLoaded) return;
                
                var baseDir = AppContext.BaseDirectory;
                var cwdDir = Directory.GetCurrentDirectory();
                
                var searchPaths = NativeLibraryFileNames
                    .SelectMany(fileName => new[]
                    {
                        Path.Combine(baseDir, fileName),
                        Path.Combine(cwdDir, fileName),
                        fileName // System path
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Console.WriteLine($"[Physics] Looking for {DLL_NAME} native library...");
                Console.WriteLine($"[Physics]   AppContext.BaseDirectory: {baseDir}");
                Console.WriteLine($"[Physics]   Current Directory: {cwdDir}");

                foreach (var path in searchPaths)
                {
                    var fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
                    Console.WriteLine($"[Physics]   Checking: {fullPath}");
                    
                    if (File.Exists(fullPath))
                    {
                        Console.WriteLine($"[Physics]   Found file at: {fullPath}");
                        try
                        {
                            if (NativeLibrary.TryLoad(fullPath, out IntPtr handle))
                            {
                                Console.WriteLine($"[Physics]   Successfully loaded {DLL_NAME}!");
                                _dllLoaded = true;
                                
                                // Now preload maps
                                PreloadMapsInternal();
                                return;
                            }
                            else
                            {
                                Console.WriteLine($"[Physics]   Failed to load (TryLoad returned false)");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Physics]   Failed to load: {ex.Message}");
                        }
                    }
                }

                // If we couldn't load explicitly, try to let P/Invoke find it via PATH
                Console.WriteLine($"[Physics]   Attempting P/Invoke load via system PATH...");
                try
                {
                    // This will trigger the DLL load
                    PreloadMapsInternal();
                    _dllLoaded = true;
                    Console.WriteLine($"[Physics]   Loaded via PATH successfully!");
                    return;
                }
                catch (DllNotFoundException)
                {
                    // Expected if not in PATH
                }

                throw new DllNotFoundException(
                    $"Unable to find or load '{DLL_NAME}'. " +
                    $"Searched in: {string.Join(", ", searchPaths.Select(p => Path.IsPathRooted(p) ? p : Path.GetFullPath(p)))}. " +
                    "Make sure the Navigation native library is built and in the application directory or loader path.");
            }
        }

        private static void PreloadMapsInternal()
        {
            if (_mapsPreloaded) return;
            Console.WriteLine($"[Physics]   Preloading navigation maps...");

            var dataRoot = ResolveDataRoot();
            if (!string.IsNullOrWhiteSpace(dataRoot))
            {
                Console.WriteLine($"[Physics]   Using data root: {dataRoot}");
                SetDataDirectory(dataRoot);
            }

            var mapIds = DiscoverMapIds(dataRoot);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var mapId in mapIds)
            {
                sw.Restart();
                Console.WriteLine($"[Physics]   Loading map {mapId}...");
                PreloadMap(mapId);
                Console.WriteLine($"[Physics]   Map {mapId} loaded in {sw.Elapsed.TotalSeconds:F1}s");
            }

            _preloadedMapIds = mapIds;
            _mapsPreloaded = true;
            Console.WriteLine($"[Physics]   All maps preloaded successfully! Count={_preloadedMapIds.Count}");
        }

        // ===============================
        // ESSENTIAL IMPORTS ONLY
        // ===============================

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void PreloadMap(uint mapId);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void SetDataDirectory(string dataDir);

        // Removed legacy PhysicsStep import
        //[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        //private static extern PhysicsOutput PhysicsStep(ref PhysicsInput input);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern PhysicsOutput PhysicsStepV2(ref PhysicsInput input);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetPhysicsLogLevel(int level, uint mask);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LineOfSight")]
        private static extern bool NativeLineOfSight(uint mapId, XYZ from, XYZ to);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetGroundZ")]
        private static extern float NativeGetGroundZ(uint mapId, float x, float y, float z, float maxSearchDist);

        // ===============================
        // PUBLIC METHODS
        // ===============================

        public PhysicsOutput StepPhysicsV2(PhysicsInput input, float deltaTime)
        {
            input.deltaTime = deltaTime;
            var output = PhysicsStepV2(ref input);
            return SanitizeOutput(input, output);
        }

        /// <summary>
        /// Sets the C++ physics engine log level at runtime.
        /// level: 0=ERR, 1=INFO, 2=DBG, 3=TRACE. mask: category bitmask (0=keep current).
        /// </summary>
        public void EnablePhysicsLogging(int level, uint mask = 0)
        {
            SetPhysicsLogLevel(level, mask);
        }

        // For backwards compatibility - maps to CalculatePath
        public bool LineOfSight(uint mapId, XYZ from, XYZ to)
        {
            return NativeLineOfSight(mapId, from, to);
        }

        public (float groundZ, bool found) GetGroundZ(uint mapId, float x, float y, float z, float maxSearchDist = 10.0f)
        {
            float result = NativeGetGroundZ(mapId, x, y, z, maxSearchDist);
            bool found = !float.IsNaN(result) && result > -200000f;
            return (result, found);
        }

        // ===============================
        // HELPER: sanitize legacy/undesired flags from the native engine
        // ===============================
        private static PhysicsOutput SanitizeOutput(PhysicsInput input, PhysicsOutput output)
        {
            var startFlags = (MovementFlags)input.moveFlags;
            var outFlags = (MovementFlags)output.moveFlags;

            // Never use MOVEFLAG_MOVED (legacy client-only flag). Remove if present.
            outFlags &= ~MovementFlags.MOVEFLAG_MOVED;

            // If there was no intended movement (no XZ or turn/pitch), ensure zero velocities to avoid spurious motion.
            bool intendedMove = (startFlags & MovementFlags.MOVEFLAG_MASK_MOVING_OR_TURN) != 0;
            if (!intendedMove)
            {
                output.vx = 0f;
                output.vy = 0f;
                output.vz = 0f;
            }

            output.moveFlags = (uint)outFlags;
            return output;
        }

        private static string? ResolveDataRoot()
        {
            var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(dataDir) && Directory.Exists(dataDir))
                return Path.GetFullPath(dataDir);

            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "Data"),
                Path.Combine(baseDir, "..", "Data"),
                Path.Combine(baseDir, "..", "..", "Data"),
                Path.Combine(baseDir, "..", "..", "..", "Data"),
            };

            foreach (var candidate in candidates)
            {
                var resolved = Path.GetFullPath(candidate);
                if (Directory.Exists(resolved))
                    return resolved;
            }

            return null;
        }

        private static List<uint> DiscoverMapIds(string? dataRoot)
        {
            var ids = new HashSet<uint>();

            if (!string.IsNullOrWhiteSpace(dataRoot))
            {
                AddIdsFromDirectory(Path.Combine(dataRoot, "scenes"), "*.scene", ParseWholeStem, ids);
                AddIdsFromDirectory(Path.Combine(dataRoot, "mmaps"), "*.mmap", ParseWholeStem, ids);
                AddIdsFromDirectory(Path.Combine(dataRoot, "maps"), "*.map", ParseFirstThreeDigits, ids);
            }

            if (ids.Count == 0)
            {
                ids.Add(0);
                ids.Add(1);
                ids.Add(389);
            }

            return ids.OrderBy(id => id).ToList();
        }

        private static void AddIdsFromDirectory(string directory, string pattern, Func<string, uint?> parser, HashSet<uint> ids)
        {
            if (!Directory.Exists(directory))
                return;

            foreach (var file in Directory.GetFiles(directory, pattern))
            {
                var id = parser(Path.GetFileNameWithoutExtension(file));
                if (id.HasValue)
                    ids.Add(id.Value);
            }
        }

        private static uint? ParseWholeStem(string stem)
            => uint.TryParse(stem, out var mapId) ? mapId : null;

        private static uint? ParseFirstThreeDigits(string stem)
        {
            if (stem.Length < 3)
                return null;

            return uint.TryParse(stem[..3], out var mapId) ? mapId : null;
        }
    }

    // ===============================
    // DATA STRUCTURES
    // ===============================

    [StructLayout(LayoutKind.Sequential)]
    public struct DynamicObjectInfo
    {
        public ulong guid;
        public uint displayId;
        public float x, y, z;
        public float orientation;
        public float scale;
        public uint goState;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PhysicsInput
    {
        public uint moveFlags;
        public float x, y, z;
        public float orientation;
        public float pitch;
        public float vx, vy, vz;
        public float walkSpeed;
        public float runSpeed;
        public float runBackSpeed;
        public float swimSpeed;
        public float swimBackSpeed;
        public float flightSpeed;
        public float turnSpeed;
        public ulong transportGuid;
        public float transportX, transportY, transportZ, transportO;
        public uint fallTime;
        public float fallStartZ;
        public float height;
        public float radius;
        [MarshalAs(UnmanagedType.I1)]
        public bool hasSplinePath;
        public float splineSpeed;
        public IntPtr splinePoints;
        public int splinePointCount;
        public int currentSplineIndex;
        // Previous ground tracking (mirrors PhysicsBridge.h)
        public float prevGroundZ;                // last known ground height
        public float prevGroundNx;               // previous ground normal X
        public float prevGroundNy;               // previous ground normal Y
        public float prevGroundNz;               // previous ground normal Z

		// Pending depenetration (mirrors PhysicsBridge.h)
		public float pendingDepenX;
		public float pendingDepenY;
		public float pendingDepenZ;

		// Ride-on touched object (mirrors PhysicsBridge.h)
		public uint standingOnInstanceId;
		public float standingOnLocalX;
		public float standingOnLocalY;
		public float standingOnLocalZ;

		public IntPtr nearbyObjects;
		public int nearbyObjectCount;

        public uint mapId;
        public float deltaTime;
        public uint frameCounter;
        public uint physicsFlags;
        public float stepUpBaseZ;       // step-up height to maintain (-200000 = inactive)
        public uint stepUpAge;          // frames since step-up detected
        public uint groundedWallState;  // internal selected-contact walkability state
        public uint wasGrounded;        // CMovement grounded state from previous frame
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PhysicsOutput
    {
        public float x, y, z;
        public float orientation;
        public float pitch;
        public float vx, vy, vz;
        public uint moveFlags;
        // Removed state flags: isGrounded, isSwimming, isFlying, collided
        public float groundZ;
        public float liquidZ;
        public uint liquidType;              // align with PhysicsBridge.h
        // Ground surface identification (mirrors PhysicsBridge.h)
        public float groundNx;                   // ground surface normal X
        public float groundNy;                   // ground surface normal Y
        public float groundNz;                   // ground surface normal Z

		// Pending depenetration (mirrors PhysicsBridge.h)
		public float pendingDepenX;
		public float pendingDepenY;
		public float pendingDepenZ;

		// Ride-on touched object (mirrors PhysicsBridge.h)
		public uint standingOnInstanceId;
		public float standingOnLocalX;
		public float standingOnLocalY;
		public float standingOnLocalZ;

        public float fallDistance;
        public float fallStartZ;
        public float fallTime;
        public int currentSplineIndex;
        public float splineProgress;

        // Wall contact feedback (mirrors PhysicsBridge.h)
        [MarshalAs(UnmanagedType.I1)]
        public bool hitWall;         // true if horizontal movement was blocked by a wall
        public float wallNormalX;    // world-space wall surface normal
        public float wallNormalY;
        public float wallNormalZ;
        public float blockedFraction; // 0=fully blocked, 1=no block
        public float stepUpBaseZ;       // step-up height to maintain (-200000 = inactive)
        public uint stepUpAge;          // frames since step-up detected
        public uint groundedWallState;  // internal selected-contact walkability state
    }
}
