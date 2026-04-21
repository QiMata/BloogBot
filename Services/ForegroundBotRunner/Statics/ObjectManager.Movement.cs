using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System.Runtime.InteropServices;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace ForegroundBotRunner.Statics
{
    public partial class ObjectManager
    {
        private static readonly ControlBits[] DiscreteControlBits =
        [
            ControlBits.Front,
            ControlBits.Back,
            ControlBits.Left,
            ControlBits.Right,
            ControlBits.StrafeLeft,
            ControlBits.StrafeRight,
            ControlBits.Jump,
            ControlBits.CtmWalk,
        ];


        public void MoveToward(Position position, float facing)
        {
            SetFacing(facing);
            StartMovement(ControlBits.Front);
        }


        private DateTime _lastSetFacingDiagUtc = DateTime.MinValue;

        public void SetFacing(float facing)
        {
            if (Player is not LocalPlayer localPlayer || localPlayer.Pointer == nint.Zero)
            {
                DiagLog($"[SetFacing] BAIL: Player={Player != null}, Pointer={((Player as LocalPlayer)?.Pointer ?? nint.Zero)}");
                return;
            }

            // Skip redundant facing updates. MoveToward fires every 100ms and often
            // requests the same facing — sending MSG_MOVE_SET_FACING every tick floods
            // the server (~70 packets in 7s) and triggers VMaNGOS anti-cheat disconnect.
            var currentFacing = localPlayer.Facing;
            if (MathF.Abs(facing - currentFacing) < 0.01f)
                return;

            Functions.SetFacing(nint.Add(localPlayer.Pointer, MemoryAddresses.LocalPlayer_SetFacingOffset), facing);
            Functions.SendMovementUpdate(localPlayer.Pointer, (int)Opcode.MSG_MOVE_SET_FACING);

            if ((DateTime.UtcNow - _lastSetFacingDiagUtc).TotalSeconds >= 2)
            {
                DiagLog($"[SetFacing] facing={facing:F3} hp={localPlayer.Health}/{localPlayer.MaxHealth} ghost={localPlayer.InGhostForm}");
                _lastSetFacingDiagUtc = DateTime.UtcNow;
            }
        }
        // the client will NOT send a packet to the server if a key is already pressed, so you're safe to spam this
        public void StartMovement(ControlBits bits)
        {
            if (bits == ControlBits.Nothing)
                return;

            try
            {
                var shouldUseGhostForwardInput = ShouldUseGhostForwardKeyInput(Player, bits);
                if (shouldUseGhostForwardInput)
                {
                    ThreadSynchronizer.SimulateForwardKeyPress();
                    bits &= ~ControlBits.Front;
                    if (bits == ControlBits.Nothing)
                        return;
                }

                int tickCount = Environment.TickCount;
                var expandedBits = ExpandControlBits(bits);
                ThreadSynchronizer.RunOnMainThread(() =>
                {
                    foreach (var bit in expandedBits)
                    {
                        Functions.SetControlBit((int)bit, 1, tickCount);
                    }
                });
            }
            catch (Exception ex)
            {
                DiagLog($"[StartMovement] exception bits={bits}: {ex}");
                throw;
            }
        }



        public void StopAllMovement()
        {
            // Always clear all movement control bits unconditionally.
            // MovementFlags can read 0x0 when opposing directions cancel out,
            // but the underlying control bits are still set.
            var bits = ControlBits.Front | ControlBits.Back | ControlBits.Left | ControlBits.Right | ControlBits.StrafeLeft | ControlBits.StrafeRight;
            StopMovement(bits);
        }

        public void ForceStopImmediate()
        {
            StopAllMovement();

            if (Player is not LocalPlayer localPlayer || localPlayer.Pointer == nint.Zero)
                return;

            ThreadSynchronizer.RunOnMainThread(() =>
            {
                Functions.SendMovementUpdate(localPlayer.Pointer, (int)Opcode.MSG_MOVE_STOP);
                return 0;
            });

            Log.Information("[ForceStopImmediate] Cleared all movement flags and sent MSG_MOVE_STOP");
            DiagLog("[ForceStopImmediate] Cleared all movement flags and sent MSG_MOVE_STOP");
        }



        public void StopMovement(ControlBits bits)
        {
            if (bits == ControlBits.Nothing)
                return;

            try
            {
                var shouldUseGhostForwardInput = ShouldUseGhostForwardKeyInput(Player, bits);
                if (shouldUseGhostForwardInput)
                {
                    ThreadSynchronizer.SimulateForwardKeyRelease();
                    bits &= ~ControlBits.Front;
                    if (bits == ControlBits.Nothing)
                        return;
                }

                int tickCount = Environment.TickCount;
                var expandedBits = ExpandControlBits(bits);
                ThreadSynchronizer.RunOnMainThread(() =>
                {
                    foreach (var bit in expandedBits)
                    {
                        Functions.SetControlBit((int)bit, 0, tickCount);
                    }
                });
            }
            catch (Exception ex)
            {
                DiagLog($"[StopMovement] exception bits={bits}: {ex}");
                throw;
            }
        }

        internal static bool ShouldUseGhostForwardKeyInput(IWoWLocalPlayer? player, ControlBits bits)
        {
            if ((bits & ControlBits.Front) == 0 || player == null)
                return false;

            try
            {
                return ((uint)player.PlayerFlags & (uint)PlayerFlags.PLAYER_FLAGS_GHOST) != 0;
            }
            catch
            {
                return false;
            }
        }

        internal static IReadOnlyList<ControlBits> ExpandControlBits(ControlBits bits)
        {
            if (bits == ControlBits.Nothing)
                return [];

            var result = new List<ControlBits>();
            var handled = ControlBits.Nothing;

            foreach (var discreteBit in DiscreteControlBits)
            {
                if ((bits & discreteBit) == 0)
                    continue;

                result.Add(discreteBit);
                handled |= discreteBit;
            }

            var remaining = bits & ~handled;
            if (remaining != ControlBits.Nothing)
            {
                result.Add(remaining);
            }

            return result;
        }



        public void Jump()
        {
            // Vanilla 1.12.1 has no dedicated jump function (JumpFunPtr = 0).
            // SetControlBit(Jump) doesn't work for impulse actions.
            // Simulate spacebar press via PostMessage to trigger the jump.
            ThreadSynchronizer.SimulateSpacebarPress();
        }



        public void ReleaseCorpse()
        {
            if (Player is not LocalPlayer localPlayer || localPlayer.Pointer == nint.Zero)
                return;
            var ptr = localPlayer.Pointer;
            ThreadSynchronizer.RunOnMainThread(() =>
            {
                Functions.ReleaseCorpse(ptr);
                return 0;
            });
        }



        public void ReleaseSpirit() => ReleaseCorpse();



        public void RetrieveCorpse()
        {
            // Prefer in-client Lua reclaim; this mirrors the normal UI reclaim action.
            // Keep native fallback for older offsets/builds.
            try
            {
                MainThreadLuaCall("RetrieveCorpse()");
                DiagLog("RetrieveCorpse: Lua RetrieveCorpse() invoked");
            }
            catch (Exception ex)
            {
                DiagLog($"RetrieveCorpse: Lua path failed: {ex.Message}");
            }

            try
            {
                Functions.RetrieveCorpse();
                DiagLog("RetrieveCorpse: native function invoked");
            }
            catch (Exception ex)
            {
                DiagLog($"RetrieveCorpse: native path failed: {ex.Message}");
            }
        }

        // ── Smooth turning state (ported from reference LocalPlayer.cs) ──
        private readonly Random _facingRandom = new();


        private bool _turning;


        private int _totalTurns;


        private int _turnCount;


        private float _amountPerTurn;


        private Position? _turningToward;

        /// <summary>
        /// Smoothly turns the player to face the target position.
        /// Splits the turn into 2-5 steps for human-like behavior.
        /// Call repeatedly (e.g. every tick) until the player is facing the target.
        /// </summary>
        public void Face(Position pos)
        {
            if (pos == null || Player == null) return;

            // Correct negative facing (client bug)
            if (Player.Facing < 0)
            {
                SetFacing((float)(Math.PI * 2) + Player.Facing);
                return;
            }

            // If we're already turning toward a different position, reset.
            // Position is a reference type without operator== overrides, so compare values.
            if (_turning && _turningToward != null
                && (MathF.Abs(pos.X - _turningToward.X) > 0.01f
                    || MathF.Abs(pos.Y - _turningToward.Y) > 0.01f
                    || MathF.Abs(pos.Z - _turningToward.Z) > 0.01f))
            {
                ResetFacingState();
                return;
            }

            // Already facing the target - nothing to do
            if (!_turning && Player.IsFacing(pos))
                return;

            if (!_turning)
            {
                var requiredFacing = Player.GetFacingForPosition(pos);
                float amountToTurn;
                if (requiredFacing > Player.Facing)
                {
                    if (requiredFacing - Player.Facing > Math.PI)
                        amountToTurn = -((float)(Math.PI * 2) - requiredFacing + Player.Facing);
                    else
                        amountToTurn = requiredFacing - Player.Facing;
                }
                else
                {
                    if (Player.Facing - requiredFacing > Math.PI)
                        amountToTurn = (float)(Math.PI * 2) - Player.Facing + requiredFacing;
                    else
                        amountToTurn = requiredFacing - Player.Facing;
                }

                // Small turn - just snap to target
                if (Math.Abs(amountToTurn) < 0.05)
                {
                    SetFacing(requiredFacing);
                    ResetFacingState();
                    return;
                }

                _turning = true;
                _turningToward = pos;
                _totalTurns = _facingRandom.Next(2, 5);
                _amountPerTurn = amountToTurn / _totalTurns;
            }

            if (_turning)
            {
                if (_turnCount < _totalTurns - 1)
                {
                    var twoPi = (float)(Math.PI * 2);
                    var newFacing = Player.Facing + _amountPerTurn;

                    if (newFacing < 0)
                        newFacing = twoPi + _amountPerTurn + Player.Facing;
                    else if (newFacing > twoPi)
                        newFacing = _amountPerTurn - (twoPi - Player.Facing);

                    SetFacing(newFacing);
                    _turnCount++;
                }
                else
                {
                    SetFacing(Player.GetFacingForPosition(pos));
                    ResetFacingState();
                }
            }
        }



        private void ResetFacingState()
        {
            _turning = false;
            _totalTurns = 0;
            _turnCount = 0;
            _amountPerTurn = 0;
            _turningToward = null;
            StopMovement(ControlBits.StrafeLeft);
            StopMovement(ControlBits.StrafeRight);
        }



        public void MoveToward(Position pos)
        {
            if (pos == null || Player == null)
            {
                DiagLog($"[MoveToward] BAIL: pos={pos != null}, Player={Player != null}");
                return;
            }

            if (Player is not LocalPlayer localPlayer || localPlayer.Pointer == nint.Zero)
                return;

            // Snap-face to target and start moving forward.
            var requiredFacing = Player.GetFacingForPosition(pos);

            // Direct native calls (no ThreadSync) to avoid ManualResetEventSlim heap
            // pressure in the injected runtime — called every 100ms tick during movement.
            SetFacing(requiredFacing);
            StartMovement(ControlBits.Front);
            ResetFacingState();
        }



        public void Turn180()
        {
            if (Player is not LocalPlayer localPlayer2 || localPlayer2.Pointer == nint.Zero)
                return;
            var newFacing = Player.Facing + Math.PI;
            if (newFacing > Math.PI * 2)
                newFacing -= Math.PI * 2;
            SetFacing((float)newFacing);
        }
    }
}
