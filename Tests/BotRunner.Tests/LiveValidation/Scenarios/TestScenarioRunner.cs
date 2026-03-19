using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Scenarios;

/// <summary>
/// Executes a <see cref="TestScenario"/> through <see cref="LiveBotFixture"/>.
///
/// Usage in a test method:
///   var runner = new TestScenarioRunner(_bot, _output);
///   var result = await runner.RunAsync("Scenarios/Mining_BG_CopperVein.json");
///   result.AssertAll();
/// </summary>
public class TestScenarioRunner
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    public TestScenarioRunner(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
    }

    /// <summary>
    /// Load and execute a scenario from a JSON file path (relative to test output dir).
    /// </summary>
    public async Task<ScenarioResult> RunAsync(string scenarioJsonPath)
    {
        var scenario = LoadScenario(scenarioJsonPath);
        _output.WriteLine($"=== Scenario: {scenario.Name} ===");
        _output.WriteLine($"  {scenario.Description}");
        return await ExecuteAsync(scenario);
    }

    /// <summary>
    /// Execute a scenario object directly.
    /// </summary>
    public async Task<ScenarioResult> ExecuteAsync(TestScenario scenario)
    {
        var result = new ScenarioResult(scenario);

        // Phase 0: Configure coordinator
        if (scenario.EnableCoordinator)
        {
            Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "0");
            _output.WriteLine("  Coordinator: ENABLED");
        }

        try
        {
            // Phase 1: Load custom settings if specified
            if (!string.IsNullOrEmpty(scenario.Settings))
            {
                var settingsPath = ResolveSettingsPath(scenario.Settings);
                _output.WriteLine($"  Restarting with settings: {scenario.Settings}");
                await _bot.RestartWithSettingsAsync(settingsPath);
                if (!_bot.IsReady)
                {
                    result.SetupFailed = true;
                    result.FailureReason = _bot.FailureReason ?? "Fixture not ready after restart";
                    return result;
                }
            }

            // Phase 2: Wait for minimum bots
            if (scenario.MinBots > 1)
            {
                _output.WriteLine($"  Waiting for {scenario.MinBots} bots (timeout {scenario.BotEntryTimeoutSeconds}s)...");
                var sw = Stopwatch.StartNew();
                var bestCount = 0;
                while (sw.Elapsed < TimeSpan.FromSeconds(scenario.BotEntryTimeoutSeconds))
                {
                    await _bot.RefreshSnapshotsAsync();
                    if (_bot.AllBots.Count > bestCount)
                    {
                        bestCount = _bot.AllBots.Count;
                        _output.WriteLine($"    [{sw.Elapsed.TotalSeconds:F0}s] Bots in world: {bestCount}/{scenario.MinBots}");
                    }
                    if (bestCount >= scenario.MinBots)
                        break;
                    await Task.Delay(2000);
                }

                if (bestCount < scenario.MinBots)
                {
                    result.SetupFailed = true;
                    result.FailureReason = $"Only {bestCount}/{scenario.MinBots} bots entered world";
                    return result;
                }
            }

            // Phase 3: Execute per-bot setup
            foreach (var setup in scenario.Setup)
            {
                var account = ResolveAccount(setup.Account);
                if (account == null)
                {
                    _output.WriteLine($"  WARNING: Could not resolve account '{setup.Account}', skipping setup");
                    continue;
                }

                _output.WriteLine($"  Setting up {account} ({setup.Account})...");
                await ExecuteBotSetup(account, setup);
            }

            // Phase 4: Dispatch action
            if (scenario.Action != null)
            {
                var actionAccount = ResolveAccount(scenario.Action.Account);
                if (actionAccount != null)
                {
                    var action = BuildActionMessage(scenario.Action);
                    _output.WriteLine($"  Dispatching {scenario.Action.Type} to {actionAccount}");
                    await _bot.SendActionAsync(actionAccount, action);
                }
            }

            // Phase 5: Observe
            _output.WriteLine($"  Observing for up to {scenario.Observe.TimeoutSeconds}s...");
            await ObserveAsync(scenario, result);

            // Phase 6: Validate assertions
            await _bot.RefreshSnapshotsAsync();
            ValidateAssertions(scenario, result);
        }
        finally
        {
            // Restore coordinator state
            if (scenario.EnableCoordinator)
                Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "1");
        }

        return result;
    }

    private async Task ExecuteBotSetup(string account, BotSetup setup)
    {
        var label = setup.Account;

        if (setup.CleanSlate)
        {
            _output.WriteLine($"    Clean slate for {account}...");
            await _bot.EnsureCleanSlateAsync(account, label, teleportToSafeZone: true);
        }

        // GM commands first (e.g. .gm on)
        foreach (var cmd in setup.GmCommands)
        {
            _output.WriteLine($"    GM: {cmd}");
            await _bot.SendGmChatCommandAsync(account, cmd);
            await Task.Delay(500);
        }

        // Clear inventory if requested
        if (setup.ClearInventory)
        {
            _output.WriteLine($"    Clearing inventory...");
            await _bot.BotClearInventoryAsync(account);
        }

        // Learn spells
        foreach (var spellId in setup.LearnSpells)
        {
            _output.WriteLine($"    Learn spell {spellId}");
            await _bot.BotLearnSpellAsync(account, (uint)spellId);
        }

        // Set skills
        foreach (var skill in setup.SetSkills)
        {
            _output.WriteLine($"    Set skill {skill.Id} = {skill.Current}/{skill.Max}");
            await _bot.BotSetSkillAsync(account, (uint)skill.Id, skill.Current, skill.Max);
        }

        // Add items
        foreach (var item in setup.AddItems)
        {
            _output.WriteLine($"    Add item {item.Id} x{item.Count}");
            await _bot.BotAddItemAsync(account, (uint)item.Id, item.Count);
        }

        // Teleport last (after learning/equipping)
        if (setup.Teleport != null)
        {
            var t = setup.Teleport;
            _output.WriteLine($"    Teleport to map={t.MapId} ({t.X:F0},{t.Y:F0},{t.Z:F0})");
            await _bot.BotTeleportAsync(account, t.MapId, t.X, t.Y, t.Z);
            await Task.Delay(2000); // Let teleport settle
        }
    }

    private async Task ObserveAsync(TestScenario scenario, ScenarioResult result)
    {
        var sw = Stopwatch.StartNew();
        var lastLogTime = TimeSpan.Zero;
        var observe = scenario.Observe;

        while (sw.Elapsed < TimeSpan.FromSeconds(observe.TimeoutSeconds))
        {
            await Task.Delay(observe.PollIntervalMs);
            await _bot.RefreshSnapshotsAsync();

            // Track observed state
            foreach (var snap in _bot.AllBots)
            {
                var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;

                if (snap.PartyLeaderGuid != 0)
                    result.GroupFormed = true;

                var unitFlags = snap.Player?.Unit?.UnitFlags ?? 0;
                if ((unitFlags & 0x80000) != 0)
                    result.CombatSeen = true;

                if (snap.CurrentAction != null)
                    result.ActionTypesSeen.Add(snap.CurrentAction.ActionType.ToString());
                if (snap.PreviousAction != null)
                    result.ActionTypesSeen.Add(snap.PreviousAction.ActionType.ToString());

                result.TrackMapPresence((int)mapId);
            }

            // Log progress periodically
            if (sw.Elapsed - lastLogTime >= TimeSpan.FromSeconds(observe.LogIntervalSeconds))
            {
                lastLogTime = sw.Elapsed;
                _output.WriteLine($"  [{sw.Elapsed.TotalSeconds:F0}s] group={result.GroupFormed}, " +
                    $"combat={result.CombatSeen}, actions=[{string.Join(",", result.ActionTypesSeen)}]");

                foreach (var snap in _bot.AllBots)
                {
                    var pos = snap.Player?.Unit?.GameObject?.Base?.Position;
                    var mapId = snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
                    var action = snap.CurrentAction?.ActionType.ToString() ?? "none";
                    _output.WriteLine($"    {snap.AccountName}: map={mapId}, " +
                        $"pos=({pos?.X:F0},{pos?.Y:F0},{pos?.Z:F0}), action={action}");
                }
            }

            // Early exit if all key assertions already met
            if (AreAssertionsMet(scenario, result))
            {
                _output.WriteLine($"  All assertions met at {sw.Elapsed.TotalSeconds:F0}s — early exit");
                break;
            }
        }
    }

    private void ValidateAssertions(TestScenario scenario, ScenarioResult result)
    {
        var a = scenario.Assertions;

        if (a.GroupFormed == true && !result.GroupFormed)
            result.Failures.Add("Expected group to be formed, but no bots reported PartyLeaderGuid != 0");

        if (a.DungeoneeringDispatched == true && !result.ActionTypesSeen.Contains("StartDungeoneering"))
            result.Failures.Add("Expected StartDungeoneering action, but it was never seen");

        if (a.MinBotsOnMap != null)
        {
            var onMap = result.GetMapCount(a.MinBotsOnMap.MapId);
            if (onMap < a.MinBotsOnMap.Count)
                result.Failures.Add($"Expected {a.MinBotsOnMap.Count}+ bots on map {a.MinBotsOnMap.MapId}, got {onMap}");
        }

        if (a.ActionTypeSeen != null && !result.ActionTypesSeen.Contains(a.ActionTypeSeen))
            result.Failures.Add($"Expected action type '{a.ActionTypeSeen}' never observed");

        // Snapshot-level conditions
        foreach (var cond in a.SnapshotConditions)
        {
            var account = ResolveAccount(cond.Account);
            if (account == null)
            {
                result.Failures.Add($"Cannot resolve account '{cond.Account}' for snapshot condition");
                continue;
            }

            var snap = _bot.AllBots.FirstOrDefault(s =>
                s.AccountName.Equals(account, StringComparison.OrdinalIgnoreCase));
            if (snap == null)
            {
                result.Failures.Add($"No snapshot for account '{account}'");
                continue;
            }

            var value = ResolveSnapshotField(snap, cond.Field);
            if (cond.Equals != null && value != cond.Equals)
                result.Failures.Add($"{account}.{cond.Field}: expected '{cond.Equals}', got '{value}'");
            if (cond.NotEquals != null && value == cond.NotEquals)
                result.Failures.Add($"{account}.{cond.Field}: expected not '{cond.NotEquals}', got '{value}'");
            if (cond.GreaterThan != null && double.TryParse(value, out var numVal) && numVal <= cond.GreaterThan)
                result.Failures.Add($"{account}.{cond.Field}: expected > {cond.GreaterThan}, got {value}");
        }
    }

    private bool AreAssertionsMet(TestScenario scenario, ScenarioResult result)
    {
        var a = scenario.Assertions;

        if (a.GroupFormed == true && !result.GroupFormed) return false;
        if (a.DungeoneeringDispatched == true && !result.ActionTypesSeen.Contains("StartDungeoneering")) return false;
        if (a.MinBotsOnMap != null && result.GetMapCount(a.MinBotsOnMap.MapId) < a.MinBotsOnMap.Count) return false;
        if (a.ActionTypeSeen != null && !result.ActionTypesSeen.Contains(a.ActionTypeSeen)) return false;

        return true;
    }

    private string? ResolveAccount(string alias)
    {
        return alias switch
        {
            "$BG" => _bot.BgAccountName,
            "$FG" => _bot.FgAccountName,
            "$COMBAT" => _bot.CombatTestAccountName,
            _ => alias // Explicit account name
        };
    }

    private static ActionMessage BuildActionMessage(ScenarioAction action)
    {
        var msg = new ActionMessage
        {
            ActionType = Enum.Parse<ActionType>(action.Type)
        };

        foreach (var p in action.Parameters)
        {
            var param = new RequestParameter();
            if (p.IntValue.HasValue) param.IntParam = p.IntValue.Value;
            else if (p.FloatValue.HasValue) param.FloatParam = p.FloatValue.Value;
            else if (p.LongValue.HasValue) param.LongParam = p.LongValue.Value;
            else if (p.StringValue != null) param.StringParam = p.StringValue;
            msg.Parameters.Add(param);
        }

        return msg;
    }

    private static string? ResolveSnapshotField(WoWActivitySnapshot snap, string field)
    {
        return field switch
        {
            "ScreenState" => snap.ScreenState,
            "CharacterName" => snap.CharacterName,
            "AccountName" => snap.AccountName,
            "PartyLeaderGuid" => snap.PartyLeaderGuid.ToString(),
            "Player.Unit.Health" => (snap.Player?.Unit?.Health ?? 0).ToString(),
            "Player.Unit.MaxHealth" => (snap.Player?.Unit?.MaxHealth ?? 0).ToString(),
            "Player.Unit.UnitFlags" => (snap.Player?.Unit?.UnitFlags ?? 0).ToString(),
            "Player.Unit.GameObject.Base.MapId" => (snap.Player?.Unit?.GameObject?.Base?.MapId ?? 0).ToString(),
            "CurrentAction.ActionType" => snap.CurrentAction?.ActionType.ToString(),
            "PreviousAction.ActionType" => snap.PreviousAction?.ActionType.ToString(),
            _ => null
        };
    }

    public static TestScenario LoadScenario(string jsonPath)
    {
        var fullPath = ResolveScenarioPath(jsonPath);
        var json = File.ReadAllText(fullPath);
        return JsonSerializer.Deserialize<TestScenario>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? throw new InvalidOperationException($"Failed to deserialize scenario: {fullPath}");
    }

    private static string ResolveScenarioPath(string relativePath)
    {
        // Try output directory first
        var outputPath = Path.Combine(AppContext.BaseDirectory, "LiveValidation", relativePath);
        if (File.Exists(outputPath))
            return outputPath;

        // Walk up to find the test project root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Tests", "BotRunner.Tests", "LiveValidation", relativePath);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Scenario file not found: {relativePath}");
    }

    private static string ResolveSettingsPath(string settingsFileName)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "LiveValidation", "Settings", settingsFileName);
        if (File.Exists(outputPath))
            return outputPath;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Tests", "BotRunner.Tests", "LiveValidation", "Settings", settingsFileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Settings file not found: {settingsFileName}");
    }
}

