using GameData.Core.Models;
using System.Runtime.InteropServices;
using GameData.Core.Enums; // Access MovementFlags for sanitization

namespace PathfindingService.Repository
{
    public class Physics
    {
        private const string DLL_NAME = "Navigation.dll";

        // ===============================
        // ESSENTIAL IMPORTS ONLY
        // ===============================

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void PreloadMap(uint mapId);

        // Removed legacy PhysicsStep import
        //[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        //private static extern PhysicsOutput PhysicsStep(ref PhysicsInput input);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern PhysicsOutput PhysicsStepV2(ref PhysicsInput input);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LineOfSight")]
        private static extern bool NativeLineOfSight(uint mapId, XYZ from, XYZ to);

        // ===============================
        // PUBLIC METHODS
        // ===============================
        static Physics()
        {
            PreloadMap(0);
            PreloadMap(1);
            PreloadMap(389);
        }

        public PhysicsOutput StepPhysicsV2(PhysicsInput input, float deltaTime)
        {
            input.deltaTime = deltaTime;
            var output = PhysicsStepV2(ref input);
            return SanitizeOutput(input, output);
        }

        // For backwards compatibility - maps to CalculatePath
        public bool LineOfSight(uint mapId, XYZ from, XYZ to)
        {
            return NativeLineOfSight(mapId, from, to);
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
    }

    // ===============================
    // DATA STRUCTURES
    // ===============================

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

        public uint mapId;
        public float deltaTime;
        public uint frameCounter;
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
        public float fallTime;
        public int currentSplineIndex;
        public float splineProgress;
    }
}