# Claude Prompt - get WWoW mmaps into the desired state

Paste this into Claude Code from `E:/repos/Westworld of Warcraft`.

## Mission

Get the mmap data roots into the desired state without repeating the bad-copy
failure that broke NPC behavior.

Desired state:

1. `D:/MaNGOS/data/mmaps` is server-owned, generated or restored for MaNGOS
   server NPC pathing, and is not a bot-generated mmap set.
2. `D:/MaNGOS/data/mmaps` contains at minimum `000.mmap`, `001.mmap`, and
   matching `000*` and `001*` tile coverage. A single-prefix `001`-only set is
   bad.
3. `D:/wwow-bot/test-data/mmaps` remains bot-owned scratch data.
4. `D:/wwow-bot/prod-data/mmaps` remains bot production data for
   `wwow-pathfinding` and `wwow-scene-data`.
5. Bot route simplification analysis stays separate from MaNGOS server rescue.
   OG may currently reduce to the route-observed `0012840` and `0012940`
   surface; BRM must not be reduced until an endpoint-reaching probe exists.

Read this first:

- `AGENTS.md`
- `CLAUDE.md`
- `docs/physics/MMAP_DATA_FLOW.md`
- `tools/MmapGen/AGENTS.md`
- `docs/Plan/Pathfinding/MMAP_SERVER_REGEN_PROMPT_2026-06-13.md`

Use the existing rescue runbook as the source of truth for the inventory,
backup, regeneration, and route-coverage facts.

## Required sub-agent workflow

You must use Codex as a sub-agent twice:

1. Codex coding sub-agent before any repository code or script changes.
2. Codex adversarial-review sub-agent after coding and before any tests.

Do not run tests until the adversarial review has completed and all blocking
findings are fixed or explicitly documented as non-blocking.

Use the Codex CLI as described in `CLAUDE.md`. Keep the prompts focused and
paste the relevant command output or file diffs into the Codex prompt when
needed.

## Phase 0 - inventory and halt unsafe work

Do not run live long-pathing tests yet.
Do not copy bot mmaps into `D:/MaNGOS/data`.
Do not use `tools/MmapGen/build/MmapGen.exe` to generate MaNGOS server NPC
mmaps.
Do not promote `D:/MaNGOS/data/mmaps.iter21-failed`.

Start by collecting a read-only inventory:

```powershell
git status --short
Get-Process MoveMapGenerator -ErrorAction SilentlyContinue

$roots = @(
  'D:\MaNGOS\data\mmaps',
  'D:\MaNGOS\data\mmaps.iter21-failed',
  'D:\MaNGOS\data\mmaps_backup_20260315',
  'D:\MaNGOS\data\mmaps_old_pre_reextract',
  'D:\wwow-bot\test-data\mmaps',
  'D:\wwow-bot\prod-data\mmaps'
)

foreach ($root in $roots) {
  if (-not (Test-Path -LiteralPath $root)) {
    [pscustomobject]@{ Path = $root; Exists = $false }
    continue
  }

  $files = Get-ChildItem -LiteralPath $root -File -Recurse
  $prefixes = $files |
    Group-Object { if ($_.Name -match '^(\d{3})') { $matches[1] } else { 'other' } } |
    Sort-Object Name |
    ForEach-Object { "$($_.Name)=$($_.Count)" }

  [pscustomobject]@{
    Path = $root
    Exists = $true
    Files = $files.Count
    Bytes = ($files | Measure-Object Length -Sum).Sum
    Newest = ($files | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
    Prefixes = ($prefixes -join ', ')
  }
}
```

Record the inventory in a dated handoff note or in your final answer.

## Phase 1 - ask Codex to implement any safety/code support

Before editing scripts, tests, or docs beyond the handoff note, invoke Codex as
the coding sub-agent.

Use this prompt:

