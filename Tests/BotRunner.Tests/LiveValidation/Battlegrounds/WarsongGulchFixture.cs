using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Travel;
using Communication;
using WoWStateManager.Settings;
using BotRunnerType = WoWStateManager.Settings.BotRunnerType;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Fixture for Warsong Gulch tests. Launches 20 bots: 10 Horde (1 FG + 9 BG) and
/// 10 Alliance BG clients.
/// Level 60, PvP gear loadout, elixirs, mount, talents — full combat-ready prep.
/// </summary>
public class WarsongGulchFixture : BattlegroundCoordinatorFixtureBase
{
    public const int HordeBotCount = 10;
    public const int AllianceBotCount = 10;
    public const int TotalBotCount = HordeBotCount + AllianceBotCount;
    public const uint WsgMapId = 489;
    public const string HordeLeaderAccount = "WSGBOT1";
    public const string AllianceLeaderAccount = "WSGBOTA1";

    internal static readonly IReadOnlyList<string> HordeAccountsOrdered = Enumerable.Range(0, HordeBotCount)
        .Select(index => index == 0 ? HordeLeaderAccount : $"WSGBOT{index + 1}")
        .ToArray();

    internal static readonly IReadOnlyList<string> AllianceAccountsOrdered = Enumerable.Range(0, AllianceBotCount)
        .Select(index => index == 0 ? AllianceLeaderAccount : $"WSGBOTA{index + 1}")
        .ToArray();

    protected override string SettingsFileName => "WarsongGulch.settings.json";

    protected override string FixtureLabel => "WSG";

    protected virtual bool UseForegroundHordeLeader => true;

    protected virtual bool UseForegroundAllianceLeader => false;

    protected override TimeSpan EnterWorldMaxTimeout => TimeSpan.FromMinutes(10);

    protected override TimeSpan EnterWorldStaleTimeout => TimeSpan.FromMinutes(3);

    protected override int LaunchThrottleActivationBotCountOverride => 2;

    protected override int MaxPendingStartupBotsOverride => 6;

    protected override uint BattlegroundTypeId => 2;

    protected override uint BattlegroundMapId => WsgMapId;

    protected override int TargetLevel => 60;

    protected override bool PrepareDuringInitialization => false;

    protected override IReadOnlyCollection<string> HordeAccounts => HordeAccountsOrdered;

    protected override IReadOnlyCollection<string> AllianceAccounts => AllianceAccountsOrdered;

    protected override TeleportTarget HordeQueueLocation => new(
        (int)BattlemasterData.OrgrimmarWsg.MapId,
        BattlemasterData.OrgrimmarWsg.Position.X,
        BattlemasterData.OrgrimmarWsg.Position.Y,
        BattlemasterData.OrgrimmarWsg.Position.Z);

    // WSG uses faction group queue.
    // Prep only stages each faction at the battlemasters; StateManager/BotRunner reconcile grouping.

    protected override TeleportTarget AllianceQueueLocation => new(
        (int)BattlemasterData.StormwindWsg.MapId,
        BattlemasterData.StormwindWsg.Position.X,
        BattlemasterData.StormwindWsg.Position.Y,
        BattlemasterData.StormwindWsg.Position.Z);

    protected override IReadOnlyList<CharacterSettings> BuildCharacterSettings()
    {
        var roster = LoadCharacterSettingsFromConfig("WarsongGulch.config.json").ToList();

        foreach (var setting in roster)
        {
            if (setting.AccountName.Equals(HordeLeaderAccount, StringComparison.OrdinalIgnoreCase))
                setting.RunnerType = UseForegroundHordeLeader ? BotRunnerType.Foreground : BotRunnerType.Background;
            else if (setting.AccountName.Equals(AllianceLeaderAccount, StringComparison.OrdinalIgnoreCase))
                setting.RunnerType = UseForegroundAllianceLeader ? BotRunnerType.Foreground : BotRunnerType.Background;

            // P3.6: stamp the per-bot loadout onto settings so the BattlegroundCoordinator
            // hands it off via ApplyLoadout once each bot reaches the staging area. The
            // legacy fixture-driven prep helpers below remain in place as a safety net
            // until live validation confirms the hand-off path covers every BG fixture.
            setting.Loadout ??= AlteracValleyLoadoutPlan.BuildLoadoutSpecSettings(setting);
        }

        return roster;
    }

    // Legacy fixture-driven loadout prep (EnsureLoadoutPreparedAsync,
    // PrepareLoadoutsOnceAsync, RunLoadoutPrepAsync, PrepareLoadoutAsync) was
    // removed in P3.6 — the BattlegroundCoordinator's ApplyingLoadouts state
    // now hands off CharacterSettings.Loadout to BotRunner's LoadoutTask.

    internal Task<ResponseResult> SetRuntimeCoordinatorEnabledAsync(bool enabled)
        => SetCoordinatorEnabledAsync(enabled);

    internal Task ResetTrackedBattlegroundStateAsync(string label)
        => ResetBattlegroundStateAsync(AccountNames, label);

    internal async Task AttemptMountSpellsAsync(IEnumerable<string> accountNames)
    {
        foreach (var accountName in accountNames
            .Where(account => !string.IsNullOrWhiteSpace(account))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var mountSpellId = HordeAccountsOrdered.Contains(accountName, StringComparer.OrdinalIgnoreCase) ? 23509 : 23510;
            var result = await SendSilentActionAsync(accountName, new ActionMessage
            {
                ActionType = ActionType.CastSpell,
                Parameters = { new RequestParameter { IntParam = mountSpellId } }
            });
            if (result != ResponseResult.Success)
            {
                throw new InvalidOperationException(
                    $"WSG mount spell dispatch failed for '{accountName}' (spell={mountSpellId}, result={result}).");
            }

            await Task.Delay(100);
        }
    }
}

