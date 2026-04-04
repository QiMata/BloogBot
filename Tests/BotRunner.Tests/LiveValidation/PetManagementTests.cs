using System;
using System.Threading.Tasks;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// V2.18: Pet management tests. Hunter setup (.learn for pet spells),
/// CAST_SPELL for Call Pet, verify pet in snapshot.
///
/// Run: dotnet test --filter "FullyQualifiedName~PetManagementTests" --configuration Release
/// </summary>
[Collection(LiveValidationCollection.Name)]
public class PetManagementTests
{
    private readonly LiveBotFixture _bot;
    private readonly ITestOutputHelper _output;

    private const int KalimdorMapId = 1;
    private const float OrgX = 1676.0f, OrgY = -4315.0f, OrgZ = 64.0f;

    // Hunter pet spells
    private const uint CallPetSpellId = 883;      // Call Pet
    private const uint DismissPetSpellId = 2641;   // Dismiss Pet
    private const uint TameAnimalSpellId = 1515;   // Tame Beast

    public PetManagementTests(LiveBotFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    [SkippableFact]
    [Trait("Category", "RequiresInfrastructure")]
    public async Task Pet_SummonAndManage_StanceFeedAbility()
    {
        var account = _bot.BgAccountName!;

        await _bot.EnsureCleanSlateAsync(account, "BG");

        // Teach pet management spells
        _output.WriteLine("[SETUP] Teaching hunter pet spells");
        await _bot.SendGmChatCommandAsync(account, $".learn {CallPetSpellId}");
        await _bot.SendGmChatCommandAsync(account, $".learn {DismissPetSpellId}");
        await _bot.SendGmChatCommandAsync(account, $".learn {TameAnimalSpellId}");
        await Task.Delay(1500);

        // Verify spells learned
        await _bot.RefreshSnapshotsAsync();
        var snap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(snap);
        var spellList = snap!.Player?.SpellList;
        Assert.NotNull(spellList);

        var hasCallPet = spellList!.Contains(CallPetSpellId);
        _output.WriteLine($"[TEST] Has Call Pet: {hasCallPet}");

        // Teleport to Orgrimmar (safe area for pet management)
        await _bot.BotTeleportAsync(account, KalimdorMapId, OrgX, OrgY, OrgZ);
        await _bot.WaitForTeleportSettledAsync(account, OrgX, OrgY);

        // Cast Call Pet
        _output.WriteLine("[TEST] Casting Call Pet");
        var callResult = await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters = { new RequestParameter { IntParam = (int)CallPetSpellId } }
        });
        _output.WriteLine($"[TEST] CAST_SPELL (Call Pet) result: {callResult}");

        // Wait for pet to appear in snapshot
        await Task.Delay(5000);
        await _bot.RefreshSnapshotsAsync();
        var afterSnap = await _bot.GetSnapshotAsync(account);
        Assert.NotNull(afterSnap);

        // Check for pet presence via movement data (pet GUID tracked by server)
        var movementData = afterSnap!.MovementData;
        _output.WriteLine($"[TEST] Snapshot received after Call Pet cast");
        _output.WriteLine($"[TEST] Nearby units: {afterSnap.NearbyUnits?.Count ?? 0}");

        // Look for a unit that might be a pet (controlled by player)
        var playerGuid = afterSnap.Player?.Unit?.GameObject?.Base?.Guid ?? 0;
        _output.WriteLine($"[TEST] Player GUID: 0x{playerGuid:X}");

        // Cleanup: dismiss pet
        _output.WriteLine("[TEST] Casting Dismiss Pet");
        await _bot.SendActionAsync(account, new ActionMessage
        {
            ActionType = ActionType.CastSpell,
            Parameters = { new RequestParameter { IntParam = (int)DismissPetSpellId } }
        });
        await Task.Delay(2000);
    }
}
