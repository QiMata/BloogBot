using BotRunner.Interfaces;
using Communication;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks;

/// <summary>
/// P3.3: executes a <see cref="Communication.LoadoutSpec"/> received via
/// <c>ActionType.ApplyLoadout</c>. One task per bot, created exactly once
/// per loadout hand-off from StateManager; reports progress through the
/// shared <c>WoWActivitySnapshot.LoadoutStatus</c> field so the coordinator
/// can gate raid-formation / queue kick-off on every bot reaching
/// <see cref="LoadoutStatus.LoadoutReady"/>.
///
/// Plan-first + idempotent: the full ordered step list is built from the
/// spec on the first <see cref="Update"/>, then walked one step per tick
/// (with a small pacing delay so we don't flood the server with GM
/// commands). Each step is responsible for its own satisfaction check —
/// learning an already-known spell, setting a skill that already meets the
/// target, etc. are all no-ops.
///
/// Scope note: <c>honor_rank</c>, <c>faction_reps</c>, <c>completed_quest_ids</c>
/// and <c>talent_template</c> are intentionally no-op here. They run via
/// offline SOAP/MySQL during coordinator pre-launch (see
/// <c>BattlegroundCoordinatorFixtureBase.EnsureHonorRankForAccountsAsync</c>)
/// and are out of scope for the online, in-character executor.
/// </summary>
public class LoadoutTask : BotTask, IBotTask
{
    internal const int StepPacingMs = 100;
    internal const int MaxRetriesPerStep = 20;
    private const string ThrottleKey = "LoadoutTask.Pace";

    private readonly LoadoutSpec _spec;
    private LoadoutStatus _status = LoadoutStatus.LoadoutNotStarted;
    private string _failureReason = string.Empty;
    private List<LoadoutStep>? _plan;
    private int _stepIndex;

    public LoadoutTask(IBotContext context, LoadoutSpec spec)
        : base(context ?? throw new ArgumentNullException(nameof(context)))
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public LoadoutStatus Status => _status;
    public string FailureReason => _failureReason;
    public LoadoutSpec Spec => _spec;
    public int StepIndex => _stepIndex;
    internal IReadOnlyList<LoadoutStep> Plan => _plan ?? (IReadOnlyList<LoadoutStep>)Array.Empty<LoadoutStep>();

    public void Update()
    {
        if (_status == LoadoutStatus.LoadoutReady || _status == LoadoutStatus.LoadoutFailed)
            return;

        if (_plan == null)
        {
            _plan = BuildPlan(_spec);
            _status = LoadoutStatus.LoadoutInProgress;

            if (_plan.Count == 0)
            {
                _status = LoadoutStatus.LoadoutReady;
                return;
            }
        }

        // Skip leading steps that are already satisfied (idempotent short-circuit).
        while (_stepIndex < _plan.Count && TryIsSatisfied(_plan[_stepIndex]))
            _stepIndex++;

        if (_stepIndex >= _plan.Count)
        {
            _status = LoadoutStatus.LoadoutReady;
            return;
        }

        var step = _plan[_stepIndex];

        if (step.ExecuteAttempts >= MaxRetriesPerStep)
        {
            Fail($"step '{step.Description}' exceeded {MaxRetriesPerStep} retries without being satisfied");
            return;
        }

        if (!Wait.For(ThrottleKey, StepPacingMs, resetOnSuccess: true))
            return;

        if (step.TryExecute(BotContext))
        {
            step.MarkExecuted();
            if (step.IsOneShot)
                _stepIndex++;
        }
        else
        {
            step.MarkAttemptedWithoutDispatch();
        }
    }

    private bool TryIsSatisfied(LoadoutStep step)
    {
        try
        {
            return step.IsSatisfied(BotContext);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[LOADOUT] Step '{Step}' IsSatisfied threw; treating as not satisfied", step.Description);
            return false;
        }
    }

    private void Fail(string reason)
    {
        _status = LoadoutStatus.LoadoutFailed;
        _failureReason = reason;
        Log.Warning("[LOADOUT] Failed: {Reason}", reason);
        try { BotContext.AddDiagnosticMessage($"[LOADOUT-FAIL] {reason}"); } catch { }
    }

