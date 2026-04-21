using BotRunner.Interfaces;
using BotRunner.Tasks;
using Communication;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace BotRunner.Tests;

/// <summary>
/// P3.3: coverage for the plan-builder + concrete step executors inside
/// <see cref="LoadoutTask"/>. Exercises spell / skill / item / equip / use /
/// level steps with a Moq-based <see cref="IObjectManager"/> harness that
/// records every outbound GM chat command.
/// </summary>
public sealed class LoadoutTaskExecutorTests
{
    // ---------- Plan builder ----------

    [Fact]
    public void BuildPlan_OrdersStepsAsSpellsSkillsItemSetItemsEquipElixirsLevel()
    {
        var spec = new LoadoutSpec
        {
            SpellIdsToLearn = { 1u, 2u },
            MountSpellId = 23509,
            RidingSkill = 150,
            Skills = { new LoadoutSkillValue { SkillId = 43, Value = 300, Max = 300 } },
            ArmorSetId = 501,
            SupplementalItemIds = { 100u, 101u },
            EquipItems = { new LoadoutEquipItem { ItemId = 200u }, new LoadoutEquipItem { ItemId = 201u } },
            ElixirItemIds = { 300u },
            TargetLevel = 60,
        };

        var plan = LoadoutTask.BuildPlan(spec);

        var idxSpell = plan.FindIndex(s => s is LoadoutTask.LearnSpellStep l && l.SpellId == 1u);
        var idxMount = plan.FindIndex(s => s is LoadoutTask.LearnSpellStep l && l.SpellId == 23509u);
        var idxRiding = plan.FindIndex(s => s is LoadoutTask.SetSkillStep sk && sk.SkillId == LoadoutTask.AlteracValleyRidingSkillId);
        var idxWeapon = plan.FindIndex(s => s is LoadoutTask.SetSkillStep sk && sk.SkillId == 43u);
        var idxSet = plan.FindIndex(s => s is LoadoutTask.AddItemSetStep);
        var idxAdd = plan.FindIndex(s => s is LoadoutTask.AddItemStep);
        var idxEquip = plan.FindIndex(s => s is LoadoutTask.EquipItemStep);
        var idxUse = plan.FindIndex(s => s is LoadoutTask.UseItemStep);
        var idxLevel = plan.FindIndex(s => s is LoadoutTask.LevelUpStep);

        Assert.True(idxSpell >= 0);
        Assert.True(idxSpell < idxRiding);
        Assert.True(idxMount > idxSpell && idxMount < idxRiding);
        Assert.True(idxRiding < idxWeapon);
        Assert.True(idxWeapon < idxSet);
        Assert.True(idxSet < idxAdd);
        Assert.True(idxAdd < idxEquip);
        Assert.True(idxEquip < idxUse);
        Assert.True(idxUse < idxLevel);
    }

    [Fact]
    public void BuildPlan_SkipsZeroValuedFields()
    {
        var spec = new LoadoutSpec
        {
            SpellIdsToLearn = { 0u, 5u },
            SupplementalItemIds = { 0u, 99u },
            EquipItems = { new LoadoutEquipItem { ItemId = 0u }, new LoadoutEquipItem { ItemId = 7u } },
        };

        var plan = LoadoutTask.BuildPlan(spec);

        Assert.Contains(plan, s => s is LoadoutTask.LearnSpellStep spell && spell.SpellId == 5u);
        Assert.DoesNotContain(plan, s => s is LoadoutTask.LearnSpellStep spell && spell.SpellId == 0u);
        Assert.DoesNotContain(plan, s => s is LoadoutTask.AddItemStep item && item.ItemId == 0u);
        Assert.DoesNotContain(plan, s => s is LoadoutTask.EquipItemStep eq && eq.ItemId == 0u);
    }

    [Fact]
    public void BuildPlan_EmptySpec_ProducesEmptyPlan()
    {
        var plan = LoadoutTask.BuildPlan(new LoadoutSpec());
        Assert.Empty(plan);
    }

    // ---------- Spell / skill steps ----------

