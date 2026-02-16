# Technical Notes

*Extracted from TASKS.md � 2026-02-09*
*Reference this file with @file when you need offset details, constants, or protocol notes.*

---

## Environment & Paths

| Item | Value |
|------|-------|
| Server | Elysium private server (vanilla 1.12.1 build 5875), stays running |
| MaNGOS server source | `E:\repos\MaNGOS\source\src\` |
| Server protocol docs | `docs/server-protocol/` (7 docs from Task 21) |
| Recordings | `C:\Users\lrhod\Documents\BloogBot\MovementRecordings\` |
| Packet captures | `C:\Users\lrhod\Documents\BloogBot\PacketCaptures\` |
| Test account | ORWR1 (GM level 3, character: Dralrahgra on Kalimdor) |
| Memory notes | `C:\Users\lrhod\.claude\projects\e--repos-BloogBot\memory\` |
| GM commands | `SendChatMessage('.command', 'SAY')` or DoString |

---

## Physics Constants

| Constant | Value |
|----------|-------|
| Gravity | 19.2911 y/s� |
| Jump initial velocity | 7.9555 y/s |
| Terminal velocity | 60.148 y/s |
| Forward run speed | 7.001 y/s |
| Backward run speed | 4.502 y/s |
| Strafe speed | 6.941 y/s |
| Diagonal (fwd+strafe) | 6.983 y/s (99.76% of run speed � normalized, not sqrt(2)�run) |
| Jump duration | 0.800s (expected 0.825s) |
| Measured gravity | 19.43 y/s� (0.7% error vs expected 19.29) |

---

## Test Commands

```bash
# Run all test layers in dependency order
.\run-tests.ps1

# DLL availability only
.\run-tests.ps1 -Layer 1

# Physics & pathfinding
.\run-tests.ps1 -Layer 2

# Physics tests (42/43 passing)
dotnet test Tests/Navigation.Physics.Tests --settings Tests/BotRunner.Tests/test.runsettings -v n

# Manual recording test
dotnet test Tests/BotRunner.Tests --filter "FullyQualifiedName~MovementRecording" --settings Tests/BotRunner.Tests/test.runsettings -v n

# Full integration (needs MaNGOS)
dotnet test Tests/BotRunner.Tests --filter "Category=Integration" --settings Tests/BotRunner.Tests/test.runsettings -v n

# Swimming recording session (requires admin)
.\run-swimming-recording-test.ps1
```

**IMPORTANT:** Always use `--settings Tests/BotRunner.Tests/test.runsettings` for x86 platform target.

---

## Recording-to-Test Mapping

| Scenario | Recording File | Key Data |
|----------|----------------|----------|
| FlatRunForward | `Orgrimmar_2026-02-08_11-32-13` | 1203 fwd, 34 still, avgSpeed=6.97 |
| FlatRunBackward | `Durotar_2026-02-08_11-06-59` | 65 pure backward, mixed movement |
| StandingJump | `Orgrimmar_2026-02-08_11-31-46` | 200 falling, individual arcs extracted |
| RunningJump | `Orgrimmar_2026-02-08_11-01-15` | 3495 fwd, 464 falling |
| FallFromHeight | `Orgrimmar_2026-02-08_11-32-44` | 93 fallingFar, 36.6y zRange |
| StrafeDiagonal | `Durotar_2026-02-08_11-24-45` | 55 FWD+STR_R, 48 FWD+STR_L |
| StrafeOnly | `Durotar_2026-02-08_11-06-59` | 40 STR_R, 46 STR_L frames |
| ComplexMixed | `Durotar_2026-02-08_11-06-59` | fwd/back/strafe/fall, 1142 frames |
| OrgRunningJumps | `Orgrimmar_2026-02-08_11-01-15` | running + jump arcs |
| LongFlatRun | `Durotar_2026-02-08_11-37-56` | 5028 frames, 82s pure forward |
| UndercityMixed | `Undercity_2026-02-08_11-30-52` | strafe/fall/fallingFar |
| Swimming | `Durotar_2026-02-09_19-16-08` | Forward swim, ascend, descend, water entry/exit |
| Charge spline | `Dralrahgra_Durotar_2026-02-08_12-28-15` | ONLY recording with player spline JSON |
| Knockback | `Dralrahgra_Blackrock_Spire_2026-02-08_12-04-53` | Knockback flags/speeds, no spline data |

---

## Centralized PathfindingService Architecture

```
????????????????????????                         ???????????????????????
?  ForegroundBotRunner  ????? GetPath ????????????                     ?
?  (injected client)    ?     LineOfSight         ?  PathfindingService ?
????????????????????????                    ??????  (single process)   ?
?  WoWSharpClient 1     ????? PhysicsStep ???    ?                     ?
?  (headless client)    ?     GetPath             ?  Navigation.dll     ?
????????????????????????     LineOfSight          ?  (maps loaded once) ?
?  WoWSharpClient N     ???????????????????????????                     ?
????????????????????????     TCP/protobuf:5001    ???????????????????????
```

---

## Known Issues & Workarounds

- **FastCall.dll stale copy** � `Bot\Debug\net8.0\FastCall.dll` can be 12KB (stale, missing `LuaCall` export). Correct version is 62KB. `BotServiceFixture` auto-detects and fixes.
- **StateManager DLL lock race** � StateManager and test can fight over DLLs. Must kill ? build ? verify ? start SM ? test. Script `run-swimming-recording-test.ps1` handles this.
- **Orgrimmar terrain divergence** � `FlatRunForward_FrameByFrame` test fails due to terrain elevation causing PhysicsEngine position divergence beyond 0.5y tolerance. Genuine calibration gap in C++ ground detection.
- **Spline data scarcity** � Only 1 of 31 recordings has player spline data (`Dralrahgra_Durotar_2026-02-08_12-28-15.json`). All others predate the spline JSON fix.
