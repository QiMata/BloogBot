using System;
using System.Collections.Generic;
using Tests.Infrastructure;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Unit tests for <see cref="ManagedWowProcessIdResolver"/>. Verifies the
/// PID-extraction regexes against the two known StateManager log line
/// shapes used by FG/BG launchers.
/// </summary>
public class ManagedWowProcessIdResolverTests
{
    [Fact]
    public void Resolve_RecognizesProcessIdLogLine()
    {
        var lines = new[]
        {
            "[12:00:00 INF] WoW.exe started for account FG1 (Process ID: 12345); attaching loader.",
        };
        Assert.Equal(12345, ManagedWowProcessIdResolver.Resolve("FG1", lines));
    }

    [Fact]
    public void Resolve_RecognizesAddedToManagedServicesLine()
    {
        var lines = new[]
        {
            "[12:00:00 INF] Added FG1 to managed services with PID 9876.",
        };
        Assert.Equal(9876, ManagedWowProcessIdResolver.Resolve("FG1", lines));
    }

    [Fact]
    public void Resolve_PrefersMostRecentMatch()
    {
        var lines = new[]
        {
            "[12:00:00 INF] WoW.exe started for account FG1 (Process ID: 1111); old launch.",
            "[12:00:30 INF] WoW.exe started for account FG1 (Process ID: 2222); newer launch (relaunch after crash).",
        };
        Assert.Equal(2222, ManagedWowProcessIdResolver.Resolve("FG1", lines));
    }

    [Fact]
    public void Resolve_ReturnsNullWhenAccountNotFound()
    {
        var lines = new[]
        {
            "[12:00:00 INF] WoW.exe started for account FG1 (Process ID: 12345).",
        };
        Assert.Null(ManagedWowProcessIdResolver.Resolve("BG1", lines));
    }

    [Fact]
    public void Resolve_ReturnsNullForEmptyAccount()
    {
        var lines = new[] { "[12:00:00 INF] noise" };
        Assert.Null(ManagedWowProcessIdResolver.Resolve("", lines));
        Assert.Null(ManagedWowProcessIdResolver.Resolve(null!, lines));
    }

    [Fact]
    public void Resolve_HandlesNullLines()
    {
        Assert.Null(ManagedWowProcessIdResolver.Resolve("FG1", null!));
    }

    [Fact]
    public void Resolve_DoesNotMatchOtherAccountSubstring()
    {
        // A naive regex would match "FG1" inside "FG10" — escape ensures it doesn't.
        var lines = new[]
        {
            "[12:00:00 INF] WoW.exe started for account FG10 (Process ID: 9999); other bot.",
        };
        Assert.Null(ManagedWowProcessIdResolver.Resolve("FG1", lines));
    }
}
