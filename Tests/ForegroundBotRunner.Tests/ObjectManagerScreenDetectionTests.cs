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

    [Fact]
    public void GetDiagnosticPlayerLabel_DuringTransition_DoesNotTouchPlayerGetter()
    {
        bool invoked = false;

        string label = ObjectManager.GetDiagnosticPlayerLabel(
            () =>
            {
                invoked = true;
                return "Mailanaraooj";
            },
            isInTransition: true);

        Assert.Equal("(transition)", label);
        Assert.False(invoked);
    }

    [Fact]
    public void GetDiagnosticPlayerLabel_WhenGetterThrows_ReturnsUnavailable()
    {
        string label = ObjectManager.GetDiagnosticPlayerLabel(
            () => throw new InvalidOperationException("stale player"),
            isInTransition: false);

        Assert.Equal("(unavailable)", label);
    }
}
