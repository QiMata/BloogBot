# Architecture Diagrams

Editable [draw.io](https://www.drawio.com/) diagrams of the BloogBot / WWoW
architecture. Each diagram exists as a native `.drawio` file (source of truth)
and a `.drawio.png` export with the diagram XML embedded — so opening the PNG in
draw.io recovers the editable diagram.

Generated via the `drawio` skill (`.claude/skills/drawio/SKILL.md`). Diagram
content is sourced from `docs/ARCHITECTURE.md`, `docs/architecture/aota/`,
`docs/Spec/`, the `ProtoDef/*.proto` files, and the `Exports/`/`Services/`/`UI/`
project tree.

## Regenerating / exporting

Re-export a single diagram's PNG after editing its `.drawio`:

```powershell
& "C:\Program Files\draw.io\draw.io.exe" -x -f png -e -b 10 -o <name>.drawio.png <name>.drawio
```

Flags: `-x` export, `-f png` format, `-e` embed XML, `-b 10` 10px border.

## Diagram catalog

### System / Software Architecture
- [x] 1. `layered-dependency-graph` — strict GameData.Core→BotCommLayer→BotRunner→WoWSharpClient→Services→UI stack + forbidden-edge rules
- [x] 2. `component-service-map` — the worker services as boxes with ports + IPC links
- [x] 3. `c4-context` — system boundary vs MaNGOS, SOAP, MySQL, players, Shodan
- [x] 4. `c4-container` — injected WoW.exe vs headless BG runner vs service host vs WPF UI vs server
- [x] 5. `deployment-aspire` — .NET Aspire AppHost orchestration of services/containers
- [x] 6. `module-project-graph` — Exports/Services/BotProfiles/Tests/UI project graph
- [x] 7. `two-execution-modes` — Foreground (injection) vs Background (emulation) over shared BotRunnerService

### Behavioral / Flow
- [x] 8. `aota-hierarchy` — Activity→Objective→Task→Action four-layer model with wire-visibility
- [x] 9. `aota-worked-example-ubrs` — dungeon.ubrs decomposition down to atomic Actions
- [x] 10. `statemanager-fsm` — StateManagerWorker states + mode handlers
- [x] 11. `objective-dispatch-sequence` — StateManager→BotRunner ObjectiveMessage + snapshot/ACK
- [x] 12. `wow-login-sequence` — auth / realm-select / world-enter handshake
- [x] 13. `behavior-tree-task-stack` — LIFO IBotTask stack with GoToTask as universal child
- [x] 14. `decisionengine-composition-flow` — DecisionEngine composing Objectives from DB + catalog + snapshot
- [x] 15. `data-flow` — protobuf/TCP traffic, ActivitySnapshot deltas, packet streams
- [x] 16. `login-connection-flowchart` — connection/login decision flowchart

### Data / Structure
- [x] 17. `mangos-er-diagram` — MaNGOS DB tables the bot reads
- [x] 18. `key-interfaces-class-diagram` — IObjectManager/IWoWUnit/IBotTask/IActivity/IObjective inheritance + members
- [x] 19. `protobuf-message-schema` — ObjectiveMessage, WoWActivitySnapshot, CommandAckEvent + 5 generated groups
- [x] 20. `activity-catalog-dag` — Activity catalog + quest-chain DAG + item-requirement DAG

### Network / Infrastructure
- [x] 21. `network-topology` — ports, sockets, SOAP/MySQL endpoints
- [x] 22. `ipc-socket-communication` — ProtobufSocketServer/Client, length framing, retry/backoff
- [x] 23. `pathfinding-bake-pipeline` — MmapGen per-tile bake → navmesh + FG→BG physics-parity loop
- [x] 24. `build-graph` — .NET build + C++ MSBuild steps and dependencies

### UI / Design
- [x] 25. `wpf-dashboard-wireframe` — WoWStateManagerUI panel layout (MVVM)
- [x] 26. `ui-screen-flow` — dashboard panel/view navigation

### Project / Process
- [x] 27. `phase-roadmap` — docs/Plan phases with the S2.0 → Phase 12 dependency
- [x] 28. `test-account-responsibility` — Shodan (GM liaison/test director) vs dedicated test accounts

## Index

| # | Name | Source | PNG |
|---|------|--------|-----|
| 1 | layered-dependency-graph | [.drawio](layered-dependency-graph.drawio) | [.png](layered-dependency-graph.drawio.png) |
| 2 | component-service-map | [.drawio](component-service-map.drawio) | [.png](component-service-map.drawio.png) |
| 3 | c4-context | [.drawio](c4-context.drawio) | [.png](c4-context.drawio.png) |
| 4 | c4-container | [.drawio](c4-container.drawio) | [.png](c4-container.drawio.png) |
| 5 | deployment-aspire | [.drawio](deployment-aspire.drawio) | [.png](deployment-aspire.drawio.png) |
| 6 | module-project-graph | [.drawio](module-project-graph.drawio) | [.png](module-project-graph.drawio.png) |
| 7 | two-execution-modes | [.drawio](two-execution-modes.drawio) | [.png](two-execution-modes.drawio.png) |
| 8 | aota-hierarchy | [.drawio](aota-hierarchy.drawio) | [.png](aota-hierarchy.drawio.png) |
| 9 | aota-worked-example-ubrs | [.drawio](aota-worked-example-ubrs.drawio) | [.png](aota-worked-example-ubrs.drawio.png) |
| 10 | statemanager-fsm | [.drawio](statemanager-fsm.drawio) | [.png](statemanager-fsm.drawio.png) |
| 11 | objective-dispatch-sequence | [.drawio](objective-dispatch-sequence.drawio) | [.png](objective-dispatch-sequence.drawio.png) |
| 12 | wow-login-sequence | [.drawio](wow-login-sequence.drawio) | [.png](wow-login-sequence.drawio.png) |
| 13 | behavior-tree-task-stack | [.drawio](behavior-tree-task-stack.drawio) | [.png](behavior-tree-task-stack.drawio.png) |
| 14 | decisionengine-composition-flow | [.drawio](decisionengine-composition-flow.drawio) | [.png](decisionengine-composition-flow.drawio.png) |
| 15 | data-flow | [.drawio](data-flow.drawio) | [.png](data-flow.drawio.png) |
| 16 | login-connection-flowchart | [.drawio](login-connection-flowchart.drawio) | [.png](login-connection-flowchart.drawio.png) |
| 17 | mangos-er-diagram | [.drawio](mangos-er-diagram.drawio) | [.png](mangos-er-diagram.drawio.png) |
| 18 | key-interfaces-class-diagram | [.drawio](key-interfaces-class-diagram.drawio) | [.png](key-interfaces-class-diagram.drawio.png) |
| 19 | protobuf-message-schema | [.drawio](protobuf-message-schema.drawio) | [.png](protobuf-message-schema.drawio.png) |
| 20 | activity-catalog-dag | [.drawio](activity-catalog-dag.drawio) | [.png](activity-catalog-dag.drawio.png) |
| 21 | network-topology | [.drawio](network-topology.drawio) | [.png](network-topology.drawio.png) |
| 22 | ipc-socket-communication | [.drawio](ipc-socket-communication.drawio) | [.png](ipc-socket-communication.drawio.png) |
| 23 | pathfinding-bake-pipeline | [.drawio](pathfinding-bake-pipeline.drawio) | [.png](pathfinding-bake-pipeline.drawio.png) |
| 24 | build-graph | [.drawio](build-graph.drawio) | [.png](build-graph.drawio.png) |
| 25 | wpf-dashboard-wireframe | [.drawio](wpf-dashboard-wireframe.drawio) | [.png](wpf-dashboard-wireframe.drawio.png) |
| 26 | ui-screen-flow | [.drawio](ui-screen-flow.drawio) | [.png](ui-screen-flow.drawio.png) |
| 27 | phase-roadmap | [.drawio](phase-roadmap.drawio) | [.png](phase-roadmap.drawio.png) |
| 28 | test-account-responsibility | [.drawio](test-account-responsibility.drawio) | [.png](test-account-responsibility.drawio.png) |
