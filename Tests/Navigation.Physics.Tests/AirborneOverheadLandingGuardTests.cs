using Xunit;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests;

/// <summary>
/// PFS-OVERHAUL-006 round 3 (2026-05-10) — deterministic unit-test capture of
/// the residual snap-up that survives <c>SceneCache::GetWalkableGroundZ</c>
/// (round 1, commit 1c530288).
///
/// Live evidence (validator JSON
/// <c>tmp/test-runtime/screenshots/long-pathing/bake-validation/og-zeppelin/
/// bake-validation-ClimbOrgrimmarTowerToFrezza-20260511T002911Z.json</c>):
/// after round 1, BG settles at z=53.32 (the deck ABOVE the teleport target)
/// while FG falls 9y to z=42.29. Trace shows the snap completes in one physics
/// frame inside <c>PhysicsStepV2</c>; the round-1 fix correctly rejects the
/// cliff fillet at z=51.62 but a separate mechanism still pulls the bot UP
/// to the overhead deck.
///
/// Diagnosis: when FALLINGFAR is set on input, <c>StepV2</c> routes through
/// <c>useAirbornePath</c> → <c>ProcessAirMovement</c>. That function's landing
/// check (PhysicsMovement.cpp:170-179) probes ground with
/// <c>GetGroundZ(x, y, endPos.z + STEP_HEIGHT, STEP_HEIGHT + STEP_DOWN_HEIGHT)</c>.
/// The search window reaches ~STEP_HEIGHT=2.028y ABOVE the predicted feet,
/// finds the overhead deck, and the condition
/// <c>endPos.z &lt;= groundZ + LANDING_TOLERANCE</c> trivially holds when the
/// candidate ground is ABOVE the bot. The bot snaps UP. Real WoW falls past
/// overhead WMO/M2 floors; BG must do the same.
///
/// This test reproduces the cliff-fall scenario using
/// <see cref="InjectSceneTriangles"/> so it does NOT depend on MaNGOS data
/// being preloaded.
///
/// Game-portability note: every game in the monorepo (FFXI, WAR, UO, EQ,
/// EQ2, PSO, Rag, SWG, D2) will hit this same class of bug when teleporting
/// near overhead WMO/M2 structures. The canonical fix shape is to gate
/// landing on "ground was at or below the bot's previous Z" (with a small
/// LANDING_TOLERANCE for sub-frame motion).
/// </summary>
[Collection("PhysicsEngine")]
public sealed class AirborneOverheadLandingGuardTests : IDisposable
{
    private const uint TestMapId = 1u;
    private readonly PhysicsEngineFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AirborneOverheadLandingGuardTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public void Dispose()
    {
        if (_fixture.IsInitialized)
            ClearSceneCache(TestMapId);
    }

    /// <summary>
    /// Synthesizes the OG smooth-wp01-cliff-fall topology with two walkable
    /// triangles: a "deck" at z=53.5 covering the teleport XY (overhead) and
    /// an "ADT floor" at z=42.29 covering the same XY (below). The bot is
    /// stepped once at (1337.3, -4645.1, 51.7) with FALLINGFAR set and Vz=0.
    ///
    /// Required physics behavior: the bot must remain airborne. It must NOT
    /// snap UP to the deck. The deck is ~1.8y above the predicted feet, inside
    /// the current landing probe's STEP_HEIGHT-above-feet search reach.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "PhysicsParity")]
    public void StepV2_FallingFar_OverheadDeck_DoesNotSnapUp()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        PreloadMap(TestMapId);
        ClearSceneCache(TestMapId);

