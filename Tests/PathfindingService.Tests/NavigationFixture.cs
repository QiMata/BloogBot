using PathfindingService.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PathfindingService.Tests;

/// <summary>
/// xUnit fixture that provides a shared <see cref="Navigation"/> instance.
/// Performs preflight checks for Navigation.dll and nav data (mmaps/).
/// Used by: PathfindingTests, PathfindingBotTaskTests.
/// </summary>
public class NavigationFixture : IDisposable
{
    public Navigation Navigation { get; }

    public NavigationFixture()
    {
        EnsureDataDir();
        VerifyNavigationDll();
        VerifyNavDataExists();

        Navigation = new Navigation();
    }

    private static void VerifyNavigationDll()
    {
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var testOutputDir = Path.GetDirectoryName(assemblyLocation);

        if (testOutputDir == null)
            throw new InvalidOperationException("Cannot determine test output directory");

        var navigationDllPath = Path.Combine(testOutputDir, "Navigation.dll");

        if (!File.Exists(navigationDllPath))
        {
            throw new FileNotFoundException(
                $"Navigation.dll not found in test output directory: {testOutputDir}\n" +
                "Please ensure Navigation.vcxproj is built for the correct platform (x64) and configuration (Debug/Release).\n" +
                "The native DLL output path should be: ..\\..\\Bot\\$(Configuration)\\net8.0\\");
        }
    }

    private static void VerifyNavDataExists()
    {
        var dataRoot = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        string resolvedRoot;

        if (!string.IsNullOrEmpty(dataRoot))
        {
            resolvedRoot = dataRoot;
        }
        else
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            resolvedRoot = Path.GetDirectoryName(assemblyLocation)
                ?? throw new InvalidOperationException("Cannot determine test output directory");
        }

        // Validate all three required nav data subdirectories
        var requiredDirs = new[] { "maps", "vmaps", "mmaps" };
        var missing = new System.Collections.Generic.List<string>();

        foreach (var dir in requiredDirs)
        {
            if (!Directory.Exists(Path.Combine(resolvedRoot, dir)))
                missing.Add(dir);
        }

        if (missing.Count > 0)
        {
            throw new DirectoryNotFoundException(
                $"Navigation data incomplete at: {resolvedRoot}\n" +
                $"Missing directories: {string.Join(", ", missing)}\n" +
                "Please either:\n" +
                "  1. Set WWOW_DATA_DIR environment variable to point to your nav data root, or\n" +
                "  2. Run setup.ps1 to copy nav data to the test output directory, or\n" +
                $"  3. Manually copy maps/, vmaps/, and mmaps/ to: {resolvedRoot}");
        }

        var mmapsPath = Path.Combine(resolvedRoot, "mmaps");
        var mmtileFiles = Directory.GetFiles(mmapsPath, "*.mmtile");
        if (mmtileFiles.Length == 0)
        {
            throw new FileNotFoundException(
                $"No .mmtile files found in: {mmapsPath}\n" +
                "The mmaps directory exists but appears to be empty.\n" +
                "Run setup.ps1 to provision navigation data.");
        }
    }

    /// <summary>
    /// Auto-discovers WWOW_DATA_DIR from the Bot build output directory
    /// (same logic as Navigation.Physics.Tests.PhysicsEngineFixture).
    /// </summary>
    private static void EnsureDataDir()
    {
        if (IsUsableNavDataRoot(Environment.GetEnvironmentVariable("WWOW_DATA_DIR")))
            return;

        foreach (var dir in EnumerateNavDataCandidates())
        {
            if (IsUsableNavDataRoot(dir))
            {
                Environment.SetEnvironmentVariable("WWOW_DATA_DIR", Path.GetFullPath(dir!));
                return;
            }
        }
    }

    private static IEnumerable<string?> EnumerateNavDataCandidates()
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        yield return Environment.GetEnvironmentVariable("WWOW_VMANGOS_DATA_DIR");
        yield return Path.Combine(baseDir, "Data");
        yield return baseDir;

        foreach (var ancestor in EnumerateAncestors(baseDir))
        {
            yield return ancestor;
            yield return Path.Combine(ancestor, "Data");
            yield return Path.Combine(ancestor, "Bot", "Debug", "net8.0");
            yield return Path.Combine(ancestor, "Bot", "Release", "net8.0");
        }

        foreach (var driveRoot in EnumerateReadyDriveRoots())
        {
            yield return Path.Combine(driveRoot, "MaNGOS", "data");
            yield return Path.Combine(driveRoot, "Mangos", "data");
            yield return Path.Combine(driveRoot, "mangos", "data");
        }
    }

    private static IEnumerable<string> EnumerateAncestors(string path)
    {
        DirectoryInfo? current;
        try
        {
            current = new DirectoryInfo(path);
        }
        catch
        {
            yield break;
        }

        while (current != null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static IEnumerable<string> EnumerateReadyDriveRoots()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch
        {
            yield break;
        }

        foreach (var drive in drives)
        {
            bool isReady;
            try
            {
                isReady = drive.IsReady;
            }
            catch
            {
                continue;
            }

            if (isReady)
                yield return drive.RootDirectory.FullName;
        }
    }

    private static bool IsUsableNavDataRoot(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        string resolved;
        try
        {
            resolved = Path.GetFullPath(candidate);
        }
        catch
        {
            return false;
        }

        return Directory.Exists(Path.Combine(resolved, "maps"))
            && Directory.Exists(Path.Combine(resolved, "vmaps"))
            && Directory.Exists(Path.Combine(resolved, "mmaps"));
    }

    public void Dispose() { /* Navigation lives for the AppDomain – nothing to do. */ }
}
