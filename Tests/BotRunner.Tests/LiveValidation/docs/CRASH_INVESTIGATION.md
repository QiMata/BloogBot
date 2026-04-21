# Live Test Crash Investigation Log

Iterative research document tracking crashes and failures observed during LiveValidation test runs.
Each attempt is documented with: observation, hypothesis, fix attempted, and result.

---

## CRASH-001: FG Corpse Run - historical WoW.exe ACCESS_VIOLATION during ghost runback

**Test:** `DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer`
**First observed:** 2026-03-15 (crash dump), reproduced 2026-03-16
**Status:** HISTORICAL - not reproduced in 2026-04-15 opt-in revalidation. The later opt-in corpse-run validation now passes; this file is crash-history context, not an active blocker.

### 2026-04-15 Follow-up Revalidation

Command:

```powershell
$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_RETRY_FG_CRASH001='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer" --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=fg_corpse_run_after_corpse_probe_policy.trx"
```

Outcome:
- Test passed.
- WoW.exe did not crash.
- The foreground ghost released, ran back, entered reclaim range, and restored strict-alive state.
- Evidence lines in the TRX include graveyard start at 152y from corpse, recovery progress to best 34y, `Alive after 30s`, and `RetrieveCorpseTask pop reason=AliveAfterRetrieve`.

Implication:
- The old access violation remains historical.
- The temporary 2026-04-15 runback/reclaim stall was path-following behavior, not crash behavior.
- Crash triage should reopen only if a fresh WoW.exe access violation reproduces.

### 2026-04-15 Revalidation

Command:

```powershell
$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_RETRY_FG_CRASH001='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer" --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=fg_corpse_run_crash001_revalidation.trx"
```

Outcome:
- Test failed, but WoW.exe did not crash.
- The ghost was released and `RetrieveCorpseTask` queued successfully.
- Runback improved from the graveyard distance of about 152y to best 121y from the corpse.
- The foreground client then stalled near `(237.1,-4749.0,13.0)` before reclaim range.
- Captured task evidence showed `RetrieveCorpseTask` still active with `plan=81` and `resolution=waypoint`.

Implication:
- The old access violation notes below are useful historical context, but this file should no longer be treated as an active crash blocker.
- The follow-up revalidation above supersedes this failed runback result with a passing opt-in foreground corpse-run proof.

### Historical Symptom

Earlier 2026-03 runs observed WoW.exe crashing during FG ghost form corpse retrieval runback:
1. Teleport to Razor Hill `(340,-4686,19.5)`
2. Kill via `.die Testgrunt`
3. Release corpse, ghost state confirmed
4. Graveyard settled at `(233.5,-4793.7,10.2)`, 152y from corpse
5. `RetrieveCorpse` dispatched and ghost started walking toward corpse
6. WoW.exe crashed after 20-30s of ghost movement, with the bot around 117-128y from corpse

The BG test passed because the headless bot does not use WoW.exe.

### Historical Crash Details

From the 2026-03-15 `Errors/` dump:

```text
ERROR #132 (0x85100084) Fatal Exception
Exception: 0xC0000005 (ACCESS_VIOLATION) at 0023:00619CDF
The instruction at "0x00619CDF" referenced memory at "0x00000000".
The memory could not be "written".
EDX=00000000 (NULL write target)

Stack trace:
00619CDF -> 0061787B -> 0060DC82 -> 00514E73 -> 00514791 -> 005151A5 -> 00513E46
-> 006F9231 -> 006F65EF -> 006F425B -> 006F699B -> 006F423C -> 00704D27
-> 004B7B11 -> 00483BC0 -> 00765F99 -> 004246B0 -> 00423D46 -> 00423A60
-> 00423971 -> 00420D28 -> 00420BF1 -> 0040411E
```

The historical crash was on WoW's main thread in its normal frame update path:
- `0x00420BF1`: WinMain message loop
- `0x004246B0`: game frame processing
- `0x00765F99`: world/rendering update
- `0x00483BC0`: object processing
- `0x00619CDF`: NULL write in movement/update code

### Historical Attempts

| # | Hypothesis | Change | Result |
|---|-----------|--------|--------|
| 1 | First run | Baseline | Crash, bestDist=126y |
| 2 | Reproducibility check | None | Crash, bestDist=127y |
| 3 | Movement calls race with WoW's game loop | Route SetFacing, SendMovementUpdate, and SetControlBit through `RunOnMainThread()` atomically | Crash, bestDist=120y |
| 4 | Native movement functions crash ghost form | Replace SetFacing/SetControlBit with Lua `MoveForwardStart()` via `RunOnMainThread()` | Crash, bestDist=152y |
| 5 | `SendMovementUpdate(MSG_MOVE_SET_FACING)` corrupts ghost movement state | Skip `SendMovementUpdate`; write facing and SetControlBit only | Crash, bestDist=152y |
| 6 | Native `ReleaseCorpse()` corrupts internal state | Replace with Lua `RepopMe()` | Crash, bestDist=119y |
| 7 | `EnumerateVisibleObjects` via WM_USER corrupts ghost-form WoW state | Skip `EnumerateVisibleObjects` entirely when player has ghost flag | Crash, bestDist=117y |

### Historical Ruled-Out Areas

- Thread safety of movement calls did not explain the 2026-03 crash.
- Native vs Lua movement did not explain it.
- Movement packet sending did not explain it.
- Native `ReleaseCorpse()` did not explain it.
- Visible-object enumeration did not explain it.
- The crash stack was in WoW's own frame processing, not directly in our callback or hook code.

### Current Resolution Path

1. Treat this document as historical context only.
2. Keep foreground corpse-run validation available with `WWOW_RETRY_FG_CRASH001=1` while deciding whether to promote it back into the default live matrix.
3. Reopen crash triage only if a fresh access violation reproduces and matches or replaces the historical `0x00619CDF` signature.

---
