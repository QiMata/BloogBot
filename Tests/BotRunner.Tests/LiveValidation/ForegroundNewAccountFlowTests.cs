using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Communication;
using WoWStateManager.Settings;
using Xunit;
using Xunit.Abstractions;
using BotRunnerType = WoWStateManager.Settings.BotRunnerType;

namespace BotRunner.Tests.LiveValidation;

[CollectionDefinition(ForegroundNewAccountCollection.Name)]
public sealed class ForegroundNewAccountCollection : ICollectionFixture<ForegroundNewAccountFixture>
{
    public const string Name = "ForegroundNewAccountCollection";
}

/// <summary>
/// Focused fixture for diagnosing first-login foreground stability:
/// one dedicated FG account, no existing character reuse, and no battleground bring-up.
/// </summary>
public sealed class ForegroundNewAccountFixture : CoordinatorFixtureBase
{
    private readonly string _accountName = BuildUniqueAccountName();

    public string AccountName => _accountName;

    protected override string SettingsFileName => "ForegroundNewAccount.settings.json";

    protected override string FixtureLabel => "FGNEW";

    protected override bool DisableCoordinatorDuringPreparation => true;

    protected override bool PrepareDuringInitialization => false;

    protected override TimeSpan EnterWorldMaxTimeout => TimeSpan.FromMinutes(4);

    protected override TimeSpan EnterWorldStaleTimeout => TimeSpan.FromSeconds(75);

    protected override IReadOnlyList<CharacterSettings> BuildCharacterSettings()
    {
        return
        [
            CreateCharacterSetting(
                accountName: _accountName,
                characterClass: "Warrior",
                characterRace: "Orc",
                characterGender: "Female",
                runnerType: BotRunnerType.Foreground)
        ];
    }

    public async Task<WoWActivitySnapshot?> QueryForegroundSnapshotAsync()
    {
        var snapshots = await QueryAllSnapshotsAsync();
        return snapshots.FirstOrDefault(snapshot =>
            snapshot.AccountName.Equals(_accountName, StringComparison.OrdinalIgnoreCase));
    }

    internal Task<IReadOnlyList<AccountCharacterRecord>> QueryCreatedCharactersAsync()
        => QueryCharactersForAccountAsync(_accountName);

    private static string BuildUniqueAccountName()
    {
        // Keep account names deterministic-format and short enough for legacy auth paths.
        // Example: FGNEW0409153045 (MMDDHHMMSS)
        return "FGNEW" + DateTime.UtcNow.ToString("MMddHHmmss", CultureInfo.InvariantCulture);
    }
}

[Collection(ForegroundNewAccountCollection.Name)]
public sealed class ForegroundNewAccountFlowTests
{
    private readonly ForegroundNewAccountFixture _bot;
    private readonly ITestOutputHelper _output;

    public ForegroundNewAccountFlowTests(ForegroundNewAccountFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
    }

    [Fact]
    public async Task NewAccount_NewCharacter_EntersWorld()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");

        await _bot.RefreshSnapshotsAsync();
        var snapshot = await _bot.QueryForegroundSnapshotAsync();
        Assert.NotNull(snapshot);
        Assert.Equal("InWorld", snapshot!.ScreenState);
        Assert.True(snapshot.IsObjectManagerValid);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.CharacterName));

        var accountCharacters = await _bot.QueryCreatedCharactersAsync();
        Assert.NotEmpty(accountCharacters);
        Assert.Contains(
            accountCharacters,
            accountCharacter => accountCharacter.Name.Equals(snapshot.CharacterName, StringComparison.OrdinalIgnoreCase));

        _output.WriteLine(
            $"[FG-NEW] account={_bot.AccountName} screen={snapshot.ScreenState} objMgr={snapshot.IsObjectManagerValid} " +
            $"char={snapshot.CharacterName} dbChars={string.Join(", ", accountCharacters.Select(character => character.Name))}");
    }
}
