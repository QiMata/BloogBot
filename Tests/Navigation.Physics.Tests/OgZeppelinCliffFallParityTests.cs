using Xunit;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

/// <summary>
/// PFS-OVERHAUL-006 (2026-05-10) — deterministic unit-test capture of the OG
/// zeppelin <c>smooth-wp01-cliff-fall-z42</c> FG/BG parity break.
///
/// Live evidence (validator report
/// <c>tmp/test-runtime/screenshots/long-pathing/bake-validation/og-zeppelin/
/// bake-validation-ClimbOrgrimmarTowerToFrezza-20260510T191634Z.json</c>):
/// teleport to <c>(1337.30, -4645.10, 51.70)</c>, FG settles at z=42.29
/// (falls 9y to the lower platform), BG settles at z=51.62 (snaps to the
/// cliff-edge / WMO geometry the unfiltered <c>GetGroundZ</c> accepts).
///
/// Diagnosis chain:
/// <c>MovementController.PrimeAirborneTeleportFallIfNeeded</c> +
/// <c>TrySnapToNearbyTeleportSupport</c> both call
/// <c>NativeLocalPhysics.GetGroundZ</c>, which delegates to
/// <c>SceneCache::GetGroundZ</c>. That C++ function returns ANY XY-containing
/// triangle inside the search window with NO walkable-slope filter, so a
/// non-walkable cliff-edge triangle at z≈51.62 satisfies "support" and the
/// bot never enters FALLINGFAR.
///
/// Fix: parallel <c>SceneCache::GetWalkableGroundZ</c> that filters
/// candidates by computed triangle normal slope
/// (<c>|n.z| ≥ walkableMinNormalZ</c>). Used by the two post-teleport probes.
///
/// This test:
///   1. Captures the bug at the cliff-fall XY (raw GetGroundZ returns a Z
///      close to the teleport target).
///   2. Confirms the walkable variant either rejects (returns INVALID_HEIGHT
///      inside the tight 6y "nearby support" window) or returns a sufficiently
///      lower Z that the drop-gate in PrimeAirborneTeleportFallIfNeeded
///      correctly primes FALLINGFAR.
///   3. Confirms a known-walkable neighbor (approach-pos at
///      <c>(1338.10, -4646.00, 51.60)</c>) still resolves to the deck Z under
///      both functions — no regression on legitimate support.
/// </summary>
[Collection("PhysicsEngine")]
public sealed class OgZeppelinCliffFallParityTests(PhysicsEngineFixture fixture)
{
    private readonly PhysicsEngineFixture _fixture = fixture;
    private const uint KalimdorMapId = 1u;
    private const float WalkableMinNormalZ = 0.6428f; // cos 50° — matches DEFAULT_WALKABLE_MIN_NORMAL_Z
    private const float InvalidHeightCutoff = -50000f;

    // Cliff-fall coordinate from the validator JSON. FG falls 9y to z=42.29.
    private const float CliffFallX = 1337.30f;
    private const float CliffFallY = -4645.10f;
    private const float CliffFallTeleportZ = 51.70f;
    private const float CliffFallFgSettleZ = 42.29f;

    // Known-walkable neighbor (approach-pos in the same fixture). Both FG and BG
    // settle ~51.6 here. Must continue to resolve under the walkable filter.
    private const float ApproachPosX = 1338.10f;
    private const float ApproachPosY = -4646.00f;
    private const float ApproachPosExpectedZ = 51.60f;

    private const float NearbySupportProbeDist = 6.0f;        // TELEPORT_NEARBY_SUPPORT_PROBE_DISTANCE
    private const float NearbySupportUpTolerance = 0.5f;      // MovementController grace for "support above me" noise
    private const float AirborneSearchDist = 150.0f;           // POST_TELEPORT_AIRBORNE_GROUND_SEARCH_DISTANCE

    [Fact]
    [Trait("Category", "PhysicsParity")]
    public void CliffFall_RawGetGroundZ_ReturnsTeleportTargetZ_DocumentingBug()
    {
        Skip.If(!_fixture.IsInitialized, "Native physics fixture failed to initialize.");

        PreloadMap(KalimdorMapId);

        var queryZ = CliffFallTeleportZ + 0.5f;
        var raw = GetGroundZ(KalimdorMapId, CliffFallX, CliffFallY, queryZ, NearbySupportProbeDist);

        // The unfiltered probe returns a Z close to the teleport target — this
        // is the BG snap surface that fires the parity break in production.
        // If this assertion ever stops holding (e.g. scene cache regen removes
        // the cliff-edge triangle naturally), the parity break is gone for
        // independent reasons and the diagnosis needs revisiting.
        Assert.True(raw > InvalidHeightCutoff,
            $"Expected unfiltered GetGroundZ to find a candidate at cliff-fall XY; got {raw:F2}");
        Assert.True(raw > CliffFallTeleportZ - NearbySupportProbeDist,
            $"Expected unfiltered probe to find a near-teleport surface (the bug). Got {raw:F2}, " +
            $"more than {NearbySupportProbeDist:F1}y below teleport target {CliffFallTeleportZ:F2}.");
    }

