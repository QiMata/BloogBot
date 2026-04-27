using GameData.Core.Enums;
using GameData.Core.Models;
using System;
using System.IO;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;
using Xunit;

namespace WoWSharpClient.Tests.Movement
{
    /// <summary>
    /// Pins the wire-byte layout produced by <see cref="MovementPacketHandler"/> ACK
    /// builders against the WoW.exe 1.12.1 decompilation. Regression tests for the
    /// "BG bot post-teleport double-fall" investigation (Task B in
    /// docs/handoff_session_bg_movement_parity.md).
    ///
    /// Decompilation references in <c>docs/physics/</c>:
    ///   - <c>msg_move_teleport_handler.md</c> + <c>0x602FB0_disasm.txt</c>: WoW.exe
    ///     emits MSG_MOVE_TELEPORT_ACK (0xC7) as <c>opcode + GUID(8) + uint32 + uint32</c>
    ///     where the two uint32s are arg2 (counter) and arg1 (clientTimeMs) of the
    ///     internal handler. No MovementInfo block is included.
    ///   - <c>packet_ack_timing.md</c>: confirms the deferred 0x468570-gated send path.
    /// </summary>
    public class MovementPacketHandlerAckTests
    {
        private static WoWLocalPlayer MakePlayer()
        {
            return new WoWLocalPlayer(new HighGuid(0x12345678))
            {
                Position = new Position(100f, 200f, 50f),
                Facing = 1.57f,
                WalkSpeed = 2.5f,
                RunSpeed = 7.0f,
                RunBackSpeed = 4.5f,
                SwimSpeed = 4.722f,
                SwimBackSpeed = 2.5f,
            };
        }

        [Fact]
        public void BuildMoveTeleportAckPayload_MatchesWowExeDisasmLayout()
        {
            // WoW.exe 0x602FB0 emits opcode 0xC7 with payload:
            //   uint64 guid     (call 0x418370)
            //   uint32 counter  (arg2 — call 0x4181f0)
            //   uint32 time     (arg1 — call 0x4182b0)
            // Total: exactly 16 bytes. No MovementInfo follows.
            var player = MakePlayer();
            const uint counter = 0xDEADBEEF;
            const uint clientTimeMs = 0x12345;

            var payload = MovementPacketHandler.BuildMoveTeleportAckPayload(
                player, counter, clientTimeMs);

            Assert.Equal(16, payload.Length);

            using var ms = new MemoryStream(payload);
            using var r = new BinaryReader(ms);
            Assert.Equal(player.Guid, r.ReadUInt64());
            Assert.Equal(counter, r.ReadUInt32());
            Assert.Equal(clientTimeMs, r.ReadUInt32());
            Assert.Equal(payload.Length, ms.Position);
        }

        [Fact]
        public void BuildMoveTeleportAckPayload_WritesGuidLittleEndian()
        {
            var player = new WoWLocalPlayer(new HighGuid(0x0102030405060708UL))
            {
                Position = new Position(0f, 0f, 0f),
            };

            var payload = MovementPacketHandler.BuildMoveTeleportAckPayload(player, 0u, 0u);

            // Little-endian byte order: low byte first.
            Assert.Equal(0x08, payload[0]);
            Assert.Equal(0x07, payload[1]);
            Assert.Equal(0x06, payload[2]);
            Assert.Equal(0x05, payload[3]);
            Assert.Equal(0x04, payload[4]);
            Assert.Equal(0x03, payload[5]);
            Assert.Equal(0x02, payload[6]);
            Assert.Equal(0x01, payload[7]);
        }

        [Fact]
        public void BuildMoveTeleportAckPayload_DoesNotIncludeMovementInfoBlock()
        {
            // Regression guard: an earlier MEMORY.md note claimed teleport ACK had to
            // carry a full MovementInfo (position/flags/etc). The 0x602FB0 disasm
            // proves otherwise. This test fails if anyone re-introduces that mistake
            // by appending MovementInfo bytes to the teleport ACK payload.
            var player = MakePlayer();
            player.MovementFlags = MovementFlags.MOVEFLAG_FALLINGFAR;

            var payloadWithFlags = MovementPacketHandler.BuildMoveTeleportAckPayload(
                player, 1u, 1000u);

            player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            var payloadWithoutFlags = MovementPacketHandler.BuildMoveTeleportAckPayload(
                player, 1u, 1000u);

            Assert.Equal(payloadWithFlags, payloadWithoutFlags);
            Assert.Equal(16, payloadWithFlags.Length);
        }

        [Fact]
        public void BuildForceMoveAck_IncludesGuidCounterAndMovementInfo()
        {
            // CMSG_FORCE_MOVE_ROOT_ACK / CMSG_FORCE_MOVE_UNROOT_ACK / KNOCKBACK_ACK
            // wire format: GUID(8) + counter(4) + MovementInfo(...).
            var player = MakePlayer();
            const uint counter = 0xCAFEF00D;
            const uint clientTimeMs = 0x77777777;

            var ackPayload = typeof(MovementPacketHandler)
                .GetMethod("BuildForceMoveAck", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(null, new object[] { player, counter, clientTimeMs })!
                as byte[];
            Assert.NotNull(ackPayload);

            using var ms = new MemoryStream(ackPayload!);
            using var r = new BinaryReader(ms);
            Assert.Equal(player.Guid, r.ReadUInt64());
            Assert.Equal(counter, r.ReadUInt32());

            // Remainder is the MovementInfo block; verify the leading flags+timestamp
            // match BuildMovementInfoBuffer.
            uint flags = r.ReadUInt32();
            uint timestamp = r.ReadUInt32();
            Assert.Equal((uint)MovementFlags.MOVEFLAG_NONE, flags);
            Assert.Equal(clientTimeMs, timestamp);
        }

        [Fact]
        public void BuildForceSpeedChangeAck_TrailsMovementInfoWithFloatSpeed()
        {
            // CMSG_FORCE_*_SPEED_CHANGE_ACK: GUID(8) + counter(4) + MovementInfo + float speed.
            var player = MakePlayer();
            const uint counter = 7u;
            const uint clientTimeMs = 0x10203040;
            const float speed = 7.5f;

            var ackPayload = typeof(MovementPacketHandler)
                .GetMethod("BuildForceSpeedChangeAck", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(null, new object[] { player, counter, clientTimeMs, speed })!
                as byte[];
            Assert.NotNull(ackPayload);

            // Last 4 bytes must be the speed float we sent back.
            Assert.True(ackPayload!.Length >= 4);
            float trailingSpeed = BitConverter.ToSingle(ackPayload, ackPayload.Length - 4);
            Assert.Equal(speed, trailingSpeed);

            // Header layout still GUID + counter.
            using var ms = new MemoryStream(ackPayload);
            using var r = new BinaryReader(ms);
            Assert.Equal(player.Guid, r.ReadUInt64());
            Assert.Equal(counter, r.ReadUInt32());
        }
    }
}
