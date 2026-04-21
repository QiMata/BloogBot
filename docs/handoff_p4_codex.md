# P4 Codex handoff — Command ACK infrastructure

This doc is the handoff packet for picking up P4 from the Codex side. Everything
Codex needs to start from a cold open. For the full backlog + design invariants,
read `docs/TASKS.md` section **P4 - Command ACK Infrastructure**. P3 is shipped
and archived in `docs/TASKS_ARCHIVE.md` (Archived Snapshot 2026-04-21).

## Prompt (paste to Codex)

> You are picking up the **P4 Command ACK Infrastructure** phase of the
> Westworld of Warcraft / BloogBot repo at `E:\repos\Westworld of Warcraft`.
> P3 (Unified Loadout Hand-off) is complete and archived. Your job is
> **P4.1 + P4.2** this session — full spec in `docs/TASKS.md`. Do not start
> P4.3, P4.4, or P4.5 yet. Keep commits small, one per logical unit.
>
> ### What P4.1 and P4.2 are
>
> **P4.1** closes a BG (headless bot) event-parity gap. Today the BG path
> decodes `SMSG_LEARNED_SPELL`, `SMSG_REMOVED_SPELL`, `SMSG_SPELL_FAILURE`,
> `SMSG_INVENTORY_CHANGE_FAILURE`, and never registers `SMSG_NOTIFICATION` —
> they silently mutate `ObjectManager` state and no `IWoWEventHandler`
> event fires. FG sees the same information via Lua hooks; BG sees
> nothing. P4.1 adds `OnLearnedSpell`, `OnUnlearnedSpell`,
> `OnSkillUpdated`, `OnItemAddedToBag` events and routes the existing
> attack/inventory/spell failures through `FireOnErrorMessage`. See
> `docs/TASKS.md` P4.1.1 through P4.1.6 for the exact sub-tasks.
>
> **P4.2** fixes a snapshot signature churn bug. Today
> `SnapshotChangeSignature` in `Exports/BotRunner/BotRunnerService.cs`
> lines ~111–125 includes `RecentChatCount` and `RecentErrorCount`. Every
> new chat or error message changes the count, which flips the signature,
> which forces a full snapshot send — defeating the 2s heartbeat throttle.
> Under loadout dispatch or a BG fight we send full snapshots every tick
> for no good reason. Remove both fields from the signature. Messages
> still flush into the snapshot every tick but they ride along on real
> state changes or heartbeats instead of forcing a full send themselves.
> See `docs/TASKS.md` P4.2.1 through P4.2.3.
>
> ### Where to read first
>
> 1. `docs/TASKS.md` section **P4 - Command ACK Infrastructure** — rules,
>    sub-phases, design invariants.
> 2. `docs/TASKS_ARCHIVE.md` **Archived Snapshot (2026-04-21)** — what P3
>    shipped and how LoadoutTask / BattlegroundCoordinator already use
>    `LoadoutStatus` as a structured ACK. P4 builds on that pattern.
> 3. `CLAUDE.md` — build/test rules for this repo. Read carefully.
>
> ### Files you will almost certainly touch
>
> **P4.1:**
> - `Exports/GameData.Core/Interfaces/IWoWEventHandler.cs` — add new events.
> - `Exports/WoWSharpClient/WoWSharpEventEmitter.cs` — add the firing
>   methods (`FireOnLearnedSpell`, `FireOnSkillUpdated`, `FireOnItemAddedToBag`,
>   etc.).
> - `Exports/WoWSharpClient/Handlers/SpellHandler.cs` — fire
>   `OnLearnedSpell` from `HandleLearnedSpell`, `OnUnlearnedSpell` from
>   `HandleRemovedSpell`, and route `HandleSpellFailure` through
>   `FireOnErrorMessage`.
> - `Exports/WoWSharpClient/Client/WorldClient.cs` — `SMSG_ATTACKSWING_*`
>   at ~lines 234–251 and `SMSG_INVENTORY_CHANGE_FAILURE` at line ~241
>   currently go to `_attackErrors` Rx subjects / diagnostic logging only;
>   ALSO call `FireOnErrorMessage` so the standard capture path sees them.
>   Also register a handler for `SMSG_NOTIFICATION` (0x1CB) → `FireOnSystemMessage`.
> - `Exports/WoWSharpClient/Networking/ClientComponents/LootingNetworkClientComponent.cs` —
>   the existing `OnItemPushResultReceived` publishes to `_itemLoot`
>   observable; ALSO raise `OnItemAddedToBag` via the event handler so it
>   lands in the standard message capture path.
> - Find the `SMSG_UPDATE_OBJECT` path that writes `IWoWLocalPlayer.SkillInfo`
>   (grep for `SkillInt1` / `SkillInt2` in `WoWSharpObjectManager.Objects.cs`
>   — around line 1353 is where it's written). Fire `OnSkillUpdated` from
>   there when the value actually changes.
> - `Exports/BotRunner/BotRunnerService.Messages.cs` — subscribe to the new
>   events and enqueue with the prefixes noted in TASKS.md P4.1.
>   Existing prefixes: `[CHAT:*]`, `[ERROR]`, `[UI]`, `[SYSTEM]`, `[SKILL]`.
>   Use `[SKILL]` for spell-learn / skill-update, `[UI]` for item-added.
>
> **FG parity side** (so both runners fire the same events):
> - `Services/ForegroundBotRunner/Statics/WoWEventHandler.cs` already
>   handles `LEARNED_SPELL` / `UNLEARNED_SPELL` Lua events at lines
>   ~200–201. Add the `OnLearnedSpell(spellId)` / `OnUnlearnedSpell(spellId)`
>   emission there too. For `OnSkillUpdated` and `OnItemAddedToBag`, FG
>   can derive from `CHAT_MSG_SKILL` and inventory-change Lua events.
>
> **P4.2:**
> - `Exports/BotRunner/BotRunnerService.cs` — the
>   `SnapshotChangeSignature` record is around lines 100–125 and
>   `ComputeSnapshotSignature` is around line 427. Remove the two fields
>   (`RecentChatCount`, `RecentErrorCount`) and the corresponding assignments.
>
> ### Tests
>
> **Unit tests you must add:**
> - `Tests/WoWSharpClient.Tests` — one test per new event in P4.1: feed a
>   synthetic SMSG packet through the handler, assert the event fires once
>   with correct args. Follow the pattern already in
>   `Tests/WoWSharpClient.Tests/Handlers/` if one exists; otherwise mirror
>   an existing handler test.
> - `Tests/BotRunner.Tests` — one test that verifies the new events land
>   in `snapshot.RecentChatMessages` / `snapshot.RecentErrors` with the
>   right prefix (e.g., `[SKILL] Learned spell 12345`). Use the pattern
>   in `Tests/BotRunner.Tests/BotRunnerServiceSnapshotTests.cs` if it
>   exists, or `LoadoutTaskExecutorTests.cs` for the Moq-based harness.
> - **P4.2 regression test:** construct a BotRunnerService, tick it once,
>   grab its `_lastSentSignature`, enqueue N chat messages via
>   `EnqueueDiagnosticMessage`, tick again, and assert the signature did
>   not change. The full snapshot should only be sent on the 2s heartbeat
>   interval, not from message churn. Use reflection for private fields
>   (`BotRunnerServiceSnapshotTests.cs` has the pattern).
>
> **Test-run commands (Windows PowerShell, from the repo root):**
> ```powershell
> powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly
> dotnet build WestworldOfWarcraft.sln --configuration Release
> dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj `
>   --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false `
>   --filter "FullyQualifiedName~SpellHandler|FullyQualifiedName~ChatHandler|FullyQualifiedName~Events" `
>   --logger "console;verbosity=minimal"
> dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj `
>   --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false `
>   --filter "FullyQualifiedName~BotRunnerServiceSnapshot|FullyQualifiedName~LoadoutTask|FullyQualifiedName~BotRunnerServiceMessage" `
>   --logger "console;verbosity=minimal"
> ```
>
> After your changes, the combined filter above must stay green. You do
> not need to run live BG tests for P4.1/P4.2 — those are deterministic
> unit changes.
>
> ### Repository rules (non-negotiable)
>
> 1. **Kill `WoW.exe` before every build** — DLL injection locks output
>    files. `tasklist //FI "IMAGENAME eq WoW.exe" //FO LIST`;
>    `taskkill //F //PID <pid>` for each. Or use
>    `run-tests.ps1 -CleanupRepoScopedOnly`.
> 2. **Never blanket-kill dotnet or `Game.exe`.** Only kill PIDs your
>    session launched. Other repos may be running concurrent tests on
>    the same machine.
> 3. **One continuous session, never start a new one.** Auto-compaction
>    handles context limits. If you hit a wall, commit what you have
>    and keep going.
> 4. **Commit + push after every logical unit.** Small commits with the
>    trailer `Co-Authored-By: <your identity> <no-reply email>`.
>    Push immediately after commit. Do not accumulate uncommitted changes
>    across tasks.
> 5. **MaNGOS is always live via Docker.** Verify with `docker ps`, not
>    `tasklist`. Server is expected to be reachable on 127.0.0.1.
> 6. **No background agents / `run_in_background: true`.** Foreground
>    only. Background runs go stale.
> 7. **No `.learn all_myclass` / `.learn all_myspells` anywhere.** See
>    `memory/feedback_explicit_spell_learning.md`. Explicit numeric IDs
>    always.
> 8. **Proto regen:** if you touch `Exports/BotCommLayer/Models/ProtoDef/communication.proto`
>    (you probably won't in P4.1/P4.2), regen with:
>    ```bash
>    PROTOC=/c/Users/lrhod/.nuget/packages/grpc.tools/2.68.1/tools/windows_x64/protoc.exe
>    "$PROTOC" --csharp_out="E:/repos/Westworld of Warcraft/Exports/BotCommLayer/Models" \
>      -I"E:/repos/Westworld of Warcraft/Exports/BotCommLayer/Models/ProtoDef" \
>      "E:/repos/Westworld of Warcraft/Exports/BotCommLayer/Models/ProtoDef/communication.proto"
>    ```
>
> ### Commit strategy for this session
>
> Land these as separate commits in order:
>
> 1. `feat(comm): P4.1 add OnLearnedSpell/OnUnlearnedSpell events (FG+BG)`
>    — new events on `IWoWEventHandler`, BG firing from `SpellHandler`,
>    FG firing from `WoWEventHandler.EvaluateEvent`, `BotRunnerService.Messages`
>    subscription, unit tests.
> 2. `feat(comm): P4.1 add OnSkillUpdated event (FG+BG)`.
> 3. `feat(comm): P4.1 add OnItemAddedToBag event (FG+BG)`.
> 4. `feat(comm): P4.1 route attack/inventory/spell failures through OnErrorMessage`.
> 5. `feat(comm): P4.1 register SMSG_NOTIFICATION → OnSystemMessage`.
> 6. `fix(botrunner): P4.2 drop RecentChat/ErrorCount from snapshot signature`
>    — the churn fix + regression test. Separate from P4.1 so it's easy
>    to revert if something misbehaves.
>
> Each commit's body: 1-2 sentences explaining *why* (the user-visible
> effect), not just what the diff does. Mirror the tone of the recent
> P3 commits (e.g., commit `813da3ef`).
>
> ### Scope guardrails
>
> - **Don't touch `LoadoutTask.cs` in this session.** Event-driven
>   advancement is P4.3, which is the next session. P4.1+P4.2 only add
>   the plumbing; LoadoutTask keeps its current polling behavior
>   unchanged.
> - **Don't change the proto in this session.** Correlation ids and
>   `CommandAckEvent` are P4.4.
> - **Don't refactor existing event firings beyond the listed files.**
>   If you find an event that should also fire and isn't on the task
>   list, add a note to TASKS.md under P4.1 as a new sub-task rather
>   than expanding scope.
> - **Don't delete `ContainsCommandRejection` / `GetDeltaMessages`** —
>   those stay until P4.5 migration.
>
> ### Report format at end of session
>
> When you're done (or hit a wall), write a handoff block to the end of
> `docs/TASKS.md` under a `## Handoff (YYYY-MM-DD)` heading with:
> - What shipped (commits by hash + one-line summary each).
> - Test commands you ran + their results.
> - Any sub-task you couldn't close and why.
> - The next concrete command for the next session.
>
> Mirror the format of the existing `## Handoff (2026-04-20)` and
> `## Handoff (2026-04-19)` entries at the bottom of `docs/TASKS.md`.
>
> Start by running `git status` + `git log --oneline -5` + reading
> `docs/TASKS.md` P4 + reading `CLAUDE.md`. Then make a plan.
