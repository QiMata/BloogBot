using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using GameData.Core.Models;
using PathfindingService.Repository;

namespace WwowRecastBridge;

internal static class Program
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static bool resolverRegistered;

    private static int Main(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            if (!options.EnvReady && NeedsEnvironmentRelaunch(options.DataDir))
            {
                return RelaunchWithEnvironment(args, options.DataDir);
            }

            RegisterNavigationResolver();
            SetProcessEnvironment("WWOW_DATA_DIR", options.DataDir);
            SetDataDirectoryNative(options.DataDir);

            var navigation = new Navigation();
            var result = options.Mode == QueryMode.Validated
                ? navigation.CalculateValidatedPath(
                    options.MapId,
                    options.Start,
                    options.End,
                    options.SmoothPath,
                    options.AgentRadius,
                    options.AgentHeight)
                : navigation.CalculateRawPath(
                    options.MapId,
                    options.Start,
                    options.End,
                    options.SmoothPath,
                    options.AgentRadius,
                    options.AgentHeight);

            WriteSuccess(options.Mode, result);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("STATUS error");
            Console.WriteLine($"MESSAGE {Sanitize(ex.Message)}");
            return 1;
        }
    }

    private static bool NeedsEnvironmentRelaunch(string dataDir)
    {
        var current = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        return !string.Equals(
            NormalizeDirectory(current),
            NormalizeDirectory(dataDir),
            StringComparison.OrdinalIgnoreCase);
    }

    private static int RelaunchWithEnvironment(string[] args, string dataDir)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Unable to determine the bridge executable path for env relaunch.");
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppContext.BaseDirectory,
            }
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }
        process.StartInfo.ArgumentList.Add("--env-ready");
        process.StartInfo.Environment["WWOW_DATA_DIR"] = dataDir;

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start the env-prepared bridge child process.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        Console.Out.Write(stdoutTask.GetAwaiter().GetResult());
        Console.Error.Write(stderrTask.GetAwaiter().GetResult());
        return process.ExitCode;
    }

    private static string NormalizeDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var fullPath = Path.GetFullPath(path.Trim().Trim('"'));
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void RegisterNavigationResolver()
    {
        if (resolverRegistered)
        {
            return;
        }

        NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, ResolveNavigationLibrary);
        NativeLibrary.SetDllImportResolver(typeof(Navigation).Assembly, ResolveNavigationLibrary);
        resolverRegistered = true;
    }

    private static IntPtr ResolveNavigationLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals("Navigation", StringComparison.OrdinalIgnoreCase)
            && !libraryName.Equals("Navigation.dll", StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in EnumerateNavigationDllCandidates())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                if (NativeLibrary.TryLoad(candidate, out var handle))
                {
                    return handle;
                }
            }
            catch (Exception)
            {
                // Keep walking candidates until we find an x64-compatible load.
            }
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> EnumerateNavigationDllCandidates()
    {
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<string>();
        void Push(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var fullPath = Path.GetFullPath(path);
            if (emitted.Add(fullPath))
            {
                candidates.Add(fullPath);
            }
        }

        var baseDir = AppContext.BaseDirectory;
        Push(Path.Combine(baseDir, "Navigation.dll"));
        Push(Path.Combine(baseDir, "x64", "Navigation.dll"));

        var repoRoot = FindRepoRoot();
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            Push(Path.Combine(repoRoot, "Bot", "Release", "net8.0", "Navigation.dll"));
            Push(Path.Combine(repoRoot, "Bot", "Release", "net8.0", "x64", "Navigation.dll"));
            Push(Path.Combine(repoRoot, "Exports", "Navigation", "cmake_build_x64", "Release", "Navigation.dll"));
            Push(Path.Combine(repoRoot, "Exports", "Navigation", "cmake_build_x64", "Debug", "Navigation.dll"));
        }

        foreach (var candidate in candidates)
        {
            yield return candidate;
        }
    }

    private static string? FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WestworldOfWarcraft.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static void SetProcessEnvironment(string key, string value)
    {
        Environment.SetEnvironmentVariable(key, value);
        var result = _putenv($"{key}={value}");
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to set native environment variable '{key}'.");
        }
    }

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int _putenv(string envString);

    [DllImport("Navigation.dll", EntryPoint = "SetDataDirectory", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void SetDataDirectoryNative(string dataDir);

    private static void WriteSuccess(QueryMode mode, NavigationPathResult result)
    {
        Console.WriteLine("STATUS ok");
        Console.WriteLine($"MODE {mode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"RESULT {Sanitize(result.Result)}");
        Console.WriteLine($"PATH_COUNT {result.Path.Length}");
        Console.WriteLine($"RAW_PATH_COUNT {result.RawPath.Length}");
        Console.WriteLine($"BLOCKED_SEGMENT {result.BlockedSegmentIndex.GetValueOrDefault(-1)}");
        Console.WriteLine($"BLOCKED_REASON {Sanitize(result.BlockedReason)}");

        for (int i = 0; i < result.Path.Length; i++)
        {
            WritePoint("PATH", i, result.Path[i]);
        }

        for (int i = 0; i < result.RawPath.Length; i++)
        {
            WritePoint("RAW", i, result.RawPath[i]);
        }

        var summary = $"result={result.Result} validatedCorners={result.Path.Length} rawCorners={result.RawPath.Length}";
        if (result.BlockedSegmentIndex is int blockedSegmentIndex)
        {
            summary += $" blockedSegment={blockedSegmentIndex}";
        }
        if (!string.IsNullOrWhiteSpace(result.BlockedReason) && !string.Equals(result.BlockedReason, "none", StringComparison.OrdinalIgnoreCase))
        {
            summary += $" blockedReason={result.BlockedReason}";
        }

        Console.WriteLine($"SUMMARY {Sanitize(summary)}");
    }

    private static void WritePoint(string prefix, int index, XYZ point)
    {
        Console.WriteLine(
            string.Create(
                Invariant,
                $"{prefix} {index} {point.X:F4} {point.Y:F4} {point.Z:F4}"));
    }

    private static string Sanitize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "none"
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private enum QueryMode
    {
        Raw,
        Validated,
    }

    private sealed record Options(
        QueryMode Mode,
        uint MapId,
        XYZ Start,
        XYZ End,
        float AgentRadius,
        float AgentHeight,
        bool SmoothPath,
        string DataDir,
        bool EnvReady)
    {
        public static Options Parse(string[] args)
        {
            QueryMode mode = QueryMode.Validated;
            int mapId = -1;
            XYZ? start = null;
            XYZ? end = null;
            float radius = 1.0247f;
            float height = 2.625f;
            bool smooth = false;
            string dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR") ?? string.Empty;
            bool envReady = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--mode":
                        mode = ParseMode(Next(args, ref i, "--mode"));
                        break;
                    case "--map":
                        mapId = int.Parse(Next(args, ref i, "--map"), Invariant);
                        break;
                    case "--start":
                        start = ParsePoint(Next(args, ref i, "--start"));
                        break;
                    case "--end":
                        end = ParsePoint(Next(args, ref i, "--end"));
                        break;
                    case "--radius":
                        radius = float.Parse(Next(args, ref i, "--radius"), Invariant);
                        break;
                    case "--height":
                        height = float.Parse(Next(args, ref i, "--height"), Invariant);
                        break;
                    case "--smooth":
                        smooth = true;
                        break;
                    case "--data-dir":
                        dataDir = Next(args, ref i, "--data-dir");
                        break;
                    case "--env-ready":
                        envReady = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument '{args[i]}'.");
                }
            }

            if (mapId < 0 && start is null && end is null)
            {
                throw new ArgumentException("Expected --map, --start, and --end.");
            }
            if (mapId < 0)
            {
                throw new ArgumentException("Expected a non-negative --map id.");
            }
            if (start is null || end is null)
            {
                throw new ArgumentException("Expected both --start and --end.");
            }
            if (string.IsNullOrWhiteSpace(dataDir))
            {
                throw new ArgumentException("Expected --data-dir or WWOW_DATA_DIR.");
            }

            return new Options(mode, checked((uint)mapId), start.Value, end.Value, radius, height, smooth, dataDir, envReady);
        }

        private static QueryMode ParseMode(string raw)
            => raw.ToLowerInvariant() switch
            {
                "raw" => QueryMode.Raw,
                "validated" => QueryMode.Validated,
                _ => throw new ArgumentException($"Unsupported mode '{raw}'. Expected raw or validated."),
            };

        private static XYZ ParsePoint(string raw)
        {
            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                throw new ArgumentException($"Expected X,Y,Z point, got '{raw}'.");
            }

            return new XYZ(
                float.Parse(parts[0], Invariant),
                float.Parse(parts[1], Invariant),
                float.Parse(parts[2], Invariant));
        }

        private static string Next(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {option}.");
            }

            index++;
            return args[index];
        }
    }
}
