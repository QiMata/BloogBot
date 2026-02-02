namespace GameData.Core.Enums;

[Flags]
public enum MovementFlags : uint
{
    MOVEFLAG_NONE = 0x00000000, // 0
    MOVEFLAG_FORWARD = 0x00000001, // 1
    MOVEFLAG_BACKWARD = 0x00000002, // 2
    MOVEFLAG_STRAFE_LEFT = 0x00000004, // 3
    MOVEFLAG_STRAFE_RIGHT = 0x00000008, // 4
    MOVEFLAG_TURN_LEFT = 0x00000010, // 5
    MOVEFLAG_TURN_RIGHT = 0x00000020, // 6
    MOVEFLAG_PITCH_UP = 0x00000040, // 7
    MOVEFLAG_PITCH_DOWN = 0x00000080, // 8
    MOVEFLAG_WALK_MODE = 0x00000100, // 9 Walking
    MOVEFLAG_UNUSED10 = 0x00000200, // 10 ??
    MOVEFLAG_LEVITATING = 0x00000400, // 11 ?? Seems not to work
    MOVEFLAG_FIXED_Z = 0x00000800, // 12 Fixed height. Jump => Glide across the entire map
    MOVEFLAG_ROOT = 0x00001000, // 13
    MOVEFLAG_JUMPING = 0x00002000, // 14
    MOVEFLAG_FALLINGFAR = 0x00004000, // 15
    MOVEFLAG_PENDING_STOP = 0x00008000, // 16 Only used in older client versions
    MOVEFLAG_PENDING_UNSTRAFE = 0x00010000, // 17 Only used in older client versions
    MOVEFLAG_PENDING_FORWARD = 0x00020000, // 18 Only used in older client versions
    MOVEFLAG_PENDING_BACKWARD = 0x00040000, // 19 Only used in older client versions
    MOVEFLAG_PENDING_STR_LEFT = 0x00080000, // 20 Only used in older client versions
    MOVEFLAG_PENDING_STR_RGHT = 0x00100000, // 21 Only used in older client versions
    MOVEFLAG_SWIMMING = 0x00200000, // 22 Ok
    MOVEFLAG_SPLINE_ENABLED = 0x00400000, // 23 Ok
    MOVEFLAG_MOVED = 0x00800000, // 24 Only used in older client versions
    MOVEFLAG_FLYING = 0x01000000, // 25 [-ZERO] is it really need and correct value
    MOVEFLAG_ONTRANSPORT = 0x02000000, // 26 Used for flying on some creatures
    MOVEFLAG_SPLINE_ELEVATION = 0x04000000, // 27 Used for flight paths
    MOVEFLAG_UNUSED28 = 0x08000000, // 28
    MOVEFLAG_WATERWALKING = 0x10000000, // 29 Prevent unit from falling through water
    MOVEFLAG_SAFE_FALL = 0x20000000, // 30 Active rogue safe fall spell (passive)
    MOVEFLAG_HOVER = 0x40000000, // 31
    MOVEFLAG_UNUSED32 = 0x80000000, // 32

    // Can not be present with MOVEFLAG_ROOT (otherwise client freeze)
    MOVEFLAG_MASK_MOVING =
        MOVEFLAG_FORWARD | MOVEFLAG_BACKWARD | MOVEFLAG_STRAFE_LEFT | MOVEFLAG_STRAFE_RIGHT |
        MOVEFLAG_PITCH_UP | MOVEFLAG_PITCH_DOWN | MOVEFLAG_JUMPING | MOVEFLAG_FALLINGFAR |
        MOVEFLAG_SPLINE_ELEVATION,
    MOVEFLAG_MASK_MOVING_OR_TURN = MOVEFLAG_MASK_MOVING | MOVEFLAG_TURN_LEFT | MOVEFLAG_TURN_RIGHT,

    // MovementFlags mask that only contains flags for x/z translations
    // this is to avoid that a jumping character that stands still triggers melee-leeway
    MOVEFLAG_MASK_XZ = MOVEFLAG_FORWARD | MOVEFLAG_BACKWARD | MOVEFLAG_STRAFE_LEFT | MOVEFLAG_STRAFE_RIGHT
};