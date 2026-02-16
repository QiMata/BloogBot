using System;
namespace GameData.Core.Enums;

[Flags]
public enum SpellInterruptFlags
{
    SPELL_INTERRUPT_FLAG_MOVEMENT = 0x01,
    SPELL_INTERRUPT_FLAG_DAMAGE = 0x02,
    SPELL_INTERRUPT_FLAG_INTERRUPT = 0x04,
    SPELL_INTERRUPT_FLAG_AUTOATTACK = 0x08,
    SPELL_INTERRUPT_FLAG_ABORT_ON_DMG = 0x10               // _complete_ interrupt on direct damage
    // SPELL_INTERRUPT_UNK               = 0x20               // unk, 564 of 727 spells having this spell start with "Glyph"
}