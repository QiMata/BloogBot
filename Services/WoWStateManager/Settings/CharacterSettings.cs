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
        /// GM level for this account (0=player, 6=full admin).
        /// Controls command access (e.g. .go xyz, .learn). It does NOT toggle in-game
        /// runtime GM mode; live tests rely on account-level GM access only.
        /// </summary>
        [JsonProperty("GmLevel")]
        public int GmLevel { get; set; } = 6;

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

        /// <summary>
        /// Optional: Character class override. If set, used instead of parsing class from AccountName.
        /// Allows account names like "TESTBOT1" that don't encode race/class.
        /// </summary>
        [JsonProperty("CharacterClass", NullValueHandling = NullValueHandling.Ignore)]
        public string? CharacterClass { get; set; }

        /// <summary>
        /// Optional: Character race override. If set, used instead of parsing race from AccountName.
        /// </summary>
        [JsonProperty("CharacterRace", NullValueHandling = NullValueHandling.Ignore)]
        public string? CharacterRace { get; set; }

        /// <summary>
        /// Optional: Character gender override ("Male" or "Female").
        /// If set, used instead of the class-based default from WoWNameGenerator.DetermineGender().
        /// CRITICAL for parity tests: FG and BG characters must share the same gender
        /// so capsule dimensions match (race+gender determines capsule radius/height).
        /// </summary>
        [JsonProperty("CharacterGender", NullValueHandling = NullValueHandling.Ignore)]
        public string? CharacterGender { get; set; }

        /// <summary>
        /// Optional character-name seed offset used when the default generated name
        /// is already taken. Bot runners add this offset before applying retry suffixes.
        /// </summary>
        [JsonProperty("CharacterNameAttemptOffset", NullValueHandling = NullValueHandling.Ignore)]
        public int? CharacterNameAttemptOffset { get; set; }

        /// <summary>
        /// Optional explicit character name. When set, the bot uses this exact name
        /// during character creation instead of the syllable-based generator. Required
        /// for characters that must be addressable by humans (e.g. the Shodan GM
        /// liaison). Falls back to <see cref="WoWNameGenerator.GenerateName"/> when
        /// null/empty so randomized accounts keep their existing behaviour.
        /// </summary>
        [JsonProperty("CharacterName", NullValueHandling = NullValueHandling.Ignore)]
        public string? CharacterName { get; set; }

        /// <summary>
        /// Optional build configuration: spec selection, talent build, gold target, professions, quests.
        /// If null/omitted, uses default spec for the character's class.
        /// </summary>
        [JsonProperty("BuildConfig", NullValueHandling = NullValueHandling.Ignore)]
        public CharacterBuildConfig? BuildConfig { get; set; }

        /// <summary>
        /// P3: Full per-character "become raid-ready" specification. When present,
        /// StateManager hands this off to BotRunner exactly once via
        /// <c>ActionType.APPLY_LOADOUT</c>. BotRunner then executes the plan at
        /// its own pace and reports completion through
        /// <c>WoWActivitySnapshot.LoadoutStatus</c>. Null means "no loadout hand-off
        /// is configured for this bot"; fixtures may still drive prep manually
        /// during the P3 rollout.
        /// </summary>
        [JsonProperty("Loadout", NullValueHandling = NullValueHandling.Ignore)]
        public LoadoutSpecSettings? Loadout { get; set; }

        /// <summary>
        /// When true, the bot's runner is permitted to issue GM chat commands
        /// (<c>.tele name</c>, <c>.additem</c>, <c>.learn</c>, <c>.setskill</c>, etc.)
        /// as part of executing an <see cref="AssignedActivity"/>. This is the
        /// shortcut-path for tests and authoring environments where we want to
        /// "force" an activity without requiring the bot to travel and train
        /// naturally. Defaults to false; production/long-running bots should
        /// leave this off so the DecisionEngine drives real sub-objectives.
        /// </summary>
        [JsonProperty("UseGmCommands")]
        public bool UseGmCommands { get; set; } = false;

        /// <summary>
        /// Optional activity descriptor this character is currently assigned to.
        /// Parsed by <c>BotRunner.Activities.ActivityParser</c> into a concrete
        /// <c>IActivity</c> at world-entry. Supported values are activity name +
        /// optional location in brackets, e.g. <c>"Fishing[Ratchet]"</c>,
        /// <c>"Battleground[WSG]"</c>, <c>"Dungeon[RFC]"</c>. Unknown activities
        /// log a warning and fall through to the default idle sequence. Null
        /// means "no assigned activity — idle until an action is dispatched".
        /// </summary>
        [JsonProperty("AssignedActivity", NullValueHandling = NullValueHandling.Ignore)]
        public string? AssignedActivity { get; set; }
    }

    /// <summary>
    /// P3: JSON-serializable mirror of the <c>Communication.LoadoutSpec</c> proto.
    /// Kept as a plain POCO here so battleground config files stay human-editable;
    /// the fixture translates this into the proto form before enqueuing the
    /// <c>APPLY_LOADOUT</c> action.
    /// </summary>
    public sealed class LoadoutSpecSettings
    {
        [JsonProperty("TargetLevel", NullValueHandling = NullValueHandling.Ignore)]
        public uint TargetLevel { get; set; }

        [JsonProperty("HonorRank", NullValueHandling = NullValueHandling.Ignore)]
        public uint HonorRank { get; set; }

        [JsonProperty("RidingSkill", NullValueHandling = NullValueHandling.Ignore)]
        public uint RidingSkill { get; set; }

        [JsonProperty("MountSpellId", NullValueHandling = NullValueHandling.Ignore)]
        public uint MountSpellId { get; set; }

        [JsonProperty("ArmorSetId", NullValueHandling = NullValueHandling.Ignore)]
        public uint ArmorSetId { get; set; }

        [JsonProperty("SpellIdsToLearn", NullValueHandling = NullValueHandling.Ignore)]
        public uint[]? SpellIdsToLearn { get; set; }

        [JsonProperty("Skills", NullValueHandling = NullValueHandling.Ignore)]
        public LoadoutSkillValueSettings[]? Skills { get; set; }

        [JsonProperty("EquipItems", NullValueHandling = NullValueHandling.Ignore)]
        public LoadoutEquipItemSettings[]? EquipItems { get; set; }

        [JsonProperty("SupplementalItemIds", NullValueHandling = NullValueHandling.Ignore)]
        public uint[]? SupplementalItemIds { get; set; }

        [JsonProperty("ElixirItemIds", NullValueHandling = NullValueHandling.Ignore)]
        public uint[]? ElixirItemIds { get; set; }

        [JsonProperty("FactionReps", NullValueHandling = NullValueHandling.Ignore)]
        public LoadoutFactionRepSettings[]? FactionReps { get; set; }

        [JsonProperty("CompletedQuestIds", NullValueHandling = NullValueHandling.Ignore)]
        public uint[]? CompletedQuestIds { get; set; }

        [JsonProperty("TalentTemplate", NullValueHandling = NullValueHandling.Ignore)]
        public string? TalentTemplate { get; set; }
    }

    public sealed class LoadoutEquipItemSettings
    {
        [JsonProperty("ItemId")] public uint ItemId { get; set; }
        [JsonProperty("InventorySlot", NullValueHandling = NullValueHandling.Ignore)]
        public uint InventorySlot { get; set; }
    }

    public sealed class LoadoutSkillValueSettings
    {
        [JsonProperty("SkillId")] public uint SkillId { get; set; }
        [JsonProperty("Value")] public uint Value { get; set; }
        [JsonProperty("Max", NullValueHandling = NullValueHandling.Ignore)]
        public uint Max { get; set; }
    }

    public sealed class LoadoutFactionRepSettings
    {
        [JsonProperty("FactionId")] public uint FactionId { get; set; }
        [JsonProperty("Standing")] public int Standing { get; set; }
    }
}
