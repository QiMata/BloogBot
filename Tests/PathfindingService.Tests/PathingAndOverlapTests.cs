using GameData.Core.Models;
using Microsoft.Extensions.Configuration;
using PathfindingService.Repository;

namespace PathfindingService.Tests
{
    /// <summary>
    /// End‑to‑end tests for the Navigation API.
    ///   • CalculatePath
    /// </summary>
    public class NavigationFixture : IDisposable
    {
        public Navigation Navigation { get; }

        public NavigationFixture()
        {
            // Preflight checks
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
                        "Please ensure nav data exists at: {mmapsPath}\n" +
                        "Run setup.ps1 to provision data, or unset WWOW_DATA_DIR to use DLL-relative path.");
                }
            }
            else
            {
                // Check for DLL-relative mmaps
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
                        "  3. Manually copy maps/, mmaps/, and vmaps/ to: {testOutputDir}");
                }
            }

            // Verify mmaps contains .mmtile files
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

    public class PathfindingTests(NavigationFixture fixture) : IClassFixture<NavigationFixture>
    {
        private readonly Navigation _navigation = fixture.Navigation;

        [Fact]
        public void CalculatePath_ShouldReturnValidPath()
        {
            uint mapId = 1;
            Position start = new(-616.2514f, -4188.0044f, 82.316719f);
            Position end = new(1629.36f, -4373.39f, 50.2564f);

            var path = _navigation.CalculatePath(mapId, start.ToXYZ(), end.ToXYZ(), smoothPath: true);

            Assert.NotNull(path);
            Assert.NotEmpty(path);
        }
    }
}
