using System.Diagnostics;
using System.Text;
using Navigation.Physics.Tests;

internal static class Program
{
    public static int Main(string[] args)
    {
        string command = args.Length > 0 ? args[0].ToLowerInvariant() : "summary";
        bool overwrite = args.Any(arg => string.Equals(arg, "--overwrite", StringComparison.OrdinalIgnoreCase));

        try
        {
            return command switch
            {
                "summary" => RunSummary(),
                "write-sidecars" => WriteSidecars(overwrite),
                "cleanup-output-copies" => CleanupOutputCopies(),
                "compact" => RunCompact(overwrite),
                "capture" => RunCapture(args.Skip(1).ToArray()),
                _ => PrintUsage(),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int RunCompact(bool overwrite)
    {
        Console.WriteLine("== Before ==");
        RunSummary();
        Console.WriteLine();

        WriteSidecars(overwrite);
        Console.WriteLine();

        CleanupOutputCopies();
        Console.WriteLine();

        Console.WriteLine("== After ==");
        return RunSummary();
    }

    private static int RunSummary()
    {
        string canonicalDir = RecordingLoader.GetRecordingsDirectory();
        var entries = RecordingLoader.GetRecordingFiles();
        long canonicalBytes = entries.Sum(entry =>
            SafeFileLength(entry.JsonPath) + SafeFileLength(entry.ProtoPath));

        Console.WriteLine($"Canonical dir: {canonicalDir}");
        Console.WriteLine($"Logical recordings: {entries.Count}");
        Console.WriteLine($"Canonical size: {FormatBytes(canonicalBytes)}");
        Console.WriteLine();
        Console.WriteLine("Name\tFrames\tPackets\tJSON\tBIN");
        foreach (var entry in entries)
        {
            int frameCount = -1;
            int packetCount = -1;
            try
            {
                var recording = RecordingLoader.LoadFromFile(entry.PreferredPath);
                frameCount = recording.Frames.Count;
                packetCount = recording.Packets.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{entry.Name}\tload-failed\t{ex.Message}");
                continue;
            }

            Console.WriteLine($"{entry.Name}\t{frameCount}\t{packetCount}\t{FormatBytes(SafeFileLength(entry.JsonPath))}\t{FormatBytes(SafeFileLength(entry.ProtoPath))}");
        }

        foreach (var duplicateDir in GetDuplicateOutputDirs())
        {
            Console.WriteLine();
            Console.WriteLine($"{duplicateDir}: {(Directory.Exists(duplicateDir) ? FormatBytes(GetDirectoryBytes(duplicateDir)) : "missing")}");
        }

        return 0;
    }

    private static int WriteSidecars(bool overwrite)
    {
        var entries = RecordingLoader.GetRecordingFiles();
        int written = 0;
        int skipped = 0;

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.JsonPath))
            {
                skipped++;
                continue;
            }

            string targetPath = Path.ChangeExtension(entry.JsonPath, ".bin");
            bool exists = File.Exists(targetPath);
            bool upToDate = exists &&
                File.GetLastWriteTimeUtc(targetPath) >= File.GetLastWriteTimeUtc(entry.JsonPath);
            if (upToDate && !overwrite)
            {
                skipped++;
                continue;
            }

            RecordingLoader.WriteProtoCompanion(entry.JsonPath, overwrite);
            written++;
            Console.WriteLine($"{(exists ? "refreshed" : "created")} {Path.GetFileName(targetPath)}");
        }

        Console.WriteLine($"Sidecar write complete: written={written}, skipped={skipped}");
        return 0;
    }

    private static int CleanupOutputCopies()
    {
        foreach (var duplicateDir in GetDuplicateOutputDirs())
        {
            if (!Directory.Exists(duplicateDir))
            {
                Console.WriteLine($"skip {duplicateDir} (missing)");
                continue;
            }

            long bytes = GetDirectoryBytes(duplicateDir);
            Directory.Delete(duplicateDir, recursive: true);
            Console.WriteLine($"deleted {duplicateDir} ({FormatBytes(bytes)})");
        }

        return 0;
    }

