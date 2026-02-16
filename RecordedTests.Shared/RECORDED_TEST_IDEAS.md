# Recorded Test Scenarios

The automated recorded test suite currently exercises the following fully scripted workflows.
Each scenario documents the exact preparation, execution, and cleanup steps the foreground
(Game Master) and background (adventurer) runners perform during orchestration.

## Northshire Valley human intro quest chain
**Goal:** Showcase the level 1 Human Mage introductory quest sequence from Northshire Abbey
through the first set of kobold clearing objectives.

### Foreground (GM) preparation
1. Reset the Human Mage test character to level 1, clear quest log, and hearthstone to the Northshire Abbey spawn point.
   - `.character level <playerName> 1`
   - `.quest remove <questId> <playerName>` for each active starter quest
   - `.tele name <playerName> northshireabbey` or `.appear <playerName>` followed by `.modify bind <mapId> <x> <y> <z>` to set hearthstone
2. Summon Marshal McBride and the introductory quest givers inside the abbey if they are missing.
   - `.npc add <marshalMcBrideNpcId>`
   - `.npc add <sergeantWillemNpcId>`
   - `.npc add <additionalQuestGiverNpcId>` for any missing NPCs
3. Restock the character with starting reagents and hearthstone charges needed for the tutorial quests.
   - `.additem <hearthstoneItemId> <count>`
   - `.additem <starterReagentItemId> <count>`

### Background (Adventurer) execution
1. Accept "A Threat Within" and "Kobold Camp Cleanup" from Marshal McBride and Sergeant Willem.
   - `.quest add <aThreatWithinQuestId> <playerName>`
   - `.quest add <koboldCampCleanupQuestId> <playerName>`
2. Clear the required Kobold Workers around the vineyard using the Frost Mage combat script.
   - `.npc add <koboldWorkerNpcId>` to respawn missing workers if needed
   - `.npc follow stop` or `.npc move` as needed to position workers for recording
3. Return to Marshal McBride and Sergeant Willem to turn in the quests and collect rewards.
   - `.quest complete <aThreatWithinQuestId> <playerName>`
   - `.quest complete <koboldCampCleanupQuestId> <playerName>`

### Foreground cleanup
1. Wipe quest progress, reset experience, and remove temporary items granted for the run.
   - `.quest remove <aThreatWithinQuestId> <playerName>`
   - `.quest remove <koboldCampCleanupQuestId> <playerName>`
   - `.character level <playerName> 1`
   - `.removeitem <starterReagentItemId> <count>`
2. Teleport the character back to the Northshire Abbey spawn pad ready for the next recording.
   - `.tele name <playerName> northshireabbey`

## Elwynn Forest Hogger elite takedown
**Goal:** Demonstrate coordinating the "Wanted: Hogger" elite encounter with crowd control support.

### Foreground (GM) preparation
1. Stage the encounter at the Eastvale Logging Camp by clearing ambient mobs and phasing the camp.
   - `.gobject near` followed by `.gobject delete` for unwanted objects
   - `.npc info` to capture GUIDs and `.npc delete <guid>` for stray mobs
   - `.phase set <phaseId> <playerName>` to enforce the correct encounter phase
2. Summon Hogger, two Hogger Lieutenants, and place Alliance guard NPC reinforcements.
   - `.npc add <hoggerNpcId>` at encounter origin
   - `.npc add <hoggerLieutenantNpcId>` repeated twice for lieutenants
   - `.npc add <allianceGuardNpcId> <count>` positioned with `.npc move`
3. Provide the background runner with the "Wanted: Hogger" quest and capture traps.
   - `.quest add <wantedHoggerQuestId> <playerName>`
   - `.additem <captureTrapItemId> <count>`

### Background (Adventurer) execution
1. Ride from Goldshire to the Hogger camp, clearing the lieutenant waves and positioning guards.
   - `.summon <playerName>` to bring the adventurer to Goldshire start point if needed
   - `.wp add` or `.npc follow` to script guard escort paths
2. Engage Hogger, use the provided traps to capture him, and secure the quest item from the crate.
   - `.cast <trapSpellId> <targetGuid>` triggered via scripted macro or GM command
   - `.gobject add <questCrateGameObjectId>` to spawn the lootable crate if missing

### Foreground cleanup
1. Despawn Hogger, the lieutenants, and the temporary guard NPCs.
   - `.npc delete <hoggerNpcGuid>`
   - `.npc delete <hoggerLieutenantNpcGuid>` for each lieutenant
   - `.npc delete <allianceGuardNpcGuid>` for reinforcements
2. Reset the quest flag on the test character and restock the capture trap inventory.
   - `.quest remove <wantedHoggerQuestId> <playerName>`
   - `.removeitem <captureTrapItemId> <count>`

## Westfall Deadmines attunement prep
**Goal:** Prepare and record the Defias Brotherhood attunement steps culminating in the Deadmines key handoff.

### Foreground (GM) preparation
1. Teleport the party to Sentinel Hill, reset mob spawns around Moonbrook, and clear phasing blockers.
   - `.tele name <playerName> sentinelhill`
   - `.respawn` or `.reload all_creature` to refresh ambient spawns
   - `.phase set <phaseId> <playerName>` to match intended quest phase
2. Grant the "The Defias Brotherhood" quest line stages and provide the instance key components.
   - `.quest add <defiasBrotherhoodQuestIdStage1> <playerName>` through `.quest add <defiasBrotherhoodQuestIdStageN> <playerName>`
   - `.additem <deadminesKeyComponentItemId> <count>` for each component
3. Summon a practice Defias Overseer encounter outside the Deadmines entrance for the scripted warm-up.
   - `.npc add <defiasOverseerNpcId>` with `.npc move` to position the encounter

### Background (Adventurer) execution
1. Escort the contact from Sentinel Hill to Moonbrook while defending against Defias ambushers.
   - `.npc add <escortContactNpcId>` and `.npc follow <escortContactNpcGuid> <playerName>` to start escort
   - `.npc add <defiasAmbusherNpcId>` at each ambush waypoint
2. Enter the Deadmines ante-room, assemble the key from components, and unlock the door while the GM records.
   - `.gobject add <deadminesDoorGameObjectId>` if door needs respawn
   - `.cast <keyAssemblySpellId> <playerName>` or `.item add` macro to simulate assembly if scripting required

### Foreground cleanup
1. Reset the Deadmines instance state, despawn the practice Overseer encounter, and clean up phasing.
   - `.instance reset deadmines`
   - `.npc delete <defiasOverseerNpcGuid>`
   - `.phase reset <playerName>`
2. Remove temporary key components and return the character to Sentinel Hill for repeatability.
   - `.removeitem <deadminesKeyComponentItemId> <count>`
   - `.tele name <playerName> sentinelhill`
