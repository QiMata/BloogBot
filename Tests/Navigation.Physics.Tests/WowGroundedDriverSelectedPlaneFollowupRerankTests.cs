using Xunit;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

[Collection("PhysicsEngine")]
public class WowGroundedDriverSelectedPlaneFollowupRerankTests
{
    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneFollowupRerank_InRangeExactMatchKeepsRerankedPairBeforeFailedSecondPass()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneFollowupRerank(
            selectedIndex: 2u,
            selectedCount: 5u,
            inputContactNormal: new Vector3(0.25f, -0.75f, 0.5f),
            selectedRecordNormal: new Vector3(0.25f, -0.75f, 0.5f),
            inputPackedPairVector: new Vector3(3.0f, 4.0f, 0.0f),
            rerankedPackedPairVector: new Vector3(-0.8f, 0.6f, 0.0f),
            secondRerankSucceeded: 0u,
            out GroundedDriverSelectedPlaneFollowupRerankTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneFollowupRerankKind.ExitWithoutSelection, kind);
        Assert.Equal(GroundedDriverSelectedPlaneFollowupRerankKind.ExitWithoutSelection, trace.DispatchKind);
        Assert.Equal(1u, trace.SelectedIndexInRange);
        Assert.Equal(1u, trace.SelectedRecordMatchesInputNormal);
        Assert.Equal(0u, trace.ReloadedInputPackedPair);
        Assert.Equal(1u, trace.RetainedRerankedPackedPair);
        Assert.Equal(1u, trace.CalledSecondRerank);
        Assert.Equal(0u, trace.SecondRerankSucceeded);
        Assert.Equal(-0.8f, trace.EffectivePackedPairVector.X, 6);
        Assert.Equal(0.6f, trace.EffectivePackedPairVector.Y, 6);
        Assert.Equal(1.0f, trace.SecondPassWorkingVector.Z, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneFollowupRerank_SmallContactNormalZReturnsHorizontalFastPath()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneFollowupRerank(
            selectedIndex: 1u,
            selectedCount: 4u,
            inputContactNormal: new Vector3(0.5f, 0.25f, 1.0e-7f),
            selectedRecordNormal: new Vector3(0.5f, 0.25f, 1.0e-7f),
            inputPackedPairVector: new Vector3(2.0f, -1.0f, 0.0f),
            rerankedPackedPairVector: new Vector3(-1.0f, 0.0f, 0.0f),
            secondRerankSucceeded: 1u,
            out GroundedDriverSelectedPlaneFollowupRerankTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneFollowupRerankKind.HorizontalFastReturn, kind);
        Assert.Equal(GroundedDriverSelectedPlaneFollowupRerankKind.HorizontalFastReturn, trace.DispatchKind);
        Assert.Equal(1u, trace.CalledSecondRerank);
        Assert.Equal(1u, trace.SecondRerankSucceeded);
        Assert.Equal(1u, trace.HorizontalFastReturn);
        Assert.Equal(1u, trace.RetainedRerankedPackedPair);
        Assert.Equal(-1.0f, trace.EffectivePackedPairVector.X, 6);
        Assert.Equal(0.0f, trace.EffectivePackedPairVector.Y, 6);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneFollowupRerank_MismatchedSelectedRecordReloadsInputPair()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneFollowupRerank(
            selectedIndex: 0u,
            selectedCount: 3u,
            inputContactNormal: new Vector3(0.0f, 1.0f, 0.5f),
            selectedRecordNormal: new Vector3(0.0f, 1.0f, 0.25f),
            inputPackedPairVector: new Vector3(7.0f, 8.0f, 0.0f),
            rerankedPackedPairVector: new Vector3(-0.5f, -0.5f, 0.0f),
            secondRerankSucceeded: 1u,
            out GroundedDriverSelectedPlaneFollowupRerankTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneFollowupRerankKind.ContinueToUncapturedTail, kind);
        Assert.Equal(GroundedDriverSelectedPlaneFollowupRerankKind.ContinueToUncapturedTail, trace.DispatchKind);
        Assert.Equal(1u, trace.SelectedIndexInRange);
        Assert.Equal(0u, trace.SelectedRecordMatchesInputNormal);
        Assert.Equal(1u, trace.ReloadedInputPackedPair);
        Assert.Equal(0u, trace.RetainedRerankedPackedPair);
        Assert.Equal(7.0f, trace.EffectivePackedPairVector.X, 6);
        Assert.Equal(8.0f, trace.EffectivePackedPairVector.Y, 6);
        Assert.Equal(0u, trace.HorizontalFastReturn);
    }

    [Fact]
    public void EvaluateWoWGroundedDriverSelectedPlaneFollowupRerank_OutOfRangeSelectedIndexReloadsInputPairBeforeHorizontalReturn()
    {
        uint kind = EvaluateWoWGroundedDriverSelectedPlaneFollowupRerank(
            selectedIndex: 5u,
            selectedCount: 5u,
            inputContactNormal: new Vector3(-0.5f, 0.5f, -1.0e-7f),
            selectedRecordNormal: new Vector3(1.0f, 1.0f, 1.0f),
            inputPackedPairVector: new Vector3(-3.0f, 6.0f, 0.0f),
            rerankedPackedPairVector: new Vector3(0.25f, 0.75f, 0.0f),
            secondRerankSucceeded: 1u,
            out GroundedDriverSelectedPlaneFollowupRerankTrace trace);

        Assert.Equal((uint)GroundedDriverSelectedPlaneFollowupRerankKind.HorizontalFastReturn, kind);
        Assert.Equal(GroundedDriverSelectedPlaneFollowupRerankKind.HorizontalFastReturn, trace.DispatchKind);
        Assert.Equal(0u, trace.SelectedIndexInRange);
        Assert.Equal(0u, trace.SelectedRecordMatchesInputNormal);
        Assert.Equal(1u, trace.ReloadedInputPackedPair);
        Assert.Equal(-3.0f, trace.EffectivePackedPairVector.X, 6);
        Assert.Equal(6.0f, trace.EffectivePackedPairVector.Y, 6);
        Assert.Equal(1u, trace.HorizontalFastReturn);
    }
}