    private static int RunCapture(string[] args)
    {
        var options = ParseCaptureOptions(args);
        if (options.Scenarios.Count == 0)
            throw new ArgumentException("capture requires at least one scenario via --scenarios.");

        string repoRoot = ResolveRepoRoot();
        string recordingsDir = RecordingLoader.GetRecordingsDirectory();
        string stateManagerExe = ResolveStateManagerExe(repoRoot, options.Configuration);
        string wowExePath = ResolveWowExePath();
        string? mangosDirectory = ResolveMangosDirectory();
        string wowLogsDir = Path.Combine(Path.GetDirectoryName(wowExePath)!, "WWoWLogs");
        string scenarioLogPath = Path.Combine(wowLogsDir, "scenario_runner.log");
        string foregroundLogPath = Path.Combine(wowLogsDir, "foreground_bot_debug.log");
        string tempSettingsPath = Path.Combine(Path.GetTempPath(), $"wwow_fg_recording_{Guid.NewGuid():N}.json");
        string stdoutLogPath = Path.Combine(Path.GetTempPath(), $"wwow_sm_stdout_{Guid.NewGuid():N}.log");
        string stderrLogPath = Path.Combine(Path.GetTempPath(), $"wwow_sm_stderr_{Guid.NewGuid():N}.log");
        var baselineUtc = DateTime.UtcNow;
        var scenarioSelection = string.Join(",", options.Scenarios);

        Console.WriteLine($"StateManager: {stateManagerExe}");
        Console.WriteLine($"WoW.exe:      {wowExePath}");
        Console.WriteLine($"MaNGOS dir:   {mangosDirectory ?? "(default appsettings)"}");
        Console.WriteLine($"Recordings:   {recordingsDir}");
        Console.WriteLine($"Scenarios:    {scenarioSelection}");
        Console.WriteLine($"Timeout:      {options.TimeoutMinutes} minute(s)");

        Directory.CreateDirectory(recordingsDir);
        Directory.CreateDirectory(wowLogsDir);
        TryDeleteFile(scenarioLogPath);
        TryDeleteFile(foregroundLogPath);

        File.WriteAllText(tempSettingsPath, """
            [
              {
                "AccountName": "TESTBOT1",
                "Openness": 1.0,
                "Conscientiousness": 1.0,
                "Extraversion": 1.0,
                "Agreeableness": 1.0,
                "Neuroticism": 1.0,
                "ShouldRun": true,
                "RunnerType": "Foreground"
              }
            ]
            """);

        var baselineProcessIds = CaptureRelevantProcessIds();
        var trackedProcessIds = new HashSet<int>();

        var stdoutLock = new object();
        var stderrLock = new object();
        using var stdoutWriter = new StreamWriter(stdoutLogPath, append: false, Encoding.UTF8);
        using var stderrWriter = new StreamWriter(stderrLogPath, append: false, Encoding.UTF8);
        using var process = new Process
        {
            StartInfo = BuildCaptureStartInfo(
                stateManagerExe,
                recordingsDir,
                tempSettingsPath,
                wowExePath,
                mangosDirectory,
                scenarioSelection),
        };

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
                return;

            lock (stdoutLock)
            {
                stdoutWriter.WriteLine(eventArgs.Data);
                stdoutWriter.Flush();
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
                return;

            lock (stderrLock)
            {
                stderrWriter.WriteLine(eventArgs.Data);
                stderrWriter.Flush();
            }
        };

        bool[] completed = new bool[options.Scenarios.Count];
        string lastStatus = string.Empty;
        bool sawScenarioError = false;

        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Failed to start WoWStateManager.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Console.WriteLine($"Started WoWStateManager PID {process.Id}");

            var deadlineUtc = DateTime.UtcNow.AddMinutes(options.TimeoutMinutes);
            while (DateTime.UtcNow < deadlineUtc)
            {
                if (process.HasExited)
                    break;

                CaptureNewProcessIds(baselineProcessIds, trackedProcessIds);

                if (TryReadText(scenarioLogPath, out string scenarioLogText))
                {
                    for (int i = 0; i < options.Scenarios.Count; i++)
                    {
                        if (scenarioLogText.Contains($"SCENARIO_COMPLETE: {options.Scenarios[i]}", StringComparison.Ordinal))
                            completed[i] = true;
                    }

                    string statusLines = string.Join(
                        " || ",
                        scenarioLogText
                            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                            .Where(line =>
                                line.Contains("SCENARIO_", StringComparison.Ordinal) ||
                                line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                                line.Contains("TimeoutException", StringComparison.Ordinal))
                            .TakeLast(6));

                    if (!string.IsNullOrWhiteSpace(statusLines) &&
                        !string.Equals(statusLines, lastStatus, StringComparison.Ordinal))
                    {
                        Console.WriteLine(statusLines);
                        lastStatus = statusLines;
                    }

                    if (scenarioLogText.Contains("Automated recording ERROR", StringComparison.OrdinalIgnoreCase) ||
                        scenarioLogText.Contains("TimeoutException", StringComparison.Ordinal))
                    {
                        sawScenarioError = true;
                        break;
                    }

                    if (completed.All(value => value))
                        break;
                }

                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
        }
        finally
        {
            CaptureNewProcessIds(baselineProcessIds, trackedProcessIds);
            TryStopProcessTree(process);
            TryKillTrackedProcesses(trackedProcessIds, process.Id);
            TryDeleteFile(tempSettingsPath);
        }

        Console.WriteLine();
        Console.WriteLine($"Scenario log:   {scenarioLogPath}");
        Console.WriteLine($"Foreground log: {foregroundLogPath}");
        Console.WriteLine($"Stdout log:     {stdoutLogPath}");
        Console.WriteLine($"Stderr log:     {stderrLogPath}");

        PrintNewRecordingSummary(recordingsDir, baselineUtc);
        Console.WriteLine();
        CleanupOutputCopies();

        if (sawScenarioError || !completed.All(value => value))
        {
            Console.WriteLine();
            Console.WriteLine("Scenario log tail:");
            PrintFileTail(scenarioLogPath, 40);
            Console.WriteLine();
            Console.WriteLine("Foreground log tail:");
            PrintFileTail(foregroundLogPath, 40);
            throw new TimeoutException(sawScenarioError
                ? "Automated recording run failed inside the injected scenario runner."
                : "Automated recording run did not complete all requested scenarios.");
        }

        return 0;
    }

