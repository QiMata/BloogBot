using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;

namespace ForegroundBotRunner.Frames;

/// <summary>
/// ICharacterSelectScreen implementation for the Foreground (DLL-injected) bot.
/// Wraps FG's memory-based character list detection via MaxCharacterCount.
/// </summary>
public class FgCharacterSelectScreen(
    Func<WoWScreenState> getScreenState,
    Func<int> getMaxCharacterCount,
    Action<string> luaCall,
    Action<string>? captureLuaErrors = null) : ICharacterSelectScreen
{
    private DateTime? _charSelectFirstSeen;
    private static readonly TimeSpan CharListGracePeriod = TimeSpan.FromSeconds(2);
    internal static TimeSpan CreateStepThrottle { get; set; } = TimeSpan.FromSeconds(1);
    internal static TimeSpan CharacterCreateTransitionTimeout { get; set; } = TimeSpan.FromSeconds(5);
    private int _characterCreateAttempts;

    /// <summary>
    /// Snapshot of MaxCharacterCount taken during HasReceivedCharacterList.
    /// Used by CharacterSelects to prevent TOCTOU race where MaxCharacterCount
    /// drops between the two reads, causing BotRunnerService to queue CreateCharacter.
    /// </summary>
    private int _lastSnapshotCharCount;

    public bool IsOpen
    {
        get
        {
            var state = getScreenState();
            return state == WoWScreenState.CharacterSelect || state == WoWScreenState.CharacterCreate;
        }
    }

    private bool _hasLoggedGracePeriod;

    public bool HasReceivedCharacterList
    {
        get
        {
            if (Statics.ObjectManager.PauseNativeCallsDuringWorldEntry)
                return true;

            var charCount = getMaxCharacterCount();
            _lastSnapshotCharCount = charCount;

            if (!IsOpen)
                return charCount > 0;

            _charSelectFirstSeen ??= DateTime.Now;
            if (DateTime.Now - _charSelectFirstSeen.Value < CharListGracePeriod)
                return false;

            // After grace period, return true even with 0 characters.
            if (!_hasLoggedGracePeriod)
            {
                Log.Information("[FG-CHARSEL] Grace period expired. charCount={Count} - reporting HasReceivedCharacterList=true", charCount);
                _hasLoggedGracePeriod = true;
            }
            return true;
        }
        set { /* BotRunnerService may set this; FG detects from memory, so ignore */ }
    }

    public bool HasRequestedCharacterList
    {
        get => true; // WoW.exe auto-requests character list
        set { }
    }

    public bool IsCharacterCreationPending { get; private set; }

    public DateTime LastCharacterListRequestUtc { get; private set; }

    public int CharacterCreateAttempts => _characterCreateAttempts;

    public List<CharacterSelect> CharacterSelects
    {
        get
        {
            // Use the snapshot from HasReceivedCharacterList to prevent TOCTOU race.
            // If HasReceivedCharacterList returned true (charCount > 0), this must
            // also see > 0 - otherwise BotRunnerService sees Count==0 and queues
            // CreateCharacter, crashing WoW with a Lua call to a nil function.
            if (_lastSnapshotCharCount <= 0) return [];

            // FG doesn't have detailed character data from memory - return a minimal entry
            // so BotRunnerService sees at least one character and proceeds to EnterWorld.
            // Populate race/gender from env vars so the mismatch check doesn't block entry.
            var race = Race.None;
            var gender = Gender.Male;
            var raceEnv = Environment.GetEnvironmentVariable("WWOW_CHARACTER_RACE");
            var genderEnv = Environment.GetEnvironmentVariable("WWOW_CHARACTER_GENDER");
            if (!string.IsNullOrEmpty(raceEnv))
                Enum.TryParse(raceEnv, ignoreCase: true, out race);
            if (!string.IsNullOrEmpty(genderEnv))
                Enum.TryParse(genderEnv, ignoreCase: true, out gender);
            return [new CharacterSelect { Guid = 1, Name = "FG-Character", Level = 1, Race = race, Gender = gender }];
        }
    }

    public void RefreshCharacterListFromServer()
    {
        // WoW.exe handles this automatically.
        LastCharacterListRequestUtc = DateTime.UtcNow;
    }

    public void MarkCharacterListLoaded()
    {
        _charSelectFirstSeen = null;
        _hasLoggedGracePeriod = false;
        IsCharacterCreationPending = false;
        ResetCreateFlow();
        if (_lastSnapshotCharCount > 0)
            _characterCreateAttempts = 0;
    }

    public void ResetCharacterListRequest()
    {
        _charSelectFirstSeen = null;
        _hasLoggedGracePeriod = false;
        IsCharacterCreationPending = false;
        ResetCreateFlow();
    }

    public bool ShouldRetryCharacterListRequest(TimeSpan retryAfter, DateTime utcNow)
    {
        // WoW.exe owns the foreground character-list flow.
        return false;
    }

    /// <summary>Tracks the current step in the multi-phase character creation flow.</summary>
    private int _createCharStep;
    private DateTime _createStepEnteredAt = DateTime.MinValue;
    private DateTime _lastCreateStepTime;
    private string? _pendingCharName;
    private Race _pendingRace;
    private Class _pendingClass;
    private Gender _pendingGender;

    public void CreateCharacter(string name, Race race, Gender gender, Class @class,
        byte skinColor, byte face, byte hairStyle, byte hairColor, byte facialHair, byte outfitId)
    {
        // Guard: Only execute if we're on the character select or creation screen.
        var state = getScreenState();
        if (state != WoWScreenState.CharacterSelect && state != WoWScreenState.CharacterCreate)
            return;

        // Rate-limit steps to allow UI transitions
        if ((DateTime.UtcNow - _lastCreateStepTime) < CreateStepThrottle)
            return;
        _lastCreateStepTime = DateTime.UtcNow;

        // Store pending creation params
        _pendingCharName = name;
        _pendingRace = race;
        _pendingClass = @class;
        _pendingGender = gender;

        // WoW 1.12.1 character creation from the charselect screen is multi-step:
        // Step 0: Dismiss any GlueDialog, then click "Create New Character" button
        // Step 1: Set race via CharacterCreate.SetRace() (Lua index, 1-based)
        // Step 2: Set class via CharacterCreate.SetClass() (Lua index, 1-based)
        // Step 3: Set gender + call CreateCharacter("name")
        // Step 4+: Reset and wait for SMSG_CHAR_CREATE response
        switch (_createCharStep)
        {
            case 0:
                // Step 0: Dismiss any blocking GlueDialog only
                Log.Information("[FG-CHARSEL] Step 0: Dismiss any blocking dialogs (state={State})", state);
                ExecuteLua("if GlueDialog and GlueDialog:IsVisible() then " +
                    "if GlueDialogButton1 and GlueDialogButton1:IsVisible() then GlueDialogButton1:Click() end " +
                    "if GlueDialogButton2 and GlueDialogButton2:IsVisible() then GlueDialogButton2:Click() end " +
                    "end",
                    "charselect.create.step0.dismiss-dialogs");
                AdvanceCreateStep(1);
                break;

            case 1:
                // Step 1: Click "Create New Character" button (only if still on char select)
                if (state == WoWScreenState.CharacterSelect)
                {
                    Log.Information("[FG-CHARSEL] Step 1: Clicking 'Create New Character' button");
                    ExecuteLua("if CharacterSelect_CreateNewCharacter then CharacterSelect_CreateNewCharacter() " +
                        "elseif CharSelectCreateCharacterButton then CharSelectCreateCharacterButton:Click() end",
                        "charselect.create.step1.open-create-screen");
                    AdvanceCreateStep(2);
                }
                else if (state == WoWScreenState.CharacterCreate)
                {
                    Log.Information("[FG-CHARSEL] Step 1: Already on CharacterCreate screen, skipping button click");
                    AdvanceCreateStep(2);
                }
                break;

            case 2:
            {
                if (state != WoWScreenState.CharacterCreate)
                {
                    if (HasCurrentStepTimedOut(CharacterCreateTransitionTimeout))
                    {
                        Log.Warning(
                            "[FG-CHARSEL] Step 2 timed out waiting for CharacterCreate (state={State}) - retrying step 1",
                            state);
                        AdvanceCreateStep(1);
                        break;
                    }

                    Log.Warning("[FG-CHARSEL] Step 2: Not on CharacterCreate screen (state={State}), waiting", state);
                    break;
                }
                var raceIndex = GetCharCreateRaceIndex(race);
                Log.Information("[FG-CHARSEL] Step 2: Set race={Race} (index {Index})", race, raceIndex);
                ExecuteLua($"if CharacterCreateRaceButton{raceIndex} then CharacterCreateRaceButton{raceIndex}:Click() end",
                    "charselect.create.step2.select-race");
                AdvanceCreateStep(3);
                break;
            }

            case 3:
            {
                if (state != WoWScreenState.CharacterCreate)
                {
                    if (HasCurrentStepTimedOut(CharacterCreateTransitionTimeout))
                    {
                        Log.Warning(
                            "[FG-CHARSEL] Step 3 timed out waiting for CharacterCreate (state={State}) - retrying step 1",
                            state);
                        AdvanceCreateStep(1);
                        break;
                    }

                    Log.Warning("[FG-CHARSEL] Step 3: Not on CharacterCreate screen (state={State}), waiting", state);
                    break;
                }
                var classIndex = GetCharCreateClassIndex(@class);
                Log.Information("[FG-CHARSEL] Step 3: Set class={Class} (index {Index})", @class, classIndex);
                ExecuteLua($"if CharacterCreateClassButton{classIndex} then CharacterCreateClassButton{classIndex}:Click() end",
                    "charselect.create.step3.select-class");
                AdvanceCreateStep(4);
                break;
            }

            case 4:
                // Step 4: Set gender + name + create
                // Guard: only proceed if we're actually on the CharacterCreate screen
                if (state != WoWScreenState.CharacterCreate)
                {
                    Log.Warning("[FG-CHARSEL] Step 4: Not on CharacterCreate screen (state={State}), retrying step 1", state);
                    AdvanceCreateStep(1);
                    break;
                }
                Log.Information("[FG-CHARSEL] Step 4: Set gender={Gender}, create character '{Name}'", gender, name);
                if (gender == Gender.Female)
                    ExecuteLua("if CharacterCreateGenderButtonFemale then CharacterCreateGenderButtonFemale:Click() end",
                        "charselect.create.step4.select-gender-female");
                else
                    ExecuteLua("if CharacterCreateGenderButtonMale then CharacterCreateGenderButtonMale:Click() end",
                        "charselect.create.step4.select-gender-male");
                ExecuteLua($"if CharacterCreateNameEdit then CharacterCreateNameEdit:SetText(\"{name}\") end",
                    "charselect.create.step4.set-name");
                ExecuteLua($"if CreateCharacter then CreateCharacter(\"{name}\") end",
                    "charselect.create.step4.submit-create");
                IsCharacterCreationPending = true;
                _characterCreateAttempts++;
                AdvanceCreateStep(5);
                break;

            default:
                // Wait for character list to refresh - reset step counter for next attempt
                Log.Information("[FG-CHARSEL] Character creation submitted, waiting for response...");
                ResetCreateFlow();
                _charSelectFirstSeen = null;
                _hasLoggedGracePeriod = false;
                break;
        }
    }

    private void AdvanceCreateStep(int nextStep)
    {
        _createCharStep = nextStep;
        _createStepEnteredAt = DateTime.UtcNow;
    }

    private bool HasCurrentStepTimedOut(TimeSpan timeout)
    {
        if (_createStepEnteredAt == DateTime.MinValue)
            return false;

        return DateTime.UtcNow - _createStepEnteredAt >= timeout;
    }

    private void ResetCreateFlow()
    {
        _createCharStep = 0;
        _createStepEnteredAt = DateTime.MinValue;
    }

    /// <summary>Maps Race enum to WoW character creation button index (1-based).</summary>
    private static int GetCharCreateRaceIndex(Race race) => race switch
    {
        Race.Human => 1,
        Race.Dwarf => 2,
        Race.NightElf => 3,
        Race.Gnome => 4,
        Race.Orc => 5,
        Race.Undead => 6,
        Race.Tauren => 7,
        Race.Troll => 8,
        _ => 5, // Default to Orc
    };

    /// <summary>Maps Class enum to WoW character creation button index (1-based).</summary>
    private static int GetCharCreateClassIndex(Class @class) => @class switch
    {
        Class.Warrior => 1,
        Class.Paladin => 2,
        Class.Hunter => 3,
        Class.Rogue => 4,
        Class.Priest => 5,
        Class.Shaman => 7,
        Class.Mage => 8,
        Class.Warlock => 9,
        Class.Druid => 11,
        _ => 1, // Default to Warrior
    };

    public void DeleteCharacter(ulong characterGuid)
    {
        // Deferred - not critical for unification
    }

    private void ExecuteLua(string lua, string context)
    {
        try
        {
            luaCall(lua);
        }
        finally
        {
            CaptureLuaErrors(context);
        }
    }

    private void CaptureLuaErrors(string context)
    {
        if (captureLuaErrors == null)
            return;

        try
        {
            captureLuaErrors(context);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[FG-CHARSEL] Lua error capture callback failed for context {Context}", context);
        }
    }
}
