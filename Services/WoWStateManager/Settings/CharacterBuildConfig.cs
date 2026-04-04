using System.Collections.Generic;
using Newtonsoft.Json;

namespace WoWStateManager.Settings
{
    public class CharacterBuildConfig
    {
        /// <summary>
        /// BotProfile spec name (e.g., "WarriorFury", "PriestShadow", "MageArcane").
        /// Must match a BotProfile class name. Null = use default for class.
        /// </summary>
        [JsonProperty("SpecName", NullValueHandling = NullValueHandling.Ignore)]
        public string? SpecName { get; set; }

        /// <summary>
        /// Talent build name from TalentBuildDefinitions. Null = use spec default.
        /// </summary>
        [JsonProperty("TalentBuildName", NullValueHandling = NullValueHandling.Ignore)]
        public string? TalentBuildName { get; set; }

        /// <summary>
        /// BiS gear set: list of target items per slot.
        /// Priority: 1=immediate, 2=next upgrade, 3=eventual BiS.
        /// </summary>
        [JsonProperty("TargetGearSet", NullValueHandling = NullValueHandling.Ignore)]
        public List<GearGoalEntry>? TargetGearSet { get; set; }

        /// <summary>
        /// Reputation goals: faction standings to grind toward.
        /// </summary>
        [JsonProperty("ReputationGoals", NullValueHandling = NullValueHandling.Ignore)]
        public List<ReputationGoalEntry>? ReputationGoals { get; set; }

        /// <summary>
        /// Rare item goals: specific drops, quest rewards, or crafted items to acquire.
        /// </summary>
        [JsonProperty("ItemGoals", NullValueHandling = NullValueHandling.Ignore)]
        public List<ItemGoalEntry>? ItemGoals { get; set; }

        /// <summary>
        /// Mount acquisition goal. Null = no mount goal.
        /// </summary>
        [JsonProperty("MountGoal", NullValueHandling = NullValueHandling.Ignore)]
        public MountGoalEntry? MountGoal { get; set; }

        /// <summary>
        /// Savings goal in copper (e.g., 1000000 = 100g for epic mount).
        /// </summary>
        [JsonProperty("GoldTargetCopper")]
        public int GoldTargetCopper { get; set; }

        /// <summary>
        /// Ordered list of profession training targets (e.g., "Mining:300", "Engineering:300").
        /// </summary>
        [JsonProperty("SkillPriorities")]
        public List<string> SkillPriorities { get; set; } = [];

        /// <summary>
        /// Quest chains to complete by ID (e.g., "OnyxiaAttunement", "MoltenCoreAttunement").
        /// </summary>
        [JsonProperty("QuestChains")]
        public List<string> QuestChains { get; set; } = [];
    }

    /// <summary>
    /// Target gear item for a specific equipment slot.
    /// Source: "Dungeon:StratholmeBaron", "Quest:InMyHour", "Vendor:OrgWeaponsmith", "Craft:ArcaniteReaper", "AH".
    /// </summary>
    public class GearGoalEntry
    {
        [JsonProperty("Slot")] public string Slot { get; set; } = "";
        [JsonProperty("ItemId")] public int ItemId { get; set; }
        [JsonProperty("ItemName")] public string ItemName { get; set; } = "";
        [JsonProperty("Source")] public string Source { get; set; } = "";
        [JsonProperty("Priority")] public int Priority { get; set; } = 2;
    }

    /// <summary>
    /// Reputation standing goal for a faction.
    /// GrindMethod: "Quests", "Dungeon:Stratholme", "Turnin:RuneclothBandage", "Mob:TimbermawFurbolg".
    /// </summary>
    public class ReputationGoalEntry
    {
        [JsonProperty("FactionId")] public int FactionId { get; set; }
        [JsonProperty("FactionName")] public string FactionName { get; set; } = "";
        [JsonProperty("TargetStanding")] public string TargetStanding { get; set; } = "Exalted";
        [JsonProperty("GrindMethod")] public string GrindMethod { get; set; } = "Quests";
    }

    /// <summary>
    /// Rare item acquisition goal.
    /// Source: "Boss:Stratholme:BaronRivendare", "Quest:SunkenTemple", "Craft:Arcanite".
    /// </summary>
    public class ItemGoalEntry
    {
        [JsonProperty("ItemId")] public int ItemId { get; set; }
        [JsonProperty("ItemName")] public string ItemName { get; set; } = "";
        [JsonProperty("Source")] public string Source { get; set; } = "";
        [JsonProperty("Quantity")] public int Quantity { get; set; } = 1;
        [JsonProperty("IsMount")] public bool IsMount { get; set; }
        [JsonProperty("EstimatedDropRate")] public float EstimatedDropRate { get; set; }
    }

    /// <summary>
    /// Mount acquisition goal.
    /// Type: "BasicMount" (level 40, 100% speed), "EpicMount" (level 60, 60% speed), "RareDrop".
    /// </summary>
    public class MountGoalEntry
    {
        [JsonProperty("Type")] public string Type { get; set; } = "BasicMount";
        [JsonProperty("TargetItemId")] public int? TargetItemId { get; set; }
        [JsonProperty("GoldCostCopper")] public int GoldCostCopper { get; set; }
        [JsonProperty("RequiredLevel")] public int RequiredLevel { get; set; } = 40;
        [JsonProperty("RequiredRidingSkill")] public int RequiredRidingSkill { get; set; } = 75;
    }
}
