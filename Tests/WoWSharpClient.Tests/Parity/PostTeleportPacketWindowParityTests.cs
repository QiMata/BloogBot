using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;
using WoWSharpClient.Utils;
using Xunit;

namespace WoWSharpClient.Tests.Parity;

/// <summary>
/// Pins the post-teleport packet-window parity oracle for the BG bot
/// double-fall investigation (Stream 1 of
/// docs/handoff_session_bg_movement_parity_followup_v5.md).
///
/// The committed FG baseline at
/// Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json
/// is the binary-parity oracle: it was captured live from WoW.exe
/// (NetClient::Send / NetClient::ProcessMessage hooks) for a same-map
/// teleport from Durotar road ground (Z=38) to Z+10. Any future change
/// that regresses the BG bot's post-teleport packet behaviour will diverge
/// from the oracle and fail the parity assertions in this file.
/// </summary>
[Collection("Sequential ObjectManager tests")]
public sealed class PostTeleportPacketWindowParityTests
{
    private const string BaselineFileName = "foreground_durotar_vertical_drop_baseline.json";
    private const ulong CapturedPlayerGuid = 366ul; // matches captured ACK fixture

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void ForegroundBaseline_ReportsExpectedTeleportPacketSequence()
    {
        var fixture = LoadBaseline();

        Assert.Equal(1, fixture.SchemaVersion);
        Assert.Equal("post_teleport_packet_window", fixture.CaptureScenario);

        // Trigger must be inbound MSG_MOVE_TELEPORT_ACK at deltaMs=0 — the
        // server-side teleport notification that drives both FG and BG.
        Assert.NotNull(fixture.Trigger);
        Assert.Equal("Recv", fixture.Trigger!.Direction);
        Assert.Equal("MSG_MOVE_TELEPORT_ACK", fixture.Trigger.OpcodeName);
        Assert.Equal(0, fixture.Trigger.DeltaMs);
        Assert.Equal(37, fixture.Trigger.Size);

        // The first packet in the window IS the trigger.
        var packets = fixture.Packets;
        Assert.NotNull(packets);
        Assert.True(packets!.Count > 0, "Captured window must contain at least the trigger.");
        Assert.Equal("MSG_MOVE_TELEPORT_ACK", packets[0].OpcodeName);
        Assert.Equal("Recv", packets[0].Direction);

        // Outbound stream from FG (the binary's authoritative behaviour).
        var fgOutbound = packets.Where(p => p.Direction == "Send").ToArray();
        Assert.True(fgOutbound.Length >= 4,
            $"FG baseline must record >=4 outbound packets in the snap window; got {fgOutbound.Length}.");

        // First outbound MUST be MSG_MOVE_TELEPORT_ACK within ~50ms of the trigger.
        var firstAck = fgOutbound[0];
        Assert.Equal("MSG_MOVE_TELEPORT_ACK", firstAck.OpcodeName);
        Assert.True(firstAck.DeltaMs <= 50,
            $"FG outbound ACK must fire within 50ms of trigger; got {firstAck.DeltaMs}ms.");
        Assert.Equal(20, firstAck.Size); // 4 opcode + 16 payload (guid+counter+clientTimeMs)

        // The binary continues broadcasting heartbeats during the fall,
        // and emits MSG_MOVE_FALL_LAND on landing. This is the data the
        // BG bot's MovementController:379 _needsGroundSnap suppression is
        // currently dropping — pin it so we can't lose this evidence.
        var heartbeats = fgOutbound.Where(p => p.OpcodeName == "MSG_MOVE_HEARTBEAT").ToArray();
        Assert.True(heartbeats.Length >= 2,
            $"FG baseline must record >=2 MSG_MOVE_HEARTBEAT during the snap window; got {heartbeats.Length}.");

        var fallLands = fgOutbound.Where(p => p.OpcodeName == "MSG_MOVE_FALL_LAND").ToArray();
        Assert.Single(fallLands);
        Assert.True(fallLands[0].DeltaMs > 1000 && fallLands[0].DeltaMs < 2000,
            $"FG MSG_MOVE_FALL_LAND must fire 1-2s after trigger; got {fallLands[0].DeltaMs}ms.");
    }

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void Background_AfterTeleportTrigger_EmitsOutboundTeleportAckMatchingForegroundShape()
    {
        // Pins the part of the parity story BG already gets right: when the
        // server-side teleport notification (inbound MSG_MOVE_TELEPORT_ACK)
        // arrives, BG flushes a 16-byte client ACK with the same structural
        // shape as WoW.exe (guid + counter + clientTimeMs).
        var fgFixture = LoadBaseline();
        var fgOutboundAck = fgFixture.Packets!
            .First(p => p.Direction == "Send" && p.OpcodeName == "MSG_MOVE_TELEPORT_ACK");

        using var trace = new PacketFlowTraceFixture();
        trace.SeedLocalPlayer(
            CapturedPlayerGuid,
            mapId: 1,
            position: new Position(-460f, -4760f, 38f),
            facing: 0f,
            fixedWorldTimeMs: 4242);
        trace.EnsureTeleportAckFlushSupport();

        // The FG fixture's trigger PayloadBytes start with the 2-byte SMSG
        // opcode prefix; strip it to get the bare payload that PacketFlowTraceFixture
        // expects (it adds the opcode separately in Dispatch).
        var triggerPayload = HexToBytes(fgFixture.Trigger!.PayloadHex)[2..];
        trace.Dispatch(Opcode.MSG_MOVE_TELEPORT_ACK, triggerPayload);

        Assert.True(trace.FlushTeleportAck());

        var bgOutbound = trace.Events
            .Where(e => e.Kind == "outbound" && e.Opcode == Opcode.MSG_MOVE_TELEPORT_ACK)
            .ToArray();
        Assert.Single(bgOutbound);

        // BG's outbound payload (no opcode prefix) must be 16 bytes:
        // 8 guid + 4 counter + 4 clientTimeMs — matching the FG outbound ACK
        // shape (FG payload includes the 4-byte CMSG opcode prefix → 20 total).
        var bgPayload = bgOutbound[0].Payload!;
        Assert.Equal(16, bgPayload.Length);
        Assert.Equal(20, fgOutboundAck.Size);

        // First 8 bytes: guid. Both FG and BG should use the player guid 366.
        Assert.Equal(CapturedPlayerGuid, BitConverter.ToUInt64(bgPayload, 0));

        // ClientTimeMs differs by design (BG uses fixedWorldTimeMs=4242; FG
        // uses WoW.exe's GetTickCount). The structural shape and field
        // ordering is what parity pins.
    }

