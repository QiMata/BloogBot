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
}
