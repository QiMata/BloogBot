using System.Text.Json.Serialization;

namespace BotRunner.Constants;

/// <summary>
/// Per-bot behavior configuration. All values have sensible defaults matching
/// the original hardcoded constants. Can be overridden per-character via settings JSON.
/// </summary>
public class BotBehaviorConfig
{
    // ── Combat ──────────────────────────────────────────────
    /// <summary>Max yards to pull a target from.</summary>
    public float MaxPullRange { get; set; } = 80.0f;

    /// <summary>Levels below player to consider for targeting (positive = lower level).</summary>
    public int TargetLevelRangeBelow { get; set; } = 7;

    /// <summary>Levels above player to consider for targeting.</summary>
    public int TargetLevelRangeAbove { get; set; } = 3;

    /// <summary>Yards within which nearby mobs count as a pack.</summary>
    public float SocialAggroRange { get; set; } = 10.0f;

    /// <summary>Yard penalty applied per nearby mob when scoring targets.</summary>
    public float MobDensityPenalty { get; set; } = 15.0f;

    /// <summary>Max mobs in a pack before the target is deprioritized.</summary>
    public int MaxSafeNearbyMobs { get; set; } = 2;

    /// <summary>Seconds to blacklist a mob after failed engagement.</summary>
    public int BlacklistDurationMs { get; set; } = 120_000;

    /// <summary>Combat timeout before giving up (ms).</summary>
    public int CombatTimeoutMs { get; set; } = 60_000;

    // ── Rest ────────────────────────────────────────────────
    /// <summary>HP percentage below which the bot should rest.</summary>
    public int RestHpThresholdPct { get; set; } = 50;

    /// <summary>Mana percentage below which the bot should rest (mana classes).</summary>
    public int RestManaThresholdPct { get; set; } = 30;

    /// <summary>HP percentage above which the bot resumes fighting.</summary>
    public int RestResumeHpPct { get; set; } = 80;

    /// <summary>Mana percentage above which the bot resumes fighting.</summary>
    public int RestResumeManaPercent { get; set; } = 80;

    // ── Potion ──────────────────────────────────────────────
    /// <summary>HP percentage below which to use a health potion in combat.</summary>
    public int HealthPotionThresholdPct { get; set; } = 30;

    /// <summary>Mana percentage below which to use a mana potion in combat.</summary>
    public int ManaPotionThresholdPct { get; set; } = 15;

    /// <summary>Shared potion cooldown (ms).</summary>
    public int PotionCooldownMs { get; set; } = 120_000;

    // ── Vendor ──────────────────────────────────────────────
    /// <summary>Free bag slots at or below which to visit vendor.</summary>
    public int BagFullThreshold { get; set; } = 2;

    /// <summary>Minimum kills before considering another vendor visit.</summary>
    public int MinKillsBetweenVendor { get; set; } = 5;

    /// <summary>Low consumable count triggering vendor visit.</summary>
    public int LowConsumableThreshold { get; set; } = 4;

    /// <summary>Target food count to purchase at vendor.</summary>
    public int FoodPurchaseTarget { get; set; } = 20;

    /// <summary>Target drink count to purchase at vendor.</summary>
    public int DrinkPurchaseTarget { get; set; } = 20;

    // ── Trainer ─────────────────────────────────────────────
    // Trainer visits are triggered by level-up; no tunable needed.

    // ── Exploration ─────────────────────────────────────────
    /// <summary>Initial minimum explore radius (yards).</summary>
    public float ExploreMinRadius { get; set; } = 40.0f;

    /// <summary>Initial maximum explore radius (yards).</summary>
    public float ExploreMaxRadius { get; set; } = 80.0f;

    /// <summary>Yards added to explore radius each leg expansion.</summary>
    public float ExploreRadiusGrowth { get; set; } = 20.0f;

    // ── Stuck Detection ─────────────────────────────────────
    /// <summary>Minimum movement (yards) over check interval to not count as stuck.</summary>
    public float StuckDistanceThreshold { get; set; } = 1.0f;

    /// <summary>Interval between stuck checks (ms).</summary>
    public int StuckCheckIntervalMs { get; set; } = 3_000;

