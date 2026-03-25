using GameData.Core.Enums;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using WoWSharpClient.Models;

namespace WoWSharpClient.Movement
{
    /// <summary>Immutable data copied straight from SMSG_MONSTER_MOVE.</summary>
    public sealed class Spline(ulong owner, uint id, uint t0, SplineFlags flags,
                  IReadOnlyList<Position> pts, uint durationMs)
    {
        public ulong OwnerGuid { get; } = owner;
        public uint Id { get; } = id;
        public uint StartMs { get; } = t0;
        public SplineFlags Flags { get; } = flags;
        public IReadOnlyList<Position> Points { get; } = pts;
        public uint DurationMs { get; } = durationMs;

        internal float SegmentMs => (Points.Count <= 1) ? 0 : DurationMs / (float)(Points.Count - 1);
    }

    /// <summary>Per-tick state machine that walks along one spline.</summary>
    internal sealed class ActiveSpline(Spline s)
    {
        public Spline Spline { get; } = s;
        private float _elapsed;   // ms since spline start
        private int _seg;         // current segment index
        private Position? _lastPosition;

        public ActiveSpline(Spline s, uint? currentTimeMs)
            : this(s)
        {
            if (!currentTimeMs.HasValue || s.StartMs == 0 || s.DurationMs == 0 || s.Points.Count == 0
                || currentTimeMs.Value < s.StartMs)
                return;

            uint absoluteElapsedMs = currentTimeMs.Value - s.StartMs;
            _elapsed = NormalizeElapsed(absoluteElapsedMs);
            RecalculateSegment();
            _lastPosition = EvaluateCurrentPosition();
        }

        /// <summary>Advance <paramref name="dtMs"/> and return the new position.</summary>
        public Position Step(float dtMs)
        {
            // Frozen splines (0x400000): halt at current position, never advance.
            // WoW.exe uses this for stunned/rooted NPCs with active splines.
            if (Spline.Flags.HasFlag(SplineFlags.Frozen))
                return _lastPosition ?? Spline.Points[0];

            _elapsed += dtMs;

            // Cyclic splines (0x100000): wrap back to start instead of finishing.
            // Used for NPC patrol routes that loop continuously.
            if (Spline.Flags.HasFlag(SplineFlags.Cyclic) && Spline.DurationMs > 0)
            {
                _elapsed = NormalizeElapsed(_elapsed);
            }
            else if (Spline.DurationMs > 0 && _elapsed > Spline.DurationMs)
            {
                _elapsed = Spline.DurationMs;
            }

            RecalculateSegment();

            if (_seg + 1 >= Spline.Points.Count)
            {
                _lastPosition = Spline.Points[^1];
                return _lastPosition;
            }

            _lastPosition = EvaluateSegmentPosition(_elapsed, _seg);
            return _lastPosition;
        }

        public bool Finished => !Spline.Flags.HasFlag(SplineFlags.Cyclic)
            && _seg + 1 >= Spline.Points.Count;

        private float NormalizeElapsed(float absoluteElapsedMs)
        {
            if (Spline.DurationMs == 0)
                return 0f;

            if (!Spline.Flags.HasFlag(SplineFlags.Cyclic))
                return MathF.Min(absoluteElapsedMs, Spline.DurationMs);

            if (absoluteElapsedMs <= 0f)
                return 0f;

            float remainder = absoluteElapsedMs % Spline.DurationMs;
            return remainder == 0f ? Spline.DurationMs : remainder;
        }

        private void RecalculateSegment()
        {
            _seg = 0;
            while (_seg + 1 < Spline.Points.Count &&
                   _elapsed >= (_seg + 1) * Spline.SegmentMs)
            {
                _seg++;
            }
        }

        private Position EvaluateCurrentPosition()
        {
            if (Spline.Points.Count == 0)
                return new Position(0f, 0f, 0f);

            if (Spline.Points.Count == 1 || _seg + 1 >= Spline.Points.Count)
                return Spline.Points[^1];

            return EvaluateSegmentPosition(_elapsed, _seg);
        }

        private Position EvaluateSegmentPosition(float elapsedMs, int segmentIndex)
        {
            float u = (elapsedMs - segmentIndex * Spline.SegmentMs) / Spline.SegmentMs;

            // Flying splines (0x200): use Catmull-Rom for smooth curves.
            // WoW.exe SplineStep at 0x7C5360 dispatches to multiple spline evaluators;
            // Vanilla MONSTER_MOVE reaches the smooth path through the Flying/Catmull-Rom flag.
            if (Spline.Flags.HasFlag(SplineFlags.Flying) && Spline.Points.Count >= 4)
            {
                var p0 = GetCatmullRomPoint(segmentIndex - 1);
                var p1 = GetCatmullRomPoint(segmentIndex);
                var p2 = GetCatmullRomPoint(segmentIndex + 1);
                var p3 = GetCatmullRomPoint(segmentIndex + 2);
                return CatmullRom(p0, p1, p2, p3, u);
            }

            var a = Spline.Points[segmentIndex];
            var b = Spline.Points[segmentIndex + 1];
            return new Position(
                a.X + (b.X - a.X) * u,
                a.Y + (b.Y - a.Y) * u,
                a.Z + (b.Z - a.Z) * u
            );
        }

