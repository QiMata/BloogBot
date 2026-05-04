# Attunements / Keys

> **Pass 1 placeholder.** Every key + raid attunement chain at 60. Pass 9.

## Planned files (pass 9)

### Raid attunements
| File | Raid gated | Prereqs |
|---|---|---|
| `molten-core.md` | MC | UBRS Drakkisath head → Lothos Riftwaker turn-in (alt: Hand of the Marshal NPC chain) |
| `onyxia-alliance.md` | Ony (A) | Marshal Windsor BRD prison rescue → UBRS Drakkisath head → SW masquerade event → "Stormwind Rendezvous" final |
| `onyxia-horde.md` | Ony (H) | Eitrigg's wisdom + Rexxar (Dustwallow Marsh) + UBRS Warchief's Mandate → "Warlord's Command" |
| `blackwing-lair.md` | BWL | Vaelastrasz orb in BRS upper (must reach UBRS, Vaelan-side door) |
| `naxxramas.md` | Naxx | AD **Revered** + 60g + crafted **Eye of Shadow** (rare drop in EPL/WPL) |

### 5-man / open-area keys
| File | Unlocks | Source |
|---|---|---|
| `seal-of-ascension.md` | UBRS direct entrance (otherwise 5-player ritual at door) | quest chain in Burning Steppes (Vaelan) → Blackrock Spire UBRS area |
| `shadowforge-key.md` | BRD beyond first wing | quest chain Searing Gorge (Bael'Gar / Doomforge Artificer) → BRD inner |
| `crescent-key.md` | Sunken Temple, Dire Maul (all wings) | quest chain Tabetha (Dustwallow) + Marvon Rivetseeker (Tanaris) → key crafted via Yor (Hinterlands chain "The Atal'ai Exile") |
| `master-key.md` | Blackrock Mountain orb (between LBRS / UBRS / MC) | quest in BRD lower city; uses Brann Bronzebeard chain |
| `workshop-key.md` | Gnomeregan workshop wing | quest in Tinker Town (Ironforge) / Steelgrill's Depot |
| `skeleton-key.md` | Scholomance | quest "Sacred Hammer of Light" → "Torch of Holy Flame" → cured with Light's Hope chapel quest set; uses Sk'shgn (mid-bracket) |
| `key-to-the-city.md` | Stratholme UD-side gate | EPL quest chain (the Argent Dawn / Mograine side) |

### Other notable keys (often forgotten)
| File | Unlocks |
|---|---|
| `prison-cell-key.md` | BRD prison cells (for Marshal Windsor, Onyxia attune) — drops off Hate'rel / Wrath'erel / Anger'rel inside BRD |
| `relic-coffer-keys.md` | BRD vault (12 keys; opens vault for Banner of Provocation, Battlechicken, Ribbly's Crank) |

## Standard sections per attunement / key file (pass 9 contract)

1. **What it unlocks** (raid / dungeon / area)
2. **Full quest chain** step-by-step:
   - NPCs (name, zone, coordinates)
   - Quest item turn-ins
   - Mob kills required
   - Group requirements (some chains need a 5-man dungeon kill)
3. **Prerequisite quests** (quests that gate the chain itself)
4. **Time investment** (estimated /played hours from start to completion)
5. **Per-faction differences** (Ony Alliance vs Horde is the most divergent example)
6. **Decision-Engine Rules** — typically priority **800-820** at lvl 60
7. **Snapshot Fields Needed**

## Cross-cutting decision-engine note

Attunement chains are **multi-instance, multi-zone, multi-week** efforts. The engine should:
1. Plan the **shortest chain that satisfies the most attunements simultaneously** (UBRS Drakkisath head feeds both MC and Onyxia-Alliance).
2. Bundle attune steps with already-planned dungeon runs (running UBRS for tier 0 anyway → fold the Drakkisath turn-in into that run).
3. Surface "missing key" diagnostic when a planned dungeon run lacks a required key — emit a decision request before queueing the dungeon.

See [decision-engine/unlock-graph.md](../decision-engine/unlock-graph.md) for the full prerequisite graph.