        // Two large walkable triangles covering the test XY (1337.3, -4645.1).
        // GroupFlags 0 = outdoor terrain (not relevant to the landing logic).
        var triangles = new[]
        {
            // ADT floor at z=42.29 — what the bot SHOULD eventually fall to.
            new InjectedTriangle
            {
                V0X = 1330f, V0Y = -4640f, V0Z = 42.29f,
                V1X = 1345f, V1Y = -4640f, V1Z = 42.29f,
                V2X = 1337f, V2Y = -4650f, V2Z = 42.29f,
                SourceType = 0u,
                InstanceId = 0x1001u,
                GroupFlags = 0u,
            },
            // Deck at z=53.5 — what the buggy ProcessAirMovement snaps UP to.
            new InjectedTriangle
            {
                V0X = 1330f, V0Y = -4640f, V0Z = 53.5f,
                V1X = 1345f, V1Y = -4640f, V1Z = 53.5f,
                V2X = 1337f, V2Y = -4650f, V2Z = 53.5f,
                SourceType = 0u,
                InstanceId = 0x1002u,
                GroupFlags = 0u,
            },
        };

        Assert.True(InjectSceneTriangles(TestMapId, 1325f, -4655f, 1350f, -4635f, triangles, triangles.Length),
            "Failed to inject test triangles");

        // Sanity check: both surfaces are discoverable.
        float deckProbe = GetGroundZ(TestMapId, 1337.3f, -4645.1f, 60f, 50f);
        float adtProbe = GetGroundZ(TestMapId, 1337.3f, -4645.1f, 46f, 50f);
        _output.WriteLine($"Probe: deckProbe={deckProbe:F3} adtProbe={adtProbe:F3}");

        // The deck must be findable (otherwise the test isn't exercising the
        // bug surface), and the ADT must be discoverable from below the deck.
        Assert.True(deckProbe > 53.0f && deckProbe < 54.0f,
            $"Deck triangle not discoverable from above (deckProbe={deckProbe:F3})");
        Assert.True(adtProbe > 42.0f && adtProbe < 43.0f,
            $"ADT triangle not discoverable from below (adtProbe={adtProbe:F3})");

        // FALLINGFAR = 0x4000. Set this to simulate the post-teleport state
        // primed by MovementController.PrimeAirborneTeleportFallIfNeeded.
        const uint MOVEFLAG_FALLINGFAR = 0x4000u;

        var input = new PhysicsInput
        {
            MapId = TestMapId,
            X = 1337.3f,
            Y = -4645.1f,
            Z = 51.7f,                  // teleport target — between deck and ADT
            Vx = 0f,
            Vy = 0f,
            Vz = 0f,  // post-teleport: no velocity
            MoveFlags = MOVEFLAG_FALLINGFAR,
            Height = 2.625f,
            Radius = 1.025f,
            DeltaTime = 1f / 60f,       // 16.67ms
            FallTime = 0u,
            FallStartZ = 51.7f,
            // _prevGroundZ primed BELOW the teleport (Prime's "below-overhead"
            // branch — see MovementController.cs:551). Use 42.29 to mirror live.
            PrevGroundZ = 42.29f,
            PrevGroundNx = 0f,
            PrevGroundNy = 0f,
            PrevGroundNz = 1f,
            WasGrounded = 0u,
        };

        var output = StepPhysicsV2(ref input);
        _output.WriteLine(
            $"AfterStep: Z={output.Z:F3} Vz={output.Vz:F3} " +
            $"moveFlags=0x{output.MoveFlags:X} groundZ={output.GroundZ:F3} " +
            $"fallTime={output.FallTime}");

        // PRIMARY ASSERTION: the bot MUST NOT have snapped UP to the overhead
        // deck. Pre-fix behavior: Z=53.317. Post-fix expected: Z stays at or
        // slightly below the teleport Z=51.7 (gravity has only had 16.67ms
        // to act, so dz ≈ -0.003y at vz0=0).
        Assert.True(output.Z <= 51.7f + 0.01f,
            $"ProcessAirMovement snapped UP to overhead surface. " +
            $"Expected Z ≤ 51.7, got Z={output.Z:F3}. " +
            $"The landing check is treating an overhead deck as walkable support.");

        // Secondary: bot should still be falling — FALLINGFAR retained,
        // negative Vz from one frame of gravity.
        bool stillFalling = (output.MoveFlags & MOVEFLAG_FALLINGFAR) != 0;
        Assert.True(stillFalling,
            $"FALLINGFAR was cleared after one frame. moveFlags=0x{output.MoveFlags:X}. " +
            $"The bot was treated as grounded on an overhead surface.");

