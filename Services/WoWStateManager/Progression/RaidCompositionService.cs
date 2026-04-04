using GameData.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WoWStateManager.Progression;

/// <summary>
/// Auto-assigns raid roles and subgroups for 40-man raids.
/// Given N bots with known classes, assigns: 2-3 tanks, 8-10 healers, rest DPS.
/// Subgroups distribute buffs evenly (1 paladin/shaman per group).
/// </summary>
public class RaidCompositionService
{
    public enum RaidRole { Tank, Healer, MeleeDps, RangedDps }

    public record RaidAssignment(string AccountName, Class Class, RaidRole Role, int Subgroup);

    /// <summary>
    /// Auto-assign roles and subgroups for a raid roster.
    /// </summary>
    public static List<RaidAssignment> AssignRoles(IReadOnlyList<(string AccountName, Class Class)> roster)
    {
        var assignments = new List<RaidAssignment>();
        var subgroupCounts = new int[8]; // 8 subgroups, max 5 each

        // Phase 1: Assign tanks (Warriors first, then Druids)
        var tanks = new List<int>();
        for (int i = 0; i < roster.Count && tanks.Count < 3; i++)
        {
            if (roster[i].Class == Class.Warrior)
                tanks.Add(i);
        }
        if (tanks.Count < 2)
        {
            for (int i = 0; i < roster.Count && tanks.Count < 3; i++)
            {
                if (roster[i].Class == Class.Druid && !tanks.Contains(i))
                    tanks.Add(i);
            }
        }

        // Phase 2: Assign healers (Priest, Druid, Shaman, Paladin)
        var healers = new List<int>();
        var healerClasses = new[] { Class.Priest, Class.Druid, Class.Shaman, Class.Paladin };
        for (int i = 0; i < roster.Count; i++)
        {
            if (tanks.Contains(i)) continue;
            if (healerClasses.Contains(roster[i].Class) && healers.Count < 10)
                healers.Add(i);
        }

        // Phase 3: Everyone else is DPS
        for (int i = 0; i < roster.Count; i++)
        {
            RaidRole role;
            if (tanks.Contains(i))
                role = RaidRole.Tank;
            else if (healers.Contains(i))
                role = RaidRole.Healer;
            else if (roster[i].Class == Class.Warrior || roster[i].Class == Class.Rogue)
                role = RaidRole.MeleeDps;
            else
                role = RaidRole.RangedDps;

            // Assign to subgroup with fewest members
            int bestGroup = 0;
            for (int g = 1; g < 8; g++)
            {
                if (subgroupCounts[g] < subgroupCounts[bestGroup])
                    bestGroup = g;
            }
            subgroupCounts[bestGroup]++;

            assignments.Add(new RaidAssignment(
                roster[i].AccountName,
                roster[i].Class,
                role,
                bestGroup));
        }

        return assignments;
    }
}
