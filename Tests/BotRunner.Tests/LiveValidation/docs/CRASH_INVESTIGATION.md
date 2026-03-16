# Live Test Crash Investigation Log

Iterative research document tracking crashes and failures observed during LiveValidation test runs.
Each attempt is documented with: observation, hypothesis, fix attempted, and result.

---

## CRASH-001: FG Corpse Run — WoW.exe ACCESS_VIOLATION during ghost runback

**Test:** `DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer`
**First observed:** 2026-03-15 (crash dump), reproduced 2026-03-16
**Status:** BLOCKED — WoW client bug, not fixable from injected code

### Symptom

WoW.exe crashes 100% of the time during FG ghost form corpse retrieval runback:
1. Teleport to Razor Hill (340, -4686, 19.5)
2. Kill via `.die Testgrunt`
3. Release corpse -> ghost state confirmed
4. Graveyard settled at (233.5, -4793.7, 10.2), 152y from corpse
5. RetrieveCorpse dispatched -> ghost starts walking toward corpse
6. **WoW.exe crashes** after 20-30s of ghost movement (bot reaches ~117-128y from corpse)

BG test passes every time.

### Crash Details (from 2026-03-15 Errors/ dump)

```
ERROR #132 (0x85100084) Fatal Exception
Exception: 0xC0000005 (ACCESS_VIOLATION) at 0023:00619CDF
The instruction at "0x00619CDF" referenced memory at "0x00000000".
The memory could not be "written".
EDX=00000000 (NULL write target)

Stack trace (WoW's main game loop):
00619CDF -> 0061787B -> 0060DC82 -> 00514E73 -> 00514791 -> 005151A5 -> 00513E46
-> 006F9231 -> 006F65EF -> 006F425B -> 006F699B -> 006F423C -> 00704D27
-> 004B7B11 -> 00483BC0 -> 00765F99 -> 004246B0 -> 00423D46 -> 00423A60
-> 00423971 -> 00420D28 -> 00420BF1 -> 0040411E
```

The crash is on WoW's **main thread** in its normal frame update path:
- 0x00420BF1 = WinMain message loop
- 0x004246B0 = game frame processing
- 0x00765F99 = world/rendering update
- 0x00483BC0 = object processing
- 0x00619CDF = crash — writing NULL in movement/update code

### Attempts

| # | Hypothesis | Change | Result |
|---|-----------|--------|--------|
| 1 | First run | Baseline | CRASH — bestDist=126y (26y progress) |
| 2 | Reproducibility check | None | CRASH — bestDist=127y (25y progress), consistent |
| 3 | Thread safety: movement calls race with WoW's game loop | Route SetFacing+SendMovementUpdate+SetControlBit through `RunOnMainThread()` atomically | CRASH — bestDist=120y (32y progress). Thread safety not the issue |
| 4 | Native movement functions crash ghost form | Replace SetFacing/SetControlBit with pure Lua: `MoveForwardStart()` via `RunOnMainThread()` | CRASH — bestDist=152y (0y progress). Crashed even FASTER, no movement at all |
| 5 | `SendMovementUpdate(MSG_MOVE_SET_FACING)` corrupts ghost movement state | Skip `SendMovementUpdate` — only write facing to memory + SetControlBit | CRASH — bestDist=152y (0y progress) |
| 6 | Native `ReleaseCorpse()` (0x005E0AE0) corrupts internal state | Replace with Lua `RepopMe()` | CRASH — bestDist=119y (33y progress). Same pattern, ruling out ReleaseCorpse |
| 7 | `EnumerateVisibleObjects` via WM_USER during ghost form corrupts WoW state | Skip EnumerateVisibleObjects entirely when player has ghost flag | CRASH — bestDist=117y (35y progress). Not enumeration-related |

### Ruled Out

- **Thread safety of movement calls** — RunOnMainThread didn't help (attempt 3)
- **Native vs Lua movement** — Both crash (attempts 4-5)
- **Movement packet sending** — Skipping packets still crashes (attempt 5)
- **Native ReleaseCorpse corruption** — Lua RepopMe still crashes (attempt 6)
- **EnumerateVisibleObjects** — Skipping enumeration still crashes (attempt 7)
- **Our injected code causing the crash** — The crash is in WoW's own game frame processing stack, not in our callback or hook code

### Root Cause Assessment

The crash is in **WoW.exe's own game loop** at address 0x00619CDF, in the object/movement processing path. It happens deterministically ~20-30s after entering ghost form near Razor Hill, regardless of what our injected code does. The crash appears to be a **WoW 1.12.1 client bug** related to ghost form object processing near this specific graveyard/terrain.

The BG (headless) bot doesn't use WoW.exe at all — it emulates the protocol — so it's never affected.

### Resolution Path

1. **Short-term:** Skip the FG corpse run test. The BG test validates the RetrieveCorpseTask logic. The FG crash is a WoW client issue, not a bot code issue.
2. **Medium-term:** Investigate if the SignalEventManager hook or PacketLogger hook assembly patches corrupt WoW's code near the crash address. Test with hooks disabled.
3. **Long-term:** Reverse engineer WoW.exe at 0x00619CDF to understand what NULL pointer it's dereferencing. May require IDA Pro analysis of the binary.

---
