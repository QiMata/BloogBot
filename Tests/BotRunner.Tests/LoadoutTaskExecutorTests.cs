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

    // ---------- Moq harness ----------

    private sealed class Harness
    {
        public readonly Mock<IObjectManager> ObjectManager = new(MockBehavior.Loose);
        public readonly Mock<IWoWLocalPlayer> Player = new(MockBehavior.Loose);
        public readonly Mock<IBotContext> ContextMock = new(MockBehavior.Loose);
        public readonly HashSet<uint> KnownSpells = new();
        public readonly List<string> SentChat = new();
        public readonly Dictionary<(int, int), IWoWItem> Bags = new();
        public readonly SkillInfo[] Skills = Enumerable.Range(0, 128).Select(_ => new SkillInfo()).ToArray();
        public readonly Stack<IBotTask> BotTasks = new();

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