        Assert.True(output.Vz <= 0f + 1e-3f,
            $"Vz should be ≤ 0 after one frame of gravity, got Vz={output.Vz:F3}");
    }

    /// <summary>
    /// Companion test: when the bot starts ABOVE the deck (z=55, deck at 53.5)
    /// with FALLINGFAR set and Vz=0, ProcessAirMovement MUST land it on the
    /// deck on a subsequent frame. This guards against the fix over-rejecting
    /// legitimate landings (regression vector).
    /// </summary>
    [SkippableFact]
    [Trait("Category", "PhysicsParity")]
    public void StepV2_FallingFar_AboveDeck_LandsOnDeck()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        PreloadMap(TestMapId);
        ClearSceneCache(TestMapId);

        var triangles = new[]
        {
            new InjectedTriangle
            {
                V0X = 1330f, V0Y = -4640f, V0Z = 53.5f,
                V1X = 1345f, V1Y = -4640f, V1Z = 53.5f,
                V2X = 1337f, V2Y = -4650f, V2Z = 53.5f,
                SourceType = 0u,
                InstanceId = 0x2001u,
                GroupFlags = 0u,
            },
        };

        Assert.True(InjectSceneTriangles(TestMapId, 1325f, -4655f, 1350f, -4635f, triangles, triangles.Length));

        const uint MOVEFLAG_FALLINGFAR = 0x4000u;

        // Start the bot 1.5y above the deck with downward velocity so the
        // descent will cross the deck within a couple of frames.
        var input = new PhysicsInput
        {
            MapId = TestMapId,
            X = 1337.3f,
            Y = -4645.1f,
            Z = 55.0f,
            Vx = 0f,
            Vy = 0f,
            Vz = -3.0f,
            MoveFlags = MOVEFLAG_FALLINGFAR,
            Height = 2.625f,
            Radius = 1.025f,
            DeltaTime = 1f / 60f,
            FallTime = 0u,
            FallStartZ = 55.0f,
            PrevGroundZ = 53.5f,
            PrevGroundNx = 0f,
            PrevGroundNy = 0f,
            PrevGroundNz = 1f,
            WasGrounded = 0u,
        };

        // Run up to 15 frames; bot should land within that window.
        bool landed = false;
        float finalZ = input.Z;
        uint finalFlags = input.MoveFlags;
        for (int frame = 0; frame < 15 && !landed; ++frame)
        {
            var output = StepPhysicsV2(ref input);
            _output.WriteLine($"Frame {frame}: Z={output.Z:F3} Vz={output.Vz:F3} flags=0x{output.MoveFlags:X}");

            finalZ = output.Z;
            finalFlags = output.MoveFlags;
            input.X = output.X; input.Y = output.Y; input.Z = output.Z;
            input.Vx = output.Vx; input.Vy = output.Vy; input.Vz = output.Vz;
            input.MoveFlags = output.MoveFlags;
            input.FallTime = (uint)output.FallTime;
            input.PrevGroundZ = output.GroundZ;
            input.PrevGroundNz = output.GroundNz;

            bool stillAirborne = (output.MoveFlags & MOVEFLAG_FALLINGFAR) != 0;
            if (!stillAirborne)
            {
                landed = true;
                break;
            }
        }

        Assert.True(landed,
            $"Bot failed to land on deck after 15 frames. finalZ={finalZ:F3} finalFlags=0x{finalFlags:X}. " +
            $"The landing guard may be over-rejecting legitimate landings.");

        Assert.InRange(finalZ, 53.0f, 54.0f);
    }

    /// <summary>
    /// PFS-OVERHAUL-006 round 4 iter 3 (2026-05-11) — covers the path where
    /// <c>MovementController.PrimeAirborneTeleportFallIfNeeded</c> detected an
    /// overhead deck but the below-probe failed to find a reliable ADT
    /// reference (the live OG cliff-fall case where
    /// <c>NativeLocalPhysics.GetWalkableGroundZ</c> returns the SAME overhead
    /// deck because it's nearest-walkable, not strictly-downward). Prime then
    /// sets <c>_prevGroundZ = -200000f</c> (INVALID) so physics knows "airborne
    /// but no reliable fall reference."
    ///
    /// Required behavior: with FALLINGFAR set AND prevGroundZ at the INVALID
    /// sentinel, the round-3 PhysicsEngine.cpp depen-skip gate AND the
    /// PhysicsMovement.cpp landing-reject gate must STILL fire — purely on
    /// the inputAirborneFlag, ignoring the missing prevGroundZ. Otherwise
    /// the bot snaps UP to the overhead deck (the round-4 iter-1/2 regression).
    /// </summary>
    [SkippableFact]
    [Trait("Category", "PhysicsParity")]
    public void StepV2_FallingFar_OverheadDeck_InvalidPrevGroundZ_DoesNotSnapUp()
    {
        Skip.If(!_fixture.IsInitialized, "Physics engine not available");

        PreloadMap(TestMapId);
        ClearSceneCache(TestMapId);

        var triangles = new[]
        {
            new InjectedTriangle
            {
                V0X = 1330f, V0Y = -4640f, V0Z = 42.29f,
                V1X = 1345f, V1Y = -4640f, V1Z = 42.29f,
                V2X = 1337f, V2Y = -4650f, V2Z = 42.29f,
                SourceType = 0u,
                InstanceId = 0x3001u,
                GroupFlags = 0u,
            },
            new InjectedTriangle
            {
                V0X = 1330f, V0Y = -4640f, V0Z = 53.5f,
                V1X = 1345f, V1Y = -4640f, V1Z = 53.5f,
                V2X = 1337f, V2Y = -4650f, V2Z = 53.5f,
                SourceType = 0u,
                InstanceId = 0x3002u,
                GroupFlags = 0u,
            },
        };

        Assert.True(InjectSceneTriangles(TestMapId, 1325f, -4655f, 1350f, -4635f, triangles, triangles.Length),
            "Failed to inject test triangles");

        const uint MOVEFLAG_FALLINGFAR = 0x4000u;

        var input = new PhysicsInput
        {
            MapId = TestMapId,
            X = 1337.3f,
            Y = -4645.1f,
            Z = 51.7f,
            Vx = 0f,
            Vy = 0f,
            Vz = 0f,
            MoveFlags = MOVEFLAG_FALLINGFAR,
            Height = 2.625f,
            Radius = 1.025f,
            DeltaTime = 1f / 60f,
            FallTime = 0u,
            FallStartZ = 51.7f,
            // INVALID sentinel — Prime hit the no-support-found branch.
            PrevGroundZ = -200000f,
            PrevGroundNx = 0f,
            PrevGroundNy = 0f,
            PrevGroundNz = 1f,
            WasGrounded = 0u,
        };

        var output = StepPhysicsV2(ref input);
        _output.WriteLine(
            $"AfterStep: Z={output.Z:F3} Vz={output.Vz:F3} " +
            $"moveFlags=0x{output.MoveFlags:X} groundZ={output.GroundZ:F3} " +
            $"fallTime={output.FallTime}");

        Assert.True(output.Z <= 51.7f + 0.01f,
            $"PhysicsStepV2 snapped UP to overhead surface despite FALLINGFAR + " +
            $"INVALID prevGroundZ. Expected Z ≤ 51.7, got Z={output.Z:F3}. " +
            $"The prevGroundUnknown branch of the depen/landing gates is missing.");

        bool stillFalling = (output.MoveFlags & MOVEFLAG_FALLINGFAR) != 0;
        Assert.True(stillFalling,
            $"FALLINGFAR was cleared after one frame with INVALID prevGroundZ. " +
            $"moveFlags=0x{output.MoveFlags:X}. The bot was incorrectly grounded.");

        Assert.True(output.Vz <= 0f + 1e-3f,
            $"Vz should be ≤ 0 after one frame of gravity, got Vz={output.Vz:F3}");
    }
}
