using System;
using Communication;

namespace Tests.Infrastructure;

/// <summary>
/// Live-test guard that fails fast when a bot is taking environmental damage
/// (e.g. standing in lava) rather than letting the test soldier on through the
/// full travel timeout. Detected via continuous health loss outside combat.
///
/// Exempt map IDs let lava-zone tests (Molten Core, Ragefire Chasm) pass through
/// — there it's expected the bot will take environmental ticks during fights.
///
/// Heuristic and intentional: WoW classic doesn't surface liquid type on the
/// activity-snapshot wire, but lava ticks (~600 hp/s in combat-free terrain)
/// are unmistakable across consecutive polls. Tune via constructor if a future
/// scenario needs a different sensitivity.
/// </summary>
public sealed class LavaHazardGuard
{
    /// <summary>UNIT_FIELD_FLAGS bit for "in combat" — exempt path while fighting.</summary>
    private const uint UnitFlagInCombat = 0x00080000u;

    private readonly string _label;
    private readonly int _consecutiveDropsThreshold;
    private readonly int[] _exemptMapIds;

    private uint? _previousHealth;
    private int _consecutiveDrops;

    /// <summary>
    /// Build a guard. <paramref name="consecutiveDropsThreshold"/> defaults to 3
    /// non-combat health drops in a row — high enough to absorb a single tick of
    /// natural damage (falling, lag-induced sample), low enough to fire well
    /// inside the 10 s the screenshot scenario typically lingers in lava.
    /// </summary>
    public LavaHazardGuard(string label, int consecutiveDropsThreshold = 3, params int[] exemptMapIds)
    {
        _label = label;
        _consecutiveDropsThreshold = Math.Max(1, consecutiveDropsThreshold);
        _exemptMapIds = exemptMapIds ?? Array.Empty<int>();
    }

    /// <summary>Reset state — call between unrelated test legs.</summary>
    public void Reset()
    {
        _previousHealth = null;
        _consecutiveDrops = 0;
    }

    /// <summary>
    /// Inspect a fresh snapshot. If it shows continuous out-of-combat health loss
    /// across <see cref="_consecutiveDropsThreshold"/> polls on a non-exempt map,
    /// invoke <paramref name="fail"/> with a descriptive message and the offending
    /// snapshot so the test surfaces a clear failure (screenshot + log artifact).
    /// </summary>
    public void FailIfBurning(WoWActivitySnapshot? snapshot, Action<string, WoWActivitySnapshot?> fail)
    {
        if (snapshot?.Player?.Unit == null) return;

        var mapId = (int)snapshot.CurrentMapId;
        foreach (var exempt in _exemptMapIds)
        {
            if (exempt == mapId) return;
        }

        var unit = snapshot.Player.Unit;
        var currentHealth = unit.Health;
        if (currentHealth == 0) return; // Bot is dead/zoning — no signal here.

        var inCombat = (unit.UnitFlags & UnitFlagInCombat) != 0;

        if (_previousHealth.HasValue && currentHealth < _previousHealth.Value && !inCombat)
        {
            _consecutiveDrops++;
            if (_consecutiveDrops >= _consecutiveDropsThreshold)
            {
                var pos = snapshot.Player.Unit.GameObject?.Base?.Position;
                var posDisplay = pos != null ? $"({pos.X:F1},{pos.Y:F1},{pos.Z:F1})" : "(unknown)";
                fail(
                    $"{_label}: bot taking environmental damage outside combat — health "
                    + $"{_previousHealth.Value} → {currentHealth} over {_consecutiveDrops} consecutive polls. "
                    + $"map={mapId} pos={posDisplay}. Likely lava/fire/poison from a failed path. "
                    + $"(If the test target is a lava zone, add its mapId to the LavaHazardGuard exempt list.)",
                    snapshot);
                return;
            }
        }
        else if (currentHealth >= (_previousHealth ?? currentHealth))
        {
            _consecutiveDrops = 0; // health stable or rising — reset the tick counter
        }

        _previousHealth = currentHealth;
    }
}
