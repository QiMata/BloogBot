using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Reflection;
using Serilog;
using System.Collections.Generic;
using System;
using System.IO;

namespace WoWStateManager.Settings
{
    /// <summary>
    /// Top-level dispatch mode for the StateManager. Determines whether the
    /// service waits for explicit fixture-driven action dispatch (Test),
    /// drives configured bots through their per-character Loadout +
    /// AssignedActivity automatically (Automated), or accepts on-demand
    /// activity requests from human players via Shodan / the WPF UI
    /// (OnDemandActivities).
    ///
    /// The mode is determined at config-load time. See
    /// docs/statemanager_modes_design.md for the full plan.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum StateManagerMode
    {
        /// <summary>
        /// StateManager waits for ActionMessage dispatch from a test fixture.
        /// This is the default for legacy bare-array configs and for every
        /// per-category live-validation config that drives an xUnit test.
        /// </summary>
        Test = 0,

        /// <summary>
        /// At world-entry, StateManager auto-dispatches the configured
        /// CharacterSettings.Loadout (APPLY_LOADOUT) and then parses
        /// CharacterSettings.AssignedActivity to start the configured
        /// activity. Bots self-progress through all stages; tests assert
        /// against snapshot milestones.
        /// </summary>
        Automated = 1,

        /// <summary>
        /// StateManager listens for human-player activity requests
        /// (Shodan whisper commands, WPF UI POST). Same dispatch path as
        /// Automated, but triggered by external request rather than at
        /// world-entry.
        /// </summary>
        OnDemandActivities = 2,
    }

    /// <summary>
    /// Wrapped settings shape used by the new schema. The legacy bare-array
    /// shape is still supported by <see cref="StateManagerSettings.LoadConfig"/>;
    /// when present, it is interpreted as <see cref="StateManagerMode.Test"/>.
    /// </summary>
    internal sealed class WrappedStateManagerSettings
    {
        [JsonProperty("Mode")]
        public StateManagerMode Mode { get; set; } = StateManagerMode.Test;

        [JsonProperty("Characters")]
        public List<CharacterSettings> Characters { get; set; } = [];
    }

    public class StateManagerSettings
    {
        private static StateManagerSettings _instance;
        public static StateManagerSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new StateManagerSettings();
                    _instance.LoadConfig();
                }
                return _instance;
            }
        }

        private void LoadConfig()
        {
            // Allow tests to override the settings file via environment variable
            var overridePath = Environment.GetEnvironmentVariable("WWOW_SETTINGS_OVERRIDE");
            string settingsFilePath;

            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            {
                settingsFilePath = overridePath;
                Log.Information("Loading settings from override: {Path}", settingsFilePath);
            }
            else
            {
                string currentFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                settingsFilePath = Path.Combine(currentFolder, "Settings\\StateManagerSettings.json");
            }

            var raw = File.ReadAllText(settingsFilePath);
            var token = JToken.Parse(raw);

            if (token is JArray)
            {
                // Legacy bare-array shape — interpret as Test mode + that roster.
                Mode = StateManagerMode.Test;
                CharacterSettings = token.ToObject<List<CharacterSettings>>() ?? [];
                Log.Information(
                    "Loaded {Count} character(s) from legacy bare-array config (Mode=Test): {Path}",
                    CharacterSettings.Count,
                    settingsFilePath);
            }
            else if (token is JObject)
            {
                var wrapped = token.ToObject<WrappedStateManagerSettings>()
                    ?? new WrappedStateManagerSettings();
                Mode = wrapped.Mode;
                CharacterSettings = wrapped.Characters ?? [];
                Log.Information(
                    "Loaded {Count} character(s) from wrapped config (Mode={Mode}): {Path}",
                    CharacterSettings.Count,
                    Mode,
                    settingsFilePath);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unexpected JSON root in {settingsFilePath}: must be array (legacy) or object with 'Mode' + 'Characters'.");
            }
        }

        public static void SaveConfig()
        {
            try
            {
                string currentFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string settingsFilePath = Path.Combine(currentFolder, "Settings\\StateManagerSettings.json");
                // Persist using the legacy bare-array shape unless the in-memory
                // Mode is non-default. Keeping Test-mode files as bare arrays
                // avoids touching every existing config; non-Test modes write
                // the wrapped form so the mode survives a round-trip.
                string json;
                if (_instance.Mode == StateManagerMode.Test)
                {
                    json = JsonConvert.SerializeObject(_instance.CharacterSettings, Formatting.Indented);
                }
                else
                {
                    var wrapped = new WrappedStateManagerSettings
                    {
                        Mode = _instance.Mode,
                        Characters = _instance.CharacterSettings,
                    };
                    json = JsonConvert.SerializeObject(wrapped, Formatting.Indented);
                }

                File.WriteAllText(settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving config");
            }
        }

        private StateManagerSettings() { }

        /// <summary>
        /// Resets the singleton instance so the next access to <see cref="Instance"/>
        /// re-reads the settings file from disk. Intended for test isolation only.
        /// </summary>
        public static void ResetInstance()
        {
            _instance = null;
        }

        /// <summary>
        /// Top-level dispatch mode. Defaults to <see cref="StateManagerMode.Test"/>
        /// for legacy bare-array configs. Wrapped configs may override to
        /// <see cref="StateManagerMode.Automated"/> or
        /// <see cref="StateManagerMode.OnDemandActivities"/>.
        /// </summary>
        public StateManagerMode Mode { get; private set; } = StateManagerMode.Test;

        /// <summary>
        /// List of character settings defining which bots to run and how.
        /// </summary>
        public List<CharacterSettings> CharacterSettings { get; private set; } = [];
    }
}
