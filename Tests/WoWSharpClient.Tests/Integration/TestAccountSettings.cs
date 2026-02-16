namespace WoWSharpClient.Tests.Integration;

/// <summary>
/// Configuration for test accounts and services used in integration tests.
/// The WoW emulator server is expected to be running externally (always up).
/// These tests connect to the existing server - they do not start it.
/// </summary>
public static class TestAccountSettings
{
    #region WoW Server Settings (External - Always Running)

    /// <summary>
    /// Server IP address for the WoW emulator (auth server).
    /// The server is expected to be running externally.
    /// </summary>
    public const string ServerIpAddress = "127.0.0.1";

    /// <summary>
    /// Auth server port (realmd).
    /// </summary>
    public const int AuthPort = 3724;

    /// <summary>
    /// World server port (mangosd).
    /// </summary>
    public const int WorldPort = 8085;

    #endregion

    #region PathfindingService Settings

    /// <summary>
    /// PathfindingService IP address.
    /// </summary>
    public const string PathfindingServiceIpAddress = "127.0.0.1";

    /// <summary>
    /// PathfindingService port.
    /// </summary>
    public const int PathfindingServicePort = 5001;

    #endregion

    #region CharacterStateListener Settings

    /// <summary>
    /// CharacterStateListener IP address.
    /// </summary>
    public const string CharacterStateListenerIpAddress = "127.0.0.1";

    /// <summary>
    /// CharacterStateListener port.
    /// </summary>
    public const int CharacterStateListenerPort = 5002;

    #endregion

    #region Test Account Settings

    /// <summary>
    /// Default test account username.
    /// This account should have GM level 3 for full command access.
    /// </summary>
    public const string TestAccountUsername = "TESTBOT1";

    /// <summary>
    /// Default test account password.
    /// </summary>
    public const string TestAccountPassword = "PASSWORD";

    /// <summary>
    /// Secondary test account for multi-character scenarios.
    /// </summary>
    public const string SecondaryAccountUsername = "TESTBOT2";

    /// <summary>
    /// Secondary test account password.
    /// </summary>
    public const string SecondaryAccountPassword = "PASSWORD";

    #endregion

    #region Timeout Settings

    /// <summary>
    /// Timeout for connection operations in milliseconds.
    /// </summary>
    public const int ConnectionTimeoutMs = 10000;

    /// <summary>
    /// Timeout for world entry operations in milliseconds.
    /// </summary>
    public const int WorldEntryTimeoutMs = 30000;

    /// <summary>
    /// Timeout for GM command execution in milliseconds.
    /// </summary>
    public const int GmCommandTimeoutMs = 5000;

    /// <summary>
    /// Default polling interval for state checks in milliseconds.
    /// </summary>
    public const int PollingIntervalMs = 100;

    /// <summary>
    /// Timeout for service health checks in milliseconds.
    /// </summary>
    public const int ServiceHealthCheckTimeoutMs = 2000;

    #endregion
}
