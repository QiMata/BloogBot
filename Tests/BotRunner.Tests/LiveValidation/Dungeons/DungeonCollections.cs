using BotRunner.Travel;
using Xunit;

namespace BotRunner.Tests.LiveValidation.Dungeons;

// =========================================================================
// Fixture + Collection definitions for all vanilla dungeons.
// Each dungeon gets its own fixture (sets DungeonDefinition + AccountPrefix)
// and xUnit collection so tests can run independently.
// =========================================================================

// --- Shadowfang Keep ---
public class ShadowfangKeepFixture : DungeonInstanceFixture
{
    public ShadowfangKeepFixture() { Dungeon = DungeonEntryData.ShadowfangKeep; AccountPrefix = "SFKBOT"; }
}
[CollectionDefinition(Name)]
public class ShadowfangKeepCollection : ICollectionFixture<ShadowfangKeepFixture>
{ public const string Name = "ShadowfangKeepValidation"; }

// --- Blackfathom Deeps ---
public class BlackfathomDeepsFixture : DungeonInstanceFixture
{
    public BlackfathomDeepsFixture() { Dungeon = DungeonEntryData.BlackfathomDeeps; AccountPrefix = "BFDBOT"; }
}
[CollectionDefinition(Name)]
public class BlackfathomDeepsCollection : ICollectionFixture<BlackfathomDeepsFixture>
{ public const string Name = "BlackfathomDeepsValidation"; }

// --- Gnomeregan ---
public class GnomereganFixture : DungeonInstanceFixture
{
    public GnomereganFixture() { Dungeon = DungeonEntryData.Gnomeregan; AccountPrefix = "GNOMBOT"; }
}
[CollectionDefinition(Name)]
public class GnomereganCollection : ICollectionFixture<GnomereganFixture>
{ public const string Name = "GnomereganValidation"; }

// --- Razorfen Kraul ---
public class RazorfenKraulFixture : DungeonInstanceFixture
{
    public RazorfenKraulFixture() { Dungeon = DungeonEntryData.RazorfenKraul; AccountPrefix = "RFKBOT"; }
}
[CollectionDefinition(Name)]
public class RazorfenKraulCollection : ICollectionFixture<RazorfenKraulFixture>
{ public const string Name = "RazorfenKraulValidation"; }

// --- Scarlet Monastery (Cathedral — hardest wing) ---
public class ScarletMonasteryFixture : DungeonInstanceFixture
{
    public ScarletMonasteryFixture() { Dungeon = DungeonEntryData.ScarletMonasteryCathedral; AccountPrefix = "SMBOT"; }
}
[CollectionDefinition(Name)]
public class ScarletMonasteryCollection : ICollectionFixture<ScarletMonasteryFixture>
{ public const string Name = "ScarletMonasteryValidation"; }

// --- Razorfen Downs ---
public class RazorfenDownsFixture : DungeonInstanceFixture
{
    public RazorfenDownsFixture() { Dungeon = DungeonEntryData.RazorfenDowns; AccountPrefix = "RFDBOT"; }
}
[CollectionDefinition(Name)]
public class RazorfenDownsCollection : ICollectionFixture<RazorfenDownsFixture>
{ public const string Name = "RazorfenDownsValidation"; }

// --- Uldaman ---
public class UldamanFixture : DungeonInstanceFixture
{
    public UldamanFixture() { Dungeon = DungeonEntryData.Uldaman; AccountPrefix = "ULDBOT"; }
}
[CollectionDefinition(Name)]
public class UldamanCollection : ICollectionFixture<UldamanFixture>
{ public const string Name = "UldamanValidation"; }

// --- Zul'Farrak ---
public class ZulFarrakFixture : DungeonInstanceFixture
{
    public ZulFarrakFixture() { Dungeon = DungeonEntryData.ZulFarrak; AccountPrefix = "ZFBOT"; }
}
[CollectionDefinition(Name)]
public class ZulFarrakCollection : ICollectionFixture<ZulFarrakFixture>
{ public const string Name = "ZulFarrakValidation"; }

// --- Maraudon ---
public class MaraudonFixture : DungeonInstanceFixture
{
    public MaraudonFixture() { Dungeon = DungeonEntryData.Maraudon; AccountPrefix = "MARABOT"; }
}
[CollectionDefinition(Name)]
public class MaraudonCollection : ICollectionFixture<MaraudonFixture>
{ public const string Name = "MaraudonValidation"; }

