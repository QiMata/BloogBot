# Architecture (Practical Orientation)

> **Audience:** coding agents and new contributors who need to know *where code
> lives, how the major flows work, and where to make a change* — fast.
> This is a hub. It links to the authoritative detail rather than restating it.
> For the formal architecture contract see [`Spec/01_ARCHITECTURE.md`](Spec/01_ARCHITECTURE.md);
> for the full autonomous-work entry point see [`SPEC.md`](SPEC.md).
>
> **Visual diagrams:** editable draw.io renderings of this architecture (layered
> dependency graph, component/service map, C4 context/container, the
> Activity→Objective→Task→Action hierarchy, sequence diagrams, IPC contract, and
> more) live in [`architecture/diagrams/`](architecture/diagrams/README.md).

## What WWoW is

Westworld of Warcraft (WWoW, legacy name *BloogBot*) automates characters on a
**local** private World of Warcraft server (MaNGOS/VMaNGOS), driving a "living
server" of bots that perform real activities — questing, dungeons, gathering,
PvP, social. It runs entirely on local services; there is no cloud dependency
(see [`local-development.md`](local-development.md)).

Supported clients: Vanilla `1.12.1`, TBC `2.4.3`, WotLK `3.3.5a`.

## Two execution modes

A bot is driven by one of two interchangeable runtimes that share the same core
behavior engine (`Exports/BotRunner`) and the same game interfaces
(`Exports/GameData.Core`):

| Mode | Project | How it talks to the game | Use when |
|---|---|---|---|
| **Foreground** | `Services/ForegroundBotRunner` | DLL-injected into a real `WoW.exe`; direct process memory read/write + Lua | You need true client parity (rendering, exact physics, packet recording for parity baselines) |
| **Background** | `Services/BackgroundBotRunner` | Headless; pure-C# protocol emulation via `Exports/WoWSharpClient` (no game client) | You need many bots cheaply, or CI without a GUI client |

Background behavior is validated against Foreground packet/event recordings when
parity matters (see `RecordedTests.*` and [`testing.md`](testing.md)).

## Major folders & projects

| Path | What it is |
|---|---|
| `Exports/GameData.Core` | Game interfaces & shared contracts (`IObjectManager`, `IWoWUnit`, enums). **Zero dependencies** — the bottom of the stack. |
| `Exports/BotCommLayer` | Protobuf-over-TCP IPC (length-framed). `.proto` sources + generated C#. |
| `Exports/BotRunner` | Core orchestration: the behavior engine, Task stack, Objective decomposition. Shared by both runtimes. |
| `Exports/WoWSharpClient` | Pure-C# WoW protocol: packets, opcodes, auth, movement. |
| `Exports/WinImports` | Windows API P/Invoke wrappers (process/memory). |
| `Exports/Navigation` *(C++)* | Detour/Recast A* pathfinding + the physics engine. |
| `Exports/Loader`, `Exports/FastCall`, `Exports/Physics` *(C++)* | CLR-injection bootstrap, SEH-wrapped fast calls, physics companion. |
| `Services/WoWStateManager` | Central orchestrator: bot lifecycle, FG injection, IPC listeners. Hosts DecisionEngine + PromptHandling in-process. |
| `Services/PathfindingService` | A* route worker over `Navigation.dll` (Protobuf TCP). |
| `Services/SceneDataService` | Collision/scene data provider (fallback for background bots). |
| `Services/DecisionEngineService` | Picks the next Activity/Objective from game-DB knowledge. |
| `Services/PromptHandlingService[.Api]` | Dialog/quest-text automation. |
| `BotProfiles` | Per class/spec combat rotation profiles (one project, profile subdirectories). |
| `UI/WoWStateManagerUI`, `UI/StorylineManager` | WPF desktop UIs. |
| `UI/Systems/Systems.AppHost` | .NET Aspire orchestration of the Docker stack + services. |
| `Tests/*` | xUnit test suites — see [`testing.md`](testing.md). |
| `tools/*` | CLI utilities (navmesh generation in `tools/MmapGen`, visualizers, audits). |
| `docs/*` | This documentation set; canonical contracts in `docs/Spec/`. |

A per-directory breakdown lives in [`PROJECT_STRUCTURE.md`](PROJECT_STRUCTURE.md).

## Dependency direction (strict — do not violate)

```
GameData.Core → BotCommLayer → BotRunner → WoWSharpClient → Services → UI
```

Interfaces/contracts live in the **lower** layers; implementations live in the
**higher** layers. Never add an upward reference (e.g. an `Exports/` project
depending on `Services/` or `UI/`). If you must change a cross-layer contract,
update every consumer and its tests in the same change. The enforced rule and
its rationale are in [`../AGENTS.md`](../AGENTS.md) §3 and
[`Spec/01_ARCHITECTURE.md`](Spec/01_ARCHITECTURE.md).

