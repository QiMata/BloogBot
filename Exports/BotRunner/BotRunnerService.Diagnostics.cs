using Communication;
using GameData.Core.Enums;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        /// <summary>
        /// Well-known directory for physics frame recordings.
        /// Tests read from here after stopping recording.
        /// </summary>
        private static readonly string PhysicsRecordingDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WWoW", "PhysicsRecordings");

        // ── Transform recording (works for BOTH FG and BG) ──
        // FG: captures gold-standard position from WoW's memory each tick.
        // BG: captures final position after MovementController/Physics each tick.
        // BG also has the detailed physics CSV (guards, raw Z, etc.) via WoWSharpObjectManager.
        private bool _isTransformRecording;
        private readonly List<TransformFrame> _transformFrames = new();
        private readonly Stopwatch _transformStopwatch = new();
        private int _transformFrameNumber;

        /// <summary>
        /// Per-frame transform snapshot captured from IObjectManager.Player.
        /// Shared format for FG (gold standard) and BG (physics output).
        /// </summary>
        private record TransformFrame(
            int Frame,
            long ElapsedMs,
            float PosX, float PosY, float PosZ,
            float Facing,
            uint MoveFlags,
            float RunSpeed,
            uint FallTime
        );

        /// <summary>
        /// Handle diagnostic action types that don't map to CharacterAction.
        /// Returns true if the action was handled (caller should return early).
        /// </summary>
        private bool HandleDiagnosticAction(ActionMessage action)
        {
            switch (action.ActionType)
            {
                case ActionType.StartPhysicsRecording:
                    StartPhysicsRecording();
                    StartTransformRecording();
                    return true;

                case ActionType.StopPhysicsRecording:
                    StopPhysicsRecording();
                    StopTransformRecording();
                    return true;

                default:
                    return false;
            }
        }

        // ── BG-specific physics frame recording (detailed guards, raw Z, etc.) ──

        private void StartPhysicsRecording()
        {
            if (_objectManager is WoWSharpClient.WoWSharpObjectManager wsOm)
            {
                wsOm.ClearPhysicsFrameRecording();
                wsOm.IsPhysicsRecording = true;
                Log.Information("[DIAG] Physics frame recording STARTED");
            }
        }

        private void StopPhysicsRecording()
        {
            if (_objectManager is not WoWSharpClient.WoWSharpObjectManager wsOm)
                return;

            wsOm.IsPhysicsRecording = false;
            var frames = wsOm.GetPhysicsFrameRecording();
            Log.Information("[DIAG] Physics frame recording STOPPED — {Count} frames captured", frames.Count);

            if (frames.Count == 0) return;

            // Write frames to CSV in well-known location
            Directory.CreateDirectory(PhysicsRecordingDir);
            var accountName = _activitySnapshot?.AccountName ?? "unknown";
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var filePath = Path.Combine(PhysicsRecordingDir, $"physics_{accountName}_{timestamp}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("Frame,GameTimeMs,DeltaSec,PosX,PosY,PosZ,RawPosZ,PhysicsGroundZ,PrevGroundZ," +
                "HasGroundContact,VelX,VelY,VelZ,FallTimeMs,IsFalling,MoveFlags," +
                "SlopeGuard,PathGuard,FalseFreefallSup,TeleportClamp,UndergroundSnap," +
                "HitWall,WallNX,WallNY,BlockedFrac,PathWpZ,PathWpIdx,ZDelta");

            foreach (var f in frames)
            {
                sb.Append(f.FrameNumber).Append(',');
                sb.Append(f.GameTimeMs).Append(',');
                sb.Append(f.DeltaSec.ToString("F4", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.PosX.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.PosY.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.PosZ.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.RawPosZ.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.PhysicsGroundZ.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.PrevGroundZ.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.HasPhysicsGroundContact ? '1' : '0').Append(',');
                sb.Append(f.VelX.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.VelY.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.VelZ.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.FallTimeMs).Append(',');
                sb.Append(f.IsFalling ? '1' : '0').Append(',');
                sb.Append($"0x{f.MovementFlags:X}").Append(',');
                sb.Append(f.SlopeGuardRejected ? '1' : '0').Append(',');
                sb.Append(f.PathGroundGuardActive ? '1' : '0').Append(',');
                sb.Append(f.FalseFreefallSuppressed ? '1' : '0').Append(',');
                sb.Append(f.TeleportClampActive ? '1' : '0').Append(',');
                sb.Append(f.UndergroundSnapFired ? '1' : '0').Append(',');
                sb.Append(f.HitWall ? '1' : '0').Append(',');
                sb.Append(f.WallNormalX.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.WallNormalY.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.BlockedFraction.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(float.IsNaN(f.PathWaypointZ) ? "NaN" : f.PathWaypointZ.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.PathWaypointIndex).Append(',');
                sb.AppendLine(f.ZDeltaFromPrev.ToString("F4", CultureInfo.InvariantCulture));
            }

            File.WriteAllText(filePath, sb.ToString());
            Log.Information("[DIAG] Physics recording written to {Path} ({Count} frames)", filePath, frames.Count);
        }

        // ── Generic transform recording (FG gold standard + BG final position) ──

        private void StartTransformRecording()
        {
            _transformFrames.Clear();
            _transformFrameNumber = 0;
            _transformStopwatch.Restart();
            _isTransformRecording = true;
            Log.Information("[DIAG] Transform recording STARTED");
        }

        private void StopTransformRecording()
        {
            _isTransformRecording = false;
            _transformStopwatch.Stop();
            Log.Information("[DIAG] Transform recording STOPPED — {Count} frames captured", _transformFrames.Count);

            if (_transformFrames.Count == 0) return;

            Directory.CreateDirectory(PhysicsRecordingDir);
            var accountName = _activitySnapshot?.AccountName ?? "unknown";
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var filePath = Path.Combine(PhysicsRecordingDir, $"transform_{accountName}_{timestamp}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("Frame,ElapsedMs,PosX,PosY,PosZ,Facing,MoveFlags,RunSpeed,FallTime");

            foreach (var f in _transformFrames)
            {
                sb.Append(f.Frame).Append(',');
                sb.Append(f.ElapsedMs).Append(',');
                sb.Append(f.PosX.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.PosY.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.PosZ.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(f.Facing.ToString("F4", CultureInfo.InvariantCulture)).Append(',');
                sb.Append($"0x{f.MoveFlags:X}").Append(',');
                sb.Append(f.RunSpeed.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
                sb.AppendLine(f.FallTime.ToString());
            }

            File.WriteAllText(filePath, sb.ToString());
            Log.Information("[DIAG] Transform recording written to {Path} ({Count} frames)", filePath, _transformFrames.Count);
        }

        /// <summary>
        /// Called every tick from the main bot loop. Captures player transform if recording.
        /// </summary>
        internal void CaptureTransformFrame()
        {
            if (!_isTransformRecording) return;

            var player = _objectManager?.Player;
            if (player == null) return;

            var pos = player.Position;
            _transformFrames.Add(new TransformFrame(
                Frame: _transformFrameNumber++,
                ElapsedMs: _transformStopwatch.ElapsedMilliseconds,
                PosX: pos.X,
                PosY: pos.Y,
                PosZ: pos.Z,
                Facing: player.Facing,
                MoveFlags: (uint)player.MovementFlags,
                RunSpeed: player.RunSpeed,
                FallTime: player.FallTime
            ));
        }
    }
}
