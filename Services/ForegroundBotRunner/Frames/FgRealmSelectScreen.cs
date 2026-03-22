using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;

namespace ForegroundBotRunner.Frames;

/// <summary>
/// IRealmSelectScreen implementation for the Foreground (DLL-injected) bot.
///
/// On private servers with a single realm, the client auto-selects the realm
/// after login. The "Retrieving realm list" and "Retrieving character list"
/// GlueDialogs are part of this normal flow and should NOT be dismissed.
///
/// Detection: After a grace period at charselect, if MaxCharacterCount >= 0,
/// the realm was selected. The grace period lets SMSG_CHAR_ENUM arrive naturally.
/// </summary>
public class FgRealmSelectScreen : IRealmSelectScreen
{
    private static readonly Realm AutoSelectedRealm = new() { RealmName = "FG-AutoSelected", RealmId = 1 };

    private readonly Func<WoWScreenState> _getScreenState;
    private readonly Func<int> _getMaxCharacterCount;
    private readonly Action<string> _luaCall;
    private Realm? _selectedRealm;
    private DateTime? _charSelectFirstSeen;
    private bool _realmConfirmed;

    /// <summary>
    /// Grace period to wait for SMSG_CHAR_ENUM to arrive before assuming
    /// the realm is selected with 0 characters. During this time, do NOT
    /// dismiss GlueDialogs — they are part of the normal flow.
    /// </summary>
    private static readonly TimeSpan RealmGracePeriod = TimeSpan.FromSeconds(8);

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
        }
    }

    public Realm? CurrentRealm
    {
        get
        {
            if (_selectedRealm != null) return _selectedRealm;
            if (_realmConfirmed) return AutoSelectedRealm;
            if (Statics.ObjectManager.PauseNativeCallsDuringWorldEntry) return AutoSelectedRealm;
            if (_getMaxCharacterCount() > 0) return AutoSelectedRealm;
            return null;
        }
    }

    public List<Realm> GetRealmList() => [new Realm { RealmName = "Default", RealmId = 1 }];

    public void SelectRealm(Realm realm)
    {
        if (_getMaxCharacterCount() > 0)
        {
            _selectedRealm = realm;
            return;
        }

        // During grace period, do nothing — let the normal realm/char enum flow complete.
        // The "Retrieving realm list" dialog will disappear on its own.
    }

    public void SelectRealmType(RealmType realmType) { }

    public void CancelRealmSelection()
    {
        _selectedRealm = null;
    }
}