// --- Sunken Temple ---
public class SunkenTempleFixture : DungeonInstanceFixture
{
    public SunkenTempleFixture() { Dungeon = DungeonEntryData.SunkenTemple; AccountPrefix = "STBOT"; }
}
[CollectionDefinition(Name)]
public class SunkenTempleCollection : ICollectionFixture<SunkenTempleFixture>
{ public const string Name = "SunkenTempleValidation"; }

// --- Blackrock Depths ---
public class BlackrockDepthsFixture : DungeonInstanceFixture
{
    public BlackrockDepthsFixture() { Dungeon = DungeonEntryData.BlackrockDepths; AccountPrefix = "BRDBOT"; }
}
[CollectionDefinition(Name)]
public class BlackrockDepthsCollection : ICollectionFixture<BlackrockDepthsFixture>
{ public const string Name = "BlackrockDepthsValidation"; }

// --- Lower Blackrock Spire ---
public class LowerBlackrockSpireFixture : DungeonInstanceFixture
{
    public LowerBlackrockSpireFixture() { Dungeon = DungeonEntryData.LowerBlackrockSpire; AccountPrefix = "LBRSBOT"; }
}
[CollectionDefinition(Name)]
public class LowerBlackrockSpireCollection : ICollectionFixture<LowerBlackrockSpireFixture>
{ public const string Name = "LowerBlackrockSpireValidation"; }

// --- Upper Blackrock Spire ---
public class UpperBlackrockSpireFixture : DungeonInstanceFixture
{
    public UpperBlackrockSpireFixture() { Dungeon = DungeonEntryData.UpperBlackrockSpire; AccountPrefix = "UBRSBOT"; }
}
[CollectionDefinition(Name)]
public class UpperBlackrockSpireCollection : ICollectionFixture<UpperBlackrockSpireFixture>
{ public const string Name = "UpperBlackrockSpireValidation"; }

// --- Dire Maul (East) ---
public class DireMaulEastFixture : DungeonInstanceFixture
{
    public DireMaulEastFixture() { Dungeon = DungeonEntryData.DireMaulEast; AccountPrefix = "DMEBOT"; }
}
[CollectionDefinition(Name)]
public class DireMaulEastCollection : ICollectionFixture<DireMaulEastFixture>
{ public const string Name = "DireMaulEastValidation"; }

// --- Dire Maul (West) ---
public class DireMaulWestFixture : DungeonInstanceFixture
{
    public DireMaulWestFixture() { Dungeon = DungeonEntryData.DireMaulWest; AccountPrefix = "DMWBOT"; }
}
[CollectionDefinition(Name)]
public class DireMaulWestCollection : ICollectionFixture<DireMaulWestFixture>
{ public const string Name = "DireMaulWestValidation"; }

// --- Dire Maul (North) ---
public class DireMaulNorthFixture : DungeonInstanceFixture
{
    public DireMaulNorthFixture() { Dungeon = DungeonEntryData.DireMaulNorth; AccountPrefix = "DMNBOT"; }
}
[CollectionDefinition(Name)]
public class DireMaulNorthCollection : ICollectionFixture<DireMaulNorthFixture>
{ public const string Name = "DireMaulNorthValidation"; }

// --- Stratholme (Living) ---
public class StratholmeLivingFixture : DungeonInstanceFixture
{
    public StratholmeLivingFixture() { Dungeon = DungeonEntryData.StratholmeLiving; AccountPrefix = "STRLBOT"; }
}
[CollectionDefinition(Name)]
public class StratholmeLivingCollection : ICollectionFixture<StratholmeLivingFixture>
{ public const string Name = "StratholmeLivingValidation"; }

// --- Stratholme (Undead) ---
public class StratholmeUndeadFixture : DungeonInstanceFixture
{
    public StratholmeUndeadFixture() { Dungeon = DungeonEntryData.StratholmeUndead; AccountPrefix = "STRUBOT"; }
}
[CollectionDefinition(Name)]
public class StratholmeUndeadCollection : ICollectionFixture<StratholmeUndeadFixture>
{ public const string Name = "StratholmeUndeadValidation"; }

// --- Scholomance ---
public class ScholomanceFixture : DungeonInstanceFixture
{
    public ScholomanceFixture() { Dungeon = DungeonEntryData.Scholomance; AccountPrefix = "SCHOLBOT"; }
}
[CollectionDefinition(Name)]
public class ScholomanceCollection : ICollectionFixture<ScholomanceFixture>
{ public const string Name = "ScholomanceValidation"; }
