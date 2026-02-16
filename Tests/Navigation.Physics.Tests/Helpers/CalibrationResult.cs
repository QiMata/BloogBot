using System;
using System.Collections.Generic;
using System.Linq;

namespace Navigation.Physics.Tests.Helpers;

/// <summary>
/// Categorizes what kind of movement state transition a frame represents.
/// </summary>
public enum TransitionType
{
    None,            // Steady-state (no flag change)
    JumpStart,       // Ground → Air (JUMPING flag set)
    Landing,         // Air → Ground (JUMPING/FALLING cleared)
    WaterEntry,      // Non-swimming → Swimming (SWIMMING flag set)
    WaterExit,       // Swimming → Non-swimming (SWIMMING flag cleared)
    DirectionChange, // Direction flags changed but same movement mode
    SpeedChange,     // Walk/run mode changed
    SurfaceStep,     // Ground-to-ground Z change > threshold (step-up/step-down on WMO edge)
    Other            // Other flag transitions
}

/// <summary>
/// Tracks frame-by-frame calibration results from replaying recordings through the physics engine.
/// </summary>
public class CalibrationResult
{
    public List<FrameDetail> FrameDetails { get; } = [];
    public int FrameCount => FrameDetails.Count;
    public float MaxPositionError { get; private set; }
    public int WorstFrame { get; private set; } = -1;
    public float AvgPositionError => FrameDetails.Count > 0 ? FrameDetails.Average(f => f.PosError) : 0;
    public float AvgHorizError => FrameDetails.Count > 0 ? FrameDetails.Average(f => f.HorizError) : 0;
    public float AvgVertError => FrameDetails.Count > 0 ? FrameDetails.Average(f => f.VertError) : 0;
    public int TeleportCount { get; private set; }
    public int TransportTransitionCount { get; private set; }
    public int TransportFrameCount { get; private set; }
    public int TransportSimulatedCount { get; private set; }

    /// <summary>Position error at given percentile (0-100). Returns 0 if no frames.</summary>
    public float PositionErrorPercentile(int percentile)
    {
        if (FrameDetails.Count == 0) return 0;
        var sorted = FrameDetails.Select(f => f.PosError).OrderBy(e => e).ToList();
        int idx = Math.Clamp((int)(sorted.Count * percentile / 100.0), 0, sorted.Count - 1);
        return sorted[idx];
    }

    public float P50 => PositionErrorPercentile(50);
    public float P95 => PositionErrorPercentile(95);
    public float P99 => PositionErrorPercentile(99);

    /// <summary>
    /// Clean frames: excludes recording artifacts and SPLINE_ELEVATION transitions.
    /// </summary>
    public List<FrameDetail> CleanFrames => FrameDetails.Where(f => !f.IsRecordingArtifact && !f.IsSplineElevationTransition).ToList();
    public int ArtifactCount => FrameDetails.Count(f => f.IsRecordingArtifact);
    public int SplineElevationCount => FrameDetails.Count(f => f.IsSplineElevationTransition);

    /// <summary>
    /// Steady-state frames: excludes transition frames where movement flags change.
    /// </summary>
    public List<FrameDetail> SteadyStateFrames => FrameDetails.Where(f => !f.IsFlagTransition).ToList();
    public int SteadyStateCount => SteadyStateFrames.Count;
    public float SteadyStateAvg => SteadyStateFrames.Count > 0 ? SteadyStateFrames.Average(f => f.PosError) : 0;
    public float SteadyStateP99
    {
        get
        {
            var frames = SteadyStateFrames;
            if (frames.Count == 0) return 0;
            var sorted = frames.Select(f => f.PosError).OrderBy(e => e).ToList();
            int idx = Math.Clamp((int)(sorted.Count * 99 / 100.0), 0, sorted.Count - 1);
            return sorted[idx];
        }
    }

    /// <summary>
    /// Get frames of a specific transition type.
    /// </summary>
    public List<FrameDetail> GetTransitionFrames(TransitionType type) =>
        FrameDetails.Where(f => f.Transition == type).ToList();

    /// <summary>
    /// Compute error stats for a specific transition type.
    /// Returns (count, avgError, maxError, p99Error).
    /// </summary>
    public (int count, float avg, float max, float p99) TransitionStats(TransitionType type)
    {
        var frames = GetTransitionFrames(type);
        if (frames.Count == 0) return (0, 0, 0, 0);

        float avg = frames.Average(f => f.PosError);
        float max = frames.Max(f => f.PosError);
        var sorted = frames.Select(f => f.PosError).OrderBy(e => e).ToList();
        int idx = Math.Clamp((int)(sorted.Count * 99 / 100.0), 0, sorted.Count - 1);
        return (frames.Count, avg, max, sorted[idx]);
    }

    /// <summary>
    /// Compute error stats for on-transport frames specifically.
    /// </summary>
    public (int count, float avg, float max, float p99) OnTransportStats()
    {
        var frames = FrameDetails.Where(f => f.IsOnTransport).ToList();
        if (frames.Count == 0) return (0, 0, 0, 0);

        float avg = frames.Average(f => f.PosError);
        float max = frames.Max(f => f.PosError);
        var sorted = frames.Select(f => f.PosError).OrderBy(e => e).ToList();
        int idx = Math.Clamp((int)(sorted.Count * 99 / 100.0), 0, sorted.Count - 1);
        return (frames.Count, avg, max, sorted[idx]);
    }

    public void AddFrame(FrameDetail detail)
    {
        FrameDetails.Add(detail);

        if (detail.PosError > MaxPositionError)
        {
            MaxPositionError = detail.PosError;
            WorstFrame = FrameDetails.Count - 1;
        }
    }

    public void AddSkippedTeleport(int frame) => TeleportCount++;
    public void AddSkippedTransportTransition(int frame) => TransportTransitionCount++;
    public void AddSkippedTransportFrame(int frame) => TransportFrameCount++;
    public void AddSimulatedTransportFrame() => TransportSimulatedCount++;

    public record FrameDetail
    {
        public int Frame;
        public float PosError, HorizError, VertError;
        public float SimX, SimY, SimZ;
        public float RecX, RecY, RecZ;

        // Movement context for diagnostic analysis
        public uint MoveFlags;         // Cleaned flags (SPLINE_ELEVATION/TELEPORT stripped)
        public uint NextMoveFlags;     // Cleaned flags for next frame
        public uint RawMoveFlags;      // Original un-cleaned flags
        public uint RawNextMoveFlags;  // Original un-cleaned next frame flags
        public float Dt;
        public float Orientation;
        public float RecordedSpeed;
        public float EngineGroundZ;
        public float EngineVx, EngineVy, EngineVz;
        public float InputVx, InputVy, InputVz;
        public bool IsSwimming;
        public bool IsAirborne;
        public bool IsFlagTransition;
        public bool IsOnTransport;
        public float SwimPitch;
        public TransitionType Transition;
        public string RecordingName = "";

        /// <summary>True if this frame is affected by a one-frame Z spike artifact in the recording data.</summary>
        public bool IsRecordingArtifact;
        /// <summary>True if SPLINE_ELEVATION (0x04000000) toggled between current and next frame.</summary>
        public bool IsSplineElevationTransition;

        public float ErrorX => SimX - RecX;
        public float ErrorY => SimY - RecY;
        public float ErrorZ => SimZ - RecZ;

        public string MovementMode
        {
            get
            {
                if (IsOnTransport) return "transport";
                if (IsSwimming) return "swim";
                if (IsAirborne) return "air";
                if (IsFlagTransition) return "transition";
                return "ground";
            }
        }
    }
}
