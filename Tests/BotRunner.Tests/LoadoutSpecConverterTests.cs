using Communication;
using WoWStateManager.Settings;
using Xunit;

namespace BotRunner.Tests;

public sealed class LoadoutSpecConverterTests
{
    [Fact]
    public void ToProto_MapsAllScalarFields()
    {
        var settings = new LoadoutSpecSettings
        {
            TargetLevel = 60,
            HonorRank = 14,
            RidingSkill = 150,
            MountSpellId = 23509,
            ArmorSetId = 383,
            TalentTemplate = "FuryWarriorPreRaid",
        };

        var proto = LoadoutSpecConverter.ToProto(settings);

        Assert.Equal(60u, proto.TargetLevel);
        Assert.Equal(14u, proto.HonorRank);
        Assert.Equal(150u, proto.RidingSkill);
        Assert.Equal(23509u, proto.MountSpellId);
        Assert.Equal(383u, proto.ArmorSetId);
        Assert.Equal("FuryWarriorPreRaid", proto.TalentTemplate);
    }

    [Fact]
    public void ToProto_NullTalentTemplate_RendersAsEmptyString()
    {
        var settings = new LoadoutSpecSettings { TalentTemplate = null };
        var proto = LoadoutSpecConverter.ToProto(settings);
        Assert.Equal(string.Empty, proto.TalentTemplate);
    }

    [Fact]
    public void ToProto_CopiesRepeatedPrimitiveArrays()
    {
        var settings = new LoadoutSpecSettings
        {
            SpellIdsToLearn = new uint[] { 1, 2, 3 },
            SupplementalItemIds = new uint[] { 100, 200 },
            ElixirItemIds = new uint[] { 3825 },
            CompletedQuestIds = new uint[] { 9000, 9001 },
        };

        var proto = LoadoutSpecConverter.ToProto(settings);

        Assert.Equal(new uint[] { 1, 2, 3 }, proto.SpellIdsToLearn);
        Assert.Equal(new uint[] { 100, 200 }, proto.SupplementalItemIds);
        Assert.Equal(new uint[] { 3825 }, proto.ElixirItemIds);
        Assert.Equal(new uint[] { 9000, 9001 }, proto.CompletedQuestIds);
    }

    [Fact]
    public void ToProto_NullRepeatedArrays_ProduceEmptyRepeatedFields()
    {
        var settings = new LoadoutSpecSettings();
        var proto = LoadoutSpecConverter.ToProto(settings);

        Assert.Empty(proto.SpellIdsToLearn);
        Assert.Empty(proto.SupplementalItemIds);
        Assert.Empty(proto.ElixirItemIds);
        Assert.Empty(proto.CompletedQuestIds);
        Assert.Empty(proto.Skills);
        Assert.Empty(proto.EquipItems);
        Assert.Empty(proto.FactionReps);
    }

    [Fact]
    public void ToProto_CopiesEquipItemsWithInventorySlot()
    {
        var settings = new LoadoutSpecSettings
        {
            EquipItems = new[]
            {
                new LoadoutEquipItemSettings { ItemId = 16541, InventorySlot = 1 },
                new LoadoutEquipItemSettings { ItemId = 18831, InventorySlot = 16 },
            },
        };

        var proto = LoadoutSpecConverter.ToProto(settings);

        Assert.Equal(2, proto.EquipItems.Count);
        Assert.Equal(16541u, proto.EquipItems[0].ItemId);
        Assert.Equal(1u, proto.EquipItems[0].InventorySlot);
        Assert.Equal(18831u, proto.EquipItems[1].ItemId);
        Assert.Equal(16u, proto.EquipItems[1].InventorySlot);
    }

    [Fact]
    public void ToProto_CopiesSkills()
    {
        var settings = new LoadoutSpecSettings
        {
            Skills = new[]
            {
                new LoadoutSkillValueSettings { SkillId = 762, Value = 150, Max = 150 },
                new LoadoutSkillValueSettings { SkillId = 44, Value = 300, Max = 300 },
            },
        };

        var proto = LoadoutSpecConverter.ToProto(settings);

        Assert.Equal(2, proto.Skills.Count);
        Assert.Equal(762u, proto.Skills[0].SkillId);
        Assert.Equal(150u, proto.Skills[0].Value);
        Assert.Equal(300u, proto.Skills[1].Value);
    }

    [Fact]
    public void ToProto_CopiesFactionReps()
    {
        var settings = new LoadoutSpecSettings
        {
            FactionReps = new[]
            {
                new LoadoutFactionRepSettings { FactionId = 729, Standing = 42000 },
                new LoadoutFactionRepSettings { FactionId = 730, Standing = -3000 },
            },
        };

        var proto = LoadoutSpecConverter.ToProto(settings);

        Assert.Equal(2, proto.FactionReps.Count);
        Assert.Equal(729u, proto.FactionReps[0].FactionId);
        Assert.Equal(42000, proto.FactionReps[0].Standing);
        Assert.Equal(-3000, proto.FactionReps[1].Standing);
    }

    [Fact]
    public void BuildApplyLoadoutAction_SetsActionTypeAndEmbedsSpec()
    {
        var settings = new LoadoutSpecSettings { TargetLevel = 60, HonorRank = 14 };
        var action = LoadoutSpecConverter.BuildApplyLoadoutAction(settings);

        Assert.Equal(ActionType.ApplyLoadout, action.ActionType);
        Assert.NotNull(action.LoadoutSpec);
        Assert.Equal(60u, action.LoadoutSpec.TargetLevel);
        Assert.Equal(14u, action.LoadoutSpec.HonorRank);
    }
}
