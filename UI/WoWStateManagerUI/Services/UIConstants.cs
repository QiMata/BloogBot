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

        public const string CharactersConnectionString =
            "server=localhost;user=root;database=characters;port=3306;password=root";

        public const string MangosConnectionString =
            "server=localhost;user=root;database=mangos;port=3306;password=root";

        public const int ServicesRefreshSeconds = 5;
        public const int AccountsRefreshSeconds = 30;

        /// <summary>
        /// Centralized repo configs directory. The Bot csproj copies the contents
        /// of <c>Services/WoWStateManager/Settings/Configs/</c> here on build, and
        /// the live test fixtures consume the same source path. UI + tests share
        /// one source of truth, file names are tracked in git.
        /// </summary>
        public static string ConfigsDirectory =>
            System.IO.Path.Combine(System.AppContext.BaseDirectory, "Settings", "Configs");

        /// <summary>
        /// Default config the UI auto-loads on startup. New hierarchical schema
        /// (Config → Activities → Characters). The legacy flat-list
        /// <c>Default.config.json</c> stays in place untouched so pathfinding
        /// tests that rely on it keep working.
        /// </summary>
        public static string DefaultConfigFileName => "Default.json";
    }
}