        private Position GetCatmullRomPoint(int index)
        {
            if (!Spline.Flags.HasFlag(SplineFlags.Cyclic))
            {
                int clampedIndex = Math.Clamp(index, 0, Spline.Points.Count - 1);
                return Spline.Points[clampedIndex];
            }

            int uniquePointCount = GetUniqueCyclicPointCount();
            if (uniquePointCount <= 0)
            {
                int clampedIndex = Math.Clamp(index, 0, Spline.Points.Count - 1);
                return Spline.Points[clampedIndex];
            }

            int wrappedIndex = ((index % uniquePointCount) + uniquePointCount) % uniquePointCount;
            return Spline.Points[wrappedIndex];
        }

        private int GetUniqueCyclicPointCount()
        {
            if (Spline.Points.Count == 0)
                return 0;

            if (Spline.Points.Count > 1 && PositionsApproximatelyEqual(Spline.Points[0], Spline.Points[^1]))
                return Spline.Points.Count - 1;

            return Spline.Points.Count;
        }

        private static bool PositionsApproximatelyEqual(Position left, Position right) =>
            MathF.Abs(left.X - right.X) <= 0.01f
            && MathF.Abs(left.Y - right.Y) <= 0.01f
            && MathF.Abs(left.Z - right.Z) <= 0.01f;

        /// <summary>
        /// Catmull-Rom cubic spline interpolation.
        /// P(t) = 0.5 * ((2*P1) + (-P0+P2)*t + (2*P0-5*P1+4*P2-P3)*t² + (-P0+3*P1-3*P2+P3)*t³)
        /// </summary>
        private static Position CatmullRom(Position p0, Position p1, Position p2, Position p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return new Position(
                0.5f * (2*p1.X + (-p0.X+p2.X)*t + (2*p0.X-5*p1.X+4*p2.X-p3.X)*t2 + (-p0.X+3*p1.X-3*p2.X+p3.X)*t3),
                0.5f * (2*p1.Y + (-p0.Y+p2.Y)*t + (2*p0.Y-5*p1.Y+4*p2.Y-p3.Y)*t2 + (-p0.Y+3*p1.Y-3*p2.Y+p3.Y)*t3),
                0.5f * (2*p1.Z + (-p0.Z+p2.Z)*t + (2*p0.Z-5*p1.Z+4*p2.Z-p3.Z)*t2 + (-p0.Z+3*p1.Z-3*p2.Z+p3.Z)*t3)
            );
        }
    }

    /// <summary>Central registry that drives every active spline each frame.</summary>
    public sealed class SplineController
    {
        private readonly Dictionary<ulong, ActiveSpline> _active = [];
        private readonly object _gate = new();

        /// <summary>Fired when a spline for the given GUID completes or is removed.</summary>
        public event Action<ulong>? OnSplineCompleted;

        public void AddOrUpdate(Spline s, uint? currentTimeMs = null)
        {
            lock (_gate)
            {
                _active[s.OwnerGuid] = currentTimeMs.HasValue
                    ? new ActiveSpline(s, currentTimeMs)
                    : new ActiveSpline(s);
            }
            Log.Information("[SplineController] Added spline for {Guid:X}: {Points} pts, {Duration}ms, flags=0x{Flags:X}",
                s.OwnerGuid, s.Points.Count, s.DurationMs, (uint)s.Flags);
        }

        public void Remove(ulong guid)
        {
            bool removed;
            lock (_gate)
            {
                removed = _active.Remove(guid);
            }

            if (removed)
            {
                Log.Information("[SplineController] Removed spline for {Guid:X}", guid);
                OnSplineCompleted?.Invoke(guid);
            }
        }

        /// <summary>Returns true if the given GUID has an active (non-finished) spline.</summary>
        public bool HasActiveSpline(ulong guid)
        {
            lock (_gate)
            {
                return _active.ContainsKey(guid);
            }
        }

