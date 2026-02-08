using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Statics;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;

namespace ForegroundBotRunner
{
    /// <summary>
    /// Runs controlled movement scenarios for physics engine calibration.
    /// Each scenario teleports to a known location, executes specific movements,
    /// and records ground-truth data via MovementRecorder.
    ///
    /// Requires GM level 3 for .go xyz teleport commands.
    /// </summary>
    public class MovementScenarioRunner
    {
        private readonly ObjectManager _objectManager;
        private readonly MovementRecorder _recorder;
        private readonly ILogger _logger;

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
        private const int MapId = 1;

        // Fall test: 50 units above flat ground
        private const float FallZ = 62f; // FlatZ + 50

        // Tiragarde Keep moat / Durotar coast - shallow water with terrain geometry
        // The oasis/river areas have defined riverbeds unlike deep ocean
        // Southfury River near Orgrimmar (has terrain underneath)
        private const float WaterX = 1810f, WaterY = -4420f, WaterZ = -12f;

        // Slope: Durotar road approaching Razor Hill from the south (gentle slope)
        // Use Z slightly above terrain to avoid underground
        private const float SlopeX = 290f, SlopeY = -4660f, SlopeZ = 18f;

        public MovementScenarioRunner(ObjectManager objectManager, MovementRecorder recorder, ILoggerFactory loggerFactory)
        {
            _objectManager = objectManager;
            _recorder = recorder;
            _logger = loggerFactory.CreateLogger<MovementScenarioRunner>();
        }

        public async Task RunAllScenariosAsync(CancellationToken ct)
        {
            try { File.WriteAllText(DiagnosticLogPath, $"=== Scenario Runner Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n"); } catch { }
            DiagLog("Starting all scenarios");
            _logger.LogInformation("=== AUTOMATED MOVEMENT RECORDING: Starting all scenarios ===");

            try
            {
                await RunScenario("01_flat_run_forward", "Run forward on flat ground (5s)",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        _objectManager.StartMovement(ControlBits.Front);
                        await Task.Delay(5000, ct2);
                        _objectManager.StopMovement(ControlBits.Front);
                    }, ct);

                await RunScenario("02_flat_run_backward", "Run backward on flat ground (5s)",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        _objectManager.StartMovement(ControlBits.Back);
                        await Task.Delay(5000, ct2);
                        _objectManager.StopMovement(ControlBits.Back);
                    }, ct);

                await RunScenario("03_standing_jump", "Jump from standstill on flat ground",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        _objectManager.Jump();
                        await Task.Delay(2000, ct2);
                    }, ct);

