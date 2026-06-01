# API & Wire Contracts

> The interfaces between WWoW processes. Runtime traffic is **Protobuf over TCP
> with length framing** — there is no REST API for normal bot operation (the one
> HTTP surface is the optional on-demand API). Framing mechanics live in
> [`IPC_COMMUNICATION.md`](IPC_COMMUNICATION.md); this page is the index of
> *what* travels and *where the definitions are*.

## Service port map

WWoW microservice ports differ between in-process and Docker runs, so this page
does **not** duplicate the numbers — the resolved table is maintained in
[`local-development.md`](local-development.md) and
[`TECHNICAL_NOTES.md`](TECHNICAL_NOTES.md). The fixed external (MaNGOS) ports
are: MySQL `3306`, realmd `3724`, world `8085`, SOAP `7878`.

## Protobuf definitions (source of truth)

All IPC message shapes are defined in
`Exports/BotCommLayer/Models/ProtoDef/`:

| `.proto` | Carries |
|---|---|
| `communication.proto` | `ObjectiveMessage`, `ActivitySnapshot`, the `ObjectiveType` enum — the StateManager ↔ BotRunner contract |
| `game.proto` | `WoWObject`/`WoWUnit`/`WoWPlayer`, positions, items |
| `pathfinding.proto` | `PathfindingRequest`, physics in/out, line-of-sight (PathfindingService) |
| `scenedata.proto` | Scene/collision data (SceneDataService) |
| `database.proto` | MaNGOS DB table/row messages used by the decision engine |

**Regenerating C#:** run `protocsharp.bat` in that folder after any `.proto`
change, and commit the regenerated C# **with** the `.proto` change in the same
commit. Never hand-edit generated files. (`protocpp.bat` regenerates the C++
side for `Navigation`/`Physics`.) See [`../AGENTS.md`](../AGENTS.md) §11.

## Key contracts

- **`ObjectiveMessage`** — the *only* layer that crosses the wire in the
  Activity→Objective→Task→Action hierarchy. StateManager sends it; BotRunner
  decomposes it into Tasks. Conceptual model: [`data-model.md`](data-model.md),
  [`Spec/18_TERMINOLOGY.md`](Spec/18_TERMINOLOGY.md). Runtime contract:
  [`Spec/03_BOTRUNNER.md`](Spec/03_BOTRUNNER.md), [`Spec/19_AOTA_RUNTIME.md`](Spec/19_AOTA_RUNTIME.md).
- **`ActivitySnapshot`** — the bot→StateManager state feed. Carries **major
  state deltas, not full enemy/object payloads** (keep it lean). Telemetry
  contract: [`Spec/10_METRICS.md`](Spec/10_METRICS.md).
- **On-demand activity API** — how human players request activities through the
  GM liaison: [`Spec/23_ONDEMAND_API.md`](Spec/23_ONDEMAND_API.md).

## Transport implementation

The socket servers/clients live in `Exports/BotCommLayer`
(`ProtobufSocketServer.cs`, `ProtobufAsyncSocketServer.cs`). The wire WoW
protocol (client ↔ MaNGOS) is separate and lives in `Exports/WoWSharpClient`;
its opcode/auth/movement reference is [`server-protocol/`](server-protocol/) and
[`Spec/08_PROTOCOLS.md`](Spec/08_PROTOCOLS.md).
