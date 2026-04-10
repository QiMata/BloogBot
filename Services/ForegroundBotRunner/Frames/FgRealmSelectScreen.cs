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
/// On private servers with a single realm, the client can auto-select the realm
/// after login. The glue dialogs used during this flow are part of normal startup.
/// </summary>
public class FgRealmSelectScreen : IRealmSelectScreen
{
    private static readonly Realm AutoSelectedRealm = new() { RealmName = "FG-AutoSelected", RealmId = 1 };

    internal static TimeSpan RealmWizardActionCooldown { get; set; } = TimeSpan.FromSeconds(1);
    internal static TimeSpan StateBasedRealmWizardWindow { get; set; } = TimeSpan.FromSeconds(30);
    internal static TimeSpan RealmWizardControlDumpInterval { get; set; } = TimeSpan.FromSeconds(3);
    internal static int StateDetectedDirectApiThreshold { get; set; } = 6;

    private const string RealmWizardSnapshotLua =
        "local function wwow_get(name) " +
        "  if getglobal then return getglobal(name) end " +
        "  if _G then return _G[name] end " +
        "  return nil " +
        "end " +
        "local function wwow_visible(name) " +
        "  local f = wwow_get(name); " +
        "  return (f and f.IsVisible and f:IsVisible()) and 1 or 0 " +
        "end " +
        "local function wwow_enabled(name) " +
        "  local f = wwow_get(name); " +
        "  if not (f and f.IsVisible and f:IsVisible()) then return 0 end " +
        "  if f.IsEnabled then return f:IsEnabled() and 1 or 0 end " +
        "  return 1 " +
        "end " +
        "local function wwow_checked(name) " +
        "  local f = wwow_get(name); " +
        "  if not (f and f.IsVisible and f:IsVisible() and f.GetChecked) then return 0 end " +
        "  return f:GetChecked() and 1 or 0 " +
        "end " +
        "local current = CURRENT_GLUE_SCREEN or '' " +
        "local wizardVisible = ((current == 'realmwizard') or (RealmWizardFrame and RealmWizardFrame:IsVisible())) and 1 or 0 " +
        "local englishVisible = ((wwow_visible('RealmWizardFrameEnglishCheckButton') == 1) or (wwow_visible('RealmWizardFrameEnglishButton') == 1)) and 1 or 0 " +
        "local englishChecked = ((wwow_checked('RealmWizardFrameEnglishCheckButton') == 1) or (wwow_checked('RealmWizardFrameEnglishButton') == 1)) and 1 or 0 " +
        "local suggestVisible = ((wwow_visible('RealmWizardSuggestRealmButton') == 1) or (wwow_visible('RealmWizardFrameSuggestRealmButton') == 1) or (wwow_visible('RealmWizardFrameRealmButton') == 1)) and 1 or 0 " +
        "local suggestEnabled = ((wwow_enabled('RealmWizardSuggestRealmButton') == 1) or (wwow_enabled('RealmWizardFrameSuggestRealmButton') == 1) or (wwow_enabled('RealmWizardFrameRealmButton') == 1)) and 1 or 0 " +
        "local acceptVisible = ((wwow_visible('RealmWizardAcceptButton') == 1) or (wwow_visible('RealmWizardFrameAcceptButton') == 1)) and 1 or 0 " +
        "local acceptEnabled = ((wwow_enabled('RealmWizardAcceptButton') == 1) or (wwow_enabled('RealmWizardFrameAcceptButton') == 1)) and 1 or 0 " +
        "local okayVisible = ((wwow_visible('RealmWizardOkayButton') == 1) or (wwow_visible('RealmWizardFrameOkayButton') == 1)) and 1 or 0 " +
        "local okayEnabled = ((wwow_enabled('RealmWizardOkayButton') == 1) or (wwow_enabled('RealmWizardFrameOkayButton') == 1)) and 1 or 0 " +
        "{0} = table.concat({ tostring(wizardVisible), tostring(englishVisible), tostring(englishChecked), tostring(suggestVisible), tostring(suggestEnabled), tostring(acceptVisible), tostring(acceptEnabled), tostring(okayVisible), tostring(okayEnabled), current }, '|')";

