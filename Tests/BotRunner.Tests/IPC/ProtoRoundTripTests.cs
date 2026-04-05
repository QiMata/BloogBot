using Communication;
using Game;
using Google.Protobuf;
using Xunit;

namespace BotRunner.Tests.IPC;

/// <summary>
/// T4.1-4.3: Verify new proto fields survive serialization round-trips.
/// Tests QuestObjectiveProgress, ProfessionSkillEntry, and build_template fields.
/// </summary>
public class ProtoRoundTripTests
{
    // T4.1: Quest objective proto round-trip
    [Fact]
    public void QuestLogEntry_Objectives_SurviveRoundTrip()
    {
        var entry = new QuestLogEntry
        {
            QuestLog1 = 790,  // Quest ID packed
            QuestLog2 = 1,
            QuestLog3 = 0,
            QuestId = 790,
        };
        entry.Objectives.Add(new QuestObjectiveProgress
        {
            ObjectiveIndex = 0,
            CurrentCount = 3,
            RequiredCount = 8,
            Description = "Lazy Peon awakened",
        });
        entry.Objectives.Add(new QuestObjectiveProgress
        {
            ObjectiveIndex = 1,
            CurrentCount = 1,
            RequiredCount = 1,
            Description = "Foreman Thazz'ril's Pick",
        });

        // Serialize → deserialize
        var bytes = entry.ToByteArray();
        var deserialized = QuestLogEntry.Parser.ParseFrom(bytes);

        Assert.Equal(790u, deserialized.QuestId);
        Assert.Equal(2, deserialized.Objectives.Count);
        Assert.Equal(0u, deserialized.Objectives[0].ObjectiveIndex);
        Assert.Equal(3u, deserialized.Objectives[0].CurrentCount);
        Assert.Equal(8u, deserialized.Objectives[0].RequiredCount);
        Assert.Equal("Lazy Peon awakened", deserialized.Objectives[0].Description);
        Assert.Equal(1u, deserialized.Objectives[1].ObjectiveIndex);
        Assert.Equal("Foreman Thazz'ril's Pick", deserialized.Objectives[1].Description);
    }

    [Fact]
    public void QuestLogEntry_EmptyObjectives_RoundTrips()
    {
        var entry = new QuestLogEntry { QuestLog1 = 100, QuestId = 100 };
        var bytes = entry.ToByteArray();
        var deserialized = QuestLogEntry.Parser.ParseFrom(bytes);
        Assert.Equal(100u, deserialized.QuestId);
        Assert.Empty(deserialized.Objectives);
    }

    // T4.2: Profession skill proto round-trip
    [Fact]
    public void WoWPlayer_ProfessionSkills_SurviveRoundTrip()
    {
        var player = new WoWPlayer();
        player.ProfessionSkills.Add(new ProfessionSkillEntry
        {
            SkillId = 164,       // Blacksmithing
            CurrentSkill = 225,
            MaxSkill = 300,
            SkillName = "Blacksmithing",
        });
        player.ProfessionSkills.Add(new ProfessionSkillEntry
        {
            SkillId = 186,       // Mining
            CurrentSkill = 175,
            MaxSkill = 225,
            SkillName = "Mining",
        });

        var bytes = player.ToByteArray();
        var deserialized = WoWPlayer.Parser.ParseFrom(bytes);

        Assert.Equal(2, deserialized.ProfessionSkills.Count);
        Assert.Equal(164u, deserialized.ProfessionSkills[0].SkillId);
        Assert.Equal(225u, deserialized.ProfessionSkills[0].CurrentSkill);
        Assert.Equal(300u, deserialized.ProfessionSkills[0].MaxSkill);
        Assert.Equal("Blacksmithing", deserialized.ProfessionSkills[0].SkillName);
        Assert.Equal(186u, deserialized.ProfessionSkills[1].SkillId);
        Assert.Equal("Mining", deserialized.ProfessionSkills[1].SkillName);
    }

    [Fact]
    public void WoWPlayer_EmptyProfessionSkills_RoundTrips()
    {
        var player = new WoWPlayer();
        var bytes = player.ToByteArray();
        var deserialized = WoWPlayer.Parser.ParseFrom(bytes);
        Assert.Empty(deserialized.ProfessionSkills);
    }

    // T4.3: Build template proto round-trip
    [Fact]
    public void CharacterDefinition_BuildTemplate_SurvivesRoundTrip()
    {
        var charDef = new CharacterDefinition
        {
            AccountName = "TESTBOT1",
            Openness = 0.5f,
            BuildTemplate = "FuryWarriorPreRaid",
        };

        var bytes = charDef.ToByteArray();
        var deserialized = CharacterDefinition.Parser.ParseFrom(bytes);

        Assert.Equal("TESTBOT1", deserialized.AccountName);
        Assert.Equal(0.5f, deserialized.Openness);
        Assert.Equal("FuryWarriorPreRaid", deserialized.BuildTemplate);
    }

    [Fact]
    public void CharacterDefinition_EmptyBuildTemplate_RoundTrips()
    {
        var charDef = new CharacterDefinition { AccountName = "TEST" };
        var bytes = charDef.ToByteArray();
        var deserialized = CharacterDefinition.Parser.ParseFrom(bytes);
        Assert.Equal("TEST", deserialized.AccountName);
        Assert.Equal("", deserialized.BuildTemplate);
    }

    [Fact]
    public void CharacterDefinition_AllTemplateNames_RoundTrip()
    {
        var templates = new[] { "FuryWarriorPreRaid", "HolyPriestMCReady", "FrostMageAoEFarmer", "ProtectionWarriorTank" };
        foreach (var template in templates)
        {
            var def = new CharacterDefinition { AccountName = "BOT", BuildTemplate = template };
            var bytes = def.ToByteArray();
            var rt = CharacterDefinition.Parser.ParseFrom(bytes);
            Assert.Equal(template, rt.BuildTemplate);
        }
    }
}
