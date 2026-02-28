namespace WWoW.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="WoWProcessManager"/> state transitions, teardown guarantees,
/// and configuration. These tests do NOT launch WoW.exe; they exercise the manager's
/// state machine and disposal behavior using non-existent paths to trigger early failures.
/// </summary>
[UnitTest]
public class WoWProcessManagerTests
{
    // ======== WoWProcessConfig Defaults ========

    [Fact]
    public void WoWProcessConfig_Defaults_WoWExecutablePath_IsNotEmpty()
    {
        var config = new WoWProcessConfig();
        Assert.False(string.IsNullOrWhiteSpace(config.WoWExecutablePath));
    }

    [Fact]
    public void WoWProcessConfig_Defaults_LoaderDllPath_IsNotEmpty()
    {
        var config = new WoWProcessConfig();
        Assert.False(string.IsNullOrWhiteSpace(config.LoaderDllPath));
    }

    [Fact]
    public void WoWProcessConfig_Defaults_ProcessInitDelayMs_Is2000()
    {
        var config = new WoWProcessConfig();
        Assert.Equal(2000, config.ProcessInitDelayMs);
    }

    [Fact]
    public void WoWProcessConfig_Defaults_InjectionTimeoutMs_Is10000()
    {
        var config = new WoWProcessConfig();
        Assert.Equal(10000, config.InjectionTimeoutMs);
    }

    [Fact]
    public void WoWProcessConfig_Defaults_RuntimeInitDelayMs_Is5000()
    {
        var config = new WoWProcessConfig();
        Assert.Equal(5000, config.RuntimeInitDelayMs);
    }

    [Fact]
    public void WoWProcessConfig_Defaults_TerminateOnDispose_IsTrue()
    {
        var config = new WoWProcessConfig();
        Assert.True(config.TerminateOnDispose);
    }

    [Fact]
    public void WoWProcessConfig_FromEnvironment_ReturnsNonNull()
    {
        var config = WoWProcessConfig.FromEnvironment();
        Assert.NotNull(config);
    }

    [Fact]
    public void WoWProcessConfig_CanOverride_TerminateOnDispose()
    {
        var config = new WoWProcessConfig { TerminateOnDispose = false };
        Assert.False(config.TerminateOnDispose);
    }

    [Fact]
    public void WoWProcessConfig_CanOverride_ProcessInitDelayMs()
    {
        var config = new WoWProcessConfig { ProcessInitDelayMs = 500 };
        Assert.Equal(500, config.ProcessInitDelayMs);
    }

    [Fact]
    public void WoWProcessConfig_CanOverride_InjectionTimeoutMs()
    {
        var config = new WoWProcessConfig { InjectionTimeoutMs = 3000 };
        Assert.Equal(3000, config.InjectionTimeoutMs);
    }

    [Fact]
    public void WoWProcessConfig_CanOverride_RuntimeInitDelayMs()
    {
        var config = new WoWProcessConfig { RuntimeInitDelayMs = 1000 };
        Assert.Equal(1000, config.RuntimeInitDelayMs);
    }

