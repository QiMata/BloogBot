using BotRunner.Tasks.Crafting;

namespace BotRunner.Tests.Crafting;

public class ProfessionTrainerSchedulerTests
{
    [Fact]
    public void GetTrainer_ReturnsCorrectForFaction()
    {
        var hordeTrainer = ProfessionTrainerScheduler.GetTrainer("Mining", isHorde: true);
        var allianceTrainer = ProfessionTrainerScheduler.GetTrainer("Mining", isHorde: false);

        Assert.NotNull(hordeTrainer);
        Assert.NotNull(allianceTrainer);
        Assert.Equal("Horde", hordeTrainer!.Faction);
        Assert.Equal("Alliance", allianceTrainer!.Faction);
        Assert.NotEqual(hordeTrainer.TrainerEntry, allianceTrainer.TrainerEntry);
    }

    [Fact]
    public void HordeTrainers_ContainsExpectedProfessions()
    {
        var professionNames = ProfessionTrainerScheduler.HordeTrainers
            .Select(t => t.ProfessionName)
            .ToHashSet();

        Assert.Contains("Mining", professionNames);
        Assert.Contains("Herbalism", professionNames);
        Assert.Contains("Skinning", professionNames);
        Assert.Contains("Blacksmithing", professionNames);
        Assert.Contains("Alchemy", professionNames);
        Assert.Contains("Enchanting", professionNames);
        Assert.Contains("Engineering", professionNames);
        Assert.Contains("Cooking", professionNames);
        Assert.Contains("Fishing", professionNames);
    }

    [Fact]
    public void AllianceTrainers_ContainsExpectedProfessions()
    {
        var professionNames = ProfessionTrainerScheduler.AllianceTrainers
            .Select(t => t.ProfessionName)
            .ToHashSet();

        Assert.Contains("Mining", professionNames);
        Assert.Contains("Herbalism", professionNames);
        Assert.Contains("Skinning", professionNames);
        Assert.Contains("Tailoring", professionNames);
        Assert.Contains("Leatherworking", professionNames);
        Assert.Contains("First Aid", professionNames);
    }

    [Fact]
    public void GetTrainer_ReturnsNull_ForUnknownProfession()
    {
        var trainer = ProfessionTrainerScheduler.GetTrainer("Jewelcrafting", isHorde: true);

        Assert.Null(trainer);
    }

    [Fact]
    public void HordeAndAllianceHaveSameProfessionCount()
    {
        Assert.Equal(
            ProfessionTrainerScheduler.HordeTrainers.Count,
            ProfessionTrainerScheduler.AllianceTrainers.Count);
    }
}
