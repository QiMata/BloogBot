using Navigation.Physics.Tests.Helpers;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Navigation.Physics.Tests;

/// <summary>
/// Diagnostic and calibration tests for the Valley of Trials slope terrain.
///
/// The BG bot's Navigation_LongPath test navigates from (-284,-4383,57) to (-350,-4450,50)
/// across sloped terrain. The physics engine returns wildly oscillating Z values
/// (surface → underground → surface) causing position reversals and 3.3x travel overhead.
///
/// This test class:
/// 1) Probes GetGroundZ at each point along the route to identify bad ground detection
/// 2) Simulates walking the route via StepPhysicsV2 to reproduce the Z oscillation
/// 3) Validates fixes by asserting smooth Z progression along the slope
/// </summary>
[Collection("PhysicsEngine")]
public class ValleyOfTrialsSlopeTests
{
    private readonly PhysicsEngineFixture _fixture;
    private readonly ITestOutputHelper _output;

    private const uint MapId = 1; // Kalimdor

    // Navigation_LongPath route endpoints
    private const float StartX = -284f, StartY = -4383f, StartZ = 57f;
    private const float EndX = -350f, EndY = -4450f, EndZ = 50f;

    public ValleyOfTrialsSlopeTests(PhysicsEngineFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Probe GetGroundZ at evenly-spaced points along the slope route.
    /// Identifies where the physics engine returns underground Z values.
    /// </summary>
    [Fact]
    public void SlopeRoute_GetGroundZ_ReturnsConsistentSurfaceZ()
    {
        Assert.True(_fixture.IsInitialized, "Physics engine not initialized (Navigation.dll + data missing)");

        int numSamples = 30;
        var results = new List<(float x, float y, float queryZ, float groundZ, float delta)>();
        int badSamples = 0;

        _output.WriteLine($"=== Valley of Trials Slope Route: GetGroundZ Survey ===");
        _output.WriteLine($"Route: ({StartX},{StartY},{StartZ}) -> ({EndX},{EndY},{EndZ})");
        _output.WriteLine($"Samples: {numSamples} evenly spaced");
        _output.WriteLine("");
        _output.WriteLine($"{"#",3} {"X",10} {"Y",12} {"QueryZ",8} {"GroundZ",10} {"Delta",8} {"Status",10}");
        _output.WriteLine(new string('-', 70));

        float prevGroundZ = float.NaN;

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / (numSamples - 1);
            float x = StartX + t * (EndX - StartX);
            float y = StartY + t * (EndY - StartY);
            float queryZ = StartZ + t * (EndZ - StartZ); // Interpolated surface Z

            float groundZ = NavigationInterop.GetGroundZ(MapId, x, y, queryZ, 50.0f);

            float delta = groundZ - queryZ;
            string status;
            if (groundZ < -50000f)
            {
                status = "NO_GROUND";
                badSamples++;
            }
            else if (groundZ < queryZ - 15f)
            {
                status = "UNDERGROUND";
                badSamples++;
            }
            else if (!float.IsNaN(prevGroundZ) && MathF.Abs(groundZ - prevGroundZ) > 10f)
            {
                status = "Z_JUMP";
                badSamples++;
            }
            else
            {
                status = "OK";
            }

            results.Add((x, y, queryZ, groundZ, delta));
            _output.WriteLine($"{i,3} {x,10:F1} {y,12:F1} {queryZ,8:F1} {groundZ,10:F1} {delta,8:F1} {status,10}");

            if (groundZ > -50000f)
                prevGroundZ = groundZ;
        }

        _output.WriteLine("");
        _output.WriteLine($"Bad samples: {badSamples}/{numSamples}");

        // At least 80% of samples should have valid, consistent ground Z
        Assert.True(badSamples <= numSamples * 0.2,
            $"Too many bad ground Z samples along slope route: {badSamples}/{numSamples}");
    }

    /// <summary>
    /// Probe GetGroundZ at the same XY but from different query heights.
    /// This reveals the "closest to query Z" bug: when querying from too high,
    /// GetGroundZ may find cave geometry below instead of the terrain surface.
    /// </summary>
    [Fact]
    public void SlopeRoute_GetGroundZ_MultiLevel_DiagnosticSurvey()
    {
        Assert.True(_fixture.IsInitialized, "Physics engine not initialized");

        // Pick 5 evenly-spaced XY points along the route
        int numPoints = 5;
        float[] queryHeights = { 100f, 80f, 60f, 50f, 40f, 30f, 20f, 10f, 0f, -10f };

        _output.WriteLine("=== Multi-Level Ground Z Survey (same XY, different query heights) ===");
        _output.WriteLine("Reveals which query heights trigger 'closest to Z' cave detection");
        _output.WriteLine("");

        for (int p = 0; p < numPoints; p++)
        {
            float t = (float)p / (numPoints - 1);
            float x = StartX + t * (EndX - StartX);
            float y = StartY + t * (EndY - StartY);
            float expectedZ = StartZ + t * (EndZ - StartZ);

            _output.WriteLine($"--- Point {p}: ({x:F1}, {y:F1}), expected surface ~{expectedZ:F1} ---");
            _output.WriteLine($"  {"QueryZ",8} {"GroundZ",10} {"Delta",8}");

            foreach (float qz in queryHeights)
            {
                float gz = NavigationInterop.GetGroundZ(MapId, x, y, qz, 50.0f);
                float delta = gz > -50000f ? gz - expectedZ : float.NaN;
                string marker = (gz > -50000f && MathF.Abs(gz - expectedZ) < 5f) ? " <== surface" :
                               (gz > -50000f && gz < expectedZ - 15f) ? " <== CAVE/UNDERGROUND" : "";
                _output.WriteLine($"  {qz,8:F1} {gz,10:F1} {delta,8:F1}{marker}");
            }
            _output.WriteLine("");
        }

        // Also probe with EnumerateAllSurfacesAt to find ALL surfaces at each XY
        _output.WriteLine("=== EnumerateAllSurfacesAt Survey ===");
        for (int p = 0; p < numPoints; p++)
        {
            float t = (float)p / (numPoints - 1);
            float x = StartX + t * (EndX - StartX);
            float y = StartY + t * (EndY - StartY);

            float[] zValues = new float[10];
            uint[] instanceIds = new uint[10];
            int count = NavigationInterop.EnumerateAllSurfacesAt(MapId, x, y, zValues, instanceIds, 10);

            _output.WriteLine($"Point {p} ({x:F1}, {y:F1}): {count} surfaces");
            for (int s = 0; s < count; s++)
            {
                _output.WriteLine($"  Z={zValues[s]:F1} instance={instanceIds[s]}");
            }
        }
    }

    /// <summary>
    /// Simulate walking the slope route via StepPhysicsV2 and verify
    /// the Z values don't oscillate wildly.
    /// </summary>
    [Fact]
    public void SlopeRoute_StepPhysics_ZDoesNotOscillate()
    {
        Assert.True(_fixture.IsInitialized, "Physics engine not initialized");

        // Simulate 60 physics steps walking from start to end
        int numSteps = 60;
        float dt = 0.2f; // 200ms per tick (typical BG bot tick rate)
        float runSpeed = 7.0f;

        // Direction vector from start to end
        float dx = EndX - StartX;
        float dy = EndY - StartY;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float dirX = dx / dist;
        float dirY = dy / dist;
        float facing = MathF.Atan2(dirY, dirX);

        float posX = StartX, posY = StartY, posZ = StartZ;
        float prevGroundZ = StartZ;
        float vx = dirX * runSpeed;
        float vy = dirY * runSpeed;

        int zOscillations = 0;
        float prevZ = posZ;
        int direction = 0; // 1=up, -1=down

        _output.WriteLine($"=== StepPhysics Slope Walk Simulation ===");
        _output.WriteLine($"Route: ({StartX},{StartY},{StartZ}) -> ({EndX},{EndY},{EndZ})");
        _output.WriteLine($"Steps: {numSteps}, dt={dt}s, speed={runSpeed}");
        _output.WriteLine("");
        _output.WriteLine($"{"Step",4} {"X",10} {"Y",12} {"Z",10} {"GroundZ",10} {"DeltaZ",8} {"Flag",12}");
        _output.WriteLine(new string('-', 70));

        for (int step = 0; step < numSteps; step++)
        {
            var input = new NavigationInterop.PhysicsInput
            {
                MoveFlags = 0x00000001, // MOVEFLAG_FORWARD
                X = posX,
                Y = posY,
                Z = posZ,
                Orientation = facing,
                Vx = vx,
                Vy = vy,
                Vz = 0f,
                WalkSpeed = 2.5f,
                RunSpeed = runSpeed,
                RunBackSpeed = 4.5f,
                SwimSpeed = 4.722f,
                SwimBackSpeed = 2.5f,
                TurnSpeed = 3.14159f,
                Height = 1.83f,
                Radius = 0.45f,
                PrevGroundZ = prevGroundZ,
                StepUpBaseZ = -200000f,
                MapId = MapId,
                DeltaTime = dt
            };

            var output = NavigationInterop.StepPhysicsV2(ref input);

            float deltaZ = output.Z - prevZ;
            string flag = "";

            // Detect Z oscillation: direction changes > 5y are abnormal on a smooth slope
            int newDir = deltaZ > 0.5f ? 1 : (deltaZ < -0.5f ? -1 : 0);
            if (newDir != 0 && direction != 0 && newDir != direction && MathF.Abs(deltaZ) > 5f)
            {
                zOscillations++;
                flag = "OSCILLATION";
            }
            if (newDir != 0) direction = newDir;

            if (output.Z < -10f)
                flag = "UNDERGROUND";

            _output.WriteLine($"{step,4} {output.X,10:F1} {output.Y,12:F1} {output.Z,10:F1} {output.GroundZ,10:F1} {deltaZ,8:F1} {flag,12}");

            posX = output.X;
            posY = output.Y;
            posZ = output.Z;
            prevZ = posZ;
            if (output.GroundZ > -50000f)
                prevGroundZ = output.GroundZ;

            // Stop if we've reached the destination
            float remainDist = MathF.Sqrt((posX - EndX) * (posX - EndX) + (posY - EndY) * (posY - EndY));
            if (remainDist < 5f)
            {
                _output.WriteLine($"\n  Arrived at destination after {step + 1} steps");
                break;
            }
        }

        _output.WriteLine($"\nZ oscillations: {zOscillations}");
        _output.WriteLine($"Final position: ({posX:F1}, {posY:F1}, {posZ:F1})");

        // Z should NOT oscillate on a smooth slope
        Assert.True(zOscillations <= 2,
            $"Z oscillated {zOscillations} times during slope walk — physics engine is bouncing between surface and underground geometry");
    }

    /// <summary>
    /// Diagnostic: use GetGroundZBypassCache to compare VMAP, ADT, BIH, and SceneCache
    /// results at each point along the slope. Identifies which subsystem returns the wrong Z.
    /// </summary>
    [Fact]
    public void SlopeRoute_BypassCache_IdentifiesBadSubsystem()
    {
        Assert.True(_fixture.IsInitialized, "Physics engine not initialized");

        int numSamples = 10;

        _output.WriteLine("=== GetGroundZBypassCache: VMAP vs ADT vs BIH vs SceneCache ===");
        _output.WriteLine($"Route: ({StartX},{StartY}) -> ({EndX},{EndY})");
        _output.WriteLine("");
        _output.WriteLine($"{"#",3} {"X",8} {"Y",10} {"QueryZ",7} {"Result",8} {"VMAP",8} {"ADT",8} {"BIH",8} {"Scene",8} {"ExpZ",8}");
        _output.WriteLine(new string('-', 85));

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / (numSamples - 1);
            float x = StartX + t * (EndX - StartX);
            float y = StartY + t * (EndY - StartY);
            float expectedZ = StartZ + t * (EndZ - StartZ);

            float result = NavigationInterop.GetGroundZBypassCache(
                MapId, x, y, expectedZ, 50.0f,
                out float vmapZ, out float adtZ, out float bihZ, out float sceneCacheZ);

            _output.WriteLine($"{i,3} {x,8:F1} {y,10:F1} {expectedZ,7:F1} {result,8:F1} {vmapZ,8:F1} {adtZ,8:F1} {bihZ,8:F1} {sceneCacheZ,8:F1} {expectedZ,8:F1}");
        }
    }

    /// <summary>
    /// Compare GetTerrainHeight (bilinear, original MaNGOS formula) vs GetGroundZ (triangle cache + multi-source)
    /// at each point along the slope. If they disagree, the triangle cache has a bug.
    /// </summary>
    [Fact]
    public void SlopeRoute_BilinearVsTriangleCache_Comparison()
    {
        Assert.True(_fixture.IsInitialized, "Physics engine not initialized");

        int numSamples = 30;
        int mismatches = 0;

        _output.WriteLine("=== Bilinear (GetTerrainHeight) vs TriangleCache (GetGroundZ ADT) ===");
        _output.WriteLine($"{"#",3} {"X",10} {"Y",12} {"Bilinear",10} {"TriCache",10} {"Diff",8} {"Status",10}");
        _output.WriteLine(new string('-', 70));

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / (numSamples - 1);
            float x = StartX + t * (EndX - StartX);
            float y = StartY + t * (EndY - StartY);
            float expectedZ = StartZ + t * (EndZ - StartZ);

            float bilinear = NavigationInterop.GetTerrainHeight(MapId, x, y);
            float groundZ = NavigationInterop.GetGroundZ(MapId, x, y, expectedZ, 50.0f);

            float diff = (bilinear > -50000f && groundZ > -50000f) ? MathF.Abs(bilinear - groundZ) : float.NaN;
            string status = float.IsNaN(diff) ? "NO_DATA" : (diff > 1.0f ? "MISMATCH" : "OK");
            if (diff > 1.0f) mismatches++;

            _output.WriteLine($"{i,3} {x,10:F1} {y,12:F1} {bilinear,10:F1} {groundZ,10:F1} {diff,8:F1} {status,10}");
        }

        _output.WriteLine($"\nMismatches: {mismatches}/{numSamples}");
    }
}
