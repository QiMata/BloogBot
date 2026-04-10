using System;
using System.Collections.Generic;
using ForegroundBotRunner.Frames;
using GameData.Core.Enums;
using GameData.Core.Models;

namespace ForegroundBotRunner.Tests;

public sealed class FgRealmSelectScreenTests
{
    [Fact]
    public void IsOpen_WhenRealmWizardActive_ReturnsTrue()
    {
        var screen = new FgRealmSelectScreen(
            () => WoWScreenState.CharacterSelect,
            () => 0,
            _ => { },
            _ => [CreateWizardSnapshot(isActive: true)]);

        Assert.True(screen.IsOpen);
    }

    [Fact]
    public void CurrentRealm_WhenRealmWizardActive_RemainsNull()
    {
        var screen = new FgRealmSelectScreen(
            () => WoWScreenState.CharacterSelect,
            () => 0,
            _ => { },
            _ => [CreateWizardSnapshot(isActive: true)]);

        Assert.Null(screen.CurrentRealm);
    }

    [Fact]
    public void SelectRealm_WhenEnglishIsNotChecked_ClicksEnglishControl()
    {
        var luaCalls = new List<string>();
        var screen = new FgRealmSelectScreen(
            () => WoWScreenState.CharacterSelect,
            () => 0,
            lua => luaCalls.Add(lua),
            _ => [CreateWizardSnapshot(
                isActive: true,
                isEnglishVisible: true,
                isEnglishChecked: false,
                isSuggestVisible: true,
                isSuggestEnabled: true)]);

        screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });

        Assert.NotEmpty(luaCalls);
        Assert.Contains(luaCalls, lua => lua.Contains("RealmWizardFrameEnglishCheckButton", StringComparison.Ordinal));
        Assert.All(luaCalls, AssertNoLuaSweep);
    }

    [Fact]
    public void SelectRealm_WhenSuggestRealmIsReady_ClicksSuggestButton()
    {
        var luaCalls = new List<string>();
        var screen = new FgRealmSelectScreen(
            () => WoWScreenState.CharacterSelect,
            () => 0,
            lua => luaCalls.Add(lua),
            _ => [CreateWizardSnapshot(
                isActive: true,
                isEnglishVisible: true,
                isEnglishChecked: true,
                isSuggestVisible: true,
                isSuggestEnabled: true)]);

        screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });

        Assert.NotEmpty(luaCalls);
        Assert.Contains(luaCalls, lua => lua.Contains("RealmWizardSuggestRealmButton", StringComparison.Ordinal));
        Assert.All(luaCalls, AssertNoLuaSweep);
    }

    [Fact]
    public void SelectRealm_WhenSuggestedRealmNeedsConfirmation_ClicksOkayOrAccept()
    {
        var luaCalls = new List<string>();
        var screen = new FgRealmSelectScreen(
            () => WoWScreenState.CharacterSelect,
            () => 0,
            lua => luaCalls.Add(lua),
            _ => [CreateWizardSnapshot(
                isActive: true,
                isEnglishVisible: true,
                isEnglishChecked: true,
                isSuggestVisible: false,
                isSuggestEnabled: false,
                isAcceptVisible: true,
                isAcceptEnabled: true)]);

        screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });

        Assert.NotEmpty(luaCalls);
        Assert.Contains(luaCalls, lua => lua.Contains("RealmWizardAcceptButton", StringComparison.Ordinal)
            || lua.Contains("RealmWizardOkayButton", StringComparison.Ordinal));
        Assert.All(luaCalls, AssertNoLuaSweep);
    }

    [Fact]
    public void SelectRealm_WhenActionRuns_InvokesLuaErrorCaptureCallback()
    {
        var captureContexts = new List<string>();
        var screen = new FgRealmSelectScreen(
            () => WoWScreenState.CharacterSelect,
            () => 0,
            _ => { },
            _ => [CreateWizardSnapshot(
                isActive: true,
                isEnglishVisible: true,
                isEnglishChecked: false,
                isSuggestVisible: true,
                isSuggestEnabled: true)],
            captureLuaErrors: context => captureContexts.Add(context));

        screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });

        Assert.Contains("realmwizard.select-english", captureContexts);
    }

    [Fact]
    public void SelectRealm_WhenLuaSnapshotUnavailableButLoginStateIsRealmWizard_UsesStateSequence()
    {
        var originalCooldown = FgRealmSelectScreen.RealmWizardActionCooldown;
        try
        {
            FgRealmSelectScreen.RealmWizardActionCooldown = TimeSpan.Zero;

            var luaCalls = new List<string>();
            var screen = new FgRealmSelectScreen(
                () => WoWScreenState.CharacterSelect,
                () => 0,
                lua => luaCalls.Add(lua),
                _ => [],
                () => "realmwizard");

            screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });
            screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });
            screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });

            Assert.True(luaCalls.Count >= 3, $"Expected at least 3 Lua calls, observed {luaCalls.Count}.");
            Assert.Contains(luaCalls, lua => lua.Contains("RealmWizardFrameEnglishCheckButton", StringComparison.Ordinal));
            Assert.Contains(luaCalls, lua => lua.Contains("RealmWizardSuggestRealmButton", StringComparison.Ordinal));
            Assert.Contains(luaCalls, lua => lua.Contains("RealmWizardOkayButton", StringComparison.Ordinal)
                || lua.Contains("RealmWizardAcceptButton", StringComparison.Ordinal));
            Assert.All(luaCalls, AssertNoLuaSweep);
        }
        finally
        {
            FgRealmSelectScreen.RealmWizardActionCooldown = originalCooldown;
        }
    }

    [Fact]
    public void SelectRealm_WhenSnapshotUnavailableAndCharSelectHasNoCharacters_UsesStateWindowSequence()
    {
        var originalCooldown = FgRealmSelectScreen.RealmWizardActionCooldown;
        var originalWindow = FgRealmSelectScreen.StateBasedRealmWizardWindow;
        try
        {
            FgRealmSelectScreen.RealmWizardActionCooldown = TimeSpan.Zero;
            FgRealmSelectScreen.StateBasedRealmWizardWindow = TimeSpan.FromSeconds(30);

            var luaCalls = new List<string>();
            var screen = new FgRealmSelectScreen(
                () => WoWScreenState.CharacterSelect,
                () => 0,
                lua => luaCalls.Add(lua),
                _ => [],
                () => null);

            screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });
            screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });
            screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });

            Assert.True(luaCalls.Count >= 3, $"Expected at least 3 Lua calls, observed {luaCalls.Count}.");
            Assert.Contains(luaCalls, lua => lua.Contains("RealmWizardFrameEnglishCheckButton", StringComparison.Ordinal));
            Assert.Contains(luaCalls, lua => lua.Contains("RealmWizardSuggestRealmButton", StringComparison.Ordinal));
            Assert.Contains(luaCalls, lua => lua.Contains("RealmWizardOkayButton", StringComparison.Ordinal)
                || lua.Contains("RealmWizardAcceptButton", StringComparison.Ordinal));
            Assert.All(luaCalls, AssertNoLuaSweep);
        }
        finally
        {
            FgRealmSelectScreen.RealmWizardActionCooldown = originalCooldown;
            FgRealmSelectScreen.StateBasedRealmWizardWindow = originalWindow;
        }
    }

    [Fact]
    public void SelectRealm_WhenRealmWizardTransitionsToCharSelect_StopsRealmWizardAutomation()
    {
        var originalCooldown = FgRealmSelectScreen.RealmWizardActionCooldown;
        try
        {
            FgRealmSelectScreen.RealmWizardActionCooldown = TimeSpan.Zero;

            var luaCalls = new List<string>();
            var activeSnapshot = CreateWizardSnapshot(isActive: true, currentGlueScreen: "realmwizard");
            var charSelectSnapshot = CreateWizardSnapshot(isActive: false, currentGlueScreen: "charselect");
            var snapshotReadCount = 0;
            var loginState = "realmwizard";

            var screen = new FgRealmSelectScreen(
                () => WoWScreenState.CharacterSelect,
                () => 0,
                lua => luaCalls.Add(lua),
                _ =>
                {
                    snapshotReadCount++;
                    return [snapshotReadCount == 1 ? activeSnapshot : charSelectSnapshot];
                },
                () => loginState);

            screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });
            var callsAfterWizardStep = luaCalls.Count;

            loginState = "charselect";
            screen.SelectRealm(new Realm { RealmId = 1, RealmName = "Default" });

            Assert.Equal(callsAfterWizardStep, luaCalls.Count);
            Assert.False(screen.IsOpen);
            Assert.NotNull(screen.CurrentRealm);
        }
        finally
        {
            FgRealmSelectScreen.RealmWizardActionCooldown = originalCooldown;
        }
    }

    private static string CreateWizardSnapshot(
        bool isActive,
        bool isEnglishVisible = false,
        bool isEnglishChecked = false,
        bool isSuggestVisible = false,
        bool isSuggestEnabled = false,
        bool isAcceptVisible = false,
        bool isAcceptEnabled = false,
        bool isOkayVisible = false,
        bool isOkayEnabled = false,
        string currentGlueScreen = "realmwizard")
    {
        return string.Join("|",
            ToFlag(isActive),
            ToFlag(isEnglishVisible),
            ToFlag(isEnglishChecked),
            ToFlag(isSuggestVisible),
            ToFlag(isSuggestEnabled),
            ToFlag(isAcceptVisible),
            ToFlag(isAcceptEnabled),
            ToFlag(isOkayVisible),
            ToFlag(isOkayEnabled),
            currentGlueScreen);
    }

    private static string ToFlag(bool enabled) => enabled ? "1" : "0";

    private static void AssertNoLuaSweep(string lua)
    {
        Assert.DoesNotContain("for _, frame in pairs", lua, StringComparison.Ordinal);
        Assert.DoesNotContain("pairs(_G)", lua, StringComparison.Ordinal);
        Assert.DoesNotContain("getglobals and getglobals()", lua, StringComparison.Ordinal);
    }
}
