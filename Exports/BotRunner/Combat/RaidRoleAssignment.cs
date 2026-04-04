using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Combat;

/// <summary>
/// Tracks Main Tank and Main Assist assignments for raid coordination.
/// In vanilla 1.12.1, there is no server-side MT/MA — this is tracked
/// client-side by the raid leader and communicated via raid chat or addons.
/// Bots coordinate via this shared state.
/// </summary>
public class RaidRoleAssignment
{
    private ulong _mainTankGuid;
    private ulong _mainAssistGuid;
    private readonly Dictionary<ulong, RaidRole> _roleAssignments = new();

    public enum RaidRole { DPS, Tank, Healer, OffTank }

    /// <summary>Current Main Tank GUID. 0 if unset.</summary>
    public ulong MainTankGuid => _mainTankGuid;

    /// <summary>Current Main Assist GUID. 0 if unset.</summary>
    public ulong MainAssistGuid => _mainAssistGuid;

    /// <summary>Set the Main Tank.</summary>
    public void SetMainTank(ulong playerGuid)
    {
        _mainTankGuid = playerGuid;
        _roleAssignments[playerGuid] = RaidRole.Tank;
    }

    /// <summary>Set the Main Assist.</summary>
    public void SetMainAssist(ulong playerGuid)
    {
        _mainAssistGuid = playerGuid;
    }

    /// <summary>Assign a role to a raid member.</summary>
    public void AssignRole(ulong playerGuid, RaidRole role)
    {
        _roleAssignments[playerGuid] = role;
    }

    /// <summary>Get the role for a player. Defaults to DPS.</summary>
    public RaidRole GetRole(ulong playerGuid)
    {
        return _roleAssignments.TryGetValue(playerGuid, out var role) ? role : RaidRole.DPS;
    }

    /// <summary>Get all players with a specific role.</summary>
    public IReadOnlyList<ulong> GetPlayersWithRole(RaidRole role)
    {
        return _roleAssignments
            .Where(kv => kv.Value == role)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>
    /// Auto-assign MT from tank-role players. Picks the first Tank.
    /// </summary>
    public void AutoAssignMainTank()
    {
        var tanks = GetPlayersWithRole(RaidRole.Tank);
        if (tanks.Count > 0 && _mainTankGuid == 0)
            _mainTankGuid = tanks[0];
    }

    /// <summary>Reset all assignments.</summary>
    public void Reset()
    {
        _mainTankGuid = 0;
        _mainAssistGuid = 0;
        _roleAssignments.Clear();
    }
}
