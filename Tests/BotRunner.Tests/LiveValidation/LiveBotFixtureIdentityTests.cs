using System.Reflection;
using Communication;
using Game;

namespace BotRunner.Tests.LiveValidation;

public class LiveBotFixtureIdentityTests
{
    [Fact]
    public void NormalizeSnapshotCharacterName_UsesKnownAccountMappingToPassHydrationGate()
    {
        var fixture = new TestLiveBotFixture();
        fixture.SeedKnownCharacterName("ABBOT2", "Zulmokthaoud");

        var snapshot = new WoWActivitySnapshot
        {
            AccountName = "ABBOT2",
            ScreenState = "InWorld",
            IsObjectManagerValid = true,
            Player = new WoWPlayer
            {
                Unit = new WoWUnit
                {
                    Health = 410,
                    MaxHealth = 410,
                    GameObject = new WoWGameObject
                    {
                        Base = new WoWObject
                        {
                            Position = new Position { X = 1, Y = 2, Z = 3 }
                        }
                    }
                }
            }
        };

        fixture.Normalize(snapshot);

        Assert.Equal("Zulmokthaoud", snapshot.CharacterName);
        Assert.True(InvokeIsHydratedInWorldSnapshot(snapshot));
    }

    private static bool InvokeIsHydratedInWorldSnapshot(WoWActivitySnapshot snapshot)
    {
        var method = typeof(LiveBotFixture).GetMethod(
            "IsHydratedInWorldSnapshot",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method!.Invoke(null, [snapshot]));
    }

    private sealed class TestLiveBotFixture : LiveBotFixture
    {
        public void SeedKnownCharacterName(string accountName, string characterName)
            => RememberKnownCharacterName(accountName, characterName);

        public void Normalize(WoWActivitySnapshot snapshot)
            => NormalizeSnapshotCharacterName(snapshot);
    }
}
