using GameData.Core.Enums;
using GameData.Core.Models;

namespace BotRunner.Tests.Combat;

public class SpellModelTests
{
    [Fact]
    public void Constructor_StoresAllProperties()
    {
        var spell = new Spell(1126, 100, "Mark of the Wild", "Increases stats", "Druid buff");

        Assert.Equal(1126u, spell.Id);
        Assert.Equal(100u, spell.Cost);
        Assert.Equal("Mark of the Wild", spell.Name);
        Assert.Equal("Increases stats", spell.Description);
        Assert.Equal("Druid buff", spell.Tooltip);
    }

    [Fact]
    public void Clone_CopiesAllProperties()
    {
        var original = new Spell(5308, 15, "Execute", "Finisher", "Kill them");
        var clone = original.Clone();

        Assert.Equal(original.Id, clone.Id);
        Assert.Equal(original.Cost, clone.Cost);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.Description, clone.Description);
        Assert.Equal(original.Tooltip, clone.Tooltip);
    }

    [Fact]
    public void Clone_IsIndependentInstance()
    {
        var original = new Spell(100, 50, "Test", "Desc", "Tip");
        var clone = original.Clone();

        Assert.NotSame(original, clone);
    }

    [Fact]
    public void Constructor_ZeroCost()
    {
        var spell = new Spell(1, 0, "Auto Attack", "", "");

        Assert.Equal(0u, spell.Cost);
    }

    [Fact]
    public void Constructor_EmptyStrings()
    {
        var spell = new Spell(1, 0, "", "", "");

        Assert.Equal(string.Empty, spell.Name);
        Assert.Equal(string.Empty, spell.Description);
        Assert.Equal(string.Empty, spell.Tooltip);
    }

    [Fact]
    public void Constructor_MaxValues()
    {
        var spell = new Spell(uint.MaxValue, uint.MaxValue, "Max", "Max", "Max");

        Assert.Equal(uint.MaxValue, spell.Id);
        Assert.Equal(uint.MaxValue, spell.Cost);
    }
}

public class SpellEffectModelTests
{
    [Fact]
    public void Constructor_StoresProperties()
    {
        var effect = new SpellEffect("spell_fire_fireball", 3, EffectType.Magic);

        Assert.Equal("spell_fire_fireball", effect.Icon);
        Assert.Equal(3u, effect.StackCount);
        Assert.Equal(EffectType.Magic, effect.Type);
    }

    [Fact]
    public void Constructor_ZeroStacks()
    {
        var effect = new SpellEffect("icon", 0, EffectType.Poison);

        Assert.Equal(0u, effect.StackCount);
    }
}

public class CooldownModelTests
{
    [Fact]
    public void Constructor_StoresProperties()
    {
        var cd = new Cooldown("spell_frost_frostbolt", 1, EffectType.Magic);

        Assert.Equal("spell_frost_frostbolt", cd.Icon);
        Assert.Equal(1u, cd.StackCount);
        Assert.Equal(EffectType.Magic, cd.Type);
    }
}

public class CharacterSelectTests
{
    [Fact]
    public void DefaultValues()
    {
        var cs = new CharacterSelect();

        Assert.Equal(0UL, cs.Guid);
        Assert.Equal(string.Empty, cs.Name);
        Assert.Equal(0, (int)cs.Race);
        Assert.Equal(0, (int)cs.Class);
        Assert.Equal(0, (int)cs.Gender);
        Assert.Equal(0, cs.Skin);
        Assert.Equal(0, cs.Face);
        Assert.Equal(0, cs.HairStyle);
        Assert.Equal(0, cs.HairColor);
        Assert.Equal(0, cs.FacialHair);
        Assert.Equal(0, cs.Level);
        Assert.Equal(0u, cs.ZoneId);
        Assert.Equal(0u, cs.MapId);
        Assert.NotNull(cs.Position);
        Assert.Equal(0u, cs.GuildId);
        Assert.NotNull(cs.Equipment);
        Assert.Empty(cs.Equipment);
        Assert.Equal(0u, cs.PetDisplayId);
        Assert.Equal(0u, cs.PetLevel);
        Assert.Equal(0u, cs.PetFamily);
    }

