using System;
using System.Windows;

namespace WoWStateManagerUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Env var set by <c>Tests.Infrastructure.UiSidecarLauncher</c> to indicate the
        /// UI was launched as a test-fixture sidecar. Read by <c>MainWindow</c> on load
        /// to append a <c>[FIXTURE]</c> marker to the title bar so screenshots are
        /// distinguishable from manual UI runs.
        /// </summary>
        public const string AutoConnectEnvVar = "WWOW_UI_AUTOCONNECT";

        /// <summary>
        /// Env var that carries the StateManager protobuf endpoint (e.g.
        /// <c>tcp://127.0.0.1:8088</c>). Reserved for Phase 3 work that wires the UI's
        /// bot-state surface to the StateManager protobuf channel; not consumed today.
        /// </summary>
        public const string StateManagerUrlEnvVar = "WWOW_UI_STATEMANAGER_URL";

        public App()
        {
        }

        /// <summary>
        /// True when the UI was launched by an integration test fixture (env var
        /// WWOW_UI_AUTOCONNECT=1). Used by MainWindow to mark the title bar.
        /// </summary>
        internal static bool IsFixtureSidecar =>
            Environment.GetEnvironmentVariable(AutoConnectEnvVar) == "1";

        /// <summary>
        /// The StateManager protobuf endpoint passed via env var, or null if the UI is
        /// running in standalone mode. Reserved for Phase 3 consumption.
        /// </summary>
        internal static string? FixtureStateManagerUrl =>
            Environment.GetEnvironmentVariable(StateManagerUrlEnvVar);
    }
}
