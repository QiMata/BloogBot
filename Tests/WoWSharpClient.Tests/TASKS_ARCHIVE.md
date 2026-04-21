# Task Archive

Completed items moved from TASKS.md.

## Completed 2026-04-15

### WSC-TST-PAR-001: Add BG server-packet trigger coverage to full movement parity
- Added `ObjectManagerWorldSessionTests.MoveKnockBack_ServerPacketFeedsMovementControllerNextFrame`, proving `SMSG_MOVE_KNOCK_BACK` flows through `MovementHandler`, mutates/ACKs in `WoWSharpObjectManager`, and feeds pending knockback velocity into `MovementController.Update()`.
- Tagged force speed, force root, server movement flag toggles, compressed movement trigger variants, knockback ACK coverage, and controller pending-knockback coverage with `Category=MovementParity`.
- Repaired the singleton handler test seam by wiring `ObjectManagerFixture` and `ResetObjectManager()` to `WoWSharpEventEmitter.Instance`.
- Fixed the controller pending-knockback test to construct the controller with the object manager that owns pending state.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ObjectManagerWorldSessionTests.MoveKnockBack|FullyQualifiedName~ObjectManagerWorldSessionTests.ServerControlledMovementFlagChanges_ParseApplyAndAck|FullyQualifiedName~MovementControllerTests.PendingKnockback_OverridesDirectionalInputAndFeedsPhysicsVelocity" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (29/29)`

### WSC-TST-PAR-002: Add stop/use ordering and gather retry signal coverage
- Added `ObjectManagerWorldSessionTests.ForceStopImmediate_BlocksStopPacketBeforeGameObjectUse`, proving `MSG_MOVE_STOP` is sent before `CMSG_GAMEOBJ_USE` when the BG object-manager path forces an immediate stop before interaction.
- Added `SpellHandlerTests.HandleCastFailed_TryAgainReason_FiresNamedErrorMessage`, pinning server reason `0x7A` to `TRY_AGAIN`.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "Category=MovementParity" --logger "console;verbosity=minimal"` -> `passed (30/30)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellHandlerTests.HandleCastFailed" --logger "console;verbosity=minimal"` -> `passed (3/3)`

## Completed 2026-02-28

### WSC-TST-001: Replace TODO-only object update replay test with deterministic assertions
- Replaced `ShouldDecompressAndParseAllCompressedUpdateObjectPackets` with two focused tests:
  - `DecompressAndParse_CreatesObjectsWithNonZeroGuids`: verifies object creation, count stability, and GUID validity.
  - `DecompressAndParse_CreatesGameObjectsWithValidDisplayIds`: verifies typed WoWGameObject creation with valid DisplayIds.
- Added `FullSessionReplay_GuidInvariant_NoDuplicatesWithinSingleReplay` to SessionTimelineReplayTests for GUID invariant coverage.
- File: `Tests/WoWSharpClient.Tests/Handlers/SMSG_UPDATE_OBJECT_Tests.cs`.

### WSC-TST-002: Replace TODO-only opcode dispatch replay test with observable assertions
- Rewrote `ShouldHandleOpcodePackets` Theory to use `handlerType` for category-specific postconditions:
  - `spellInitial`: asserts Spells collection is populated after SMSG_INITIAL_SPELLS dispatch.
  - `login`: asserts OnLoginVerifyWorld event fires after SMSG_LOGIN_VERIFY_WORLD dispatch.
  - `worldState`: asserts OnWorldStatesInit event fires after SMSG_INIT_WORLD_STATES dispatch.
  - All categories: verify no-throw as baseline (real captured packet dispatch).
- Added `Dispatch_UnregisteredOpcode_DoesNotThrow` and `Dispatch_EmptyPayload_DoesNotCrashRunner` edge-case tests.
- File: `Tests/WoWSharpClient.Tests/Handlers/OpcodeHandler_Tests.cs`.

### WSC-TST-003: Remove sleep-based flakiness from object replay tests
- Created `UpdateProcessingHelper.DrainPendingUpdates()` utility that polls ObjectManager.Objects count for stability rather than using fixed Thread.Sleep.
- Replaced `Thread.Sleep(100)` in `SMSG_UPDATE_OBJECT_Tests` and `Thread.Sleep(200)` in `SessionTimelineReplayTests.ReplayAndProcess` with `UpdateProcessingHelper.DrainPendingUpdates()`.
- Helper uses 3 consecutive stable-count checks at 50ms intervals before declaring processing complete, with a 5-second safety timeout.
- Files: `Tests/WoWSharpClient.Tests/Util/UpdateProcessingHelper.cs` (new), `Tests/WoWSharpClient.Tests/Handlers/SMSG_UPDATE_OBJECT_Tests.cs`.

### WSC-TST-004: Add regression tests mapped to WSC-MISS-* implementation backlog
- Created `Tests/WoWSharpClient.Tests/Handlers/RegressionTests.cs` with three test classes:
  - `DismissBuffCancelAuraTests` (6 tests): DismissBuff return values, case sensitivity, HasBuff/HasDebuff checks, GetBuffs/GetDebuffs spell effect flow (WoWUnit.cs:268-280).
  - `PlayerFieldMappingTests` (7 tests): All player fields from WoWSharpObjectManager.cs:2000-2049 have correct defaults, KnownTitles low/high combination, CombatRating/SpellCritPercentage/DailyQuests indexing, PvP fields, float fields (ModManaRegen, OffhandCritPercentage).
  - `UnitGeometryTests` (3 tests): GetPointBehindUnit geometry calculation, Z preservation, zero-distance identity.
- File: `Tests/WoWSharpClient.Tests/Handlers/RegressionTests.cs` (new).
