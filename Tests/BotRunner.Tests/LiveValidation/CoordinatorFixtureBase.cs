using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Tests.Infrastructure;
using WoWStateManager.Settings;
using Xunit.Sdk;
using WoWActivitySnapshot = Communication.WoWActivitySnapshot;

namespace BotRunner.Tests.LiveValidation;

public abstract class CoordinatorFixtureBase : LiveBotFixture, IAsyncLifetime
{
    private static readonly JsonSerializerOptions SettingsSerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly object _prepareLock = new();
    private Task? _prepareTask;
    private const int InitialRaidInviteBatchSize = 4;
    private const int MaxGroupFormationAttempts = 3;
    private const float StageMaxDistance2D = 15f;
    private const float StageMaxVerticalDelta = 4.5f;

    public IReadOnlyList<CharacterSettings> CharacterSettings { get; private set; } = Array.Empty<CharacterSettings>();

    public IReadOnlyList<string> AccountNames => CharacterSettings.Select(settings => settings.AccountName).ToArray();

    public int ExpectedBotCount => CharacterSettings.Count;

    protected virtual string FixtureLabel => GetType().Name.Replace("Fixture", string.Empty, StringComparison.Ordinal);

    protected virtual bool DisableCoordinatorDuringPreparation => false;

    protected virtual bool PrepareDuringInitialization => true;

    /// <summary>
    /// When true, launch prep preserves existing characters as long as at least one
    /// character on the account matches the configured race/class/gender.
    /// </summary>
    protected virtual bool PreserveExistingCharactersWhenAnyMatch => false;

    protected virtual TimeSpan EnterWorldMaxTimeout => TimeSpan.FromMinutes(3);

    protected virtual TimeSpan EnterWorldStaleTimeout => TimeSpan.FromSeconds(30);

    protected override TimeSpan InitialWorldEntryTimeout => EnterWorldMaxTimeout;

    /// <summary>
    /// Minimum bots that must enter world. Defaults to all configured bots.
    /// Override for fixtures with unreliable FG bots.
    /// </summary>
    protected virtual int MinimumBotCount => ExpectedBotCount;

    protected virtual async Task AfterPrepareAsync()
    {
        if (!DisableCoordinatorDuringPreparation)
            return;

        var result = await SetCoordinatorEnabledAsync(true);
        Assert.Equal(global::Communication.ResponseResult.Success, result);
    }

    protected abstract string SettingsFileName { get; }

    protected abstract IReadOnlyList<CharacterSettings> BuildCharacterSettings();

    protected virtual void ConfigureCoordinatorEnvironment()
    {
        Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", DisableCoordinatorDuringPreparation ? "1" : "0");
        Environment.SetEnvironmentVariable("WWOW_COORDINATOR_MODE", null);
        Environment.SetEnvironmentVariable("Injection__DisablePacketHooks", null);
        Environment.SetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS", null);
        Environment.SetEnvironmentVariable("WWOW_BG_TYPE", null);
        Environment.SetEnvironmentVariable("WWOW_BG_MAP", null);
        Environment.SetEnvironmentVariable("WWOW_BG_HORDE_QUEUE_MAP", null);
        Environment.SetEnvironmentVariable("WWOW_BG_HORDE_QUEUE_X", null);
        Environment.SetEnvironmentVariable("WWOW_BG_HORDE_QUEUE_Y", null);
        Environment.SetEnvironmentVariable("WWOW_BG_HORDE_QUEUE_Z", null);
        Environment.SetEnvironmentVariable("WWOW_BG_ALLIANCE_QUEUE_MAP", null);
        Environment.SetEnvironmentVariable("WWOW_BG_ALLIANCE_QUEUE_X", null);
        Environment.SetEnvironmentVariable("WWOW_BG_ALLIANCE_QUEUE_Y", null);
        Environment.SetEnvironmentVariable("WWOW_BG_ALLIANCE_QUEUE_Z", null);
    }

    /// <summary>
    /// Hook for fixture-specific offline data prep (for example DB rank updates)
    /// before runners launch and characters enter world.
    /// </summary>
    protected virtual Task PrepareOfflineAccountStateAsync() => Task.CompletedTask;

    protected virtual Task PrepareBotsAsync() => Task.CompletedTask;

    public async Task EnsurePreparedAsync()
    {
        Console.WriteLine($"[PREP] EnsurePreparedAsync called. _prepareTask is {(_prepareTask == null ? "NULL" : "SET")}. IsReady={IsReady}");
        Task prepareTask;
        lock (_prepareLock)
        {
            _prepareTask ??= PrepareBotsOnceAsync();
            prepareTask = _prepareTask;
        }

        await prepareTask;
        Console.WriteLine("[PREP] EnsurePreparedAsync completed");
    }

