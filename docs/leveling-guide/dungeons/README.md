# Dungeons

> **Pass 1 placeholder.** One file per 5-man dungeon. Pass 4.

## Planned files (pass 4)

| File | Level range | Faction natural | Key required |
|---|---|---|---|
| `ragefire-chasm.md` | 13-18 | Horde (Orgrimmar interior) | none |
| `wailing-caverns.md` | 15-25 | both | none |
| `deadmines.md` | 17-26 | Alliance | none |
| `shadowfang-keep.md` | 22-30 | Horde | none |
| `stockades.md` | 24-32 | Alliance | none |
| `blackfathom-deeps.md` | 24-32 | both | none |
| `gnomeregan.md` | 29-38 | Alliance | Workshop Key (for workshop wing) |
| `razorfen-kraul.md` | 30-40 | Horde | none |
| `scarlet-monastery-graveyard.md` | 28-38 | both | none |
| `scarlet-monastery-library.md` | 29-39 | both | none |
| `scarlet-monastery-armory.md` | 32-42 | both | none |
| `scarlet-monastery-cathedral.md` | 35-45 | both | none |
| `razorfen-downs.md` | 37-46 | both | none |
| `uldaman.md` | 41-51 | both | Reliquary of Purity (sub-quest gate; not a hard key) |
| `zul-farrak.md` | 44-54 | both | Mallet of Zul'Farrak (event); Sacred Mallet for full pyramid |
| `maraudon.md` | 45-55 | both | Scepter of Celebras (quest reward, optional but key for inner) |
| `sunken-temple.md` | 50-60 | both | Crescent Key |
| `blackrock-depths.md` | 52-60 | both | Shadowforge Key (full clears) |
| `lower-blackrock-spire.md` | 55-60 | both | Master's Key (BRM gate to LBRS) |
| `upper-blackrock-spire.md` | 58-63 | both | Seal of Ascension OR 5-player group (ritual at door) |
| `stratholme-live.md` | 58-63 | both | none (entrance from outside city) |
| `stratholme-undead.md` | 58-63 | both | Key to the City (quest) for the side-gate, otherwise via Live |
| `scholomance.md` | 58-63 | both | Skeleton Key |
| `dire-maul-north.md` | 58-62 | both | Crescent Key for Warpwood (West) door — N is open; tribute-event ring optional but advised |
| `dire-maul-west.md` | 58-62 | both | Crescent Key |
| `dire-maul-east.md` | 58-62 | both | Crescent Key |

## Standard sections per dungeon file (pass 4 contract)

1. **Entrance location** (zone, coords if available, summon stone position)
2. **Key/attunement requirements**
3. **Recommended group composition** (tank/healer/dps mix; class hard requirements)
4. **Boss list with mechanics** (per-boss table)
5. **Notable loot per boss** (BiS-relevance flagged)
6. **Quest list** (in-dungeon quests + leadup quests from major hubs)
7. **Tribute / event mechanics** (DM N tribute, Strat 45-min Baron, ZF pyramid, BFD altar)
8. **Map / route** (recommended pull order)
9. **Decision-Engine Rules**
10. **Snapshot Fields Needed**

## VMaNGOS implementation notes (called out in each file)

VMaNGOS scripts most 5-mans well, but some boss mechanics differ from retail 1.12. Each dungeon file ends with a `## VMaNGOS Notes` section flagging known divergences (e.g., DM tribute book despawn timing, Postmaster trigger reliability, Baron 45-min start condition).
