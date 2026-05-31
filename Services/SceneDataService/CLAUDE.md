# SceneDataService — World Scene Data

Serves game-world tile and object snapshots to other services over a TCP socket.
A consumer of navigation/scene data, exposed via the protobuf IPC layer.

## Role

- Provides scene tile + object data (see `scenedata.proto` →
  `Exports/BotCommLayer/Models/Scenedata.cs`).
- Communicates over protobuf/TCP, length-framed like the rest of the IPC stack.

## Dependencies

- `Exports/BotCommLayer` (IPC + generated `Scenedata` models),
  `Exports/GameData.Core` (interfaces).

## Build / test

`.\scripts\build.ps1`; movement/scene-data contract tests live under
`Tests/WoWSharpClient.Tests/Movement/SceneData*`.

## Special rules

- The `Scenedata.cs` model is generated — edit `scenedata.proto` + regenerate
  (see `.github/instructions/protobuf.instructions.md`).

> Path-specific agent rules: `.github/instructions/services.instructions.md`.