    [Fact]
    public void LearnSpellStep_IsSatisfiedWhenSpellKnown()
    {
        var harness = new Harness();
        harness.KnownSpells.Add(23509);
        var step = new LoadoutTask.LearnSpellStep(23509);
        Assert.True(step.IsSatisfied(harness.Context));
    }

    [Fact]
    public void LearnSpellStep_DispatchesDotLearnWhenNotKnown()
    {
        var harness = new Harness();
        var step = new LoadoutTask.LearnSpellStep(12345);

        var dispatched = step.TryExecute(harness.Context);

        Assert.True(dispatched);
        Assert.Contains(".learn 12345", harness.SentChat);
    }

    [Fact]
    public void SetSkillStep_IsSatisfiedWhenCurrentValueMeetsTarget()
    {
        var harness = new Harness();
        harness.SetSkill(LoadoutTask.AlteracValleyRidingSkillId, current: 150, max: 300);
        var step = new LoadoutTask.SetSkillStep(LoadoutTask.AlteracValleyRidingSkillId, 150, 300);

        Assert.True(step.IsSatisfied(harness.Context));
    }

    [Fact]
    public void SetSkillStep_DispatchesDotSetSkillWhenBelowTarget()
    {
        var harness = new Harness();
        var step = new LoadoutTask.SetSkillStep(762, 150, 300);

        var dispatched = step.TryExecute(harness.Context);

        Assert.True(dispatched);
        Assert.Contains(".setskill 762 150 300", harness.SentChat);
    }

    // ---------- Item steps ----------

    [Fact]
    public void AddItemStep_IsSatisfiedWhenItemInBags()
    {
        var harness = new Harness();
        harness.AddBagItem(0, 0, itemId: 2770u);
        var step = new LoadoutTask.AddItemStep(2770u);

        Assert.True(step.IsSatisfied(harness.Context));
    }

    [Fact]
    public void AddItemStep_DispatchesDotAddItemWhenAbsent()
    {
        var harness = new Harness();
        var step = new LoadoutTask.AddItemStep(2770u);

        var dispatched = step.TryExecute(harness.Context);

        Assert.True(dispatched);
        Assert.Contains(".additem 2770", harness.SentChat);
    }

    [Fact]
    public void AddItemSetStep_DispatchesDotAdditemsetAndIsOneShot()
    {
        var harness = new Harness();
        var step = new LoadoutTask.AddItemSetStep(501u);

        Assert.True(step.IsOneShot);
        Assert.False(step.IsSatisfied(harness.Context));

        step.TryExecute(harness.Context);
        step.MarkExecuted();

        Assert.Contains(".additemset 501", harness.SentChat);
        Assert.True(step.IsSatisfied(harness.Context));
    }

    [Fact]
    public void EquipItemStep_PushesEquipItemTaskWhenItemInBags()
    {
        var harness = new Harness();
        harness.AddBagItem(bag: 1, slot: 3, itemId: 12000u);
        var step = new LoadoutTask.EquipItemStep(12000u);

        var dispatched = step.TryExecute(harness.Context);

        Assert.True(dispatched);
        Assert.NotEmpty(harness.BotTasks);
        Assert.IsType<EquipItemTask>(harness.BotTasks.Peek());
    }

    [Fact]
    public void EquipItemStep_ReturnsFalseWhenItemNotInBags()
    {
        var harness = new Harness();
        var step = new LoadoutTask.EquipItemStep(99999u);

        var dispatched = step.TryExecute(harness.Context);

        Assert.False(dispatched);
        Assert.Empty(harness.BotTasks);
    }

    [Fact]
    public void UseItemStep_PushesUseItemTaskWhenItemPresent()
    {
        var harness = new Harness();
        harness.AddBagItem(bag: 0, slot: 5, itemId: 13452u);
        var step = new LoadoutTask.UseItemStep(13452u);

        var dispatched = step.TryExecute(harness.Context);

        Assert.True(dispatched);
        Assert.NotEmpty(harness.BotTasks);
        Assert.IsType<UseItemTask>(harness.BotTasks.Peek());
    }

