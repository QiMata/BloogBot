# WWoW Autonomous — Live-Integration Runbook (the verification harness)

> **What this is.** The operational runbook for running the WWoW live
> integration suite **headless** against an already-up VMaNGOS docker
> stack, so an autonomous agent (or a human) can treat an
> `Accept: LIVE:<Class>.<Method>` gate in
> [`CHURN_TRACKER.md`](CHURN_TRACKER.md) as a real, reproducible signal.
>
> Pairs with the wrapper script
> [`../../../tools/run-live.ps1`](../../../tools/run-live.ps1) (run from
> the repo root). This is tracker rows **A.1 + A.2**.
>
> **Status (2026-05-28, layout session):** `tools/run-live.ps1` is a
> **layout-session skeleton** — it does the docker preflight, sets the
> headless env gates, builds, runs a single filter, and retries on
> setup-timeout. The two env gates it sets (`WWOW_SKIP_SERVER_RESTART`,
> `WWOW_DISABLE_UI`) are **not yet consumed by the fixture** — that is
> tracker row **A.1**. Until A.1 lands, the script still runs (the unset
> gates are harmless), but the live suite may do its normal per-settings
> StateManager reload. A.2 finalizes the script once A.1 wires the gates.
>
> **Last updated:** 2026-05-28.

---

## 0. TL;DR

```powershell
# from E:\repos\Westworld of Warcraft
pwsh tools/run-live.ps1 -Filter <TestClassOrMethod>
```

That command: confirms the 5 VMaNGOS containers are `Up`, sets the
headless env gates, points temp/results at repo-local `tmp/`, builds
once, runs the named `Category=Integration` filter, retries flaky boots,
and prints where the artifacts landed. Exit `0` = green.

---

## 1. Preconditions (the operator sets these up once)

1. **The VMaNGOS docker stack is up + healthy.** The harness does **not**
   manage docker — `run-live.ps1` only *checks* it and aborts (exit `2`)
   if it isn't ready. Verify:
   ```powershell
   docker ps --filter name=wow- --filter name=maria --filter name=wwow- --format "{{.Names}}: {{.Status}}"
   ```
   You want five lines, each `Up`:
   `wow-mangosd`, `wow-realmd`, `maria-db`, `wwow-pathfinding` (port
   9002), `wwow-scene-data` (port 9003).

2. **The WoW 1.12.1 client + injection tooling exist** (paths via env
   vars — R14):
   - `WoW.exe` — `GameClient:ExecutablePath` in
     `Services/WoWStateManager/appsettings.json` and/or
     `$env:WWOW_TEST_WOW_PATH` (default probe `D:\World of Warcraft\WoW.exe`).
   - The built native DLLs: `Loader.dll` (x86), `Navigation.dll` (x64),
     `FastCall.dll` (x86) — see `CLAUDE.md` build commands.
   - **Kill any running `WoW.exe` before building** — the FG injector
     locks the DLLs in the build output (`CLAUDE.md`, MSB3027).

3. **Repo on `main`, clean-ish tree.** The working tree carries
   untracked logs + `*_wpftmp.csproj` + `tmp/test-runtime/` artifacts —
   **never `git add -A`**; stage specific files (R15).

---

## 2. The headless env gates (why headless works) — tracker A.1

| Env var | Value | Effect (target — wired by A.1) |
|---|---|---|
| `WWOW_SKIP_SERVER_RESTART` | `1` | The fixture (`Tests/Tests.Infrastructure/BotServiceFixture.cs`) skips the StateManager/MaNGOS restart-on-settings-change and runs against the already-up stack. Today the fixture does a lazy `EnsureSettingsAsync` reload (`Services/WoWStateManager/CLAUDE.md`); A.1 makes the skip explicit so a long-up stack is never bounced. |
| `WWOW_DISABLE_UI` | `1` | Suppresses spawning `UI/WoWStateManagerUI` during a run so it is truly headless (Phase 3 makes the UI the default host; this gate keeps the loop headless). |

