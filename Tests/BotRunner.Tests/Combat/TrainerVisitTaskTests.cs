using BotRunner.Tasks;
using GameData.Core.Enums;

namespace BotRunner.Tests.Combat;

public class TrainerVisitTaskIsClassTrainerMatchTests
{
    [Theory]
    [InlineData("Warrior Trainer", Class.Warrior, true)]
    [InlineData("Paladin Trainer", Class.Paladin, true)]
    [InlineData("Hunter Trainer", Class.Hunter, true)]
    [InlineData("Rogue Trainer", Class.Rogue, true)]
    [InlineData("Priest Trainer", Class.Priest, true)]
    [InlineData("Shaman Trainer", Class.Shaman, true)]
    [InlineData("Mage Trainer", Class.Mage, true)]
    [InlineData("Warlock Trainer", Class.Warlock, true)]
    [InlineData("Druid Trainer", Class.Druid, true)]
    public void MatchesOwnClassTrainer(string npcName, Class playerClass, bool expected)
    {
        Assert.Equal(expected, TrainerVisitTask.IsClassTrainerMatch(npcName, playerClass));
    }

    [Theory]
    [InlineData("Warrior Trainer", Class.Mage)]
    [InlineData("Priest Trainer", Class.Rogue)]
    [InlineData("Shaman Trainer", Class.Paladin)]
    public void DoesNotMatchOtherClassTrainer(string npcName, Class playerClass)
    {
        Assert.False(TrainerVisitTask.IsClassTrainerMatch(npcName, playerClass));
    }

    [Theory]
    [InlineData("warrior trainer", Class.Warrior)] // lowercase
    [InlineData("WARRIOR TRAINER", Class.Warrior)] // uppercase
    [InlineData("Master Warrior Trainer", Class.Warrior)] // embedded
    public void CaseInsensitiveMatch(string npcName, Class playerClass)
    {
        Assert.True(TrainerVisitTask.IsClassTrainerMatch(npcName, playerClass));
    }

    [Fact]
    public void EmptyName_ReturnsFalse()
    {
        Assert.False(TrainerVisitTask.IsClassTrainerMatch("", Class.Warrior));
    }

    [Fact]
    public void NullName_ReturnsFalse()
    {
        Assert.False(TrainerVisitTask.IsClassTrainerMatch(null!, Class.Warrior));
    }

    [Fact]
    public void GenericNpc_ReturnsFalse()
    {
        Assert.False(TrainerVisitTask.IsClassTrainerMatch("Innkeeper Gryshka", Class.Warrior));
    }
}

public class TrainerVisitTaskIsWrongTrainerTests
{
    [Theory]
    [InlineData("Mining Trainer")]
    [InlineData("Herbalism Trainer")]
    [InlineData("Skinning Trainer")]
    [InlineData("Fishing Trainer")]
    [InlineData("Cooking Trainer")]
    [InlineData("First Aid Trainer")]
    [InlineData("Blacksmithing Trainer")]
    [InlineData("Leatherworking Trainer")]
    [InlineData("Tailoring Trainer")]
    [InlineData("Engineering Trainer")]
    [InlineData("Enchanting Trainer")]
    [InlineData("Alchemy Trainer")]
    [InlineData("Riding Trainer")]
    [InlineData("Weapon Master")]
    public void ProfessionTrainer_IsWrong(string npcName)
    {
        Assert.True(TrainerVisitTask.IsWrongTrainer(npcName, Class.Warrior));
    }

    [Theory]
    [InlineData("Warrior Trainer", Class.Warrior)]
    [InlineData("Mage Trainer", Class.Mage)]
    [InlineData("Druid Trainer", Class.Druid)]
    public void OwnClassTrainer_IsNotWrong(string npcName, Class playerClass)
    {
        Assert.False(TrainerVisitTask.IsWrongTrainer(npcName, playerClass));
    }

    [Theory]
    [InlineData("Warrior Trainer", Class.Mage)]
    [InlineData("Priest Trainer", Class.Warrior)]
    [InlineData("Hunter Trainer", Class.Rogue)]
    public void OtherClassTrainer_IsWrong(string npcName, Class playerClass)
    {
        Assert.True(TrainerVisitTask.IsWrongTrainer(npcName, playerClass));
    }

    [Fact]
    public void GenericNpc_IsNotWrong()
    {
        // A generic NPC with no class or profession keyword is acceptable
        Assert.False(TrainerVisitTask.IsWrongTrainer("Innkeeper Gryshka", Class.Warrior));
    }

    [Fact]
    public void EmptyName_IsNotWrong()
    {
        Assert.False(TrainerVisitTask.IsWrongTrainer("", Class.Warrior));
    }

    [Fact]
    public void NullName_IsNotWrong()
    {
        Assert.False(TrainerVisitTask.IsWrongTrainer(null!, Class.Warrior));
    }

    [Theory]
    [InlineData("mining trainer", Class.Warrior)] // lowercase
    [InlineData("ALCHEMY TRAINER", Class.Warrior)] // uppercase
    public void ProfessionKeyword_CaseInsensitive(string npcName, Class playerClass)
    {
        Assert.True(TrainerVisitTask.IsWrongTrainer(npcName, playerClass));
    }

    [Theory]
    [InlineData("warrior trainer", Class.Mage)] // lowercase other class
    [InlineData("SHAMAN TRAINER", Class.Warrior)] // uppercase other class
    public void OtherClassKeyword_CaseInsensitive(string npcName, Class playerClass)
    {
        Assert.True(TrainerVisitTask.IsWrongTrainer(npcName, playerClass));
    }

    [Fact]
    public void NameContainsBothOwnAndOtherClass_NotWrong()
    {
        // Edge case: if NPC name somehow contains both, and our class is present, it's OK
        Assert.False(TrainerVisitTask.IsWrongTrainer("Warrior and Mage Trainer", Class.Warrior));
    }
}
