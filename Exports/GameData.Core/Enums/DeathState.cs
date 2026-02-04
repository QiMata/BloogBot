namespace GameData.Core.Enums;

public enum DeathState
{
    ALIVE = 0,     ///< show as alive
    JUST_DIED = 1,     ///< temporary state at die, for creature auto converted to CORPSE, for player at next update call
    CORPSE = 2,     ///< corpse state, for player this also meaning that player not leave corpse
    DEAD = 3,     ///< for creature despawned state (corpse despawned), for player CORPSE/DEAD not clear way switches (FIXME), and use m_deathtimer > 0 check for real corpse state
    JUST_ALIVED = 4      ///< temporary state at resurrection, for creature auto converted to ALIVE, for player at next update call
};