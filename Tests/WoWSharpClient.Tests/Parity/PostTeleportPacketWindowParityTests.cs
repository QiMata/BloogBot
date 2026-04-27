using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GameData.Core.Enums;
using GameData.Core.Models;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
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
    private const string BackgroundBaselineFileName = "background_durotar_vertical_drop_baseline.json";
    private const string BackgroundHighDropBaselineFileName = "background_durotar_high_drop_baseline.json";
    private const string ForegroundCrossMapBaselineFileName = "foreground_kalimdor_to_ek_cross_map_baseline.json";
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

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void Background_AfterTeleportTrigger_OutboundStream_StructurallyMatchesForegroundBaseline()
    {
        // Stream 2B exit criterion: after receiving the same teleport trigger
        // and running its physics tick loop through the FG fixture's window
        // duration, BG must emit the same ordered sequence of outbound opcode
        // names as WoW.exe — proving the suppression-drop in MovementController
        // is sufficient (no additional gating regression).
        var fg = LoadBaseline();
        var fgOutboundOpcodes = fg.Packets!
            .Where(p => p.Direction == "Send")
            .Select(p => p.OpcodeName)
            .ToArray();

        using var trace = new PacketFlowTraceFixture();
        trace.SeedLocalPlayer(
            CapturedPlayerGuid,
            mapId: 1,
            position: new Position(-460f, -4760f, 38f),
            facing: 0f,
            fixedWorldTimeMs: 4242);
        trace.EnsureTeleportAckFlushSupport();

        // Script physics: ~38 frames of free-fall (matching FG's 1.27s fall),
        // then a landed result that clears FALLINGFAR. GroundZ stays at -50001
        // during the fall so the snap clamp doesn't short-circuit (matches the
        // existing Update_PostTeleport_NoGroundBelow_AllowsGraceFall scenario).
        const int FreefallFrames = 38;
        var physicsCallCount = 0;
        NativeLocalPhysics.TestClearSceneCacheOverride ??= _ => { };
        NativeLocalPhysics.TestStepOverride = input =>
        {
            physicsCallCount++;
            bool stillFalling = physicsCallCount <= FreefallFrames;

            if (stillFalling)
            {
                return new NativePhysics.PhysicsOutput
                {
                    X = input.X,
                    Y = input.Y,
                    Z = input.Z - 1.5f,
                    Orientation = input.Orientation,
                    Pitch = input.Pitch,
                    Vx = 0f,
                    Vy = 0f,
                    Vz = -5f,
                    GroundZ = -50001f,
                    GroundNx = 0f,
                    GroundNy = 0f,
                    GroundNz = 1f,
                    MoveFlags = (uint)MovementFlags.MOVEFLAG_FALLINGFAR,
                    FallTime = (uint)(physicsCallCount * 33),
                };
            }

            return new NativePhysics.PhysicsOutput
            {
                X = input.X,
                Y = input.Y,
                Z = input.Z,
                Orientation = input.Orientation,
                Pitch = input.Pitch,
                Vx = 0f,
                Vy = 0f,
                Vz = 0f,
                GroundZ = input.Z,
                GroundNx = 0f,
                GroundNy = 0f,
                GroundNz = 1f,
                MoveFlags = 0u,
                FallTime = 0,
            };
        };

        try
        {
            var triggerPayload = HexToBytes(fg.Trigger!.PayloadHex)[2..];
            trace.Dispatch(Opcode.MSG_MOVE_TELEPORT_ACK, triggerPayload);
            Assert.True(trace.FlushTeleportAck(),
                "TryFlushPendingTeleportAck must succeed on the seeded fixture so the BG outbound ACK appears in the events stream before physics ticks.");

            trace.RunPhysicsFor(durationMs: (uint)fg.WindowDurationMs, stepMs: 33u);
        }
        finally
        {
            NativeLocalPhysics.TestStepOverride = null;
        }

        var bgOutboundOpcodes = trace.Events
            .Where(e => e.Kind == "outbound" && e.Opcode.HasValue)
            .Select(e => e.Opcode!.Value.ToString())
            .ToArray();

        // Stream 2C closes the +1 HB gap: NotifyExternalPacketSent (called from
        // TryFlushPendingTeleportAck after the outbound ACK) opens a one-cadence
        // suppression window, so the first physics-tick NONE -> FALLINGFAR
        // transition does not double-fire a heartbeat inside the 500ms WoW.exe
        // leaves silent after its outbound TELEPORT_ACK.
        //
        // FG fixture order: [TELEPORT_ACK, HEARTBEAT, HEARTBEAT, FALL_LAND]
        // BG order today:   [TELEPORT_ACK, HEARTBEAT, HEARTBEAT, FALL_LAND] ← matches
        var fgOpcodeSet = string.Join(",", fgOutboundOpcodes);
        var bgOpcodeSet = string.Join(",", bgOutboundOpcodes);

        Assert.StartsWith("MSG_MOVE_TELEPORT_ACK,", bgOpcodeSet);
        Assert.EndsWith(",MSG_MOVE_FALL_LAND", bgOpcodeSet);

        // Stream 2C exit criterion: BG's outbound stream is byte-for-opcode-name
        // identical to FG's. Any future regression that re-introduces the +1 HB
        // (or any other outbound packet drift) will fail this assertion.
        Assert.Equal(fgOutboundOpcodes, bgOutboundOpcodes);
        Assert.Equal(
            "MSG_MOVE_TELEPORT_ACK,MSG_MOVE_HEARTBEAT,MSG_MOVE_HEARTBEAT,MSG_MOVE_FALL_LAND",
            bgOpcodeSet);
    }

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void BackgroundBaseline_ReportsLiveCapturedTeleportPacketSequence()
    {
        // Stream 2D oracle: the BG-side counterpart to ForegroundBaseline_*.
        // Captured by BackgroundPostTeleportWindowRecorder during a live
        // BG bot vertical-drop teleport on Durotar (Z=38 → Z=48). Pins the
        // BG-today live shape so any future BG drift fails this test.
        //
        // Stream 2E.1 closed: the spurious CMSG_SET_ACTIVE_MOVER that the
        // initial Stream 2D capture pinned was a side-effect of
        // RestoreLocalPlayerControlFromHydratedUpdate firing on every
        // teleport (the early-return condition gated on `!_isBeingTeleported`).
        // After the fix at WoWSharpObjectManager.Network.cs:758, the early
        // return triggers on `_isInControl` alone — same-map teleport leaves
        // control intact and skips the spurious SET_ACTIVE_MOVER. The
        // recapture (this baseline) confirms BG's outbound stream now begins
        // with the cadence-gated heartbeat + outbound TELEPORT_ACK, in line
        // with FG's [TELEPORT_ACK, HEARTBEAT, HEARTBEAT, FALL_LAND] shape.
        //
        // KNOWN remaining FG/BG live divergence (Stream 2E.2/2E.3 in
        // follow-up): live BG still does not emit MSG_MOVE_FALL_LAND in
        // the 2.5s window for a 10-yard drop. Server-pushed SMSG_MONSTER_MOVE
        // updates and NativeLocalPhysics ground-snap absorb the small drop
        // without going through FALLINGFAR -> grounded. Higher-Z (e.g.
        // Z+100) capture is needed to determine whether BG ever emits
        // FALL_LAND live, or whether the synthetic parity test (which
        // forces a 38-frame free-fall) is testing a code path the live
        // BG never exercises.
        var fixture = LoadBackgroundBaseline();

        Assert.Equal(1, fixture.SchemaVersion);
        Assert.Equal("post_teleport_packet_window", fixture.CaptureScenario);
        Assert.Contains("BackgroundBotRunner", fixture.Source);

        // Trigger: inbound MSG_MOVE_TELEPORT_ACK at deltaMs=0 with the
        // server-side payload (35 bytes incl. SMSG opcode prefix).
        Assert.NotNull(fixture.Trigger);
        Assert.Equal("Recv", fixture.Trigger!.Direction);
        Assert.Equal("MSG_MOVE_TELEPORT_ACK", fixture.Trigger.OpcodeName);
        Assert.Equal(0, fixture.Trigger.DeltaMs);

        var packets = fixture.Packets;
        Assert.NotNull(packets);
        Assert.True(packets!.Count > 0, "Captured BG window must contain at least the trigger.");
        Assert.Equal("MSG_MOVE_TELEPORT_ACK", packets[0].OpcodeName);
        Assert.Equal("Recv", packets[0].Direction);

        // BG-live outbound stream we observe today (post-Stream-2E.1):
        //   1. MSG_MOVE_HEARTBEAT (~60ms after trigger) — the post-teleport
        //      first-frame heartbeat. The Stream 2C cadence-gate keeps
        //      synthetic tests at zero pre-ACK heartbeats; live BG still
        //      emits one because the physics-tick HB and TryFlushPendingTeleportAck
        //      land within the same poll cycle.
        //   2. MSG_MOVE_TELEPORT_ACK (~60ms after trigger) — the outbound
        //      ACK matches FG structurally (16 bytes: guid+counter+clientTimeMs).
        //   No FALL_LAND in the 2.5s window — see comment block above for
        //   the open Stream 2E.2/2E.3 follow-up.
        //
        // Regression guard: explicitly assert NO CMSG_SET_ACTIVE_MOVER in
        // the outbound stream. The Stream 2E.1 fix removed the spurious
        // emission; if a future change re-introduces it (e.g., re-adding
        // `!_isBeingTeleported` to the early-return condition), this test
        // fails loudly.
        var bgOutbound = packets.Where(p => p.Direction == "Send").ToArray();
        Assert.True(bgOutbound.Length >= 2,
            $"BG live baseline must record >=2 outbound packets in the snap window; got {bgOutbound.Length}.");

        Assert.DoesNotContain(
            bgOutbound,
            p => p.OpcodeName == "CMSG_SET_ACTIVE_MOVER");

        var outboundAck = bgOutbound.FirstOrDefault(p => p.OpcodeName == "MSG_MOVE_TELEPORT_ACK");
        Assert.NotNull(outboundAck);
        Assert.Equal(16, outboundAck!.Size);

        var firstHeartbeat = bgOutbound.FirstOrDefault(p => p.OpcodeName == "MSG_MOVE_HEARTBEAT");
        Assert.NotNull(firstHeartbeat);
        Assert.Equal(28, firstHeartbeat!.Size);

        // The first outbound packet must fire within ~150ms of the inbound
        // trigger (BG's same-poll-cycle TryFlushPendingTeleportAck + first
        // physics-tick heartbeat).
        var firstOutbound = bgOutbound[0];
        Assert.True(firstOutbound.DeltaMs <= 150,
            $"BG first outbound packet must fire within 150ms of trigger; got {firstOutbound.DeltaMs}ms.");
    }

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void BackgroundHighDropBaseline_DoesNotEmitFallLand_PinsBgPhysicsEmissionGap()
    {
        // Stream 2E.2 oracle: a 100-yard vertical-drop teleport (Z=38 -> Z=138)
        // captured live from BG. Even at 10x the standard vertical-drop test
        // height, BG still does NOT emit MSG_MOVE_FALL_LAND in the 2.5s
        // window. Outbound stream: [MSG_MOVE_HEARTBEAT, MSG_MOVE_TELEPORT_ACK]
        // — the same shape as the 10y baseline. Server-pushed
        // SMSG_MONSTER_MOVE updates absorb the entire fall.
        //
        // This pins a CURRENT-BUG state: WoW.exe (FG) emits MSG_MOVE_FALL_LAND
        // on every fall regardless of server-side intervention. BG's
        // NativeLocalPhysics + MovementController.DetermineOpcode path is
        // either:
        //   - Never going through the FALLINGFAR -> grounded transition
        //     locally (server position updates short-circuit the local fall).
        //   - Going through the transition but suppressing the FALL_LAND
        //     packet because some gate (e.g. _isBeingTeleported, ground-snap)
        //     is still active when the transition fires.
        //
        // When Stream 2E.3 fixes the gap, this test will FAIL because
        // FALL_LAND will appear in the outbound stream. At that point,
        // update the assertion to require the FALL_LAND (and update this
        // comment block to reflect the resolved state).
        var fixture = LoadBackgroundHighDropBaseline();

        Assert.Equal(1, fixture.SchemaVersion);
        Assert.Equal("post_teleport_packet_window", fixture.CaptureScenario);
        Assert.Contains("BackgroundBotRunner", fixture.Source);

        Assert.NotNull(fixture.Trigger);
        Assert.Equal("Recv", fixture.Trigger!.Direction);
        Assert.Equal("MSG_MOVE_TELEPORT_ACK", fixture.Trigger.OpcodeName);
        Assert.Equal(0, fixture.Trigger.DeltaMs);

        var packets = fixture.Packets;
        Assert.NotNull(packets);

        var bgOutbound = packets!.Where(p => p.Direction == "Send").ToArray();
        Assert.True(bgOutbound.Length >= 2,
            $"BG high-drop baseline must record >=2 outbound packets; got {bgOutbound.Length}.");

        // Stream 2E.1 regression guard: no spurious SET_ACTIVE_MOVER even
        // for high-drop teleports.
        Assert.DoesNotContain(
            bgOutbound,
            p => p.OpcodeName == "CMSG_SET_ACTIVE_MOVER");

        // The Stream 2E.2 finding: NO FALL_LAND in the 2.5s window even
        // for a 100y drop. Pin this so any future change that emits
        // FALL_LAND on a long drop trips the assertion and forces the
        // test author to update Stream 2E.3 docs.
        Assert.DoesNotContain(
            bgOutbound,
            p => p.OpcodeName == "MSG_MOVE_FALL_LAND");

        var outboundAck = bgOutbound.FirstOrDefault(p => p.OpcodeName == "MSG_MOVE_TELEPORT_ACK");
        Assert.NotNull(outboundAck);
        Assert.Equal(16, outboundAck!.Size);
    }

    [Fact]
    [Trait("Category", "PacketFlowParity")]
    public void ForegroundCrossMapBaseline_PinsTransferPendingNewWorldShape()
    {
        // Stream 4 cross-map oracle: live FG capture of an Orgrimmar (Kalimdor)
        // -> Ironforge (Eastern Kingdoms) hop. Recorder fires on SMSG_TRANSFER_PENDING
        // (the early heads-up the server sends before tearing down the world
        // session); the captured window includes the destination
        // SMSG_COMPRESSED_UPDATE_OBJECT, SMSG_NEW_WORLD, and the immediate
        // CMSG_CANCEL_TRADE the client emits as part of its world-change
        // cleanup.
        //
        // Note: the post-load MSG_MOVE_WORLDPORT_ACK does NOT appear in the
        // 2.5s window because WoW.exe pauses packet processing during the
        // map load. To capture the WORLDPORT_ACK we'd either need a longer
        // recording window or a second recorder triggered on
        // SMSG_NEW_WORLD specifically. For now this baseline pins the
        // transfer-pending side of the cross-map sequence.
        var fixture = LoadForegroundCrossMapBaseline();

        Assert.Equal(1, fixture.SchemaVersion);
        Assert.Equal("post_teleport_packet_window", fixture.CaptureScenario);
        Assert.Contains("WoW.exe", fixture.Source);

        Assert.NotNull(fixture.Trigger);
        Assert.Equal("Recv", fixture.Trigger!.Direction);
        Assert.Equal("SMSG_TRANSFER_PENDING", fixture.Trigger.OpcodeName);
        Assert.Equal(0, fixture.Trigger.DeltaMs);

        var packets = fixture.Packets;
        Assert.NotNull(packets);
        Assert.True(packets!.Count > 0, "Captured FG cross-map window must contain at least the trigger.");

        // The captured window should include SMSG_NEW_WORLD as part of the
        // cross-map handshake (the destination map + landing position).
        Assert.Contains(packets, p => p.OpcodeName == "SMSG_NEW_WORLD" && p.Direction == "Recv");

        // The destination object update lands inside the same window.
        Assert.Contains(packets, p => p.OpcodeName == "SMSG_COMPRESSED_UPDATE_OBJECT" && p.Direction == "Recv");

        // Sanity check: the trigger payload encodes the destination map ID
        // (4 bytes after the opcode/size header in SMSG_TRANSFER_PENDING).
        // Org-to-Ironforge crosses Kalimdor (mapId=1) -> EK (mapId=0). The
        // captured payload starts with 3F00 (opcode+size prefix) followed
        // by 00000000 — the destination map ID 0 (EK) in little-endian.
        Assert.StartsWith("3F00", fixture.Trigger.PayloadHex);
        Assert.Contains("00000000", fixture.Trigger.PayloadHex);
    }

    private static PostTeleportWindowFixture LoadBaseline() => LoadFixture(BaselineFileName);

    private static PostTeleportWindowFixture LoadBackgroundBaseline() => LoadFixture(BackgroundBaselineFileName);

    private static PostTeleportWindowFixture LoadBackgroundHighDropBaseline() => LoadFixture(BackgroundHighDropBaselineFileName);

    private static PostTeleportWindowFixture LoadForegroundCrossMapBaseline() => LoadFixture(ForegroundCrossMapBaselineFileName);

    private static PostTeleportWindowFixture LoadFixture(string fileName)
    {
        var path = ResolveFixturePath(fileName);
        var json = File.ReadAllText(path);
        var fixture = JsonSerializer.Deserialize<PostTeleportWindowFixture>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(fixture);
        return fixture!;
    }

    private static string ResolveFixturePath(string fileName)
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
                fileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not resolve {fileName} starting from {AppContext.BaseDirectory}.");
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
