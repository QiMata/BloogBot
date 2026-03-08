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
/// In WoW 1.12.1 (MaNGOS), after DefaultServerLogin() the client authenticates and
/// either auto-selects a saved realm or shows a realm list popup. Both cases report
/// loginState="charselect" in memory.
///
/// Detection strategy:
///   - If MaxCharacterCount > 0: realm was already selected (auto or manual), character list loaded.
///     Return a non-null CurrentRealm so BotRunnerService skips realm selection.
///   - If MaxCharacterCount == 0 and we're at charselect: realm list popup is likely showing.
///     IsOpen returns true. SelectRealm() clicks the first realm button and OK via Lua.
///     The behavior tree retries every tick (rate-limited to 2s) until MaxCharacterCount > 0.
/// </summary>
public class FgRealmSelectScreen : IRealmSelectScreen
{
    private static readonly Realm AutoSelectedRealm = new() { RealmName = "FG-AutoSelected", RealmId = 1 };

    private readonly Func<WoWScreenState> _getScreenState;
    private readonly Func<int> _getMaxCharacterCount;
    private readonly Action<string> _luaCall;
    private Realm? _selectedRealm;
    private DateTime? _lastRealmClickAttempt;
    private int _realmClickAttemptCount;

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
            if (_selectedRealm != null) return false;

            // During world entry handshake, do NOT report open — prevents Lua calls during handshake
            if (Statics.ObjectManager.PauseNativeCallsDuringWorldEntry) return false;

            var screenState = _getScreenState();

            // During Connecting state, the auth handshake is in progress.
            // No Lua calls should be issued — m_netState is not ready.
            if (screenState == WoWScreenState.Connecting) return false;

            // At charselect with no character list loaded → realm selection needed
            return screenState == WoWScreenState.CharacterSelect
                && _getMaxCharacterCount() == 0;
        }
    }

    public Realm? CurrentRealm
    {
        get
        {
            if (_selectedRealm != null) return _selectedRealm;

            // During world entry handshake, report as selected — prevents BotRunnerService
            // from triggering realm selection Lua calls during the critical handshake phase.
            // MaxCharacterCount can temporarily drop to 0 during world entry transition.
            if (Statics.ObjectManager.PauseNativeCallsDuringWorldEntry)
                return AutoSelectedRealm;

            // If character list has loaded (MaxCharacterCount > 0), realm was already selected
            if (_getMaxCharacterCount() > 0)
                return AutoSelectedRealm;

            return null;
        }
    }

    public List<Realm> GetRealmList() => [new Realm { RealmName = "Default", RealmId = 1 }];

    public void SelectRealm(Realm realm)
    {
        // If MaxCharacterCount > 0, the realm was already auto-selected by the client.
        if (_getMaxCharacterCount() > 0)
        {
            _selectedRealm = realm;
            return;
        }

        // Wait for screen transition animation to complete before issuing Lua calls.
        // Sending commands during animations causes ACCESS_VIOLATION crashes.
        if (Statics.ObjectManager.IsInScreenTransitionCooldown)
            return;

        // Rate-limit UI clicks to avoid spamming the client (called every ~100ms by behavior tree)
        if (_lastRealmClickAttempt.HasValue
            && (DateTime.UtcNow - _lastRealmClickAttempt.Value).TotalMilliseconds < 1500)
            return;
        _lastRealmClickAttempt = DateTime.UtcNow;

        _realmClickAttemptCount++;
        Log.Information("[FG-REALM] SelectRealm attempt #{Count}", _realmClickAttemptCount);

        // Alternate between different strategies each attempt since some require
        // multiple frames (ChangeRealm opens list, then next attempt clicks the button)
        try
        {
            if (_realmClickAttemptCount % 3 == 1)
            {
                // Strategy A: Open the realm list from the wizard (takes effect next frame)
                _luaCall("if ChangeRealm then ChangeRealm() end");
                Log.Information("[FG-REALM] Strategy A: ChangeRealm() called");
            }
            else if (_realmClickAttemptCount % 3 == 2)
            {
                // Strategy B: Click the first realm in the list and OK (after ChangeRealm opened it)
                _luaCall("if RealmListButton1 and RealmListButton1:IsVisible() then RealmListButton1:Click() end");
                _luaCall("if RealmListOkButton and RealmListOkButton:IsVisible() then RealmListOkButton:Click() end");
                Log.Information("[FG-REALM] Strategy B: RealmListButton1 + RealmListOkButton clicked");
            }
            else
            {
                // Strategy C: Try wizard accept/OK buttons directly
                _luaCall("if RealmWizard_OnOkay then RealmWizard_OnOkay() end");
                _luaCall("if RealmWizardOkayButton and RealmWizardOkayButton:IsVisible() then RealmWizardOkayButton:Click() end");
                Log.Information("[FG-REALM] Strategy C: RealmWizard_OnOkay + RealmWizardOkayButton");
            }
        }
        catch (Exception ex) { Log.Warning("[FG-REALM] Strategy failed: {Error}", ex.Message); }

        // Do NOT set _selectedRealm here — let CurrentRealm detect realm selection
        // via MaxCharacterCount > 0. This ensures the UI actually transitioned.
    }

    public void SelectRealmType(RealmType realmType) { }

    public void CancelRealmSelection()
    {
        try
        {
            _luaCall("if RealmListCancelButton ~= nil then if RealmListCancelButton:IsVisible() then RealmListCancelButton:Click(); end end");
        }
        catch { }

        _selectedRealm = null;
    }
}
