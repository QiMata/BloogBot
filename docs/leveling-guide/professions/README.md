# Professions

> **Pass 1 placeholder.** Two primaries + three secondaries, plus per-specialization breakdowns. Pass 6.

## Planned files (pass 6)

### Primary professions
| File | Skill cap | Spec branches |
|---|---|---|
| `alchemy.md` | 300 | none |
| `blacksmithing.md` | 300 | Armorsmith @ 200, Weaponsmith @ 200 → Swordsmith / Macesmith / Axesmith @ 250+ |
| `enchanting.md` | 300 | none |
| `engineering.md` | 300 | **Goblin** or **Gnomish** at 200 (irreversible) |
| `herbalism.md` | 300 | none |
| `leatherworking.md` | 300 | Dragonscale / Elemental / Tribal at 225 |
| `mining.md` | 300 | none |
| `skinning.md` | 300 | none |
| `tailoring.md` | 300 | none (in 1.12.1 — TBC adds Mooncloth/Shadoweave/Spellfire specs) |

### Secondary professions
| File | Skill cap |
|---|---|
| `cooking.md` | 300 |
| `first-aid.md` | 300 |
| `fishing.md` | 300 |

## Standard sections per profession file (pass 6 contract)

1. **1-300 skill-up route** (recipe → recipe → recipe with mat lists)
2. **Trainer locations** per faction at each rank threshold (Apprentice 75, Journeyman 150, Expert 225, Artisan 300)
3. **Faction-restricted recipes**
4. **BoP crafted gear at 60** — flagged by class:
   - Tailoring: Truefaith Vestments, Robe of the Archmage, Mooncloth, Bloodvine
   - Blacksmithing: Lionheart Helm, Argent Avenger, Drakefire Amulet (quest, not crafted), Hammer of the Titans
   - Leatherworking: Hide of the Wild, Stormshroud, Devilsaur set
   - Engineering: Goblin Sapper Charges, Force Reactive Disk (Gnomish), Goblin Rocket Boots
   - Alchemy: transmutes (Elemental → essence), Flask cooldowns
5. **Recipe drop locations** — every recipe sourced from a dungeon / mob / quest / vendor
6. **Decision-Engine Rules**
7. **Snapshot Fields Needed**

## Account-level allocation strategy

The end-state goal requires **all primary professions maxed** at 300 on at least one alt across the account. With 2 primary slots per character × 9 classes = 18 primary slots. With 9 primaries to cover, the engine chooses pairings per character. Common pairings:

| Class | Common primary pairing | Why |
|---|---|---|
| Warrior | Mining + Blacksmithing | self-craft Lionheart Helm + repair income |
| Paladin | Mining + Blacksmithing | tank itemization + repairs |
| Hunter | Skinning + Leatherworking | self-craft Devilsaur + Stormshroud |
| Rogue | Engineering + Mining or Herbalism + Alchemy | Goblin Sapper utility / pot uptime |
| Priest | Tailoring + Enchanting | self-craft Truefaith Vestments + bag-DE economy |
| Shaman | Mining + Engineering or Herbalism + Alchemy | Goblin utility / mana potion uptime |
| Mage | Tailoring + Enchanting | Robe of the Archmage + bag-DE economy |
| Warlock | Tailoring + Enchanting | mooncloth / spellpower + DE |
| Druid | Skinning + Leatherworking | self-craft Hide of the Wild + feral / cat sets |

The engine's account planner allocates pairings to satisfy "all 9 primaries maxed across the account" with minimum redundancy.
