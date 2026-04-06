# Master Tasks — Test & Validate Everything

## Rules
1. **Use ONE continuous session.** Auto-compaction handles context limits.
2. Execute tasks in priority order.
3. Move completed items to `docs/ARCHIVE.md`.
4. **The MaNGOS server is ALWAYS live** (Docker).
5. **WoW.exe binary parity is THE rule** for physics/movement.
6. **GM Mode OFF after setup** — `.gm on` corrupts UnitReaction bits. Always `.gm off` before test actions.
7. **Kill WoW.exe before building.** DLL injection locks output files.
8. **Previous phases archived** — see `docs/ARCHIVE.md`.

---

## Test Baseline (2026-04-06 — R4 SceneData fix)

| Suite | Passed | Failed | Skipped | Notes |
|-------|--------|--------|---------|-------|
| WoWSharpClient.Tests | 1425 | 0 | 1 | +8 SceneDataClient integration tests |
| Navigation.Physics.Tests | 666 | 2 | 1 | **Confirmed** — 2 pre-existing elevator |
| BotRunner.Tests (unit) | 1626 | 0 | 4 | **Confirmed** |
| BotRunner.Tests (LiveValidation) | TBD | TBD | TBD | Pending rerun with SceneData fix |

---

## R1/R2/R3 — Archived (see docs/ARCHIVE.md)

---

## R4 — SceneDataService Physics Fix (Priority: CRITICAL)

**Root cause:** Bots float in air after teleport because `ConfigureNativeSceneMode()` was a no-op — it never called `SetSceneSliceMode(true)`. Without this, Navigation.dll doesn't use injected scene triangles and has no collision geometry.

| # | Task | Spec |
|---|------|------|
| 4.1 | **Enable SetSceneSliceMode(true)** when SceneDataClient present. | **Done** (8de77f7c) |
| 4.2 | **Defer native call** — SetSceneSliceMode sets managed flag immediately, defers DLL call to avoid BadImageFormatException during x86 construction. | **Done** (b2e5c53d) |
| 4.3 | **x86/x64 DLL resolution** — Default path has x86, x64/ subdirectory has x64. NavigationInterop updated. | **Done** (b2e5c53d) |
| 4.4 | **SceneDataClient integration tests** — 12 tests: grid quantization, retry/dedup, response packing, live connectivity. | **Done** (50812ea7) |
| 4.5 | **LiveValidation rerun** — Verify bots fall properly after teleport with SceneData fix. | Open |
| 4.6 | **AV test** — Bots fall, form group, enter Alterac Valley. | Open |

---

## R3 — LiveValidation Deep Dive (Priority: High) — **COMPLETE**

**Goal:** For each LiveValidation test category, verify it exercises real game behavior, not just snapshot != null.

| # | Task | Verdict |
|---|------|---------|
| 3.1 | **BasicLoopTests** — Login test is trivial (snapshot check). Physics Z test is **real** (verifies no underground fall). | **Done** — Mixed |
| 3.2 | **VendorBuySellTests** — **Real behavior**: verifies inventory delta + coinage delta via snapshot polling. | **Done** — Real |
| 3.3 | **CombatTests** — **Real behavior**: mob health regression over time, `firstDamageConfirmed` gate, kill confirmation. GM flag check before combat. | **Done** — Real |
| 3.4 | **NavigationTests** — **Real behavior**: end-to-end pathfinding, position polling, arrival within 12yd tolerance. | **Done** — Real |
| 3.5 | **EconomyTests** — Mixed: Bank/AH interaction tests are `Assert.NotNull` placeholders (10 tests). Mail test verifies coinage delta. | **Done** — Needs work |
| 3.6 | **GroupFormationTests** — **Real behavior**: PartyLeaderGuid transitions (0→non-zero→0), invite/accept/disband verified. | **Done** — Real |
| 3.7 | **RFC DungeonRun** — **Real behavior**: DungeonEntryTestRunner verifies bot count, group formation, MapId transition to instance map. | **Done** — Real |

**Summary:** ~37 tests exercise real game behavior; ~10 are trivial `Assert.NotNull` placeholders (concentrated in Bank/AH parity tests). The core test categories (combat, navigation, vendor, groups, dungeons, fishing, gathering) are **substantive**.

**Placeholder tests to flesh out later:**
- BankInteractionTests (2 tests): Add deposit/withdraw assertions
- AuctionHouseTests (2 tests): Add AH frame state assertions
- AuctionHouseParityTests (3 tests): Add search/post/cancel parity assertions
- BankParityTests (2 tests): Add deposit/withdraw parity assertions
- RaidCoordinationTests.MarkTargeting (1 test): Add mark assertion

---

## Canonical Commands

```bash
# Kill everything before building
taskkill //F //IM WoW.exe 2>/dev/null
taskkill //F //IM BackgroundBotRunner.exe 2>/dev/null
taskkill //F //IM WoWStateManager.exe 2>/dev/null
taskkill //F //IM testhost.x86.exe 2>/dev/null

# Build .NET + C++ (both architectures)
dotnet build WestworldOfWarcraft.sln --configuration Release
MSBUILD="C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSBUILD" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145
"$MSBUILD" Exports/Physics/Physics.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145
"$MSBUILD" Exports/Physics/Physics.vcxproj -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145

# Tests
dotnet test Tests/WoWSharpClient.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure" --no-build
dotnet test Tests/Navigation.Physics.Tests/ --configuration Release --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "Category!=RequiresInfrastructure&FullyQualifiedName!~LiveValidation" --no-build
dotnet test Tests/BotRunner.Tests/ --configuration Release --filter "FullyQualifiedName~LiveValidation" --no-build --blame-hang --blame-hang-timeout 10m
```