    public new async Task InitializeAsync()
    {
        CharacterSettings = BuildCharacterSettings();
        if (CharacterSettings.Count == 0)
            throw new InvalidOperationException($"{GetType().Name} must define at least one character setting.");

        await EnsureAccountsAndCharactersReadyForLaunchAsync();
        await PrepareOfflineAccountStateAsync();
        ConfigureCoordinatorEnvironment();
        SkipGroupCleanup = true;
        SetCustomSettingsPath(WriteSettingsFile(CharacterSettings, SettingsFileName));

        await base.InitializeAsync();
        if (!IsReady)
            return;

        await WaitForExactBotCountAsync(MinimumBotCount, EnterWorldMaxTimeout, EnterWorldStaleTimeout, $"{FixtureLabel}:EnterWorld");
        if (PrepareDuringInitialization)
            await EnsurePreparedAsync();
    }

    private async Task PrepareBotsOnceAsync()
    {
        Console.WriteLine($"[PREP] PrepareBotsOnceAsync starting ({GetType().Name})");
        await PrepareBotsAsync();
        Console.WriteLine("[PREP] PrepareBotsAsync complete, refreshing snapshots");
        await RefreshSnapshotsAsync();
        await AfterPrepareAsync();
        Console.WriteLine("[PREP] PrepareBotsOnceAsync complete");
    }

    internal static bool CanReuseExistingCharacters(
        CharacterSettings settings,
        IReadOnlyList<AccountCharacterRecord> existingCharacters)
    {
        if (existingCharacters.Count != 1)
            return false;

        return CharacterMatchesSettings(settings, existingCharacters[0]);
    }

    internal static bool HasAnyMatchingCharacter(
        CharacterSettings settings,
        IReadOnlyList<AccountCharacterRecord> existingCharacters)
    {
        return existingCharacters.Any(existingCharacter => CharacterMatchesSettings(settings, existingCharacter));
    }

    internal static CharacterSettings CreateCharacterSetting(
        string accountName,
        string characterClass,
        string characterRace,
        string characterGender,
        BotRunnerType runnerType)
    {
        return new CharacterSettings
        {
            AccountName = accountName,
            CharacterClass = characterClass,
            CharacterRace = characterRace,
            CharacterGender = characterGender,
            RunnerType = runnerType,
            GmLevel = 6,
            ShouldRun = true,
            Openness = 0.7f,
            Conscientiousness = 0.85f,
            Extraversion = 0.6f,
            Agreeableness = 0.8f,
            Neuroticism = 0.3f,
        };
    }

    internal static string SerializeSettings(IEnumerable<CharacterSettings> settings)
        => JsonSerializer.Serialize(settings, SettingsSerializerOptions);

    internal static async Task<bool> WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        var stopwatch = Stopwatch.StartNew();

        do
        {
            if (await condition())
                return true;

            if (stopwatch.Elapsed >= timeout)
                break;

            await Task.Delay(pollInterval);
        } while (stopwatch.Elapsed < timeout);

