using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Infrastructure;

/// <summary>
/// PFS-OVERHAUL-006 (2026-05-07): waypoint-correctness fixture, sister to
/// <see cref="PathfindingTestFixture"/>. Provides a pathfinding service
/// instance whose Navigation.dll is pointed at the bot's test-data dir
/// (D:/wwow-bot/test-data by default) and listens on a dedicated port that
/// does not collide with either the Docker prod service (5001) or the
/// existing live-test fixture (5101).
///
/// Why a separate fixture instead of reusing <see cref="PathfindingTestFixture"/>:
/// waypoint-generation correctness tests run before any client boot. They
/// assert that the bake produced a real walkable corridor (not a synthetic
/// interpolation between dangling off-mesh anchors) for a given route.
/// Reusing the live-test fixture's port (5101) and process lifetime would
/// either (a) contend with a concurrent live test or (b) make the
/// correctness suite sit behind the live test's ~3-minute mmap preload.
/// Splitting the port + process lets the two suites run independently and
/// makes the data-dir flow explicit: this fixture always reads test-data,
/// the live fixture may read MaNGOS/data when the test-data first-path
/// latency bites.
///
/// Port allocation (also documented in docs/physics/MMAP_DATA_FLOW.md):
///   5001 — Docker prod (wwow-pathfinding container)
///   5101 — PathfindingTestFixture (live-bot tests)
///   5111 — PathfindingValidationFixture (waypoint-correctness tests)
///
/// Side-effect: this fixture sets <c>WWOW_DATA_DIR</c> on the test process
/// itself so direct P/Invoke calls into Navigation.dll (loaded into the
/// test process by <see cref="PathfindingService.Tests.NavigationInterop"/>)
/// resolve the same test-data tiles the spawned service is loading.
/// Navigation.dll's strict gate <c>std::exit(1)</c>s if WWOW_DATA_DIR is
/// unset at first P/Invoke, so this must run before any direct call.
/// </summary>
public sealed class PathfindingValidationFixture : IAsyncDisposable
{
    private const string EnableEnvVar = "WWOW_USE_VALIDATION_PATHFINDING_SERVICE";
    private const string PortEnvVar = "WWOW_VALIDATION_PATHFINDING_PORT";
    private const string DataDirEnvVar = "WWOW_VALIDATION_DATA_DIR";
    private const int DefaultValidationPort = 5111;
    private const int MaxStartupSeconds = 600;

    private Process? _process;
    private Action<string> _log;
    private volatile bool _preloadComplete;

    public int Port { get; private set; }
    public string DataDir { get; private set; } = string.Empty;

    /// <summary>
    /// xUnit-compatible parameterless constructor. Sets WWOW_DATA_DIR on the
    /// test process so direct P/Invoke into Navigation.dll resolves the
    /// validation data dir (test-data) instead of falling through to
    /// MaNGOS/data via NavigationFixture's auto-discovery. Does NOT spawn
    /// PathfindingService.exe — call <see cref="LaunchAsync(Action{string})"/>
    /// for socket-protocol validation tests that need the service process.
    /// </summary>
    public PathfindingValidationFixture()
    {
        Port = ResolveValidationPort();
        _log = _ => { };
        DataDir = ConfigureProcessDataDir();
    }

    private PathfindingValidationFixture(int port, Action<string> log)
    {
        Port = port;
        _log = log;
    }