    private const string RealmWizardControlDumpLua =
        "local function wwow_get(name) " +
        "  if getglobal then return getglobal(name) end " +
        "  if _G then return _G[name] end " +
        "  return nil " +
        "end " +
        "local function safe_text(frame) " +
        "  if not frame then return '' end " +
        "  if frame.GetText then " +
        "    local ok, txt = pcall(frame.GetText, frame) " +
        "    if ok and txt then " +
        "      local s = tostring(txt) " +
        "      s = string.gsub(s, '[|;\\r\\n]', '/') " +
        "      return s " +
        "    end " +
        "  end " +
        "  return '' " +
        "end " +
        "local function push(tbl, name, frame) " +
        "  if not frame then return end " +
        "  local visible = 0 " +
        "  if frame.IsVisible then " +
        "    local okVisible, v = pcall(frame.IsVisible, frame) " +
        "    if okVisible and v then visible = 1 end " +
        "  end " +
        "  local enabled = 1 " +
        "  if frame.IsEnabled then " +
        "    local okEnabled, e = pcall(frame.IsEnabled, frame) " +
        "    enabled = (okEnabled and e) and 1 or 0 " +
        "  end " +
        "  local checked = -1 " +
        "  if frame.GetChecked then " +
        "    local okChecked, c = pcall(frame.GetChecked, frame) " +
        "    if okChecked then checked = c and 1 or 0 end " +
        "  end " +
        "  local txt = safe_text(frame) " +
        "  table.insert(tbl, tostring(name) .. ':v' .. tostring(visible) .. ':e' .. tostring(enabled) .. ':c' .. tostring(checked) .. ':t=' .. txt) " +
        "end " +
        "local entries = {} " +
        "local names = { " +
        "  'RealmWizardFrame', " +
        "  'RealmWizardFrameEnglishCheckButton', " +
        "  'RealmWizardFrameEnglishButton', " +
        "  'RealmWizardSuggestRealmButton', " +
        "  'RealmWizardFrameSuggestRealmButton', " +
        "  'RealmWizardFrameRealmButton', " +
        "  'RealmWizardOkayButton', " +
        "  'RealmWizardFrameOkayButton', " +
        "  'RealmWizardAcceptButton', " +
        "  'RealmWizardFrameAcceptButton', " +
        "  'GlueDialog', " +
        "  'GlueDialogButton1', " +
        "  'GlueDialogButton2' " +
        "} " +
        "for i = 1, table.getn(names) do " +
        "  local n = names[i] " +
        "  push(entries, n, wwow_get(n)) " +
        "end " +
        "local probeFunctions = { " +
        "  'ChangeRealm', " +
        "  'RequestRealmList', " +
        "  'GetNumRealms', " +
        "  'GetRealmInfo', " +
        "  'GetRealmCategories', " +
        "  'GetDefaultLanguage', " +
        "  'GetInputLanguage', " +
        "  'ToggleInputLanguage' " +
        "} " +
        "for i = 1, table.getn(probeFunctions) do " +
        "  local fnName = probeFunctions[i] " +
        "  local fn = wwow_get(fnName) " +
        "  local marker = 'fn.' .. fnName .. ':f' .. tostring(type(fn) == 'function' and 1 or 0) " +
        "  if type(fn) == 'function' and fnName == 'GetNumRealms' then " +
        "    local okCount, count = pcall(fn) " +
        "    marker = marker .. ':count=' .. tostring(okCount and count or 'error') " +
        "  elseif type(fn) == 'function' and fnName == 'GetRealmInfo' then " +
        "    local okInfoA, nameA = pcall(fn, 1, 1) " +
        "    if okInfoA and nameA then " +
        "      marker = marker .. ':sample=' .. tostring(nameA) " +
        "    else " +
        "      local okInfoB, nameB = pcall(fn, 0, 1) " +
        "      marker = marker .. ':sample=' .. tostring(okInfoB and nameB or 'error') " +
        "    end " +
        "  end " +
        "  table.insert(entries, marker) " +
        "end " +
        "local globals = (getglobals and getglobals()) or _G " +
        "local funcs = {} " +
        "local visibles = {} " +
        "if globals then " +
        "  local extra = 0 " +
        "  local fx = 0 " +
        "  local vx = 0 " +
        "  for n, frame in pairs(globals) do " +
        "    if type(n) == 'string' then " +
        "      local lower = string.lower(n) " +
        "      if (type(frame) == 'table' or type(frame) == 'userdata') and frame and frame.IsVisible and frame.Click and vx < 40 then " +
        "        local okVisible, isVisible = pcall(frame.IsVisible, frame) " +
        "        if okVisible and isVisible then " +
        "          table.insert(visibles, tostring(n) .. ':t=' .. safe_text(frame)) " +
        "          vx = vx + 1 " +
        "        end " +
        "      end " +
        "      if (string.find(lower, 'realmwizard', 1, true) or string.find(lower, 'realm', 1, true) or string.find(lower, 'language', 1, true) or string.find(lower, 'suggest', 1, true) or string.find(lower, 'gluedialogbutton', 1, true)) and frame and (frame.IsVisible or frame.Click or frame.GetText) and extra < 60 then " +
        "        push(entries, n, frame) " +
        "        extra = extra + 1 " +
        "      end " +
        "      if (string.find(lower, 'realmwizard', 1, true) or string.find(lower, 'realm', 1, true) or string.find(lower, 'language', 1, true) or string.find(lower, 'suggest', 1, true) or string.find(lower, 'gluedialog', 1, true)) and type(frame) == 'function' and fx < 80 then " +
        "        table.insert(funcs, n) " +
        "        fx = fx + 1 " +
        "      end " +
        "    end " +
        "  end " +
        "end " +
        "table.sort(entries) " +
        "table.sort(funcs) " +
        "table.sort(visibles) " +
        "{0} = tostring(CURRENT_GLUE_SCREEN or '') " +
        "{1} = table.concat(entries, ';') " +
        "{2} = table.concat(funcs, ';') " +
        "{3} = table.concat(visibles, ';')";