    /// <summary>Consecutive stuck ticks before recovery action.</summary>
    public int StuckTicksBeforeRecovery { get; set; } = 3;

    /// <summary>General state timeout before forced reset (ms).</summary>
    public int StuckTimeoutMs { get; set; } = 60_000;

    // ── Gathering ───────────────────────────────────────────
    /// <summary>Detection range for herb/ore nodes (yards).</summary>
    public float GatherDetectRange { get; set; } = 40.0f;

    /// <summary>Detection range for fishing pools (yards).</summary>
    public float FishingPoolDetectRange { get; set; } = 30.0f;

    /// <summary>Max casts at a single fishing pool before moving on.</summary>
    public int MaxFishingCasts { get; set; } = 8;

    /// <summary>Fishing cooldown between sessions (ms).</summary>
    public int FishingCooldownMs { get; set; } = 30_000;

    /// <summary>Crafting cooldown between sessions (ms).</summary>
    public int CraftCooldownMs { get; set; } = 120_000;

    /// <summary>Minimum material items before crafting.</summary>
    public int CraftMaterialThreshold { get; set; } = 5;

    // ── Economy ─────────────────────────────────────────────
    /// <summary>Detection range for auctioneers (yards).</summary>
    public float AuctioneerDetectRange { get; set; } = 50.0f;

    /// <summary>Auction house visit cooldown (ms).</summary>
    public int AhVisitCooldownMs { get; set; } = 900_000;

    /// <summary>Detection range for mailboxes (yards).</summary>
    public float MailboxDetectRange { get; set; } = 50.0f;

    /// <summary>Mail check cooldown (ms).</summary>
    public int MailCheckCooldownMs { get; set; } = 300_000;

    /// <summary>Detection range for bankers (yards).</summary>
    public float BankerDetectRange { get; set; } = 50.0f;

    /// <summary>Bank visit cooldown (ms).</summary>
    public int BankVisitCooldownMs { get; set; } = 600_000;

    /// <summary>Free bag slots threshold for banking.</summary>
    public int BagSlotsBeforeBanking { get; set; } = 4;

    // ── NPC Interaction ─────────────────────────────────────
    /// <summary>Flight master detection range (yards).</summary>
    public float FlightMasterDetectRange { get; set; } = 60.0f;

    /// <summary>Quest frame check cooldown (ms).</summary>
    public int QuestCheckCooldownMs { get; set; } = 15_000;

    /// <summary>NPC interaction range (yards).</summary>
    public float NpcInteractRange { get; set; } = 5.0f;

    // ── Follow ──────────────────────────────────────────────
    /// <summary>Distance from leader before following (yards).</summary>
    public float FollowRange { get; set; } = 25.0f;

    /// <summary>Distance from leader to stop following (yards).</summary>
    public float FollowClose { get; set; } = 10.0f;

    // ── Buff Consumables ────────────────────────────────────
    /// <summary>Cooldown between scroll uses (ms).</summary>
    public int ScrollUseCooldownMs { get; set; } = 5_000;

    /// <summary>Cooldown between buff food/elixir uses (ms).</summary>
    public int BuffConsumableCooldownMs { get; set; } = 8_000;

    // ── Stats Logging ───────────────────────────────────────
    /// <summary>Interval between session stats log lines (ms).</summary>
    public int StatsLogIntervalMs { get; set; } = 300_000;

    // ── Hearthstone ─────────────────────────────────────────
    /// <summary>Hearthstone check cooldown (ms).</summary>
    public int HearthstoneCheckCooldownMs { get; set; } = 60_000;

    // ── Durability ──────────────────────────────────────────
    /// <summary>Equipment durability % below which to visit repair vendor.</summary>
    public int DurabilityRepairThresholdPct { get; set; } = 20;

    // ── Talent ──────────────────────────────────────────────
    /// <summary>Cooldown between talent allocation attempts (ms).</summary>
    public int TalentAllocationCooldownMs { get; set; } = 10_000;

    /// <summary>Creates a deep copy of this config.</summary>
    public BotBehaviorConfig Clone() => (BotBehaviorConfig)MemberwiseClone();
}
