using System;
using System.Collections.Generic;
using Communication;
using Game;

namespace BotRunner.Tests.LiveValidation.Harness;

/// <summary>
/// PFS-OVERHAUL-006 (2026-05-10) — quick-fail signals that complement
/// <c>SnapshotStallGuard</c> with shorter fuses than the existing
/// XY-1.5y/45s stall catcher. Each guard is independent and idempotent
/// — compose multiple per check to detect distinct failure modes
/// (ledge fall vs. off-path drift vs. unexplained Z runaway).
///
/// All guards use the same callback signature as
/// <c>SnapshotStallGuard.FailIfStalled</c> so the existing
/// <c>WaitForSnapshotConditionAsync</c> bodies can compose them by
/// calling each guard's <c>FailIfStalled</c> in sequence.
/// </summary>

/// <summary>
/// Lightweight 2D path-geometry helpers used by <see cref="OffPathGuard"/>
/// and the <c>WaypointSettleValidator</c>. Allocation-free per call.
/// </summary>
public static class PathGeometry
{
    public readonly record struct Point2D(float X, float Y);

    /// <summary>
    /// Returns the minimum 2D distance from <c>(x, y)</c> to the polyline
    /// defined by <paramref name="path"/>. The distance is computed against
    /// every segment with point-to-segment projection (clamped to segment
    /// endpoints). Returns <see cref="float.PositiveInfinity"/> if the path
    /// is empty.
    /// </summary>
    public static float NearestPointDistance2D(float x, float y, IReadOnlyList<Point2D> path)
    {
        if (path == null || path.Count == 0) return float.PositiveInfinity;
        if (path.Count == 1)
        {
            var dx = x - path[0].X;
            var dy = y - path[0].Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        var minSqr = float.PositiveInfinity;
        for (int i = 0; i < path.Count - 1; i++)
        {
            var d = SegmentDistanceSqr(x, y, path[i], path[i + 1]);
            if (d < minSqr) minSqr = d;
        }
        return MathF.Sqrt(minSqr);
    }

    private static float SegmentDistanceSqr(float px, float py, Point2D a, Point2D b)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var apx = px - a.X;
        var apy = py - a.Y;
        var ab2 = abx * abx + aby * aby;
        var t = ab2 > 1e-6f ? (apx * abx + apy * aby) / ab2 : 0f;
        if (t < 0f) t = 0f;
        else if (t > 1f) t = 1f;
        var cx = a.X + t * abx;
        var cy = a.Y + t * aby;
        var dx = px - cx;
        var dy = py - cy;
        return dx * dx + dy * dy;
    }
}

/// <summary>
/// Detects unintentional ledge falls — a step-down between consecutive
/// snapshots greater than <paramref name="ledgeFallThresholdY"/> while the
/// player is neither jumping nor on a transport. Unlike the XY-stall
/// guard's 45s fuse, this fires within one snapshot-pair (typically ~1-2s
/// at ~500ms poll cadence). Mirrors the WoW convention that
/// <c>MovementFlags &amp; 0x2000</c> indicates JUMPING and a non-zero
/// <c>TransportGuid</c> indicates the player is riding a moveable
/// platform — falls in those states are legitimate.
/// </summary>
public sealed class LedgeFallGuard
{
    private const uint MovementFlagJumping = 0x2000u;

    private readonly string _label;
    private readonly float _ledgeFallThresholdY;
    private readonly Func<DateTime> _clock;
    private readonly TimeSpan _confirmationWindow;

    private bool _hasPrevious;
    private uint _previousMapId;
    private float _previousZ;
    private bool _falling;
    private DateTime _fallingSinceUtc;

    public LedgeFallGuard(
        string label,
        float ledgeFallThresholdY = 5f,
        TimeSpan? confirmationWindow = null,
        Func<DateTime>? clock = null)
    {
        _label = label;
        _ledgeFallThresholdY = ledgeFallThresholdY;
        _clock = clock ?? (() => DateTime.UtcNow);
        _confirmationWindow = confirmationWindow ?? TimeSpan.Zero;
    }

    public void Reset()
    {
        _hasPrevious = false;
        _falling = false;
    }

    public void FailIfStalled(WoWActivitySnapshot snapshot, Action<string, WoWActivitySnapshot?> fail)
    {
        var pos = snapshot?.MovementData?.Position;
        if (pos == null) { Reset(); return; }

        var mapId = snapshot!.CurrentMapId;
        var nowUtc = _clock();

        if (!_hasPrevious || _previousMapId != mapId)
        {
            _hasPrevious = true;
            _previousMapId = mapId;
            _previousZ = pos.Z;
            _falling = false;
            return;
        }

        var jumping = (snapshot.MovementData?.MovementFlags ?? 0u) & MovementFlagJumping;
        var onTransport = (snapshot.MovementData?.TransportGuid ?? 0ul) != 0ul;

        if (jumping != 0u || onTransport)
        {
            _previousZ = pos.Z;
            _falling = false;
            return;
        }

        var dz = pos.Z - _previousZ;

        if (dz <= -_ledgeFallThresholdY)
        {
            if (!_falling)
            {
                _falling = true;
                _fallingSinceUtc = nowUtc;
            }

            if (nowUtc - _fallingSinceUtc >= _confirmationWindow)
            {
                fail(
                    $"LedgeFall {_label}: dz={dz:F2}y exceeds threshold {_ledgeFallThresholdY:F2}y; " +
                    $"map={mapId} pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1}) prevZ={_previousZ:F1} " +
                    $"flags=0x{snapshot.MovementData?.MovementFlags ?? 0:X} transport=0x{snapshot.MovementData?.TransportGuid ?? 0:X}.",
                    snapshot);
                _falling = false;
            }
            return;
        }

        _falling = false;
        _previousZ = pos.Z;
    }
}

