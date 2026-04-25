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
    public void CreateCharacter_WhenSelectingMage_ClicksClassButtonByClassId()
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

            InvokeCreate(screen, race: Race.Troll, gender: Gender.Male, @class: Class.Mage); // step 0
            InvokeCreate(screen, race: Race.Troll, gender: Gender.Male, @class: Class.Mage); // step 1

            screenState = WoWScreenState.CharacterCreate;
            InvokeCreate(screen, race: Race.Troll, gender: Gender.Male, @class: Class.Mage); // step 2
            InvokeCreate(screen, race: Race.Troll, gender: Gender.Male, @class: Class.Mage); // step 3

            var classSelection = Assert.Single(
                luaCalls.Where(lua => lua.Contains("wantedClassId=8", StringComparison.Ordinal)));
            Assert.Contains("wantedClassFile=\"MAGE\"", classSelection);
            Assert.Contains("fallbackClassSlot=6", classSelection);
            Assert.Contains("GetClassesForRace(selectedRace,i)", classSelection);
            Assert.Contains("SetSelectedClass(i)", classSelection);
            Assert.Contains("getglobal(\"CharacterCreateClassButton\"..i)", classSelection);
            Assert.Contains("button:GetID()", classSelection);
            Assert.Contains("wantedClassName=\"Mage\"", classSelection);
        }
        finally
        {
            FgCharacterSelectScreen.CreateStepThrottle = originalThrottle;
            FgCharacterSelectScreen.CharacterCreateTransitionTimeout = originalTransitionTimeout;
        }
    }

    [Fact]
    public void CharacterSelects_WhenConfiguredClassEnvIsSet_ReportsConfiguredClass()
    {
        var originalClass = Environment.GetEnvironmentVariable("WWOW_CHARACTER_CLASS");
        var originalRace = Environment.GetEnvironmentVariable("WWOW_CHARACTER_RACE");
        var originalGender = Environment.GetEnvironmentVariable("WWOW_CHARACTER_GENDER");

        try
        {
            Environment.SetEnvironmentVariable("WWOW_CHARACTER_CLASS", "Mage");
            Environment.SetEnvironmentVariable("WWOW_CHARACTER_RACE", "Troll");
            Environment.SetEnvironmentVariable("WWOW_CHARACTER_GENDER", "Male");

            var screen = new FgCharacterSelectScreen(
                () => WoWScreenState.Unknown,
                () => 1,
                _ => { });

            Assert.True(screen.HasReceivedCharacterList);
            var character = Assert.Single(screen.CharacterSelects);
            Assert.Equal(Class.Mage, character.Class);
            Assert.Equal(Race.Troll, character.Race);
            Assert.Equal(Gender.Male, character.Gender);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WWOW_CHARACTER_CLASS", originalClass);
            Environment.SetEnvironmentVariable("WWOW_CHARACTER_RACE", originalRace);
            Environment.SetEnvironmentVariable("WWOW_CHARACTER_GENDER", originalGender);
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

    [Fact]
    public void IsCharacterCreationPending_WhenNameUnavailableDialogVisible_ClearsPendingForRetry()
    {
        var originalThrottle = FgCharacterSelectScreen.CreateStepThrottle;
        var originalTransitionTimeout = FgCharacterSelectScreen.CharacterCreateTransitionTimeout;

        try
        {
            FgCharacterSelectScreen.CreateStepThrottle = TimeSpan.Zero;
            FgCharacterSelectScreen.CharacterCreateTransitionTimeout = TimeSpan.FromSeconds(5);

            var screenState = WoWScreenState.CharacterSelect;
            string dialogText = string.Empty;
            var screen = new FgCharacterSelectScreen(
                () => screenState,
                () => 0,
                _ => { },
                getGlueDialogText: () => dialogText);

            InvokeCreate(screen); // step 0
            InvokeCreate(screen); // step 1

            screenState = WoWScreenState.CharacterCreate;
            InvokeCreate(screen); // step 2
            InvokeCreate(screen); // step 3
            InvokeCreate(screen, name: "Takenname"); // step 4

            Assert.True(screen.IsCharacterCreationPending);

            dialogText = "That name is unavailable.";

            Assert.False(screen.IsCharacterCreationPending);
            Assert.Equal(1, screen.CharacterCreateAttempts);
        }
        finally
        {
            FgCharacterSelectScreen.CreateStepThrottle = originalThrottle;
            FgCharacterSelectScreen.CharacterCreateTransitionTimeout = originalTransitionTimeout;
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
