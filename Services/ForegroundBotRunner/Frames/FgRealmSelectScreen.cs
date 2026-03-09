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
            && (DateTime.UtcNow - _lastRealmClickAttempt.Value).TotalMilliseconds < 1000)
            return;
        _lastRealmClickAttempt = DateTime.UtcNow;

        _realmClickAttemptCount++;
        Log.Information("[FG-REALM] SelectRealm attempt #{Count}", _realmClickAttemptCount);

        // The RealmWizard is a multi-step dialog that appears on first login.
        // On private servers with one realm, dismissing the wizard should auto-select it.
        // Rotate through multiple approaches: cancel, hide, advance, and realm list clicks.
        try
        {
            int strategy = _realmClickAttemptCount % 8;
            switch (strategy)
            {
                case 1:
                    // Strategy A: Cancel the wizard — on single-realm servers, this auto-selects
                    _luaCall("if RealmWizard_OnCancel then RealmWizard_OnCancel() end");
                    Log.Information("[FG-REALM] Strategy A: RealmWizard_OnCancel()");
                    break;
                case 2:
                    // Strategy B: Hide the wizard frame directly
                    _luaCall("if RealmWizard and RealmWizard.Hide then RealmWizard:Hide() end");
                    _luaCall("if ChangeRealm then ChangeRealm() end");
                    Log.Information("[FG-REALM] Strategy B: Hide wizard + ChangeRealm");
                    break;
                case 3:
                    // Strategy C: Click Next to advance through wizard steps
                    _luaCall("if RealmWizardNextButton and RealmWizardNextButton:IsVisible() then RealmWizardNextButton:Click() end");
                    Log.Information("[FG-REALM] Strategy C: RealmWizardNextButton clicked");
                    break;
                case 4:
                    // Strategy D: Click wizard OK/accept (final wizard step)
                    _luaCall("if RealmWizardOkayButton and RealmWizardOkayButton:IsVisible() then RealmWizardOkayButton:Click() end");
                    _luaCall("if RealmWizard_OnOkay then RealmWizard_OnOkay() end");
                    Log.Information("[FG-REALM] Strategy D: RealmWizardOkayButton + OnOkay");
                    break;
                case 5:
                    // Strategy E: Open realm list directly
                    _luaCall("if ChangeRealm then ChangeRealm() end");
                    Log.Information("[FG-REALM] Strategy E: ChangeRealm()");
                    break;
                case 6:
                    // Strategy F: Click first realm in list + OK
                    _luaCall("if RealmListButton1 and RealmListButton1:IsVisible() then RealmListButton1:Click() end");
                    _luaCall("if RealmListOkButton and RealmListOkButton:IsVisible() then RealmListOkButton:Click() end");
                    Log.Information("[FG-REALM] Strategy F: RealmListButton1 + OK");
                    break;
                case 7:
                    // Strategy G: Dismiss any GlueDialog that may be blocking
                    _luaCall("if GlueDialogButton1 and GlueDialogButton1:IsVisible() then GlueDialogButton1:Click() end");
                    _luaCall("if GlueDialogButton2 and GlueDialogButton2:IsVisible() then GlueDialogButton2:Click() end");
                    Log.Information("[FG-REALM] Strategy G: GlueDialog dismiss");
                    break;
                default:
                    // Strategy H: Brute-force — advance wizard + cancel + realm list all at once
                    _luaCall("if RealmWizardNextButton and RealmWizardNextButton:IsVisible() then for i=1,5 do RealmWizardNextButton:Click() end end");
                    _luaCall("if RealmWizardOkayButton and RealmWizardOkayButton:IsVisible() then RealmWizardOkayButton:Click() end");
                    _luaCall("if RealmWizard_OnCancel then RealmWizard_OnCancel() end");
                    _luaCall("if RealmListButton1 and RealmListButton1:IsVisible() then RealmListButton1:Click() end");
                    _luaCall("if RealmListOkButton and RealmListOkButton:IsVisible() then RealmListOkButton:Click() end");
                    Log.Information("[FG-REALM] Strategy H: Full brute-force");
                    break;
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
