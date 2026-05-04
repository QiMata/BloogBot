# Onyxia's Lair — 40-Man Raid Guide

> **Sources** (crawl date 2026-05-01):
> - https://boosting-ground.com/wow-classic/guides/raid-guides/onyxias-lair-guide
> - https://www.warcrafttavern.com/wow-classic/guides/onyxias-lair/
> - https://wowwiki-archive.fandom.com/wiki/Onyxia's_Lair_loot_(original) (referenced via search)
> - https://wowpedia.fandom.com/wiki/Onyxia's_Lair_loot_(Classic) (referenced via search; direct fetch returned 403)
>
> **Pass 2.** Some details (BoP class-specific epic listings, exact lockout duration 5 vs 7 days) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. The 5-day raid lockout is 1.12.1-canonical (changed to 7 days in TBC).

## Identity

**Onyxia's Lair is a single-boss 40-man raid** — the entire instance is Onyxia in her cave under Dustwallow Marsh. The raid is **the second raid players unlock** after MC and is **shorter than every other 40-man** (typical clear: 10-20 minutes once geared). Drops are **all T2 helmets** (the iconic vanilla helm tier) plus the **Onyxia Head** (triggers the **Rallying Cry of the Dragonslayer** 2-hour world buff via Stormwind/Orgrimmar turn-in).

| Feature | Value |
|---|---|
| Raid size | 40-player |
| Level required | 60 |
| Attunement | [Drakefire Amulet](../attunements/onyxia-alliance.md) (Alliance) / [same](../attunements/onyxia-horde.md) (Horde) |
| Lockout | **5 days** (changed to 7 in TBC) |
| Full clear time | 10-20 minutes (single boss) |
| Boss count | 1 (just Onyxia) |
| Location | Onyxia's Lair, Dustwallow Marsh (entrance ~52, 75) |

## Important version-correction — T2 helms, not chests

A common confusion in vanilla guides: **Onyxia drops T2 HELMS** (one per class, 9 classes, RNG per kill). She does NOT drop T2 chests — that's Nefarian in BWL. **Engine class files written prior to this iteration that mention "Onyxia chest" should be corrected** in pass 3 to "Onyxia helm + Nefarian chest" for the T2 set distribution.

| Class | T2 helm name (Onyxia drop) |
|---|---|
| Warrior | **Helm of Wrath** |
| Paladin | **Judgement Crown** |
| Hunter | **Dragonstalker's Helm** |
| Rogue | **Bloodfang Hood** |
| Priest | **Halo of Transcendence** |
| Shaman | **Helm of Ten Storms** |
| Mage | **Crown of Netherwind** |
| Warlock | **Nemesis Skullcap** |
| Druid | **Stormrage Cover** |

`[verify pass 3 — exact T2 helm names; some sources use slightly different naming]`

**Per-kill drop count**: ~2 T2 helms per kill (random class). With 9-class roster + 5-day lockout, full T2 helm coverage takes 5-10 weeks of guild farming.

## Fight phases — 3-phase encounter

### Phase 1 (100% → 65%): Ground

| Mechanic | Detail |
|---|---|
| Tank positioning | Tank holds Onyxia at one end of the room, **head facing away from raid** (Flame Breath cone forward) and **tail away from raid** (Tail Sweep knockback) |
| Cleave | ~3000 damage frontal-cone every ~10s on tank — tank prioritizes mitigation gear |
| Tail Sweep | Knockback + ~2000 damage; tank's job to ensure tail clear of raid |
| Knock Away | Aggro reset; tank uses Taunt to recover |
| Flame Breath | Frontal cone fire AoE on facing direction |
| Raid position | Stack on Onyxia's flanks (sides), away from head/tail |

**Engine rule**: Phase 1 is straightforward melee burn. Tank facing requires precise positioning — engine plans tank position relative to raid stack.

### Phase 2 (65% → 40%): Air phase