    public static bool IsValidationServiceEnabled()
    {
        var v = Environment.GetEnvironmentVariable(EnableEnvVar);
        return string.Equals(v, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sets WWOW_DATA_DIR on the test process to the validation data dir
    /// (test-data by default) without spawning the service. Use when only
    /// direct P/Invoke correctness checks are needed and the spawned
    /// service would just add startup latency.
    /// </summary>
    public static string ConfigureProcessDataDir()
    {
        var dataDir = ResolveDataDir();
        EnsureDataDirShape(dataDir);
        Environment.SetEnvironmentVariable("WWOW_DATA_DIR", dataDir);
        return dataDir;
    }

    /// <summary>
    /// Spawns the validation pathfinding service on its dedicated port and
    /// waits for PRELOAD_COMPLETE. Use when a test wants to exercise the
    /// service's socket protocol against test-data without contention with
    /// the live-test fixture.
    /// </summary>
    public static async Task<PathfindingValidationFixture> LaunchAsync(Action<string> log)
    {
        var port = ResolveValidationPort();
        var fixture = new PathfindingValidationFixture(port, log);
        await fixture.StartAsync().ConfigureAwait(false);
        return fixture;
    }

    private static int ResolveValidationPort()
    {
        var configured = Environment.GetEnvironmentVariable(PortEnvVar);
        if (int.TryParse(configured, out var p) && p > 0 && p < 65536)
            return p;
        return DefaultValidationPort;
    }

    private static string ResolveDataDir()
    {
        // Priority (highest first):
        //   1. WWOW_VALIDATION_DATA_DIR — explicit override for this fixture.
        //   2. WWOW_DATA_DIR if pre-set to a valid data dir — honors the
        //      caller's external choice (e.g. PFS-OVERHAUL BRM property tests
        //      run with WWOW_DATA_DIR=D:\wwow-bot\prod-data to validate the
        //      live-FG bake instead of test-data). Before 2026-05-14 the
        //      fixture unconditionally overwrote WWOW_DATA_DIR with
        //      test-data, which made the documented data-source-aware recipe
        //      a silent no-op.
        //   3. WWOW_TEST_DATA_DIR — legacy alias.
        //   4. Default test-data root.
        var explicitOverride = Environment.GetEnvironmentVariable(DataDirEnvVar);
        if (!string.IsNullOrWhiteSpace(explicitOverride))
            return explicitOverride;

        var inheritedDataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(inheritedDataDir) && HasNavDataShape(inheritedDataDir))
            return inheritedDataDir;

        return Environment.GetEnvironmentVariable("WWOW_TEST_DATA_DIR")
            ?? @"D:\wwow-bot\test-data";
    }

    private static bool HasNavDataShape(string dataDir)
    {
        try
        {
            return Directory.Exists(Path.Combine(dataDir, "mmaps"))
                && Directory.Exists(Path.Combine(dataDir, "maps"))
                && Directory.Exists(Path.Combine(dataDir, "vmaps"));
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureDataDirShape(string dataDir)
    {
        if (!Directory.Exists(Path.Combine(dataDir, "mmaps"))
            || !Directory.Exists(Path.Combine(dataDir, "maps"))
            || !Directory.Exists(Path.Combine(dataDir, "vmaps")))
        {
            throw new InvalidOperationException(
                $"PathfindingValidationFixture: data dir '{dataDir}' is missing one of mmaps/ maps/ vmaps/. "
                + "See docs/physics/MMAP_DATA_FLOW.md (junction maps/ and vmaps/ from D:/MaNGOS/data, mmaps/ written by MmapGen).");
        }
    }

    private async Task StartAsync()
    {
        var exePath = ResolvePathfindingExe()
            ?? throw new InvalidOperationException(
                "PathfindingService.exe not found. Build the solution first; "
                + "the test fixture expects Bot/Release/net8.0/PathfindingService.exe.");
        var workingDir = Path.GetDirectoryName(exePath)!;

        DataDir = ResolveDataDir();
        EnsureDataDirShape(DataDir);

        // The test process's own Navigation.dll (loaded by direct P/Invoke
        // from NavigationInterop) needs to see the same data dir as the
        // spawned service. Set it before any P/Invoke fires.
        Environment.SetEnvironmentVariable("WWOW_DATA_DIR", DataDir);

        if (IsPortInUse(Port))
        {
            _log($"[PathfindingValidationFixture] WARNING: port {Port} already in use; "
                + "we'll skip spawning a new process and use whatever is listening. "
                + "Set WWOW_VALIDATION_PATHFINDING_PORT to a free port to spawn cleanly.");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        psi.Environment["WWOW_DATA_DIR"] = DataDir;
        psi.Environment["PathfindingService__IpAddress"] = "127.0.0.1";
        psi.Environment["PathfindingService__Port"] = Port.ToString();
        psi.Environment["WWOW_NAVIGATION_PRELOAD_MAPS"] = "all";
        psi.Environment["Navigation__PreloadMaps"] = "all";
        psi.Environment["Navigation__RunStartupDiagnostics"] = "false";
        psi.Environment["Navigation__EnableDynamicObjectOverlay"] = "false";
        psi.Environment["WWOW_ENABLE_PATHFINDING_DYNAMIC_OVERLAY"] = "0";

        // Phase 5.3.6 freeze: route pack and repair pipeline OFF. Validation
        // tests assert pure-Detour behavior; any repair would mask bake bugs.
        psi.Environment["WWOW_ENABLE_STATIC_ROUTE_PACK"] = "0";
        psi.Environment["WWOW_ENABLE_PATH_REPAIR"] = "0";
        psi.Environment["WWOW_ROUTE_PACK_STARTUP_WARMUP"] = "0";

        _log($"[PathfindingValidationFixture] launching {exePath} on port {Port} (data={DataDir})");
        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Process.Start returned null for PathfindingService.exe.");

        _process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            _log($"[validate-pf-stdout] {e.Data}");
            if (e.Data.Contains("PRELOAD_COMPLETE", StringComparison.Ordinal))
                _preloadComplete = true;
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _log($"[validate-pf-stderr] {e.Data}");
        };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitForReadyAsync().ConfigureAwait(false);
        _log($"[PathfindingValidationFixture] ready on port {Port} (PID {_process.Id})");
    }

    private async Task WaitForReadyAsync()
    {
        var sw = Stopwatch.StartNew();
        var portAcceptedAt = TimeSpan.Zero;
        while (sw.Elapsed < TimeSpan.FromSeconds(MaxStartupSeconds))
        {
            if (_process?.HasExited == true)
                throw new InvalidOperationException(
                    $"PathfindingService.exe (validation) exited prematurely (code {_process.ExitCode}) before reaching ready.");

            if (portAcceptedAt == TimeSpan.Zero)
            {
                if (await IsPortAcceptingAsync(Port, 1000).ConfigureAwait(false))
                {
                    portAcceptedAt = sw.Elapsed;
                    _log($"[PathfindingValidationFixture] port {Port} accepting at {portAcceptedAt.TotalSeconds:F1}s; waiting for PRELOAD_COMPLETE...");
                }
                else
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    continue;
                }
            }

            if (_preloadComplete)
            {
                _log($"[PathfindingValidationFixture] PRELOAD_COMPLETE seen at {sw.Elapsed.TotalSeconds:F1}s (port-accept->preload {(sw.Elapsed - portAcceptedAt).TotalSeconds:F1}s)");
                return;
            }

            await Task.Delay(250).ConfigureAwait(false);
        }
        var phase = _preloadComplete ? "post-preload" : (portAcceptedAt == TimeSpan.Zero ? "port-accept" : "preload");
        throw new TimeoutException(
            $"PathfindingService.exe (validation) did not become ready (phase={phase}) on port {Port} within {MaxStartupSeconds}s.");
    }

    private static string? ResolvePathfindingExe()
    {
        var baseDir = AppContext.BaseDirectory;
        var direct = Path.Combine(baseDir, "PathfindingService.exe");
        if (File.Exists(direct)) return direct;

        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Bot", "Release", "net8.0", "PathfindingService.exe");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    private static async Task<bool> IsPortAcceptingAsync(int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(IPAddress.Loopback, port, cts.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_process == null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                _log($"[PathfindingValidationFixture] stopping PID {_process.Id}");
                _process.Kill(entireProcessTree: true);
                await Task.Run(() => _process.WaitForExit(5000)).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _log($"[PathfindingValidationFixture] dispose error: {ex.Message}");
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }
}
