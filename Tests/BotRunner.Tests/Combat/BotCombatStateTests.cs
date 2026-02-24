using BotRunner.Combat;

namespace BotRunner.Tests.Combat
{
    public class BotCombatStateTests
    {
        [Fact]
        public void InitialState_NoTarget()
        {
            var state = new BotCombatState();
            Assert.Null(state.CurrentTargetGuid);
        }

        [Fact]
        public void SetCurrentTarget_SetsGuid()
        {
            var state = new BotCombatState();
            state.SetCurrentTarget(42);
            Assert.Equal(42ul, state.CurrentTargetGuid);
        }

        [Fact]
        public void SetCurrentTarget_OverwritesPrevious()
        {
            var state = new BotCombatState();
            state.SetCurrentTarget(1);
            state.SetCurrentTarget(2);
            Assert.Equal(2ul, state.CurrentTargetGuid);
        }

        [Fact]
        public void ClearCurrentTarget_MatchingGuid_ClearsTarget()
        {
            var state = new BotCombatState();
            state.SetCurrentTarget(42);
            state.ClearCurrentTarget(42);
            Assert.Null(state.CurrentTargetGuid);
        }

        [Fact]
        public void ClearCurrentTarget_NonMatchingGuid_DoesNotClear()
        {
            var state = new BotCombatState();
            state.SetCurrentTarget(42);
            state.ClearCurrentTarget(99);
            Assert.Equal(42ul, state.CurrentTargetGuid);
        }

        [Fact]
        public void HasLooted_InitiallyFalse()
        {
            var state = new BotCombatState();
            Assert.False(state.HasLooted(42));
        }

        [Fact]
        public void TryMarkLooted_FirstTime_ReturnsTrue()
        {
            var state = new BotCombatState();
            Assert.True(state.TryMarkLooted(42));
        }

        [Fact]
        public void TryMarkLooted_SecondTime_ReturnsFalse()
        {
            var state = new BotCombatState();
            state.TryMarkLooted(42);
            Assert.False(state.TryMarkLooted(42));
        }

        [Fact]
        public void TryMarkLooted_SetsHasLooted()
        {
            var state = new BotCombatState();
            state.TryMarkLooted(42);
            Assert.True(state.HasLooted(42));
        }

        [Fact]
        public void TryMarkLooted_ClearsCurrentTarget_WhenMatch()
        {
            var state = new BotCombatState();
            state.SetCurrentTarget(42);
            state.TryMarkLooted(42);
            Assert.Null(state.CurrentTargetGuid);
        }

        [Fact]
        public void TryMarkLooted_KeepsCurrentTarget_WhenNoMatch()
        {
            var state = new BotCombatState();
            state.SetCurrentTarget(42);
            state.TryMarkLooted(99);
            Assert.Equal(42ul, state.CurrentTargetGuid);
        }

        [Fact]
        public void SetCurrentTarget_RemovesFromLooted()
        {
            var state = new BotCombatState();
            state.TryMarkLooted(42);
            Assert.True(state.HasLooted(42));

            // Re-targeting the same GUID should remove it from looted
            state.SetCurrentTarget(42);
            Assert.False(state.HasLooted(42));
        }

        [Fact]
        public void MultipleTargets_IndependentLootTracking()
        {
            var state = new BotCombatState();
            state.TryMarkLooted(1);
            state.TryMarkLooted(2);

            Assert.True(state.HasLooted(1));
            Assert.True(state.HasLooted(2));
            Assert.False(state.HasLooted(3));
        }

        [Fact]
        public void ConcurrentAccess_ThreadSafe()
        {
            var state = new BotCombatState();
            var errors = new List<Exception>();

            var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
            {
                try
                {
                    ulong guid = (ulong)(i % 10);
                    state.SetCurrentTarget(guid);
                    _ = state.CurrentTargetGuid;
                    state.TryMarkLooted(guid);
                    _ = state.HasLooted(guid);
                    state.ClearCurrentTarget(guid);
                }
                catch (Exception ex)
                {
                    lock (errors) errors.Add(ex);
                }
            })).ToArray();

            Task.WaitAll(tasks);
            Assert.Empty(errors);
        }
    }
}
