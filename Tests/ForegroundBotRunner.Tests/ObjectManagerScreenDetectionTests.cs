using ForegroundBotRunner.Statics;
using GameData.Core.Enums;

namespace ForegroundBotRunner.Tests;

public sealed class ObjectManagerScreenDetectionTests
{
    [Fact]
    public void ResolveScreenState_MovieWithCharacterList_TreatedAsLoadingWorld()
    {
        var screenState = ObjectManager.ResolveScreenState(
            loginState: "movie",
            continentId: 0xFFFFFFFF,
            hasEnteredWorld: false,
            maxCharacterCount: 1);

        Assert.Equal(WoWScreenState.LoadingWorld, screenState);
    }

    [Fact]
    public void ResolveScreenState_MovieWithoutCharacterList_RemainsLoginScreen()
    {
        var screenState = ObjectManager.ResolveScreenState(
            loginState: "movie",
            continentId: 0xFFFFFFFF,
            hasEnteredWorld: false,
            maxCharacterCount: 0);

        Assert.Equal(WoWScreenState.LoginScreen, screenState);
    }
}