    // ======== Constructor ========

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new WoWProcessManager(null!));
    }

    [Fact]
    public void Constructor_ValidConfig_InitialStateIsNotStarted()
    {
        var config = new WoWProcessConfig { WoWExecutablePath = "nonexistent.exe" };
        using var manager = new WoWProcessManager(config);

        Assert.Equal(InjectionState.NotStarted, manager.State);
    }

    [Fact]
    public void Constructor_ValidConfig_ProcessIdIsNull()
    {
        var config = new WoWProcessConfig { WoWExecutablePath = "nonexistent.exe" };
        using var manager = new WoWProcessManager(config);

        Assert.Null(manager.ProcessId);
    }

    [Fact]
    public void Constructor_ValidConfig_IsProcessRunning_IsFalse()
    {
        var config = new WoWProcessConfig { WoWExecutablePath = "nonexistent.exe" };
        using var manager = new WoWProcessManager(config);

        Assert.False(manager.IsProcessRunning);
    }

    // ======== LaunchAndInjectAsync — Missing WoW.exe ========

    [Fact]
    public async Task LaunchAndInjectAsync_MissingWoWExe_ReturnsFailed()
    {
        var config = new WoWProcessConfig
        {
            WoWExecutablePath = @"C:\nonexistent_path\WoW.exe",
            LoaderDllPath = @"C:\nonexistent_path\Loader.dll"
        };

        using var manager = new WoWProcessManager(config);
        var result = await manager.LaunchAndInjectAsync();

        Assert.False(result.Success);
        Assert.Equal(InjectionState.Failed, result.FinalState);
        Assert.Contains("WoW.exe not found", result.ErrorMessage);
    }

    [Fact]
    public async Task LaunchAndInjectAsync_MissingWoWExe_StateIsFailed()
    {
        var config = new WoWProcessConfig
        {
            WoWExecutablePath = @"C:\nonexistent_path\WoW.exe",
            LoaderDllPath = @"C:\nonexistent_path\Loader.dll"
        };

        using var manager = new WoWProcessManager(config);
        await manager.LaunchAndInjectAsync();

        Assert.Equal(InjectionState.Failed, manager.State);
    }

    [Fact]
    public async Task LaunchAndInjectAsync_MissingWoWExe_ProcessIdIsNull()
    {
        var config = new WoWProcessConfig
        {
            WoWExecutablePath = @"C:\nonexistent_path\WoW.exe",
            LoaderDllPath = @"C:\nonexistent_path\Loader.dll"
        };

        using var manager = new WoWProcessManager(config);
        var result = await manager.LaunchAndInjectAsync();

        Assert.Null(result.ProcessId);
    }

    // ======== LaunchAndInjectAsync — Missing Loader.dll ========

    [Fact]
    public async Task LaunchAndInjectAsync_MissingLoaderDll_ReturnsFailed()
    {
        // WoW.exe path must exist, so use an actual file on disk (the test assembly itself)
        var existingFile = typeof(WoWProcessManagerTests).Assembly.Location;
        var config = new WoWProcessConfig
        {
            WoWExecutablePath = existingFile,
            LoaderDllPath = @"C:\nonexistent_path\Loader.dll"
        };

        using var manager = new WoWProcessManager(config);
        var result = await manager.LaunchAndInjectAsync();

        Assert.False(result.Success);
        Assert.Equal(InjectionState.Failed, result.FinalState);
        Assert.Contains("Loader.dll not found", result.ErrorMessage);
    }

    // ======== TerminateProcess ========

    [Fact]
    public void TerminateProcess_WhenNotStarted_SetsStateToProcessExited()
    {
        var config = new WoWProcessConfig { WoWExecutablePath = "nonexistent.exe" };
        using var manager = new WoWProcessManager(config);

        // TerminateProcess should be safe to call even when no process is running
        manager.TerminateProcess();

        Assert.Equal(InjectionState.ProcessExited, manager.State);
    }

    [Fact]
    public void TerminateProcess_CalledTwice_DoesNotThrow()
    {
        var config = new WoWProcessConfig { WoWExecutablePath = "nonexistent.exe" };
        using var manager = new WoWProcessManager(config);

        manager.TerminateProcess();
        manager.TerminateProcess();

        Assert.Equal(InjectionState.ProcessExited, manager.State);
    }

    // ======== Dispose ========

    [Fact]
    public void Dispose_WhenNotStarted_DoesNotThrow()
    {
        var config = new WoWProcessConfig { WoWExecutablePath = "nonexistent.exe" };
        var manager = new WoWProcessManager(config);

        // Should not throw
        manager.Dispose();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var config = new WoWProcessConfig { WoWExecutablePath = "nonexistent.exe" };
        var manager = new WoWProcessManager(config);

        manager.Dispose();
        manager.Dispose(); // Second call should be a no-op
    }

    [Fact]
    public void Dispose_WithTerminateOnDispose_SetsStateToProcessExited()
    {
        var config = new WoWProcessConfig
        {
            WoWExecutablePath = "nonexistent.exe",
            TerminateOnDispose = true
        };
        var manager = new WoWProcessManager(config);

        manager.Dispose();

        Assert.Equal(InjectionState.ProcessExited, manager.State);
    }

    [Fact]
    public void Dispose_WithTerminateOnDisposeFalse_DoesNotChangeStateFromNotStarted()
    {
        var config = new WoWProcessConfig
        {
            WoWExecutablePath = "nonexistent.exe",
            TerminateOnDispose = false
        };
        var manager = new WoWProcessManager(config);

        manager.Dispose();

        // When TerminateOnDispose is false, TerminateProcess is not called,
        // so state remains NotStarted (no process was ever launched)
        Assert.Equal(InjectionState.NotStarted, manager.State);
    }

    // ======== InjectionState Enum ========

    [Fact]
    public void InjectionState_HasExpectedValues()
    {
        Assert.Equal(0, (int)InjectionState.NotStarted);
        Assert.Equal(1, (int)InjectionState.ProcessLaunched);
        Assert.Equal(2, (int)InjectionState.MemoryAllocated);
        Assert.Equal(3, (int)InjectionState.DllPathWritten);
        Assert.Equal(4, (int)InjectionState.LoaderInjected);
        Assert.Equal(5, (int)InjectionState.LoaderInitialized);
        Assert.Equal(6, (int)InjectionState.ManagedCodeRunning);
        Assert.Equal(7, (int)InjectionState.Failed);
        Assert.Equal(8, (int)InjectionState.ProcessExited);
    }

    [Fact]
    public void InjectionState_EnumCount_Is9()
    {
        var values = Enum.GetValues<InjectionState>();
        Assert.Equal(9, values.Length);
    }

    // ======== InjectionResult Record ========

    [Fact]
    public void InjectionResult_SuccessRecord_HasExpectedValues()
    {
        var result = new InjectionResult(
            Success: true,
            FinalState: InjectionState.ManagedCodeRunning,
            ProcessId: 12345);

        Assert.True(result.Success);
        Assert.Equal(InjectionState.ManagedCodeRunning, result.FinalState);
        Assert.Equal(12345, result.ProcessId);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void InjectionResult_FailureRecord_HasErrorMessage()
    {
        var result = new InjectionResult(
            Success: false,
            FinalState: InjectionState.Failed,
            ErrorMessage: "Test error");

        Assert.False(result.Success);
        Assert.Equal(InjectionState.Failed, result.FinalState);
        Assert.Equal("Test error", result.ErrorMessage);
        Assert.Null(result.ProcessId);
    }

    [Fact]
    public void InjectionResult_DefaultProcessHandle_IsIntPtrZero()
    {
        var result = new InjectionResult(Success: false, FinalState: InjectionState.Failed);
        Assert.Equal(IntPtr.Zero, result.ProcessHandle);
    }

    // ======== LaunchAndInjectAsync — Cancellation ========

    [Fact]
    public async Task LaunchAndInjectAsync_AlreadyCancelled_ReturnsFailed()
    {
        var config = new WoWProcessConfig
        {
            WoWExecutablePath = @"C:\nonexistent_path\WoW.exe",
            LoaderDllPath = @"C:\nonexistent_path\Loader.dll"
        };

        using var manager = new WoWProcessManager(config);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The method checks file existence before using the cancellation token,
        // so it should fail with "not found" before hitting the cancellation
        var result = await manager.LaunchAndInjectAsync(cts.Token);
        Assert.False(result.Success);
    }
}