/// <summary>
/// Result of running a test scenario. Tracks observed state and assertion failures.
/// </summary>
public class ScenarioResult
{
    public TestScenario Scenario { get; }
    public bool SetupFailed { get; set; }
    public string? FailureReason { get; set; }
    public bool GroupFormed { get; set; }
    public bool CombatSeen { get; set; }
    public HashSet<string> ActionTypesSeen { get; } = new();
    public List<string> Failures { get; } = new();

    private readonly Dictionary<int, int> _mapPresence = new();

    public ScenarioResult(TestScenario scenario)
    {
        Scenario = scenario;
    }

    public void TrackMapPresence(int mapId)
    {
        _mapPresence[mapId] = _mapPresence.GetValueOrDefault(mapId, 0) + 1;
    }

    public int GetMapCount(int mapId) => _mapPresence.GetValueOrDefault(mapId, 0);

    /// <summary>
    /// Assert all conditions passed. Throws Xunit assertion failure if any failed.
    /// </summary>
    public void AssertAll()
    {
        if (SetupFailed)
            Xunit.Assert.Fail($"Scenario '{Scenario.Name}' setup failed: {FailureReason}");

        if (Failures.Count > 0)
            Xunit.Assert.Fail($"Scenario '{Scenario.Name}' failed:\n  " + string.Join("\n  ", Failures));
    }
}
