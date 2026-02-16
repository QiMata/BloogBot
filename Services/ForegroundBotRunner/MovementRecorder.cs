using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Statics;
using Game;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

// Aliases to avoid ambiguity with Game.* protobuf types
using LocalWoWUnit = ForegroundBotRunner.Objects.WoWUnit;
using LocalWoWGameObject = ForegroundBotRunner.Objects.WoWGameObject;
using Race = GameData.Core.Enums.Race;
using Gender = GameData.Core.Enums.Gender;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;
using System.IO;

namespace ForegroundBotRunner
{
    /// <summary>
    /// Records player movement data for physics engine testing.
    /// Toggle via WoW chat: /say rec
    /// With description:    /say rec jump
    /// Call Poll() from the main loop every tick.
    /// </summary>
    public class MovementRecorder : IDisposable
    {
        private readonly ILogger<MovementRecorder> _logger;
        private readonly Func<ObjectManager?> _getObjectManager;

        private volatile bool _isRecording;
        private CancellationTokenSource? _recordingCts;
        private Task? _recordingTask;

        private MovementRecording? _currentRecording;
        private readonly object _recordingLock = new();

        private int _lastToggleValue;
        private bool _chatHookInstalled;
        private bool _wasInTransition;
        private int _diagnosticFrameCount;

        private int _splineSampleCount;

        /// <summary>Tracks last-known GoState per GUID to log transitions (doors opening/closing).</summary>
        private readonly System.Collections.Generic.Dictionary<ulong, uint> _lastGoState = new();

        private const int DefaultFrameIntervalMs = 16;  // ~60 FPS
        private const int DiagnosticIntervalFrames = 300; // Log position every ~5 seconds

        /// <summary>Replace NaN/Infinity with 0. Memory reads can return garbage during state transitions.</summary>
        private static float Safe(float v) => float.IsFinite(v) ? v : 0f;

        public bool IsRecording => _isRecording;

        public MovementRecorder(Func<ObjectManager?> getObjectManager, ILoggerFactory loggerFactory)
        {
            _getObjectManager = getObjectManager;
            _logger = loggerFactory.CreateLogger<MovementRecorder>();
            _logger.LogInformation("MovementRecorder initialized. Say 'rec' in chat to toggle recording.");
        }

        /// <summary>
        /// Installs a Lua chat event handler that listens for /say messages starting with "rec".
        /// When detected, increments the REC global variable which Poll() watches.
        /// If text follows "rec " (with space), it's stored as the recording description in RD.
        /// Only the player's own messages are matched (arg2 == UnitName('player')).
        /// </summary>
        private void EnsureChatHook()
        {
            if (_chatHookInstalled) return;

            try
            {
                // Lua: create a hidden frame that listens for CHAT_MSG_SAY.
                // When the player says "rec" or "rec <desc>", increment REC and optionally set RD.
                // Increment REC FIRST to guarantee toggle even if description parsing errors.
                Functions.LuaCall(
                    "if not _RF then " +
                        "_RF=CreateFrame('Frame') " +
                        "_RF:RegisterEvent('CHAT_MSG_SAY') " +
                        "_RF:SetScript('OnEvent',function() " +
                            "if arg2==UnitName('player') then " +
                                "local m=strlower(arg1 or '') " +
                                "if m=='rec' or strfind(m,'^rec ') then " +
                                    "REC=(REC or 0)+1 " +
                                    "if strfind(m,'^rec ') then " +
                                        "RD=strsub(arg1,5) " +
                                    "end " +
                                "end " +
                            "end " +
                        "end) " +
                    "end"
                );

                _chatHookInstalled = true;
                _logger.LogInformation("Chat hook installed. Say 'rec' to toggle, 'rec jump' to toggle with description.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to install chat hook, falling back to manual REC variable");
            }
        }

