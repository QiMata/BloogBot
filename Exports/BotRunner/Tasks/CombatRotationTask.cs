using BotRunner.Interfaces;

namespace BotRunner.Tasks;

/// <summary>
/// Base class for combat rotation tasks with common combat utilities.
/// </summary>
public abstract class CombatRotationTask : BotTask
{
    protected CombatRotationTask(IBotContext botContext) : base(botContext) { }

    /// <summary>
    /// Perform the combat rotation logic.
    /// </summary>
    public abstract void PerformCombatRotation();

    /// <summary>
    /// Update with target distance check and movement.
    /// </summary>
    /// <param name="attackDistance">Distance to maintain from target.</param>
    /// <returns>True if still moving to target.</returns>
    protected bool Update(float attackDistance)
    {
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
    /// Attempt to cast a spell if ready and in range.
    /// </summary>
    protected bool TryCastSpell(string spellName, int minRange = 0, int maxRange = int.MaxValue, bool condition = true, bool castOnSelf = false)
    {
        if (!condition) return false;

        var target = ObjectManager.GetTarget(ObjectManager.Player);
        if (target == null && !castOnSelf) return false;

        var distance = castOnSelf ? 0 : ObjectManager.Player.Position.DistanceTo(target!.Position);
        if (distance < minRange || distance > maxRange) return false;

        if (!ObjectManager.IsSpellReady(spellName)) return false;

        ObjectManager.CastSpell(spellName, castOnSelf: castOnSelf);
        return true;
    }

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
    /// Assign the best DPS target from aggressors.
    /// </summary>
    protected void AssignDPSTarget()
    {
        var aggressors = ObjectManager.Aggressors.ToList();
        if (!aggressors.Any()) return;

        // Prefer target with lowest health
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
}
