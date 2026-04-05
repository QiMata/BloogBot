using BotRunner.Tasks.Questing;
using BotRunner.Progression;

namespace BotRunner.Tests.Progression;

public class QuestChainRouterTests
{
    [Fact]
    public void GetNextStep_SkipsCompletedQuests()
    {
        var chain = QuestChainData.GetChain("OnyxiaAttunementHorde")!;
        var firstQuestId = chain.Steps[0].QuestId;
        var completed = new HashSet<uint> { firstQuestId };

        var next = QuestChainRouter.GetNextStep("OnyxiaAttunementHorde", completed);

        Assert.NotNull(next);
        Assert.Equal(chain.Steps[1].QuestId, next!.QuestId);
    }

    [Fact]
    public void GetNextStep_NullForCompleteChain()
    {
        var chain = QuestChainData.GetChain("MoltenCoreAttunement")!;
        var allCompleted = new HashSet<uint>(chain.Steps.Select(s => s.QuestId));

        var next = QuestChainRouter.GetNextStep("MoltenCoreAttunement", allCompleted);

        Assert.Null(next);
    }

    [Fact]
    public void GetNextStep_NullForUnknownChain()
    {
        var next = QuestChainRouter.GetNextStep("NonExistentChain", new HashSet<uint>());

        Assert.Null(next);
    }

    [Fact]
    public void GetActiveChainSteps_ReturnsRemaining()
    {
        var chainIds = new List<string> { "MoltenCoreAttunement", "BWLAttunement" };
        var completed = new HashSet<uint>();

        var active = QuestChainRouter.GetActiveChainSteps(chainIds, completed);

        Assert.Equal(2, active.Count);
        Assert.Contains(active, x => x.ChainId == "MoltenCoreAttunement");
        Assert.Contains(active, x => x.ChainId == "BWLAttunement");
    }

    [Fact]
    public void GetActiveChainSteps_ExcludesCompletedChains()
    {
        var mcChain = QuestChainData.GetChain("MoltenCoreAttunement")!;
        var allMcDone = new HashSet<uint>(mcChain.Steps.Select(s => s.QuestId));
        var chainIds = new List<string> { "MoltenCoreAttunement", "BWLAttunement" };

        var active = QuestChainRouter.GetActiveChainSteps(chainIds, allMcDone);

        Assert.Single(active);
        Assert.Equal("BWLAttunement", active[0].ChainId);
    }

    [Fact]
    public void GetNextStep_ReturnsFirstForEmptyCompleted()
    {
        var chain = QuestChainData.GetChain("OnyxiaAttunementHorde")!;
        var next = QuestChainRouter.GetNextStep("OnyxiaAttunementHorde", new HashSet<uint>());

        Assert.NotNull(next);
        Assert.Equal(chain.Steps[0].QuestId, next!.QuestId);
    }
}
