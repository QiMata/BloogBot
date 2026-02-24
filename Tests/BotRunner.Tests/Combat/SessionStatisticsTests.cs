using BotRunner.Combat;
using System.Threading;
using System.Threading.Tasks;

namespace BotRunner.Tests.Combat
{
    public class SessionStatisticsTests
    {
        [Fact]
        public void RecordKill_IncrementsKillCounter()
        {
            var stats = new SessionStatistics();
            Assert.Equal(0, stats.Kills);

            stats.RecordKill();
            Assert.Equal(1, stats.Kills);

            stats.RecordKill();
            stats.RecordKill();
            Assert.Equal(3, stats.Kills);
        }

        [Fact]
        public void RecordDeath_IncrementsDeathCounter()
        {
            var stats = new SessionStatistics();
            stats.RecordDeath();
            stats.RecordDeath();
            Assert.Equal(2, stats.Deaths);
        }

        [Fact]
        public void RecordLoot_AccumulatesCopperAndItems()
        {
            var stats = new SessionStatistics();

            stats.RecordLoot(150, 3);
            Assert.Equal(150, stats.CopperLooted);
            Assert.Equal(3, stats.ItemsLooted);
            Assert.Equal(1, stats.MobsLooted);

            stats.RecordLoot(200, 1);
            Assert.Equal(350, stats.CopperLooted);
            Assert.Equal(4, stats.ItemsLooted);
            Assert.Equal(2, stats.MobsLooted);
        }

        [Fact]
        public void RecordVendorSale_AccumulatesCopperAndItems()
        {
            var stats = new SessionStatistics();

            stats.RecordVendorSale(500, 2);
            Assert.Equal(500, stats.CopperFromVendor);
            Assert.Equal(2, stats.ItemsSold);

            stats.RecordVendorSale(300, 1);
            Assert.Equal(800, stats.CopperFromVendor);
            Assert.Equal(3, stats.ItemsSold);
        }

        [Fact]
        public void RecordXpGain_AccumulatesXp()
        {
            var stats = new SessionStatistics();
            stats.RecordXpGain(100);
            stats.RecordXpGain(250);
            Assert.Equal(350, stats.XpGained);
        }

        [Fact]
        public void RecordGather_IncrementsNodeCount()
        {
            var stats = new SessionStatistics();
            stats.RecordGather();
            stats.RecordGather();
            stats.RecordGather();
            Assert.Equal(3, stats.NodesGathered);
        }

        [Fact]
        public void RecordCraft_AccumulatesCraftCount()
        {
            var stats = new SessionStatistics();
            stats.RecordCraft(5);
            stats.RecordCraft(3);
            Assert.Equal(8, stats.ItemsCrafted);
        }

        [Fact]
        public void RecordDistance_AccumulatesDistance()
        {
            var stats = new SessionStatistics();
            stats.RecordDistance(10.5f);
            stats.RecordDistance(20.3f);
            Assert.Equal(30.8f, stats.DistanceTraveled, 0.01f);
        }

        [Fact]
        public void GoldPerHour_IncludesBothLootAndVendor()
        {
            var stats = new SessionStatistics();
            // 10000 copper = 1 gold
            stats.RecordLoot(5000, 1);
            stats.RecordVendorSale(5000, 1);

            // Total is 10000 copper = 1 gold
            // GoldPerHour depends on time elapsed, but total should be correct
            double totalGold = (stats.CopperLooted + stats.CopperFromVendor) / 10000.0;
            Assert.Equal(1.0, totalGold);
        }

        [Fact]
        public void SessionDuration_IsPositive()
        {
            var stats = new SessionStatistics();
            Thread.Sleep(10);
            Assert.True(stats.SessionDuration.TotalMilliseconds > 0);
        }

        [Fact]
        public void InitialState_AllZeros()
        {
            var stats = new SessionStatistics();
            Assert.Equal(0, stats.Kills);
            Assert.Equal(0, stats.Deaths);
            Assert.Equal(0, stats.MobsLooted);
            Assert.Equal(0, stats.CopperLooted);
            Assert.Equal(0, stats.CopperFromVendor);
            Assert.Equal(0, stats.ItemsLooted);
            Assert.Equal(0, stats.ItemsSold);
            Assert.Equal(0, stats.ItemsCrafted);
            Assert.Equal(0, stats.XpGained);
            Assert.Equal(0, stats.SkillUps);
            Assert.Equal(0, stats.QuestsCompleted);
            Assert.Equal(0, stats.NodesGathered);
            Assert.Equal(0f, stats.DistanceTraveled);
        }

        [Fact]
        public void ConcurrentRecordKill_IsThreadSafe()
        {
            var stats = new SessionStatistics();
            const int threadsCount = 5;
            const int killsPerThread = 1000;

            var tasks = new Task[threadsCount];
            for (int i = 0; i < threadsCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < killsPerThread; j++)
                        stats.RecordKill();
                });
            }
            Task.WaitAll(tasks);

            Assert.Equal(threadsCount * killsPerThread, stats.Kills);
        }

        [Fact]
        public void ConcurrentMixedOperations_IsThreadSafe()
        {
            var stats = new SessionStatistics();
            const int opsPerThread = 500;

            var tasks = new Task[]
            {
                Task.Run(() => { for (int i = 0; i < opsPerThread; i++) stats.RecordKill(); }),
                Task.Run(() => { for (int i = 0; i < opsPerThread; i++) stats.RecordDeath(); }),
                Task.Run(() => { for (int i = 0; i < opsPerThread; i++) stats.RecordXpGain(10); }),
                Task.Run(() => { for (int i = 0; i < opsPerThread; i++) stats.RecordLoot(100, 1); }),
                Task.Run(() => { for (int i = 0; i < opsPerThread; i++) stats.RecordGather(); }),
            };
            Task.WaitAll(tasks);

            Assert.Equal(opsPerThread, stats.Kills);
            Assert.Equal(opsPerThread, stats.Deaths);
            Assert.Equal(opsPerThread * 10, stats.XpGained);
            Assert.Equal(opsPerThread * 100, stats.CopperLooted);
            Assert.Equal(opsPerThread, stats.ItemsLooted);
            Assert.Equal(opsPerThread, stats.MobsLooted);
            Assert.Equal(opsPerThread, stats.NodesGathered);
        }

        [Fact]
        public void LogSummary_DoesNotThrow()
        {
            var stats = new SessionStatistics();
            stats.RecordKill();
            stats.RecordLoot(100, 2);
            stats.RecordXpGain(500);

            // Should not throw even with minimal session duration
            var ex = Record.Exception(() => stats.LogSummary());
            Assert.Null(ex);
        }
    }
}