    [Fact]
    public void SetAllProperties()
    {
        var cs = new CharacterSelect
        {
            Guid = 12345UL,
            Name = "Testchar",
            Race = Race.Orc,
            Class = Class.Warrior,
            Gender = Gender.Male,
            Skin = 1,
            Face = 2,
            HairStyle = 3,
            HairColor = 4,
            FacialHair = 5,
            Level = 60,
            ZoneId = 14,
            MapId = 1,
            Position = new Position(100, 200, 300),
            GuildId = 42,
            CharacterFlags = CharacterFlags.CHARACTER_FLAG_GHOST,
            FirstLogin = AtLoginFlags.AT_LOGIN_RENAME,
            PetDisplayId = 100,
            PetLevel = 30,
            PetFamily = 5
        };

        Assert.Equal(12345UL, cs.Guid);
        Assert.Equal("Testchar", cs.Name);
        Assert.Equal(Race.Orc, cs.Race);
        Assert.Equal(Class.Warrior, cs.Class);
        Assert.Equal(Gender.Male, cs.Gender);
        Assert.Equal((byte)1, cs.Skin);
        Assert.Equal((byte)60, cs.Level);
        Assert.Equal(14u, cs.ZoneId);
        Assert.Equal(1u, cs.MapId);
        Assert.Equal(100f, cs.Position.X);
        Assert.Equal(42u, cs.GuildId);
        Assert.Equal(CharacterFlags.CHARACTER_FLAG_GHOST, cs.CharacterFlags);
        Assert.Equal(AtLoginFlags.AT_LOGIN_RENAME, cs.FirstLogin);
        Assert.Equal(100u, cs.PetDisplayId);
    }

    [Fact]
    public void Equipment_CanAddItems()
    {
        var cs = new CharacterSelect();
        cs.Equipment.Add((1234, InventoryType.Head));
        cs.Equipment.Add((5678, InventoryType.Chest));

        Assert.Equal(2, cs.Equipment.Count);
        Assert.Equal(1234u, cs.Equipment[0].DisplayId);
        Assert.Equal(InventoryType.Head, cs.Equipment[0].InventoryType);
    }
}

public class QuestSlotTests
{
    [Fact]
    public void DefaultValues()
    {
        var slot = new QuestSlot();

        Assert.Equal(0u, slot.QuestId);
        Assert.NotNull(slot.QuestCounters);
        Assert.Equal(0u, slot.QuestState);
        Assert.Equal(0u, slot.QuestTime);
    }

    [Fact]
    public void SetProperties()
    {
        var slot = new QuestSlot
        {
            QuestId = 100,
            QuestCounters = [1, 2, 3, 4],
            QuestState = 1,
            QuestTime = 3600
        };

        Assert.Equal(100u, slot.QuestId);
        Assert.Equal(4, slot.QuestCounters.Length);
        Assert.Equal((byte)2, slot.QuestCounters[1]);
        Assert.Equal(1u, slot.QuestState);
        Assert.Equal(3600u, slot.QuestTime);
    }
}

public class QuestEnumTests
{
    [Theory]
    [InlineData(QuestState.Completed, 1)]
    [InlineData(QuestState.InProgress, 0)]
    [InlineData(QuestState.Failed, -1)]
    public void QuestState_HasCorrectValues(QuestState state, int expected)
    {
        Assert.Equal(expected, (int)state);
    }

    [Theory]
    [InlineData(QuestObjectiveTypes.Kill, 1)]
    [InlineData(QuestObjectiveTypes.Collect, 2)]
    [InlineData(QuestObjectiveTypes.Event, 3)]
    public void QuestObjectiveTypes_HasCorrectValues(QuestObjectiveTypes type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    [Theory]
    [InlineData(GameData.Core.Models.QuestSlotOffsets.QUEST_ID_OFFSET, 0)]
    [InlineData(GameData.Core.Models.QuestSlotOffsets.QUEST_COUNT_STATE_OFFSET, 1)]
    [InlineData(GameData.Core.Models.QuestSlotOffsets.QUEST_TIME_OFFSET, 2)]
    public void QuestSlotOffsets_HasCorrectValues(GameData.Core.Models.QuestSlotOffsets offset, int expected)
    {
        Assert.Equal(expected, (int)offset);
    }
}