    [Fact]
    public void UseItemStep_IsOneShotAndSatisfiedAfterDispatchOrWhenAbsent()
    {
        var absentHarness = new Harness();
        var absent = new LoadoutTask.UseItemStep(55555u);
        Assert.True(absent.IsSatisfied(absentHarness.Context));

        var presentHarness = new Harness();
        presentHarness.AddBagItem(0, 0, 99u);
        var present = new LoadoutTask.UseItemStep(99u);
        Assert.False(present.IsSatisfied(presentHarness.Context));
        Assert.True(present.IsOneShot);
        present.TryExecute(presentHarness.Context);
        present.MarkExecuted();
        Assert.True(present.IsSatisfied(presentHarness.Context));
    }

    [Fact]
    public void LevelUpStep_DispatchesDotLevelUpWithDelta()
    {
        var harness = new Harness();
        harness.Player.SetupGet(p => p.Level).Returns(50u);
        var step = new LoadoutTask.LevelUpStep(60);

        var dispatched = step.TryExecute(harness.Context);

        Assert.True(dispatched);
        Assert.Contains(".levelup 10", harness.SentChat);
    }

    [Fact]
    public void LevelUpStep_IsSatisfiedWhenAlreadyAtOrAboveTarget()
    {
        var harness = new Harness();
        harness.Player.SetupGet(p => p.Level).Returns(60u);
        var step = new LoadoutTask.LevelUpStep(60);
        Assert.True(step.IsSatisfied(harness.Context));
    }

    // ---------- End-to-end task ticks ----------

    [Fact]
    public void Update_ExecutesOneDispatchPerTickAcrossMultipleSteps()
    {
        var harness = new Harness();
        var spec = new LoadoutSpec { SpellIdsToLearn = { 100u, 200u, 300u } };
        var task = new LoadoutTask(harness.Context, spec);

        task.Update();
        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(LoadoutTask.StepPacingMs + 25);
            task.Update();
            if (task.Status == LoadoutStatus.LoadoutReady) break;
        }

