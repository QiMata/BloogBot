namespace GameData.Core.Enums;

/// <summary>
/// MaNGOS death state enumeration (shared between creatures and players).
/// For players: ALIVE → JUST_DIED → CORPSE (ghost form, corpse in world) → DEAD (corpse despawned).
/// Player transitions: CORPSE while ghost timer active (corpseRecoveryDelaySeconds > 0),
/// DEAD once corpse despawns (death timer expired). Use Health == 0 + ghost flag for runtime checks.
/// For creatures: ALIVE → JUST_DIED → CORPSE (lootable) → DEAD (despawned).
/// </summary>
public enum DeathState
{
    ALIVE = 0,          // Living — Health > 0, no ghost flag
    JUST_DIED = 1,      // Transient frame after death. Creature: auto-converts to CORPSE. Player: converts at next update.
    CORPSE = 2,         // Player: ghost form active, corpse object exists in world, can retrieve. Creature: lootable corpse.
    DEAD = 3,           // Player: corpse despawned (death timer expired), must use spirit healer. Creature: fully despawned.
    JUST_ALIVED = 4     // Transient frame after resurrection. Auto-converts to ALIVE at next update.
};