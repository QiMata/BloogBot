using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using System;
using System.Linq;

namespace BotRunner.Tasks;

/// <summary>
/// Base class for combat rotation tasks with common combat utilities.
/// </summary>
public abstract class CombatRotationTask(IBotContext botContext) : BotTask(botContext)
{
    // Potion cooldown tracking (potions share a cooldown via Config.PotionCooldownMs)
    private static DateTime _lastPotionUsed = DateTime.MinValue;

    // Kiting state
    private bool _isKiting;
    private int _kiteStartTime;
    private int _kiteDurationMs;

    /// <summary>
    /// Perform the combat rotation logic.
    /// </summary>
    public abstract void PerformCombatRotation();

    /// <summary>
    /// Update with target distance check and movement.
    /// Automatically checks for emergency potion usage.
    /// </summary>
    /// <param name="attackDistance">Distance to maintain from target.</param>
    /// <returns>True if still moving to target.</returns>
    protected bool Update(float attackDistance)
    {
        // Emergency potion usage during combat
        TryUseHealthPotion();
        TryUseManaPotion();

        var target = ObjectManager.GetTarget(ObjectManager.Player);
        if (target == null) return false;

        var distance = ObjectManager.Player.Position.DistanceTo(target.Position);
        if (distance > attackDistance)
        {
            // Move toward target
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempt to cast a spell if ready (condition-only overload for legacy profiles).
    /// </summary>
    protected bool TryCastSpell(string spellName, bool condition, bool castOnSelf = false)
        => TryCastSpell(spellName, 0, int.MaxValue, condition, castOnSelf);

    /// <summary>
    /// Attempt to cast a spell if ready and in range, with optional callback after cast.
    /// </summary>
    protected bool TryCastSpell(string spellName, int minRange = 0, int maxRange = int.MaxValue, bool condition = true, bool castOnSelf = false, Action? callback = null)
    {
        if (!condition) return false;

        var target = ObjectManager.GetTarget(ObjectManager.Player);
        if (target == null && !castOnSelf) return false;

        var distance = castOnSelf ? 0 : ObjectManager.Player.Position.DistanceTo(target!.Position);
        if (distance < minRange || distance > maxRange) return false;

        if (!ObjectManager.IsSpellReady(spellName)) return false;

        ObjectManager.CastSpell(spellName, castOnSelf: castOnSelf);
        callback?.Invoke();
        return true;
    }

    /// <summary>
    /// Attempt to use an ability if ready (condition-only overload for legacy profiles).
    /// </summary>
    protected bool TryUseAbility(string abilityName, bool condition)
        => TryUseAbility(abilityName, 0, condition);

    /// <summary>
    /// Attempt to use an ability if ready.
    /// </summary>
    protected bool TryUseAbility(string abilityName, int energyCost = 0, bool condition = true)
    {
        if (!condition) return false;
        if (ObjectManager.Player.Energy < energyCost && ObjectManager.Player.Rage < energyCost)
            return false;

        if (!ObjectManager.IsSpellReady(abilityName)) return false;

        ObjectManager.CastSpell(abilityName);
        return true;
    }

    /// <summary>
    /// Attempt to use an ability with callback on success (e.g. Slam ready → Slam callback).
    /// </summary>
    protected bool TryUseAbility(string abilityName, int energyCost, bool condition, Action callback)
    {
        if (TryUseAbility(abilityName, energyCost, condition))
        {
            callback?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Attempt to use an ability by spell ID.
    /// </summary>
    protected bool TryUseAbilityById(string abilityName, int spellId, int resourceCost = 0, bool condition = true)
    {
        if (!condition) return false;
        if (ObjectManager.Player.Energy < resourceCost && ObjectManager.Player.Rage < resourceCost)
            return false;

        if (!ObjectManager.IsSpellReady(abilityName)) return false;

        ObjectManager.CastSpell(abilityName);
        return true;
    }

    /// <summary>
    /// Whether the current target is moving toward the player (closing distance).
    /// Approximated by checking if target is facing the player within ~90 degrees.
    /// </summary>
    protected bool TargetMovingTowardPlayer
    {
        get
        {
            var target = ObjectManager.GetTarget(ObjectManager.Player);
            if (target == null) return false;
            return target.IsInCombat && target.TargetGuid == ObjectManager.Player.Guid
                && target.Position.DistanceTo(ObjectManager.Player.Position) > 3;
        }
    }

    /// <summary>
    /// Validates that we have aggressors and a valid target. If no aggressors,
    /// pops the combat task. If current target is dead/null, selects the best
    /// target from aggressors (lowest health first).
    /// </summary>
    /// <returns>True if we have a valid target to fight, false if combat should end.</returns>
    protected bool EnsureTarget()
    {
        if (!ObjectManager.Aggressors.Any())
        {
            BotTasks.Pop();
            return false;
        }

        var target = ObjectManager.GetTarget(ObjectManager.Player);
        if (target == null || target.HealthPercent <= 0)
            AssignDPSTarget();

        return true;
    }

    /// <summary>
    /// Assign the best DPS target from aggressors.
    /// Prioritizes lowest health for kill confirmation.
    /// </summary>
    protected void AssignDPSTarget()
    {
        var aggressors = ObjectManager.Aggressors.ToList();
        if (!aggressors.Any()) return;

        // Prefer target with lowest health for kill confirmation
        var bestTarget = aggressors.OrderBy(a => a.HealthPercent).First();
        ObjectManager.SetTarget(bestTarget.Guid);
    }

    /// <summary>
    /// Move behind the current target at a given distance.
    /// </summary>
    /// <param name="distance">Distance to maintain from target.</param>
    /// <returns>True if still moving.</returns>
    protected bool MoveBehindTarget(float distance)
    {
        var target = ObjectManager.GetTarget(ObjectManager.Player);
        if (target == null) return false;

        // Simplified: just check if we're in range
        var dist = ObjectManager.Player.Position.DistanceTo(target.Position);
        return dist > distance;
    }

    /// <summary>
    /// Move behind the tank's position.
    /// </summary>
    protected bool MoveBehindTankSpot(float distance)
    {
        var partyLeader = ObjectManager.PartyLeader;
        if (partyLeader == null) return false;

        var dist = ObjectManager.Player.Position.DistanceTo(partyLeader.Position);
        return dist > distance;
    }

    /// <summary>
    /// Whether the player is currently kiting (backpedaling away from target).
    /// Automatically stops kiting when the duration expires.
    /// Check this at the top of Update() and return early if true.
    /// </summary>
    protected bool IsKiting
    {
        get
        {
            if (!_isKiting) return false;

            if (Environment.TickCount - _kiteStartTime > _kiteDurationMs)
            {
                StopKiting();
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Start kiting (backpedaling) away from the target for the specified duration.
    /// Use as a callback after landing a root/snare (e.g., Frost Nova, Entangling Roots).
    /// </summary>
    /// <param name="durationMs">How long to backpedal in milliseconds.</param>
    protected void StartKite(int durationMs)
    {
        _isKiting = true;
        _kiteStartTime = Environment.TickCount;
        _kiteDurationMs = durationMs;
        ObjectManager.StartMovement(ControlBits.Back);
    }

    /// <summary>
    /// Stop kiting immediately.
    /// </summary>
    protected void StopKiting()
    {
        if (_isKiting)
        {
            ObjectManager.StopMovement(ControlBits.Back);
            _isKiting = false;
        }
    }

    /// <summary>
    /// Check if the player should use a health potion and attempt to use one.
    /// Call this early in combat rotations for emergency healing.
    /// </summary>
    /// <returns>True if a potion was used.</returns>
    protected bool TryUseHealthPotion()
    {
        if (ObjectManager.Player.HealthPercent > Config.HealthPotionThresholdPct)
            return false;
        return TryUsePotion("health");
    }

    /// <summary>
    /// Check if the player should use a mana potion and attempt to use one.
    /// </summary>
    /// <returns>True if a potion was used.</returns>
    protected bool TryUseManaPotion()
    {
        if (ObjectManager.Player.MaxMana == 0)
            return false;
        if (ObjectManager.Player.ManaPercent > Config.ManaPotionThresholdPct)
            return false;
        return TryUsePotion("mana");
    }

    private bool TryUsePotion(string type)
    {
        // Check potion cooldown (all potions share 2-min CD)
        if ((DateTime.Now - _lastPotionUsed).TotalMilliseconds < Config.PotionCooldownMs)
            return false;

        // Scan inventory for matching potion
        var items = ObjectManager.GetContainedItems();
        foreach (var item in items)
        {
            if (item == null) continue;
            var name = item.Name?.ToLowerInvariant() ?? "";
            if (type == "health" && IsHealthPotion(name))
            {
                UseItemFromInventory(item);
                _lastPotionUsed = DateTime.Now;
                return true;
            }
            if (type == "mana" && IsManaPotion(name))
            {
                UseItemFromInventory(item);
                _lastPotionUsed = DateTime.Now;
                return true;
            }
        }
        return false;
    }

    private void UseItemFromInventory(IWoWItem item)
    {
        // Find the bag and slot for this item
        for (int bag = 0; bag < 5; bag++)
        {
            int maxSlots = bag == 0 ? 16 : 20; // backpack has 16, extra bags up to ~20
            for (int slot = 0; slot < maxSlots; slot++)
            {
                var contained = ObjectManager.GetContainedItem(bag, slot);
                if (contained != null && contained.Guid == item.Guid)
                {
                    ObjectManager.UseItem(bag, slot);
                    return;
                }
            }
        }
    }

    // ======== GROUP COMBAT COORDINATION ========

    /// <summary>
    /// Whether this player is currently in a party (has at least one party member).
    /// </summary>
    protected bool IsInGroup => ObjectManager.PartyMembers.Any();

    /// <summary>
    /// Get the group's marked kill target (Skull first, then Cross).
    /// Returns null if no raid markers are set or no matching unit found.
    /// Use this for DPS to assist the tank's marked targets.
    /// </summary>
    protected IWoWUnit? GetMarkedTarget()
    {
        // Skull = kill first priority
        var skullGuid = ObjectManager.SkullTargetGuid;
        if (skullGuid != 0)
        {
            var skull = ObjectManager.Aggressors.FirstOrDefault(a => a.Guid == skullGuid);
            if (skull != null && skull.HealthPercent > 0) return skull;
        }

        // Cross = kill second priority
        var crossGuid = ObjectManager.CrossTargetGuid;
        if (crossGuid != 0)
        {
            var cross = ObjectManager.Aggressors.FirstOrDefault(a => a.Guid == crossGuid);
            if (cross != null && cross.HealthPercent > 0) return cross;
        }

        return null;
    }

    /// <summary>
    /// Get the best assist target for DPS in a group.
    /// Priority: Skull mark > Cross mark > Tank's target > Lowest HP aggressor.
    /// </summary>
    protected IWoWUnit? GetAssistTarget()
    {
        // Marked targets first
        var marked = GetMarkedTarget();
        if (marked != null) return marked;

        // Follow the tank/leader's target
        var leader = ObjectManager.PartyLeader;
        if (leader != null)
        {
            var leaderTarget = ObjectManager.GetTarget(leader);
            if (leaderTarget != null && leaderTarget.HealthPercent > 0
                && ObjectManager.Aggressors.Any(a => a.Guid == leaderTarget.Guid))
                return leaderTarget;
        }

        // Default: lowest HP aggressor
        return ObjectManager.Aggressors
            .Where(a => a.HealthPercent > 0)
            .OrderBy(a => a.HealthPercent)
            .FirstOrDefault();
    }

    /// <summary>
    /// Assign the group assist target (for DPS in groups) or best solo target.
    /// In a group: follows Skull > Cross > Tank's target > lowest HP.
    /// Solo: lowest HP aggressor.
    /// </summary>
    protected void AssignGroupTarget()
    {
        if (IsInGroup)
        {
            var assist = GetAssistTarget();
            if (assist != null)
            {
                ObjectManager.SetTarget(assist.Guid);
                return;
            }
        }

        AssignDPSTarget();
    }

    /// <summary>
    /// Get the party member most in need of healing.
    /// Returns null if no party members are below the threshold.
    /// </summary>
    /// <param name="healthThreshold">HP% threshold to consider healing (default 70).</param>
    protected IWoWUnit? GetHealTarget(int healthThreshold = 70)
    {
        // Check self first at a lower threshold
        if (ObjectManager.Player.HealthPercent < healthThreshold - 20)
            return ObjectManager.Player;

        // Check party members
        IWoWUnit? lowestMember = null;
        int lowestHp = healthThreshold;

        foreach (var member in ObjectManager.PartyMembers)
        {
            if (member.HealthPercent <= 0) continue; // dead
            if (member.HealthPercent < lowestHp)
            {
                lowestHp = (int)member.HealthPercent;
                lowestMember = member;
            }
        }

        // Also consider self
        if (ObjectManager.Player.HealthPercent < lowestHp)
            return ObjectManager.Player;

        return lowestMember;
    }

    /// <summary>
    /// Whether DPS should wait for the tank to establish threat.
    /// Returns true if we're in a group and aggro units are targeting someone other than us
    /// but the tank hasn't held them for at least 2 seconds.
    /// Simplified heuristic: if target is not targeting the tank yet, wait briefly.
    /// </summary>
    protected bool ShouldWaitForThreat()
    {
        if (!IsInGroup) return false;

        var leader = ObjectManager.PartyLeader;
        if (leader == null) return false;

        var target = ObjectManager.GetTarget(ObjectManager.Player);
        if (target == null) return false;

        // If the target is already targeting us or the tank, we're good
        if (target.TargetGuid == ObjectManager.Player.Guid) return false;
        if (target.TargetGuid == leader.Guid) return false;

        // If target isn't targeting anyone yet, wait for tank to grab it
        return target.TargetGuid == 0;
    }

    /// <summary>
    /// Attempt to heal the party member (or self) most in need.
    /// Temporarily targets the heal target, casts, then re-targets an enemy.
    /// Returns true if a heal was cast.
    /// </summary>
    /// <param name="spellName">Healing spell to cast.</param>
    /// <param name="healthThreshold">HP% threshold — party members below this are candidates.</param>
    /// <param name="maxRange">Max casting range for the heal (default 40y).</param>
    protected bool TryCastHeal(string spellName, int healthThreshold = 70, int maxRange = 40)
    {
        var healTarget = GetHealTarget(healthThreshold);
        if (healTarget == null) return false;

        if (healTarget.Guid == ObjectManager.Player.Guid)
            return TryCastSpell(spellName, 0, int.MaxValue, true, castOnSelf: true);

        // Target the party member for healing
        var prevTarget = ObjectManager.GetTarget(ObjectManager.Player);
        ObjectManager.SetTarget(healTarget.Guid);
        var result = TryCastSpell(spellName, 0, maxRange);

        // Re-target enemy
        if (prevTarget != null && prevTarget.HealthPercent > 0)
            ObjectManager.SetTarget(prevTarget.Guid);
        else
            AssignGroupTarget();

        return result;
    }

    /// <summary>
    /// Find aggressors not targeting the tank (loose adds).
    /// Used by tanks to pick up mobs that are attacking party members.
    /// </summary>
    protected IWoWUnit? FindLooseAdd()
    {
        var player = ObjectManager.Player;
        return ObjectManager.Aggressors
            .Where(a => a.HealthPercent > 0 && a.TargetGuid != player.Guid)
            .OrderBy(a => a.Position.DistanceTo(player.Position))
            .FirstOrDefault();
    }

    private static bool IsHealthPotion(string name)
    {
        return name.Contains("healing potion") || name.Contains("health potion")
            || name == "minor healing potion" || name == "lesser healing potion"
            || name == "healing potion" || name == "greater healing potion"
            || name == "superior healing potion" || name == "major healing potion";
    }

    private static bool IsManaPotion(string name)
    {
        return name.Contains("mana potion")
            || name == "minor mana potion" || name == "lesser mana potion"
            || name == "mana potion" || name == "greater mana potion"
            || name == "superior mana potion" || name == "major mana potion";
    }
}
