using GameData.Core.Enums;
using GameData.Core.Frames;
using Serilog;
using System;

namespace ForegroundBotRunner.Frames;

/// <summary>
/// ILoginScreen implementation for the Foreground (DLL-injected) bot.
/// Wraps FG's memory-based login via Lua calls.
/// </summary>
public class FgLoginScreen(
    Func<WoWScreenState> getScreenState,
    Action<string, string> defaultServerLogin,
    Action resetLogin,
    Action dismissGlueDialog) : ILoginScreen
{
    private DateTime? _loginScreenFirstSeen;
    private static readonly TimeSpan LuaInitGracePeriod = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Tracks when DefaultServerLogin was last called. After firing, we must NOT call it again
    /// for at least LoginAttemptCooldown — the WoW client's m_netState needs time to transition
    /// from NS_INITIALIZED to connected. Calling DefaultServerLogin while m_netState != NS_INITIALIZED
    /// causes ERROR #134 (0x85100086) Fatal Condition crash.
    /// </summary>
    private DateTime? _lastLoginAttemptAt;
    private static readonly TimeSpan LoginAttemptCooldown = TimeSpan.FromSeconds(15);

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

        // Wait for screen transition animation to complete before issuing Lua calls.
        // Sending commands during animations causes ACCESS_VIOLATION crashes.
        if (Statics.ObjectManager.IsInScreenTransitionCooldown)
            return;

        // Track when login screen was first seen — Lua needs ~3s to initialize
        _loginScreenFirstSeen ??= DateTime.Now;
        if (DateTime.Now - _loginScreenFirstSeen.Value < LuaInitGracePeriod)
            return; // BotRunnerService retries every 100ms via behavior tree re-tick

        // CRITICAL: Prevent double-fire of DefaultServerLogin.
        // After calling DefaultServerLogin, the WoW client's loginState memory ("login") takes
        // multiple frames to update to "connecting". During this window, the behavior tree re-ticks
        // and calls Login() again. The second DefaultServerLogin call hits m_netState != NS_INITIALIZED
        // → ERROR #134 crash. Wait for the cooldown before allowing another attempt.
        if (_lastLoginAttemptAt.HasValue
            && (DateTime.UtcNow - _lastLoginAttemptAt.Value) < LoginAttemptCooldown)
        {
            return;
        }

        // Dismiss any open GlueDialog (e.g. "Disconnected from server" Okay button)
        // before attempting login. The dialog blocks DefaultServerLogin Lua call.
        // IMPORTANT: Only dismiss error dialogs, NOT the "Success!" dialog which is part
        // of the auth handshake flow.
        dismissGlueDialog();

        Log.Information("[FG-LOGIN] Calling DefaultServerLogin (cooldown {Cooldown}s)", LoginAttemptCooldown.TotalSeconds);
        _lastLoginAttemptAt = DateTime.UtcNow;
        defaultServerLogin(username, password);
    }

    public bool IsLoggedIn
    {
        get
        {
            var state = getScreenState();
            // Connecting means the auth handshake is in progress — NOT yet logged in.
            // Reporting true during Connecting causes the behavior tree to advance to
            // realm selection, which issues Lua calls that crash the client (m_netState assertion).
            return state != WoWScreenState.LoginScreen
                && state != WoWScreenState.Connecting
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
        _lastLoginAttemptAt = null;
    }
}
