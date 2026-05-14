namespace WoWStateManagerUI.Services
{
    /// <summary>
    /// Known fixed values for the running WWoW dev system. The UI auto-connects to
    /// these endpoints at startup rather than prompting the user. Per-system overrides
    /// will be added when a second system is supported.
    /// </summary>
    public static class UIConstants
    {
        public const string ListenerAddress = "127.0.0.1";
        public const int ListenerPort = 9090;

        public const string MangosSoapUrl = "http://localhost:7878";
        public const string MangosUsername = "ADMINISTRATOR";
        public const string MangosPassword = "PASSWORD";

        public const string RealmdConnectionString =
            "server=localhost;user=root;database=realmd;port=3306;password=root";

        public const int ServicesRefreshSeconds = 5;
        public const int AccountsRefreshSeconds = 30;
    }
}
