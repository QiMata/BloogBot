using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Models;
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
    Action<string> luaCall) : ICharacterSelectScreen
{
    private DateTime? _charSelectFirstSeen;
    private static readonly TimeSpan CharListGracePeriod = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Snapshot of MaxCharacterCount taken during HasReceivedCharacterList.
    /// Used by CharacterSelects to prevent TOCTOU race where MaxCharacterCount
    /// drops between the two reads, causing BotRunnerService to queue CreateCharacter.
    /// </summary>
    private int _lastSnapshotCharCount;

    public bool IsOpen => getScreenState() == WoWScreenState.CharacterSelect;

    public bool HasReceivedCharacterList
    {
        get
        {
            // During world entry handshake, report true to prevent BotRunnerService
            // from triggering any UI interactions (Lua calls) during the critical phase.
            if (Statics.ObjectManager.PauseNativeCallsDuringWorldEntry)
                return true;

            // Snapshot MaxCharacterCount once per check to prevent TOCTOU race.
            var charCount = getMaxCharacterCount();
            _lastSnapshotCharCount = charCount;

            // When InWorld, IsOpen is false (we're past the charselect screen).
            // Return true so BotRunnerService doesn't think we're waiting for a character list.
            if (!IsOpen)
                return charCount > 0;

            // Grace period — WoW.exe needs time to populate character list in memory
            _charSelectFirstSeen ??= DateTime.Now;
            if (DateTime.Now - _charSelectFirstSeen.Value < CharListGracePeriod)
                return false;

            return charCount > 0;
        }
        set { /* BotRunnerService may set this; FG detects from memory, so ignore */ }
    }

    public bool HasRequestedCharacterList
    {
        get => true; // WoW.exe auto-requests character list
        set { }
    }

    public List<CharacterSelect> CharacterSelects
    {
        get
        {
            // Use the snapshot from HasReceivedCharacterList to prevent TOCTOU race.
            // If HasReceivedCharacterList returned true (charCount > 0), this must
            // also see > 0 — otherwise BotRunnerService sees Count==0 and queues
            // CreateCharacter, crashing WoW with a Lua call to a nil function.
            if (_lastSnapshotCharCount <= 0) return [];

            // FG doesn't have detailed character data from memory — return a minimal entry
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
        // WoW.exe handles this automatically
    }

    public void CreateCharacter(string name, Race race, Gender gender, Class @class,
        byte skinColor, byte face, byte hairStyle, byte hairColor, byte facialHair, byte outfitId)
    {
        // Guard: Only execute CreateCharacter Lua if we're actually on the character select screen.
        // During zone transitions, stale LoginState memory can make BotRunnerService think
        // we're at charselect when we're in-world. Calling CreateCharacter Lua in-world
        // causes a nil function error that can destabilize WoW → ERROR #132.
        if (getScreenState() != WoWScreenState.CharacterSelect)
            return;

        luaCall($"CreateCharacter(\"{name}\")");
    }

    public void DeleteCharacter(ulong characterGuid)
    {
        // Deferred — not critical for unification
    }
}