    private const string SelectEnglishLua =
        "if RealmWizardFrameEnglishCheckButton and RealmWizardFrameEnglishCheckButton:IsVisible() and RealmWizardFrameEnglishCheckButton.Click then " +
        "  if RealmWizardFrameEnglishCheckButton.GetChecked then " +
        "    local okChecked, checked = pcall(RealmWizardFrameEnglishCheckButton.GetChecked, RealmWizardFrameEnglishCheckButton) " +
        "    if okChecked and checked then return end " +
        "  end " +
        "  RealmWizardFrameEnglishCheckButton:Click() " +
        "elseif RealmWizardFrameEnglishButton and RealmWizardFrameEnglishButton:IsVisible() and RealmWizardFrameEnglishButton.Click then " +
        "  if RealmWizardFrameEnglishButton.GetChecked then " +
        "    local okChecked2, checked2 = pcall(RealmWizardFrameEnglishButton.GetChecked, RealmWizardFrameEnglishButton) " +
        "    if okChecked2 and checked2 then return end " +
        "  end " +
        "  RealmWizardFrameEnglishButton:Click() " +
        "elseif GlueDialogButton1 and GlueDialogButton1:IsVisible() and GlueDialogButton1.Click and GlueDialogButton1.GetText and string.find(string.lower(tostring(GlueDialogButton1:GetText() or '')), 'english', 1, true) then " +
        "  GlueDialogButton1:Click() " +
        "elseif GlueDialogButton2 and GlueDialogButton2:IsVisible() and GlueDialogButton2.Click and GlueDialogButton2.GetText and string.find(string.lower(tostring(GlueDialogButton2:GetText() or '')), 'english', 1, true) then " +
        "  GlueDialogButton2:Click() " +
        "elseif RealmWizardFrameEnglishCheckButton and RealmWizardFrameEnglishCheckButton.Click then " +
        "  pcall(RealmWizardFrameEnglishCheckButton.Click, RealmWizardFrameEnglishCheckButton) " +
        "elseif RealmWizardFrameEnglishButton and RealmWizardFrameEnglishButton.Click then " +
        "  pcall(RealmWizardFrameEnglishButton.Click, RealmWizardFrameEnglishButton) " +
        "elseif GlueDialogButton1 and GlueDialogButton1.GetScript then " +
        "  local okScript, script = pcall(GlueDialogButton1.GetScript, GlueDialogButton1, 'OnClick') " +
        "  if okScript and script then pcall(script, GlueDialogButton1); return end " +
        "elseif GlueDialogButton1 and GlueDialogButton1.Click then " +
        "  pcall(GlueDialogButton1.Click, GlueDialogButton1) " +
        "elseif GlueDialogButton2 and GlueDialogButton2.GetScript then " +
        "  local okScript2, script2 = pcall(GlueDialogButton2.GetScript, GlueDialogButton2, 'OnClick') " +
        "  if okScript2 and script2 then pcall(script2, GlueDialogButton2); return end " +
        "elseif GlueDialogButton2 and GlueDialogButton2.Click then " +
        "  pcall(GlueDialogButton2.Click, GlueDialogButton2) " +
        "end";

    private const string ClickSuggestRealmLua =
        "if RealmWizardSuggestRealmButton and RealmWizardSuggestRealmButton:IsVisible() and RealmWizardSuggestRealmButton.Click and ((not RealmWizardSuggestRealmButton.IsEnabled) or RealmWizardSuggestRealmButton:IsEnabled()) then " +
        "  RealmWizardSuggestRealmButton:Click() " +
        "elseif RealmWizardFrameSuggestRealmButton and RealmWizardFrameSuggestRealmButton:IsVisible() and RealmWizardFrameSuggestRealmButton.Click and ((not RealmWizardFrameSuggestRealmButton.IsEnabled) or RealmWizardFrameSuggestRealmButton:IsEnabled()) then " +
        "  RealmWizardFrameSuggestRealmButton:Click() " +
        "elseif RealmWizardFrameRealmButton and RealmWizardFrameRealmButton:IsVisible() and RealmWizardFrameRealmButton.Click and ((not RealmWizardFrameRealmButton.IsEnabled) or RealmWizardFrameRealmButton:IsEnabled()) then " +
        "  RealmWizardFrameRealmButton:Click() " +
        "elseif GlueDialogButton1 and GlueDialogButton1:IsVisible() and GlueDialogButton1.Click and GlueDialogButton1.GetText and string.find(string.lower(tostring(GlueDialogButton1:GetText() or '')), 'suggest', 1, true) then " +
        "  GlueDialogButton1:Click() " +
        "elseif GlueDialogButton2 and GlueDialogButton2:IsVisible() and GlueDialogButton2.Click and GlueDialogButton2.GetText and string.find(string.lower(tostring(GlueDialogButton2:GetText() or '')), 'suggest', 1, true) then " +
        "  GlueDialogButton2:Click() " +
        "elseif RealmWizardSuggestRealmButton and RealmWizardSuggestRealmButton.Click then " +
        "  pcall(RealmWizardSuggestRealmButton.Click, RealmWizardSuggestRealmButton) " +
        "elseif RealmWizardFrameSuggestRealmButton and RealmWizardFrameSuggestRealmButton.Click then " +
        "  pcall(RealmWizardFrameSuggestRealmButton.Click, RealmWizardFrameSuggestRealmButton) " +
        "elseif RealmWizardFrameRealmButton and RealmWizardFrameRealmButton.Click then " +
        "  pcall(RealmWizardFrameRealmButton.Click, RealmWizardFrameRealmButton) " +
        "elseif RealmWizardSuggestRealm and type(RealmWizardSuggestRealm) == 'function' then " +
        "  RealmWizardSuggestRealm() " +
        "elseif GlueDialogButton1 and GlueDialogButton1.GetScript then " +
        "  local okScript, script = pcall(GlueDialogButton1.GetScript, GlueDialogButton1, 'OnClick') " +
        "  if okScript and script then pcall(script, GlueDialogButton1); return end " +
        "elseif GlueDialogButton1 and GlueDialogButton1.Click then " +
        "  pcall(GlueDialogButton1.Click, GlueDialogButton1) " +
        "elseif GlueDialogButton2 and GlueDialogButton2.GetScript then " +
        "  local okScript2, script2 = pcall(GlueDialogButton2.GetScript, GlueDialogButton2, 'OnClick') " +
        "  if okScript2 and script2 then pcall(script2, GlueDialogButton2); return end " +
        "elseif GlueDialogButton2 and GlueDialogButton2.Click then " +
        "  pcall(GlueDialogButton2.Click, GlueDialogButton2) " +
        "end";