    internal static List<LoadoutStep> BuildPlan(LoadoutSpec spec)
    {
        var plan = new List<LoadoutStep>();

        // 1) Spells first — mounts require the spell before the riding skill is useful.
        foreach (var spellId in spec.SpellIdsToLearn)
        {
            if (spellId != 0)
                plan.Add(new LearnSpellStep(spellId));
        }
        if (spec.MountSpellId != 0)
            plan.Add(new LearnSpellStep(spec.MountSpellId));

        // 2) Skills (riding, weapon proficiency, profession values).
        if (spec.RidingSkill != 0)
            plan.Add(new SetSkillStep(AlteracValleyRidingSkillId, spec.RidingSkill, Math.Max(spec.RidingSkill, 300)));

        foreach (var skill in spec.Skills)
        {
            if (skill == null) continue;
            if (skill.SkillId == 0) continue;
            var maxValue = skill.Max == 0 ? Math.Max(skill.Value, 300) : skill.Max;
            plan.Add(new SetSkillStep(skill.SkillId, skill.Value, maxValue));
        }

        // 3) Armor set + individual supplementals populate bags.
        if (spec.ArmorSetId != 0)
            plan.Add(new AddItemSetStep(spec.ArmorSetId));

        foreach (var itemId in spec.SupplementalItemIds)
        {
            if (itemId != 0)
                plan.Add(new AddItemStep(itemId));
        }

        // 4) Equip everything the spec asked for.
        foreach (var equip in spec.EquipItems)
        {
            if (equip == null || equip.ItemId == 0) continue;
            plan.Add(new EquipItemStep(equip.ItemId));
        }

        // 5) Elixirs — one-shot use-item per id.
        foreach (var itemId in spec.ElixirItemIds)
        {
            if (itemId != 0)
                plan.Add(new UseItemStep(itemId));
        }

        // 6) Target level last so gear required-level checks have already passed.
        if (spec.TargetLevel > 0)
            plan.Add(new LevelUpStep(spec.TargetLevel));

        return plan;
    }

    // Riding skill id in WoW 1.12.1; reused from the legacy fixture loadout plan so
    // a spec built from CharacterSettings.Loadout (which only carries the numeric
    // target) dispatches the correct `.setskill` command.
    internal const uint AlteracValleyRidingSkillId = 762;

