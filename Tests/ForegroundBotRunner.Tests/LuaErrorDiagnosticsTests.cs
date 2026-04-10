using System;
using System.Linq;
using ForegroundBotRunner.Diagnostics;

namespace ForegroundBotRunner.Tests;

public sealed class LuaErrorDiagnosticsTests
{
    [Fact]
    public void InstallCaptureHandler_InvokesLuaWithErrorHandlerScript()
    {
        string? capturedLua = null;

        LuaErrorDiagnostics.InstallCaptureHandler(lua => capturedLua = lua);

        Assert.False(string.IsNullOrWhiteSpace(capturedLua));
        Assert.Contains("seterrorhandler", capturedLua, StringComparison.Ordinal);
        Assert.Contains("WWOW_LUA_ERROR_BUFFER", capturedLua, StringComparison.Ordinal);
    }

    [Fact]
    public void DrainCapturedErrors_WhenCountIsZero_ReturnsEmpty()
    {
        var drained = LuaErrorDiagnostics.DrainCapturedErrors(_ => ["0"]);

        Assert.Empty(drained);
    }

    [Fact]
    public void DrainCapturedErrors_WhenLuaReturnsNullArray_ReturnsEmpty()
    {
        var drained = LuaErrorDiagnostics.DrainCapturedErrors(_ => null!);

        Assert.Empty(drained);
    }

    [Fact]
    public void DrainCapturedErrors_WhenLuaReturnsEntries_ParsesNonEmptyValues()
    {
        var drained = LuaErrorDiagnostics.DrainCapturedErrors(_ => ["3", "  first  ", "", "second", "   ", "third"]);

        Assert.Equal(3, drained.Count);
        Assert.Equal("first", drained[0]);
        Assert.Equal("second", drained[1]);
        Assert.Equal("third", drained[2]);
    }

    [Fact]
    public void DrainCapturedErrors_WhenLuaCountInvalid_ReturnsEmpty()
    {
        var drained = LuaErrorDiagnostics.DrainCapturedErrors(_ => ["not-a-number", "err"]);

        Assert.Empty(drained);
    }
}