                await RunScenario("04_running_jump", "Jump while running forward on flat ground",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        _objectManager.StartMovement(ControlBits.Front);
                        await Task.Delay(500, ct2);
                        _objectManager.Jump();
                        await Task.Delay(2000, ct2);
                        _objectManager.StopMovement(ControlBits.Front);
                    }, ct);

                await RunScenario("05_fall_from_height", "Fall from 50 units above flat ground",
                    setup: async ct2 =>
                    {
                        // Teleport high above flat ground - DON'T settle (we want to capture the fall)
                        var cmd = $".go xyz {FlatX:F1} {FlatY:F1} {FallZ:F1} {MapId}";
                        DiagLog($"Teleporting for fall: {cmd}");
                        _objectManager.SendChatMessage(cmd);
                        await Task.Delay(1000, ct2); // Brief delay for teleport to register
                    },
                    execute: async ct2 =>
                    {
                        // Character should be falling from Z=62 to Z~10 (about 3s of free fall)
                        await Task.Delay(5000, ct2);
                    }, ct);

                await RunScenario("06_strafe_forward", "Run forward + strafe right (diagonal, 5s)",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        _objectManager.StartMovement(ControlBits.Front | ControlBits.StrafeRight);
                        await Task.Delay(5000, ct2);
                        _objectManager.StopMovement(ControlBits.Front | ControlBits.StrafeRight);
                    }, ct);

                await RunScenario("07_strafe_only", "Strafe right only (no forward, 5s)",
                    setup: async ct2 => { await TeleportAndSettle(FlatX, FlatY, FlatZ, ct2); SetFacing(0f); await Settle(ct2); },
                    execute: async ct2 =>
                    {
                        _objectManager.StartMovement(ControlBits.StrafeRight);
                        await Task.Delay(5000, ct2);
                        _objectManager.StopMovement(ControlBits.StrafeRight);
                    }, ct);

                await RunScenario("08_swim_forward", "Swim forward in Southfury River (5s)",
                    setup: async ct2 =>
                    {
                        await TeleportAndSettle(WaterX, WaterY, WaterZ, ct2);
                        SetFacing(0f); // Face north (along river)
                        await Settle(ct2);
                    },
                    execute: async ct2 =>
                    {
                        _objectManager.StartMovement(ControlBits.Front);
                        await Task.Delay(5000, ct2);
                        _objectManager.StopMovement(ControlBits.Front);
                    }, ct);

                await RunScenario("09_slope_uphill", "Run uphill on a slope (5s)",
                    setup: async ct2 =>
                    {
                        await TeleportAndSettle(SlopeX, SlopeY, SlopeZ, ct2);
                        SetFacing(2.356f); // ~135 degrees (northwest, toward higher ground)
                        await Settle(ct2);
                    },
                    execute: async ct2 =>
                    {
                        _objectManager.StartMovement(ControlBits.Front);
                        await Task.Delay(5000, ct2);
                        _objectManager.StopMovement(ControlBits.Front);
                    }, ct);

                await RunScenario("10_slope_downhill", "Run downhill on a slope (5s)",
                    setup: async ct2 =>
                    {
                        await TeleportAndSettle(SlopeX, SlopeY, SlopeZ + 20f, ct2);
                        SetFacing(5.497f); // ~315 degrees (southeast, toward lower ground)
                        await Settle(ct2);
                    },
                    execute: async ct2 =>
                    {
                        _objectManager.StartMovement(ControlBits.Front);
                        await Task.Delay(5000, ct2);
                        _objectManager.StopMovement(ControlBits.Front);
                    }, ct);

                DiagLog("All scenarios complete");
                _logger.LogInformation("=== AUTOMATED MOVEMENT RECORDING: All scenarios complete ===");
            }
            catch (OperationCanceledException)
            {
                DiagLog("Automated recording CANCELLED");
                _logger.LogWarning("Automated recording cancelled");
                _objectManager.StopAllMovement();
            }
            catch (Exception ex)
            {
                DiagLog($"Automated recording ERROR: {ex.Message}\n{ex.StackTrace}");
                _logger.LogError(ex, "Error during automated recording");
                _objectManager.StopAllMovement();
            }
        }

        private async Task RunScenario(string name, string description,
            Func<CancellationToken, Task> setup, Func<CancellationToken, Task> execute, CancellationToken ct)
        {
            DiagLog($"SCENARIO_START: {name} - {description}");
            _logger.LogInformation("SCENARIO_START: {Name} - {Description}", name, description);

            // Phase 1: Setup (teleport, face, settle) - NOT recorded
            _objectManager.StopAllMovement();
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
                _objectManager.StopAllMovement();
                await Task.Delay(500, ct); // Capture final position

                var filePath = _recorder.StopRecording();
                DiagLog($"SCENARIO_COMPLETE: {name} -> {filePath ?? "(no frames)"}");
                _logger.LogInformation("SCENARIO_COMPLETE: {Name} -> {FilePath}", name, filePath ?? "(no frames)");
            }

            await Task.Delay(2000, ct); // Settle between scenarios
        }

        private async Task TeleportAndSettle(float x, float y, float z, CancellationToken ct)
        {
            var cmd = $".go xyz {x:F1} {y:F1} {z:F1} {MapId}";
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

        private async Task Settle(CancellationToken ct)
        {
            // Brief pause to let client settle after teleport/facing change
            await Task.Delay(500, ct);
        }
    }
}
