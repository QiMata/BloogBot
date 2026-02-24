using BotRunner.Constants;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WoWStateManager.Settings
{
    /// <summary>
    /// Specifies which bot runner type to use for a character.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BotRunnerType
    {
        /// <summary>
        /// Foreground bot runner - injects into an existing WoW.exe process via DLL injection.
        /// Use this when you want to hijack an already running WoW client.
        /// </summary>
        Foreground = 0,

        /// <summary>
        /// Background bot runner - runs headless without a WoW client.
        /// Use this for server-side bots that don't need a game client.
        /// </summary>
        Background = 1
    }

    /// <summary>
    /// Extended character definition settings that include bot runner configuration.
    /// This class is used for JSON serialization of character settings.
    /// </summary>
    public class CharacterSettings
    {
        /// <summary>
        /// The account name to use for this character.
        /// </summary>
        [JsonProperty("AccountName")]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>
        /// Openness personality trait (0.0 to 1.0).
        /// </summary>
        [JsonProperty("Openness")]
        public float Openness { get; set; } = 1.0f;

        /// <summary>
        /// Conscientiousness personality trait (0.0 to 1.0).
        /// </summary>
        [JsonProperty("Conscientiousness")]
        public float Conscientiousness { get; set; } = 1.0f;

        /// <summary>
        /// Extraversion personality trait (0.0 to 1.0).
        /// </summary>
        [JsonProperty("Extraversion")]
        public float Extraversion { get; set; } = 1.0f;

        /// <summary>
        /// Agreeableness personality trait (0.0 to 1.0).
        /// </summary>
        [JsonProperty("Agreeableness")]
        public float Agreeableness { get; set; } = 1.0f;

        /// <summary>
        /// Neuroticism personality trait (0.0 to 1.0).
        /// </summary>
        [JsonProperty("Neuroticism")]
        public float Neuroticism { get; set; } = 1.0f;

        /// <summary>
        /// Whether this character should be automatically started.
        /// </summary>
        [JsonProperty("ShouldRun")]
        public bool ShouldRun { get; set; } = true;

        /// <summary>
        /// The type of bot runner to use for this character.
        /// Defaults to Foreground (DLL injection into existing WoW process).
        /// </summary>
        [JsonProperty("RunnerType")]
        public BotRunnerType RunnerType { get; set; } = BotRunnerType.Foreground;

        /// <summary>
        /// Optional: Process ID of an existing WoW.exe to inject into.
        /// If not specified and RunnerType is Foreground, a new WoW process will be launched.
        /// </summary>
        [JsonProperty("TargetProcessId")]
        public int? TargetProcessId { get; set; }

        /// <summary>
        /// Optional bot behavior tuning. If null/omitted, uses defaults.
        /// Controls pull range, rest thresholds, vendor triggers, gathering, etc.
        /// </summary>
        [JsonProperty("BehaviorConfig", NullValueHandling = NullValueHandling.Ignore)]
        public BotBehaviorConfig? BehaviorConfig { get; set; }
    }
}
