namespace GameData.Core.Enums;

public enum VictimState
{
    VICTIMSTATE_UNAFFECTED = 0,                         // seen in relation with HITINFO_MISS
    VICTIMSTATE_NORMAL = 1,
    VICTIMSTATE_DODGE = 2,
    VICTIMSTATE_PARRY = 3,
    VICTIMSTATE_INTERRUPT = 4,
    VICTIMSTATE_BLOCKS = 5,
    VICTIMSTATE_EVADES = 6,
    VICTIMSTATE_IS_IMMUNE = 7,
    VICTIMSTATE_DEFLECTS = 8
}