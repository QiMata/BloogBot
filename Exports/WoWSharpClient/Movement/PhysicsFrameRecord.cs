namespace WoWSharpClient.Movement
{
    /// <summary>
    /// Per-frame snapshot of physics state for parity diagnostics.
    /// Captures everything needed to diagnose Z bouncing, ground clamping issues,
    /// and divergence between FG (gold standard) and BG (custom physics).
    /// </summary>
    public readonly record struct PhysicsFrameRecord
    {
        // Timing
        public int FrameNumber { get; init; }
        public uint GameTimeMs { get; init; }
        public float DeltaSec { get; init; }

        // Position (after all guards applied)
        public float PosX { get; init; }
        public float PosY { get; init; }
        public float PosZ { get; init; }

        // Raw physics output (before guards)
        public float RawPosZ { get; init; }
        public float PhysicsGroundZ { get; init; }

        // Ground tracking state
        public float PrevGroundZ { get; init; }
        public bool HasPhysicsGroundContact { get; init; }

        // Velocity
        public float VelX { get; init; }
        public float VelY { get; init; }
        public float VelZ { get; init; }

        // Fall state
        public uint FallTimeMs { get; init; }
        public bool IsFalling { get; init; }

        // Movement flags (after physics)
        public uint MovementFlags { get; init; }

        // Guard decisions — which guards fired this frame
        public bool SlopeGuardRejected { get; init; }
        public bool PathGroundGuardActive { get; init; }
        public bool FalseFreefallSuppressed { get; init; }
        public bool TeleportClampActive { get; init; }
        public bool UndergroundSnapFired { get; init; }

        // Wall contact
        public bool HitWall { get; init; }
        public float WallNormalX { get; init; }
        public float WallNormalY { get; init; }
        public float BlockedFraction { get; init; }

        // Path state
        public float PathWaypointZ { get; init; }
        public int PathWaypointIndex { get; init; }

        // Computed: Z delta from previous frame
        public float ZDeltaFromPrev { get; init; }
    }
}
