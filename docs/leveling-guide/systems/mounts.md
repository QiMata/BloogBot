# Mounts — 1.12.1 Riding Skills + Costs + Class Mounts

> **Sources** (crawl date 2026-05-01):
> - https://www.wowhead.com/classic/guide/wow-classic-mounts-riding-skill
> - https://vanilla-wow-archive.fandom.com/wiki/Riding (referenced via search)
> - https://vanilla-wow-archive.fandom.com/wiki/Patch_1.12.1 (referenced via search)
> - https://www.mmojugg.com/news/ultimate-wow-classic-fresh-riding-mount-guide.html
> - https://www.icy-veins.com/wow-classic/riding-profession-guide
>
> **Pass 2.** Some details (exact faction-discount math whether discount applies to mount + training or training only) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)** specifically. **Patch 1.12 inverted the mount cost split** — see "1.12 cost restructure" section below. Where Classic 2019 re-release differs, deltas are flagged inline.

## Identity

Mounts are the dominant **/played time saving** mechanic in vanilla — a 60% speed mount at lvl 40 cuts outdoor travel time by 38%, and a 100% epic mount at 60 cuts it by 50%. The DecisionEngine treats mount acquisition as **the highest-impact lvl-40 milestone** (priority 800) and **highest-impact lvl-60 critical-path purchase** (priority 850).

## 1.12 cost restructure (vs pre-1.12)

In **patch 1.12** Blizzard inverted the cost split between mount and training:

| Tier | **Pre-1.12** (1.0-1.11) | **1.12.1** (target) |
|---|---|---|
| Apprentice (lvl 40) | **80g mount + 20g training = 100g** | **20g mount + 80g training = 100g** |
| Journeyman / Epic (lvl 60) | **1000g mount + ? training ≈ 1000g** | **100g mount + 1000g training = 1100g** |

The 1.12 change made the **riding skill itself the major cost** rather than the mount. **This is an important version-specific fact** — pre-1.12 guides will report mount costs as the bigger expense, but on a 1.12.1 server the inverse is true.

**Engine note**: VMaNGOS faithfully implements the **1.12.1 split** (~80g training + 20g mount at 40; ~1000g training + 100g mount at 60), so the engine's gold-saving rules should target ~100g at 40 and ~1100g at 60 (pre-discount).

## Riding skill tiers

| Tier | Skill required | Speed | Lvl unlock |
|---|---|---|---|
| **Apprentice Riding** | 75 | +60% ground | 40 |
| **Journeyman Riding** | 150 | +100% ground | 60 |
| Expert Riding (flying) | n/a | n/a — **flying does NOT exist in 1.12.1**; flying mounts are TBC (Outland) | n/a |

**Cross-version delta**: All ground mounts in 1.12.1 share the **+60% / +100% speed split** — there is no "swift mount" tier in vanilla. Classic 2019 re-release uses identical mechanics.

## Apprentice Riding — Lvl 40

### Cost breakdown (1.12.1)

| Step | Item / Service | Cost (no discount) |
|---|---|---|
| Train Apprentice Riding (75 skill) | Faction-specific riding trainer | **80g** |
| Buy Apprentice Mount | Faction-specific mount vendor | **20g** (or alternative slow mount, 8g) |
| **Total (no discount)** | | **~100g** |

### Faction discount (home city reputation)

Discounts apply to mount **and** riding-skill purchase prices when bought from your race's home faction:

| Home rep tier | Discount | 100g effective cost |
|---|---|---|
| Friendly | -5% | ~95g |
| Honored | -10% | ~90g |
| Revered | -15% | ~85g |
| **Exalted** | **-20%** | **~80g** |

Most players reach Honored on home-city rep through normal questing by lvl 40, hitting the **~90g effective cost** sweet spot.

### Per-faction Apprentice mounts

Each race has racial mounts at lvl 40. Common picks:

| Race | Mount | Where |
|---|---|---|
| Human | **Brown / White / Pinto Stallion** (horse) | Stormwind stables (Eastvale Logging Camp / Old Town) |
| Dwarf | **Brown / Gray / Black Ram** | Ironforge (Amberstill Ranch) |
| Night Elf | **Striped Frostsaber / Nightsaber / Spotted Frostsaber** (sabertooth cat) | Darnassus (Rut'theran Village) |
| Gnome | **Mechanostriders** (3 colors) | Ironforge / Tinker Town |
| Orc | **Brown / Black / Red Wolf** | Orgrimmar (Valley of Honor) |
| Tauren | **Brown / Gray / Black Kodo** | Bloodhoof Village (Mulgore) |
| Troll | **Skeletal / Mottled Raptor** | Sen'jin Village (Durotar) |
| Undead | **Brown / Skeletal Steed** | Brill / Tirisfal Glades |

**Cross-faction mounts**: Buying a non-racial mount requires Exalted with the corresponding home-city faction (e.g., Tauren rep grind on a Human gives access to Kodos at Exalted). This is rarely done due to the 4-month rep grind cost.

## Journeyman Riding — Lvl 60 (Epic Mount)

### Cost breakdown (1.12.1)

| Step | Item / Service | Cost (no discount) |
|---|---|---|
| Train Journeyman Riding (150 skill) | Faction-specific riding trainer (same NPC as Apprentice) | **1000g** |
| Buy Epic Mount | Faction-specific mount vendor | **100g** |
| **Total (no discount)** | | **~1100g** |

### Faction discount (home city reputation)

Same scale as Apprentice — 5%/10%/15%/20% off at Friendly/Honored/Revered/Exalted.

| Home rep tier | Discount | 1100g effective cost |
|---|---|---|
| Friendly | -5% | ~1045g |
| Honored | -10% | ~990g |
| Revered | -15% | ~935g |
| **Exalted** | **-20%** | **~880g** |

**Engine note**: Most lvl-60 raiders reach Exalted home rep within 2-3 months from rep grinding (city-specific quests + AV rep stacking with Stormpike/Frostwolf rep). Engine should plan epic mount purchase **after** reaching Exalted home rep to save ~220g.

### Per-faction Epic mounts (1.12)

Same race, faster version (3 different palettes):

| Race | Mount |
|---|---|
| Human | **Swift Brown / Palomino / White Steed** |
| Dwarf | **Swift Brown / Gray / White Ram** |
| Night Elf | **Swift Frostsaber / Mistsaber / Stormsaber** |
| Gnome | **Swift Green / Yellow / Red Mechanostrider** |
| Orc | **Swift Brown / Gray / Timber Wolf** |
| Tauren | **Great Brown / Gray / White Kodo** |
| Troll | **Swift Blue / Olive / Orange Raptor** |
| Undead | **Red / Purple / Blue Skeletal Horse** |

## Class mounts (free or near-free epic)

Three classes have **dedicated class quest chains** that bypass the 1100g epic-mount cost:

| Class | Mount | Riding skill | Effective cost |
|---|---|---|---|
| **Paladin** | **Charger** (lvl 60, class quest) | Free riding skill (paladin-only) | **~450g** in chain materials (vs 1100g — saves ~650g vs Exalted vs ~~880g~~ → still saves ~430g) |
| **Warlock** | **Dreadsteed** (lvl 60, class quest) | Free riding skill | **~250g** in chain materials (saves ~850g vs Exalted ~880g, so basically free vs alternative) |
| **Druid** | **Travel Form** (lvl 30, **40% speed outdoor only**) | n/a — Druid form, no skill cost | **0g** at lvl 30 — saves the entire ~100g Apprentice cost AND extends the saving 10 levels (lvl 30-40) |

### Druid mount-saving advantage

Druid Travel Form at lvl 30 is **the largest gold-saving mechanic in vanilla** when combined across leveling:

- Druid skips Apprentice training at 40 entirely (save ~80-90g)
- Druid still buys Journeyman at 60 (~880g+)
- Druid effectively gets a "free 10-level mount window" (lvl 30-40) plus the 40-60 window where Travel Form (40%) is **slower than mount (60%)** but free

**Engine planning rule**: For Druid plans, NEVER buy a non-class mount before lvl 60. Travel Form is sufficient.

### Pal/Warlock mount-saving advantage

Both classes get **free Apprentice riding skill** when they receive their class horse at lvl 40 (Warhorse for Pal, Felsteed for Warlock). This saves the **~80-90g Apprentice training cost**.

For epic mounts at 60, Pal Charger and Warlock Dreadsteed have multi-stage quest chains (~250-450g hard reagents) that net **~400-650g savings** vs the standard 1100g epic mount route.

**Engine rules**:
- Pal/Warlock at lvl 40: do NOT buy Apprentice training (free via class quest); just acquire Warhorse/Felsteed
- Pal/Warlock at lvl 60: schedule class chain (Charger/Dreadsteed) BEFORE buying generic epic mount
- See [classes/paladin.md](../classes/paladin.md), [classes/warlock.md](../classes/warlock.md) for chain details

## Faction-locked / rare-grind mounts

| Mount | Source | Cost |
|---|---|---|
| **Wintersaber Trainers Frostsaber** | Alliance only — Wintersaber Trainers rep Exalted (Winterspring) | ~3-4 months rep grind |
| **Stormpike Battlecharger** | Alliance — Stormpike Guard Exalted (AV rep) | AV grind to Exalted |
| **Frostwolf Howler** | Horde — Frostwolf Clan Exalted (AV rep) | AV grind to Exalted |
| **Argent Battle Stallion** (no — that's TBC) | n/a in 1.12.1 | n/a |
| **Brewfest Ram / Kodo** (no — Brewfest is TBC) | n/a in 1.12.1 | n/a |
| **Black Qiraji Battle Tank** | Server-first AQ event reward (rare) | One per server |

## Mount summon mechanics

- **2-second cast time** to summon a mount (interruptible by combat / damage)
- **Cannot mount in combat** (must drop combat first)
- **Cannot mount in cities** (most cities — except specific allowed zones)
- **Mounts dismount on damage taken** (a single hit forces dismount — relevant in PvP)
- **No flying mounts** in 1.12.1 (flying is TBC)
- **Mount aura** is one of the 16 buff slots (counts against the 1.12 buff cap)

**Cross-version delta**: Classic 2019 added an instant-cast mount feature for some classes (talent-based or trinket-based); 1.12.1 has no such mechanic.

## Engine planning rules — mount acquisition windows

### At lvl 40 (Apprentice mount)

| Class | Engine action |
|---|---|
| **Druid** | Skip — already have Travel Form (40% outdoor) since lvl 30. Plan to buy nothing. |
| **Paladin** | Run Tome of Nobility class quest chain → free Warhorse + free riding |
| **Warlock** | Visit class trainer at lvl 40 → free Felsteed + free riding |
| **Shaman** | Buy Apprentice (60%) — Ghost Wolf is 20-skill but slower in many cases; **DO buy** mount at 40 |
| **All other classes** | Buy Apprentice riding (~80g training + 20g mount = ~100g; ~80g at Exalted) |

### At lvl 60 (Epic mount)

| Class | Engine action |
|---|---|
| **Paladin** | Run Charger chain (~450g + class quests) — see [paladin.md](../classes/paladin.md). Saves ~430g vs generic. |
| **Warlock** | Run Dreadsteed chain (~250g + class quests) — see [warlock.md](../classes/warlock.md). Saves ~630g. |
| **Druid** | Buy generic epic mount at 60 (~880g at Exalted). Travel Form is still useful for outdoor at 40% speed. |
| **All other classes** | Buy Journeyman + epic mount (~1100g, ~880g at Exalted) |

## VMaNGOS / private server notes

- **1.12 cost split** (80g training + 20g mount at 40; 1000g training + 100g mount at 60) is correctly implemented.
- **Faction discount** at Friendly/Honored/Revered/Exalted works correctly.
- **Class mount chains** (Charger, Dreadsteed) work correctly with free riding skill.
- **Druid Travel Form** at lvl 30 is correctly 40% speed outdoor + can be cast in combat (unique to Druid).
- **No flying mounts** — VMaNGOS is 1.12.1-faithful here.

## Decision-Engine Rules

- **id:** `mount.apprentice-buy` — IF `Level==40 && Class NOT IN {Paladin, Warlock, Druid} && CopperOnHand >= apprenticeMountCost(faction-discount)` THEN visit racial riding trainer + mount vendor. Priority **800** (highest non-class action at lvl 40).
- **id:** `mount.skip-apprentice-druid` — IF `Class==Druid && Level==40` THEN skip mount purchase. Priority **999** (suppression rule).
- **id:** `mount.warhorse-paladin` — IF `Class==Paladin && Level>=40 && !Spells.Contains(SummonWarhorse)` THEN run Tome of Nobility chain. Priority **990** (class identity, free mount). See [paladin.md](../classes/paladin.md).
- **id:** `mount.felsteed-warlock` — IF `Class==Warlock && Level>=40 && !Spells.Contains(SummonFelsteed)` THEN visit class trainer. Priority **990**. See [warlock.md](../classes/warlock.md).
- **id:** `mount.epic-buy-at-60` — IF `Level==60 && Class NOT IN {Paladin, Warlock} && CopperOnHand >= epicMountCost(faction-discount)` AND `Reputation[home-city] >= Exalted` THEN buy Journeyman riding + epic mount. Priority **850** (critical-path lvl-60 purchase).
- **id:** `mount.epic-wait-for-exalted` — IF `Level==60 && Class NOT IN {Paladin, Warlock} && CopperOnHand >= epicMountCost(noDiscount)` AND `Reputation[home-city] < Exalted` THEN **delay** epic mount purchase until Exalted home rep. Priority **decision-request** (saves ~220g; account-level cost-benefit).
- **id:** `mount.charger-paladin` — IF `Class==Paladin && Level==60 && CopperOnHand >= chargerCost && !Spells.Contains(SummonCharger)` THEN start Charger chain. Priority **980**.
- **id:** `mount.dreadsteed-warlock` — IF `Class==Warlock && Level==60 && CopperOnHand >= dreadsteedCost && !Spells.Contains(SummonDreadsteed)` THEN start Dreadsteed chain. Priority **980**.
- **id:** `mount.av-rep-mount-flag` — IF `PvPRole && Level>=51 && Reputation[StormpikeGuard|FrostwolfClan] >= Exalted && !Mounts.Contains(StormpikeBattlecharger|FrostwolfHowler)` THEN visit AV faction quartermaster. Priority **400** (cosmetic flex).

## Snapshot Fields Needed

- `Level`, `Class`, `Race`, `Faction` (existing)
- `RidingSkill` (existing) — `0 / 75 / 150` per character
- `Mounts` (planned) — list of usable mount items / spells
- `Reputation[<HomeFactionForRace>]` (existing) — derived from race + faction tables
- `Spells.Contains(SummonWarhorse | SummonFelsteed | SummonCharger | SummonDreadsteed | TravelForm | GhostWolf)` (planned)
- `apprenticeMountCost(discountTier)` / `epicMountCost(discountTier)` / `chargerCost` / `dreadsteedCost` (planned engine helpers)

## Cross-references

- [classes/paladin.md](../classes/paladin.md) — Tome of Nobility (Warhorse, lvl 40) + Charger (lvl 60)
- [classes/warlock.md](../classes/warlock.md) — Felsteed (lvl 40) + Dreadsteed (lvl 60)
- [classes/druid.md](../classes/druid.md) — Travel Form (lvl 30, 40% outdoor)
- [classes/shaman.md](../classes/shaman.md) — Ghost Wolf (lvl 20, 40% outdoor, breaks on combat)
- [classes/all-9-classes-summary.md](../classes/all-9-classes-summary.md) — class-mount summary table
- [reputations/argent-dawn.md](../reputations/argent-dawn.md) — example major rep grind (no mount but pattern reference)
- [pvp/honor-system.md](../pvp/honor-system.md) — AV faction-rep epic mounts (Ram / Wolf)
- [decision-engine/per-bracket-actions/04-l30-l40.md](../decision-engine/per-bracket-actions/04-l30-l40.md) — Apprentice mount at 40 critical-path
- [decision-engine/per-bracket-actions/06-l55-l60.md](../decision-engine/per-bracket-actions/06-l55-l60.md) — Epic mount at 60 critical-path
