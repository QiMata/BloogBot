# NpcInteractionTests

Shodan-directed NPC interaction coverage for vendor, trainer, flight-master,
and NPC flag snapshot behavior.

## Bot Execution Mode

**Shodan FG+BG-action / tracked skip** - `NpcInteraction.config.json` launches
`NPCBG1` as a Background Orc Hunter action target, `NPCFG1` as a Foreground Orc
Rogue action target, and SHODAN as the Background Gnome Mage director. SHODAN
stages world/loadout state; SHODAN is never resolved as an action target.

## Test Methods

- `Vendor_VisitTask_FindsAndInteracts`: stages FG/BG at the Razor Hill vendor
  location, detects a vendor-flagged NPC, and dispatches `ActionType.VisitVendor`.
- `FlightMaster_VisitTask_DiscoversPaths`: stages FG/BG at the Orgrimmar flight
  master, detects the flight-master flag, and dispatches
  `ActionType.VisitFlightMaster`.
- `ObjectManager_DetectsNpcFlags`: stages FG/BG near Razor Hill NPCs and asserts
  snapshot `NearbyUnits` include non-zero `NpcFlags`.
- `Trainer_LearnAvailableSpells`: Shodan-shaped hunter trainer path, currently
  skipped because the live environment cannot fund the hunter reliably.

## Shodan Staging

The test body calls only fixture helpers and BotRunner action dispatches. The
fixture owns:

- `AssertConfiguredCharactersMatchAsync(...)` for the `NpcInteraction.config.json`
  roster.
- `ResolveBotRunnerActionTargets(...)` so only FG/BG BotRunner accounts receive
  actions.
- `StageBotRunnerAtRazorHillVendorAsync(...)` for vendor/NPC flag checks.
- `StageBotRunnerAtOrgrimmarFlightMasterAsync(...)` for taxi discovery.
- `StageBotRunnerAtRazorHillHunterTrainerAsync(...)`,
  `StageBotRunnerLoadoutAsync(...)`, and `StageBotRunnerSpellAbsentAsync(...)`
  for the skipped trainer path.

## Runtime Linkage

- `ActionType.VisitVendor` -> `CharacterAction.VisitVendor` -> `VendorVisitTask`
- `ActionType.VisitTrainer` -> `CharacterAction.VisitTrainer` -> `TrainerVisitTask`
- `ActionType.VisitFlightMaster` -> `CharacterAction.VisitFlightMaster` ->
  `FlightMasterVisitTask`
- NPC visibility flows through `BotRunnerService.Snapshot` via `NearbyUnits` and
  `NpcFlags`.

## Current Functional Gap

`Trainer_LearnAvailableSpells` is skipped after migration because trainer
learning needs starter copper and both live funding paths are blocked in this
environment:

- In-client `.modify money` dispatches through the BotRunner account but is
  unavailable/no-op for the target.
- SOAP `.send money` can create `Trainer Gold` mail, but collecting it requires
  mailbox staging and `CheckMail`; the pre-skip live run failed before
  collection with `[SHODAN-STAGE] BG mailbox staging failed` while strict
  Orgrimmar mailbox staging could not enable GM mode.

This is documented as a runtime staging/funding gap, not a reason to leave the
file in SHODAN-CANDIDATE.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with existing warnings.
- Safety bundle -> passed `33/33`.
- Dispatch readiness bundle -> passed `60/60`.
- `npc_interaction_shodan.trx` -> passed `3`, skipped `1`.
- Pre-skip diagnostic artifact: `npc_interaction_shodan_final.trx` -> failed
  `Trainer_LearnAvailableSpells` at mailbox staging.
