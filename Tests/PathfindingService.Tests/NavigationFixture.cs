using PathfindingService.Repository;
using System;
using System.IO;

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
        string mmapsPath;

        if (!string.IsNullOrEmpty(dataRoot))
        {
            mmapsPath = Path.Combine(dataRoot, "mmaps");
            if (!Directory.Exists(mmapsPath))
            {
                throw new DirectoryNotFoundException(
                    $"WWOW_DATA_DIR is set to '{dataRoot}' but mmaps/ subdirectory not found.\n" +
                    $"Please ensure nav data exists at: {mmapsPath}\n" +
                    "Run setup.ps1 to provision data, or unset WWOW_DATA_DIR to use DLL-relative path.");
            }
        }
        else
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var testOutputDir = Path.GetDirectoryName(assemblyLocation);

            if (testOutputDir == null)
                throw new InvalidOperationException("Cannot determine test output directory");

            mmapsPath = Path.Combine(testOutputDir, "mmaps");

            if (!Directory.Exists(mmapsPath))
            {
                throw new DirectoryNotFoundException(
                    $"Navigation data not found. Expected mmaps/ at: {mmapsPath}\n" +
                    "Please either:\n" +
                    "  1. Set WWOW_DATA_DIR environment variable to point to your nav data root, or\n" +
                    "  2. Run setup.ps1 to copy nav data to the test output directory, or\n" +
                    $"  3. Manually copy maps/, mmaps/, and vmaps/ to: {testOutputDir}");
            }
        }

        var mmtileFiles = Directory.GetFiles(mmapsPath, "*.mmtile");
        if (mmtileFiles.Length == 0)
        {
            throw new FileNotFoundException(
                $"No .mmtile files found in: {mmapsPath}\n" +
                "The mmaps directory exists but appears to be empty.\n" +
                "Run setup.ps1 to provision navigation data.");
        }
    }

    public void Dispose() { /* Navigation lives for the AppDomain – nothing to do. */ }
}
