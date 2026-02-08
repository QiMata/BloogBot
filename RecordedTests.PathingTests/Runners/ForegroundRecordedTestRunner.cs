using GameData.Core.Enums;
using RecordedTests.PathingTests.Configuration;
using RecordedTests.Shared;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;
using WoWSharpClient;
using WoWSharpClient.Client;

namespace RecordedTests.PathingTests.Runners;

/// <summary>
/// Foreground bot runner that connects with GM account privileges and executes GM commands.
/// Implements both IBotRunner (for orchestration) and IGmCommandExecutor (for command execution).
/// </summary>
public class ForegroundRecordedTestRunner : IBotRunner, IGmCommandExecutor
{
    private readonly string _account;
    private readonly string _password;
    private readonly string _character;
    private readonly ITestLogger _logger;
    private readonly TestConfiguration? _config;
    private WoWClientOrchestrator? _orchestrator;

    /// <summary>
    /// Initializes a new instance of the ForegroundRecordedTestRunner.
    /// </summary>
    /// <param name="account">GM account username</param>
    /// <param name="password">GM account password</param>
    /// <param name="character">GM character name</param>
    /// <param name="logger">Test logger</param>
    /// <param name="config">Optional test configuration for recording target</param>
    public ForegroundRecordedTestRunner(
        string account,
        string password,
        string character,
        ITestLogger logger,
        TestConfiguration? config = null)
    {
        _account = account ?? throw new ArgumentNullException(nameof(account));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _character = character ?? throw new ArgumentNullException(nameof(character));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config;
    }

    /// <inheritdoc />
    public async Task ConnectAsync(ServerInfo server, CancellationToken cancellationToken)
    {
        _logger.Info($"[FG] Connecting to server {server.Host}:{server.Port} with GM account '{_account}'");

        _orchestrator = WoWClientFactory.CreateOrchestrator();

        // Login to auth server
        await _orchestrator.LoginAsync(server.Host, _account, _password, server.Port, cancellationToken);
        _logger.Info("[FG] Authentication successful");

        // Get realm list
        var realms = await _orchestrator.GetRealmListAsync(cancellationToken);
        if (realms.Count == 0)
            throw new InvalidOperationException("No realms available");

        // Connect to first realm (or match by server.Realm if specified)
        var realm = string.IsNullOrEmpty(server.Realm)
            ? realms[0]
            : realms.First(r => string.Equals(r.RealmName, server.Realm, StringComparison.OrdinalIgnoreCase));

        await _orchestrator.ConnectToRealmAsync(realm, cancellationToken);
        _logger.Info($"[FG] Connected to realm '{realm.RealmName}'");

        // Get character list and find character GUID
        await _orchestrator.RefreshCharacterListAsync(cancellationToken);
        await Task.Delay(1000, cancellationToken); // Wait for character list to populate

        var characters = WoWSharpObjectManager.Instance.CharacterSelectScreen.CharacterSelects;
        var character = characters.FirstOrDefault(c =>
            string.Equals(c.Name, _character, StringComparison.OrdinalIgnoreCase));

        if (character == null)
        {
            var availableNames = string.Join(", ", characters.Select(c => c.Name));
            throw new InvalidOperationException(
                $"Character '{_character}' not found. Available characters: {availableNames}");
        }

        // Enter world with character GUID
        await _orchestrator.EnterWorldAsync(character.Guid, cancellationToken);
        _logger.Info($"[FG] Entered world as '{_character}' (GUID: {character.Guid})");
    }

    /// <inheritdoc />
    public async Task ExecuteCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (_orchestrator == null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        _logger.Info($"[FG] Executing GM command: {command}");

        // GM commands are sent as SAY chat messages starting with '.'
        await _orchestrator.SendChatMessageAsync(
            ChatMsg.CHAT_MSG_SAY,
            Language.Universal,
            string.Empty,
            command,
            cancellationToken);

        // Brief delay to allow command to execute on server
        await Task.Delay(500, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RecordingTarget> GetRecordingTargetAsync(CancellationToken cancellationToken)
    {
        // Prefer config over environment variables
        if (_config != null)
        {
            if (!string.IsNullOrEmpty(_config.WowWindowTitle))
            {
                _logger.Info($"[FG] Recording target (from config): Window title '{_config.WowWindowTitle}'");
                return Task.FromResult(new RecordingTarget(RecordingTargetType.WindowByTitle, WindowTitle: _config.WowWindowTitle));
            }
            if (_config.WowProcessId.HasValue)
            {
                _logger.Info($"[FG] Recording target (from config): Process ID {_config.WowProcessId}");
                return Task.FromResult(new RecordingTarget(RecordingTargetType.ProcessId, ProcessId: _config.WowProcessId.Value));
            }
            if (_config.WowWindowHandle.HasValue)
            {
                _logger.Info($"[FG] Recording target (from config): Window handle {_config.WowWindowHandle}");
                return Task.FromResult(new RecordingTarget(RecordingTargetType.WindowHandle, WindowHandle: _config.WowWindowHandle.Value));
            }
        }

        // Fallback to environment variables
        var windowTitle = Environment.GetEnvironmentVariable("WWOW_WOW_WINDOW_TITLE");
        var processIdStr = Environment.GetEnvironmentVariable("WWOW_WOW_PROCESS_ID");
        var windowHandleStr = Environment.GetEnvironmentVariable("WWOW_WOW_WINDOW_HANDLE");

        if (!string.IsNullOrEmpty(windowTitle))
        {
            _logger.Info($"[FG] Recording target: Window title '{windowTitle}'");
            return Task.FromResult(new RecordingTarget(RecordingTargetType.WindowByTitle, WindowTitle: windowTitle));
        }

        if (int.TryParse(processIdStr, out var pid))
        {
            _logger.Info($"[FG] Recording target: Process ID {pid}");
            return Task.FromResult(new RecordingTarget(RecordingTargetType.ProcessId, ProcessId: pid));
        }

        if (IntPtr.TryParse(windowHandleStr, out var handle))
        {
            _logger.Info($"[FG] Recording target: Window handle {handle}");
            return Task.FromResult(new RecordingTarget(RecordingTargetType.WindowHandle, WindowHandle: handle));
        }

        throw new InvalidOperationException(
            "No recording target configured. Please set one of the following environment variables:\n" +
            "  - WWOW_WOW_WINDOW_TITLE (e.g., 'World of Warcraft')\n" +
            "  - WWOW_WOW_PROCESS_ID (e.g., '1234')\n" +
            "  - WWOW_WOW_WINDOW_HANDLE (e.g., '0x12345678')");
    }

    /// <inheritdoc />
    public Task PrepareServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        // Server state preparation is handled by GmCommandServerDesiredState
        // via the IGmCommandExecutor interface
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResetServerStateAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        // Server state reset is handled by GmCommandServerDesiredState
        // via the IGmCommandExecutor interface
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RunTestAsync(IRecordedTestContext context, CancellationToken cancellationToken)
    {
        // Foreground runner doesn't execute the test logic
        // It only provides GM capabilities for setup/teardown
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_orchestrator != null)
        {
            _logger.Info("[FG] Disconnecting from server");
            await _orchestrator.DisconnectWorldAsync(cancellationToken);
            await _orchestrator.DisconnectAuthAsync(cancellationToken);
            _orchestrator.Dispose();
            _orchestrator = null;
        }
    }

    /// <inheritdoc />
    public Task ShutdownUiAsync(CancellationToken cancellationToken)
    {
        // No UI to shutdown for this runner
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None);
    }
}
