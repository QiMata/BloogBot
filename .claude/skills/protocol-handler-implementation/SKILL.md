---
name: protocol-handler-implementation
description: Add a CMSG/SMSG WoW protocol opcode handler in WoWSharpClient (with packet-capture tests), and—when the change crosses the IPC wire—regenerate the protobuf contract. Use when the headless bot must parse a new server message or send a new client message.
trigger: add a packet handler, new opcode, CMSG SMSG handler, parse a server packet, OpCodeDispatcher, WoWSharpClient protocol, protobuf contract change, regenerate proto
---

# Protocol Handler Implementation

## Goal

Teach the headless protocol client (`Exports/WoWSharpClient`) to handle one more
WoW wire message: define the opcode, parse/emit the packet, route it through the
dispatcher, and surface the result on the object manager / event emitter — proven
by packet-capture tests. When the new data must also travel between WWoW services
(StateManager ↔ BotRunner), update the protobuf IPC contract in the same change.

## Inputs

- The opcode name + numeric value and direction (SMSG = server→client to parse,
  CMSG = client→server to build), per the WoW 1.12.1/2.4.3/3.3.5a protocol.
- The packet layout (fields, order, types) and the game-state effect.
- Key files (canonical homes — open the concrete file at author time; some are
  scaffold stubs on the current branch):
  - Opcode enum: `Exports/GameData.Core/Enums/Opcode.cs`.
  - Handlers: `Exports/WoWSharpClient/Handlers/*Handler.cs` — static methods with
    signature `(Opcode opcode, byte[] data, HandlerContext ctx)`
    (`ChatHandler`, `MovementHandler`, `ObjectUpdateHandler`, `LoginHandler`,
    `SpellHandler`, `QuestHandler`, …) + `HandlerContext.cs`
    (gives `ObjectManager` + `EventEmitter`).
  - Dispatcher: `Exports/WoWSharpClient/OpCodeDispatcher.cs` (routes incoming
    packets to handlers); generic router
    `Exports/WoWSharpClient/Networking/Implementation/MessageRouter.cs`
    (`Register(opcode, handler)` / `RouteAsync`); optional
    `[PacketHandler(Opcode)]` attribute at
    `Exports/WoWSharpClient/Attributes/PacketHandlerAttribute.cs`.
  - Surfaces: `Exports/WoWSharpClient/WoWSharpObjectManager.cs`,
    `Exports/WoWSharpClient/WoWSharpEventEmitter.cs`,
    `Exports/WoWSharpClient/WoWSharpClient.cs`.
- IPC contract (only if the change crosses the wire):
  `Exports/BotCommLayer/Models/ProtoDef/*.proto` (`communication.proto` carries
  `ObjectiveMessage` / `WoWActivitySnapshot`), regenerated with
  `Exports/BotCommLayer/Models/ProtoDef/protocsharp.bat`.
- Reference: `docs/server-protocol/*` (1.12.1 protocol docs), area rules
  `.github/instructions/shared-libraries.instructions.md` and
  `.github/instructions/protobuf.instructions.md`.

## Preconditions

- You have read the relevant `docs/server-protocol/*` page and confirmed the
  packet layout for the target client version.
- A captured FG packet/event recording exists for the message (the monorepo
  contract requires BG parsing to be validated against FG recordings).
- The build is green: `dotnet build Exports/WoWSharpClient/WoWSharpClient.csproj`.

## Procedure

1. **Add the opcode** to `Exports/GameData.Core/Enums/Opcode.cs` with the correct
   numeric value (match the client version; do not guess).
2. **Parse or build the packet** in the matching handler under
   `Exports/WoWSharpClient/Handlers/` (e.g. add a method to `ObjectUpdateHandler`
   for an SMSG, or a builder for a CMSG). Read/write fields in exact wire order
   using the existing binary-reader/writer helpers; take `HandlerContext` for
   dependencies.
3. **Register** the handler so the opcode routes to it — wire it in
   `OpCodeDispatcher.cs` (or via `MessageRouter.Register(opcode, handler)`),
   following the existing registration for similar handlers.
4. **Surface the effect**: update `WoWSharpObjectManager` state and/or fire an
   event on `WoWSharpEventEmitter` so `BotRunner` Tasks can observe it.
5. **If the data crosses the IPC wire**: edit the relevant `*.proto`, run
   `protocsharp.bat`, and keep the regenerated `*.cs` in the **same commit** as the
   `.proto` change (AGENTS.md §11). Never hand-edit generated files.
6. **Add packet-capture tests** (see Verification).

## Verification

- Build: `dotnet build Exports/WoWSharpClient/WoWSharpClient.csproj`
  (and `BotCommLayer.csproj` if the proto changed — protoc errors surface here).
- Handler tests:
  `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj -v n`
  — handler tests touching shared object state use
  `[Collection("Sequential ObjectManager tests")]`.
- Network/socket behavior: `Tests/WowSharpClient.NetworkTests`.
- IPC-contract shape: `Tests/PathfindingService.Tests` / `Tests/BotRunner.Tests/IPC`
  exercise the protobuf contract by design.
- **Parity**: replay the captured FG recording and assert the BG parse matches the
  FG-observed result (`docs/server-protocol/*` + recorded fixtures).
- Layered: `.\run-tests.ps1 -Layer 3`.

## Outputs

- New `Opcode` value; handler parse/build method; dispatcher registration.
- Object-manager/event-emitter surface for the new data.
- (If applicable) updated `*.proto` + regenerated `*.cs` in one commit.
- Packet-capture/handler tests, validated against an FG recording.

## Failure modes and recovery

- **Wrong opcode value or client version.** Values differ across 1.12.1/2.4.3/
  3.3.5a; confirm against `docs/server-protocol/*`.
- **Field order/endianness mistakes.** The most common parse bug — diff against a
  real captured packet, not an assumption.
- **Hand-editing generated protobuf `*.cs`.** Always regenerate via
  `protocsharp.bat`; commit `.proto` + generated together.
- **Skipping FG-recording parity.** A BG parse that "looks right" but diverges from
  the FG client is a silent regression; validate against the recording.
- **Forgetting to surface the effect.** A parsed packet that never updates the
  object manager or fires an event is invisible to `BotRunner`.

## Related skills

- [[botrunner-task-implementation]] — consume the new event/state in a Task.
- [[live-validation-test-authoring]] — verify the end-to-end behavior.
- [[fg-bg-physics-parity]] — when the message affects movement/physics parity.
- [[failure-reason-mapping]] — classify a parse/transport failure.
- Reference: `docs/server-protocol/*`, AGENTS.md §11 (proto + generated code).
