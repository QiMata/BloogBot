using Communication;
using Serilog;
using System;
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
                    return true;

                case ActionType.StopPhysicsRecording:
                    StopPhysicsRecording();
                    return true;

                default:
                    return false;
            }
        }

        private void StartPhysicsRecording()
        {
            if (_objectManager is WoWSharpClient.WoWSharpObjectManager wsOm)
            {
                wsOm.ClearPhysicsFrameRecording();
                wsOm.IsPhysicsRecording = true;
                Log.Information("[DIAG] Physics frame recording STARTED");
            }
            else
            {
                Log.Warning("[DIAG] Physics recording not available (not WoWSharpObjectManager)");
            }
        }

        private void StopPhysicsRecording()
        {
            if (_objectManager is not WoWSharpClient.WoWSharpObjectManager wsOm)
            {
                Log.Warning("[DIAG] Physics recording not available (not WoWSharpObjectManager)");
                return;
            }

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
    }
}
