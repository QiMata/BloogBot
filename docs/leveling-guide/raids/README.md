# Raids

> **Pass 1 placeholder.** One file per 1.12.1-era raid. Pass 5.

## Planned files (pass 5)

### Standard raids
| File | Size | Min level | Attunement | VMaNGOS-completeness |
|---|---|---|---|---|
| `molten-core.md` | 40 | 60 | UBRS Drakkisath head → Lothos Riftwaker (or Marshal NPC chain) | high |
| `onyxias-lair.md` | 40 | 60 | Marshal Windsor (A) / Eitrigg-Rexxar (H) chain | high |
| `blackwing-lair.md` | 40 | 60 | Vaelastrasz orb in BRS upper (UBRS prereq) | medium-high |
| `zul-gurub.md` | 20 | 60 | none (entrance in Mudsprocket / Stranglethorn) | high |
| `ruins-of-ahn-qiraj.md` | 20 | 60 | none post-event | medium-high |
| `temple-of-ahn-qiraj.md` | 40 | 60 | none post-event; **Scepter of the Shifting Sands** for server-first | medium (C'Thun is famously buggy on private servers) |
| `naxxramas.md` | 40 | 60 | Argent Dawn revered + 60g + crafted Eye of Shadow | low-medium (frequently incomplete) |

### World bosses
| File | Zone | Spawn pattern |
|---|---|---|
| `world-boss-azuregos.md` | Azshara | rare timer |
| `world-boss-lord-kazzak.md` | Blasted Lands | rare timer |
| `world-boss-dragons-of-nightmare.md` | Duskwood / Hinterlands / Feralas / Ashenvale (Ysondre / Lethon / Emeriss / Taerar) | rotating spawn |
| `world-boss-highlord-kruul.md` | pre-AQ event spawn (Stormwind / Orgrimmar gates) | event-tied |

## Standard sections per raid file (pass 5 contract)

1. **Attunement chain** (link to [attunements/](../attunements/))
2. **Full boss list** with mechanics, in encounter order
3. **Loot tables** — notable BiS items per boss
4. **Tier set source bosses** — which bosses drop tokens for the raid's Tier set
5. **Materials drops** (Bindings of the Windseeker, Eye of Sulfuras, Onyxia Hide Backpack, Eye of C'Thun, etc.)
6. **Resistance gear requirements** (Fire MC/BWL Ragnaros/Vael, Frost AQ40 Princess, Nature AQ40 Huhuran, Shadow Naxx Loatheb)
7. **Recommended raid composition**
8. **Trash farming notes** (Hydraxian douses, AD insignia farms, etc.)
9. **VMaNGOS implementation status** (known boss-script gaps)
10. **Decision-Engine Rules**
11. **Snapshot Fields Needed**

## Decision-engine note: server capabilities

For raids with substantial known VMaNGOS divergence (Naxx, AQ40, parts of BWL), the engine should consult a `serverCapabilities` config flag before recommending the raid action — see [sections/00-questions-and-answers.md Q2](../sections/00-questions-and-answers.md#q2--how-does-the-engine-handle-vmangos-vs-retail-1121-divergence-on-raid-content).