    internal static (int Bag, int Slot, IWoWItem Item)? FindItemByItemId(IObjectManager objectManager, uint itemId)
    {
        if (objectManager == null || itemId == 0)
            return null;

        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                IWoWItem? item;
                try { item = objectManager.GetContainedItem(bag, slot); }
                catch { continue; }
                if (item == null) continue;
                if (item.ItemId == itemId)
                    return (bag, slot, item);
            }
        }

        return null;
    }

    internal static uint GetSkillValue(IWoWLocalPlayer player, uint skillId)
    {
        if (player?.SkillInfo == null) return 0;
        foreach (var skill in player.SkillInfo)
        {
            if (skill == null) continue;
            if ((skill.SkillInt1 & 0xFFFF) == skillId)
                return skill.SkillInt2 & 0xFFFF;
        }
        return 0;
    }

    internal static bool KnowsSpell(IObjectManager objectManager, uint spellId)
    {
        if (objectManager == null || spellId == 0) return false;
        var known = objectManager.KnownSpellIds;
        if (known == null) return false;
        foreach (var id in known)
            if (id == spellId) return true;
        return false;
    }

    internal static bool BagContainsItem(IObjectManager objectManager, uint itemId)
        => itemId != 0 && FindItemByItemId(objectManager, itemId) != null;

    internal abstract class LoadoutStep
    {
        public int ExecuteAttempts { get; private set; }
        public int DispatchCount { get; private set; }
        public abstract string Description { get; }

        /// <summary>
        /// One-shot steps (like <c>.additemset</c> or pushing a child task) advance
        /// the plan pointer immediately after a successful Execute without waiting
        /// for an observable post-state. Verified steps (LearnSpell, SetSkill) stay
        /// on the pointer until <see cref="IsSatisfied"/> flips true.
        /// </summary>
        public virtual bool IsOneShot => false;

        public abstract bool IsSatisfied(IBotContext context);

        /// <summary>
        /// Issue the underlying command. Return <c>true</c> if the command was
        /// actually dispatched (so the task can count it against the retry budget
        /// and start a pacing wait). Return <c>false</c> when the context was not
        /// ready (e.g. no ObjectManager/Player yet) so the task retries without
        /// burning an attempt.
        /// </summary>
        public abstract bool TryExecute(IBotContext context);

        internal void MarkExecuted()
        {
            ExecuteAttempts++;
            DispatchCount++;
        }

        internal void MarkAttemptedWithoutDispatch() => ExecuteAttempts++;
    }

    internal sealed class LearnSpellStep : LoadoutStep
    {
        private readonly uint _spellId;
        public LearnSpellStep(uint spellId) { _spellId = spellId; }
        public uint SpellId => _spellId;
        public override string Description => $"learn spell {_spellId}";
        public override bool IsSatisfied(IBotContext context) => KnowsSpell(context.ObjectManager, _spellId);

        public override bool TryExecute(IBotContext context)
        {
            var om = context.ObjectManager;
            if (om?.Player == null) return false;
            if (KnowsSpell(om, _spellId)) return true;
            om.SendChatMessage($".learn {_spellId}");
            return true;
        }
    }

    internal sealed class SetSkillStep : LoadoutStep
    {
        private readonly uint _skillId;
        private readonly uint _value;
        private readonly uint _max;

        public SetSkillStep(uint skillId, uint value, uint max)
        {
            _skillId = skillId;
            _value = value;
            _max = max;
        }

        public uint SkillId => _skillId;
        public uint Value => _value;
        public uint Max => _max;
        public override string Description => $"set skill {_skillId}={_value}/{_max}";

        public override bool IsSatisfied(IBotContext context)
        {
            var player = context.ObjectManager?.Player;
            if (player == null) return false;
            return GetSkillValue(player, _skillId) >= _value;
        }

        public override bool TryExecute(IBotContext context)
        {
            var om = context.ObjectManager;
            if (om?.Player == null) return false;
            if (GetSkillValue(om.Player, _skillId) >= _value) return true;
            om.SendChatMessage($".setskill {_skillId} {_value} {_max}");
            return true;
        }
    }

    internal sealed class AddItemStep : LoadoutStep
    {
        private readonly uint _itemId;
        public AddItemStep(uint itemId) { _itemId = itemId; }
        public uint ItemId => _itemId;
        public override string Description => $"add item {_itemId}";

        public override bool IsSatisfied(IBotContext context)
            => BagContainsItem(context.ObjectManager, _itemId);

        public override bool TryExecute(IBotContext context)
        {
            var om = context.ObjectManager;
            if (om?.Player == null) return false;
            if (BagContainsItem(om, _itemId)) return true;
            om.SendChatMessage($".additem {_itemId}");
            return true;
        }
    }

    internal sealed class AddItemSetStep : LoadoutStep
    {
        private readonly uint _setId;
        public AddItemSetStep(uint setId) { _setId = setId; }
        public uint SetId => _setId;
        public override string Description => $"add item set {_setId}";

        /// <summary>One-shot: there's no reliable "set X is in bags" predicate.</summary>
        public override bool IsOneShot => true;

        public override bool IsSatisfied(IBotContext context) => DispatchCount > 0;

        public override bool TryExecute(IBotContext context)
        {
            var om = context.ObjectManager;
            if (om?.Player == null) return false;
            om.SendChatMessage($".additemset {_setId}");
            return true;
        }
    }

    internal sealed class EquipItemStep : LoadoutStep
    {
        private readonly uint _itemId;
        private ulong _equippedGuid;
        public EquipItemStep(uint itemId) { _itemId = itemId; }
        public uint ItemId => _itemId;
        public override string Description => $"equip item {_itemId}";

        public override bool IsSatisfied(IBotContext context)
        {
            var om = context.ObjectManager;
            if (om == null) return false;

            if (_equippedGuid != 0)
            {
                // If the item is no longer in bags, we consider it equipped.
                if (!BagContainsItem(om, _itemId))
                    return true;
            }

            // Fallback: scan equipped slots for matching itemId (Ammo..Tabard = 0..19).
            for (int slot = 0; slot <= 19; slot++)
            {
                IWoWItem? equipped;
                try { equipped = om.GetEquippedItem((GameData.Core.Enums.EquipSlot)slot); }
                catch { continue; }
                if (equipped != null && equipped.ItemId == _itemId)
                    return true;
            }

            return false;
        }

        public override bool TryExecute(IBotContext context)
        {
            var om = context.ObjectManager;
            if (om?.Player == null) return false;

            var located = FindItemByItemId(om, _itemId);
            if (located == null)
            {
                // Item not in bags yet; caller will retry after more pacing.
                return false;
            }

            _equippedGuid = located.Value.Item.Guid;
            context.BotTasks.Push(new EquipItemTask(context, located.Value.Bag, located.Value.Slot));
            return true;
        }
    }

    internal sealed class UseItemStep : LoadoutStep
    {
        private readonly uint _itemId;
        public UseItemStep(uint itemId) { _itemId = itemId; }
        public uint ItemId => _itemId;
        public override string Description => $"use item {_itemId}";

        /// <summary>One-shot: an elixir is consumed, so post-state = item gone.</summary>
        public override bool IsOneShot => true;

        public override bool IsSatisfied(IBotContext context)
            => DispatchCount > 0 || !BagContainsItem(context.ObjectManager, _itemId);

        public override bool TryExecute(IBotContext context)
        {
            var om = context.ObjectManager;
            if (om?.Player == null) return false;
            var located = FindItemByItemId(om, _itemId);
            if (located == null)
            {
                // Nothing to use — treat as already-consumed.
                return true;
            }
            context.BotTasks.Push(new UseItemTask(context, located.Value.Bag, located.Value.Slot));
            return true;
        }
    }

    internal sealed class LevelUpStep : LoadoutStep
    {
        private readonly uint _targetLevel;
        public LevelUpStep(uint targetLevel) { _targetLevel = targetLevel; }
        public uint TargetLevel => _targetLevel;
        public override string Description => $"level up to {_targetLevel}";

        public override bool IsSatisfied(IBotContext context)
        {
            var player = context.ObjectManager?.Player;
            if (player == null) return false;
            return player.Level >= _targetLevel;
        }

        public override bool TryExecute(IBotContext context)
        {
            var om = context.ObjectManager;
            var player = om?.Player;
            if (player == null) return false;
            if (player.Level >= _targetLevel) return true;
            var delta = (int)_targetLevel - (int)player.Level;
            if (delta <= 0) return true;
            om!.SendChatMessage($".levelup {delta}");
            return true;
        }
    }
}