    [Fact]
    [Trait("Category", "PhysicsParity")]
    public void CliffFall_WalkableGetGroundZ_DoesNotSatisfyNearbySupport()
    {
        Skip.If(!_fixture.IsInitialized, "Native physics fixture failed to initialize.");

        PreloadMap(KalimdorMapId);

        // The TrySnapToNearbyTeleportSupport gate uses the 6y near-support probe
        // (it only snaps the bot when there is walkable support within range).
        // With the walkable filter, the cliff-fall XY must NOT satisfy this
        // gate (else BG keeps snapping to the wrong Z and the parity break
        // persists). Acceptable: INVALID_HEIGHT, OR a Z that the
        // `supportDrop < -0.5f || supportDrop > 6f` reject already catches.
        var queryZ = CliffFallTeleportZ + 0.5f;
        var walkable = GetWalkableGroundZ(KalimdorMapId, CliffFallX, CliffFallY, queryZ,
            NearbySupportProbeDist, WalkableMinNormalZ);

        if (walkable <= InvalidHeightCutoff)
            return; // gate's foundSupport check returns early; correct

        var drop = CliffFallTeleportZ - walkable;
        var insideStandingZone = drop >= -NearbySupportUpTolerance && drop <= NearbySupportProbeDist;
        Assert.False(insideStandingZone,
            $"Walkable probe returned a Z that satisfies the nearby-support standing zone " +
            $"[{-NearbySupportUpTolerance:F1}, {NearbySupportProbeDist:F1}]y. walkableZ={walkable:F2}, " +
            $"drop={drop:F2}y. The parity break would persist: BG would snap to this surface " +
            $"while FG correctly falls to z={CliffFallFgSettleZ:F2}.");
    }

    [Fact]
    [Trait("Category", "PhysicsParity")]
    public void CliffFall_WalkableGetGroundZ_WideSearchPrimesAirborneFall()
    {
        Skip.If(!_fixture.IsInitialized, "Native physics fixture failed to initialize.");

        PreloadMap(KalimdorMapId);

        // The PrimeAirborneTeleportFallIfNeeded gate uses the 150y wide search.
        // For the cliff-fall XY, the gate MUST prime FALLINGFAR (the bot is in
        // the air below an overhead deck, with the lower platform 9y below).
        // The gate primes FALLINGFAR when:
        //   * no walkable found at all, OR
        //   * walkable found ABOVE the teleport (drop < -0.5y), OR
        //   * walkable found FAR BELOW (drop > 6y).
        var queryZ = CliffFallTeleportZ + 0.5f;
        var walkable = GetWalkableGroundZ(KalimdorMapId, CliffFallX, CliffFallY, queryZ,
            AirborneSearchDist, WalkableMinNormalZ);

        if (walkable <= InvalidHeightCutoff)
            return; // no walkable found ⇒ prime FALLINGFAR; correct

        var drop = CliffFallTeleportZ - walkable;
        var primesFalling = drop < -NearbySupportUpTolerance || drop > NearbySupportProbeDist;
        Assert.True(primesFalling,
            $"Wide-search walkable probe returned a Z inside the standing zone " +
            $"[{-NearbySupportUpTolerance:F1}, {NearbySupportProbeDist:F1}]y. " +
            $"walkableZ={walkable:F2}, drop={drop:F2}y. PrimeAirborneTeleportFallIfNeeded " +
            $"would treat the bot as standing — parity break persists. " +
            $"FG-observed settle={CliffFallFgSettleZ:F2}.");
    }

    [Fact]
    [Trait("Category", "PhysicsParity")]
    public void ApproachPos_WalkableGetGroundZ_StillResolvesToDeck_NoRegression()
    {
        Skip.If(!_fixture.IsInitialized, "Native physics fixture failed to initialize.");

        PreloadMap(KalimdorMapId);

        // Known-walkable spot, ~1.2y from the cliff-fall coord. Both FG and BG
        // settle here at z≈51.60 in the live OG fixture. The walkable filter
        // MUST continue to find this triangle — otherwise it over-rejects and
        // breaks legitimate teleport-onto-deck cases (regression vector).
        var queryZ = ApproachPosExpectedZ + 0.5f;
        var raw = GetGroundZ(KalimdorMapId, ApproachPosX, ApproachPosY, queryZ, NearbySupportProbeDist);
        var walkable = GetWalkableGroundZ(KalimdorMapId, ApproachPosX, ApproachPosY, queryZ,
            NearbySupportProbeDist, WalkableMinNormalZ);

        Assert.True(raw > InvalidHeightCutoff,
            $"Expected GetGroundZ to find the approach-pos deck Z; got {raw:F2}");
        Assert.True(walkable > InvalidHeightCutoff,
            $"Walkable filter rejected the legitimate deck triangle at approach-pos " +
            $"(rawZ={raw:F2}, walkableZ={walkable:F2}). This is a fix regression — the deck " +
            $"top must remain reachable through the walkable-aware probe.");

        // Both functions should agree on the deck Z within tolerance.
        Assert.True(System.MathF.Abs(raw - walkable) < 0.5f,
            $"GetGroundZ and GetWalkableGroundZ disagree on the deck Z at approach-pos: " +
            $"raw={raw:F2}, walkable={walkable:F2}. The walkable filter should not change the " +
            $"answer at a fully-supported deck point.");
    }
}
