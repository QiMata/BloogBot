namespace GameData.Core.Enums;

public enum MeleeHitOutcome
{
    MELEE_HIT_EVADE = 0,
    MELEE_HIT_MISS = 1,
    MELEE_HIT_DODGE = 2,     ///< used as misc in SPELL_AURA_IGNORE_COMBAT_RESULT
    MELEE_HIT_BLOCK = 3,     ///< used as misc in SPELL_AURA_IGNORE_COMBAT_RESULT
    MELEE_HIT_PARRY = 4,     ///< used as misc in SPELL_AURA_IGNORE_COMBAT_RESULT
    MELEE_HIT_GLANCING = 5,
    MELEE_HIT_CRIT = 6,
    MELEE_HIT_CRUSHING = 7,
    MELEE_HIT_NORMAL = 8,
    MELEE_HIT_BLOCK_CRIT = 9,
}