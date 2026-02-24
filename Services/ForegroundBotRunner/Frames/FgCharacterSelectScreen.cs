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

    public bool IsOpen => getScreenState() == WoWScreenState.CharacterSelect;

    public bool HasReceivedCharacterList
    {
        get
        {
            // When InWorld, IsOpen is false (we're past the charselect screen).
            // Return true so BotRunnerService doesn't think we're waiting for a character list.
            if (!IsOpen)
                return getMaxCharacterCount() > 0;

            // Grace period — WoW.exe needs time to populate character list in memory
            _charSelectFirstSeen ??= DateTime.Now;
            if (DateTime.Now - _charSelectFirstSeen.Value < CharListGracePeriod)
                return false;

            return getMaxCharacterCount() > 0;
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
            var count = getMaxCharacterCount();
            if (count <= 0) return [];

            // FG doesn't have detailed character data from memory — return a minimal entry
            // so BotRunnerService sees at least one character and proceeds to EnterWorld.
            return [new CharacterSelect { Guid = 1, Name = "FG-Character", Level = 1 }];
        }
    }

    public void RefreshCharacterListFromServer()
    {
        // WoW.exe handles this automatically
    }

    public void CreateCharacter(string name, Race race, Gender gender, Class @class,
        byte skinColor, byte face, byte hairStyle, byte hairColor, byte facialHair, byte outfitId)
    {
        // Lua-based character creation (deferred — not critical path)
        luaCall($"CreateCharacter(\"{name}\")");
    }

    public void DeleteCharacter(ulong characterGuid)
    {
        // Deferred — not critical for unification
    }
}
