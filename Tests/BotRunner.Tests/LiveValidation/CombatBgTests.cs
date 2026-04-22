using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// BG combat test: two dedicated background warriors attack the same natural
/// Valley of Trials boar from a fresh arena-style fixture.
/// </summary>
[RequiresMangosStack]
[Collection(CombatBgArenaCollection.Name)]
public class CombatBgTests
{
    private readonly CombatBgArenaFixture _bot;
    private readonly ITestOutputHelper _output;

    private const uint MottledBoarEntry = 3098;
    private const string MottledBoarName = "Mottled Boar";
    private const ulong CreatureGuidHighMask = 0xF000000000000000UL;
    private const ulong CreatureGuidHighPrefix = 0xF000000000000000UL;

    public CombatBgTests(CombatBgArenaFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "CombatBgArena fixture not ready");
    }

    [SkippableFact]
    public async Task Combat_BG_AutoAttacksSharedBoar_FromFreshBackgroundRoster()
    {
        await _bot.RefreshSnapshotsAsync();

        var primary = await _bot.GetSnapshotAsync(_bot.PrimaryBgAccount);
        var secondary = await _bot.GetSnapshotAsync(_bot.SecondaryBgAccount);
        Assert.NotNull(primary);
        Assert.NotNull(secondary);

        var primaryGuid = primary!.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        var secondaryGuid = secondary!.Player?.Unit?.GameObject?.Base?.Guid ?? 0UL;
        Assert.True(primaryGuid != 0, "Primary BG bot must have a self GUID after world entry.");
        Assert.True(secondaryGuid != 0, "Secondary BG bot must have a self GUID after world entry.");

        _output.WriteLine(
            $"[COMBAT-BG] Start state: {_bot.PrimaryBgAccount}/{primary.CharacterName} " +
            $"(HP={primary.Player?.Unit?.Health}/{primary.Player?.Unit?.MaxHealth}) and " +
            $"{_bot.SecondaryBgAccount}/{secondary.CharacterName} " +
            $"(HP={secondary.Player?.Unit?.Health}/{secondary.Player?.Unit?.MaxHealth})");

        var targetGuid = await FindBoarVisibleToBothBotsAsync(
            _bot.PrimaryBgAccount,
            primaryGuid,
            _bot.SecondaryBgAccount,
            secondaryGuid,
            TimeSpan.FromSeconds(20));
        Assert.True(targetGuid != 0,
            "A Mottled Boar must be visible in both BG snapshots within 20s of staging.");

        foreach (var account in new[] { _bot.PrimaryBgAccount, _bot.SecondaryBgAccount })
        {
            var result = await _bot.SendActionAsync(account, new ActionMessage
            {
                ActionType = ActionType.StartMeleeAttack,
                Parameters = { new RequestParameter { LongParam = (long)targetGuid } }
            });
            Assert.Equal(ResponseResult.Success, result);
        }

        var deadOrGone = await WaitForMobDeadOrGoneOnEitherBotAsync(
            targetGuid,
            _bot.PrimaryBgAccount,
            _bot.SecondaryBgAccount,
            TimeSpan.FromSeconds(45));
        Assert.True(deadOrGone,
            $"Boar 0x{targetGuid:X} should die from real melee combat within 45s.");

        await _bot.RefreshSnapshotsAsync();
        primary = await _bot.GetSnapshotAsync(_bot.PrimaryBgAccount);
        secondary = await _bot.GetSnapshotAsync(_bot.SecondaryBgAccount);
        Assert.NotNull(primary);
        Assert.NotNull(secondary);

        var primaryHealth = primary!.Player?.Unit?.Health ?? 0;
        var secondaryHealth = secondary!.Player?.Unit?.Health ?? 0;
        Assert.True(primaryHealth > 0, $"Primary BG attacker should survive the fight (HP={primaryHealth}).");
        Assert.True(secondaryHealth > 0, $"Secondary BG attacker should survive the fight (HP={secondaryHealth}).");
    }

    private async Task<ulong> FindBoarVisibleToBothBotsAsync(
        string firstAccount,
        ulong firstSelfGuid,
        string secondAccount,
        ulong secondSelfGuid,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var first = await _bot.GetSnapshotAsync(firstAccount);
            var second = await _bot.GetSnapshotAsync(secondAccount);

            var firstGuids = CollectLivingBoarGuids(first, firstSelfGuid);
            var secondGuids = CollectLivingBoarGuids(second, secondSelfGuid);
            var shared = firstGuids.Intersect(secondGuids).FirstOrDefault();
            if (shared != 0)
                return shared;

            await Task.Delay(500);
        }

        return 0UL;
    }

    private static List<ulong> CollectLivingBoarGuids(WoWActivitySnapshot? snap, ulong selfGuid)
    {
        return snap?.NearbyUnits?
            .Where(unit =>
            {
                var guid = unit.GameObject?.Base?.Guid ?? 0UL;
                if (guid == 0 || guid == selfGuid)
                    return false;
                if ((guid & CreatureGuidHighMask) != CreatureGuidHighPrefix)
                    return false;
                if (unit.Health == 0 || unit.MaxHealth == 0)
                    return false;
                if (unit.GameObject?.Level > 10)
                    return false;
                if (unit.MaxHealth > 200)
                    return false;
                if (unit.NpcFlags != 0)
                    return false;

                var entry = unit.GameObject?.Entry ?? 0;
                var name = unit.GameObject?.Name ?? string.Empty;
                return entry == MottledBoarEntry
                    || string.Equals(name, MottledBoarName, StringComparison.OrdinalIgnoreCase)
                    || name.Contains("mottled boar", StringComparison.OrdinalIgnoreCase);
            })
            .Select(unit => unit.GameObject?.Base?.Guid ?? 0UL)
            .Where(guid => guid != 0)
            .ToList() ?? new List<ulong>();
    }

    private async Task<bool> WaitForMobDeadOrGoneOnEitherBotAsync(
        ulong targetGuid,
        string firstAccount,
        string secondAccount,
        TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            await _bot.RefreshSnapshotsAsync();
            var first = await _bot.GetSnapshotAsync(firstAccount);
            var second = await _bot.GetSnapshotAsync(secondAccount);
            if (IsTargetDeadOrGone(first, targetGuid) || IsTargetDeadOrGone(second, targetGuid))
                return true;

            await Task.Delay(350);
        }

        return false;
    }

    private static bool IsTargetDeadOrGone(WoWActivitySnapshot? snap, ulong targetGuid)
    {
        var target = snap?.NearbyUnits?.FirstOrDefault(unit => (unit.GameObject?.Base?.Guid ?? 0UL) == targetGuid);
        return target == null || target.Health == 0;
    }
}
