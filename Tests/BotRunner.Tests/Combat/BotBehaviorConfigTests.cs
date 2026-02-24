using BotRunner.Constants;

namespace BotRunner.Tests.Combat
{
    public class BotBehaviorConfigTests
    {
        // ======== Default Values ========

        [Fact]
        public void Defaults_CombatValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(80.0f, config.MaxPullRange);
            Assert.Equal(7, config.TargetLevelRangeBelow);
            Assert.Equal(3, config.TargetLevelRangeAbove);
            Assert.Equal(10.0f, config.SocialAggroRange);
            Assert.Equal(15.0f, config.MobDensityPenalty);
            Assert.Equal(2, config.MaxSafeNearbyMobs);
            Assert.Equal(120_000, config.BlacklistDurationMs);
            Assert.Equal(60_000, config.CombatTimeoutMs);
        }

        [Fact]
        public void Defaults_RestValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(50, config.RestHpThresholdPct);
            Assert.Equal(30, config.RestManaThresholdPct);
            Assert.Equal(80, config.RestResumeHpPct);
            Assert.Equal(80, config.RestResumeManaPercent);
        }

        [Fact]
        public void Defaults_PotionValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(30, config.HealthPotionThresholdPct);
            Assert.Equal(15, config.ManaPotionThresholdPct);
            Assert.Equal(120_000, config.PotionCooldownMs);
        }

        [Fact]
        public void Defaults_VendorValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(2, config.BagFullThreshold);
            Assert.Equal(5, config.MinKillsBetweenVendor);
            Assert.Equal(4, config.LowConsumableThreshold);
            Assert.Equal(20, config.FoodPurchaseTarget);
            Assert.Equal(20, config.DrinkPurchaseTarget);
        }

        [Fact]
        public void Defaults_ExplorationValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(40.0f, config.ExploreMinRadius);
            Assert.Equal(80.0f, config.ExploreMaxRadius);
            Assert.Equal(20.0f, config.ExploreRadiusGrowth);
        }

        [Fact]
        public void Defaults_StuckDetectionValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(1.0f, config.StuckDistanceThreshold);
            Assert.Equal(3_000, config.StuckCheckIntervalMs);
            Assert.Equal(3, config.StuckTicksBeforeRecovery);
            Assert.Equal(60_000, config.StuckTimeoutMs);
        }

        [Fact]
        public void Defaults_GatheringValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(40.0f, config.GatherDetectRange);
            Assert.Equal(30.0f, config.FishingPoolDetectRange);
            Assert.Equal(8, config.MaxFishingCasts);
            Assert.Equal(30_000, config.FishingCooldownMs);
            Assert.Equal(120_000, config.CraftCooldownMs);
            Assert.Equal(5, config.CraftMaterialThreshold);
        }

        [Fact]
        public void Defaults_EconomyValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(50.0f, config.AuctioneerDetectRange);
            Assert.Equal(900_000, config.AhVisitCooldownMs);
            Assert.Equal(50.0f, config.MailboxDetectRange);
            Assert.Equal(300_000, config.MailCheckCooldownMs);
            Assert.Equal(50.0f, config.BankerDetectRange);
            Assert.Equal(600_000, config.BankVisitCooldownMs);
            Assert.Equal(4, config.BagSlotsBeforeBanking);
        }

        [Fact]
        public void Defaults_NpcInteractionValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(60.0f, config.FlightMasterDetectRange);
            Assert.Equal(15_000, config.QuestCheckCooldownMs);
            Assert.Equal(5.0f, config.NpcInteractRange);
        }

        [Fact]
        public void Defaults_FollowValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(25.0f, config.FollowRange);
            Assert.Equal(10.0f, config.FollowClose);
        }

        [Fact]
        public void Defaults_BuffConsumableValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(5_000, config.ScrollUseCooldownMs);
            Assert.Equal(8_000, config.BuffConsumableCooldownMs);
        }

        [Fact]
        public void Defaults_MiscValues()
        {
            var config = new BotBehaviorConfig();
            Assert.Equal(300_000, config.StatsLogIntervalMs);
            Assert.Equal(60_000, config.HearthstoneCheckCooldownMs);
            Assert.Equal(20, config.DurabilityRepairThresholdPct);
            Assert.Equal(10_000, config.TalentAllocationCooldownMs);
        }

        // ======== Property Overrides ========

        [Fact]
        public void Properties_CanBeOverridden()
        {
            var config = new BotBehaviorConfig
            {
                MaxPullRange = 50.0f,
                RestHpThresholdPct = 60,
                BagFullThreshold = 4,
                NpcInteractRange = 8.0f
            };

            Assert.Equal(50.0f, config.MaxPullRange);
            Assert.Equal(60, config.RestHpThresholdPct);
            Assert.Equal(4, config.BagFullThreshold);
            Assert.Equal(8.0f, config.NpcInteractRange);
        }

        // ======== Clone ========

        [Fact]
        public void Clone_CopiesAllValues()
        {
            var original = new BotBehaviorConfig
            {
                MaxPullRange = 99.0f,
                TargetLevelRangeBelow = 10,
                RestHpThresholdPct = 70,
                BagFullThreshold = 1,
                ExploreMinRadius = 100.0f,
                StuckDistanceThreshold = 2.5f,
                GatherDetectRange = 60.0f,
                AuctioneerDetectRange = 100.0f,
                FlightMasterDetectRange = 80.0f,
                FollowRange = 30.0f,
                StatsLogIntervalMs = 60_000,
                DurabilityRepairThresholdPct = 10
            };

            var clone = original.Clone();

            Assert.Equal(99.0f, clone.MaxPullRange);
            Assert.Equal(10, clone.TargetLevelRangeBelow);
            Assert.Equal(70, clone.RestHpThresholdPct);
            Assert.Equal(1, clone.BagFullThreshold);
            Assert.Equal(100.0f, clone.ExploreMinRadius);
            Assert.Equal(2.5f, clone.StuckDistanceThreshold);
            Assert.Equal(60.0f, clone.GatherDetectRange);
            Assert.Equal(100.0f, clone.AuctioneerDetectRange);
            Assert.Equal(80.0f, clone.FlightMasterDetectRange);
            Assert.Equal(30.0f, clone.FollowRange);
            Assert.Equal(60_000, clone.StatsLogIntervalMs);
            Assert.Equal(10, clone.DurabilityRepairThresholdPct);
        }

        [Fact]
        public void Clone_IndependentFromOriginal()
        {
            var original = new BotBehaviorConfig { MaxPullRange = 50.0f };
            var clone = original.Clone();

            clone.MaxPullRange = 100.0f;

            Assert.Equal(50.0f, original.MaxPullRange);
            Assert.Equal(100.0f, clone.MaxPullRange);
        }

        [Fact]
        public void Clone_DefaultsPreserved()
        {
            var original = new BotBehaviorConfig();
            var clone = original.Clone();

            Assert.Equal(original.MaxPullRange, clone.MaxPullRange);
            Assert.Equal(original.RestHpThresholdPct, clone.RestHpThresholdPct);
            Assert.Equal(original.PotionCooldownMs, clone.PotionCooldownMs);
            Assert.Equal(original.BagFullThreshold, clone.BagFullThreshold);
            Assert.Equal(original.StuckTimeoutMs, clone.StuckTimeoutMs);
        }

        // ======== Sensibility Checks ========

        [Fact]
        public void Defaults_RestResumeHigherThanThreshold()
        {
            var config = new BotBehaviorConfig();
            Assert.True(config.RestResumeHpPct > config.RestHpThresholdPct,
                "Resume HP should be higher than rest threshold to avoid ping-ponging");
            Assert.True(config.RestResumeManaPercent > config.RestManaThresholdPct,
                "Resume mana should be higher than rest threshold to avoid ping-ponging");
        }

        [Fact]
        public void Defaults_FollowCloseWithinFollowRange()
        {
            var config = new BotBehaviorConfig();
            Assert.True(config.FollowClose < config.FollowRange,
                "Follow-close distance should be less than follow-start distance");
        }

        [Fact]
        public void Defaults_ExploreMinLessThanMax()
        {
            var config = new BotBehaviorConfig();
            Assert.True(config.ExploreMinRadius < config.ExploreMaxRadius,
                "Explore min radius should be less than max radius");
        }

        [Fact]
        public void Defaults_AllTimersPositive()
        {
            var config = new BotBehaviorConfig();
            Assert.True(config.BlacklistDurationMs > 0);
            Assert.True(config.CombatTimeoutMs > 0);
            Assert.True(config.PotionCooldownMs > 0);
            Assert.True(config.StuckCheckIntervalMs > 0);
            Assert.True(config.StuckTimeoutMs > 0);
            Assert.True(config.FishingCooldownMs > 0);
            Assert.True(config.CraftCooldownMs > 0);
            Assert.True(config.AhVisitCooldownMs > 0);
            Assert.True(config.MailCheckCooldownMs > 0);
            Assert.True(config.BankVisitCooldownMs > 0);
            Assert.True(config.QuestCheckCooldownMs > 0);
            Assert.True(config.ScrollUseCooldownMs > 0);
            Assert.True(config.BuffConsumableCooldownMs > 0);
            Assert.True(config.StatsLogIntervalMs > 0);
            Assert.True(config.HearthstoneCheckCooldownMs > 0);
            Assert.True(config.TalentAllocationCooldownMs > 0);
        }

        [Fact]
        public void Defaults_AllRangesPositive()
        {
            var config = new BotBehaviorConfig();
            Assert.True(config.MaxPullRange > 0);
            Assert.True(config.SocialAggroRange > 0);
            Assert.True(config.ExploreMinRadius > 0);
            Assert.True(config.ExploreMaxRadius > 0);
            Assert.True(config.GatherDetectRange > 0);
            Assert.True(config.FishingPoolDetectRange > 0);
            Assert.True(config.AuctioneerDetectRange > 0);
            Assert.True(config.MailboxDetectRange > 0);
            Assert.True(config.BankerDetectRange > 0);
            Assert.True(config.FlightMasterDetectRange > 0);
            Assert.True(config.NpcInteractRange > 0);
            Assert.True(config.FollowRange > 0);
            Assert.True(config.FollowClose > 0);
        }

        [Fact]
        public void Defaults_PercentagesInValidRange()
        {
            var config = new BotBehaviorConfig();
            Assert.InRange(config.RestHpThresholdPct, 1, 100);
            Assert.InRange(config.RestManaThresholdPct, 1, 100);
            Assert.InRange(config.RestResumeHpPct, 1, 100);
            Assert.InRange(config.RestResumeManaPercent, 1, 100);
            Assert.InRange(config.HealthPotionThresholdPct, 1, 100);
            Assert.InRange(config.ManaPotionThresholdPct, 1, 100);
            Assert.InRange(config.DurabilityRepairThresholdPct, 1, 100);
        }
    }
}