    private const string ClickSuggestedRealmAcceptanceLua =
        "if RealmWizardOkayButton and RealmWizardOkayButton:IsVisible() and RealmWizardOkayButton.Click and ((not RealmWizardOkayButton.IsEnabled) or RealmWizardOkayButton:IsEnabled()) then " +
        "  RealmWizardOkayButton:Click() " +
        "elseif RealmWizardFrameOkayButton and RealmWizardFrameOkayButton:IsVisible() and RealmWizardFrameOkayButton.Click and ((not RealmWizardFrameOkayButton.IsEnabled) or RealmWizardFrameOkayButton:IsEnabled()) then " +
        "  RealmWizardFrameOkayButton:Click() " +
        "elseif RealmWizardAcceptButton and RealmWizardAcceptButton:IsVisible() and RealmWizardAcceptButton.Click and ((not RealmWizardAcceptButton.IsEnabled) or RealmWizardAcceptButton:IsEnabled()) then " +
        "  RealmWizardAcceptButton:Click() " +
        "elseif RealmWizardFrameAcceptButton and RealmWizardFrameAcceptButton:IsVisible() and RealmWizardFrameAcceptButton.Click and ((not RealmWizardFrameAcceptButton.IsEnabled) or RealmWizardFrameAcceptButton:IsEnabled()) then " +
        "  RealmWizardFrameAcceptButton:Click() " +
        "elseif GlueDialogButton1 and GlueDialogButton1:IsVisible() and GlueDialogButton1.Click and GlueDialogButton1.GetText and (string.find(string.lower(tostring(GlueDialogButton1:GetText() or '')), 'okay', 1, true) or string.find(string.lower(tostring(GlueDialogButton1:GetText() or '')), 'accept', 1, true) or string.find(string.lower(tostring(GlueDialogButton1:GetText() or '')), 'ok', 1, true)) then " +
        "  GlueDialogButton1:Click() " +
        "elseif GlueDialogButton2 and GlueDialogButton2:IsVisible() and GlueDialogButton2.Click and GlueDialogButton2.GetText and (string.find(string.lower(tostring(GlueDialogButton2:GetText() or '')), 'okay', 1, true) or string.find(string.lower(tostring(GlueDialogButton2:GetText() or '')), 'accept', 1, true) or string.find(string.lower(tostring(GlueDialogButton2:GetText() or '')), 'ok', 1, true)) then " +
        "  GlueDialogButton2:Click() " +
        "elseif RealmWizardOkayButton and RealmWizardOkayButton.Click then " +
        "  pcall(RealmWizardOkayButton.Click, RealmWizardOkayButton) " +
        "elseif RealmWizardFrameOkayButton and RealmWizardFrameOkayButton.Click then " +
        "  pcall(RealmWizardFrameOkayButton.Click, RealmWizardFrameOkayButton) " +
        "elseif RealmWizardAcceptButton and RealmWizardAcceptButton.Click then " +
        "  pcall(RealmWizardAcceptButton.Click, RealmWizardAcceptButton) " +
        "elseif RealmWizardFrameAcceptButton and RealmWizardFrameAcceptButton.Click then " +
        "  pcall(RealmWizardFrameAcceptButton.Click, RealmWizardFrameAcceptButton) " +
        "elseif GlueDialogButton1 and GlueDialogButton1.GetScript then " +
        "  local okScript, script = pcall(GlueDialogButton1.GetScript, GlueDialogButton1, 'OnClick') " +
        "  if okScript and script then pcall(script, GlueDialogButton1); return end " +
        "elseif GlueDialogButton1 and GlueDialogButton1.Click then " +
        "  pcall(GlueDialogButton1.Click, GlueDialogButton1) " +
        "elseif GlueDialogButton2 and GlueDialogButton2.GetScript then " +
        "  local okScript2, script2 = pcall(GlueDialogButton2.GetScript, GlueDialogButton2, 'OnClick') " +
        "  if okScript2 and script2 then pcall(script2, GlueDialogButton2); return end " +
        "elseif GlueDialogButton2 and GlueDialogButton2.Click then " +
        "  pcall(GlueDialogButton2.Click, GlueDialogButton2) " +
        "end";