| Mechanic | Detail |
|---|---|
| Onyxia takes flight | Hovers in center of room |
| **Whelp adds spawn** | Onyxian Whelps emerge from caves on west and east walls. ~20-30 whelps total over the air phase. Each whelp ~lvl 60 elite, low HP. |
| **Deep Breath** | Onyxia's signature: **massive fire AoE down the center of the room** every ~30s. Players must move to **left or right wall** to avoid (one-shots ~80% of raid if hit). Telegraphed by Onyxia's flight pattern. |
| Fireball / Burning Adrenaline | Random raid member gets BA debuff: 25% damage taken increase + ~30s self-detonation if not cured | dispels by Mage Decurse / Druid |
| Air phase exit | At ~40% HP, Onyxia returns to ground |

**Engine rule**: Whelps are AoE-burned by Mages (Blizzard/Arcane Explosion) and Hunters (Volley) on the cave-walls. Deep Breath alert: engine warns players via raid-positioning system to clear the center.

### Phase 3 (40% → 0%): Ground (final burn)

| Mechanic | Detail |
|---|---|
| Tank position | **Tank moves to opposite end of room** — Onyxia's flight resets her direction; tank picks her up at far wall |
| **Bellowing Roar** | **Raid-wide 4-second AoE fear** every ~30s. Mitigation: Tremor Totem (Horde Shaman) / Fear Ward (Dwarf Priest) on tank, plus consumable Free Action Potions for ranged DPS |
| Continues Whelp spawns | Adds keep coming from caves |
| Tail Sweep + Cleave | Same as P1 mechanics |
| Burst phase | Raid blows DPS cooldowns + heroism-equivalent (n/a in 1.12 — no heroism) for fast kill |

**Engine rule**: Bellowing Roar fear management is the **dominant mechanic of P3**. Tremor Totem rotation (every 8s, Horde Shaman) + Fear Ward on tank (Alliance Dwarf Priest) is mandatory raid prep.

## Notable drops

| Drop | Source | Use |
|---|---|---|
| **Head of Onyxia** | 100% drop, quest item | Turn in to Major Mattingly (Stormwind, Alliance) / Bram Stoutfellow (Orgrimmar, Horde) → triggers **Rallying Cry of the Dragonslayer** raid-wide 2-hour world buff in capital. **Critical raid utility.** See [systems/world-buffs.md](../systems/world-buffs.md). |
| **T2 helmets** (×~2 per kill, RNG class) | Boss drop | Full T2 set component — see table above |
| **Onyxia Tooth Pendant** | Quest reward (Celebrating Good Times choice) | Neck slot, +24 AP / +12 Stamina / Hit (DPS variant) |
| **Dragonslayer's Signet** | Quest reward (Celebrating Good Times choice) | Ring slot — DPS / tank variant |
| **Onyxia Blood Talisman** | Quest reward (Celebrating Good Times choice) | Trinket slot — heal/cast version |
| **Onyxia Hide Backpack** | Quest chain reward (Tribute to Heroism / equivalent) | **18-slot bag** — vanilla's largest non-profession bag (only Mooncloth Bag at 16 slots is pre-Naxx-comparable) |
| **Mature Black Dragon Sinew** | ~30% drop (~5-10 per kill `[verify pass 3]`) | Hunter Demon Stalker chain reagent — see [classes/hunter.md](../classes/hunter.md) |
| **Class-specific epic items** (BoP, varies) | Boss drops | Various rings, weapons, accessories per class |

## Pre-raid prep

The Onyxia raid is **the easiest 40-man** in vanilla 1.12.1, but full prep is still required:

