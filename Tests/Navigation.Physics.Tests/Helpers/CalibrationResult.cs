namespace Navigation.Physics.Tests.Helpers;

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

    public void AddFrame(int frame, float posError, float horizError, float vertError,
        float simX, float simY, float simZ, float recX, float recY, float recZ)
    {
        FrameDetails.Add(new FrameDetail
        {
            Frame = frame,
            PosError = posError,
            HorizError = horizError,
            VertError = vertError,
            SimX = simX, SimY = simY, SimZ = simZ,
            RecX = recX, RecY = recY, RecZ = recZ
        });

        if (posError > MaxPositionError)
        {
            MaxPositionError = posError;
            WorstFrame = FrameDetails.Count - 1;
        }
    }

    public void AddSkippedTeleport(int frame) => TeleportCount++;

    public record FrameDetail
    {
        public int Frame;
        public float PosError, HorizError, VertError;
        public float SimX, SimY, SimZ;
        public float RecX, RecY, RecZ;
    }
}
