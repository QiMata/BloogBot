using Database;

namespace BotRunner.Progression;

/// <summary>
/// Scores items from the MaNGOS item_template table against a <see cref="StatWeightProfile"/>.
/// Handles the 10 stat slots, armor, and weapon DPS from the protobuf <see cref="ItemTemplate"/>.
/// </summary>
public static class ItemScorer
{
    /// <summary>
    /// WoW 1.12.1 item_template stat_type IDs.
    /// See: https://mangoszero-docs.github.io/schema/mangos/item_template.html
    /// </summary>
    private const uint STAT_MANA = 0;
    private const uint STAT_HEALTH = 1;
    private const uint STAT_AGILITY = 3;
    private const uint STAT_STRENGTH = 4;
    private const uint STAT_INTELLECT = 5;
    private const uint STAT_SPIRIT = 6;
    private const uint STAT_STAMINA = 7;

    // Extended stat types found on items (item_template stat_type values 12+)
    private const uint STAT_DEFENSE_SKILL_RATING = 12;
    private const uint STAT_DODGE_RATING = 13;
    private const uint STAT_PARRY_RATING = 14;
    private const uint STAT_BLOCK_RATING = 15;
    private const uint STAT_HIT_MELEE_RATING = 16;
    private const uint STAT_HIT_RANGED_RATING = 17;
    private const uint STAT_HIT_SPELL_RATING = 18;
    private const uint STAT_CRIT_MELEE_RATING = 19;
    private const uint STAT_CRIT_RANGED_RATING = 20;
    private const uint STAT_CRIT_SPELL_RATING = 21;
    private const uint STAT_HIT_RATING = 31;
    private const uint STAT_CRIT_RATING = 32;
    private const uint STAT_RESILIENCE_RATING = 35;
    private const uint STAT_HASTE_RATING = 36;

    /// <summary>
    /// Computes a weighted score for the given item template against the provided stat weight profile.
    /// Higher scores indicate items that are more desirable for the spec.
    /// </summary>
    /// <param name="item">The item template from MaNGOS item_template table.</param>
    /// <param name="weights">The stat weight profile for the target spec.</param>
    /// <returns>A float score; higher is better. Returns 0 for null items.</returns>
    public static float ScoreItem(ItemTemplate? item, StatWeightProfile weights)
    {
        if (item == null) return 0f;

        float score = 0f;

        // Score all 10 stat slots
        foreach (var stat in item.Stats)
        {
            if (stat.Value == 0) continue;
            score += GetStatWeight(stat.Type, weights) * stat.Value;
        }

        // Score armor
        score += item.Armor * weights.Armor;

        // Score weapon DPS from the first damage slot (primary damage)
        if (item.Damages.Count > 0 && item.Delay > 0)
        {
            float totalDps = 0f;
            foreach (var dmg in item.Damages)
            {
                if (dmg.Min <= 0 && dmg.Max <= 0) continue;
                totalDps += (dmg.Min + dmg.Max) / 2.0f / (item.Delay / 1000.0f);
            }
            score += totalDps * weights.DpsWeight;
        }

        return score;
    }

    /// <summary>
    /// Maps a WoW stat_type ID to its corresponding weight in the profile.
    /// </summary>
    private static float GetStatWeight(uint statType, StatWeightProfile weights)
    {
        return statType switch
        {
            STAT_MANA => 0f,     // Raw mana is generally not weighted
            STAT_HEALTH => 0f,   // Raw health is generally not weighted
            STAT_AGILITY => weights.Agility,
            STAT_STRENGTH => weights.Strength,
            STAT_INTELLECT => weights.Intellect,
            STAT_SPIRIT => weights.Spirit,
            STAT_STAMINA => weights.Stamina,
            STAT_DEFENSE_SKILL_RATING => weights.DodgeRating * 0.5f, // Defense contributes partially to avoidance
            STAT_DODGE_RATING => weights.DodgeRating,
            STAT_PARRY_RATING => weights.ParryRating,
            STAT_BLOCK_RATING => weights.BlockRating,
            STAT_HIT_MELEE_RATING => weights.HitRating,
            STAT_HIT_RANGED_RATING => weights.HitRating,
            STAT_HIT_SPELL_RATING => weights.HitRating,
            STAT_CRIT_MELEE_RATING => weights.CritRating,
            STAT_CRIT_RANGED_RATING => weights.CritRating,
            STAT_CRIT_SPELL_RATING => weights.CritRating,
            STAT_HIT_RATING => weights.HitRating,
            STAT_CRIT_RATING => weights.CritRating,
            _ => 0f
        };
    }
}
