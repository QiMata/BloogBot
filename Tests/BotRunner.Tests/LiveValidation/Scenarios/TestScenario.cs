using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BotRunner.Tests.LiveValidation.Scenarios;

/// <summary>
/// Declarative test scenario definition. Each test loads a JSON file describing
/// the entire setup → action → validation pipeline. The TestScenarioRunner
/// executes it through LiveBotFixture.
///
/// Example JSON:
/// {
///   "name": "Mining_BG_CopperVein",
///   "description": "BG bot mines copper ore in Valley of Trials",
///   "settings": null,
///   "enableCoordinator": false,
///   "minBots": 1,
///   "botEntryTimeoutSeconds": 30,
///   "setup": [
///     {
///       "account": "$BG",
///       "cleanSlate": true,
///       "teleport": { "mapId": 1, "x": -800, "y": -4500, "z": 34 },
///       "learnSpells": [2575],
///       "addItems": [{ "id": 2901, "count": 1 }],
///       "setSkills": [{ "id": 186, "current": 1, "max": 300 }],
///       "gmCommands": [".character level 8"]
///     }
///   ],
///   "action": {
///     "account": "$BG",
///     "type": "StartGatheringRoute",
///     "parameters": [
///       { "int": 2575 },
///       { "string": "1731" },
///       { "int": 1 }
///     ]
///   },
///   "observe": {
///     "timeoutSeconds": 120,
///     "pollIntervalMs": 3000,
///     "logIntervalSeconds": 15
///   },
///   "assertions": {
///     "snapshotConditions": [
///       { "account": "$BG", "field": "CurrentAction.ActionType", "equals": "StartGatheringRoute" }
///     ]
///   }
/// }
/// </summary>
public class TestScenario
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    /// <summary>
    /// Optional path to a custom StateManagerSettings file (e.g. "RagefireChasm.settings.json").
    /// If null, uses the default fixture settings.
    /// </summary>
    [JsonPropertyName("settings")]
    public string? Settings { get; set; }

    /// <summary>
    /// Whether to enable the DungeoneeringCoordinator for this scenario.
    /// Default false (coordinator disabled via WWOW_TEST_DISABLE_COORDINATOR).
    /// </summary>
    [JsonPropertyName("enableCoordinator")]
    public bool EnableCoordinator { get; set; }

    /// <summary>
    /// Minimum number of bots that must be in-world before setup begins.
    /// </summary>
    [JsonPropertyName("minBots")]
    public int MinBots { get; set; } = 1;

    /// <summary>
    /// Seconds to wait for minBots to enter world.
    /// </summary>
    [JsonPropertyName("botEntryTimeoutSeconds")]
    public int BotEntryTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Per-bot setup steps. Each entry configures one bot.
    /// Use "$BG" for the default background bot, "$FG" for foreground,
    /// or an explicit account name like "TESTBOT1".
    /// </summary>
    [JsonPropertyName("setup")]
    public List<BotSetup> Setup { get; set; } = new();

    /// <summary>
    /// The action to dispatch after setup. Null if the scenario is coordinator-driven.
    /// </summary>
    [JsonPropertyName("action")]
    public ScenarioAction? Action { get; set; }

    /// <summary>
    /// Observation/polling configuration for the test phase.
    /// </summary>
    [JsonPropertyName("observe")]
    public ObserveConfig Observe { get; set; } = new();

    /// <summary>
    /// Assertions to validate after the observation phase.
    /// </summary>
    [JsonPropertyName("assertions")]
    public ScenarioAssertions Assertions { get; set; } = new();
}

public class BotSetup
{
    /// <summary>
    /// Account identifier. Special values: "$BG" = default BG bot, "$FG" = default FG bot.
    /// Otherwise use explicit account names (e.g. "TESTBOT1", "RFCBOT2").
    /// </summary>
    [JsonPropertyName("account")]
    public string Account { get; set; } = "$BG";

    /// <summary>
    /// If true, runs EnsureCleanSlateAsync (revive if dead, teleport to Org safe zone).
    /// </summary>
    [JsonPropertyName("cleanSlate")]
    public bool CleanSlate { get; set; } = true;

    /// <summary>
    /// Optional teleport before other setup. Null = stay where you are.
    /// </summary>
    [JsonPropertyName("teleport")]
    public TeleportSpec? Teleport { get; set; }