    private static IEnumerable<string> GetDuplicateOutputDirs()
    {
        string repoRoot = ResolveRepoRoot();
        yield return Path.Combine(repoRoot, "Bot", "Debug", "net8.0", "Recordings");
        yield return Path.Combine(repoRoot, "Bot", "Release", "net8.0", "Recordings");
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Tests", "Navigation.Physics.Tests")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve repo root from tool output path.");
    }

    private static ProcessStartInfo BuildCaptureStartInfo(
        string stateManagerExe,
        string recordingsDir,
        string tempSettingsPath,
        string wowExePath,
        string? mangosDirectory,
        string scenarioSelection)
    {
        var psi = new ProcessStartInfo
        {
            FileName = stateManagerExe,
            WorkingDirectory = Path.GetDirectoryName(stateManagerExe)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        psi.Environment["Logging__LogLevel__Default"] = "Information";
        psi.Environment["BLOOGBOT_AUTOMATED_RECORDING"] = "1";
        psi.Environment["WWOW_AUTOMATED_SCENARIOS"] = scenarioSelection;
        psi.Environment["WWOW_RECORDINGS_DIR"] = recordingsDir;
        psi.Environment["WWOW_SETTINGS_OVERRIDE"] = tempSettingsPath;
        psi.Environment["WWOW_WOW_EXE_PATH"] = wowExePath;

        if (!string.IsNullOrWhiteSpace(mangosDirectory))
            psi.Environment["MangosServer__MangosDirectory"] = mangosDirectory;

        return psi;
    }

    private static CaptureOptions ParseCaptureOptions(string[] args)
    {
        var options = new CaptureOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--scenarios":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--scenarios requires a comma-separated value.");
                    options.Scenarios.AddRange(ParseScenarioList(args[++i]));
                    break;
                case "--timeout-minutes":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out int timeoutMinutes) || timeoutMinutes <= 0)
                        throw new ArgumentException("--timeout-minutes requires a positive integer.");
                    options.TimeoutMinutes = timeoutMinutes;
                    break;
                case "--configuration":
                    if (i + 1 >= args.Length)
                        throw new ArgumentException("--configuration requires Debug or Release.");
                    options.Configuration = args[++i];
                    break;
                default:
                    throw new ArgumentException($"Unknown capture argument '{arg}'.");
            }
        }

        options.Scenarios = options.Scenarios
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return options;
    }

    private static IEnumerable<string> ParseScenarioList(string value)
        => value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token));

    private static string ResolveStateManagerExe(string repoRoot, string configuration)
    {
        string preferred = Path.Combine(repoRoot, "Bot", configuration, "net8.0", "WoWStateManager.exe");
        if (File.Exists(preferred))
            return preferred;

        throw new FileNotFoundException($"WoWStateManager.exe not found at '{preferred}'. Build Services/WoWStateManager first.");
    }

    private static string ResolveWowExePath()
    {
        string? envPath = Environment.GetEnvironmentVariable("WWOW_WOW_EXE_PATH");
        foreach (string candidate in new[]
        {
            envPath ?? string.Empty,
            @"D:\World of Warcraft\WoW.exe",
            @"C:\World of Warcraft\WoW.exe",
            @"C:\Games\World of Warcraft\WoW.exe",
            @"C:\Games\WoW-1.12.1\WoW.exe",
        })
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("Could not locate WoW.exe. Set WWOW_WOW_EXE_PATH to the client path.");
    }

    private static string? ResolveMangosDirectory()
    {
        string? envDir = Environment.GetEnvironmentVariable("MangosServer__MangosDirectory");
        foreach (string candidate in new[]
        {
            envDir ?? string.Empty,
            @"D:\MaNGOS",
            @"D:\vmangos-server",
            @"C:\Mangos\server",
        })
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static bool TryReadText(string path, out string text)
    {
        text = string.Empty;
        if (!File.Exists(path))
            return false;

        try
        {
            text = File.ReadAllText(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryStopProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
        }
        catch
        {
        }
    }

    private static HashSet<int> CaptureRelevantProcessIds()
    {
        var ids = new HashSet<int>();
        foreach (string processName in RelevantProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    ids.Add(process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return ids;
    }

    private static void CaptureNewProcessIds(HashSet<int> baselineIds, HashSet<int> trackedIds)
    {
        foreach (string processName in RelevantProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (!baselineIds.Contains(process.Id))
                        trackedIds.Add(process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    private static void TryKillTrackedProcesses(HashSet<int> trackedIds, int rootProcessId)
    {
        foreach (int pid in trackedIds.OrderByDescending(id => id))
        {
            if (pid == rootProcessId)
                continue;

            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                    continue;

                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
            catch
            {
            }
        }
    }

    private static void PrintNewRecordingSummary(string recordingsDir, DateTime baselineUtc)
    {
        var candidates = Directory.EnumerateFiles(recordingsDir, "*", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                string extension = Path.GetExtension(path);
                return extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                       extension.Equals(".bin", StringComparison.OrdinalIgnoreCase);
            })
            .Where(path => File.GetLastWriteTimeUtc(path) >= baselineUtc.AddSeconds(-5))
            .ToList();

        if (candidates.Count == 0)
        {
            Console.WriteLine("New recordings: none");
            return;
        }

        Console.WriteLine("New recordings:");
        foreach (var entry in candidates
                     .GroupBy(path => Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path)),
                         StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            string jsonPath = entry.FirstOrDefault(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) ??
                              string.Empty;
            string binPath = entry.FirstOrDefault(path => path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) ??
                             string.Empty;
            string preferredPath = !string.IsNullOrEmpty(binPath) ? binPath : jsonPath;
            string fileName = Path.GetFileName(preferredPath);

            try
            {
                var recording = RecordingLoader.LoadFromFile(preferredPath);
                Console.WriteLine($"  {fileName}: frames={recording.Frames.Count}, packets={recording.Packets.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  {fileName}: load failed ({ex.Message})");
            }
        }
    }

    private static void PrintFileTail(string path, int lineCount)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine("  (missing)");
            return;
        }

        foreach (string line in File.ReadLines(path).TakeLast(lineCount))
            Console.WriteLine(line);
    }

    private static long GetDirectoryBytes(string path)
        => Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length)
            : 0L;

    private static long SafeFileLength(string? path)
        => !string.IsNullOrEmpty(path) && File.Exists(path)
            ? new FileInfo(path).Length
            : 0L;

    private static string FormatBytes(long bytes)
    {
        const double kib = 1024.0;
        const double mib = kib * 1024.0;
        const double gib = mib * 1024.0;

        if (bytes >= gib)
            return $"{bytes / gib:F2} GiB";
        if (bytes >= mib)
            return $"{bytes / mib:F2} MiB";
        if (bytes >= kib)
            return $"{bytes / kib:F2} KiB";
        return $"{bytes} B";
    }

    private static int PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project Tools/RecordingMaintenance/RecordingMaintenance.csproj -- [summary|write-sidecars|cleanup-output-copies|compact] [--overwrite]");
        Console.WriteLine("  dotnet run --project Tools/RecordingMaintenance/RecordingMaintenance.csproj -- capture --scenarios <name1,name2> [--timeout-minutes 30] [--configuration Release]");
        return 1;
    }

    private static readonly string[] RelevantProcessNames =
    [
        "WoW",
        "WoWStateManager",
        "PathfindingService",
        "realmd",
        "mangosd",
    ];

    private sealed class CaptureOptions
    {
        public List<string> Scenarios { get; set; } = [];
        public int TimeoutMinutes { get; set; } = 30;
        public string Configuration { get; set; } = "Release";
    }
}