    private const string DirectRealmSelectionApiLua =
        "local function wwow_get(name) " +
        "  if getglobal then return getglobal(name) end " +
        "  if _G then return _G[name] end " +
        "  return nil " +
        "end " +
        "local changeRealm = wwow_get('ChangeRealm') " +
        "local requestRealmList = wwow_get('RequestRealmList') " +
        "local getNumRealms = wwow_get('GetNumRealms') " +
        "local getRealmInfo = wwow_get('GetRealmInfo') " +
        "local changed = false " +
        "if type(requestRealmList) == 'function' then pcall(requestRealmList) end " +
        "if type(changeRealm) == 'function' then " +
        "  local candidates = { {1,1}, {0,1}, {1,0}, {0,0}, {2,1}, {1,2}, {2,0}, {0,2} } " +
        "  for i = 1, table.getn(candidates) do " +
        "    local pair = candidates[i] " +
        "    if pcall(changeRealm, pair[1], pair[2]) then changed = true; break end " +
        "  end " +
        "end " +
        "if (not changed) and type(getNumRealms) == 'function' and type(changeRealm) == 'function' then " +
        "  local okCount, realmCount = pcall(getNumRealms) " +
        "  if okCount and realmCount and realmCount > 0 then " +
        "    for i = 1, realmCount do " +
        "      if pcall(changeRealm, 1, i) then changed = true; break end " +
        "      if pcall(changeRealm, 0, i) then changed = true; break end " +
        "    end " +
        "  end " +
        "end " +
        "if (not changed) and type(getRealmInfo) == 'function' and type(changeRealm) == 'function' then " +
        "  local okName, realmName = pcall(getRealmInfo, 1, 1) " +
        "  if okName and realmName and SetCVar and type(SetCVar) == 'function' then pcall(SetCVar, 'realmName', tostring(realmName)) end " +
        "end " +
        "if not changed then " +
        "  if GlueDialogButton1 and GlueDialogButton1.GetScript then local okScript, script = pcall(GlueDialogButton1.GetScript, GlueDialogButton1, 'OnClick'); if okScript and script then pcall(script, GlueDialogButton1); changed = true end end " +
        "end " +
        "if (not changed) and GlueDialogButton1 and GlueDialogButton1.Click then pcall(GlueDialogButton1.Click, GlueDialogButton1); changed = true end " +
        "if (not changed) and GlueDialog_OnAccept then pcall(GlueDialog_OnAccept); changed = true end " +
        "if (not changed) and GlueDialog_OnKeyDown then arg1 = 'ENTER'; pcall(GlueDialog_OnKeyDown); changed = true end";

    private readonly Func<WoWScreenState> _getScreenState;
    private readonly Func<int> _getMaxCharacterCount;
    private readonly Action<string> _luaCall;
    private readonly Func<string, string[]> _luaCallWithResult;
    private readonly Func<string?> _getLoginState;
    private readonly Action<string>? _captureLuaErrors;
    private readonly Action<string>? _traceLog;

    private Realm? _selectedRealm;
    private DateTime? _charSelectFirstSeen;
    private bool _realmConfirmed;
    private bool _hasSeenActiveRealmWizard;
    private DateTime _lastRealmWizardActionAt = DateTime.MinValue;
    private DateTime _lastRealmWizardControlDumpAt = DateTime.MinValue;
    private RealmWizardState _lastRealmWizardState = RealmWizardState.None;
    private RealmWizardSequenceStep _realmWizardSequenceStep = RealmWizardSequenceStep.SelectEnglish;
    private int _stateDetectedSequenceAttemptCount;
    private string _lastSnapshotSignature = string.Empty;

    public FgRealmSelectScreen(
        Func<WoWScreenState> getScreenState,
        Func<int> getMaxCharacterCount,
        Action<string> luaCall,
        Func<string, string[]> luaCallWithResult,
        Func<string?>? getLoginState = null,
        Action<string>? captureLuaErrors = null,
        Action<string>? traceLog = null)
    {
        _getScreenState = getScreenState;
        _getMaxCharacterCount = getMaxCharacterCount;
        _luaCall = luaCall;
        _luaCallWithResult = luaCallWithResult;
        _getLoginState = getLoginState ?? (() => null);
        _captureLuaErrors = captureLuaErrors;
        _traceLog = traceLog;
    }

    public bool IsOpen
    {
        get
        {
            if (_selectedRealm != null || _realmConfirmed)
                return false;

            if (Statics.ObjectManager.PauseNativeCallsDuringWorldEntry)
                return false;

            var wizardSnapshot = ReadRealmWizardSnapshot();
            if (wizardSnapshot.IsActive)
                return true;

            var screenState = _getScreenState();
            if (screenState == WoWScreenState.Connecting)
                return false;
            if (screenState != WoWScreenState.CharacterSelect)
                return false;

            var maxChar = _getMaxCharacterCount();
            if (maxChar > 0)
            {
                _realmConfirmed = true;
                return false;
            }

            // Keep this open while we run state-based realmwizard automation.
            _charSelectFirstSeen ??= DateTime.UtcNow;
            if (DateTime.UtcNow - _charSelectFirstSeen.Value > StateBasedRealmWizardWindow)
            {
                Trace(
                    "[FG-REALM] State-based window expired ({Period}s). Char enum still 0; treating realm as selected.",
                    StateBasedRealmWizardWindow.TotalSeconds);
                _realmConfirmed = true;
                return false;
            }

            return true;
        }
    }

    public Realm? CurrentRealm
    {
        get
        {
            var wizardSnapshot = ReadRealmWizardSnapshot();
            if (wizardSnapshot.IsActive)
                return null;

            if (_selectedRealm != null)
                return _selectedRealm;
            if (_realmConfirmed)
                return AutoSelectedRealm;
            if (Statics.ObjectManager.PauseNativeCallsDuringWorldEntry)
                return AutoSelectedRealm;
            if (_getMaxCharacterCount() > 0)
                return AutoSelectedRealm;
            return null;
        }
    }

    public List<Realm> GetRealmList() => [new Realm { RealmName = "Default", RealmId = 1 }];