```text
You are Codex acting as a coding sub-agent inside E:/repos/Westworld of Warcraft.

Goal: help Claude get the mmap data roots into the desired state without
repeating the bad-copy failure.

Read:
- AGENTS.md
- CLAUDE.md
- docs/physics/MMAP_DATA_FLOW.md
- docs/Plan/Pathfinding/MMAP_SERVER_REGEN_PROMPT_2026-06-13.md
- tools/MmapGen/AGENTS.md

Task:
1. Inspect the current repo for existing scripts/tests that verify mmap root
   ownership, prefix coverage, bot/server data separation, and route coverage.
2. If an adequate verifier already exists, report the exact command Claude
   should run later.
3. If no adequate verifier exists, implement a small non-destructive verifier
   script under tools/scripts/ that:
   - checks D:/MaNGOS/data/mmaps exists
   - fails if it is 001-only
   - checks 000.mmap and 001.mmap
   - reports prefix counts for server, bot test, and bot prod roots
   - warns if bot data appears to have been copied into MaNGOS by exact
     filename/size overlap
   - does not delete, rename, copy, regenerate, or restart anything
4. Add focused tests for the verifier if the repo has a nearby pattern for
   script/verifier tests. If not, keep the script self-validating with a dry-run
   mode and document manual validation.
5. Do not modify generated files. Do not touch D:/MaNGOS/data.
6. Return a concise implementation summary and the exact validation commands.
```

Claude must review Codex's changes before continuing. If Codex proposes a
destructive script or direct MaNGOS mutation, reject that part and keep only
non-destructive verification.

## Phase 2 - choose the server rescue path

Choose one of these paths after inventory:

### Preferred path: regenerate server mmaps

Use `D:/MaNGOS/source/bin/MoveMapGenerator.exe`, run from `D:/MaNGOS/data`.
Back up the current bad server mmap directory first.

```powershell
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$data = 'D:\MaNGOS\data'
$bad = Join-Path $data 'mmaps'
$backupName = "mmaps.bad-$stamp"

$resolvedData = (Resolve-Path -LiteralPath $data).Path
$resolvedBad = (Resolve-Path -LiteralPath $bad).Path
$resolvedDataPrefix = $resolvedData.TrimEnd('\') + '\'
if (-not $resolvedBad.StartsWith($resolvedDataPrefix, [StringComparison]::OrdinalIgnoreCase)) {
  throw "Refusing to mutate a path outside D:\MaNGOS\data: $resolvedBad"
}

Rename-Item -LiteralPath $bad -NewName $backupName
New-Item -ItemType Directory -Path (Join-Path $data 'mmaps') | Out-Null

Push-Location $data
try {
  & 'D:\MaNGOS\source\bin\MoveMapGenerator.exe' --threads 8 --silent *> "server-mmap-all-$stamp.log"
  if ($LASTEXITCODE -ne 0) {
    throw "MoveMapGenerator failed with exit code $LASTEXITCODE"
  }
} finally {
  Pop-Location
}
```

If full generation is too slow for the immediate recovery, regenerate maps `0`
and `1` first, but record that the server is not yet fully restored for
instances/BGs/raids.

### Emergency path: restore known-good server backup

Use this only if regeneration cannot complete or the server must be restored
immediately.

```powershell
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$data = 'D:\MaNGOS\data'
$bad = Join-Path $data 'mmaps'
$source = Join-Path $data 'mmaps_backup_20260315'

# Path-containment guard: refuse to mutate anything outside D:\MaNGOS\data.
$resolvedData = (Resolve-Path -LiteralPath $data).Path
$resolvedBad  = (Resolve-Path -LiteralPath $bad).Path
$resolvedSrc  = (Resolve-Path -LiteralPath $source).Path
$dataPrefix = $resolvedData.TrimEnd('\') + '\'
if (-not $resolvedBad.StartsWith($dataPrefix, [StringComparison]::OrdinalIgnoreCase)) {
  throw "Refusing to mutate a path outside ${data}: $resolvedBad"
}
if (-not $resolvedSrc.StartsWith($dataPrefix, [StringComparison]::OrdinalIgnoreCase)) {
  throw "Restore source outside ${data}: $resolvedSrc"
}

# Stop ONLY the mangosd container first to avoid file locks during the copy
# (net container impact is identical to the `docker restart wow-mangosd` below).
docker stop wow-mangosd

Rename-Item -LiteralPath $bad -NewName "mmaps.bad-$stamp"   # preserve the bad set as evidence
Copy-Item -LiteralPath $source -Destination $bad -Recurse

docker start wow-mangosd   # reloads the restored server mmaps
```

Do not use `mmaps.iter21-failed`.

## Phase 3 - verify desired server state before tests

Run the verifier from the Codex coding sub-agent if one was added or found.
Also run this direct check:

```powershell
$files = Get-ChildItem -LiteralPath 'D:\MaNGOS\data\mmaps' -File
$files |
  Group-Object { if ($_.Name -match '^(\d{3})') { $matches[1] } else { 'other' } } |
  Sort-Object Name |
  Select-Object Name, Count

Test-Path 'D:\MaNGOS\data\mmaps\000.mmap'
Test-Path 'D:\MaNGOS\data\mmaps\001.mmap'
```

