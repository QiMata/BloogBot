using System;

namespace GameData.Core.Enums;

/**
     * internal state flags for some auras and movement generators, other. (Taken from comment)
     */
[Flags]
public enum UnitState : uint
{
    // persistent state (applied by aura/etc until expire)
    UNIT_STATE_MELEE_ATTACKING = 0x00000001,                 // unit is melee attacking someone Unit::Attack
    UNIT_STATE_ATTACK_PLAYER = 0x00000002,                 // unit attack player or player's controlled unit and have contested pvpv timer setup, until timer expire, combat end and etc
    UNIT_STATE_DIED = 0x00000004,                 // Unit::SetFeignDeath
    UNIT_STATE_STUNNED = 0x00000008,                 // Aura::HandleAuraModStun
    UNIT_STATE_ROOT = 0x00000010,                 // Aura::HandleAuraModRoot
    UNIT_STATE_ISOLATED = 0x00000020,                 // area auras do not affect other players, Aura::HandleAuraModSchoolImmunity
    UNIT_STATE_CONTROLLED = 0x00000040,                 // Aura::HandleAuraModPossess

    // persistent movement generator state (all time while movement generator applied to unit (independent from top state of movegen)
    UNIT_STATE_TAXI_FLIGHT = 0x00000080,                 // player is in flight mode (in fact interrupted at far teleport until next map telport landing)
    UNIT_STATE_DISTRACTED = 0x00000100,                 // DistractedMovementGenerator active

    // persistent movement generator state with non-persistent mirror states for stop support
    // (can be removed temporary by stop command or another movement generator apply)
    // not use _MOVE versions for generic movegen state, it can be removed temporary for unit stop and etc
    UNIT_STATE_CONFUSED = 0x00000200,                 // ConfusedMovementGenerator active/onstack
    UNIT_STATE_CONFUSED_MOVE = 0x00000400,
    UNIT_STATE_ROAMING = 0x00000800,                 // RandomMovementGenerator/PointMovementGenerator/WaypointMovementGenerator active (now always set)
    UNIT_STATE_ROAMING_MOVE = 0x00001000,
    UNIT_STATE_CHASE = 0x00002000,                 // ChaseMovementGenerator active
    UNIT_STATE_CHASE_MOVE = 0x00004000,
    UNIT_STATE_FOLLOW = 0x00008000,                 // FollowMovementGenerator active
    UNIT_STATE_FOLLOW_MOVE = 0x00010000,
    UNIT_STATE_FLEEING = 0x00020000,                 // FleeMovementGenerator/TimedFleeingMovementGenerator active/onstack
    UNIT_STATE_FLEEING_MOVE = 0x00040000,
    // More room for other MMGens

    // High-Level states (usually only with Creatures)
    UNIT_STATE_NO_COMBAT_MOVEMENT = 0x01000000,           // Combat Movement for MoveChase stopped
    UNIT_STATE_RUNNING = 0x02000000,           // SetRun for waypoints and such
    UNIT_STATE_WAYPOINT_PAUSED = 0x04000000,           // Waypoint-Movement paused genericly (ie by script)

    UNIT_STATE_IGNORE_PATHFINDING = 0x10000000,           // do not use pathfinding in any MovementGenerator

    // masks (only for check)

    // can't move currently
    UNIT_STATE_CAN_NOT_MOVE = UNIT_STATE_ROOT | UNIT_STATE_STUNNED | UNIT_STATE_DIED,

    // stay by different reasons
    UNIT_STATE_NOT_MOVE = UNIT_STATE_ROOT | UNIT_STATE_STUNNED | UNIT_STATE_DIED |
                          UNIT_STATE_DISTRACTED,

    // stay or scripted movement for effect( = in player case you can't move by client command)
    UNIT_STATE_NO_FREE_MOVE = UNIT_STATE_ROOT | UNIT_STATE_STUNNED | UNIT_STATE_DIED |
                              UNIT_STATE_TAXI_FLIGHT |
                              UNIT_STATE_CONFUSED | UNIT_STATE_FLEEING,

    // not react at move in sight or other
    UNIT_STATE_CAN_NOT_REACT = UNIT_STATE_STUNNED | UNIT_STATE_DIED |
                               UNIT_STATE_CONFUSED | UNIT_STATE_FLEEING,

    // AI disabled by some reason
    UNIT_STATE_LOST_CONTROL = UNIT_STATE_CONFUSED | UNIT_STATE_FLEEING | UNIT_STATE_CONTROLLED,

    // above 2 state cases
    UNIT_STATE_CAN_NOT_REACT_OR_LOST_CONTROL = UNIT_STATE_CAN_NOT_REACT | UNIT_STATE_LOST_CONTROL,

    // masks (for check or reset)

    // for real move using movegen check and stop (except unstoppable flight)
    UNIT_STATE_MOVING = UNIT_STATE_ROAMING_MOVE | UNIT_STATE_CHASE_MOVE | UNIT_STATE_FOLLOW_MOVE | UNIT_STATE_FLEEING_MOVE,

    UNIT_STATE_RUNNING_STATE = UNIT_STATE_CHASE_MOVE | UNIT_STATE_FLEEING_MOVE | UNIT_STATE_RUNNING,

    UNIT_STATE_ALL_STATE = 0xFFFFFFFF,
    UNIT_STATE_ALL_DYN_STATES = UNIT_STATE_ALL_STATE & ~(UNIT_STATE_NO_COMBAT_MOVEMENT | UNIT_STATE_RUNNING | UNIT_STATE_WAYPOINT_PAUSED | UNIT_STATE_IGNORE_PATHFINDING)
};