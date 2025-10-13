# Recorded Test Scenarios

The automated recorded test suite currently exercises the following fully scripted workflows.
Each scenario documents the exact preparation, execution, and cleanup steps the foreground
(Game Master) and background (adventurer) runners perform during orchestration.

## Northshire Valley human intro quest chain
**Goal:** Showcase the level 1 Human Mage introductory quest sequence from Northshire Abbey
through the first set of kobold clearing objectives.

### Foreground (GM) preparation
1. Reset the Human Mage test character to level 1, clear quest log, and hearthstone to the Northshire Abbey spawn point.
2. Summon Marshal McBride and the introductory quest givers inside the abbey if they are missing.
3. Restock the character with starting reagents and hearthstone charges needed for the tutorial quests.

### Background (Adventurer) execution
1. Accept "A Threat Within" and "Kobold Camp Cleanup" from Marshal McBride and Sergeant Willem.
2. Clear the required Kobold Workers around the vineyard using the Frost Mage combat script.
3. Return to Marshal McBride and Sergeant Willem to turn in the quests and collect rewards.

### Foreground cleanup
1. Wipe quest progress, reset experience, and remove temporary items granted for the run.
2. Teleport the character back to the Northshire Abbey spawn pad ready for the next recording.

## Elwynn Forest Hogger elite takedown
**Goal:** Demonstrate coordinating the "Wanted: Hogger" elite encounter with crowd control support.

### Foreground (GM) preparation
1. Stage the encounter at the Eastvale Logging Camp by clearing ambient mobs and phasing the camp.
2. Summon Hogger, two Hogger Lieutenants, and place Alliance guard NPC reinforcements.
3. Provide the background runner with the "Wanted: Hogger" quest and capture traps.

### Background (Adventurer) execution
1. Ride from Goldshire to the Hogger camp, clearing the lieutenant waves and positioning guards.
2. Engage Hogger, use the provided traps to capture him, and secure the quest item from the crate.

### Foreground cleanup
1. Despawn Hogger, the lieutenants, and the temporary guard NPCs.
2. Reset the quest flag on the test character and restock the capture trap inventory.

## Westfall Deadmines attunement prep
**Goal:** Prepare and record the Defias Brotherhood attunement steps culminating in the Deadmines key handoff.

### Foreground (GM) preparation
1. Teleport the party to Sentinel Hill, reset mob spawns around Moonbrook, and clear phasing blockers.
2. Grant the "The Defias Brotherhood" quest line stages and provide the instance key components.
3. Summon a practice Defias Overseer encounter outside the Deadmines entrance for the scripted warm-up.

### Background (Adventurer) execution
1. Escort the contact from Sentinel Hill to Moonbrook while defending against Defias ambushers.
2. Enter the Deadmines ante-room, assemble the key from components, and unlock the door while the GM records.

### Foreground cleanup
1. Reset the Deadmines instance state, despawn the practice Overseer encounter, and clean up phasing.
2. Remove temporary key components and return the character to Sentinel Hill for repeatability.
