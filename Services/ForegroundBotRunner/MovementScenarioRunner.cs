using ForegroundBotRunner.Statics;
using GameData.Core.Models;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ForegroundBotRunner
{
    /// <summary>
    /// Runs controlled movement scenarios for physics engine calibration.
    /// Each scenario teleports to a known location, executes specific movements,
    /// and records ground-truth data via MovementRecorder.
    ///
    /// Requires GM level 3 for .go xyz teleport commands.
    /// </summary>
    public class MovementScenarioRunner(ObjectManager objectManager, MovementRecorder recorder, ILoggerFactory loggerFactory)
    {
        private readonly ObjectManager _objectManager = objectManager;
        private readonly MovementRecorder _recorder = recorder;
        private readonly ILogger _logger = loggerFactory.CreateLogger<MovementScenarioRunner>();
        private readonly string[] _scenarioSelection = ParseScenarioSelection(
            Environment.GetEnvironmentVariable("WWOW_AUTOMATED_SCENARIOS"));

        // Diagnostic log (writes to WoW's WWoWLogs directory so tests can read it)
        private static readonly string DiagnosticLogPath;
        private static readonly object DiagLogLock = new();
        static MovementScenarioRunner()
        {
            string wowDir;
            try { wowDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName) ?? AppContext.BaseDirectory; }
            catch { wowDir = AppContext.BaseDirectory; }
            var logsDir = Path.Combine(wowDir, "WWoWLogs");
            try { Directory.CreateDirectory(logsDir); } catch { }
            DiagnosticLogPath = Path.Combine(logsDir, "scenario_runner.log");
        }
        private static void DiagLog(string message)
        {
            try { lock (DiagLogLock) { File.AppendAllText(DiagnosticLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); } } catch { }
        }

        // Known locations in Durotar (map 1 = Kalimdor)
        // Flat road south of Razor Hill - wide open, level terrain
        // Ground level confirmed ~9.6-10.2 in recordings
        private const float FlatX = 317f, FlatY = -4734f, FlatZ = 12f;
        private const uint KalimdorMapId = 1;
        private const uint EasternKingdomsMapId = 0;

        // Fall test: 50 units above flat ground
        private const float FallZ = 62f; // FlatZ + 50

        // Tiragarde Keep moat / Durotar coast - shallow water with terrain geometry
        // The oasis/river areas have defined riverbeds unlike deep ocean
        // Southfury River near Orgrimmar (has terrain underneath)
        private const float WaterX = 1810f, WaterY = -4420f, WaterZ = -12f;

        // Slope: Durotar road approaching Razor Hill from the south (gentle slope)
        // Use Z slightly above terrain to avoid underground
        private const float SlopeX = 290f, SlopeY = -4660f, SlopeZ = 18f;

        // Valley of Trials slope: the BG Navigation_LongPath route
        // This crosses terrain where ADT oscillates between surface (~52) and gullies (~30-40),
        // causing BG bot Z oscillation. FG recording captures WoW client's actual Z behavior.
        private const float VotSlopeStartX = -284f, VotSlopeStartY = -4383f, VotSlopeStartZ = 60f;
        private const float VotSlopeEndX = -350f, VotSlopeEndY = -4450f;

        // Undercity underground route and west elevator coordinates lifted from the
        // packetless but geometry-complete FG capture Dralrahgra_Undercity_2026-02-13_19-26-54.
        private const uint UndercityWestElevatorEntry = 20655;
        private const float UndercityWestElevatorLowerZ = -40.8f;
        private const float UndercityWestElevatorUpperExitZ = 55.1f;
        private const float UndercityWestElevatorExitX = 1552.1f;
        private const float UndercityWestElevatorExitY = 242.2f;
        private const float UndercityLowerBoardStartX = 1532.3f;
        private const float UndercityLowerBoardStartY = 242.2f;
        private const float UndercityLowerBoardStartZ = -41.4f;
        private const float UndercityBoardFacing = 0.0f;
        private static readonly Position[] UndercityLowerRouteWaypoints =
        [
            new Position(1549.0f, 222.4f, -43.1f),
            new Position(1538.5f, 217.5f, -43.1f),
            new Position(1529.3f, 219.3f, -43.1f),
            new Position(1527.2f, 230.3f, -41.7f),
            new Position(1531.8f, 240.2f, -41.4f),
            new Position(1532.3f, 242.2f, -41.4f),
        ];

        internal static string[] ParseScenarioSelection(string? rawSelection)
        {
            if (string.IsNullOrWhiteSpace(rawSelection))
                return [];

            return rawSelection
                .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static bool ShouldRunScenario(string scenarioName, IReadOnlyList<string> scenarioSelection)
        {
            if (scenarioSelection.Count == 0)
                return true;

            return scenarioSelection.Any(token =>
                scenarioName.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        private bool ShouldRunScenario(string scenarioName)
            => ShouldRunScenario(scenarioName, _scenarioSelection);

        internal static string? BuildMovementStopLua(ControlBits bits)
        {
            var commands = new List<string>();
            foreach (var bit in ObjectManager.ExpandControlBits(bits))
            {
                switch (bit)
                {
                    case ControlBits.Front:
                        commands.Add("MoveForwardStop()");
                        break;
                    case ControlBits.Back:
                        commands.Add("MoveBackwardStop()");
                        break;
                    case ControlBits.Left:
                        commands.Add("TurnLeftStop()");
                        break;
                    case ControlBits.Right:
                        commands.Add("TurnRightStop()");
                        break;
                    case ControlBits.StrafeLeft:
                        commands.Add("StrafeLeftStop()");
                        break;
                    case ControlBits.StrafeRight:
                        commands.Add("StrafeRightStop()");
                        break;
                }
            }

            return commands.Count == 0 ? null : string.Join("; ", commands);
        }

        internal static string? BuildMovementStartLua(ControlBits bits)
        {
            var commands = new List<string>();
            foreach (var bit in ObjectManager.ExpandControlBits(bits))
            {
                switch (bit)
                {
                    case ControlBits.Front:
                        commands.Add("MoveForwardStart()");
                        break;
                    case ControlBits.Back:
                        commands.Add("MoveBackwardStart()");
                        break;
                    case ControlBits.Left:
                        commands.Add("TurnLeftStart()");
                        break;
                    case ControlBits.Right:
                        commands.Add("TurnRightStart()");
                        break;
                    case ControlBits.StrafeLeft:
                        commands.Add("StrafeLeftStart()");
                        break;
                    case ControlBits.StrafeRight:
                        commands.Add("StrafeRightStart()");
                        break;
                }
            }

            return commands.Count == 0 ? null : string.Join("; ", commands);
        }

        public async Task RunAllScenariosAsync(CancellationToken ct)
        {
            try { File.WriteAllText(DiagnosticLogPath, $"=== Scenario Runner Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n"); } catch { }
            DiagLog("Starting all scenarios");
            _logger.LogInformation("=== AUTOMATED MOVEMENT RECORDING: Starting all scenarios ===");

            if (_scenarioSelection.Length > 0)
            {
                var selectionText = string.Join(", ", _scenarioSelection);
                DiagLog($"Scenario filter active: {selectionText}");
                _logger.LogInformation("Scenario filter active: {Selection}", selectionText);
            }

            try
            {
                async Task RunIfSelected(
                    string name,
                    string description,
                    Func<CancellationToken, Task> setup,
                    Func<CancellationToken, Task> execute)
                {
                    if (!ShouldRunScenario(name))
                    {
                        DiagLog($"SCENARIO_SKIP: {name}");
                        _logger.LogInformation("SCENARIO_SKIP: {Name}", name);
                        return;
                    }

                    await RunScenario(name, description, setup, execute, ct);
                }

                await RunIfSelected("01_flat_run_forward", "Run forward on flat ground (5s)",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        TryStartMovement(ControlBits.Front);
                        await Task.Delay(5000, ct2);
                        TryStopMovement(ControlBits.Front);
                    });

                await RunIfSelected("02_flat_run_backward", "Run backward on flat ground (5s)",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        TryStartMovement(ControlBits.Back);
                        await Task.Delay(5000, ct2);
                        TryStopMovement(ControlBits.Back);
                    });

                await RunIfSelected("03_standing_jump", "Jump from standstill on flat ground",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        _objectManager.Jump();
                        await Task.Delay(2000, ct2);
                    });

                await RunIfSelected("04_running_jump", "Jump while running forward on flat ground",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        TryStartMovement(ControlBits.Front);
                        await Task.Delay(500, ct2);
                        _objectManager.Jump();
                        await Task.Delay(2000, ct2);
                        TryStopMovement(ControlBits.Front);
                    });

                await RunIfSelected("05_fall_from_height", "Fall from 50 units above flat ground",
                    setup: async ct2 =>
                    {
                        // Teleport high above flat ground - DON'T settle (we want to capture the fall)
                        var cmd = $".go xyz {FlatX:F1} {FlatY:F1} {FallZ:F1} {KalimdorMapId}";
                        DiagLog($"Teleporting for fall: {cmd}");
                        _objectManager.SendChatMessage(cmd);
                        await Task.Delay(1000, ct2); // Brief delay for teleport to register
                    },
                    execute: async ct2 =>
                    {
                        // Character should be falling from Z=62 to Z~10 (about 3s of free fall)
                        await Task.Delay(5000, ct2);
                    });

                await RunIfSelected("06_strafe_forward", "Run forward + strafe right (diagonal, 5s)",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        TryStartMovement(ControlBits.Front | ControlBits.StrafeRight);
                        await Task.Delay(5000, ct2);
                        TryStopMovement(ControlBits.Front | ControlBits.StrafeRight);
                    });

                await RunIfSelected("07_strafe_only", "Strafe right only (no forward, 5s)",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        TryStartMovement(ControlBits.StrafeRight);
                        await Task.Delay(5000, ct2);
                        TryStopMovement(ControlBits.StrafeRight);
                    });

                await RunIfSelected("08_swim_forward", "Swim forward in Southfury River (5s)",
                    setup: async ct2 =>
                    {
                        await TeleportAndSettle(WaterX, WaterY, WaterZ, ct2);
                        SetFacing(0f); // Face north (along river)
                        await Settle(ct2);
                    },
                    execute: async ct2 =>
                    {
                        TryStartMovement(ControlBits.Front);
                        await Task.Delay(5000, ct2);
                        TryStopMovement(ControlBits.Front);
                    });

                await RunIfSelected("09_slope_uphill", "Run uphill on a slope (5s)",
                    setup: async ct2 =>
                    {
                        await TeleportAndSettle(SlopeX, SlopeY, SlopeZ, ct2);
                        SetFacing(2.356f); // ~135 degrees (northwest, toward higher ground)
                        await Settle(ct2);
                    },
                    execute: async ct2 =>
                    {
                        TryStartMovement(ControlBits.Front);
                        await Task.Delay(5000, ct2);
                        TryStopMovement(ControlBits.Front);
                    });

                await RunIfSelected("10_slope_downhill", "Run downhill on a slope (5s)",
                    setup: async ct2 =>
                    {
                        await TeleportAndSettle(SlopeX, SlopeY, SlopeZ + 20f, ct2);
                        SetFacing(5.497f); // ~315 degrees (southeast, toward lower ground)
                        await Settle(ct2);
                    },
                    execute: async ct2 =>
                    {
                        TryStartMovement(ControlBits.Front);
                        await Task.Delay(5000, ct2);
                        TryStopMovement(ControlBits.Front);
                    });

                // Valley of Trials slope: the problematic BG Navigation_LongPath route
                // Walking southwest on sloped terrain where ADT has gullies causing BG bot Z oscillation
                await RunIfSelected("11_vot_slope_southwest", "Valley of Trials slope route (SW, ~100y, matches BG Navigation_LongPath)",
                    setup: async ct2 =>
                    {
                        await TeleportAndSettle(VotSlopeStartX, VotSlopeStartY, VotSlopeStartZ, ct2);
                        // Face toward destination: atan2(endY-startY, endX-startX)
                        float facing = MathF.Atan2(VotSlopeEndY - VotSlopeStartY, VotSlopeEndX - VotSlopeStartX);
                        if (facing < 0) facing += 2 * MathF.PI;
                        SetFacing(facing);
                        await Settle(ct2);
                    },
                    execute: async ct2 =>
                    {
                        // Run time: ~100y at 7.0 speed = ~14s + buffer
                        TryStartMovement(ControlBits.Front);
                        await Task.Delay(18000, ct2);
                        TryStopMovement(ControlBits.Front);
                    });

                // Return trip (NE uphill) to capture the reverse slope behavior
                await RunIfSelected("12_vot_slope_northeast", "Valley of Trials slope route (NE uphill, return path)",
                    setup: async ct2 =>
                    {
                        await TeleportAndSettle(VotSlopeEndX, VotSlopeEndY, VotSlopeStartZ, ct2);
                        float facing = MathF.Atan2(VotSlopeStartY - VotSlopeEndY, VotSlopeStartX - VotSlopeEndX);
                        if (facing < 0) facing += 2 * MathF.PI;
                        SetFacing(facing);
                        await Settle(ct2);
                    },
                    execute: async ct2 =>
                    {
                        TryStartMovement(ControlBits.Front);
                        await Task.Delay(18000, ct2);
                        TryStopMovement(ControlBits.Front);
                    });

                await RunIfSelected("13_undercity_lower_route", "Undercity underground lower route toward west elevator",
                    setup: async ct2 =>
                    {
                        var start = UndercityLowerRouteWaypoints[0];
                        await TeleportAndSettle(start.X, start.Y, start.Z, EasternKingdomsMapId, ct2);
                        await Settle(ct2);
                    },
                    execute: async ct2 =>
                    {
                        await DriveWaypointsAsync(UndercityLowerRouteWaypoints, TimeSpan.FromSeconds(20), ct2);
                    });

                await RunIfSelected("14_undercity_elevator_west_up", "Undercity west elevator lower to upper ride with disembark",
                    setup: async ct2 =>
                    {
                        await TeleportAndSettle(UndercityLowerBoardStartX, UndercityLowerBoardStartY, UndercityLowerBoardStartZ, EasternKingdomsMapId, ct2);
                        SetFacing(UndercityBoardFacing);
                        await Settle(ct2);
                    },
                    execute: async ct2 =>
                    {
                        await RideUndercityWestElevatorLowerToUpperAsync(ct2);
                    });

                DiagLog("All scenarios complete");
                _logger.LogInformation("=== AUTOMATED MOVEMENT RECORDING: All scenarios complete ===");
            }
            catch (OperationCanceledException)
            {
                DiagLog("Automated recording CANCELLED");
                _logger.LogWarning("Automated recording cancelled");
                TryStopAllMovement();
            }
            catch (Exception ex)
            {
                DiagLog($"Automated recording ERROR: {ex.Message}\n{ex.StackTrace}");
                _logger.LogError(ex, "Error during automated recording");
                TryStopAllMovement();
            }
        }

        private async Task RunScenario(string name, string description,
            Func<CancellationToken, Task> setup, Func<CancellationToken, Task> execute, CancellationToken ct)
        {
            DiagLog($"SCENARIO_START: {name} - {description}");
            _logger.LogInformation("SCENARIO_START: {Name} - {Description}", name, description);

            // Phase 1: Setup (teleport, face, settle) - NOT recorded
            TryStopAllMovement();
            await Task.Delay(500, ct);
            await setup(ct);

            // Phase 2: Record the actual movement
            _recorder.StartRecording($"{name}: {description}");
            await Task.Delay(200, ct); // Capture starting position

            try
            {
                await execute(ct);
            }
            finally
            {
                TryStopAllMovement();
                await Task.Delay(500, ct); // Capture final position

                var filePath = _recorder.StopRecording();
                DiagLog($"SCENARIO_COMPLETE: {name} -> {filePath ?? "(no frames)"}");
                _logger.LogInformation("SCENARIO_COMPLETE: {Name} -> {FilePath}", name, filePath ?? "(no frames)");
            }

            await Task.Delay(2000, ct); // Settle between scenarios
        }

        private async Task TeleportAndSettle(float x, float y, float z, CancellationToken ct)
            => await TeleportAndSettle(x, y, z, KalimdorMapId, ct);

        private async Task TeleportAndSettle(float x, float y, float z, uint mapId, CancellationToken ct)
        {
            var cmd = $".go xyz {x:F1} {y:F1} {z:F1} {mapId}";
            DiagLog($"Teleporting: {cmd}");
            _objectManager.SendChatMessage(cmd);

            // Wait for teleport to take effect
            await Task.Delay(2000, ct);

            // Wait until character is on the ground (not falling) or timeout
            for (int i = 0; i < 20; i++) // 20 x 250ms = 5s max
            {
                var player = _objectManager.Player;
                if (player is Objects.WoWUnit unit)
                {
                    var flags = (uint)unit.MovementFlags;
                    var pos = unit.Position;
                    DiagLog($"  Settle[{i}]: pos=({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1}), moveFlags=0x{flags:X8}");

                    // Check if character is grounded (not falling/swimming)
                    const uint MOVEFLAG_FALLING = 0x2000;
                    if ((flags & MOVEFLAG_FALLING) == 0)
                    {
                        DiagLog($"  Settled at ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
                        return;
                    }
                }
                await Task.Delay(250, ct);
            }

            DiagLog("  Settle TIMEOUT - proceeding anyway");
        }

        private void SetFacing(float facing)
        {
            _objectManager.SetFacing(facing);
        }

        private async Task DriveWaypointsAsync(IReadOnlyList<Position> waypoints, TimeSpan timeout, CancellationToken ct)
        {
            if (waypoints.Count == 0)
                return;

            const float arriveDistance = 2.5f;
            int waypointIndex = 0;
            var sw = Stopwatch.StartNew();

            while (waypointIndex < waypoints.Count && sw.Elapsed < timeout)
            {
                ct.ThrowIfCancellationRequested();

                var player = _objectManager.Player;
                if (player == null)
                {
                    await Task.Delay(100, ct);
                    continue;
                }

                var waypoint = waypoints[waypointIndex];
                var position = player.Position;
                var distance = position.DistanceTo(waypoint);

                if (distance <= arriveDistance)
                {
                    DiagLog($"Waypoint {waypointIndex} reached at ({position.X:F1},{position.Y:F1},{position.Z:F1})");
                    waypointIndex++;
                    continue;
                }

                float facing = MathF.Atan2(waypoint.Y - position.Y, waypoint.X - position.X);
                if (facing < 0f)
                    facing += MathF.PI * 2f;

                SetFacing(facing);
                TryStartMovement(ControlBits.Front);
                await Task.Delay(100, ct);
            }

            TryStopAllMovement();

            if (waypointIndex < waypoints.Count)
            {
                var remaining = waypoints.Count - waypointIndex;
                throw new TimeoutException($"Timed out driving waypoints; {remaining} waypoint(s) remaining.");
            }
        }

        private async Task<Objects.WoWGameObject?> WaitForGameObjectAsync(
            uint entry,
            Func<Objects.WoWGameObject, bool> predicate,
            TimeSpan timeout,
            CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                ct.ThrowIfCancellationRequested();

                foreach (var gameObject in _objectManager.GameObjects)
                {
                    if (gameObject is not Objects.WoWGameObject go || go.Entry != entry)
                        continue;

                    if (predicate(go))
                        return go;
                }

                await Task.Delay(200, ct);
            }

            return null;
        }

        private async Task RideUndercityWestElevatorLowerToUpperAsync(CancellationToken ct)
        {
            DiagLog("Waiting for west Undercity elevator at lower stop");
            var elevator = await WaitForGameObjectAsync(
                UndercityWestElevatorEntry,
                go => MathF.Abs(go.Position.Z - UndercityWestElevatorLowerZ) <= 6f,
                TimeSpan.FromSeconds(45),
                ct);

            if (elevator == null)
                throw new TimeoutException("West Undercity elevator never reached the lower stop.");

            DiagLog($"Elevator arrived: guid=0x{elevator.Guid:X} z={elevator.Position.Z:F2}");

            SetFacing(UndercityBoardFacing);
            TryStartMovement(ControlBits.Front);

            var boardDeadline = Stopwatch.StartNew();
            while (boardDeadline.Elapsed < TimeSpan.FromSeconds(10))
            {
                ct.ThrowIfCancellationRequested();

                if (_objectManager.Player is Objects.WoWUnit unit && unit.TransportGuid == elevator.Guid)
                {
                    DiagLog($"Boarded elevator guid=0x{elevator.Guid:X}");
                    break;
                }

                await Task.Delay(100, ct);
            }

            if (_objectManager.Player is not Objects.WoWUnit boardedUnit || boardedUnit.TransportGuid != elevator.Guid)
            {
                TryStopAllMovement();
                throw new TimeoutException("Failed to board west Undercity elevator within 10 seconds.");
            }

            var rideDeadline = Stopwatch.StartNew();
            while (rideDeadline.Elapsed < TimeSpan.FromSeconds(30))
            {
                ct.ThrowIfCancellationRequested();

                if (_objectManager.Player is not Objects.WoWUnit unit)
                {
                    await Task.Delay(100, ct);
                    continue;
                }

                if (unit.TransportGuid == 0 && unit.Position.Z >= UndercityWestElevatorUpperExitZ - 1.5f)
                {
                    var dx = unit.Position.X - UndercityWestElevatorExitX;
                    var dy = unit.Position.Y - UndercityWestElevatorExitY;
                    if (MathF.Sqrt(dx * dx + dy * dy) <= 8f)
                    {
                        DiagLog($"Disembarked upper stop at ({unit.Position.X:F2},{unit.Position.Y:F2},{unit.Position.Z:F2})");
                        break;
                    }
                }

                await Task.Delay(100, ct);
            }

            await Task.Delay(750, ct);
            TryStopAllMovement();
        }

        private async Task Settle(CancellationToken ct)
        {
            // Brief pause to let client settle after teleport/facing change
            await Task.Delay(500, ct);
        }

        private void TryStopAllMovement()
        {
            const ControlBits stopBits =
                ControlBits.Front |
                ControlBits.Back |
                ControlBits.Left |
                ControlBits.Right |
                ControlBits.StrafeLeft |
                ControlBits.StrafeRight;

            TryStopMovement(stopBits);
        }

        private void TryStartMovement(ControlBits bits)
        {
            try
            {
                _objectManager.StartMovement(bits);
                return;
            }
            catch (Exception ex)
            {
                DiagLog($"Native StartMovement failed for {bits}: {ex.Message}");
                DiagLog(ex.ToString());
                _logger.LogWarning(ex, "Native StartMovement failed for {Bits}; falling back to Lua start commands", bits);
            }

            string? lua = BuildMovementStartLua(bits);
            if (string.IsNullOrWhiteSpace(lua))
                return;

            try
            {
                ObjectManager.MainThreadLuaCall(lua);
                DiagLog($"Lua movement start fallback: {lua}");
            }
            catch (Exception ex)
            {
                DiagLog($"Lua movement start fallback failed: {ex.Message}");
                _logger.LogWarning(ex, "Lua movement start fallback failed for {Bits}", bits);
            }
        }

        private void TryStopMovement(ControlBits bits)
        {
            try
            {
                _objectManager.StopMovement(bits);
                return;
            }
            catch (Exception ex)
            {
                DiagLog($"Native StopMovement failed for {bits}: {ex.Message}");
                DiagLog(ex.ToString());
                _logger.LogWarning(ex, "Native StopMovement failed for {Bits}; falling back to Lua stop commands", bits);
            }

            string? lua = BuildMovementStopLua(bits);
            if (string.IsNullOrWhiteSpace(lua))
                return;

            try
            {
                ObjectManager.MainThreadLuaCall(lua);
                DiagLog($"Lua movement stop fallback: {lua}");
            }
            catch (Exception ex)
            {
                DiagLog($"Lua movement stop fallback failed: {ex.Message}");
                _logger.LogWarning(ex, "Lua movement stop fallback failed for {Bits}", bits);
            }
        }
    }
}
