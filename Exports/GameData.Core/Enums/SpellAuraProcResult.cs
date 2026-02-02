namespace GameData.Core.Enums;

public enum SpellAuraProcResult
{
    SPELL_AURA_PROC_OK = 0,                    // proc was processed, will remove charges
    SPELL_AURA_PROC_FAILED = 1,                    // proc failed - if at least one aura failed the proc, charges won't be taken
    SPELL_AURA_PROC_CANT_TRIGGER = 2                     // aura can't trigger - skip charges taking, move to next aura if exists
}