using System.ComponentModel;

namespace GameData.Core.Enums;

public enum Race
{
    [Description("None")]
    None,
    [Description("Human")]
    Human,
    [Description("Orc")]
    Orc,
    [Description("Dwarf")]
    Dwarf,
    [Description("Night Elf")]
    NightElf,
    [Description("Undead")]
    Undead,
    [Description("Tauren")]
    Tauren,
    [Description("Gnome")]
    Gnome,
    [Description("Troll")]
    Troll,
}