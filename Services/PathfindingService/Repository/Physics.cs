using GameData.Core.Constants;
using GameData.Core.Models;
using System;
using System.Runtime.InteropServices;

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

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern PhysicsOutput PhysicsStep(ref PhysicsInput input);

        // ===============================
        // PUBLIC METHODS
        // ===============================
        static Physics()
        {
            PreloadMap(0);
            PreloadMap(1);
            PreloadMap(389);
        }

        public PhysicsOutput StepPhysics(PhysicsInput input, float deltaTime)
        {
            input.deltaTime = deltaTime;
            return PhysicsStep(ref input);
        }

        // For backwards compatibility - maps to CalculatePath
        public bool LineOfSight(uint mapId, XYZ from, XYZ to)
        {
            return false;
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
        public uint mapId;
        public float deltaTime;
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
        public float fallDistance;
        public float fallTime;
        public int currentSplineIndex;
        public float splineProgress;
    }
}