        return false;
    }

    internal static bool CharacterMatchesSettings(
        CharacterSettings settings,
        AccountCharacterRecord existingCharacter)
    {
        return MatchesExpectedValue(ParseRaceId(settings.CharacterRace), existingCharacter.RaceId)
            && MatchesExpectedValue(ParseClassId(settings.CharacterClass), existingCharacter.ClassId)
            && MatchesExpectedValue(ParseGenderId(settings.CharacterGender), existingCharacter.GenderId);
    }

    internal static string WriteSettingsFile(IEnumerable<CharacterSettings> settings, string fileName)
    {
        var directory = TestRuntimePaths.GetOrCreateSubdirectory("settings", "WWoW", "TestSettings");

        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, SerializeSettings(settings));
        return path;
    }

    protected async Task WaitForExactBotCountAsync(
        int expectedCount,
        TimeSpan maxTimeout,
        TimeSpan staleTimeout,
        string phaseName)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastCount = -1;
        var lastChange = stopwatch.Elapsed;

        while (stopwatch.Elapsed < maxTimeout)
        {
            if (ClientCrashed)
                throw new XunitException($"[{phaseName}] CRASHED - {CrashMessage ?? "managed child process exited unexpectedly"}");

            await RefreshSnapshotsAsync();
            var currentCount = AllBots.Count;
            if (currentCount >= expectedCount)
                return;

            if (currentCount != lastCount)
            {
                lastCount = currentCount;
                lastChange = stopwatch.Elapsed;
                Console.WriteLine($"[{phaseName}] botCount={currentCount}/{expectedCount} at {stopwatch.Elapsed.TotalSeconds:F0}s");
            }

            if (stopwatch.Elapsed - lastChange > staleTimeout)
            {
                var diagnostics = await DescribeMissingAccountsAsync();
                throw new XunitException(
                    $"[{phaseName}] STALE - bot count stopped at {currentCount}/{expectedCount} for {(stopwatch.Elapsed - lastChange).TotalSeconds:F0}s. {diagnostics}");
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        throw new XunitException($"[{phaseName}] TIMEOUT - expected {expectedCount} bots, got {AllBots.Count}. {await DescribeMissingAccountsAsync()}");
    }

    private async Task EnsureAccountsAndCharactersReadyForLaunchAsync()
    {
        foreach (var settings in CharacterSettings)
        {
            if (!await AccountExistsAsync(settings.AccountName))
            {
                Console.WriteLine($"[{FixtureLabel}:LaunchPrep] creating missing account '{settings.AccountName}' via SOAP");
                _ = await ExecuteGMCommandWithRetryAsync($".account create {settings.AccountName} PASSWORD");

                var accountCreated = await WaitForConditionAsync(
                    () => AccountExistsAsync(settings.AccountName),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromMilliseconds(500));
                if (!accountCreated)
                {
                    throw new InvalidOperationException(
                        $"[{FixtureLabel}:LaunchPrep] account '{settings.AccountName}' was not created before launch.");
                }

                await Task.Delay(250);
            }

            var existingCharacters = (await QueryCharactersForAccountAsync(settings.AccountName)).ToArray();
            if (existingCharacters.Length == 0 || CanReuseExistingCharacters(settings, existingCharacters))
                continue;

            if (PreserveExistingCharactersWhenAnyMatch && HasAnyMatchingCharacter(settings, existingCharacters))
            {
                Console.WriteLine(
                    $"[{FixtureLabel}:LaunchPrep] preserving account '{settings.AccountName}' " +
                    $"with {existingCharacters.Length} existing character(s); at least one matches configured race/class/gender.");
                continue;
            }

            var summary = string.Join(", ", existingCharacters.Select(existingCharacter =>
                $"{existingCharacter.Name}[race={existingCharacter.RaceId}, class={existingCharacter.ClassId}, gender={existingCharacter.GenderId}]"));
            Console.WriteLine($"[{FixtureLabel}:LaunchPrep] resetting account '{settings.AccountName}' before launch: {summary}");

            foreach (var existingCharacter in existingCharacters)
            {
                _ = await ExecuteGMCommandWithRetryAsync($".character erase {existingCharacter.Name}");
                await Task.Delay(250);
            }

            await WaitForCharacterCountAsync(settings.AccountName, expectedCount: 0, TimeSpan.FromSeconds(15));
        }
    }

    private async Task<string> DescribeMissingAccountsAsync()
    {
        static string TrimDiagnostic(string? value, int maxLength = 120)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "-";

            var compact = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (compact.Length <= maxLength)
                return compact;

            return compact[..maxLength] + "...";
        }

        var snapshots = await QueryAllSnapshotsAsync();
        var snapshotsByAccount = snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .GroupBy(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var hydratedAccounts = new HashSet<string>(
            AllBots
                .Select(snapshot => snapshot.AccountName)
                .Where(accountName => !string.IsNullOrWhiteSpace(accountName)),
            StringComparer.OrdinalIgnoreCase);

        var missingAccounts = AccountNames
            .Where(accountName => !hydratedAccounts.Contains(accountName))
            .Select(accountName =>
            {
                var expectedRunner = CharacterSettings
                    .FirstOrDefault(settings => settings.AccountName.Equals(accountName, StringComparison.OrdinalIgnoreCase))
                    ?.RunnerType.ToString() ?? "?";

                if (!snapshotsByAccount.TryGetValue(accountName, out var snapshot))
                    return $"{accountName}(runner={expectedRunner}, no snapshot)";

                var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
                var health = snapshot.Player?.Unit?.Health ?? 0;
                var maxHealth = snapshot.Player?.Unit?.MaxHealth ?? 0;
                var screenState = string.IsNullOrWhiteSpace(snapshot.ScreenState) ? "?" : snapshot.ScreenState;
                var characterName = string.IsNullOrWhiteSpace(snapshot.CharacterName) ? "?" : snapshot.CharacterName;
                var connectionState = snapshot.ConnectionState.ToString();
                var recentError = TrimDiagnostic(snapshot.RecentErrors.LastOrDefault());
                return $"{accountName}(runner={expectedRunner}, screen={screenState}, conn={connectionState}, char={characterName}, objMgr={snapshot.IsObjectManagerValid}, map={mapId}, hp={health}/{maxHealth}, err={recentError})";
            })
            .ToArray();

        return missingAccounts.Length == 0
            ? "missing accounts: none"
            : $"missing accounts: {string.Join(", ", missingAccounts)}";
    }

    private async Task WaitForCharacterCountAsync(string accountName, int expectedCount, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            var currentCharacters = await QueryCharactersForAccountAsync(accountName);
            if (currentCharacters.Count == expectedCount)
                return;

            await Task.Delay(500);
        }

        var finalCharacters = await QueryCharactersForAccountAsync(accountName);
        var details = finalCharacters.Count == 0
            ? "none"
            : string.Join(", ", finalCharacters.Select(character => character.Name));
        throw new InvalidOperationException(
            $"[{FixtureLabel}:LaunchPrep] account '{accountName}' still has {finalCharacters.Count} character(s) after cleanup: {details}");
    }

    private static bool MatchesExpectedValue(byte? expectedValue, byte actualValue)
        => !expectedValue.HasValue || expectedValue.Value == actualValue;

    private static byte? ParseClassId(string? characterClass)
        => ParseEnumId<Class>(characterClass);

    private static byte? ParseGenderId(string? characterGender)
        => ParseEnumId<Gender>(characterGender);

    private static byte? ParseRaceId(string? characterRace)
        => ParseEnumId<Race>(characterRace);

    private static byte? ParseEnumId<TEnum>(string? configuredValue)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
            return null;

        var token = new string(configuredValue.Where(char.IsLetterOrDigit).ToArray());
        if (!Enum.TryParse<TEnum>(token, ignoreCase: true, out var value))
            return null;

        return Convert.ToByte(value);
    }

    internal static IReadOnlyList<string[]> BuildRaidInviteBatches(IReadOnlyCollection<string> accounts)
    {
        var orderedAccounts = NormalizeAccounts(accounts);
        if (orderedAccounts.Count <= 1)
            return Array.Empty<string[]>();

        var members = orderedAccounts.Skip(1).ToList();
        var batches = new List<string[]>
        {
            members.Take(InitialRaidInviteBatchSize).ToArray()
        };

        if (members.Count > InitialRaidInviteBatchSize)
            batches.Add(members.Skip(InitialRaidInviteBatchSize).ToArray());

        return batches;
    }

    internal static IReadOnlyList<string> DescribeAccountsNotGroupedToLeader(
        string leaderAccount,
        IReadOnlyCollection<string> accounts,
        IEnumerable<WoWActivitySnapshot> snapshots)
    {
        var snapshotLookup = snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .GroupBy(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        if (!snapshotLookup.TryGetValue(leaderAccount, out var leaderSnapshot))
            return [$"{leaderAccount}(no snapshot)"];

        var leaderGuid = GetSelfGuid(leaderSnapshot);
        if (leaderGuid == 0)
            return [$"{leaderAccount}(leader guid missing)"];

        var issues = new List<string>();
        foreach (var account in NormalizeAccounts(accounts))
        {
            if (!snapshotLookup.TryGetValue(account, out var snapshot))
            {
                issues.Add($"{account}(no snapshot)");
                continue;
            }

            if (!snapshot.IsObjectManagerValid)
            {
                issues.Add($"{account}(world not ready)");
                continue;
            }

            if (snapshot.PartyLeaderGuid != leaderGuid)
                issues.Add($"{account}(leader=0x{snapshot.PartyLeaderGuid:X})");
        }

        return issues;
    }

    internal static bool IsBattlegroundMapId(uint mapId)
        => mapId is 30u or 489u or 529u;

    internal static IReadOnlyList<string> DescribeAccountsOnBattlegroundMaps(
        IReadOnlyCollection<string> accounts,
        IEnumerable<WoWActivitySnapshot> snapshots)
    {
        var snapshotLookup = snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.AccountName))
            .GroupBy(snapshot => snapshot.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var results = new List<string>();
        foreach (var account in NormalizeAccounts(accounts))
        {
            if (!snapshotLookup.TryGetValue(account, out var snapshot))
                continue;

            var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
            if (IsBattlegroundMapId(mapId))
                results.Add($"{account}(map={mapId})");
        }

        return results;
    }

    private static List<string> NormalizeAccounts(IEnumerable<string> accounts)
    {
        return accounts
            .Where(account => !string.IsNullOrWhiteSpace(account))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ulong GetSelfGuid(WoWActivitySnapshot? snapshot)
        => snapshot?.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;

    private WoWActivitySnapshot? FindSnapshot(string accountName)
    {
        return AllBots.LastOrDefault(snapshot =>
            accountName.Equals(snapshot.AccountName, StringComparison.OrdinalIgnoreCase));
    }

    protected async Task EnsureAccountsNotGroupedAsync(
        IReadOnlyCollection<string> accounts,
        string label)
    {
        var orderedAccounts = NormalizeAccounts(accounts);
        if (orderedAccounts.Count == 0)
            return;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await RefreshSnapshotsAsync();

            var groupedSnapshots = orderedAccounts
                .Select(account => (Account: account, Snapshot: FindSnapshot(account)))
                .Where(item => item.Snapshot?.PartyLeaderGuid != 0)
                .ToArray();

            if (groupedSnapshots.Length == 0)
                return;

            foreach (var (account, snapshot) in groupedSnapshots)
            {
                var selfGuid = GetSelfGuid(snapshot);
                var actionType = selfGuid != 0 && snapshot!.PartyLeaderGuid == selfGuid
                    ? Communication.ActionType.DisbandGroup
                    : Communication.ActionType.LeaveGroup;

                Console.WriteLine(
                    $"[{FixtureLabel}:{label}] clearing stale group for {account} via {actionType} (attempt {attempt}/5)");
                var result = await SendActionAsync(account, new Communication.ActionMessage { ActionType = actionType });
                if (result != Communication.ResponseResult.Success)
                    throw new XunitException($"[{FixtureLabel}:{label}] Failed to clear stale group for {account}: {result}");

                await Task.Delay(250);
            }

            await Task.Delay(1200);
        }

        await RefreshSnapshotsAsync();
        var remaining = orderedAccounts
            .Where(account => (FindSnapshot(account)?.PartyLeaderGuid ?? 0) != 0)
            .ToArray();
        if (remaining.Length > 0)
        {
            throw new XunitException(
                $"[{FixtureLabel}:{label}] Failed to clear existing group state: {string.Join(", ", remaining)}");
        }
    }

    private async Task<bool> WaitForAccountsGroupedToLeaderAsync(
        IReadOnlyCollection<string> accounts,
        string leaderAccount,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            await RefreshSnapshotsAsync();
            if (DescribeAccountsNotGroupedToLeader(leaderAccount, accounts, AllBots).Count == 0)
                return true;

            await Task.Delay(400);
        }

        return false;
    }

    private async Task InviteMemberToLeaderAsync(
        string leaderAccount,
        string memberAccount,
        string label)
    {
        for (var attempt = 1; attempt <= MaxGroupFormationAttempts; attempt++)
        {
            await RefreshSnapshotsAsync();

            var leaderSnapshot = FindSnapshot(leaderAccount);
            var memberSnapshot = FindSnapshot(memberAccount);
            var leaderGuid = GetSelfGuid(leaderSnapshot);
            var memberName = memberSnapshot?.CharacterName;

            if (leaderGuid != 0 && memberSnapshot?.PartyLeaderGuid == leaderGuid)
                return;

            if (leaderGuid == 0 || string.IsNullOrWhiteSpace(memberName))
            {
                await Task.Delay(500);
                continue;
            }

            Console.WriteLine(
                $"[{FixtureLabel}:{label}] {leaderAccount} inviting {memberAccount} ({memberName}) (attempt {attempt}/{MaxGroupFormationAttempts})");
            var inviteResult = await SendActionAsync(
                leaderAccount,
                new Communication.ActionMessage
                {
                    ActionType = Communication.ActionType.SendGroupInvite,
                    Parameters = { new Communication.RequestParameter { StringParam = memberName } }
                });
            if (inviteResult != Communication.ResponseResult.Success)
            {
                await Task.Delay(750);
                continue;
            }

            await Task.Delay(900);

            var acceptResult = await SendActionAsync(
                memberAccount,
                new Communication.ActionMessage { ActionType = Communication.ActionType.AcceptGroupInvite });
            if (acceptResult != Communication.ResponseResult.Success)
            {
                await Task.Delay(750);
                continue;
            }

            if (await WaitForAccountsGroupedToLeaderAsync(
                [leaderAccount, memberAccount],
                leaderAccount,
                TimeSpan.FromSeconds(8)))
            {
                return;
            }
        }

        await RefreshSnapshotsAsync();
        var issues = DescribeAccountsNotGroupedToLeader(leaderAccount, [leaderAccount, memberAccount], AllBots);
        throw new XunitException(
            $"[{FixtureLabel}:{label}] Failed to group {memberAccount} with {leaderAccount}: {string.Join(", ", issues)}");
    }

    private async Task ConvertFactionToRaidAsync(
        string leaderAccount,
        IReadOnlyCollection<string> currentGroupAccounts,
        string label)
    {
        for (var attempt = 1; attempt <= MaxGroupFormationAttempts; attempt++)
        {
            Console.WriteLine(
                $"[{FixtureLabel}:{label}] converting {leaderAccount} party to raid (attempt {attempt}/{MaxGroupFormationAttempts})");
            var result = await SendActionAsync(
                leaderAccount,
                new Communication.ActionMessage { ActionType = Communication.ActionType.ConvertToRaid });
            if (result != Communication.ResponseResult.Success)
            {
                await Task.Delay(1000);
                continue;
            }

            if (await WaitForAccountsGroupedToLeaderAsync(currentGroupAccounts, leaderAccount, TimeSpan.FromSeconds(8)))
                return;
        }

        await RefreshSnapshotsAsync();
        var issues = DescribeAccountsNotGroupedToLeader(leaderAccount, currentGroupAccounts, AllBots);
        throw new XunitException(
            $"[{FixtureLabel}:{label}] Failed to stabilize raid conversion for {leaderAccount}: {string.Join(", ", issues)}");
    }

    private async Task FormFactionRaidAsync(
        IReadOnlyCollection<string> accounts,
        string label)
    {
        var orderedAccounts = NormalizeAccounts(accounts);
        if (orderedAccounts.Count <= 1)
            return;

        var leaderAccount = orderedAccounts[0];
        Console.WriteLine(
            $"[{FixtureLabel}:{label}] forming faction raid with leader {leaderAccount} and {orderedAccounts.Count - 1} members");

        await EnsureAccountsNotGroupedAsync(orderedAccounts, label);

        var inviteBatches = BuildRaidInviteBatches(orderedAccounts);
        foreach (var memberAccount in inviteBatches[0])
            await InviteMemberToLeaderAsync(leaderAccount, memberAccount, label);

        if (inviteBatches.Count > 1)
        {
            var initialGroup = orderedAccounts.Take(InitialRaidInviteBatchSize + 1).ToArray();
            await ConvertFactionToRaidAsync(leaderAccount, initialGroup, label);

            foreach (var memberAccount in inviteBatches[1])
                await InviteMemberToLeaderAsync(leaderAccount, memberAccount, label);
        }

        if (!await WaitForAccountsGroupedToLeaderAsync(
            orderedAccounts,
            leaderAccount,
            TimeSpan.FromSeconds(Math.Max(20, orderedAccounts.Count * 3))))
        {
            await RefreshSnapshotsAsync();
            var issues = DescribeAccountsNotGroupedToLeader(leaderAccount, orderedAccounts, AllBots);
            throw new XunitException(
                $"[{FixtureLabel}:{label}] Faction raid did not stabilize: {string.Join(", ", issues)}");
        }
    }

    protected async Task ResetBattlegroundStateAsync(
        IReadOnlyCollection<string> accounts,
        string label)
    {
        var orderedAccounts = NormalizeAccounts(accounts);
        if (orderedAccounts.Count == 0)
            return;

        await RefreshSnapshotsAsync();
        var accountsOnBgMaps = DescribeAccountsOnBattlegroundMaps(orderedAccounts, AllBots);
        if (accountsOnBgMaps.Count > 0)
        {
            Console.WriteLine(
                $"[{FixtureLabel}:{label}] accounts still on battleground maps before reset: {string.Join(", ", accountsOnBgMaps)}");
        }

        for (var pass = 1; pass <= 2; pass++)
        {
            Console.WriteLine(
                $"[{FixtureLabel}:{label}] sending LeaveBattleground to {orderedAccounts.Count} accounts (pass {pass}/2)");

            foreach (var account in orderedAccounts)
            {
                var result = await SendActionAsync(
                    account,
                    new Communication.ActionMessage { ActionType = Communication.ActionType.LeaveBattleground });
                if (result != Communication.ResponseResult.Success)
                {
                    throw new XunitException(
                        $"[{FixtureLabel}:{label}] Failed to leave battleground for {account}: {result}");
                }

                await Task.Delay(150);
            }

            await Task.Delay(1500);
        }

        await RefreshSnapshotsAsync();
        var remainingOnBgMaps = DescribeAccountsOnBattlegroundMaps(orderedAccounts, AllBots);
        if (remainingOnBgMaps.Count > 0)
        {
            Console.WriteLine(
                $"[{FixtureLabel}:{label}] accounts still on battleground maps after reset: {string.Join(", ", remainingOnBgMaps)}");
        }
    }

    protected async Task EnsureAccountsStagedAtLocationAsync(
        IReadOnlyCollection<string> accounts,
        TeleportTarget target,
        string label)
    {
        foreach (var account in accounts)
        {
            await BotTeleportAsync(account, target.MapId, target.X, target.Y, target.Z);
            await Task.Delay(200);
        }

        var unstagedAccounts = await WaitForAccountsNearTargetAsync(accounts, target, TimeSpan.FromSeconds(15));
        if (unstagedAccounts.Count > 0)
        {
            foreach (var account in unstagedAccounts)
            {
                await BotTeleportAsync(account, target.MapId, target.X, target.Y, target.Z);
                await Task.Delay(200);
            }

            unstagedAccounts = await WaitForAccountsNearTargetAsync(unstagedAccounts, target, TimeSpan.FromSeconds(15));
        }

        if (unstagedAccounts.Count > 0)
        {
            throw new XunitException(
                $"[{FixtureLabel}:{label}] Failed to stage: {await DescribeAccountsAgainstTargetAsync(unstagedAccounts, target)}");
        }
    }

    private async Task<List<string>> WaitForAccountsNearTargetAsync(
        IReadOnlyCollection<string> accounts,
        TeleportTarget target,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var remaining = accounts.ToList();

        while (stopwatch.Elapsed < timeout)
        {
            await RefreshSnapshotsAsync();
            remaining = accounts
                .Where(account => !IsSnapshotNearTarget(AllBots.FirstOrDefault(snapshot =>
                    account.Equals(snapshot.AccountName, StringComparison.OrdinalIgnoreCase)), target))
                .ToList();

            if (remaining.Count == 0)
                return remaining;

            await Task.Delay(500);
        }

        return remaining;
    }

    private async Task<string> DescribeAccountsAgainstTargetAsync(
        IReadOnlyCollection<string> accounts,
        TeleportTarget target)
    {
        await RefreshSnapshotsAsync();

        return string.Join(", ", accounts.Select(account =>
        {
            var snapshot = AllBots.FirstOrDefault(item => account.Equals(item.AccountName, StringComparison.OrdinalIgnoreCase));
            if (snapshot == null)
                return $"{account}(no snapshot)";

            var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
            var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;
            if (position == null)
                return $"{account}(map={mapId}, pos=unknown)";

            var distance = DistanceToTarget(position.X, position.Y, target.X, target.Y);
            var zDelta = MathF.Abs(position.Z - target.Z);
            return $"{account}(map={mapId}, dist={distance:F1}, z={position.Z:F1}, dz={zDelta:F1})";
        }));
    }

    private static float DistanceToTarget(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool IsSnapshotNearTarget(WoWActivitySnapshot? snapshot, TeleportTarget target)
    {
        if (snapshot == null)
            return false;

        var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? snapshot.CurrentMapId;
        if (mapId != target.MapId)
            return false;

        var position = snapshot.Player?.Unit?.GameObject?.Base?.Position;
        if (position == null)
            return false;

        var distance2D = DistanceToTarget(position.X, position.Y, target.X, target.Y);
        var zDelta = MathF.Abs(position.Z - target.Z);
        return distance2D <= StageMaxDistance2D && zDelta <= StageMaxVerticalDelta;
    }

    protected async Task ReviveAndLevelBotsAsync(int targetLevel)
    {
        await RefreshSnapshotsAsync();
        Console.WriteLine($"[PREP] ReviveAndLevel: AllBots.Count={AllBots.Count}, Expected={ExpectedBotCount}, Min={MinimumBotCount}, targetLevel={targetLevel}");
        if (AllBots.Count < MinimumBotCount)
            throw new XunitException($"[{FixtureLabel}:Prep] Expected at least {MinimumBotCount} bots before prep, got {AllBots.Count}");

        foreach (var snapshot in AllBots)
        {
            Console.WriteLine($"[PREP] .revive {snapshot.CharacterName}");
            await ExecuteGMCommandAsync($".revive {snapshot.CharacterName}");
        }
        await Task.Delay(1000);

        // Use bot chat .levelup instead of SOAP .character level.
        // SOAP .character level only updates DB; for online characters the
        // in-memory level stays at 1. Bot chat .levelup applies immediately
        // because it executes in the game client context.
        // IMPORTANT: .levelup adds N levels to CURRENT level — must compute delta.
        var overLeveledCount = 0;
        foreach (var snapshot in AllBots)
        {
            var currentLevel = (int)(snapshot.Player?.Unit?.GameObject?.Level ?? 1);
            var account = snapshot.AccountName ?? snapshot.CharacterName;

            if (currentLevel > targetLevel)
            {
                // Over-leveled from a previous run. Use .reset level (resets to 1) then re-level.
                Console.WriteLine($"[PREP] {account} is level {currentLevel} > target {targetLevel}. Resetting to 1.");
                await SendGmChatCommandAsync(account, ".reset level");
                overLeveledCount++;
            }
        }

        if (overLeveledCount > 0)
        {
            // Wait for reset level to take effect on server + refresh snapshots
            await Task.Delay(2000);
            await RefreshSnapshotsAsync();
        }

        foreach (var snapshot in AllBots)
        {
            var currentLevel = (int)(snapshot.Player?.Unit?.GameObject?.Level ?? 1);
            var account = snapshot.AccountName ?? snapshot.CharacterName;

            if (currentLevel >= targetLevel)
                continue;

            var levelsToAdd = targetLevel - currentLevel;
            Console.WriteLine($"[PREP] .levelup {levelsToAdd} for {account} (current={currentLevel}, target={targetLevel})");
            await SendGmChatCommandAsync(account, $".levelup {levelsToAdd}");
        }
        await Task.Delay(2000);
    }

    protected async Task StageBattlegroundRaidAsync(
        IReadOnlyCollection<string> hordeAccounts,
        TeleportTarget hordeQueueLocation,
        IReadOnlyCollection<string> allianceAccounts,
        TeleportTarget allianceQueueLocation)
    {
        await ResetBattlegroundStateAsync(AccountNames, "BgResetPreStage");
        await EnsureAccountsStagedAtLocationAsync(hordeAccounts, hordeQueueLocation, "HordeStage");
        await EnsureAccountsStagedAtLocationAsync(allianceAccounts, allianceQueueLocation, "AllianceStage");
        await ResetBattlegroundStateAsync(AccountNames, "BgResetPostStage");
        await FormFactionRaidAsync(hordeAccounts, "HordeRaid");
        await FormFactionRaidAsync(allianceAccounts, "AllianceRaid");
        await ResetBattlegroundStateAsync(AccountNames, "BgResetPostRaid");

        foreach (var account in AccountNames)
            await SendGmChatCommandAsync(account, ".gm off");
        await Task.Delay(1000);
    }

    protected async Task PrepareBotsForBattlegroundAsync(
        int targetLevel,
        IReadOnlyCollection<string> hordeAccounts,
        TeleportTarget hordeQueueLocation,
        IReadOnlyCollection<string> allianceAccounts,
        TeleportTarget allianceQueueLocation)
    {
        await ReviveAndLevelBotsAsync(targetLevel);
        await StageBattlegroundRaidAsync(hordeAccounts, hordeQueueLocation, allianceAccounts, allianceQueueLocation);
    }

    protected readonly record struct TeleportTarget(int MapId, float X, float Y, float Z);
}

