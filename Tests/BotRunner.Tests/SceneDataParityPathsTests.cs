using Tests.Infrastructure;

namespace BotRunner.Tests;

public sealed class SceneDataParityPathsTests : IDisposable
{
    private readonly Dictionary<string, string?> _environmentSnapshot = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _tempDirectories = [];

    [Fact]
    public void ResolveDockerHostDataRoot_PrefersVmangosHostPathOverRepoLocalWwowDataDir()
    {
        var dockerRoot = CreateDataRoot("docker-root");
        var repoLocalRoot = CreateDataRoot("repo-local-root");

        SetEnvironmentVariable("WWOW_VMANGOS_DATA_DIR", dockerRoot);
        SetEnvironmentVariable("WWOW_DATA_DIR", repoLocalRoot);

        var resolved = SceneDataParityPaths.ResolveDockerHostDataRoot();

        Assert.Equal(Path.GetFullPath(dockerRoot), resolved);
    }

    [Fact]
    public void ResolvePreferredDataRoot_PrefersDockerParityRootOverRepoFallback()
    {
        var dockerRoot = CreateDataRoot("docker-root");
        var repoRoot = CreateTempDirectory("repo-root");
        var repoDataRoot = CreateDataRoot(Path.Combine(repoRoot, "Data"));

        SetEnvironmentVariable("WWOW_VMANGOS_DATA_DIR", dockerRoot);
        SetEnvironmentVariable("WWOW_DATA_DIR", repoDataRoot);

        var resolved = SceneDataParityPaths.ResolvePreferredDataRoot(
            repoDataRoot,
            Path.Combine(repoRoot, "Tests", "Navigation.Physics.Tests", "bin", "Release", "net8.0"));

        Assert.Equal(Path.GetFullPath(dockerRoot), resolved);
    }

    [Fact]
    public void EnumerateRepoFallbackCandidates_IncludesRepoDataRootFromNestedTestOutputPath()
    {
        var repoRoot = CreateTempDirectory("repo-root");
        var repoDataRoot = CreateDataRoot(Path.Combine(repoRoot, "Data"));

        var candidates = SceneDataParityPaths.EnumerateRepoFallbackCandidates(
                Path.Combine(repoRoot, "Tests", "Navigation.Physics.Tests", "bin", "Release", "net8.0"))
            .ToList();

        Assert.Contains(Path.GetFullPath(repoDataRoot), candidates, StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        foreach (var kvp in _environmentSnapshot)
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);

        foreach (var dir in _tempDirectories.OrderByDescending(static d => d.Length))
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private string CreateDataRoot(string nameOrPath)
    {
        var root = Path.IsPathRooted(nameOrPath)
            ? nameOrPath
            : Path.Combine(CreateTempDirectory(nameOrPath), "data");

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "maps"));
        Directory.CreateDirectory(Path.Combine(root, "vmaps"));
        Directory.CreateDirectory(Path.Combine(root, "scenes"));
        return root;
    }

    private string CreateTempDirectory(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "wwow-scene-parity-tests", Guid.NewGuid().ToString("N"), name);
        Directory.CreateDirectory(dir);
        _tempDirectories.Add(dir);
        return dir;
    }

    private void SetEnvironmentVariable(string name, string? value)
    {
        if (!_environmentSnapshot.ContainsKey(name))
            _environmentSnapshot[name] = Environment.GetEnvironmentVariable(name);

        Environment.SetEnvironmentVariable(name, value);
    }
}