/// <summary>
/// Detects sustained off-path drift — the bot's <c>(x, y)</c> position is
/// further than <paramref name="offPathThresholdY"/> from the nearest point
/// on the planned smooth path for at least <paramref name="dwellTimeout"/>.
/// Use the live route's smooth-path waypoint XY pairs to seed the guard.
/// </summary>
public sealed class OffPathGuard
{
    private readonly string _label;
    private readonly IReadOnlyList<PathGeometry.Point2D> _path;
    private readonly float _offPathThresholdY;
    private readonly TimeSpan _dwellTimeout;
    private readonly Func<DateTime> _clock;

    private bool _isOff;
    private DateTime _firstOffUtc;
    private uint _anchorMapId;

    public OffPathGuard(
        string label,
        IReadOnlyList<PathGeometry.Point2D> path,
        float offPathThresholdY = 10f,
        TimeSpan? dwellTimeout = null,
        Func<DateTime>? clock = null)
    {
        _label = label;
        _path = path ?? Array.Empty<PathGeometry.Point2D>();
        _offPathThresholdY = offPathThresholdY;
        _dwellTimeout = dwellTimeout ?? TimeSpan.FromSeconds(2);
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public void Reset()
    {
        _isOff = false;
    }

    public void FailIfStalled(WoWActivitySnapshot snapshot, Action<string, WoWActivitySnapshot?> fail)
    {
        if (_path.Count == 0) return;

        var pos = snapshot?.MovementData?.Position;
        if (pos == null) { Reset(); return; }

        var mapId = snapshot!.CurrentMapId;
        var nowUtc = _clock();
        var distance = PathGeometry.NearestPointDistance2D(pos.X, pos.Y, _path);

        // Map change resets the guard — paths are per-map.
        if (_isOff && _anchorMapId != mapId)
        {
            _isOff = false;
        }

        if (distance <= _offPathThresholdY)
        {
            _isOff = false;
            return;
        }

        if (!_isOff)
        {
            _isOff = true;
            _anchorMapId = mapId;
            _firstOffUtc = nowUtc;
            return;
        }

        if (nowUtc - _firstOffUtc < _dwellTimeout) return;

        fail(
            $"OffPath {_label}: bot {distance:F2}y from nearest smooth-path point for " +
            $"{(nowUtc - _firstOffUtc).TotalSeconds:F1}s (threshold {_offPathThresholdY:F2}y); " +
            $"map={mapId} pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1}).",
            snapshot);
        _isOff = false;
    }
}

/// <summary>
/// Detects unexplained cumulative Z displacement — sums |Δz| between
/// consecutive snapshots and fires when the running total exceeds
/// <paramref name="zBudgetYards"/>. Callers compute the expected Z budget
/// for the route by summing the magnitudes of legitimate drops/climbs from
/// the path's affordance summary (JumpGap/Drop/SafeDrop legs) and adding a
/// slack margin (e.g., 30y default for stair-stepping noise).
///
/// Resets on map change or explicit <see cref="MarkLegitimateZTransition"/>
/// — for example, when an off-mesh-link traversal or a flight-master
/// teleport injects a known-good Z displacement that would otherwise blow
/// the budget.
/// </summary>
public sealed class ZRunawayGuard
{
    private readonly string _label;
    private readonly float _zBudgetYards;
    private bool _hasPrevious;
    private uint _previousMapId;
    private float _previousZ;
    private float _accumulated;

    public ZRunawayGuard(string label, float zBudgetYards = 30f)
    {
        _label = label;
        _zBudgetYards = zBudgetYards;
    }

    public float Accumulated => _accumulated;

    public void Reset()
    {
        _hasPrevious = false;
        _accumulated = 0f;
    }

    /// <summary>
    /// Called by the test caller when it observes a legitimate Z transition
    /// (off-mesh link traversal, teleport, summon). Subtracts the absolute
    /// magnitude from the accumulated total; clamps at zero.
    /// </summary>
    public void MarkLegitimateZTransition(float legitimateAbsDz)
    {
        if (legitimateAbsDz <= 0f) return;
        _accumulated = MathF.Max(0f, _accumulated - legitimateAbsDz);
    }

    public void FailIfStalled(WoWActivitySnapshot snapshot, Action<string, WoWActivitySnapshot?> fail)
    {
        var pos = snapshot?.MovementData?.Position;
        if (pos == null) { Reset(); return; }

        var mapId = snapshot!.CurrentMapId;

        if (!_hasPrevious || _previousMapId != mapId)
        {
            _hasPrevious = true;
            _previousMapId = mapId;
            _previousZ = pos.Z;
            _accumulated = 0f;
            return;
        }

        var dz = pos.Z - _previousZ;
        _accumulated += MathF.Abs(dz);
        _previousZ = pos.Z;

        if (_accumulated <= _zBudgetYards) return;

        fail(
            $"ZRunaway {_label}: cumulative |Σdz|={_accumulated:F2}y exceeded budget {_zBudgetYards:F2}y; " +
            $"map={mapId} pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1}). " +
            $"Either the route hits unaccounted-for drops/climbs or the bot is bouncing on a phantom polygon.",
            snapshot);
        // After firing, reset so a single test run can observe multiple breaches.
        _accumulated = 0f;
    }
}