    [Fact(Skip = "Stream 2B — BG MovementController:379 currently suppresses heartbeats and FALL_LAND during _needsGroundSnap. Unblock once the suppression is narrowed to match the FG fixture.")]
    [Trait("Category", "PacketFlowParity")]
    public void Background_AfterTeleportTrigger_OutboundStream_StructurallyMatchesForegroundBaseline()
    {
        // End-state regression: BG, after receiving the same trigger and
        // running its physics tick loop for the FG fixture's window
        // duration, must emit the same ordered sequence of outbound opcode
        // names as WoW.exe. Driving the physics tick loop in a unit test
        // also requires native NavigationDLL ground-snap support; this test
        // is the canonical exit criterion for Stream 2B.
        //
        // Expected when unblocked:
        //   var fg = LoadBaseline();
        //   var fgOutboundOpcodes = fg.Packets.Where(p => p.Direction=="Send").Select(p => p.OpcodeName).ToArray();
        //   var bgOutboundOpcodes = ... drive PacketFlowTraceFixture + tick MovementController for fg.WindowDurationMs ...
        //   Assert.Equal(fgOutboundOpcodes, bgOutboundOpcodes);
        Assert.Fail("Pending Stream 2B implementation.");
    }

    private static PostTeleportWindowFixture LoadBaseline()
    {
        var path = ResolveBaselinePath();
        var json = File.ReadAllText(path);
        var fixture = JsonSerializer.Deserialize<PostTeleportWindowFixture>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(fixture);
        return fixture!;
    }

    private static string ResolveBaselinePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "Tests",
                "WoWSharpClient.Tests",
                "Fixtures",
                "post_teleport_packet_window",
                BaselineFileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not resolve {BaselineFileName} starting from {AppContext.BaseDirectory}.");
    }

    private static byte[] HexToBytes(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return Array.Empty<byte>();

        if ((hex.Length & 1) != 0)
            throw new ArgumentException("Hex string must have an even length.", nameof(hex));

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    private sealed class PostTeleportWindowFixture
    {
        public int SchemaVersion { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public string CaptureScenario { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string CloseReason { get; set; } = string.Empty;
        public int WindowDurationMs { get; set; }
        public PostTeleportPacketEntry? Trigger { get; set; }
        public List<PostTeleportPacketEntry>? Packets { get; set; }
    }

    private sealed class PostTeleportPacketEntry
    {
        public int DeltaMs { get; set; }
        public string Direction { get; set; } = string.Empty;
        public ushort Opcode { get; set; }
        public string OpcodeName { get; set; } = string.Empty;
        public int Size { get; set; }
        public string PayloadHex { get; set; } = string.Empty;
    }
}
