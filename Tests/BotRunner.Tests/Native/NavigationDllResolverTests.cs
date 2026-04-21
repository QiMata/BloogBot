using System.IO;
using System.Runtime.InteropServices;
using BotRunner.Native;

namespace BotRunner.Tests.Native;

public class NavigationDllResolverTests
{
    [Fact]
    public void GetCandidatePaths_X64_PrefersRootOutputBeforeLegacySubdirectory()
    {
        var baseDir = Path.Combine("E:", "repos", "Westworld of Warcraft", "Bot", "Release", "net8.0");

        var candidates = NavigationDllResolver.GetCandidatePaths(baseDir, Architecture.X64, "Navigation.dll");

        Assert.Equal(
        [
            Path.Combine(baseDir, "Navigation.dll"),
            Path.Combine(baseDir, "x64", "Navigation.dll")
        ], candidates);
    }

    [Fact]
    public void GetCandidatePaths_X86_PrefersX86SubdirectoryBeforeRootFallback()
    {
        var baseDir = Path.Combine("E:", "repos", "Westworld of Warcraft", "Bot", "Release", "net8.0");

        var candidates = NavigationDllResolver.GetCandidatePaths(baseDir, Architecture.X86, "Navigation.dll");

        Assert.Equal(
        [
            Path.Combine(baseDir, "x86", "Navigation.dll"),
            Path.Combine(baseDir, "Navigation.dll")
        ], candidates);
    }
}
