using BotRunner.Travel;

namespace BotRunner.Tests.LiveValidation.Dungeons;

/// <summary>
/// Fixture for Wailing Caverns dungeon test.
/// 10 bots (1 FG + 9 BG), meeting stone summoning test included.
/// </summary>
public class WailingCavernsFixture : DungeonInstanceFixture
{
    public WailingCavernsFixture()
    {
        Dungeon = DungeonEntryData.WailingCaverns;
        AccountPrefix = "WCBOT";
    }
}
