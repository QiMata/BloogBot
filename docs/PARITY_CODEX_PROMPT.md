# Complete FG/BG Movement & Collision Parity — Codex CLI Prompt

Copy this entire prompt into Codex CLI to complete the remaining parity work.

---

## MISSION

You are finishing 100% parity between the BG (headless Background Bot — pure C# + native physics DLL) and FG (Foreground Bot — injected into WoW.exe 1.12.1 which is the GOLD STANDARD). The BG must reproduce the exact movement, collision, and packet behavior of the original WoW.exe client.

**WoW.exe binary location:** `D:/World of Warcraft/WoW.exe` (1.12.1 vanilla, x86 PE32, image base 0x400000)
**Use Python + capstone to disassemble:** `py -c "from capstone import *; ..."` — VA to file offset: `file_offset = VA - 0x400000`

**RULES:**
1. Every change MUST be backed by WoW.exe binary evidence. Disassemble the relevant function, document what you find, then implement.
2. Remove any BG code that implements behavior NOT present in WoW.exe. No workarounds, no heuristics, no approximations without binary proof.
3. After every code change, rebuild and run the proof gates. Never commit code that fails tests.
4. Add regression tests for every behavior change so regressions are caught immediately.
5. Commit and push after each logical unit of work.

## BINARY REFERENCE — KEY ADDRESSES

### Movement Controller (CMovement struct)
| Address | Function | Purpose |
|---------|----------|---------|
| `0x633840` | `CMovement::CollisionStep` | Main collision orchestrator — grounded, airborne, swim |
| `0x6367B0` | Grounded wall/corner driver | Retry loop (up to 5 iter), calls helpers below |
| `0x636610` | Blocker-axis merge | 1/2/3/4 contact merge via jump table |
| `0x636100` | Branch gate | Returns 0/1/2; gates horizontal vs vertical correction |
| `0x635D80` | Horizontal epsilon correction | 0.001f pushout after blocker projection |
| `0x635C00` | Vertical correction | Selected-plane Z correction with radius clamp |
| `0x6373B0` | AABB merge | Unions start/end/half-step AABBs |
| `0x6721B0` | TestTerrain | Static position AABB terrain query |
| `0x637300` | ExpandAndSweep | Epsilon-expanded swept AABB |
| `0x637330` | Vec3Negate | Flips contact normals from TestTerrain |
| `0x617430` | GetBoundingRadius | Returns capsule height (default 2.0) |
| `0x616DE0` | Remote unit extrapolation | Position prediction for other players/NPCs |

### Terrain/Collision Data Loading
| Address | Function | Purpose |
|---------|----------|---------|
| `0x6AA8B0` | Spatial collision grid query | Grid-based terrain/model lookup |
| `0x6AADC0` | Per-chunk intersection test | Tests one map chunk |
| `0x6334A0` | CheckWalkable | Determines if a contact surface is walkable |

### Movement Packet Handling
| Address | Function | Purpose |
|---------|----------|---------|
| `0x537AA0` | NetClient::ProcessMessage | Incoming packet dispatcher |
| `0x5379A0` | NetClient::Send | Outgoing packet send |

### Key Constants (read from binary with Python)
| Address | Value | Meaning |
|---------|-------|---------|
| `0x7FF9D8` | 1.0f | Distance threshold for loop exit |
| `0x8026BC` | 9.54e-7f | FP epsilon for zero-distance check |
| `0x80DFFC` | 0.6428f | cos(50 deg) — walkable slope threshold |
| `0x80E008` | 1.1918f | stepHeight * STEP_HEIGHT_FACTOR |
| `0x7FFD74` | 0.0f | Zero constant |
| `0x80DFEC` | 1/720 | Angular skin epsilon |

### CMovement Struct Layout (partial)
| Offset | Type | Field |
|--------|------|-------|
| +0x10 | float | Position.X |
| +0x14 | float | Position.Y |
| +0x18 | float | Position.Z |
| +0x40 | uint32 | MovementFlags |
| +0xB0 | float | BoundingHeight |
| +0x15C | ptr | Model/collision data |

## EXISTING DISASSEMBLY FILES

Read these before starting — they contain raw x86 disassembly already captured from the binary:
- `docs/physics/0x6367B0_disasm.txt` — Grounded wall/corner driver (main loop)
- `docs/physics/0x636100_disasm.txt` — Branch gate function
- `docs/physics/0x635c00_disasm.txt` — Vertical correction helper
- `docs/physics/0x635d80_disasm.txt` — Horizontal epsilon helper
- `docs/physics/0x636610_disasm.txt` — Blocker-axis merge
- `docs/physics/0x6367B0_pseudocode.md` — Decompiled pseudocode
- `docs/physics/wow_exe_decompilation.md` — Full decompilation notes

## CURRENT BG IMPLEMENTATION FILES

| File | Lines | What it does |
|------|-------|-------------|
| `Exports/Navigation/PhysicsEngine.cpp` | 2688 | C++ native physics (CollisionStepWoW, wall resolution, ground snap) |
| `Exports/WoWSharpClient/Movement/MovementController.cs` | 1258 | C# movement controller (heartbeat timing, packet sending, physics bridge) |
| `Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs` | 781 | C# movement API (MoveToward, StopAllMovement, teleport handling) |
| `Exports/WoWSharpClient/WoWSharpObjectManager.cs` | 997 | Game loop (physics tick, sub-stepping, update dispatch) |
| `Exports/Navigation/PhysicsEngine.h` | ~130 | Physics constants and MovementState struct |

## WHAT TO DO — IN ORDER

### Phase 1: Binary Audit of Collision Pipeline

1. **Disassemble `0x633840` (CollisionStep)** — the top-level collision orchestrator. Document how it decides between grounded, airborne, and swim paths. Compare against `CollisionStepWoW` in `PhysicsEngine.cpp`. Remove any branches in our code that don't exist in the binary.

2. **Disassemble the airborne collision path** in `0x633840` — find where it handles FALLINGFAR/jumping. Compare against our airborne handling in `PhysicsEngine.cpp`. Ensure gravity, terminal velocity, and fall-land detection match exactly.

3. **Disassemble `0x6334A0` (CheckWalkable)** — understand exactly how WoW.exe determines if a surface is walkable. Compare against our walkability checks. The cos(50°) = 0.6428 threshold must match exactly.

4. **Disassemble the terrain data loading path** — starting from `0x6AA8B0` (spatial grid), trace how ADT/WMO/M2 geometry is loaded and queried. Compare against our `SceneQuery::TestTerrainAABB`. Ensure the AABB query dimensions, step heights, and search volumes match the binary.

### Phase 2: Binary Audit of MovementController Packet Timing

5. **Disassemble the movement packet send path** — find where WoW.exe decides to send `MSG_MOVE_HEARTBEAT`, `MSG_MOVE_START_FORWARD`, `MSG_MOVE_STOP`, `MSG_MOVE_SET_FACING`, and `MSG_MOVE_FALL_LAND`. Document the exact timing (heartbeat interval), the conditions for each packet type, and the MovementInfo payload format.

6. **Compare against `MovementController.cs`** — our BG sends heartbeats at ~500ms. Verify this matches the binary. Check that the conditions for sending STOP, START_FORWARD, SET_FACING, and FALL_LAND match exactly.

7. **Disassemble the teleport ACK flow** — trace `MSG_MOVE_TELEPORT_ACK` handling in WoW.exe. Compare against our `EventEmitter_OnTeleport` and `NotifyTeleportIncoming`. Ensure flags are cleared correctly, position is updated correctly, and the first post-teleport packet matches.

### Phase 3: Remove Non-Binary Code

8. **Audit `PhysicsEngine.cpp` for heuristics** — search for any threshold, guard, fallback, or workaround that isn't backed by a specific binary address. Either find the binary evidence or remove the code. Key suspects:
   - `opposeScore <= 0.15f` threshold in blocker candidate selection
   - `> 0.25f` dominant-axis tests
   - The uphill correction discard (`projectedMove.z > 0.0f && horizontalResolved2D > slopedResolved2D + 1e-4f`)
   - Multi-level terrain disambiguation at GetGroundZ
   - Any `// TODO`, `// heuristic`, `// approximate` comments

9. **Audit `MovementController.cs` for non-WoW behaviors** — check for any timing, threshold, or packet logic that doesn't exist in WoW.exe. Remove or replace with binary-backed implementations.

10. **Audit `WoWSharpObjectManager.Movement.cs`** — check MoveToward, StopAllMovement, and all movement flag manipulation against the binary. Ensure flag clearing, setting, and packet sending matches exactly.

### Phase 4: Regression Test Coverage

11. **Add deterministic tests for every collision path** — grounded wall slide, airborne fall, slope detection, step-up/step-down, walkable surface selection. Each test should pin a specific binary behavior.

12. **Add packet timing tests** — verify heartbeat intervals, START/STOP sequencing, SET_FACING conditions, and FALL_LAND triggers against captured FG packet evidence.

13. **Add teleport tests** — verify flag reset, position update, ground snap timing, and first post-teleport packet match FG behavior.

14. **Run the full live parity suite** — these tests must ALL pass:
    ```bash
    # Deterministic physics (must be 55+ pass, 0 fail)
    dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerPhysics|FullyQualifiedName~PhysicsReplayTests" --logger "console;verbosity=minimal"

    # WoWSharpClient movement (must be 100+ pass, 0 fail)
    dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementControllerTests|FullyQualifiedName~ObjectManagerWorldSessionTests|FullyQualifiedName~SplineTests" --logger "console;verbosity=minimal"

    # Live parity (must pass individually — run one at a time)
    WWOW_TEST_PRESERVE_EXISTING_PATHFINDING=1 dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"
    WWOW_TEST_PRESERVE_EXISTING_PATHFINDING=1 dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"
    WWOW_TEST_PRESERVE_EXISTING_PATHFINDING=1 dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatBgTests.Combat_BG_AutoAttacksMob_WithFgObserver" --logger "console;verbosity=minimal"
    WWOW_TEST_PRESERVE_EXISTING_PATHFINDING=1 dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer" --logger "console;verbosity=minimal"
    ```

### Phase 5: Terrain/Game Object Data Validation

15. **Verify ADT height map reading** — disassemble how WoW.exe reads ADT terrain heights. Compare against our `SceneQuery::GetGroundZ`. Ensure barycentric interpolation, triangle selection, and height clamping match.

16. **Verify WMO collision reading** — disassemble how WoW.exe handles WMO (building) collision. Compare against our WMO handling in the native DLL.

17. **Verify M2 collision** — disassemble how WoW.exe handles M2 (doodad/model) collision. Our rule: 99% of M2s have collision, NPCs do not. Verify this matches the binary's `ShouldHaveCollision` logic.

## BUILD COMMANDS

```bash
# Native C++ DLL
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal

# .NET solution
dotnet build WestworldOfWarcraft.sln --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false

# Python disassembly
py -c "
from capstone import *
def disasm(va, size=512):
    with open('D:/World of Warcraft/WoW.exe', 'rb') as f:
        f.seek(va - 0x400000)
        code = f.read(size)
    md = Cs(CS_ARCH_X86, CS_MODE_32)
    for insn in md.disasm(code, va):
        print(f'  0x{insn.address:08X}: {insn.mnemonic:8s} {insn.op_str}')
        if insn.mnemonic in ('ret', 'retn'): break
disasm(0x633840, 2048)  # Change address as needed
"

# Read float constant from binary
py -c "
import struct
with open('D:/World of Warcraft/WoW.exe', 'rb') as f:
    f.seek(0x7FF9D8 - 0x400000)  # Change address
    print(struct.unpack('<f', f.read(4))[0])
"
```

## COMMIT PROTOCOL

After each logical unit:
1. Run the relevant test suite
2. `git add <changed files>`
3. `git commit -m "description\n\nCo-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"`
4. `git push`

## PROCESS SAFETY

- NEVER blanket-kill dotnet or Game processes
- NEVER edit the MaNGOS MySQL database directly
- Only kill specific PIDs that YOUR session launched
- The MaNGOS server is ALWAYS running — live tests can run anytime
