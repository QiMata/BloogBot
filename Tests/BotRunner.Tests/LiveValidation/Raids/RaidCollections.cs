using BotRunner.Travel;
using Xunit;

namespace BotRunner.Tests.LiveValidation.Raids;

// =========================================================================
// Fixture + Collection definitions for all vanilla raids.
// Each uses DungeonInstanceFixture (10 bots, raid-converts after group form).
// =========================================================================

// --- Zul'Gurub (20-man) ---
public class ZulGurubFixture : DungeonInstanceFixture
{
    public ZulGurubFixture()
    {
        Dungeon = new DungeonEntryData.DungeonDefinition(
            RaidEntryData.ZulGurub.Name, RaidEntryData.ZulGurub.Abbreviation,
            RaidEntryData.ZulGurub.InstanceMapId, RaidEntryData.ZulGurub.EntranceMapId,
            RaidEntryData.ZulGurub.EntrancePosition, RaidEntryData.ZulGurub.InstanceEntryPosition, RaidEntryData.ZulGurub.MeetingStonePosition,
            RaidEntryData.ZulGurub.MeetingStoneMapId,
            RaidEntryData.ZulGurub.MinLevel, 60, RaidEntryData.ZulGurub.MaxPlayers,
            RaidEntryData.ZulGurub.Faction);
        AccountPrefix = "ZGBOT";
    }
}
[CollectionDefinition(Name)]
public class ZulGurubCollection : ICollectionFixture<ZulGurubFixture>
{ public const string Name = "ZulGurubValidation"; }

// --- Ruins of Ahn'Qiraj (20-man) ---
public class AQ20Fixture : DungeonInstanceFixture
{
    public AQ20Fixture()
    {
        Dungeon = new DungeonEntryData.DungeonDefinition(
            RaidEntryData.RuinsOfAhnQiraj.Name, RaidEntryData.RuinsOfAhnQiraj.Abbreviation,
            RaidEntryData.RuinsOfAhnQiraj.InstanceMapId, RaidEntryData.RuinsOfAhnQiraj.EntranceMapId,
            RaidEntryData.RuinsOfAhnQiraj.EntrancePosition, RaidEntryData.RuinsOfAhnQiraj.InstanceEntryPosition, null, null,
            RaidEntryData.RuinsOfAhnQiraj.MinLevel, 60, RaidEntryData.RuinsOfAhnQiraj.MaxPlayers,
            RaidEntryData.RuinsOfAhnQiraj.Faction);
        AccountPrefix = "AQ20BOT";
    }
}
[CollectionDefinition(Name)]
public class AQ20Collection : ICollectionFixture<AQ20Fixture>
{ public const string Name = "AQ20Validation"; }

// --- Molten Core (40-man) ---
public class MoltenCoreFixture : DungeonInstanceFixture
{
    public MoltenCoreFixture()
    {
        Dungeon = new DungeonEntryData.DungeonDefinition(
            RaidEntryData.MoltenCore.Name, RaidEntryData.MoltenCore.Abbreviation,
            RaidEntryData.MoltenCore.InstanceMapId, RaidEntryData.MoltenCore.EntranceMapId,
            RaidEntryData.MoltenCore.EntrancePosition, RaidEntryData.MoltenCore.InstanceEntryPosition, RaidEntryData.MoltenCore.MeetingStonePosition,
            RaidEntryData.MoltenCore.MeetingStoneMapId,
            RaidEntryData.MoltenCore.MinLevel, 60, RaidEntryData.MoltenCore.MaxPlayers,
            RaidEntryData.MoltenCore.Faction);
        AccountPrefix = "MCBOT";
    }
}
[CollectionDefinition(Name)]
public class MoltenCoreCollection : ICollectionFixture<MoltenCoreFixture>
{ public const string Name = "MoltenCoreValidation"; }

