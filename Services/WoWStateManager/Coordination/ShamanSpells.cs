using Google.Protobuf.Collections;

namespace WoWStateManager.Coordination;

/// <summary>
/// Vanilla 1.12.1 shaman spell IDs by category, ordered highest rank first.
/// At runtime, cross-reference with the shaman's spellList from the snapshot
/// to find the highest available rank.
/// </summary>
public static class ShamanSpells
{
    // Healing Wave — primary heal (ranks 1-10, highest first)
    public static readonly uint[] HealingWave =
        [25357, 10396, 10395, 8005, 959, 939, 913, 547, 332, 331];

    // Lesser Healing Wave — fast heal (ranks 1-6, highest first)
    public static readonly uint[] LesserHealingWave =
        [10468, 10467, 10466, 6375, 8008, 8004];

    // Lightning Bolt — primary damage (ranks 1-10, highest first)
    public static readonly uint[] LightningBolt =
        [15208, 15207, 10392, 10391, 6041, 943, 915, 548, 529, 403];

    // Earth Shock — interrupt + damage (ranks 1-7, highest first)
    public static readonly uint[] EarthShock =
        [10414, 10413, 10412, 8046, 8045, 8044, 8042];

    // Flame Shock — DoT damage (ranks 1-6, highest first)
    public static readonly uint[] FlameShock =
        [29228, 10448, 10447, 8053, 8052, 8050];

    // Frost Shock — slow + damage (ranks 1-4, highest first)
    public static readonly uint[] FrostShock =
        [10473, 10472, 8058, 8056];

    /// <summary>
    /// Finds the highest rank spell ID the character knows from a given spell category.
    /// Returns 0 if the character doesn't know any rank.
    /// </summary>
    public static uint FindBestSpell(uint[] spellCategory, RepeatedField<uint> knownSpells)
    {
        foreach (var spellId in spellCategory)
        {
            if (knownSpells.Contains(spellId))
                return spellId;
        }
        return 0;
    }
}
