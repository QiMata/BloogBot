using GameData.Core.Models;
using System;
using System.Runtime.InteropServices;

namespace BotRunner.Physics;

/// <summary>
/// P9.18: Batch physics step processing. Collects physics inputs from multiple bots
/// and processes them in a single P/Invoke call, amortizing marshaling overhead.
/// Target: process 100 physics steps per call instead of 1.
///
/// Uses StepPhysicsV2Batch export from Navigation.dll when available,
/// falls back to sequential StepPhysicsV2 calls otherwise.
/// </summary>
public static class PhysicsBatchProcessor
{
    /// <summary>
    /// Process a batch of physics inputs in one call.
    /// Returns an array of outputs corresponding 1:1 with inputs.
    /// </summary>
    public static PhysicsOutputInterop[] ProcessBatch(
        PhysicsInputInterop[] inputs,
        float dt,
        bool useBatchApi = true)
    {
        if (inputs.Length == 0) return [];

        var outputs = new PhysicsOutputInterop[inputs.Length];

        if (useBatchApi && IsBatchApiAvailable())
        {
            // Batch P/Invoke — single marshaling boundary for N inputs
            StepPhysicsV2Batch(inputs, inputs.Length, dt, outputs);
        }
        else
        {
            // Sequential fallback — one P/Invoke per input
            for (int i = 0; i < inputs.Length; i++)
            {
                outputs[i] = StepPhysicsV2Single(inputs[i], dt);
            }
        }

        return outputs;
    }

    /// <summary>Check if the batch API is exported from Navigation.dll.</summary>
    public static bool IsBatchApiAvailable()
    {
        try
        {
            // Try to resolve the batch function pointer
            var handle = NativeLibrary.Load("Navigation");
            var found = NativeLibrary.TryGetExport(handle, "StepPhysicsV2Batch", out _);
            NativeLibrary.Free(handle);
            return found;
        }
        catch
        {
            return false;
        }
    }

    // Batch API — processes N inputs in one call
    [DllImport("Physics", CallingConvention = CallingConvention.Cdecl, EntryPoint = "StepPhysicsV2Batch")]
    private static extern void StepPhysicsV2Batch(
        [In] PhysicsInputInterop[] inputs,
        int count,
        float dt,
        [Out] PhysicsOutputInterop[] outputs);

    // Single API — fallback
    [DllImport("Physics", CallingConvention = CallingConvention.Cdecl, EntryPoint = "StepPhysicsV2")]
    private static extern PhysicsOutputInterop StepPhysicsV2Single(
        in PhysicsInputInterop input,
        float dt);

    /// <summary>Interop struct matching Navigation.dll PhysicsInput layout.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PhysicsInputInterop
    {
        public float PosX, PosY, PosZ;
        public float Facing;
        public uint MovementFlags;
        public float ForwardSpeed, BackwardSpeed, SwimSpeed, TurnSpeed;
        public float JumpVelocity;
        public float FallStartZ;
        public uint FallTimeMs;
        public float TransportPosX, TransportPosY, TransportPosZ;
        public float TransportFacing;
        public ulong TransportGuid;
    }

    /// <summary>Interop struct matching Navigation.dll PhysicsOutput layout.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PhysicsOutputInterop
    {
        public float PosX, PosY, PosZ;
        public float Facing;
        public uint MovementFlags;
        public float FallStartZ;
        public uint FallTimeMs;
        public byte IsGrounded;
        public byte HitWall;
        public float WallNormalX, WallNormalY;
        public float BlockedFraction;
    }
}
