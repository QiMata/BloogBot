using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Travel;

public enum TeleportSpellType { SelfTeleport, GroupPortal }

/// <summary>
/// A mage teleport or portal spell with destination coordinates and metadata.
/// </summary>
public record MageTeleportSpell(
    int SpellId,
    string DestinationName,
    uint DestinationMapId,
    float X, float Y, float Z,
    TeleportSpellType Type,
    int? ReagentItemId,
    int CooldownSeconds,
    string Faction);

/// <summary>
/// Static database of all mage teleport and portal spells in vanilla WoW 1.12.1.
/// Self-teleports have a ~10 minute cooldown and require Rune of Teleportation (17031).
/// Group portals have a ~1 minute cooldown and require Rune of Portals (17032).
/// </summary>
public static class MageTeleportData
{
    /// <summary>Rune of Teleportation item ID, consumed by all self-teleport spells.</summary>
    public const int RuneOfTeleportation = 17031;

    /// <summary>Rune of Portals item ID, consumed by all portal spells.</summary>
    public const int RuneOfPortals = 17032;

    private static readonly List<MageTeleportSpell> _allSpells =
    [
        // =====================================================================
        // SELF TELEPORTS (~10 min cooldown, requires Rune of Teleportation 17031)
        // =====================================================================

        // Horde
        new(3567, "Orgrimmar",     1, 1676f,  -4315f,  61f,  TeleportSpellType.SelfTeleport, RuneOfTeleportation, 600, "Horde"),
        new(3563, "Undercity",     0, 1586f,    239f, -52f,  TeleportSpellType.SelfTeleport, RuneOfTeleportation, 600, "Horde"),
        new(3566, "Thunder Bluff", 1, -1278f,   127f, 131f,  TeleportSpellType.SelfTeleport, RuneOfTeleportation, 600, "Horde"),

        // Alliance
        new(3561, "Stormwind",  0, -8913f,  554f,   94f,  TeleportSpellType.SelfTeleport, RuneOfTeleportation, 600, "Alliance"),
        new(3562, "Ironforge",  0, -4981f, -881f,  502f,  TeleportSpellType.SelfTeleport, RuneOfTeleportation, 600, "Alliance"),
        new(3565, "Darnassus",  1,  9947f, 2482f, 1316f,  TeleportSpellType.SelfTeleport, RuneOfTeleportation, 600, "Alliance"),

        // =====================================================================
        // GROUP PORTALS (~1 min cooldown, requires Rune of Portals 17032)
        // =====================================================================

        // Horde
        new(11417, "Orgrimmar",     1, 1676f,  -4315f,  61f,  TeleportSpellType.GroupPortal, RuneOfPortals, 60, "Horde"),
        new(11418, "Undercity",     0, 1586f,    239f, -52f,  TeleportSpellType.GroupPortal, RuneOfPortals, 60, "Horde"),
        new(11420, "Thunder Bluff", 1, -1278f,   127f, 131f,  TeleportSpellType.GroupPortal, RuneOfPortals, 60, "Horde"),

        // Alliance
        new(10059, "Stormwind",  0, -8913f,  554f,   94f,  TeleportSpellType.GroupPortal, RuneOfPortals, 60, "Alliance"),
        new(11416, "Ironforge",  0, -4981f, -881f,  502f,  TeleportSpellType.GroupPortal, RuneOfPortals, 60, "Alliance"),
        new(11419, "Darnassus",  1,  9947f, 2482f, 1316f,  TeleportSpellType.GroupPortal, RuneOfPortals, 60, "Alliance"),
    ];

    /// <summary>Returns all mage teleport and portal spells.</summary>
    public static IReadOnlyList<MageTeleportSpell> GetAllSpells() => _allSpells;

    /// <summary>Returns spells available to a given faction (includes faction-neutral if any).</summary>
    public static IReadOnlyList<MageTeleportSpell> GetSpellsForFaction(string faction) =>
        _allSpells.Where(s =>
            s.Faction.Equals(faction, System.StringComparison.OrdinalIgnoreCase) ||
            s.Faction.Equals("Both", System.StringComparison.OrdinalIgnoreCase))
        .ToList();

    /// <summary>Finds a self-teleport spell to the named destination for the given faction.</summary>
    public static MageTeleportSpell? FindTeleportTo(string destinationName, string faction) =>
        _allSpells.FirstOrDefault(s =>
            s.Type == TeleportSpellType.SelfTeleport &&
            s.DestinationName.Equals(destinationName, System.StringComparison.OrdinalIgnoreCase) &&
            (s.Faction.Equals(faction, System.StringComparison.OrdinalIgnoreCase) ||
             s.Faction.Equals("Both", System.StringComparison.OrdinalIgnoreCase)));

    /// <summary>Finds a group portal spell to the named destination for the given faction.</summary>
    public static MageTeleportSpell? FindPortalTo(string destinationName, string faction) =>
        _allSpells.FirstOrDefault(s =>
            s.Type == TeleportSpellType.GroupPortal &&
            s.DestinationName.Equals(destinationName, System.StringComparison.OrdinalIgnoreCase) &&
            (s.Faction.Equals(faction, System.StringComparison.OrdinalIgnoreCase) ||
             s.Faction.Equals("Both", System.StringComparison.OrdinalIgnoreCase)));

    /// <summary>Finds a spell by its spell ID.</summary>
    public static MageTeleportSpell? FindBySpellId(int spellId) =>
        _allSpells.FirstOrDefault(s => s.SpellId == spellId);
}