        /// <summary>
        /// Call every tick from the main loop. Installs chat hook on first call,
        /// then watches the REC Lua variable for changes to toggle recording.
        /// </summary>
        public void Poll()
        {
            var objectManager = _getObjectManager();
            if (objectManager?.Player == null)
                return;

            // After a map transition, Lua state may have been reset — reinstall chat hook.
            if (_wasInTransition)
            {
                _chatHookInstalled = false;
                _wasInTransition = false;
                _logger.LogInformation("Map transition ended — reinstalling chat hook");
            }

            // Track transition state so we can detect when it clears
            if (objectManager.IsContinentTransition)
            {
                _wasInTransition = true;
                return;
            }

            EnsureChatHook();

            try
            {
                var result = Functions.LuaCallWithResult("{0} = tostring(REC or 0)");
                if (result.Length == 0) return;

                if (!int.TryParse(result[0], out int currentValue))
                    return;

                if (currentValue != _lastToggleValue && currentValue > 0)
                {
                    if (_isRecording)
                    {
                        _logger.LogInformation("REC toggled ({Value}) - STOPPING recording.", currentValue);
                        StopRecording();
                    }
                    else
                    {
                        _logger.LogInformation("REC toggled ({Value}) - STARTING recording.", currentValue);
                        StartRecording();
                    }
                }

                _lastToggleValue = currentValue;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error polling REC variable");
            }
        }

