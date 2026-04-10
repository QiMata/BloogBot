using System;
using System.Collections.Generic;
using System.Linq;
using ForegroundBotRunner.Frames;
using GameData.Core.Enums;

namespace ForegroundBotRunner.Tests;

public sealed class FgCharacterSelectScreenTests
{
    [Fact]
    public void CreateCharacter_WhenStepTwoNeverReachesCharacterCreate_RetriesStepOne()
    {
        var originalThrottle = FgCharacterSelectScreen.CreateStepThrottle;
        var originalTransitionTimeout = FgCharacterSelectScreen.CharacterCreateTransitionTimeout;

        try
        {
            FgCharacterSelectScreen.CreateStepThrottle = TimeSpan.Zero;
            FgCharacterSelectScreen.CharacterCreateTransitionTimeout = TimeSpan.Zero;

            var screenState = WoWScreenState.CharacterSelect;
            var luaCalls = new List<string>();
            var screen = new FgCharacterSelectScreen(
                () => screenState,
                () => 0,
                lua => luaCalls.Add(lua));

            InvokeCreate(screen); // step 0
            InvokeCreate(screen); // step 1: click create-new-character
            InvokeCreate(screen); // step 2: timeout -> back to step 1
            InvokeCreate(screen); // step 1 again: click create-new-character

            var createButtonClicks = luaCalls.Count(lua => lua.Contains("CharacterSelect_CreateNewCharacter", StringComparison.Ordinal));
            Assert.True(createButtonClicks >= 2, $"Expected at least two create-button attempts, observed {createButtonClicks}.");
        }
        finally
        {
            FgCharacterSelectScreen.CreateStepThrottle = originalThrottle;
            FgCharacterSelectScreen.CharacterCreateTransitionTimeout = originalTransitionTimeout;
        }
    }

    [Fact]
    public void CreateCharacter_WhenCharacterCreateScreenReached_SubmitsCharacterCreation()
    {
        var originalThrottle = FgCharacterSelectScreen.CreateStepThrottle;
        var originalTransitionTimeout = FgCharacterSelectScreen.CharacterCreateTransitionTimeout;

        try
        {
            FgCharacterSelectScreen.CreateStepThrottle = TimeSpan.Zero;
            FgCharacterSelectScreen.CharacterCreateTransitionTimeout = TimeSpan.FromSeconds(5);

            var screenState = WoWScreenState.CharacterSelect;
            var luaCalls = new List<string>();
            var screen = new FgCharacterSelectScreen(
                () => screenState,
                () => 0,
                lua => luaCalls.Add(lua));

            InvokeCreate(screen); // step 0
            InvokeCreate(screen); // step 1

            screenState = WoWScreenState.CharacterCreate;
            InvokeCreate(screen); // step 2
            InvokeCreate(screen); // step 3
            InvokeCreate(screen, name: "FgLeaderOne"); // step 4

            Assert.True(screen.IsCharacterCreationPending);
            Assert.Equal(1, screen.CharacterCreateAttempts);
            Assert.Contains(luaCalls, lua => lua.Contains("CreateCharacter(\"FgLeaderOne\")", StringComparison.Ordinal));
        }
        finally
        {
            FgCharacterSelectScreen.CreateStepThrottle = originalThrottle;
            FgCharacterSelectScreen.CharacterCreateTransitionTimeout = originalTransitionTimeout;
        }
    }

    [Fact]
    public void CreateCharacter_WhenLuaStepExecutes_InvokesLuaErrorCaptureCallback()
    {
        var originalThrottle = FgCharacterSelectScreen.CreateStepThrottle;
        try
        {
            FgCharacterSelectScreen.CreateStepThrottle = TimeSpan.Zero;

            var captureContexts = new List<string>();
            var screen = new FgCharacterSelectScreen(
                () => WoWScreenState.CharacterSelect,
                () => 0,
                _ => { },
                context => captureContexts.Add(context));

            InvokeCreate(screen); // step 0

            Assert.Contains("charselect.create.step0.dismiss-dialogs", captureContexts);
        }
        finally
        {
            FgCharacterSelectScreen.CreateStepThrottle = originalThrottle;
        }
    }

    private static void InvokeCreate(
        FgCharacterSelectScreen screen,
        string name = "FGBot",
        Race race = Race.Orc,
        Gender gender = Gender.Female,
        Class @class = Class.Warrior)
    {
        screen.CreateCharacter(name, race, gender, @class, 0, 0, 0, 0, 0, 0);
    }
}