## Runtime flow

**1. Orchestration & the foreground injection pipeline.**
`WoWStateManager` owns bot lifecycle and exposes Protobuf/TCP listeners. To
start a foreground bot it launches/locates `WoW.exe`, injects `Loader.dll`
(C++), which bootstraps the .NET 8 CLR via `hostfxr` and loads
`ForegroundBotRunner`, which connects back to StateManager. Background bots
skip all of this and connect over the WoW protocol via `WoWSharpClient`.

**2. The behavior loop.** Work flows through a strict four-layer hierarchy
(read [`Spec/18_TERMINOLOGY.md`](Spec/18_TERMINOLOGY.md) before using these
words as synonyms — and see [`data-model.md`](data-model.md)):

```
Activity  →  Objective  →  Task  →  Action
(event)      (wire msg)     (IBotTask)  (atomic primitive)
```

Only the **Objective** crosses the wire (as `ObjectiveMessage`). `BotRunner`
decomposes it into **Tasks** (behavior-tree nodes on a LIFO stack) which
compose atomic **Actions** (one memory read, one bit write, one opcode, one key
press) per tick. The deep treatment is in
[`architecture/aota/`](architecture/aota/README.md).

**3. Inter-service IPC.** Runtime traffic is Protobuf/TCP with length framing.
Bots call `PathfindingService` for routes and `SceneDataService` for collision
data; StateManager publishes `ActivitySnapshot` deltas. Framing details:
[`IPC_COMMUNICATION.md`](IPC_COMMUNICATION.md); wire contracts and ports:
[`api-contracts.md`](api-contracts.md).

## Where to make common changes

| You want to… | Go to | Then |
|---|---|---|
| Fix/add a class combat rotation | `BotProfiles/<ClassSpec>/` | See the `bot-profile` skill; no contract change |
| Add a behavior (Task) | `Exports/BotRunner/` | Compose existing Actions; `GoToTask` is the universal child |
| Add/parse a packet or opcode | `Exports/WoWSharpClient/` (`Client/`, `Networking/`) | Add background↔foreground parity tests |
| Add/change an IPC message | `Exports/BotCommLayer/Models/ProtoDef/*.proto` | Regenerate (below) and update both ends in one commit |
| Change pathfinding/physics | **Read [`physics/README.md`](physics/README.md) first** — stack is frozen | Mesh fixes go in `tools/MmapGen/`, not new managed repair |
| Add a new activity family | `docs/Plan/Activities/` then `Exports/BotRunner` | Follow the slot in [`Plan/`](Plan/) |
| Add a test | see [`testing.md`](testing.md) | Mirror the source project name |

## Generated code

- **Protobuf C#** is generated from the `.proto` files in
  `Exports/BotCommLayer/Models/ProtoDef/` (`database`, `communication`, `game`,
  `pathfinding`, `scenedata`) by `protocsharp.bat`. Keep the `.proto` change and
  the regenerated C# in the **same commit**; never hand-edit generated files.
- **Navmesh data** (`.mmap`/`.mmtile`) is produced by the C++ generator in
  `tools/MmapGen/` (CMake), *not* by managed code.

## External systems

| System | Where it runs | Notes |
|---|---|---|
| MaNGOS world server | local Docker / native | World `8085`, realmd (auth) `3724` |
| MaNGOS MySQL/MariaDB | local | `3306` — **read-mostly**; all mutations go through SOAP |
| MaNGOS SOAP API | local | `7878` — GM commands; the only sanctioned write path (see [`security.md`](security.md)) |
| `WoW.exe` game client | local Windows | Foreground injection target |
| Ollama (optional) | local | LLM for decision/prompt services |
| PostgreSQL `bloogbot_memory`, SQLite `storyline_runtime` | local | App-owned stores — see [`data-model.md`](data-model.md) |

The authoritative, resolved port map (in-proc vs Docker) is in
[`local-development.md`](local-development.md) and
[`TECHNICAL_NOTES.md`](TECHNICAL_NOTES.md).

## Deeper reading

- [`Spec/01_ARCHITECTURE.md`](Spec/01_ARCHITECTURE.md) — formal component contract
- [`architecture/aota/README.md`](architecture/aota/README.md) — Activity/Objective/Task/Action deep-dive
- [`Spec/02_STATEMANAGER.md`](Spec/02_STATEMANAGER.md), [`Spec/03_BOTRUNNER.md`](Spec/03_BOTRUNNER.md)
- [`local-development.md`](local-development.md) · [`testing.md`](testing.md) · [`troubleshooting.md`](troubleshooting.md) · [`security.md`](security.md)
