# Spec 08 — Protocols

## Two protocols

1. **WoW 1.12.1 protocol** — between bots and the VMaNGOS server.
   115 CMSG opcodes sent, 141 SMSG opcodes handled. Reference:
   [`docs/server-protocol/`](../server-protocol/).
2. **WWoW IPC protocol** — between services, between StateManager and
   bots, between the UI and StateManager. Protobuf over TCP with
   4-byte little-endian length framing.

## WoW protocol (reference, not authored)

Detailed reference lives in [`docs/server-protocol/`](../server-protocol/):

- `auth-protocol.md` — SRP6, realm list.
- `world-login.md` — world server handshake, header encryption.
- `movement-protocol.md` — movement opcodes, flags, ACK pairing.
- `object-updates.md` — `SMSG_UPDATE_OBJECT` parsing.
- `opcodes-1.12.1.md` — opcode table.
- `transport-protocol.md` — boat/zeppelin/elevator state.
- `update-fields-1.12.1.md` — update-mask field IDs.

Implementation: `Exports/WoWSharpClient/` (handlers under `Handlers/`,
codec under `Client/`).

### Movement parity

Movement opcodes must be byte-identical between FG and BG. The parity
test suite (`Tests/WoWSharpClient.Tests/MovementParity/`) ships 30+
recorded sessions; new sessions are added when client behavior surprises
us.

The parity contract:

- BG sends the same opcode (`MSG_MOVE_*`) at the same flag state with
  the same payload as FG would.
- Timing tolerance: ± 100 ms for self-initiated movement, ± 10 ms for
  server-initiated movement (knockback, teleport, root).
- Server ACKs are paired and recorded; mismatched ACKs are bugs.

## IPC protocol — framing

All inter-service traffic uses:

```
| 4 bytes (uint32, LE) | <length> bytes of protobuf payload |
```

Length includes only the payload, not the prefix. Maximum payload is
`MaxFrameSize` (default 32 MiB). Larger payloads (full inventory dumps,
snapshot bulk fetch) are paginated.

Implementation: `Exports/BotCommLayer/ProtobufSocketServer.cs` and
`ProtobufSocketClient.cs`. The async variant is the default for new
services.

## IPC message catalog

All proto definitions live under `Exports/BotCommLayer/communication.proto`
(versioned alongside `Exports/BotCommLayer`).

### StateManager ↔ Bot

| Message | Direction | Purpose |
|---|---|---|
| `BotHeartbeat` | Bot → SM | Per-tick liveness, lifecycle state |
| `WoWActivitySnapshot` | Bot → SM | Per-tick state delta |
| `ObjectiveMessage` | SM → Bot | Action dispatch (75 action types) |
| `ActionAck` | Bot → SM | Accept/reject of action |
| `LoadoutSpecMessage` | SM → Bot | Loadout plan |
| `ActivityAssignment` | SM → Bot | Start/stop activity by descriptor |
| `ConfigChangedEvent` | SM → Bot | Hot-reload signal |
| `FullSnapshotRequest` | SM → Bot | Force a full (non-delta) snapshot |
| `InventoryFetchRequest` | SM → Bot | On-demand full inventory dump |

### StateManager ↔ UI

| Message | Direction | Purpose |
|---|---|---|
| `GetBotsSummary` / `BotsSummary` | UI ↔ SM | Roster overview |
| `GetActivitiesSummary` / `ActivitiesSummary` | UI ↔ SM | Active/queued |
| `GetLeasesSummary` / `LeasesSummary` | UI ↔ SM | Lease ledger |
| `GetErrorsSummary` / `ErrorsSummary` | UI ↔ SM | Top normalized errors |
| `GetServiceHealth` / `ServiceHealth` | UI ↔ SM | realmd/mangosd/SOAP/services |
| `GetActivityCatalog` / `ActivityCatalog` | UI ↔ SM | Catalog (compiled) |
| `GetActivityConfig` / `ActivityConfig` | UI ↔ SM | Single config row |
| `SaveActivityConfig` / `SaveAck` | UI ↔ SM | Atomic write + broadcast |
| `RequestActivity` / `RequestActivityResponse` | UI ↔ SM | On-demand request |
| `CancelActivityInstance` / `CancelAck` | UI ↔ SM | Cancel running |
| `GetMetricsCatalog` / `MetricsCatalog` | UI ↔ SM | Available metrics |

### PathfindingService

| Message | Direction | Purpose |
|---|---|---|
| `PathRequest` / `PathResponse` | Bot → PFS | Single A* request |
| `PathBatchEstimate` / `PathBatchEstimateResponse` | SM → PFS | Cheap ETA estimates for scheduler |
| `RoutePackInvalidate` | PFS → PFS | Dynamic overlay change |

### SceneDataService

| Message | Direction | Purpose |
|---|---|---|
| `SceneSliceRequest` / `SceneSliceResponse` | Bot → SDS | 3×3 grid slice |
| `GroundZRequest` / `GroundZResponse` | Bot → SDS | Walkable Z query |

## Forward-compatibility rules

1. **Never reuse a field number.** Removed fields stay reserved.
2. **New fields are optional with sensible defaults.**
3. **Enums have a `*_UNSPECIFIED = 0` value.** No enum reaches the wire
   as 0 unless explicitly set.
4. **Renaming a field is fine; renaming a message type requires both
   sides to update.** The on-wire format does not depend on names.
5. **Schema versioning** — major-bump only on a breaking change; minor-
   bump for additive changes. The version is in
   `Exports/BotCommLayer/SchemaVersion.cs` and emitted on every
   handshake.

## Length-prefix handshake

Both ends of a socket exchange a `ProtocolHandshake` as the first
framed message:

```protobuf
message ProtocolHandshake {
  string service_name = 1;       // "WoWStateManager"
  string git_sha = 2;             // build provenance
  uint32 schema_major = 3;
  uint32 schema_minor = 4;
  uint32 max_frame_size = 5;
}
```

Mismatched major → fatal error. Mismatched minor (downward) → log
warning, continue with reduced feature set.

## Socket lifecycle

- Connect → ProtocolHandshake → bidirectional messages.
- Clean EOF after a completed message → close silently (info log).
- Truncated mid-frame → log warning with bytes-received, close.
- Healthcheck disconnects (Docker `healthcheck` curl at ~30s cadence)
  → log debug; `ProtobufSocketServer.TryReadExact(allowCleanEndOfStream:true)`
  handles cleanly. **Do not** treat healthcheck disconnects as Warnings.

## Existing code anchors

| Concept | File |
|---|---|
| Async server | `Exports/BotCommLayer/ProtobufAsyncSocketServer.cs` |
| Async client | `Exports/BotCommLayer/ProtobufSocketClient.cs` |
| Sync server | `Exports/BotCommLayer/ProtobufSocketServer.cs` |
| Proto defs | `Exports/BotCommLayer/communication.proto` |
| Schema version | `Exports/BotCommLayer/SchemaVersion.cs` (to be added) |
| WoW protocol handlers | `Exports/WoWSharpClient/Handlers/` |
| WoW protocol docs | [`docs/server-protocol/`](../server-protocol/) |
