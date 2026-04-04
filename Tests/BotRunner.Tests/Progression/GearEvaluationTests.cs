using BotRunner.Progression;
using GameData.Core.Enums;
using Moq;
using GameData.Core.Interfaces;
using Xunit;
using System.Collections.Generic;

namespace BotRunner.Tests.Progression;

/// <summary>
/// Tests GearEvaluationService.EvaluateGaps against a target BiS gear set.
/// </summary>
public class GearEvaluationTests
{
    private readonly GearEvaluationService _service = new();

    [Fact]
    public void EvaluateGaps_AllSlotsEmpty_ReturnsAllGaps()
    {
        var mockOm = new Mock<IObjectManager>();
        mockOm.Setup(om => om.GetEquippedItem(It.IsAny<EquipSlot>())).Returns((IWoWItem?)null);

        var targetSet = new List<GearGoal>
        {
            new(EquipSlot.Head, 12640, "Lionheart Helm", "Craft:ArcaniteBar", 2),
            new(EquipSlot.Shoulders, 12927, "Truestrike Shoulders", "Dungeon:UBRS", 1),
            new(EquipSlot.Chest, 11726, "Savage Gladiator Chain", "Dungeon:BRD", 1),
        };

        var gaps = _service.EvaluateGaps(mockOm.Object, targetSet, new StatWeightProfile("Test", 1, 1, 1, 0, 0, 1, 0, 0, 0.5f, 0.5f, 0, 0, 0, 0, 1, 0, 0));
        Assert.Equal(3, gaps.Count);
        Assert.All(gaps, g => Assert.Equal(0, g.CurrentItemId));
    }

    [Fact]
    public void EvaluateGaps_OneSlotMatches_OmitsThatSlot()
    {
        var mockOm = new Mock<IObjectManager>();
        var mockItem = new Mock<IWoWItem>();
        mockItem.Setup(i => i.ItemId).Returns(12927);
        mockItem.Setup(i => i.Name).Returns("Truestrike Shoulders");

        mockOm.Setup(om => om.GetEquippedItem(EquipSlot.Shoulders)).Returns(mockItem.Object);
        mockOm.Setup(om => om.GetEquippedItem(It.Is<EquipSlot>(s => s != EquipSlot.Shoulders))).Returns((IWoWItem?)null);

        var targetSet = new List<GearGoal>
        {
            new(EquipSlot.Head, 12640, "Lionheart Helm", "Craft:ArcaniteBar", 2),
            new(EquipSlot.Shoulders, 12927, "Truestrike Shoulders", "Dungeon:UBRS", 1),
            new(EquipSlot.Chest, 11726, "Savage Gladiator Chain", "Dungeon:BRD", 1),
        };

        var gaps = _service.EvaluateGaps(mockOm.Object, targetSet, new StatWeightProfile("Test", 1, 1, 1, 0, 0, 1, 0, 0, 0.5f, 0.5f, 0, 0, 0, 0, 1, 0, 0));
        Assert.Equal(2, gaps.Count);
        Assert.DoesNotContain(gaps, g => g.Slot == EquipSlot.Shoulders);
    }

    [Fact]
    public void EvaluateGaps_ResultOrderedByPriority()
    {
        var mockOm = new Mock<IObjectManager>();
        mockOm.Setup(om => om.GetEquippedItem(It.IsAny<EquipSlot>())).Returns((IWoWItem?)null);

        var targetSet = new List<GearGoal>
        {
            new(EquipSlot.Head, 12640, "Lionheart Helm", "Craft", 3),
            new(EquipSlot.Shoulders, 12927, "Truestrike Shoulders", "Dungeon:UBRS", 1),
            new(EquipSlot.Chest, 11726, "Savage Gladiator Chain", "Dungeon:BRD", 2),
        };

        var gaps = _service.EvaluateGaps(mockOm.Object, targetSet, new StatWeightProfile("Test", 1, 1, 1, 0, 0, 1, 0, 0, 0.5f, 0.5f, 0, 0, 0, 0, 1, 0, 0));
        Assert.Equal(3, gaps.Count);
        Assert.Equal(1, gaps[0].Priority);
        Assert.Equal(2, gaps[1].Priority);
        Assert.Equal(3, gaps[2].Priority);
    }
}