        public void Update(float dtMs)
        {
            KeyValuePair<ulong, ActiveSpline>[] snapshot;
            lock (_gate)
            {
                snapshot = _active.ToArray();
            }

            foreach (var (guid, active) in snapshot)
            {
                if (!IsCurrentSnapshotEntry(guid, active))
                    continue;

                if (active.Finished)
                {
                    if (TryRemoveSnapshotEntry(guid, active))
                    {
                        Log.Information("[SplineController] Spline finished for {Guid:X}", guid);
                        OnSplineCompleted?.Invoke(guid);
                    }
                    continue;
                }

                // Look up the unit — check Player first (not in Objects collection), then Objects
                WoWUnit? woWUnit = null;
                var player = WoWSharpObjectManager.Instance.Player;
                if (player != null && player.Guid == guid)
                    woWUnit = (WoWUnit)player;
                else
                    woWUnit = WoWSharpObjectManager.Instance.Objects.OfType<WoWUnit>().FirstOrDefault(x => x.Guid == guid);

                if (woWUnit != null)
                {
                    var previousSplinePosition = woWUnit.TransportGuid != 0
                        ? new Position(woWUnit.TransportOffset.X, woWUnit.TransportOffset.Y, woWUnit.TransportOffset.Z)
                        : new Position(woWUnit.Position.X, woWUnit.Position.Y, woWUnit.Position.Z);
                    var nextSplinePosition = active.Step(dtMs);
                    var nextFacing = ResolveFacing(woWUnit, previousSplinePosition, nextSplinePosition);

                    if (woWUnit.TransportGuid != 0)
                    {
                        woWUnit.TransportOffset = nextSplinePosition;
                        woWUnit.TransportOrientation = nextFacing;
                        WoWSharpObjectManager.Instance.SyncTransportPassengerWorldPosition(woWUnit);
                    }
                    else
                    {
                        woWUnit.Position = nextSplinePosition;
                        woWUnit.Facing = nextFacing;
                    }
                }
                else if (WoWSharpObjectManager.Instance.GetObjectByGuid(guid) is WoWGameObject gameObject)
                {
                    var previousSplinePosition = new Position(
                        gameObject.Position.X,
                        gameObject.Position.Y,
                        gameObject.Position.Z);
                    var nextSplinePosition = active.Step(dtMs);
                    var nextFacing = ResolveFacing(gameObject, previousSplinePosition, nextSplinePosition);
                    gameObject.Position = nextSplinePosition;
                    gameObject.Facing = nextFacing;
                    WoWSharpObjectManager.Instance.SyncTransportPassengerWorldPositions();
                }
                else
                    TryRemoveSnapshotEntry(guid, active); // object vanished
            }
        }

        private bool IsCurrentSnapshotEntry(ulong guid, ActiveSpline active)
        {
            lock (_gate)
            {
                return _active.TryGetValue(guid, out var current) && ReferenceEquals(current, active);
            }
        }

        private bool TryRemoveSnapshotEntry(ulong guid, ActiveSpline active)
        {
            lock (_gate)
            {
                if (_active.TryGetValue(guid, out var current) && ReferenceEquals(current, active))
                    return _active.Remove(guid);

                return false;
            }
        }

        internal static float ResolveFacing(WoWUnit unit, Position previousPosition, Position nextPosition)
        {
            switch (unit.SplineType)
            {
                case SplineType.FacingAngle:
                    return TransportCoordinateHelper.NormalizeFacing(unit.FacingAngle);
                case SplineType.FacingSpot:
                    return FaceTowards(nextPosition, unit.FacingSpot, unit.Facing);
                case SplineType.FacingTarget:
                    if (WoWSharpObjectManager.Instance.GetObjectByGuid(unit.SplineTargetGuid) is WoWObject target)
                        return FaceTowards(nextPosition, target.Position, unit.Facing);
                    return unit.Facing;
                case SplineType.Stop:
                    return unit.Facing;
                default:
                    return FaceTowards(previousPosition, nextPosition, unit.Facing);
            }
        }

        internal static float ResolveFacing(WoWGameObject gameObject, Position previousPosition, Position nextPosition)
        {
            switch (gameObject.MovementSplineType)
            {
                case SplineType.FacingAngle:
                    return TransportCoordinateHelper.NormalizeFacing(gameObject.MovementFacingAngle);
                case SplineType.FacingSpot:
                    return FaceTowards(nextPosition, gameObject.MovementFacingSpot, gameObject.Facing);
                case SplineType.FacingTarget:
                    if (WoWSharpObjectManager.Instance.GetObjectByGuid(gameObject.MovementSplineTargetGuid) is WoWObject target)
                        return FaceTowards(nextPosition, target.Position, gameObject.Facing);
                    return gameObject.Facing;
                case SplineType.Stop:
                    return gameObject.Facing;
                default:
                    return FaceTowards(previousPosition, nextPosition, gameObject.Facing);
            }
        }

        private static float FaceTowards(Position origin, Position target, float fallbackFacing)
        {
            float dx = target.X - origin.X;
            float dy = target.Y - origin.Y;
            if (MathF.Abs(dx) < 0.0001f && MathF.Abs(dy) < 0.0001f)
                return fallbackFacing;

            return TransportCoordinateHelper.NormalizeFacing(MathF.Atan2(dy, dx));
        }
    }

    /// <summary>Global helper (lazy-init singleton).</summary>
    public static class Splines
    {
        public static readonly SplineController Instance = new();
    }
}