    public void SelectRealm(Realm realm)
    {
        var wizardSnapshot = ReadRealmWizardSnapshot();
        Trace(
            "[FG-REALM] SelectRealm invoked. active={Active} stateDetected={StateDetected} screen={Screen} maxChars={MaxChars} transitionCooldown={TransitionCooldown}",
            wizardSnapshot.IsActive,
            wizardSnapshot.IsStateDetected,
            _getScreenState(),
            _getMaxCharacterCount(),
            Statics.ObjectManager.IsInScreenTransitionCooldown);

        if (!wizardSnapshot.IsActive && _hasSeenActiveRealmWizard && IsCharacterListState(wizardSnapshot))
        {
            Trace(
                "[FG-REALM] Realm wizard handoff detected (screen='{Screen}' loginState='{LoginState}' maxChars={MaxChars}).",
                wizardSnapshot.CurrentGlueScreen,
                _getLoginState()?.Trim() ?? string.Empty,
                _getMaxCharacterCount());
            _realmConfirmed = true;
            ResetRealmWizardAutomationState();
            return;
        }

        if (ShouldDriveStateBasedRealmWizardFlow(wizardSnapshot))
        {
            if (!Statics.ObjectManager.IsInScreenTransitionCooldown)
            {
                TryDumpRealmWizardControls("select-realm");
                DriveRealmWizardFlow(wizardSnapshot.IsActive
                    ? wizardSnapshot
                    : RealmWizardSnapshot.StateOnly(isStateDetected: true));
            }
            else
            {
                Trace("[FG-REALM] Deferring wizard flow due to screen transition cooldown.");
            }
            return;
        }

        ResetRealmWizardAutomationState();

        if (_getMaxCharacterCount() > 0)
        {
            _selectedRealm = realm;
            return;
        }
    }

    public void SelectRealmType(RealmType realmType) { }

    public void CancelRealmSelection()
    {
        _selectedRealm = null;
    }

    private RealmWizardSnapshot ReadRealmWizardSnapshot()
    {
        var stateDetected = IsRealmWizardByLoginState();

        try
        {
            var result = ExecuteLuaWithResult(RealmWizardSnapshotLua, "realmwizard.snapshot");
            if (result.Length == 0 || string.IsNullOrWhiteSpace(result[0]))
            {
                var stateOnly = RealmWizardSnapshot.StateOnly(stateDetected);
                TraceSnapshotIfChanged(stateOnly, "lua-empty");
                return stateOnly;
            }

            var parts = result[0].Split('|');
            if (parts.Length < 10)
            {
                var stateOnly = RealmWizardSnapshot.StateOnly(stateDetected);
                TraceSnapshotIfChanged(stateOnly, "lua-malformed");
                return stateOnly;
            }

            var currentGlueScreen = parts[9];
            var activeFromLua = IsFlagEnabled(parts[0]);
            var activeFromCurrentGlue = currentGlueScreen.Equals("realmwizard", StringComparison.OrdinalIgnoreCase);
            var isActive = activeFromLua || activeFromCurrentGlue || stateDetected;
            var effectiveStateDetected = stateDetected || activeFromCurrentGlue;

            var snapshot = new RealmWizardSnapshot(
                isActive,
                IsFlagEnabled(parts[1]),
                IsFlagEnabled(parts[2]),
                IsFlagEnabled(parts[3]),
                IsFlagEnabled(parts[4]),
                IsFlagEnabled(parts[5]),
                IsFlagEnabled(parts[6]),
                IsFlagEnabled(parts[7]),
                IsFlagEnabled(parts[8]),
                currentGlueScreen,
                effectiveStateDetected);
            if (snapshot.IsActive)
                _hasSeenActiveRealmWizard = true;
            TraceSnapshotIfChanged(snapshot, "lua");
            return snapshot;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[FG-REALM] Realm wizard snapshot Lua query failed.");
            var stateOnly = RealmWizardSnapshot.StateOnly(stateDetected);
            if (stateOnly.IsActive)
                _hasSeenActiveRealmWizard = true;
            TraceSnapshotIfChanged(stateOnly, "lua-exception");
            return stateOnly;
        }
    }

