using BotRunner.Travel;
using System.Linq;
using Xunit;

namespace BotRunner.Tests.Travel;

public class MageTeleportDataTests
{
    [Fact]
    public void GetAllSpells_SelfTeleportsRequireRuneOfTeleportation()
    {
        var selfTeleports = MageTeleportData.GetAllSpells().Where(spell => spell.Type == TeleportSpellType.SelfTeleport);

        Assert.NotEmpty(selfTeleports);
        Assert.All(selfTeleports, spell => Assert.Equal(MageTeleportData.RuneOfTeleportation, spell.ReagentItemId));
    }
}
