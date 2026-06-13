# Claude orchestrator loop prompt — fix the OG zeppelin deck-lip navmesh route (iterate until GREEN)

> Run this as a fresh Claude (Opus) session, or via `/loop` for self-paced iteration. You are the
> ORCHESTRATOR: you marshal diverse agents (Workflow adversarial fan-outs across models,
> `codex:rescue` for native/bake implementation + an independent GPT-5.x diagnosis lens, parallel
> sub-agents) AND do what only you can — run the probe, READ the FG screenshots (R16), and own the
> validation loop + commits. The stack is UP; toolchains installed (managed `E:\dotnet8`; native
> MSBuild VS18). Repo `E:\repos\Westworld of Warcraft` (remote `QiMata/BloogBot`), branch
> `fix/decklip-arrival-false-green` (PR #65). Full evidence + the prior onion-peel:
> `docs/physics/PATHFIX_GRUNT_FREZZA_HANDOFF_2026-06-01.md` (UPDATE 1-4). Portable rule:
> `E:\repos\docs\PATHFINDING_COLLISION_CONTRACT.md`.

## Mission

Make the WWoW bot climb the Orgrimmar zeppelin tower from the Grunt base to Zeppelin Master Frezza
with PURE Detour (no managed waypoint band-aids) and drive
`Tests/BotRunner.Tests/LiveValidation/LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza` to
GREEN. Loop until fixed.

## Root cause (confirmed — this is what to fix)

The OG zeppelin tower is a vertical SPIRAL: the same (x,y) stacks multiple Z-layers (lower wrap
~24, mid ~35, upper ~47, deck ~53.6). The MmapGen navmesh bake at the deck-lip/upper-spiral
produces polygons that MERGE across original geometry the runtime physics cannot traverse
(step-up height > runtime climb, and overlapping spiral wraps). So Detour's `findSmoothPath`
densification (`moveAlongSurface` + `getPolyHeight`) hops between the overlapping Z-layers and
emits an OSCILLATING, non-monotonic waypoint sequence at the deck-lip. The bot, fed those
waypoints, is steered backward/down, makes no net progress (`reason=stalled_near_waypoint`), and
slides back down the steep ramp. The earlier "arrival" bugs (false-green test, vertical-blind
walk-leg + top-level arrival, 15y arrival radius, cross-map hook disable on a same-map test) are
ALREADY FIXED and committed (PR #65) — the bot now climbs base z24 → deck-lip z47 (~80%); this is
the LAST blocker.

PROVEN ORACLE EVIDENCE (reproduce first, then keep using it):
- RAW `--detour-resolve` Grunt1→Frezza = 56 segs, 1 DOWN segment, monotonic.
- SMOOTH `--detour-resolve --smooth` (what the bot follows) = 127 segs, 16 DOWN segs / 5.7y
  backward; deck-lip corners idx ~86-101 bounce z47→46.5→47 in a ~2y XY cluster. Every segment is
  `Walk`/`StepUp`/`Clear` (none `Blocked`) — so it is NOT per-segment walkability and NOT managed
  arrival; it is a ROUTE-VALIDITY / bake-vs-runtime defect.

## End state (done = ALL of)

1. SMOOTH probe Grunt1→Frezza is near-monotonic (DOWN-seg count ≈ RAW's, ≤ ~2), ZERO `Blocked`,
   ends at Frezza.
2. Live `DeckLipClimbFromGruntToLiteralFrezza` GREEN: bot at Frezza (2D ≤ 6y, |dz| ≤ 4y, z≈53.6).
3. You READ the success FG screenshot and confirm the bot is ON the deck at Frezza (R16 — logs lie).
4. A regression guard committed (probe route-validity assertion and/or deterministic test).
5. Fix on the correct surface (bake / off-mesh / geometry / native query), NOT new managed repair.

## The diverse-agent toolkit (use it — this is the point)

- **Workflow** (per loop iteration): fan out independent agents — ideally across model tiers /
  lenses — to PROPOSE fix hypotheses for the localized defect, then a second stage of skeptics to
  ADVERSARIALLY REFUTE each (different angle each), then synthesize the survivor(s). This is the
  "offer ideas + adversarially review" engine. Bake the probe/source evidence into each agent's
  prompt; have them return structured verdicts.
- **`codex:rescue`** (GPT-5.x): delegate native C++ implementation (`PathFinder.cpp findSmoothPath`
  instrumentation + fix) and/or MmapGen bake-config changes with a tight sub-prompt (you can reuse
  the technical contract below as the payload). Also use it for an INDEPENDENT second diagnosis to
  cross-check your localization.
- **Agent / Explore / Plan**: parallel reads of MmapGen config/README, PathFinder.cpp, the bake
  pipeline; map the walkableClimb/step-up plumbing before changing it.
- **You (Claude)**: run the probe, RUN + READ the live screenshots (the PNGs — Codex can't),
  decide the fix surface, gate on R13, and own commits. Never delegate the screenshot read.

## Loop protocol (orchestrated; one concrete change per iteration)

1. **LOCALIZE** (you): probe `--detour-resolve --smooth`, `--dump-poly-stack` + `--dump-polyrefs`
   at the deck-lip corners, `--verbose`. Pin the FIRST non-monotonic point and the overlapping
   spiral-layer polyref(s)/coords. State them with evidence.
2. **IDEATE + ADVERSARIALLY VET** (Workflow): fan out proposers (seed them with the fix-candidate
   menu + your localization), then skeptics that try to refute each (what it breaks, why it might
   not be root, what evidence refutes it). Synthesize the surviving smallest-correct fix.
3. **IMPLEMENT**: you for managed/simple; `codex:rescue` for native (`PathFinder.cpp`) or MmapGen
   bake. Smallest-correct, on the correct surface. Mesh fixes in tools/MmapGen (respect the
   freeze). NO managed waypoint repair.
4. **VALIDATE** (you): rebuild; re-probe (DOWN-seg count must drop toward RAW's; ZERO Blocked;
   reaches Frezza); run the live DeckLip test; READ the timeline screenshots/snapshots to confirm
   the bot's real final position (z≈53.6 at Frezza), not just the pass line.
5. If not GREEN, record what the evidence now shows (the failure usually MOVES — that is progress),
   re-localize, and loop. When GREEN: add a regression guard, commit, confirm a clean re-run holds.

## Fix-candidate menu (seed hypotheses to vet — not a to-do list)

- **A. BAKE — walkableClimb vs runtime step-up (most likely).** If MmapGen's `walkableClimb` is
  more permissive than the runtime step-up (`PhysicsConstants::STEP_HEIGHT = 2.125f`, harvested
  from the client), the navmesh merges polys across non-climbable steps + spiral wraps. Harvest
  the true step; set the bake walkableClimb to match (per-tile for the OG tower tile); re-bake;
  re-probe. The navmesh should break at non-traversable steps so Detour can't route through them.
- **B. BAKE — break deck-edge / spiral-wrap 2D-adjacent-across-Z polys** (filterLedgeSpans /
  rcFilterWalkableLowHeightSpans / mark edge cells non-walkable) so overlapping Z-layers become
  distinct, non-adjacent polys and smoothPath stops hopping.
- **C. OFF-MESH** for a discrete deck-lip transition the auto-bake can't represent — only after
  the polygon-stack dump proves it can't be a continuous walkable contour.
- **D. NATIVE smoothPath** (`Exports/Navigation/PathFinder.cpp findSmoothPath` 1750-1926) — last
  resort: instrument the loop FIRST (confirm layer-hop vs 2y-step-overshoot), then a monotonic /
  forward-progress guard on the smooth OUTPUT. A wrong change regresses ALL pathing — adversarially
  vet + re-probe other routes.
- **E. FORBIDDEN** — no managed waypoint repair/relaxation in `Navigation.cs`/`NavigationPath.cs`,
  no loosening runtime thresholds, no widening arrival tolerances (15 such caller relaxations
  already failed). Route validity must come from the mesh/query, not the consumer.

## Environment / commands

Managed build: `$env:DOTNET_ROOT='E:\dotnet8'; $env:PATH='E:\dotnet8;'+$env:PATH` then
`dotnet build "Tests\BotRunner.Tests\BotRunner.Tests.csproj" -c Release -m:1 -p:UseSharedCompilation=false`
(global.json pins SDK 8.0.100 — only 8.0.421 at E:\dotnet8 satisfies it; do NOT edit global.json).

Native build (if C++ changes): `& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" Exports\Navigation\Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`.
Probe rebuild: `dotnet build tools\PathPhysicsProbe\PathPhysicsProbe.csproj -c Release`.

Probe (oracle): `$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'`; then
`.\Bot\Release\net8.0\PathPhysicsProbe.exe --map 1 --start 1332.76,-4633.40,24.0783 --end 1331.11,-4649.45,53.6269 --detour-resolve --smooth` (+`--verbose`, `--dump-poly-stack`, `--dump-polyrefs`).
TSV cols: idx,sx,sy,sz,ex,ey,ez,hDist,vDelta,affordance,validation,climb,drop,slope,resolvedZ; filter
data rows with `^\d+\t` (native DLL log lines share stdout). Grunt#1=(1332.76,-4633.40,24.0783),
Frezza=(1331.11,-4649.45,53.6269), map 1; deck-lip stall ≈ (1340-1348,-4646..-4653,z46-48).

Live test (~2.5 min): set `$env:DOTNET_ROOT`,`$env:PATH` as above, then
`$env:WWOW_DATA_DIR='D:\wwow-bot\test-data'; $env:WWOW_USE_LOCAL_PATHFINDING_SERVICE='1'; $env:WWOW_DECKLIP_DIRECT_FREZZA_TEST='1'; $env:WWOW_NAV_SCREENSHOT_EVERY_N_WAYPOINTS='4'; [Environment]::SetEnvironmentVariable('WWOW_LONG_PATHING_SETTINGS_PATH',$null,'Process')`
then `dotnet test "Tests\BotRunner.Tests\BotRunner.Tests.csproj" -c Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LongPathingTests.DeckLipClimbFromGruntToLiteralFrezza" --logger "console;verbosity=minimal" -- RunConfiguration.TestSessionTimeout=1200000`.
Diag: `D:\World of Warcraft\logs\botrunner_LPATHFG1.diag.log`; screenshots:
`tmp\test-runtime\screenshots\long-pathing\timeline\DeckLipClimbFromGruntToLiteralFrezza\` (READ the PNGs).

Bake: `tools\MmapGen\` (read README + config.json; per-tile config + `--debug` as loop-25 did).
Promoted tile = `D:\wwow-bot\test-data\mmaps\0012940.mmtile` (map 1). Confirm the bake toolchain +
source geometry are present before committing to a re-bake; if not reproducible, switch to the
native smoothPath fix and say so.

## Hard rules

- **R13 / freeze**: localize the FIRST bake-vs-runtime disagreement with the probe; fix on the
  correct surface (bake/off-mesh/geometry/native query); NEVER a managed query-time repair.
- **R16**: you must READ the success screenshot; a green pass line with the bot not on the deck is
  a false green (it already happened once this campaign).
- **Grounding**: SHOW the bake defect (polygon-stack dump of overlapping/merged polys, or a
  step-up vs walkableClimb measurement) — do not assert it. Label hypotheses as hypotheses.
- **Scope/safety**: changes scoped to the OG-tower deck-lip; re-probe other OG routes after any
  global change (walkableClimb, smoothPath guard) to confirm no regression; never bypass git hooks.
- **Commit**: branch `fix/decklip-arrival-false-green` (PR #65, QiMata/BloogBot); stage specific
  files only; commit per validated iteration; append findings to the handoff (UPDATE 5+); promote
  any cross-game lesson to `PATHFINDING_COLLISION_CONTRACT.md` (PCECore develop). Do NOT edit global.json.

## Stop condition

Stop only when the END STATE (all 5) holds on a clean re-run, OR you hit a genuine non-headless
wall (missing source geometry for a re-bake, etc.) — in which case capture a turnkey handoff
(root cause + exact recipe + the probe/live validation gate) and the §5 skill/lesson learnings,
and report. "The failure moved higher" is mid-loop progress, not done.
