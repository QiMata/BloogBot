using System;

namespace GameData.Core.Enums;

[Flags]
public enum SpellChannelInterruptFlags
{
    CHANNEL_FLAG_DAMAGE = 0x0002,
    CHANNEL_FLAG_MOVEMENT = 0x0008,
    CHANNEL_FLAG_TURNING = 0x0010,
    CHANNEL_FLAG_DAMAGE2 = 0x0080,
    CHANNEL_FLAG_DELAY = 0x4000
}