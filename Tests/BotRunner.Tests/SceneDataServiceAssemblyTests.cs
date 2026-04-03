using System.Reflection;
using SceneDataService;

namespace BotRunner.Tests;

public sealed class SceneDataServiceAssemblyTests : IDisposable
{
    private readonly string _originalCurrentDirectory = Directory.GetCurrentDirectory();

    [Fact]
    public void NormalizePath_PreservesDriveRoot()
    {
        var root = Path.GetPathRoot(Path.GetTempPath());
        Assert.False(string.IsNullOrWhiteSpace(root));

        var normalized = (string?)NormalizePathMethod.Invoke(null, [root]);

        Assert.Equal(root, normalized);
    }

    [Fact]
    public void EnumerateAncestors_TerminatesAtDriveRootWithoutRepeatingPaths()
    {
        var root = Path.GetPathRoot(Path.GetTempPath());
        Assert.False(string.IsNullOrWhiteSpace(root));

        var nested = Path.Combine(root!, "repos", "scene-data-test", "Bot", "Release", "net8.0");
        var ancestors = ((IEnumerable<string>)EnumerateAncestorsMethod.Invoke(null, [nested])!)
            .Take(16)
            .ToList();

        Assert.NotEmpty(ancestors);
        Assert.Equal(ancestors.Count, ancestors.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(root, ancestors, StringComparer.OrdinalIgnoreCase);
        Assert.True(ancestors.Count < 16, "Ancestor enumeration should terminate once it reaches the drive root.");
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCurrentDirectory);
    }

    private static Type ProgramType => typeof(SceneDataSocketServer).Assembly.GetType("SceneDataService.Program")
        ?? throw new InvalidOperationException("SceneDataService.Program type not found.");

    private static MethodInfo NormalizePathMethod => ProgramType.GetMethod(
        "NormalizePath",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("SceneDataService.Program.NormalizePath not found.");

    private static MethodInfo EnumerateAncestorsMethod => ProgramType.GetMethod(
        "EnumerateAncestors",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("SceneDataService.Program.EnumerateAncestors not found.");
}