`run-live.ps1` sets both by default. It also points
`TEMP`/`TMP`/`DOTNET_CLI_HOME`/`VSTEST_RESULTS_DIRECTORY`/`WWOW_REPO_ROOT`/
`WWOW_TEST_RUNTIME_ROOT` at repo-local `tmp/` dirs (matching
`run-tests.ps1` lines 23-46) so a live run never pollutes the user
profile.

> The full set of `WWOW_TEST_*` connection env vars
> (`WWOW_TEST_WOW_PATH`, `WWOW_TEST_AUTH_IP/PORT`, `WWOW_TEST_WORLD_PORT`,
> `WWOW_TEST_PATHFINDING_IP/PORT`, `WWOW_TEST_MYSQL_*`, `WWOW_TEST_SOAP_*`,
> `WWOW_TEST_USERNAME/PASSWORD`) is read by
> `Tests/Tests.Infrastructure/IntegrationTestConfig.cs` and defaults to
> the local stack — you only override them if your ports differ.

---

## 3. Running a live test

### 3.1 Via the wrapper (preferred)

```powershell
pwsh tools/run-live.ps1 -Filter <TestClassOrMethod> [-NoBuild] [-WithUi] [-RestartServers] [-SkipDockerCheck] [-MaxAttempts 3]
```

- `-Filter` is matched as `FullyQualifiedName~<Filter>`; pass a class
  name or a single method.
- `-NoBuild` reuses the last build (faster when you didn't touch code).
- The script builds once, runs, restores env, and prints the artifact
  summary. Its exit code is `dotnet test`'s exit code (or `2` if the
  docker preflight fails).

### 3.2 Raw (what the wrapper runs under the hood)

```powershell
$env:WWOW_SKIP_SERVER_RESTART = "1"
$env:WWOW_DISABLE_UI = "1"
dotnet build WestworldOfWarcraft.sln -c Debug
dotnet test Tests/BotRunner.Tests --no-build `
  --filter "FullyQualifiedName~<TestClassOrMethod>" `
  --blame-hang --blame-hang-timeout 3m
```

A live FG test: StateManager auto-launches `WoW.exe`
(`StartForegroundBotRunner` → `CreateProcess` → `VirtualAllocEx` +
`CreateRemoteThread(LoadLibraryW)` → injects `Loader.dll` → bootstraps
.NET 8 → `ForegroundBotRunner.dll` connects back on port 9001), drives
the scenario, and the test polls StateManager snapshots (port 9000) with
tight timeouts + crash checks. A live BG test runs a headless
`WoWSharpClient` — no `WoW.exe`.

---

## 4. Artifacts — what to read to verify reality (R16)

Live-test artifacts land under repo-local `tmp/test-runtime/` (set by the
env above):

- **`tmp/test-runtime/screenshots/<TestClass.TestMethod>/...png`** — the
  FG capture (the *real* frame). For ANY Task/Action diagnosis, READ this
  PNG (the Read tool ingests PNGs). If the log says X but the screenshot
  shows Y, **trust the screenshot** — the bug is in state detection, not
  the action.
- **`tmp/test-runtime/screenshots/.../timeline/<scenario>/<phase>.json`**
  — the StateManager-side snapshot at assertion points.
- **`tmp/test-runtime/results/`** — the VSTest results (`VSTEST_RESULTS_DIRECTORY`).
- **`tmp/test-runtime/traces/<TestClass.TestMethod>/*.jsonl`** — the
  training trace (`Activities/00_INDEX.md §Training-trace contract`); the
  dynamic-progressive invariant (`roster_distance_delta ≤ 0` on
  completion) is asserted from these.
- **`TestResults/ServiceLogs/<TestName>/`** — per-test merged service logs.

---

## 5. Named live tests (the harness inventory)

The integration tests live in `Tests/BotRunner.Tests/LiveValidation/`
(~105 files; base fixture `LiveBotFixture.cs` + partials,
`CoordinatorFixtureBase.cs` for multi-bot). The canonical Activity → test
mapping + each Activity's status lives in
[`../Activities/00_INDEX.md`](../Activities/00_INDEX.md); tracker row
**A.4** re-validates every `done`/`partial` claim against a real run.

To enumerate every live test:
```powershell
dotnet test Tests/BotRunner.Tests --list-tests
```
To run just the live layer the existing way (whole layer, no single
filter):
```powershell
pwsh run-tests.ps1 -Layer 4
```

