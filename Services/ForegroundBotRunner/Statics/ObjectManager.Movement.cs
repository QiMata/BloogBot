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


        public void MoveToward(Position position, float facing)
        {
            SetFacing(facing);
            StartMovement(ControlBits.Front);
        }


        public void SetFacing(float facing)
        {
            if (Player is not LocalPlayer localPlayer || localPlayer.Pointer == nint.Zero)
                return;

            Functions.SetFacing(nint.Add(localPlayer.Pointer, MemoryAddresses.LocalPlayer_SetFacingOffset), facing);
            Functions.SendMovementUpdate(localPlayer.Pointer, (int)Opcode.MSG_MOVE_SET_FACING);
        }
        // the client will NOT send a packet to the server if a key is already pressed, so you're safe to spam this
        public void StartMovement(ControlBits bits)
        {
            if (bits == ControlBits.Nothing)
                return;

            Functions.SetControlBit((int)bits, 1, Environment.TickCount);
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

            Functions.SetControlBit((int)bits, 0, Environment.TickCount);
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
            Functions.ReleaseCorpse(localPlayer.Pointer);
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

            // If we're already turning toward a different position, reset
            if (_turning && pos != _turningToward)
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
                return;

            Face(pos);
            StartMovement(ControlBits.Front);
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