// --- Onyxia's Lair (40-man) ---
public class OnyxiasLairFixture : DungeonInstanceFixture
{
    public OnyxiasLairFixture()
    {
        Dungeon = new DungeonEntryData.DungeonDefinition(
            RaidEntryData.OnyxiasLair.Name, RaidEntryData.OnyxiasLair.Abbreviation,
            RaidEntryData.OnyxiasLair.InstanceMapId, RaidEntryData.OnyxiasLair.EntranceMapId,
            RaidEntryData.OnyxiasLair.EntrancePosition, RaidEntryData.OnyxiasLair.InstanceEntryPosition, null, null,
            RaidEntryData.OnyxiasLair.MinLevel, 60, RaidEntryData.OnyxiasLair.MaxPlayers,
            RaidEntryData.OnyxiasLair.Faction);
        AccountPrefix = "ONYBOT";
    }
}
[CollectionDefinition(Name)]
public class OnyxiasLairCollection : ICollectionFixture<OnyxiasLairFixture>
{ public const string Name = "OnyxiasLairValidation"; }

// --- Blackwing Lair (40-man) ---
public class BlackwingLairFixture : DungeonInstanceFixture
{
    public BlackwingLairFixture()
    {
        Dungeon = new DungeonEntryData.DungeonDefinition(
            RaidEntryData.BlackwingLair.Name, RaidEntryData.BlackwingLair.Abbreviation,
            RaidEntryData.BlackwingLair.InstanceMapId, RaidEntryData.BlackwingLair.EntranceMapId,
            RaidEntryData.BlackwingLair.EntrancePosition, RaidEntryData.BlackwingLair.InstanceEntryPosition, RaidEntryData.BlackwingLair.MeetingStonePosition,
            RaidEntryData.BlackwingLair.MeetingStoneMapId,
            RaidEntryData.BlackwingLair.MinLevel, 60, RaidEntryData.BlackwingLair.MaxPlayers,
            RaidEntryData.BlackwingLair.Faction);
        AccountPrefix = "BWLBOT";
    }
}
[CollectionDefinition(Name)]
public class BlackwingLairCollection : ICollectionFixture<BlackwingLairFixture>
{ public const string Name = "BlackwingLairValidation"; }

// --- Temple of Ahn'Qiraj (40-man) ---
public class AQ40Fixture : DungeonInstanceFixture
{
    public AQ40Fixture()
    {
        Dungeon = new DungeonEntryData.DungeonDefinition(
            RaidEntryData.TempleOfAhnQiraj.Name, RaidEntryData.TempleOfAhnQiraj.Abbreviation,
            RaidEntryData.TempleOfAhnQiraj.InstanceMapId, RaidEntryData.TempleOfAhnQiraj.EntranceMapId,
            RaidEntryData.TempleOfAhnQiraj.EntrancePosition, RaidEntryData.TempleOfAhnQiraj.InstanceEntryPosition, null, null,
            RaidEntryData.TempleOfAhnQiraj.MinLevel, 60, RaidEntryData.TempleOfAhnQiraj.MaxPlayers,
            RaidEntryData.TempleOfAhnQiraj.Faction);
        AccountPrefix = "AQ40BOT";
    }
}
[CollectionDefinition(Name)]
public class AQ40Collection : ICollectionFixture<AQ40Fixture>
{ public const string Name = "AQ40Validation"; }

// --- Naxxramas (40-man) ---
public class NaxxramasFixture : DungeonInstanceFixture
{
    public NaxxramasFixture()
    {
        Dungeon = new DungeonEntryData.DungeonDefinition(
            RaidEntryData.Naxxramas.Name, RaidEntryData.Naxxramas.Abbreviation,
            RaidEntryData.Naxxramas.InstanceMapId, RaidEntryData.Naxxramas.EntranceMapId,
            RaidEntryData.Naxxramas.EntrancePosition, RaidEntryData.Naxxramas.InstanceEntryPosition, null, null,
            RaidEntryData.Naxxramas.MinLevel, 60, RaidEntryData.Naxxramas.MaxPlayers,
            RaidEntryData.Naxxramas.Faction);
        AccountPrefix = "NAXXBOT";
    }
}
[CollectionDefinition(Name)]
public class NaxxramasCollection : ICollectionFixture<NaxxramasFixture>
{ public const string Name = "NaxxramasValidation"; }
