using System;
using System.Diagnostics;
using System.IO;

namespace Tests.Infrastructure;

/// <summary>
/// Launches WoWStateManagerUI.exe as a sidecar process for integration tests so that
/// the operator console (auto-polling health checks for realmd/mangosd/SOAP/PathfindingService/
/// SceneDataService) is visible while a live test runs. Mirrors D2Bot's
/// <c>D2Orchestrator.Tests.Tasks.AutonomousNormalAct1Test.TryLaunchUiSidecar</c> pattern.
///
/// Lifecycle:
///   1. BotServiceFixture.InitializeAsync calls <see cref="TryLaunch"/> after StateManager
///      reaches ready state (port 9000 listening).
///   2. The UI launches with WWOW_UI_AUTOCONNECT=1 and WWOW_UI_STATEMANAGER_URL pointing
///      at the StateManager's protobuf port. The UI's HealthCheckService starts auto-
///      polling immediately on construction, so no manual Connect click is required.
///   3. BotServiceFixture.TeardownProcessesAsync calls <see cref="Stop"/> before killing
///      StateManager — keeps the UI from logging a disconnect storm on teardown.
///
/// Opt-out: set <c>WWOW_DISABLE_UI_SIDECAR=1</c> in environments without a desktop
/// (CI). The launcher returns null in that case and the fixture continues without
/// sidecar visibility.
///
/// Phase-3 follow-up: the UI today renders infrastructure health only; live bot-state
/// (current Activity, Objective, Task, Action) is the Phase 3 dashboard work tracked in
/// <c>docs/Plan/04_PHASE3_UI_DEFAULT.md</c>. The env-var contract here is forward-
/// compatible — once Phase 3 lands, the UI will consume WWOW_UI_STATEMANAGER_URL to
/// open a protobuf connection without code changes here.
/// </summary>
internal static class UiSidecarLauncher
{
    internal const string DisableEnvVar = "WWOW_DISABLE_UI_SIDECAR";
    internal const string StateManagerUrlEnvVar = "WWOW_UI_STATEMANAGER_URL";
    internal const string AutoConnectEnvVar = "WWOW_UI_AUTOCONNECT";
    internal const string RepoRootEnvVar = "WWOW_REPO_ROOT";

    private const string UiBinaryName = "WoWStateManagerUI.exe";
    private const string UiProjectName = "WoWStateManagerUI.csproj";
    private const string DefaultStateManagerUrl = "tcp://127.0.0.1:9000";

    /// <summary>
    /// Attempts to launch the UI sidecar. Returns null when:
    ///   - WWOW_DISABLE_UI_SIDECAR=1 (CI opt-out)
    ///   - the repo root can't be located
    ///   - neither the built exe nor the csproj fallback is found
    ///   - <c>Process.Start</c> throws
    ///
    /// All failure paths log via <paramref name="log"/> and return null without throwing —
    /// the fixture must continue to function without UI visibility.
    /// </summary>
    internal static Process? TryLaunch(Action<string> log)
    {
        if (Environment.GetEnvironmentVariable(DisableEnvVar) == "1")
        {
            log("  [UI sidecar] Suppressed via WWOW_DISABLE_UI_SIDECAR=1");
            return null;
        }

        string? repoRoot = TryFindRepoRoot();
        if (repoRoot is null)
        {
            log("  [UI sidecar] Could not locate WoWStateManagerUI project root — skipping");
            return null;
        }

        // Probe Debug and Release bin trees. The UI csproj targets net8.0-windows10.0.22621.0
        // but the exact TFM folder can drift across SDK updates; pick the newest matching exe.
        //
        // Order matters only as a tiebreaker — picker selects newest LastWriteTimeUtc across
        // all probes. Bot/{Release,Debug}/net8.0 is the shared output `dotnet test` populates
        // when the test project transitively rebuilds the UI; it tends to be the freshest.
        // The project-local bin/{Debug,Release} folders cover a standalone UI build and are
        // retained so a developer who built only the UI csproj still gets that binary.
        var uiProjectDir = Path.Combine(repoRoot, "UI", "WoWStateManagerUI");
        var probes = new[]
        {
            Path.Combine(repoRoot, "Bot", "Release", "net8.0"),
            Path.Combine(repoRoot, "Bot", "Debug", "net8.0"),
            Path.Combine(uiProjectDir, "bin", "Debug"),
            Path.Combine(uiProjectDir, "bin", "Release"),
        };

        FileInfo? bestExe = null;
        foreach (var root in probes)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var exe in Directory.EnumerateFiles(root, UiBinaryName, SearchOption.AllDirectories))
            {
                var fi = new FileInfo(exe);
                if (bestExe is null || fi.LastWriteTimeUtc > bestExe.LastWriteTimeUtc) bestExe = fi;
            }
        }

        ProcessStartInfo psi;
        if (bestExe is not null)
        {
            psi = new ProcessStartInfo
            {
                FileName = bestExe.FullName,
                WorkingDirectory = bestExe.DirectoryName ?? uiProjectDir,
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            log($"  [UI sidecar] Launching {bestExe.FullName}");
        }
        else
        {
            var csprojPath = Path.Combine(uiProjectDir, UiProjectName);
            if (!File.Exists(csprojPath))
            {
                log($"  [UI sidecar] Built exe not found and csproj missing: {csprojPath}");
                return null;
            }

            log($"  [UI sidecar] Built exe not found; falling back to dotnet run on {csprojPath}");
            psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(csprojPath);
            psi.ArgumentList.Add("--no-restore");
        }

        var stateManagerUrl = Environment.GetEnvironmentVariable(StateManagerUrlEnvVar)
                              ?? DefaultStateManagerUrl;
        psi.Environment[StateManagerUrlEnvVar] = stateManagerUrl;
        psi.Environment[AutoConnectEnvVar] = "1";
        psi.Environment[RepoRootEnvVar] = repoRoot;

        try
        {
            var process = Process.Start(psi);
            if (process is null)
            {
                log("  [UI sidecar] Process.Start returned null");
                return null;
            }

            log($"  [UI sidecar] Launched pid={process.Id} url={stateManagerUrl}");
            return process;
        }
        catch (Exception ex)
        {
            log($"  [UI sidecar] Launch failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gracefully stops a previously-launched sidecar. Safe to call with null, with an
    /// already-exited process, or repeatedly. Uses <c>process.Kill(entireProcessTree:true)</c>
    /// because the dotnet-run fallback path spawns a child dotnet host that doesn't exit
    /// when the parent is killed.
    /// </summary>
    internal static void Stop(Process? process, string reason, Action<string> log)
    {
        if (process is null) return;

        int pid = -1;
        try { pid = process.Id; } catch { /* may have exited */ }

        try
        {
            if (!process.HasExited)
            {
                log($"  [UI sidecar] Stopping pid={pid} reason={reason}");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            else
            {
                log($"  [UI sidecar] pid={pid} already exited reason={reason}");
            }
        }
        catch (Exception ex)
        {
            log($"  [UI sidecar] WARN teardown failed reason={reason}: {ex.Message}");
        }
        finally
        {
            try { process.Dispose(); } catch { }
        }
    }

    /// <summary>
    /// Walks up from the test assembly's base directory to find a folder containing
    /// <c>UI/WoWStateManagerUI/WoWStateManagerUI.csproj</c>. Returns null if the
    /// repo root can't be located within ~10 levels.
    /// </summary>
    private static string? TryFindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int depth = 0; depth < 12 && dir != null; depth++)
        {
            var candidate = Path.Combine(dir.FullName, "UI", "WoWStateManagerUI", UiProjectName);
            if (File.Exists(candidate)) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