    /// <summary>
    /// Spell IDs to learn via .learn GM command.
    /// </summary>
    [JsonPropertyName("learnSpells")]
    public List<int> LearnSpells { get; set; } = new();

    /// <summary>
    /// Items to add via .additem GM command.
    /// </summary>
    [JsonPropertyName("addItems")]
    public List<ItemSpec> AddItems { get; set; } = new();

    /// <summary>
    /// Skills to set via .setskill GM command.
    /// </summary>
    [JsonPropertyName("setSkills")]
    public List<SkillSpec> SetSkills { get; set; } = new();

    /// <summary>
    /// Raw GM commands to execute via bot chat (e.g. ".character level 8", ".quest add 4641").
    /// </summary>
    [JsonPropertyName("gmCommands")]
    public List<string> GmCommands { get; set; } = new();

    /// <summary>
    /// If true, clear all inventory before adding items.
    /// </summary>
    [JsonPropertyName("clearInventory")]
    public bool ClearInventory { get; set; }
}

public class TeleportSpec
{
    [JsonPropertyName("mapId")]
    public int MapId { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }
}

public class ItemSpec
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;
}

public class SkillSpec
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("current")]
    public int Current { get; set; }

    [JsonPropertyName("max")]
    public int Max { get; set; }
}

public class ScenarioAction
{
    /// <summary>
    /// Account to send the action to. "$BG", "$FG", or explicit.
    /// </summary>
    [JsonPropertyName("account")]
    public string Account { get; set; } = "$BG";

    /// <summary>
    /// ActionType name (must match Communication.ActionType enum).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>
    /// Parameters for the action. Each can have int, float, long, or string.
    /// </summary>
    [JsonPropertyName("parameters")]
    public List<ActionParam> Parameters { get; set; } = new();
}

public class ActionParam
{
    [JsonPropertyName("int")]
    public int? IntValue { get; set; }

    [JsonPropertyName("float")]
    public float? FloatValue { get; set; }

    [JsonPropertyName("long")]
    public long? LongValue { get; set; }

    [JsonPropertyName("string")]
    public string? StringValue { get; set; }
}

public class ObserveConfig
{
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 120;

    [JsonPropertyName("pollIntervalMs")]
    public int PollIntervalMs { get; set; } = 3000;

    [JsonPropertyName("logIntervalSeconds")]
    public int LogIntervalSeconds { get; set; } = 15;
}

public class ScenarioAssertions
{
    /// <summary>
    /// If true, at least 2 bots must report PartyLeaderGuid != 0.
    /// </summary>
    [JsonPropertyName("groupFormed")]
    public bool? GroupFormed { get; set; }

    /// <summary>
    /// If true, at least 1 bot must have CurrentAction.ActionType == StartDungeoneering.
    /// </summary>
    [JsonPropertyName("dungeoneeringDispatched")]
    public bool? DungeoneeringDispatched { get; set; }

    /// <summary>
    /// If true, at least 1 bot must have UNIT_FLAG_IN_COMBAT during observation.
    /// </summary>
    [JsonPropertyName("combatSeen")]
    public bool? CombatSeen { get; set; }

    /// <summary>
    /// Assert N+ bots are on a specific map.
    /// </summary>
    [JsonPropertyName("minBotsOnMap")]
    public MapCountAssertion? MinBotsOnMap { get; set; }

    /// <summary>
    /// Assert a specific bot's action type during observation.
    /// </summary>
    [JsonPropertyName("actionTypeSeen")]
    public string? ActionTypeSeen { get; set; }

    /// <summary>
    /// Account-specific snapshot field assertions.
    /// </summary>
    [JsonPropertyName("snapshotConditions")]
    public List<SnapshotCondition> SnapshotConditions { get; set; } = new();
}

public class MapCountAssertion
{
    [JsonPropertyName("mapId")]
    public int MapId { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;
}

public class SnapshotCondition
{
    [JsonPropertyName("account")]
    public string Account { get; set; } = "$BG";

    /// <summary>
    /// Dot-separated field path on WoWActivitySnapshot (e.g. "ScreenState", "Player.Unit.Health").
    /// </summary>
    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("equals")]
    public string? Equals { get; set; }

    [JsonPropertyName("greaterThan")]
    public double? GreaterThan { get; set; }

    [JsonPropertyName("notEquals")]
    public string? NotEquals { get; set; }
}
