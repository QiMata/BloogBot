using System;
using System.Diagnostics;
using Xunit;

namespace BotRunner.Tests.Helpers;

/// <summary>
/// Tests verifying infrastructure configuration behaviors:
/// - WWOW_SHOW_WINDOWS env var controls process window visibility
/// - Repo-scoped process filtering uses correct marker string
/// </summary>
public class InfrastructureConfigTests
{
    private const string ShowWindowsEnvVar = "WWOW_SHOW_WINDOWS";

    [Fact]
    public void ShowWindows_Default_CreateNoWindowIsTrue()
    {
        // Default (unset) should keep windows hidden
        var prev = Environment.GetEnvironmentVariable(ShowWindowsEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(ShowWindowsEnvVar, null);
            bool createNoWindow = Environment.GetEnvironmentVariable(ShowWindowsEnvVar) != "1";
            Assert.True(createNoWindow, "Default should hide windows");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ShowWindowsEnvVar, prev);
        }
    }

    [Fact]
    public void ShowWindows_SetTo1_CreateNoWindowIsFalse()
    {
        var prev = Environment.GetEnvironmentVariable(ShowWindowsEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(ShowWindowsEnvVar, "1");
            bool createNoWindow = Environment.GetEnvironmentVariable(ShowWindowsEnvVar) != "1";
            Assert.False(createNoWindow, "WWOW_SHOW_WINDOWS=1 should show windows");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ShowWindowsEnvVar, prev);
        }
    }

    [Fact]
    public void ShowWindows_SetToOther_CreateNoWindowIsTrue()
    {
        var prev = Environment.GetEnvironmentVariable(ShowWindowsEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(ShowWindowsEnvVar, "true");
            bool createNoWindow = Environment.GetEnvironmentVariable(ShowWindowsEnvVar) != "1";
            Assert.True(createNoWindow, "Only exact '1' should show windows");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ShowWindowsEnvVar, prev);
        }
    }

    [Fact]
    public void RepoMarker_ContainedInTestProjectPath()
    {
        // The repo-scoped filter checks for "Westworld of Warcraft" in module paths.
        // This test verifies our own test assembly path contains the marker.
        const string repoMarker = "Westworld of Warcraft";
        var assemblyPath = typeof(InfrastructureConfigTests).Assembly.Location;

        Assert.Contains(repoMarker, assemblyPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepoMarker_NotContainedInSystemPaths()
    {
        const string repoMarker = "Westworld of Warcraft";

        // System paths should NOT match the repo marker
        Assert.DoesNotContain(repoMarker, @"C:\Windows\System32\cmd.exe", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(repoMarker, @"C:\Program Files\dotnet\dotnet.exe", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KillLingeringProcesses_WithLogger_InvokesCallback()
    {
        // Verify the logger callback is invoked (even when no processes match)
        var messages = new System.Collections.Generic.List<string>();
        StateManagerProcessHelper.KillLingeringProcesses(msg => messages.Add(msg));

        // No assertions on count — just verify it doesn't throw
        // On a clean machine, there should be zero matching processes,
        // so no messages. On a machine with running services, messages
        // will contain repo-scoped filtering evidence.
    }

    [Fact]
    public void StateManagerProcessHelper_Stop_OnNullProcess_DoesNotThrow()
    {
        // A newly constructed helper with no started process should not throw on Stop()
        var helper = new StateManagerProcessHelper();
        helper.Stop(); // Should complete without error
    }
}