        /// <summary>
        /// Reads the player's race via Lua UnitRace('player').
        /// Returns the Race enum value matching the Description attribute.
        /// </summary>
        private uint ReadPlayerRace()
        {
            try
            {
                var result = Functions.LuaCallWithResult("{0} = UnitRace('player')");
                if (result.Length > 0 && !string.IsNullOrEmpty(result[0]))
                {
                    var race = Enum.GetValues(typeof(Race))
                        .Cast<Race>()
                        .FirstOrDefault(v => v.GetDescription() == result[0]);
                    return (uint)race;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read player race");
            }
            return 0;
        }

        /// <summary>
        /// Reads the player's gender via Lua UnitSex('player').
        /// WoW returns: 2=Male, 3=Female. We map to: 0=Male, 1=Female.
        /// </summary>
        private uint ReadPlayerGender()
        {
            try
            {
                var result = Functions.LuaCallWithResult("{0} = UnitSex('player')");
                if (result.Length > 0 && int.TryParse(result[0], out int sex))
                {
                    return sex switch
                    {
                        2 => (uint)Gender.Male,     // 0
                        3 => (uint)Gender.Female,   // 1
                        _ => (uint)Gender.None       // 2
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read player gender");
            }
            return (uint)Gender.None;
        }

        /// <summary>
        /// Reads RD Lua variable for user-provided recording description.
        /// </summary>
        private string ReadRecordingDescription()
        {
            try
            {
                var result = Functions.LuaCallWithResult("{0} = RD or ''");
                if (result.Length > 0 && !string.IsNullOrWhiteSpace(result[0]))
                    return result[0];
            }
            catch { }
            return $"Recording started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        public void StartRecording(string? description = null, int frameIntervalMs = DefaultFrameIntervalMs)
        {
            if (_isRecording)
            {
                _logger.LogWarning("Recording is already in progress");
                return;
            }

            var objectManager = _getObjectManager();
            if (objectManager?.Player == null)
            {
                _logger.LogError("Cannot start recording: Player not available");
                return;
            }

            lock (_recordingLock)
            {
                uint race = ReadPlayerRace();
                uint gender = ReadPlayerGender();
                string desc = description ?? ReadRecordingDescription();

                _currentRecording = new MovementRecording
                {
                    CharacterName = objectManager.Player.Name ?? "Unknown",
                    MapId = objectManager.ContinentId,
                    ZoneName = objectManager.ZoneText ?? "Unknown",
                    StartTimestampUtc = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    FrameIntervalMs = (uint)frameIntervalMs,
                    Description = desc,
                    Race = race,
                    Gender = gender
                };

                _recordingCts = new CancellationTokenSource();
                _isRecording = true;
                _splineSampleCount = 0;
                _lastGoState.Clear();

                _recordingTask = Task.Run(() => RecordingLoop(frameIntervalMs, _recordingCts.Token));

                var raceName = Enum.IsDefined(typeof(Race), (int)race) ? ((Race)race).ToString() : "Unknown";
                var genderName = Enum.IsDefined(typeof(Gender), (byte)gender) ? ((Gender)gender).ToString() : "Unknown";

                _logger.LogInformation(
                    "RECORDING_STARTED: Character='{CharacterName}', Race={Race}, Gender={Gender}, Zone='{ZoneName}', Desc='{Desc}', Interval={IntervalMs}ms",
                    _currentRecording.CharacterName, raceName, genderName,
                    _currentRecording.ZoneName, desc, frameIntervalMs);
            }
        }

        public string? StopRecording()
        {
            if (!_isRecording)
            {
                _logger.LogWarning("No recording in progress");
                return null;
            }

            MovementRecording? recordingToSave = null;
            int frameCount = 0;

            lock (_recordingLock)
            {
                _isRecording = false;
                _recordingCts?.Cancel();

                try
                {
                    _recordingTask?.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException) { }

                if (_currentRecording == null || _currentRecording.Frames.Count == 0)
                {
                    _logger.LogWarning("RECORDING_STOPPED: No frames captured");
                    _currentRecording = null;
                    _recordingCts?.Dispose();
                    _recordingCts = null;
                    return null;
                }

                // Take ownership of the recording data and release the lock immediately.
                // SaveRecording does heavy JSON serialization + file I/O that must NOT
                // block the main WoW thread (which processes WM_USER messages).
                recordingToSave = _currentRecording;
                frameCount = _currentRecording.Frames.Count;
                _currentRecording = null;
                _recordingCts?.Dispose();
                _recordingCts = null;
            }

            // Save on a background thread so the main WoW message pump isn't blocked.
            // A long-running save on the main thread freezes WoW and can trigger a crash.
            _logger.LogInformation("RECORDING_STOPPED: {FrameCount} frames captured, saving in background...", frameCount);
            Task.Run(() =>
            {
                try
                {
                    string filePath = SaveRecording(recordingToSave);
                    _logger.LogInformation("RECORDING_SAVED: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save recording ({FrameCount} frames). Data lost.", frameCount);
                }
            });

            return null; // Path not known synchronously; logged when save completes
        }

        private async Task RecordingLoop(int intervalMs, CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (!cancellationToken.IsCancellationRequested && _isRecording)
            {
                try
                {
                    var frame = CaptureFrame((ulong)stopwatch.ElapsedMilliseconds);

                    if (frame != null)
                    {
                        lock (_recordingLock)
                        {
                            _currentRecording?.Frames.Add(frame);
                        }
                    }

                    await Task.Delay(intervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error capturing movement frame");
                }
            }
        }

        private MovementData? CaptureFrame(ulong frameTimestamp)
        {
            var objectManager = _getObjectManager();
            if (objectManager == null || !objectManager.IsInWorld)
                return null;

            // Secondary guard: check for continent transition directly.
            // The IsInWorld check above reads ContinentId, but the map can change
            // between that check and the memory reads below. IsContinentTransition
            // is a volatile bool set by the polling loop and is cheaper/safer to read.
            if (objectManager.IsContinentTransition)
                return null;

            var player = objectManager.Player;
            if (player == null)
                return null;

            if (player is not LocalWoWUnit unit)
                return null;

            try
            {
                var moveFlags = (uint)unit.MovementFlags;
                var pos = unit.Position;
                var frame = new MovementData
                {
                    MovementFlags = moveFlags,
                    FallTime = unit.FallTime,
                    WalkSpeed = Safe(unit.WalkSpeed),
                    RunSpeed = Safe(unit.RunSpeed),
                    RunBackSpeed = Safe(unit.RunBackSpeed),
                    SwimSpeed = Safe(unit.SwimSpeed),
                    SwimBackSpeed = Safe(unit.SwimBackSpeed),
                    TurnRate = Safe(unit.TurnRate),
                    Position = new Position
                    {
                        X = Safe(pos.X),
                        Y = Safe(pos.Y),
                        Z = Safe(pos.Z)
                    },
                    Facing = Safe(unit.Facing),
                    FrameTimestamp = frameTimestamp,
                    JumpVerticalSpeed = Safe(unit.JumpVerticalSpeed),
                    JumpSinAngle = Safe(unit.JumpSinAngle),
                    JumpCosAngle = Safe(unit.JumpCosAngle),
                    JumpHorizontalSpeed = Safe(unit.JumpHorizontalSpeed),
                    SwimPitch = Safe(unit.SwimPitch),
                    FallStartHeight = Safe(unit.FallStartHeight),
                    CurrentSpeed = Safe(unit.CurrentSpeed),
                    FallingSpeed = Safe(unit.CurrentFallingSpeed)
                    // SystemTick = (uint)Environment.TickCount  // Requires proto regeneration
                };

                // Transport data — CONFIRMED via zeppelin recording:
                // When TransportGuid != 0, the Position fields above contain transport-local coords
                // (not world coords). MOVEFLAG_ONTRANSPORT (0x200) is NEVER set in vanilla 1.12.1.
                // TransportOffset X/Y/Z read garbage (sin/cos pair) — NOT transport-local position.
                frame.TransportGuid = unit.TransportGuid;
                frame.TransportOffsetX = 0;
                frame.TransportOffsetY = 0;
                frame.TransportOffsetZ = 0;
                frame.TransportOrientation = 0;

                // Spline data - captured when the player has an active MoveSpline (flight paths, charge, knockback splines)
                // HasMoveSpline reads the MoveSpline pointer at +0xA4C (non-null = active spline).
                // Also check spline-related move flags and high speed as fallbacks.
                uint splineRelatedMask = 0x00400000 | 0x04000000 | 0x02000000;
                bool hasSplineFlags = (moveFlags & splineRelatedMask) != 0;
                bool likelyFlightPath = unit.CurrentSpeed > 20f;
                bool hasActiveSpline = unit.HasMoveSpline;
                if (hasActiveSpline || hasSplineFlags || likelyFlightPath)
                {
                    LogMoveSplineProgress(unit);

                    frame.SplineFlags = (uint)unit.SplineFlags;
                    frame.SplineTimePassed = unit.SplineTimePassed;
                    frame.SplineDuration = unit.SplineDuration;
                    frame.SplineId = unit.SplineId;
                    var finalPt = unit.SplineFinalPoint;
                    frame.SplineFinalPoint = new Position { X = Safe(finalPt.X), Y = Safe(finalPt.Y), Z = Safe(finalPt.Z) };
                    var finalDest = unit.SplineFinalDestination;
                    frame.SplineFinalDestination = new Position { X = Safe(finalDest.X), Y = Safe(finalDest.Y), Z = Safe(finalDest.Z) };
                    foreach (var node in unit.SplineNodes)
                    {
                        frame.SplineNodes.Add(new Position { X = Safe(node.X), Y = Safe(node.Y), Z = Safe(node.Z) });
                    }
                }

                // Snapshot nearby world objects
                // When on transport, unit.Position is transport-local — compute world pos for distance checks
                var playerPos = unit.Position;
                var playerWorldPos = playerPos;
                if (frame.TransportGuid != 0)
                {
                    // Find transport GO to get its world position for coordinate transform
                    foreach (var go in objectManager!.GameObjects)
                    {
                        if (go is LocalWoWGameObject tGo && tGo.Guid == frame.TransportGuid)
                        {
                            var tp = tGo.Position;
                            float cosO = MathF.Cos(tGo.Facing);
                            float sinO = MathF.Sin(tGo.Facing);
                            playerWorldPos = new GameData.Core.Models.Position(
                                playerPos.X * cosO - playerPos.Y * sinO + tp.X,
                                playerPos.X * sinO + playerPos.Y * cosO + tp.Y,
                                playerPos.Z + tp.Z);
                            break;
                        }
                    }
                }
                SnapshotNearbyGameObjects(objectManager!, frame, playerWorldPos, frame.TransportGuid);
                SnapshotNearbyUnits(objectManager!, frame, playerWorldPos, unit.Guid);

                // Periodic player position logging
                _diagnosticFrameCount++;
                if (_diagnosticFrameCount % DiagnosticIntervalFrames == 1)
                {
                    _logger.LogInformation(
                        "PLAYER_POS: flags=0x{Flags:X8} pos=({X:F1},{Y:F1},{Z:F1}) speed={Speed:F2}",
                        moveFlags, unit.Position.X, unit.Position.Y, unit.Position.Z, unit.CurrentSpeed);
                }

                return frame;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading player movement data");
                return null;
            }
        }

        /// <summary>
        /// Captures nearby game objects within range and adds them to the frame.
        /// Only tracks transports and interactive objects (doors, buttons, elevators).
        /// </summary>
        private void SnapshotNearbyGameObjects(ObjectManager objectManager, MovementData frame,
            GameData.Core.Models.Position playerWorldPos, ulong transportGuid)
        {
            const float MaxSnapshotRange = 100f; // yards

            // GameObject types relevant to physics/movement:
            // 0=Door, 1=Button, 11=Transport (elevators), 15=MoTransport (zeppelins, boats)
            const uint GO_TYPE_DOOR = 0;
            const uint GO_TYPE_BUTTON = 1;
            const uint GO_TYPE_TRANSPORT = 11;
            const uint GO_TYPE_MO_TRANSPORT = 15;

            try
            {
                foreach (var go in objectManager.GameObjects)
                {
                    if (go is not LocalWoWGameObject gameObj)
                        continue;

                    var goType = gameObj.TypeId;
                    if (goType != GO_TYPE_DOOR &&
                        goType != GO_TYPE_BUTTON &&
                        goType != GO_TYPE_TRANSPORT &&
                        goType != GO_TYPE_MO_TRANSPORT)
                        continue;

                    var goPos = gameObj.Position;
                    float dx = goPos.X - playerWorldPos.X;
                    float dy = goPos.Y - playerWorldPos.Y;
                    float dz = goPos.Z - playerWorldPos.Z;
                    float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

                    // Always include the transport the player is riding, regardless of distance
                    bool isPlayerTransport = transportGuid != 0 && gameObj.Guid == transportGuid;
                    if (!isPlayerTransport && dist > MaxSnapshotRange)
                        continue;

                    var snapshot = new GameObjectSnapshot
                    {
                        Guid = gameObj.Guid,
                        Entry = gameObj.Entry,
                        DisplayId = gameObj.DisplayId,
                        GameObjectType = goType,
                        Flags = gameObj.Flags,
                        GoState = (uint)gameObj.GoState,
                        Position = new Position
                        {
                            X = Safe(goPos.X),
                            Y = Safe(goPos.Y),
                            Z = Safe(goPos.Z)
                        },
                        Facing = Safe(gameObj.Facing),
                        Name = gameObj.Name ?? "",
                        DistanceToPlayer = Safe(dist),
                        Scale = Safe(gameObj.ScaleX)
                    };

                    // Detect GoState transitions (door open/close, elevator state changes)
                    uint currentState = snapshot.GoState;
                    if (_lastGoState.TryGetValue(gameObj.Guid, out uint prevState))
                    {
                        if (currentState != prevState)
                        {
                            _logger.LogInformation(
                                "GO_STATE_CHANGE: guid=0x{Guid:X} name='{Name}' displayId={DisplayId} type={Type} " +
                                "state {PrevState}->{NewState} pos=({X:F1},{Y:F1},{Z:F1})",
                                gameObj.Guid, snapshot.Name, snapshot.DisplayId, goType,
                                prevState, currentState, goPos.X, goPos.Y, goPos.Z);
                        }
                    }
                    else
                    {
                        _logger.LogInformation(
                            "GO_FIRST_SEEN: guid=0x{Guid:X} name='{Name}' displayId={DisplayId} type={Type} " +
                            "state={State} flags=0x{Flags:X} scale={Scale:F2} pos=({X:F1},{Y:F1},{Z:F1})",
                            gameObj.Guid, snapshot.Name, snapshot.DisplayId, goType,
                            currentState, snapshot.Flags, snapshot.Scale, goPos.X, goPos.Y, goPos.Z);
                    }
                    _lastGoState[gameObj.Guid] = currentState;

                    frame.NearbyGameObjects.Add(snapshot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error snapshotting game objects");
            }
        }

        /// <summary>
        /// Captures nearby units (NPCs and other players) within range.
        /// Useful for collision avoidance and pathing validation.
        /// </summary>
        private void SnapshotNearbyUnits(ObjectManager objectManager, MovementData frame, GameData.Core.Models.Position playerPos, ulong playerGuid)
        {
            const float MaxSnapshotRange = 60f; // yards

            try
            {
                foreach (var obj in objectManager.Units)
                {
                    if (obj is not LocalWoWUnit unit || unit.Guid == playerGuid)
                        continue;

                    var unitPos = unit.Position;
                    float dx = unitPos.X - playerPos.X;
                    float dy = unitPos.Y - playerPos.Y;
                    float dz = unitPos.Z - playerPos.Z;
                    float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

                    if (dist > MaxSnapshotRange)
                        continue;

                    var unitMoveFlags = (uint)unit.MovementFlags;
                    bool hasSpline = unit.HasMoveSpline;
                    var snapshot = new UnitSnapshot
                    {
                        Guid = unit.Guid,
                        Entry = unit.ObjectType == GameData.Core.Enums.WoWObjectType.Unit
                            ? unit.Entry : 0,
                        Name = unit.Name ?? "",
                        Position = new Position
                        {
                            X = Safe(unitPos.X),
                            Y = Safe(unitPos.Y),
                            Z = Safe(unitPos.Z)
                        },
                        Facing = Safe(unit.Facing),
                        MovementFlags = unitMoveFlags,
                        Health = unit.Health,
                        MaxHealth = unit.MaxHealth,
                        Level = unit.Level,
                        UnitFlags = (uint)unit.UnitFlags,
                        DistanceToPlayer = Safe(dist),
                        BoundingRadius = Safe(unit.BoundingRadius),
                        CombatReach = Safe(unit.CombatReach),
                        NpcFlags = (uint)unit.NpcFlags,
                        TargetGuid = unit.TargetGuid,
                        IsPlayer = unit.ObjectType == GameData.Core.Enums.WoWObjectType.Player,
                        HasSpline = hasSpline
                    };

                    // Populate spline details when available
                    if (hasSpline)
                    {
                        snapshot.SplineFlags = (uint)unit.SplineFlags;
                        snapshot.SplineTimePassed = unit.SplineTimePassed;
                        snapshot.SplineDuration = unit.SplineDuration;
                        var splineDest = unit.SplineFinalDestination;
                        snapshot.SplineFinalDestination = new Position { X = Safe(splineDest.X), Y = Safe(splineDest.Y), Z = Safe(splineDest.Z) };
                        snapshot.SplineNodeCount = (uint)unit.SplineNodes.Count;
                    }

                    frame.NearbyUnits.Add(snapshot);
                }

                // Also include other players
                foreach (var obj in objectManager.Players)
                {
                    if (obj is not LocalWoWUnit unit || unit.Guid == playerGuid)
                        continue;

                    var unitPos = unit.Position;
                    float dx = unitPos.X - playerPos.X;
                    float dy = unitPos.Y - playerPos.Y;
                    float dz = unitPos.Z - playerPos.Z;
                    float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

                    if (dist > MaxSnapshotRange)
                        continue;

                    var playerMoveFlags = (uint)unit.MovementFlags;
                    var snapshot = new UnitSnapshot
                    {
                        Guid = unit.Guid,
                        Name = unit.Name ?? "",
                        Position = new Position
                        {
                            X = Safe(unitPos.X),
                            Y = Safe(unitPos.Y),
                            Z = Safe(unitPos.Z)
                        },
                        Facing = Safe(unit.Facing),
                        MovementFlags = playerMoveFlags,
                        Health = unit.Health,
                        MaxHealth = unit.MaxHealth,
                        Level = unit.Level,
                        UnitFlags = (uint)unit.UnitFlags,
                        DistanceToPlayer = Safe(dist),
                        BoundingRadius = Safe(unit.BoundingRadius),
                        CombatReach = Safe(unit.CombatReach),
                        TargetGuid = unit.TargetGuid,
                        IsPlayer = true,
                        HasSpline = unit.HasMoveSpline
                    };

                    frame.NearbyUnits.Add(snapshot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error snapshotting nearby units");
            }
        }

        /// <summary>
        /// Logs MoveSpline progress at start of flight and periodically (~every 5s).
        /// Captures confirmed fields: time_passed, duration, nodeCount, flags, destination.
        /// </summary>
        private void LogMoveSplineProgress(LocalWoWUnit unit)
        {
            _splineSampleCount++;
            if (_splineSampleCount > 1 && _splineSampleCount % 300 != 0) return;

            try
            {
                var timePassed = unit.SplineTimePassed;
                var duration = unit.SplineDuration;
                var dest = unit.SplineFinalPoint;
                var pos = unit.Position;
                var nodeCount = unit.SplineNodes.Count;

                _logger.LogInformation(
                    "SPLINE: t={TimePassed}/{Duration}ms nodes={Nodes} flags=0x{Flags:X} dest=({DX:F1},{DY:F1},{DZ:F1}) pos=({PX:F1},{PY:F1},{PZ:F1}) speed={Speed:F1}",
                    timePassed, duration, nodeCount, (uint)unit.SplineFlags,
                    dest.X, dest.Y, dest.Z, pos.X, pos.Y, pos.Z, unit.CurrentSpeed);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error in MoveSpline progress log");
            }
        }

        private string SaveRecording(MovementRecording recording)
        {
            var recordingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BloogBot",
                "MovementRecordings"
            );
            Directory.CreateDirectory(recordingsDir);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string safeName = new string((recording.CharacterName ?? "Unknown").Where(c => char.IsLetterOrDigit(c)).ToArray());
            if (string.IsNullOrEmpty(safeName)) safeName = "Unknown";
            // Sanitize zone name: remove any characters invalid in Windows file paths
            string safeZone = recording.ZoneName ?? "Unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
                safeZone = safeZone.Replace(c, '_');
            safeZone = safeZone.Replace(' ', '_');
            string baseFileName = $"{safeName}_{safeZone}_{timestamp}";

            // Save as JSON (human-readable)
            string jsonPath = Path.Combine(recordingsDir, $"{baseFileName}.json");
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };

            var raceName = Enum.IsDefined(typeof(Race), (int)recording.Race) ? ((Race)recording.Race).ToString() : "Unknown";
            var genderName = Enum.IsDefined(typeof(Gender), (byte)recording.Gender) ? ((Gender)recording.Gender).ToString() : "Unknown";

            var serializableRecording = new
            {
                recording.CharacterName,
                recording.MapId,
                recording.ZoneName,
                recording.StartTimestampUtc,
                recording.FrameIntervalMs,
                recording.Description,
                Race = recording.Race,
                RaceName = raceName,
                Gender = recording.Gender,
                GenderName = genderName,
                FrameCount = recording.Frames.Count,
                DurationMs = recording.Frames.Count > 0 ? recording.Frames.Last().FrameTimestamp : 0,
                Frames = recording.Frames.Select(f => new
                {
                    f.FrameTimestamp,
                    f.MovementFlags,
                    MovementFlagsHex = $"0x{f.MovementFlags:X8}",
                    Position = new { f.Position.X, f.Position.Y, f.Position.Z },
                    f.Facing,
                    f.FallTime,
                    f.WalkSpeed,
                    f.RunSpeed,
                    f.RunBackSpeed,
                    f.SwimSpeed,
                    f.SwimBackSpeed,
                    f.TurnRate,
                    f.JumpVerticalSpeed,
                    f.JumpSinAngle,
                    f.JumpCosAngle,
                    f.JumpHorizontalSpeed,
                    f.SwimPitch,
                    f.FallStartHeight,
                    f.TransportGuid,
                    f.TransportOffsetX,
                    f.TransportOffsetY,
                    f.TransportOffsetZ,
                    f.TransportOrientation,
                    f.CurrentSpeed,
                    f.FallingSpeed,
                    // Player spline data (charge, flight path, knockback spline, etc.)
                    f.SplineFlags,
                    f.SplineTimePassed,
                    f.SplineDuration,
                    f.SplineId,
                    SplineFinalPoint = f.SplineFinalPoint != null
                        ? new { f.SplineFinalPoint.X, f.SplineFinalPoint.Y, f.SplineFinalPoint.Z }
                        : null,
                    SplineFinalDestination = f.SplineFinalDestination != null
                        ? new { f.SplineFinalDestination.X, f.SplineFinalDestination.Y, f.SplineFinalDestination.Z }
                        : null,
                    SplineNodeCount = f.SplineNodes.Count,
                    NearbyGameObjects = f.NearbyGameObjects.Select(go => new
                    {
                        go.Guid,
                        go.Entry,
                        go.DisplayId,
                        go.GameObjectType,
                        go.Flags,
                        go.GoState,
                        Position = new { go.Position.X, go.Position.Y, go.Position.Z },
                        go.Facing,
                        go.Name,
                        go.DistanceToPlayer
                    }),
                    NearbyUnits = f.NearbyUnits.Select(u => new
                    {
                        u.Guid,
                        u.Entry,
                        u.Name,
                        Position = new { u.Position.X, u.Position.Y, u.Position.Z },
                        u.Facing,
                        u.MovementFlags,
                        MovementFlagsHex = $"0x{u.MovementFlags:X8}",
                        u.Health,
                        u.MaxHealth,
                        u.Level,
                        u.UnitFlags,
                        u.DistanceToPlayer,
                        u.BoundingRadius,
                        u.CombatReach,
                        u.NpcFlags,
                        u.TargetGuid,
                        u.IsPlayer,
                        u.HasSpline,
                        u.SplineFlags,
                        u.SplineTimePassed,
                        u.SplineDuration,
                        SplineFinalDestination = u.SplineFinalDestination != null
                            ? new { u.SplineFinalDestination.X, u.SplineFinalDestination.Y, u.SplineFinalDestination.Z }
                            : null,
                        u.SplineNodeCount
                    })
                })
            };

            File.WriteAllText(jsonPath, JsonSerializer.Serialize(serializableRecording, jsonOptions));

            // Also save as binary protobuf
            string protoPath = Path.Combine(recordingsDir, $"{baseFileName}.bin");
            using (var output = File.Create(protoPath))
            {
                recording.WriteTo(output);
            }

            _logger.LogInformation("Saved recording to:\n  JSON: {JsonPath}\n  Binary: {ProtoPath}", jsonPath, protoPath);

            return jsonPath;
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                StopRecording();
            }

            _recordingCts?.Dispose();
        }
    }
}
