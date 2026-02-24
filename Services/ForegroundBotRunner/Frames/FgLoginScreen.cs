using GameData.Core.Enums;
using GameData.Core.Frames;
using System;

namespace ForegroundBotRunner.Frames;

/// <summary>
/// ILoginScreen implementation for the Foreground (DLL-injected) bot.
/// Wraps FG's memory-based login via Lua calls.
/// </summary>
public class FgLoginScreen(Func<WoWScreenState> getScreenState, Action<string, string> defaultServerLogin, Action resetLogin) : ILoginScreen
{
    private DateTime? _loginScreenFirstSeen;
    private static readonly TimeSpan LuaInitGracePeriod = TimeSpan.FromSeconds(3);

    public bool IsOpen
    {
        get
        {
            var state = getScreenState();
            return state == WoWScreenState.LoginScreen;
        }
    }

    public void Login(string username, string password)
    {
        var state = getScreenState();
        if (state != WoWScreenState.LoginScreen)
            return;

        // Track when login screen was first seen — Lua needs ~3s to initialize
        _loginScreenFirstSeen ??= DateTime.Now;
        if (DateTime.Now - _loginScreenFirstSeen.Value < LuaInitGracePeriod)
            return; // BotRunnerService retries every 100ms via behavior tree re-tick

        defaultServerLogin(username, password);
    }

    public bool IsLoggedIn
    {
        get
        {
            var state = getScreenState();
            return state != WoWScreenState.LoginScreen
                && state != WoWScreenState.Disconnected
                && state != WoWScreenState.Unknown
                && state != WoWScreenState.ProcessNotAvailable;
        }
    }

    public uint QueuePosition => 0; // WoW.exe handles queue UI natively

    public void CancelLogin()
    {
        resetLogin();
        _loginScreenFirstSeen = null;
    }
}