    private bool IsRealmWizardByLoginState()
    {
        try
        {
            var loginState = _getLoginState()?.Trim();
            return loginState != null
                && loginState.Equals("realmwizard", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private bool ShouldDriveStateBasedRealmWizardFlow(in RealmWizardSnapshot snapshot)
    {
        if (snapshot.IsActive)
            return true;

        if (_hasSeenActiveRealmWizard && IsCharacterListState(snapshot))
            return false;

        if (_selectedRealm != null || _realmConfirmed)
            return false;

        if (Statics.ObjectManager.PauseNativeCallsDuringWorldEntry)
            return false;

        if (_getScreenState() != WoWScreenState.CharacterSelect)
            return false;

        if (_getMaxCharacterCount() > 0)
            return false;

        _charSelectFirstSeen ??= DateTime.UtcNow;
        return DateTime.UtcNow - _charSelectFirstSeen.Value <= StateBasedRealmWizardWindow;
    }

    private bool IsCharacterListState(in RealmWizardSnapshot snapshot)
    {
        if (snapshot.CurrentGlueScreen.Equals("charselect", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            var loginState = _getLoginState()?.Trim();
            return loginState != null
                && loginState.Equals("charselect", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFlagEnabled(string value) => string.Equals(value, "1", StringComparison.Ordinal);

    private static RealmWizardState ResolveRealmWizardState(in RealmWizardSnapshot snapshot)
    {
        if (!snapshot.IsActive)
            return RealmWizardState.None;

        if ((snapshot.IsOkayVisible && snapshot.IsOkayEnabled)
            || (snapshot.IsAcceptVisible && snapshot.IsAcceptEnabled))
        {
            return RealmWizardState.ConfirmSuggestedRealm;
        }

        if (snapshot.IsEnglishVisible && !snapshot.IsEnglishChecked)
            return RealmWizardState.SelectEnglish;

        if (snapshot.IsSuggestVisible && snapshot.IsSuggestEnabled)
            return RealmWizardState.RequestSuggestedRealm;

        if (snapshot.IsSuggestVisible)
            return RealmWizardState.WaitingForSuggestion;

        if (snapshot.IsStateDetected)
            return RealmWizardState.StateDetectedSequence;

        return RealmWizardState.WaitingForExpectedControls;
    }

    private void DriveRealmWizardFlow(in RealmWizardSnapshot snapshot)
    {
        var wizardState = ResolveRealmWizardState(snapshot);
        if (wizardState != _lastRealmWizardState)
        {
            Log.Information(
                "[FG-REALM] Wizard state={State} screen={Screen} englishVisible={EnglishVisible} englishChecked={EnglishChecked} suggestVisible={SuggestVisible} suggestEnabled={SuggestEnabled} okayVisible={OkayVisible} okayEnabled={OkayEnabled} acceptVisible={AcceptVisible} acceptEnabled={AcceptEnabled} stateDetected={StateDetected}",
                wizardState,
                snapshot.CurrentGlueScreen,
                snapshot.IsEnglishVisible,
                snapshot.IsEnglishChecked,
                snapshot.IsSuggestVisible,
                snapshot.IsSuggestEnabled,
                snapshot.IsOkayVisible,
                snapshot.IsOkayEnabled,
                snapshot.IsAcceptVisible,
                snapshot.IsAcceptEnabled,
                snapshot.IsStateDetected);
            Trace(
                "[FG-REALM] Wizard state changed -> {State} screen={Screen} english={EnglishVisible}/{EnglishChecked} suggest={SuggestVisible}/{SuggestEnabled} okay={OkayVisible}/{OkayEnabled} accept={AcceptVisible}/{AcceptEnabled} stateDetected={StateDetected}",
                wizardState,
                snapshot.CurrentGlueScreen,
                snapshot.IsEnglishVisible,
                snapshot.IsEnglishChecked,
                snapshot.IsSuggestVisible,
                snapshot.IsSuggestEnabled,
                snapshot.IsOkayVisible,
                snapshot.IsOkayEnabled,
                snapshot.IsAcceptVisible,
                snapshot.IsAcceptEnabled,
                snapshot.IsStateDetected);
            TryDumpRealmWizardControls("state-change", force: true);
            _lastRealmWizardState = wizardState;
        }

        switch (wizardState)
        {
            case RealmWizardState.None:
            case RealmWizardState.WaitingForSuggestion:
            case RealmWizardState.WaitingForExpectedControls:
                return;
        }

        if (DateTime.UtcNow - _lastRealmWizardActionAt < RealmWizardActionCooldown)
        {
            Trace("[FG-REALM] Action cooldown active ({CooldownMs}ms).", RealmWizardActionCooldown.TotalMilliseconds);
            return;
        }

        _lastRealmWizardActionAt = DateTime.UtcNow;

        if (wizardState == RealmWizardState.StateDetectedSequence)
        {
            _stateDetectedSequenceAttemptCount++;
            if (_stateDetectedSequenceAttemptCount >= StateDetectedDirectApiThreshold)
            {
                Trace(
                    "[FG-REALM] Executing state-detected direct realm API action (attempt={Attempt} threshold={Threshold}).",
                    _stateDetectedSequenceAttemptCount,
                    StateDetectedDirectApiThreshold);
                ExecuteLua(DirectRealmSelectionApiLua, "realmwizard.direct-api");
                return;
            }

            wizardState = ResolveStateSequenceAction();
        }
        else
        {
            _stateDetectedSequenceAttemptCount = 0;
        }

        switch (wizardState)
        {
            case RealmWizardState.SelectEnglish:
                Trace("[FG-REALM] Executing action: select english.");
                ExecuteLua(SelectEnglishLua, "realmwizard.select-english");
                _realmWizardSequenceStep = RealmWizardSequenceStep.RequestSuggestion;
                return;

            case RealmWizardState.RequestSuggestedRealm:
                Trace("[FG-REALM] Executing action: suggest realm.");
                ExecuteLua(ClickSuggestRealmLua, "realmwizard.suggest-realm");
                _realmWizardSequenceStep = RealmWizardSequenceStep.ConfirmSuggestion;
                return;

            case RealmWizardState.ConfirmSuggestedRealm:
                Trace("[FG-REALM] Executing action: confirm suggested realm.");
                ExecuteLua(ClickSuggestedRealmAcceptanceLua, "realmwizard.confirm-suggestion");
                _realmWizardSequenceStep = RealmWizardSequenceStep.SelectEnglish;
                return;
        }
    }

    private void ExecuteLua(string lua, string context)
    {
        try
        {
            _luaCall(lua);
        }
        finally
        {
            CaptureLuaErrors(context);
        }
    }

    private string[] ExecuteLuaWithResult(string lua, string context)
    {
        try
        {
            return _luaCallWithResult(lua);
        }
        finally
        {
            CaptureLuaErrors(context);
        }
    }

    private void CaptureLuaErrors(string context)
    {
        if (_captureLuaErrors == null)
            return;

        try
        {
            _captureLuaErrors(context);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[FG-REALM] Lua error capture callback failed for context {Context}", context);
        }
    }

    private void TraceSnapshotIfChanged(in RealmWizardSnapshot snapshot, string source)
    {
        var signature = string.Join("|",
            source,
            snapshot.IsActive,
            snapshot.IsEnglishVisible,
            snapshot.IsEnglishChecked,
            snapshot.IsSuggestVisible,
            snapshot.IsSuggestEnabled,
            snapshot.IsAcceptVisible,
            snapshot.IsAcceptEnabled,
            snapshot.IsOkayVisible,
            snapshot.IsOkayEnabled,
            snapshot.CurrentGlueScreen,
            snapshot.IsStateDetected);

        if (string.Equals(signature, _lastSnapshotSignature, StringComparison.Ordinal))
            return;

        _lastSnapshotSignature = signature;
        Trace(
            "[FG-REALM] Snapshot({Source}) active={Active} screen='{Screen}' english={EnglishVisible}/{EnglishChecked} suggest={SuggestVisible}/{SuggestEnabled} okay={OkayVisible}/{OkayEnabled} accept={AcceptVisible}/{AcceptEnabled} stateDetected={StateDetected}",
            source,
            snapshot.IsActive,
            snapshot.CurrentGlueScreen,
            snapshot.IsEnglishVisible,
            snapshot.IsEnglishChecked,
            snapshot.IsSuggestVisible,
            snapshot.IsSuggestEnabled,
            snapshot.IsOkayVisible,
            snapshot.IsOkayEnabled,
            snapshot.IsAcceptVisible,
            snapshot.IsAcceptEnabled,
            snapshot.IsStateDetected);

        if (snapshot.IsActive || snapshot.IsStateDetected)
            TryDumpRealmWizardControls($"snapshot-{source}", force: true);
    }

    private void TryDumpRealmWizardControls(string reason, bool force = false)
    {
        if (_traceLog == null)
            return;

        if (!force && DateTime.UtcNow - _lastRealmWizardControlDumpAt < RealmWizardControlDumpInterval)
            return;

        _lastRealmWizardControlDumpAt = DateTime.UtcNow;

        try
        {
            var dump = ExecuteLuaWithResult(RealmWizardControlDumpLua, "realmwizard.controls");
            var current = dump.Length > 0 ? dump[0] : string.Empty;
            var controls = dump.Length > 1 ? dump[1] : string.Empty;
            var functions = dump.Length > 2 ? dump[2] : string.Empty;
            var visibles = dump.Length > 3 ? dump[3] : string.Empty;
            if (string.IsNullOrWhiteSpace(controls))
                controls = "(none)";
            if (string.IsNullOrWhiteSpace(functions))
                functions = "(none)";
            if (string.IsNullOrWhiteSpace(visibles))
                visibles = "(none)";

            Trace("[FG-REALM] controls reason={Reason} current='{Current}' entries={Entries} functions={Functions} visibleClicks={VisibleClicks}", reason, current, controls, functions, visibles);
        }
        catch (Exception ex)
        {
            Trace("[FG-REALM] controls reason={Reason} failed: {Error}", reason, ex.Message);
        }
    }

    private void Trace(string messageTemplate, params object?[] args)
    {
        Log.Information(messageTemplate, args);
        if (_traceLog == null)
            return;

        try
        {
            if (args.Length == 0)
            {
                _traceLog(messageTemplate);
                return;
            }

            var renderedArgs = Array.ConvertAll(args, static arg => arg?.ToString() ?? "(null)");
            _traceLog($"{messageTemplate} | args=[{string.Join(", ", renderedArgs)}]");
        }
        catch
        {
            // Swallow trace callback failures to keep login flow resilient.
        }
    }

    private RealmWizardState ResolveStateSequenceAction() =>
        _realmWizardSequenceStep switch
        {
            RealmWizardSequenceStep.SelectEnglish => RealmWizardState.SelectEnglish,
            RealmWizardSequenceStep.RequestSuggestion => RealmWizardState.RequestSuggestedRealm,
            _ => RealmWizardState.ConfirmSuggestedRealm
        };

    private void ResetRealmWizardAutomationState()
    {
        _lastRealmWizardState = RealmWizardState.None;
        _realmWizardSequenceStep = RealmWizardSequenceStep.SelectEnglish;
        _stateDetectedSequenceAttemptCount = 0;
    }

    private enum RealmWizardState
    {
        None,
        SelectEnglish,
        RequestSuggestedRealm,
        ConfirmSuggestedRealm,
        StateDetectedSequence,
        WaitingForSuggestion,
        WaitingForExpectedControls
    }

    private enum RealmWizardSequenceStep
    {
        SelectEnglish,
        RequestSuggestion,
        ConfirmSuggestion
    }

    private readonly struct RealmWizardSnapshot(
        bool isActive,
        bool isEnglishVisible,
        bool isEnglishChecked,
        bool isSuggestVisible,
        bool isSuggestEnabled,
        bool isAcceptVisible,
        bool isAcceptEnabled,
        bool isOkayVisible,
        bool isOkayEnabled,
        string currentGlueScreen,
        bool isStateDetected)
    {
        public static RealmWizardSnapshot Inactive =>
            new(false, false, false, false, false, false, false, false, false, string.Empty, false);

        public static RealmWizardSnapshot StateOnly(bool isStateDetected) =>
            isStateDetected
                ? new(true, false, false, false, false, false, false, false, false, "realmwizard", true)
                : Inactive;

        public bool IsActive { get; } = isActive;
        public bool IsEnglishVisible { get; } = isEnglishVisible;
        public bool IsEnglishChecked { get; } = isEnglishChecked;
        public bool IsSuggestVisible { get; } = isSuggestVisible;
        public bool IsSuggestEnabled { get; } = isSuggestEnabled;
        public bool IsAcceptVisible { get; } = isAcceptVisible;
        public bool IsAcceptEnabled { get; } = isAcceptEnabled;
        public bool IsOkayVisible { get; } = isOkayVisible;
        public bool IsOkayEnabled { get; } = isOkayEnabled;
        public string CurrentGlueScreen { get; } = currentGlueScreen;
        public bool IsStateDetected { get; } = isStateDetected;
    }
}