The load-bearing harness anchor is tracker row **A.3** (the
single-bot "login → in-world → writes artifacts" smoke test). Establish
or identify it first; it is what `run-live.ps1` is validated against.

---

## 6. MaNGOS server ops (test setup) — SOAP only

**NEVER edit the MaNGOS MySQL DB directly.** All character/server
mutation goes through SOAP (`http://127.0.0.1:7878/`,
`ADMINISTRATOR:PASSWORD`) via the fixture helpers
(`LiveBotFixture.ExecuteGMCommandAsync` /
`SendGmChatCommandAsync`):

- Clean setup: `.reset items` / `.reset level` / `.reset spells` /
  `.reset talents` / `.reset all`.
- Positioning: `.tele name <char> <loc>` / `.go xyz <x> <y> <z> <map>`.
- Read-only MySQL is acceptable for connectivity + non-mutating lookups
  (faction/item ids from `mangos.faction_template` / `mangos.item_template`).
- **Shodan** is the production GM Liaison / test director — director
  only, never a behavior-test subject. Dispatch behavior tests to the
  dedicated test accounts and assert on *their* snapshots.

---

## 7. Flake policy (the verification-trust rule)

A `LIVE:` Accept gate is satisfied **only** when the test passes in a run
executed *this iteration*. A stale "it passed before" is treated as red
— the `00_INDEX.md` `done` claims (gathering, dungeon task-family)
predate a reliable headless harness and MUST be re-verified (`A.4`). For
multi-bot / timing-sensitive tests (group formation, dungeons, BGs),
"passed 1 of 3" is **not** done — treat the flakiness as a bug to
root-cause (`B.group` organic formation), not noise to retry past.

---

## 8. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Script exits `2` before any test | A VMaNGOS container is missing or not `Up` | Bring the stack up; re-run the `docker ps` check. The script never starts docker. |
| Build fails with MSB3027 (DLL locked) | A `WoW.exe` from a prior FG run is still holding the injected DLLs | Kill the specific `WoW.exe` PID(s) (NOT a blanket kill — other repos run concurrently); `pwsh run-tests.ps1 -CleanupRepoScopedOnly` cleans repo-scoped processes. |
| Pathfinding test reports `no_route` at an OG sea-level coord | Test-runner data-dir drift (`run-pathfinding-tests.ps1` defaults to `D:\MaNGOS\data` not `D:\wwow-bot\prod-data`) | Tracker `A.6` / `Q-D5-1` — repoint the runner to `prod-data` (pre-approved, just apply). |
| OG-Zeppelin tower-climb live test stalls mid-climb | Caller-side route consumption gate (unit-green, live-red) | Tracker `A.7` — read the captured PNG (R16); mesh fixes go in `tools/MmapGen/` only (freeze). |
| Setup times out, then `run-live.ps1` retries | Flaky client boot / injection | Expected — the wrapper retries up to `-MaxAttempts` (default 3) on setup-timeout only. A real behavioral failure is NOT retried. |
| A WPF window pops up during a "headless" run | `WWOW_DISABLE_UI` not consumed yet (pre-A.1) | Land tracker `A.1`; or run with the Aspire/UI host disabled. |
| Live test fails on a missing resource (pool/node/mob) | This is a REAL failure (detection/pathing/ObjectManager bug), NOT a skip | `CLAUDE.md` test-skip policy — do not `Skip.If`; walking to find the resource IS the test. |

---

## 9. Acceptance for this runbook (A.1 + A.2)

- **A.1** is `done` when the `WWOW_SKIP_SERVER_RESTART` + `WWOW_DISABLE_UI`
  gates are wired into the fixture and a single FG smoke test (`A.3`) runs
  past setup against the up stack with no UI window and no full server
  restart.
- **A.2** is `done` when this file is internally consistent with
  `run-live.ps1` and `IntegrationTestConfig`/`BotServiceFixture`, **and**
  `pwsh tools/run-live.ps1 -Filter <A.3 test>` produces a green run
  (`exit 0`) executed in the closing iteration. Record the run date +
  commit in the tracker rows.