The server mmap root is not acceptable if:

- `000.mmap` is missing
- `001.mmap` is missing
- prefixes show only `001`
- the only source of the replacement was bot test/prod data
- the replacement came from `mmaps.iter21-failed`

Restart only the server container that must reload server mmap data:

```powershell
docker restart wow-mangosd
```

If auth or DB health fails later, restart only the named failing service
identified by the health check, such as `wow-realmd` or `maria-db`.

## Phase 4 - Codex adversarial review before tests

Before running tests, invoke Codex as an adversarial reviewer.

Use this prompt:

```text
You are Codex acting as an adversarial review sub-agent inside
E:/repos/Westworld of Warcraft.

Review Claude's mmap recovery work before any tests run.

Inputs to inspect:
- git diff
- the before/after inventory for D:/MaNGOS/data/mmaps,
  D:/wwow-bot/test-data/mmaps, and D:/wwow-bot/prod-data/mmaps
- any new verifier script or test changes
- the exact regeneration or restore commands that were run
- docs/physics/MMAP_DATA_FLOW.md
- docs/Plan/Pathfinding/MMAP_SERVER_REGEN_PROMPT_2026-06-13.md

Be adversarial. Look for:
1. Any bot-generated mmaps copied into D:/MaNGOS/data.
2. Any use of tools/MmapGen/build/MmapGen.exe for MaNGOS server NPC mmaps.
3. Any use of D:/MaNGOS/data/mmaps.iter21-failed as a source.
4. Missing backup of the previous D:/MaNGOS/data/mmaps.
5. Path mistakes, especially operations outside D:/MaNGOS/data.
6. Swapped tile-coordinate mistakes: OG tower is --tile 40,29 and
   writes 0012940.mmtile; 0014029.mmtile is the swapped tile.
7. Tests that point bot pathfinding at the wrong data root.
8. Claims of BRM simplification based on partial paths that do not reach the
   endpoint.
9. Any destructive or broad process cleanup, such as blanket-killing dotnet,
   game, or Docker processes.

Return findings first, ordered by severity. Include blocking/non-blocking
classification and exact file/command references. If there are no blocking
findings, explicitly say tests may proceed.
```

If Codex finds any blocking issue, fix it and repeat the adversarial review
before testing.

## Phase 5 - tests and live checks

Only run tests after the adversarial review says tests may proceed.

Run the smallest checks first:

```powershell
# If a verifier script was added by Codex, run it first.
# Example name only; use the actual script Codex implemented or found.
.\tools\scripts\verify-mmap-roots.ps1
```

Then run a server health smoke. Use existing repo health helpers if present.
If using Docker manually, keep it targeted:

```powershell
docker ps --filter "name=wow-mangosd" --filter "name=wow-realmd" --filter "name=maria-db"
```

For bot-side route probes, keep data roots isolated:

```powershell
$variant = "post-server-mmap-rescue-$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))"

.\tools\scripts\probe-routes.ps1 `
  -Manifest tools\scripts\routes\og-zeppelin.json `
  -DataDir D:\wwow-bot\test-data `
  -Variant $variant `
  -DetourResolve `
  -SmoothPath
```

Do not treat BRM as reduced or green unless an endpoint-reaching probe proves
it. The current 2026-06-13 probe evidence says BRM partial paths terminate
about 909-1214 yards from the requested endpoints.

If live BotRunner tests are needed after server health is restored, use the
live fixture contract:

- tests must poll StateManager APIs
- fail fast on disconnect/crash/stale snapshot/bad health
- capture final snapshots/logs/screenshots on failure
- use `D:/wwow-bot/test-data` for bot pathfinding, not `D:/MaNGOS/data`

Do not resume Crossroads -> Undercity live validation until the server mmap
root is verified healthy.

## Final report requirements

Report:

1. Which path was used: full server regeneration, map `0`/`1` partial
   regeneration, or emergency backup restore.
2. The backup path for the previous bad `D:/MaNGOS/data/mmaps`.
3. Before/after file counts and prefix counts.
4. Whether `000.mmap` and `001.mmap` exist.
5. The exact Codex coding sub-agent summary.
6. The exact Codex adversarial-review verdict.
7. Tests/checks run and their results.
8. Remaining risks, especially if only continents were regenerated or BRM is
   still partial.

Stop and ask the user before deleting any backups or shrinking the server mmap
set. The rescue pass is allowed to restore health; minimization is a separate
decision.