public abstract class BattlegroundCoordinatorFixtureBase : CoordinatorFixtureBase
{
    protected override bool PrepareDuringInitialization => false;
    protected override bool DisableCoordinatorDuringPreparation => true;
    protected override bool PreserveExistingCharactersWhenAnyMatch => true;

    // Freshly created battleground characters currently need to sit through their
    // intro cinematic before the first hydrated InWorld snapshot appears.
    protected override TimeSpan EnterWorldMaxTimeout => TimeSpan.FromMinutes(5);

    protected override TimeSpan EnterWorldStaleTimeout => TimeSpan.FromSeconds(90);

    protected abstract uint BattlegroundTypeId { get; }

    protected abstract uint BattlegroundMapId { get; }

    protected abstract int TargetLevel { get; }

    protected abstract IReadOnlyCollection<string> HordeAccounts { get; }

    protected abstract IReadOnlyCollection<string> AllianceAccounts { get; }

    protected abstract TeleportTarget HordeQueueLocation { get; }

    protected abstract TeleportTarget AllianceQueueLocation { get; }

    protected override void ConfigureCoordinatorEnvironment()
    {
        base.ConfigureCoordinatorEnvironment();
        Environment.SetEnvironmentVariable("WWOW_TEST_DISABLE_COORDINATOR", "1");
        // Foreground packet hooks are unstable during battleground map transfers.
        Environment.SetEnvironmentVariable("Injection__DisablePacketHooks", "true");
        Environment.SetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS", "1");
        Environment.SetEnvironmentVariable("WWOW_COORDINATOR_MODE", "battleground");
        Environment.SetEnvironmentVariable("WWOW_BG_TYPE", BattlegroundTypeId.ToString());
        Environment.SetEnvironmentVariable("WWOW_BG_MAP", BattlegroundMapId.ToString());
        Environment.SetEnvironmentVariable("WWOW_BG_HORDE_QUEUE_MAP", HordeQueueLocation.MapId.ToString());
        Environment.SetEnvironmentVariable("WWOW_BG_HORDE_QUEUE_X", HordeQueueLocation.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("WWOW_BG_HORDE_QUEUE_Y", HordeQueueLocation.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("WWOW_BG_HORDE_QUEUE_Z", HordeQueueLocation.Z.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("WWOW_BG_ALLIANCE_QUEUE_MAP", AllianceQueueLocation.MapId.ToString());
        Environment.SetEnvironmentVariable("WWOW_BG_ALLIANCE_QUEUE_X", AllianceQueueLocation.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("WWOW_BG_ALLIANCE_QUEUE_Y", AllianceQueueLocation.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("WWOW_BG_ALLIANCE_QUEUE_Z", AllianceQueueLocation.Z.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    protected override Task PrepareBotsAsync()
        => PrepareBotsForBattlegroundAsync(TargetLevel, HordeAccounts, HordeQueueLocation, AllianceAccounts, AllianceQueueLocation);

    protected override async Task AfterPrepareAsync()
    {
        await base.AfterPrepareAsync();
        await RefreshSnapshotsAsync();
    }
}