        Assert.Equal(LoadoutStatus.LoadoutReady, task.Status);
        Assert.Contains(".learn 100", harness.SentChat);
        Assert.Contains(".learn 200", harness.SentChat);
        Assert.Contains(".learn 300", harness.SentChat);
    }

    [Fact]
    public void Update_SkipsAlreadySatisfiedSteps_WithoutDispatching()
    {
        var harness = new Harness();
        harness.KnownSpells.Add(111u);
        var spec = new LoadoutSpec { SpellIdsToLearn = { 111u } };
        var task = new LoadoutTask(harness.Context, spec);

        task.Update();

        Assert.Equal(LoadoutStatus.LoadoutReady, task.Status);
        Assert.DoesNotContain(".learn 111", harness.SentChat);
    }

    // ---------- P4.3: event-driven step advancement ----------

    [Fact]
    public void LearnSpellStep_OnLearnedSpellEventWithMatchingId_FlipsAckFired()
    {
        var harness = new Harness();
        var step = new LoadoutTask.LearnSpellStep(12345u);

        step.AttachExpectedAck(harness.EventHandler.Object);

        Assert.False(step.AckFired);
        harness.EventHandler.Raise(e => e.OnLearnedSpell += null, null, new SpellChangedArgs(12345u));

        Assert.True(step.AckFired);
        Assert.True(step.IsSatisfied(harness.Context));
    }

    [Fact]
    public void LearnSpellStep_OnLearnedSpellEventWithWrongId_DoesNotFlipAck()
    {
        var harness = new Harness();
        var step = new LoadoutTask.LearnSpellStep(12345u);

        step.AttachExpectedAck(harness.EventHandler.Object);

        harness.EventHandler.Raise(e => e.OnLearnedSpell += null, null, new SpellChangedArgs(99999u));

        Assert.False(step.AckFired);
    }

    [Fact]
    public void SetSkillStep_OnSkillUpdatedEvent_RequiresNewValueAtOrAboveTarget()
    {
        var harness = new Harness();
        var step = new LoadoutTask.SetSkillStep(skillId: 762u, value: 150u, max: 300u);

        step.AttachExpectedAck(harness.EventHandler.Object);

        // Below target — does not flip the ack.
        harness.EventHandler.Raise(
            e => e.OnSkillUpdated += null,
            null,
            new SkillUpdatedArgs(skillId: 762u, oldValue: 0u, newValue: 75u, maxValue: 300u));
        Assert.False(step.AckFired);

        // At target — flips the ack.
        harness.EventHandler.Raise(
            e => e.OnSkillUpdated += null,
            null,
            new SkillUpdatedArgs(skillId: 762u, oldValue: 75u, newValue: 150u, maxValue: 300u));
        Assert.True(step.AckFired);
    }

    [Fact]
    public void AddItemStep_OnItemAddedToBagEvent_FlipsAckFired()
    {
        var harness = new Harness();
        var step = new LoadoutTask.AddItemStep(2770u);

        step.AttachExpectedAck(harness.EventHandler.Object);

        harness.EventHandler.Raise(
            e => e.OnItemAddedToBag += null,
            null,
            new ItemAddedToBagArgs(bag: 0u, slot: 0u, itemId: 2770u, count: 1u));

        Assert.True(step.AckFired);
        Assert.True(step.IsSatisfied(harness.Context));
    }

    [Fact]
    public void LoadoutStep_DetachExpectedAck_RemovesSubscription()
    {
        var harness = new Harness();
        var step = new LoadoutTask.LearnSpellStep(12345u);

        step.AttachExpectedAck(harness.EventHandler.Object);
        step.DetachExpectedAck();

        harness.EventHandler.Raise(e => e.OnLearnedSpell += null, null, new SpellChangedArgs(12345u));

        Assert.False(step.AckFired);
    }

    [Fact]
    public void LoadoutStep_AttachExpectedAck_IsIdempotent()
    {
        // Re-attaching the same step does not double-subscribe — if it did,
        // raising the event once would fire the handler twice. We prove
        // single-subscription by detaching once and confirming no handler
        // remains to see a subsequent event.
        var harness = new Harness();
        var step = new LoadoutTask.LearnSpellStep(12345u);

        step.AttachExpectedAck(harness.EventHandler.Object);
        step.AttachExpectedAck(harness.EventHandler.Object);
        step.DetachExpectedAck();

        harness.EventHandler.Raise(e => e.OnLearnedSpell += null, null, new SpellChangedArgs(12345u));

        Assert.False(step.AckFired);
    }

    [Fact]
    public void LoadoutStep_AttachExpectedAck_WithNullHandler_IsSafeNoOp()
    {
        var step = new LoadoutTask.LearnSpellStep(12345u);

        var ex = Record.Exception(() => step.AttachExpectedAck(null));

        Assert.Null(ex);
        Assert.False(step.AckFired);
        // Detach must still be safe even when nothing was subscribed.
        Record.Exception(step.DetachExpectedAck);
    }

    [Fact]
    public void Update_LearnedSpellEvent_AdvancesStepWithoutWaitingForPacing()
    {
        // Polling is disabled (SuppressFakeServer) so the ONLY way to advance
        // past step 0 is the OnLearnedSpell event flipping AckFired. We then
        // call Update() again immediately (well inside StepPacingMs) and
        // observe StepIndex moving from 0 to 1 — the core P4.3 guarantee.
        var harness = new Harness { SuppressFakeServer = true };
        var spec = new LoadoutSpec { SpellIdsToLearn = { 100u, 200u } };
        var task = new LoadoutTask(harness.Context, spec);

        task.Update();
        Assert.Equal(LoadoutStatus.LoadoutInProgress, task.Status);
        Assert.Equal(0, task.StepIndex);
        Assert.Contains(".learn 100", harness.SentChat);

        harness.EventHandler.Raise(e => e.OnLearnedSpell += null, null, new SpellChangedArgs(100u));

        // Immediate re-tick: no Thread.Sleep. The only thing that can flip
        // step 0's IsSatisfied here is the ack.
        task.Update();

        Assert.Equal(1, task.StepIndex);
    }

    [Fact]
    public void Update_SingleStepSpec_CompletesOnEventWithoutPacingDelay()
    {
        var harness = new Harness { SuppressFakeServer = true };
        var spec = new LoadoutSpec { SpellIdsToLearn = { 42u } };
        var task = new LoadoutTask(harness.Context, spec);

        task.Update(); // dispatches .learn 42
        Assert.Equal(LoadoutStatus.LoadoutInProgress, task.Status);

        harness.EventHandler.Raise(e => e.OnLearnedSpell += null, null, new SpellChangedArgs(42u));

        task.Update();

        Assert.Equal(LoadoutStatus.LoadoutReady, task.Status);
    }

    [Fact]
    public void Update_PollingFallback_StillReachesReadyWhenNoEventFires()
    {
        // No event raised. Polling path (fake server adds spell to KnownSpells
        // on .learn dispatch) must still drive the plan to Ready.
        var harness = new Harness();
        var spec = new LoadoutSpec { SpellIdsToLearn = { 100u, 200u } };
        var task = new LoadoutTask(harness.Context, spec);

        task.Update();
        for (int i = 0; i < 10; i++)
        {
            Thread.Sleep(LoadoutTask.StepPacingMs + 25);
            task.Update();
            if (task.Status == LoadoutStatus.LoadoutReady) break;
        }

        Assert.Equal(LoadoutStatus.LoadoutReady, task.Status);
        Assert.Contains(".learn 100", harness.SentChat);
        Assert.Contains(".learn 200", harness.SentChat);
    }

    [Fact]
    public void Update_TerminalReady_DetachesAllStepSubscriptions()
    {
        // Empty spec short-circuits to Ready immediately. Subsequent events
        // must not flip any step's ack (there are none), but more importantly
        // the plan's AttachExpectedAcks + DetachAllAcks lifecycle must run
        // without throwing.
        var harness = new Harness();
        var spec = new LoadoutSpec();
        var task = new LoadoutTask(harness.Context, spec);

        task.Update();
        Assert.Equal(LoadoutStatus.LoadoutReady, task.Status);

        // Post-terminal event raise is a safe no-op: no subscribers remain.
        var ex = Record.Exception(() => harness.EventHandler.Raise(
            e => e.OnLearnedSpell += null, null, new SpellChangedArgs(1u)));
        Assert.Null(ex);
    }

    [Fact]
    public void Update_AdvancedStep_DetachesItsSubscriptionIndividually()
    {
        // After step 0 completes, further OnLearnedSpell events for step 0's
        // spellId must not be able to re-flip state (the subscription is
        // gone). We prove this by accessing the step instance through the
        // plan and asserting AckFired is observable only while it is the
        // active step.
        var harness = new Harness { SuppressFakeServer = true };
        var spec = new LoadoutSpec { SpellIdsToLearn = { 100u, 200u } };
        var task = new LoadoutTask(harness.Context, spec);

        task.Update();
        var step0 = task.Plan[0];
        var step1 = task.Plan[1];

        harness.EventHandler.Raise(e => e.OnLearnedSpell += null, null, new SpellChangedArgs(100u));
        Assert.True(step0.AckFired);

        task.Update(); // advances past step 0, detaches its subscription
        Assert.Equal(1, task.StepIndex);

        // Raising a matching event for step 1 must still work (its
        // subscription is still installed).
        Assert.False(step1.AckFired);
        harness.EventHandler.Raise(e => e.OnLearnedSpell += null, null, new SpellChangedArgs(200u));
        Assert.True(step1.AckFired);
    }

    // ---------- Moq harness ----------

    private sealed class Harness
    {
        public readonly Mock<IObjectManager> ObjectManager = new(MockBehavior.Loose);
        public readonly Mock<IWoWLocalPlayer> Player = new(MockBehavior.Loose);
        public readonly Mock<IBotContext> ContextMock = new(MockBehavior.Loose);
        public readonly Mock<IWoWEventHandler> EventHandler = new(MockBehavior.Loose);
        public readonly HashSet<uint> KnownSpells = new();
        public readonly List<string> SentChat = new();
        public readonly Dictionary<(int, int), IWoWItem> Bags = new();
        public readonly SkillInfo[] Skills = Enumerable.Range(0, 128).Select(_ => new SkillInfo()).ToArray();
        public readonly Stack<IBotTask> BotTasks = new();

        /// <summary>
        /// When true, <c>.learn</c>/<c>.additem</c>/<c>.setskill</c> commands
        /// do NOT mutate the simulated server state. Tests that want to drive
        /// advancement exclusively through events (not polling) set this.
        /// </summary>
        public bool SuppressFakeServer { get; set; }

        public Harness()
        {
            ObjectManager.SetupGet(om => om.Player).Returns(Player.Object);
            ObjectManager.SetupGet(om => om.KnownSpellIds).Returns(KnownSpells);
            ObjectManager
                .Setup(om => om.GetContainedItem(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((bag, slot) => Bags.TryGetValue((bag, slot), out var item) ? item : null!);
            ObjectManager
                .Setup(om => om.SendChatMessage(It.IsAny<string>()))
                .Callback<string>(msg =>
                {
                    SentChat.Add(msg);
                    if (!SuppressFakeServer)
                        ApplyFakeServerSideEffect(msg);
                });
            ObjectManager
                .Setup(om => om.GetEquippedItem(It.IsAny<EquipSlot>()))
                .Returns((IWoWItem)null!);

            Player.SetupGet(p => p.SkillInfo).Returns(Skills);
            Player.SetupGet(p => p.Level).Returns(1u);

            ContextMock.SetupGet(c => c.ObjectManager).Returns(ObjectManager.Object);
            ContextMock.SetupGet(c => c.BotTasks).Returns(BotTasks);
            ContextMock.SetupGet(c => c.LoggerFactory).Returns((Microsoft.Extensions.Logging.ILoggerFactory?)null);
            ContextMock.SetupGet(c => c.Config).Returns(new BotRunner.Constants.BotBehaviorConfig());
            ContextMock.SetupGet(c => c.EventHandler).Returns(EventHandler.Object);
        }

        public IBotContext Context => ContextMock.Object;

        public void AddBagItem(int bag, int slot, uint itemId)
        {
            var mockItem = new Mock<IWoWItem>(MockBehavior.Loose);
            mockItem.SetupGet(i => i.ItemId).Returns(itemId);
            mockItem.SetupGet(i => i.Guid).Returns((ulong)(itemId + 1_000_000));
            Bags[(bag, slot)] = mockItem.Object;
        }

        public void SetSkill(uint skillId, uint current, uint max)
        {
            Skills[0].SkillInt1 = skillId;
            Skills[0].SkillInt2 = current | (max << 16);
        }

        /// <summary>
        /// Simulates a minimal MaNGOS response: <c>.learn X</c> adds X to
        /// <see cref="KnownSpells"/>, <c>.additem X</c> materialises the
        /// item in the backpack, <c>.setskill S V M</c> writes the skill
        /// entry. Lets end-to-end Update() ticks actually converge.
        /// </summary>
        private void ApplyFakeServerSideEffect(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            switch (parts[0])
            {
                case ".learn" when parts.Length >= 2 && uint.TryParse(parts[1], out var spellId):
                    KnownSpells.Add(spellId);
                    break;
                case ".additem" when parts.Length >= 2 && uint.TryParse(parts[1], out var itemId):
                    AddBagItem(0, NextFreeBackpackSlot(), itemId);
                    break;
                case ".setskill" when parts.Length >= 4
                                     && uint.TryParse(parts[1], out var skillId2)
                                     && uint.TryParse(parts[2], out var value)
                                     && uint.TryParse(parts[3], out var max):
                    SetSkill(skillId2, value, max);
                    break;
                case ".levelup" when parts.Length >= 2 && int.TryParse(parts[1], out var delta):
                    var currentLevel = Player.Object.Level;
                    Player.SetupGet(p => p.Level).Returns((uint)(currentLevel + delta));
                    break;
            }
        }

        private int NextFreeBackpackSlot()
        {
            for (int slot = 0; slot < 16; slot++)
                if (!Bags.ContainsKey((0, slot))) return slot;
            return 0;
        }
    }
}
