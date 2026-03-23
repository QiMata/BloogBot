using ForegroundBotRunner.Mem;

namespace ForegroundBotRunner.Tests;

public class ThreadSynchronizerGateTests
{
    [Fact]
    public void Evaluate_BeforeWorldEntryLegacyPath_DoesNotBlockCharSelectLikeState()
    {
        DispatchGateDecision gate = ThreadSynchronizerGateEvaluator.Evaluate(
            paused: false,
            hasConnectionStateMachine: false,
            isLuaSafe: false,
            managerBaseValid: false,
            continentId: 0xFFFFFFFF,
            managerBaseWasValid: false,
            hasEnteredWorldOnce: false,
            lastContinentId: 0xFFFFFFFF);

        Assert.False(gate.ShouldBlock);
        Assert.False(gate.ManagerBaseWasValid);
        Assert.False(gate.HasEnteredWorldOnce);
        Assert.False(gate.MapJustChanged);
        Assert.Equal(0xFFFFFFFFu, gate.LastContinentId);
    }

    [Fact]
    public void Evaluate_ValidWorldSeedsLegacyStateWithoutBlocking()
    {
        DispatchGateDecision gate = ThreadSynchronizerGateEvaluator.Evaluate(
            paused: false,
            hasConnectionStateMachine: false,
            isLuaSafe: false,
            managerBaseValid: true,
            continentId: 1,
            managerBaseWasValid: false,
            hasEnteredWorldOnce: false,
            lastContinentId: 0xFFFFFFFF);

        Assert.False(gate.ShouldBlock);
        Assert.True(gate.ManagerBaseWasValid);
        Assert.True(gate.HasEnteredWorldOnce);
        Assert.False(gate.MapJustChanged);
        Assert.Equal(1u, gate.LastContinentId);
    }

    [Fact]
    public void Evaluate_InvalidMapAfterWorldEntry_BlocksTransition()
    {
        DispatchGateDecision gate = ThreadSynchronizerGateEvaluator.Evaluate(
            paused: false,
            hasConnectionStateMachine: false,
            isLuaSafe: false,
            managerBaseValid: true,
            continentId: 0xFF,
            managerBaseWasValid: true,
            hasEnteredWorldOnce: true,
            lastContinentId: 1);

        Assert.True(gate.ShouldBlock);
        Assert.True(gate.ManagerBaseWasValid);
        Assert.True(gate.HasEnteredWorldOnce);
        Assert.False(gate.MapJustChanged);
        Assert.Equal(0xFFu, gate.LastContinentId);
    }

    [Fact]
    public void Evaluate_ConnectionStateMachineUnsafe_BlocksEvenWithValidMap()
    {
        DispatchGateDecision gate = ThreadSynchronizerGateEvaluator.Evaluate(
            paused: false,
            hasConnectionStateMachine: true,
            isLuaSafe: false,
            managerBaseValid: true,
            continentId: 1,
            managerBaseWasValid: true,
            hasEnteredWorldOnce: true,
            lastContinentId: 1);

        Assert.True(gate.ShouldBlock);
        Assert.False(gate.MapJustChanged);
    }

    [Fact]
    public void Evaluate_MapChangeBetweenValidMaps_RequestsAutoPauseAndBlocks()
    {
        DispatchGateDecision gate = ThreadSynchronizerGateEvaluator.Evaluate(
            paused: false,
            hasConnectionStateMachine: true,
            isLuaSafe: true,
            managerBaseValid: true,
            continentId: 389,
            managerBaseWasValid: true,
            hasEnteredWorldOnce: true,
            lastContinentId: 1);

        Assert.True(gate.ShouldBlock);
        Assert.True(gate.MapJustChanged);
        Assert.Equal(389u, gate.LastContinentId);
    }

    [Fact]
    public void Evaluate_ManagerBaseDropAfterWorldEntry_BlocksEvenWithoutStateMachine()
    {
        DispatchGateDecision gate = ThreadSynchronizerGateEvaluator.Evaluate(
            paused: false,
            hasConnectionStateMachine: false,
            isLuaSafe: false,
            managerBaseValid: false,
            continentId: 1,
            managerBaseWasValid: true,
            hasEnteredWorldOnce: true,
            lastContinentId: 1);

        Assert.True(gate.ShouldBlock);
        Assert.True(gate.ManagerBaseWasValid);
        Assert.True(gate.HasEnteredWorldOnce);
        Assert.False(gate.MapJustChanged);
    }
}
