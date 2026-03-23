using ForegroundBotRunner.Statics;

namespace ForegroundBotRunner.Tests;

public class SpellKnowledgeReconcilerTests
{
    [Fact]
    public void Reconcile_StableIdsPassThrough_WhenNoStickyDeltas()
    {
        SpellKnowledgeState state = SpellKnowledgeReconciler.Reconcile(
            stableIds: [1u, 2u, 3u],
            stickyLearnedIds: [],
            stickyRemovedIds: []);

        Assert.Equal([1u, 2u, 3u], state.PublishedIds.OrderBy(id => id));
        Assert.Empty(state.StickyLearnedIds);
        Assert.Empty(state.StickyRemovedIds);
    }

    [Fact]
    public void Reconcile_StickyLearnedIdStaysPublished_WhenStableSourcesMissIt()
    {
        SpellKnowledgeState state = SpellKnowledgeReconciler.Reconcile(
            stableIds: [10u],
            stickyLearnedIds: [20u],
            stickyRemovedIds: []);

        Assert.Equal([10u, 20u], state.PublishedIds.OrderBy(id => id));
        Assert.Equal([20u], state.StickyLearnedIds);
        Assert.Empty(state.StickyRemovedIds);
    }

    [Fact]
    public void Reconcile_StableIdsClearStickyLearnedAndRemoved()
    {
        SpellKnowledgeState state = SpellKnowledgeReconciler.Reconcile(
            stableIds: [10u, 20u, 30u],
            stickyLearnedIds: [20u, 40u],
            stickyRemovedIds: [30u, 50u]);

        Assert.Equal([10u, 20u, 30u, 40u], state.PublishedIds.OrderBy(id => id));
        Assert.Equal([40u], state.StickyLearnedIds);
        Assert.Equal([50u], state.StickyRemovedIds);
    }

    [Fact]
    public void Reconcile_StickyRemovedMasksId_WhenStableSourcesMissIt()
    {
        SpellKnowledgeState state = SpellKnowledgeReconciler.Reconcile(
            stableIds: [100u],
            stickyLearnedIds: [],
            stickyRemovedIds: [200u, 300u]);

        Assert.Equal([100u], state.PublishedIds.OrderBy(id => id));
        Assert.Empty(state.StickyLearnedIds);
        Assert.Equal([200u, 300u], state.StickyRemovedIds.OrderBy(id => id));
    }
}