- [ ] **Drakefire Amulet** in inventory (mandatory — see [onyxia-alliance.md](../attunements/onyxia-alliance.md) / [onyxia-horde.md](../attunements/onyxia-horde.md))
- [ ] **World buffs** (Rallying Cry stacking from Onyxia's previous head = clean Phase 1 burn)
- [ ] **Free Action Potions** (P3 fear mitigation for non-Tremor/Fear Ward classes)
- [ ] **Tremor Totem** rotation planned (Horde) OR **Fear Ward** assignments (Alliance Dwarf Priests)
- [ ] **Tank facing** assignment (head-away-from-raid, tail-away-from-raid)
- [ ] **Whelp AoE assignment** (1-2 Mages + 1 Hunter dedicated to wall whelps)
- [ ] **Class-specific tank consumables** (Limited Invulnerability Potion P1 for tank cleave-burst)

**Estimated pre-raid prep**: 30-60 minutes for a guild that has done it before; longer for first-time runs.

## VMaNGOS / private server notes

- **All 3 phases** are correctly scripted on most VMaNGOS forks.
- **Deep Breath telegraphing + raid-AoE damage** works correctly.
- **Whelp spawn from cave walls** is reliable.
- **Bellowing Roar fear cycle** is correctly timed.
- **Drakefire Amulet check on zone-in** is enforced.
- **Head of Onyxia turn-in → Rallying Cry of the Dragonslayer 2-hour world buff** is fully scripted on VMaNGOS — buff applies raid-wide in capital city.

## Decision-Engine Rules

- **id:** `raid.onyxia.attune-required` — IF `RaidScheduledOnyxia && !Items.Contains(DrakefireAmulet)` THEN raid action suppressed. Priority **999**.
- **id:** `raid.onyxia.head-turnin-priority` — IF `Items.Contains(HeadOfOnyxia) && InCapital` THEN turn in for Rallying Cry world buff. Priority **820** (raid utility for next raid window).
- **id:** `raid.onyxia.tank-facing-rule` — IF `Player.IsRaidOnyxiaTank` THEN orient Onyxia head away from raid + tail away from raid. Priority **999** (combat-time positional rule).
- **id:** `raid.onyxia.deep-breath-alert` — IF `Phase==2 && Onyxia.IsCenter && DeepBreathTelegraph.Detected` THEN raid-wide alert to move to side walls. Priority **999** (combat-time, ~3-second response window).
- **id:** `raid.onyxia.fear-mitigation` — IF `Phase==3 && BellowingRoar.OnCooldown == false` THEN Tremor Totem (Horde) / Fear Ward on tank (Alliance Dwarf Priest) rotation. Priority **900** (combat-time).
- **id:** `raid.onyxia.whelp-aoe-assign` — IF `Phase==2 && Whelps.Spawning` THEN AoE casters target whelps at cave wall. Priority **800** (combat-time).
- **id:** `raid.onyxia.lockout-5-day` — IF `RaidLastOnyxia.Date < (now - 5 days)` THEN raid eligible. Priority **decision-request**.

## Snapshot Fields Needed

- `Level`, `Class`, `Faction` (existing)
- `Items.Contains(DrakefireAmulet)` (existing — derived from inventory scan)
- `Items.Contains(HeadOfOnyxia)` (planned — boss drop transient until turn-in)
- `Player.IsRaidOnyxiaTank` (planned — derived from raid role)
- `Onyxia.Phase` (planned — boss-encounter state, 1/2/3)
- `BellowingRoar.OnCooldown` (planned — boss spell cooldown)
- `Whelps.Spawning` (planned — boss event flag)
- `RaidLastOnyxia.Date` (planned — 5-day lockout tracking)

## Cross-references

- [attunements/onyxia-alliance.md](../attunements/onyxia-alliance.md) — Marshal Windsor chain → Drakefire Amulet
- [attunements/onyxia-horde.md](../attunements/onyxia-horde.md) — Eitrigg/Rexxar chain → Drakefire Amulet
- [systems/world-buffs.md](../systems/world-buffs.md) — Rallying Cry of the Dragonslayer world buff (head turn-in)
- [systems/consumables.md](../systems/consumables.md) — Free Action Potions for P3 fear mitigation
- [classes/priest.md](../classes/priest.md) — Dwarf Fear Ward (Alliance fear-mitigation)
- [classes/shaman.md](../classes/shaman.md) — Tremor Totem (Horde fear-mitigation)
- [classes/hunter.md](../classes/hunter.md) — Mature Black Dragon Sinew for Demon Stalker chain (Onyxia is required for Rhok'delar/Lok'delar)
- [raids/molten-core.md](molten-core.md) — first 40-man; pre-Onyxia gear progression
- [raids/blackwing-lair.md](#) (next iteration) — Nefarian = T2 chest drop (Onyxia = T2 helm only)
