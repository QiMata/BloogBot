using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Models;
<<<<<<< HEAD
=======
using Serilog;
>>>>>>> cpp_physics_system
using System;
using System.Collections.Generic;

namespace ForegroundBotRunner.Frames;

/// <summary>
/// IRealmSelectScreen implementation for the Foreground (DLL-injected) bot.
///
<<<<<<< HEAD
/// In WoW 1.12.1 (MaNGOS), after DefaultServerLogin() the client authenticates and
/// either auto-selects a saved realm or shows a realm list popup. Both cases report
/// loginState="charselect" in memory.
///
/// Detection strategy:
///   - If MaxCharacterCount > 0: realm was already selected (auto or manual), character list loaded.
///     Return a non-null CurrentRealm so BotRunnerService skips realm selection.
///   - If MaxCharacterCount == 0 and we're at charselect: realm list popup is likely showing.
///     IsOpen returns true, and SelectRealm() marks the realm as selected.
///     In 1.12.1, ChangeRealm() is NOT a valid Glue-screen Lua global — the client
///     auto-selects the last-used realm after login. We just wait for MaxCharacterCount > 0.
=======
/// On private servers with a single realm, the client auto-selects the realm
/// after login. The "Retrieving realm list" and "Retrieving character list"
/// GlueDialogs are part of this normal flow and should NOT be dismissed.
///
/// Detection: After a grace period at charselect, if MaxCharacterCount >= 0,
/// the realm was selected. The grace period lets SMSG_CHAR_ENUM arrive naturally.
>>>>>>> cpp_physics_system
/// </summary>
public class FgRealmSelectScreen : IRealmSelectScreen
{
    private static readonly Realm AutoSelectedRealm = new() { RealmName = "FG-AutoSelected", RealmId = 1 };

    private readonly Func<WoWScreenState> _getScreenState;
    private readonly Func<int> _getMaxCharacterCount;
    private readonly Action<string> _luaCall;
    private Realm? _selectedRealm;
<<<<<<< HEAD
=======
    private DateTime? _charSelectFirstSeen;
    private bool _realmConfirmed;

    /// <summary>
    /// Grace period to wait for SMSG_CHAR_ENUM to arrive before assuming
    /// the realm is selected with 0 characters. During this time, do NOT
    /// dismiss GlueDialogs — they are part of the normal flow.
    /// </summary>
    private static readonly TimeSpan RealmGracePeriod = TimeSpan.FromSeconds(8);
>>>>>>> cpp_physics_system

    public FgRealmSelectScreen(
        Func<WoWScreenState> getScreenState,
        Func<int> getMaxCharacterCount,
        Action<string> luaCall)
    {
        _getScreenState = getScreenState;
        _getMaxCharacterCount = getMaxCharacterCount;
        _luaCall = luaCall;
    }

    public bool IsOpen
    {
        get
        {
<<<<<<< HEAD
            if (_selectedRealm != null) return false;

            // At charselect with no character list loaded → realm selection needed
            return _getScreenState() == WoWScreenState.CharacterSelect
                && _getMaxCharacterCount() == 0;
=======
            if (_selectedRealm != null || _realmConfirmed) return false;
            if (Statics.ObjectManager.PauseNativeCallsDuringWorldEntry) return false;

            var screenState = _getScreenState();
            if (screenState == WoWScreenState.Connecting) return false;
            if (screenState != WoWScreenState.CharacterSelect) return false;

            var maxChar = _getMaxCharacterCount();
            if (maxChar > 0)
            {
                _realmConfirmed = true;
                return false;
            }

            // Wait for the grace period — SMSG_CHAR_ENUM may still be in transit.
            // Do NOT dismiss dialogs or take action during this time.
            _charSelectFirstSeen ??= DateTime.UtcNow;
            if (DateTime.UtcNow - _charSelectFirstSeen.Value > RealmGracePeriod)
            {
                Log.Information("[FG-REALM] Grace period expired ({Period}s). Char enum received 0 characters — realm is selected.", RealmGracePeriod.TotalSeconds);
                _realmConfirmed = true;
                return false;
            }

            // During grace period, report IsOpen but SelectRealm will be a no-op
            return true;
>>>>>>> cpp_physics_system
        }
    }

    public Realm? CurrentRealm
    {
        get
        {
            if (_selectedRealm != null) return _selectedRealm;
<<<<<<< HEAD

            // If character list has loaded (MaxCharacterCount > 0), realm was already selected
            if (_getMaxCharacterCount() > 0)
                return AutoSelectedRealm;

=======
            if (_realmConfirmed) return AutoSelectedRealm;
            if (Statics.ObjectManager.PauseNativeCallsDuringWorldEntry) return AutoSelectedRealm;
            if (_getMaxCharacterCount() > 0) return AutoSelectedRealm;
>>>>>>> cpp_physics_system
            return null;
        }
    }

    public List<Realm> GetRealmList() => [new Realm { RealmName = "Default", RealmId = 1 }];

    public void SelectRealm(Realm realm)
    {
<<<<<<< HEAD
        // In WoW 1.12.1, the client auto-selects the last-used realm after login.
        // ChangeRealm() is NOT a valid Glue-screen Lua global in 1.12.1.
        // Just mark the realm as selected — BotRunnerService proceeds when
        // MaxCharacterCount > 0 (CurrentRealm becomes non-null).
        _selectedRealm = realm;
=======
        if (_getMaxCharacterCount() > 0)
        {
            _selectedRealm = realm;
            return;
        }

        // During grace period, do nothing — let the normal realm/char enum flow complete.
        // The "Retrieving realm list" dialog will disappear on its own.
>>>>>>> cpp_physics_system
    }

    public void SelectRealmType(RealmType realmType) { }

    public void CancelRealmSelection()
    {
<<<<<<< HEAD
        try
        {
            _luaCall("if RealmListCancelButton ~= nil then if RealmListCancelButton:IsVisible() then RealmListCancelButton:Click(); end end");
        }
        catch { }

=======
>>>>>>> cpp_physics_system
        _selectedRealm = null;
    }
}
