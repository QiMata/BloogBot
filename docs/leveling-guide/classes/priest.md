# Priest — WoW 1.12.1 Class Deep-Dive

> **Sources** (crawl date 2026-05-01):
> - https://warcraft.wiki.gg/wiki/Priest (canonical, modern)
> - https://www.icy-veins.com/wow-classic/priest-quests-in-wow-classic
> - https://www.icy-veins.com/wow-classic/benediction-anathema-quest-guide-to-the-balance-of-light-and-shadow
> - https://www.warcrafttavern.com/wow-classic/guides/benediction-anathema-quest-guide/
> - https://www.wowhead.com/classic/guide/how-to-obtain-benediction
> - https://wowpedia.fandom.com/wiki/Priest_racial_abilities (referenced via search)
> - https://blizzardwatch.com/2023/11/29/priest-racial-abilities-wow-classic/ (referenced via search)
>
> **Pass 2.** Some details (T0/T0.5 piece-by-boss, exact numerical scaling on each racial) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## Identity

Priests are the **primary raid healer** alongside Paladins (Alliance) / Druids+Shamans (Horde). Priest is the **only class with race-specific spells** in vanilla — choice of race meaningfully shapes the kit, and **Dwarf Priests are the most-sought class-race combo on Alliance** because of **Fear Ward** (3-min single-target fear immunity, the defining MC/BWL utility). Holy Priests provide the highest single-target healing throughput; Discipline brings **Power Infusion** (target +20% spell damage, 15s, 3min CD — used on Mages for AoE burst windows); Shadow Priests bring **Vampiric Embrace** (party-wide leech: 20% of priest's shadow damage heals the party — strong utility but DPS-low).

| Role | Spec | Strength |
|---|---|---|
| Raid healer | Holy 23/30/0 (or 21/30/0) | Renew + Greater Heal + Power Word: Shield + Discipline mana |
| Mage-buffer / off-healer | Discipline-leaning 14/30/7 | Power Infusion target buffs |
| Raid utility-DPS | Shadow 5/13/33 | Vampiric Embrace party-leech (raid-wide healing buff) |
| PvP | Shadow 5/13/33 + Disc splash | Mind Flay slow + Shadowform 15% damage reduction |
| Leveling | Shadow 0/5/31 → respec Holy at 60 | Shadowform pivot at 40 |

## Race availability + racial trait synergy

In 1.12.1 **Priest is restricted to 5 races**: Human, Dwarf, Night Elf, Undead, Troll. (No Gnome, Orc, Tauren in 1.12.1; Draenei/Blood Elf are TBC.)

Each race gets **2 race-specific Priest spells** in addition to standard racials. The race-specific Priest spells are **the single biggest race-pick consideration in vanilla**.

| Race | Faction | Standard racials | **Priest racial spells** | Notes |
|---|---|---|---|---|
| Human | Alliance | Sword/Mace Spec +5, Diplomacy, Perception | **Desperate Prayer** (instant 10-min CD heal of self) + **Feedback** (passive: target burns mana on melee hit) | Generalist; Desperate Prayer is a panic-button useful in Holy / Disc / leveling |
| Dwarf | Alliance | Mace/Gun Spec +5, Stoneform, Find Treasure | **Desperate Prayer** + **Fear Ward** (3-min single-target fear immunity, instant cast) | **Most-sought Priest race in 1.12 raids.** Fear Ward is required for Magmadar (every 3 min recasted on tank), Onyxia P2, Ragnaros (Sons of Flame), Nefarian (multi-target ferals), Princess Yauj (AQ40 fears). Pre-TBC Fear Ward was Dwarf-only — guilds would *pay gold* to recruit Dwarf Priests. |
| Night Elf | Alliance | Quickness 1% dodge, Shadowmeld, Wisp Spirit, +5 Bow Spec | **Starshards** (channeled DoT, ranged) + **Elune's Grace** (-25% ranged hit chance vs caster, 15s, 5min CD) | Both racials are weak in PvE; Elune's Grace is niche PvP. NE Priests are functional but considered the weakest Alliance Priest race in raid. |
| Undead | Horde | **Will of the Forsaken** (immune fear/sleep/charm 5s, 2min CD), Cannibalize, Underwater Breathing, Shadow Resist +10 | **Devouring Plague** (24s shadow DoT, heals priest for damage dealt) + **Touch of Weakness** (5min self-buff: melee attacker takes shadow damage + has -damage debuff) | **Best Horde PvP Priest** — WotF is the highest-impact PvP racial; Devouring Plague is a leveling/Shadow PvE staple. |
| Troll | Horde | **Berserking** (haste based on missing HP), Bow/Throwing +5, Beast Slaying, Regeneration | **Hex of Weakness** (2-min debuff: -damage + -20% healing received — the **second** Mortal Strike effect in vanilla after Warrior MS) + **Shadowguard** (Lightning-Shield-like reactive shadow damage on melee hit) | Hex of Weakness in PvP gives Trolls anti-healer utility; Shadowguard for solo questing. |

**Engine race-pick rule** (priests, by role):
- **Raid Healer / Holy** on Alliance → **Dwarf** (Fear Ward = mandatory raid utility)
- **Raid Healer** on Horde → **Undead** (Devouring Plague leveling speed) or **Troll** (Hex of Weakness for PvP off-nights)
- **PvP Priest** → **Undead** (WotF) on Horde / NE (Shadowmeld) on Alliance
- **Shadow PvE** → **Troll** (Berserking burst window aligns with Mind Blast / SW:Death rotation)

## Class quests in level order

| Lvl | Quest / Chain | NPCs / Locations | Reward | Engine action |
|---|---|---|---|---|
| 1-9 | Race-specific starter | Class trainer in starting zone | Starter caster weapon + initial spells | Auto-accept |
| 10 | **Race-specific Priest spell quest 1** — unlocks first racial spell | Race-specific NPC in starting zone area `[verify pass 3 — exact NPC per race]` | First racial Priest spell (e.g., Devouring Plague for Undead, Starshards for NE) | Class-identity priority **920** at lvl 10. |
| 14 | Cure Disease, Resurrection ranks | Trainer | — | Auto-trained |
| 16 | Mana Burn (lvl 14 actually `[verify]`) | Trainer | Mana Burn | Auto-trained |
| 20 | **Levitate** trained | Trainer | Levitate (slow-fall + walk-on-water-ish for 2 min) | Trainer-only |
| 20 | **Race-specific Priest spell quest 2** — unlocks second racial spell | Race-specific NPC | Second racial Priest spell (e.g., Touch of Weakness for Undead, Elune's Grace for NE, Shadowguard for Troll, Fear Ward for Dwarf — **the Fear Ward unlock is the single highest-impact race-quest in the game for Alliance raid Priests**) | Priority **940** at lvl 20 — Fear Ward gates raid utility. |
| 24 | Mind Vision rank-ups | Trainer | — | Auto-trained |
| 30 | **Mind Vision** quest chain (one-quest, race-specific) | Trainer | **Mind Vision** spell | Trainer or short quest chain `[verify pass 3]` |
| 30 | Shackle Undead trained | Trainer | Shackle Undead (mid-bracket undead CC; great in Strat / Scholo) | Trainer-only |
| 40 | Shadowform talent gate (Shadow tree, 31-pt — requires Shadow spec respec) | n/a | Shadowform (15% damage reduction, -all healing, +shadow damage; unlock at 40 with deep Shadow build) | Talent unlock, not quest |
| 40 | Mind Control rank-ups | Trainer | — | Auto-trained |
| 50 | Renew, Greater Heal, Power Word: Shield rank-ups | Trainer | — | Auto-trained |
| **60** | **The Balance of Light and Shadow → Benediction / Anathema** chain | **Eye of Divinity** (50% drop from **Majordomo Executus** in Molten Core, alternate to Hunter's Ancient Petrified Leaf) → **Eris Havenfire** (NW Eastern Plaguelands) → **Save 50 peasants while ≤14 die** event → **Eye of Shadow** (Lord Kazzak world boss, Blasted Lands — high drop chance) → combine via **Splinter of Nordrassil** | **Benediction** (epic 1H+OH staff... actually a 2H staff, +healing, +30 healing equivalent, +35 stamina, 28-49 damage, 1.30 speed `[verify pass 3 exact stats]`) ↔ **Anathema** (right-click toggle: shadow damage form) | **Multi-week chain**, MC + EPL escort + Lord Kazzak world boss kill required. Priority **970** at lvl 60. |

### The Eye of Divinity / Eye of Shadow split

The lvl-60 chain reuses the same MC drop mechanic as Hunter's Rhok'delar — Majordomo Executus's **Cache of the Firelord** drops **either** Ancient Petrified Leaf (Hunter) **or** Eye of Divinity (Priest), 50/50 split per kill. Engine implication: a guild MC roster with multiple Hunters AND Priests both needing the chain may take **multiple lockouts** (1/week per char) to cover the rolls.

### The Balance of Light and Shadow event

The escort/defense event at Eris Havenfire in EPL is **fail-state-sensitive**:

- 50 peasants must reach safety
- ≤14 peasants can die (15+ deaths = quest fail and 1-hour reset)
- Skeletons spawn waves attacking peasants; Priest must Heal the peasants + **Abolish Disease** the diseased ones + kill skeletons
- Highly tuned for Holy/Disc Priest — Shadow Priest can complete it but is more difficult
- **Soloable** — no group required, but consumables (Major Mana Pots, Greater Healing Pots) recommended

### The Eye of Shadow

**Lord Kazzak** in Blasted Lands is a world boss — single-spawn, contested between guilds, ~3-7 day respawn timer, and the kill needs a **40-man raid**. **Eye of Shadow** has a high but not 100% drop rate (estimate 60-80% `[verify pass 3]`); priests in raid roll on the eye against other priests in the raid. Most large-guild Priests get their Eye within 1-3 Kazzak kills.

## Talent trees (1.12 51-point trees)

### Discipline (Power Infusion capstone)

Mana efficiency + buff utility. The 31-pt capstone **Power Infusion** = target buff: +20% spell damage and healing for 15s, 3-min CD (commonly used on Mages for AoE burst, on Holy Priests for raid-wide healing windows).

Key talents: **Unbreakable Will** (5/5 — fear/charm/sleep/disorient resist), **Wand Specialization** (5/5 — +25% wand damage; **leveling staple**), **Improved Power Word: Fortitude** (2/2 — +15% PWFort), **Improved Power Word: Shield** (3/3 — +15% PWShield absorb), **Silent Resolve** (5/5 — -20% threat, -resist on Discipline spells), **Improved Inner Fire** (3/3 — +50% Inner Fire armor), **Mental Agility** (5/5 — -10% mana cost on instants), **Mental Strength** (5/5 — +15% intellect), **Inner Focus** (1/1 — next spell free + 25% crit, 3-min CD), **Meditation** (3/3 — 15% mana regen during cast — **mandatory for healers**), **Improved Mana Burn** (2/2), **Improved Power Word: Shield** (already listed), **Power Infusion** (1/1 — 31-pt capstone).

### Holy (Lightwell capstone)

Healing throughput + crit. The 31-pt capstone **Lightwell** is widely considered weak in 1.12 — places a stationary "well" that party clicks for healing-on-cooldown. Most Holy Priests still take it for the talent points required to reach it.

Key talents: **Holy Specialization** (5/5 — +5% crit on Holy spells), **Improved Renew** (3/3 — +15% Renew healing), **Holy Reach** (2/2 — +20% range on Smite/Holy Fire/Holy Nova/Renew/Mind Sear-equivalent), **Searing Light** (2/2 — +10% damage on Smite/Holy Fire), **Healing Focus** (2/2 — pushback resistance), **Improved Healing** (5/5 — -15% mana on Greater Heal, the workhorse spell), **Spiritual Healing** (5/5 — +10% healing done — **mandatory for healers**), **Spiritual Guidance** (5/5 — +25% spirit-to-bonus-healing/damage; **defining Holy talent**), **Inspiration** (3/3 — +25% armor on the target after a crit-heal), **Improved Mass Dispel — n/a, that's TBC**, **Holy Nova** (1/1 — instant AoE heal+damage, 30s CD, low yield), **Lightwell** (1/1 — 31-pt capstone).

### Shadow (Shadowform capstone)

Shadow damage + Vampiric Embrace utility. The 31-pt capstone **Shadowform** = +15% Shadow damage, 15% damage reduction, but disables Holy/Discipline spells (no Smite/Heal in Shadowform).

Key talents: **Spirit Tap** (5/5 — +100% spirit regen for 15s after killing a mob — **the defining Shadow leveling talent**), **Improved Shadow Word: Pain** (2/2 — +6s SW:P duration), **Improved Psychic Scream** (2/2 — -2s CD), **Improved Mind Blast** (5/5 — -2.5s CD on MB → 6s effective), **Shadow Reach** (3/3 — +20% range on Shadow spells), **Shadow Weaving** (5/5 — stack-debuff, +15% shadow damage taken at 5 stacks — **mandatory raid debuff**), **Mind Flay** (1/1 — channeled shadow DoT + slow), **Improved Vampiric Embrace** (3/3 — +50% VE leech), **Vampiric Embrace** (1/1 — toggle, 20% of priest's Shadow damage heals party), **Darkness** (5/5 — +10% Shadow damage), **Silence** (1/1 — 5s silence, 45s CD), **Shadowform** (1/1 — 31-pt capstone).

### Canonical builds at lvl 60

| Build | Spend | Role | Notes |
|---|---|---|---|
| **Holy raid healer** | **23/30/0** — Imp PWShield 3 / Wand Spec 5 / Mental Agility 5 / Mental Strength 5 / Inner Focus 1 / Meditation 3 / Improved Inner Fire 3 / Power Infusion 1 → 30 Disc; Holy Spec 5 / Spiritual Guidance 5 / Spiritual Healing 5 / Healing Focus 2 / Imp Healing 5 / Holy Reach 1 → 23 Holy | Canonical raid healer | Power Infusion + Greater Heal spam |
| **Power Infusion utility healer** | 14/30/7 (variant) | Buffs Mages on Twin Emp / AoE bosses | Less raw heal, more raid DPS contribution |
| **Shadow PvE / utility** | **5/13/33** — Wand Spec 5 → 5 Disc; Improved Mind Blast 5 / Imp PWPain 2 / Shadow Reach 3 / Shadow Weaving 5 / Mind Flay 1 → 21 Shadow path; deep Shadow to Shadowform: ... → 33 Shadow | Raid utility-DPS (Vampiric Embrace + Shadow Weaving debuff) | Most raid groups bring 1 Shadow Priest for Shadow Weaving stack + VE party-leech |
| **Shadow PvP** | 5/13/33 (same as PvE) or 0/3/45 deep | BG ladder | Shadowform damage reduction + Mind Flay slow |
| **Leveling: Shadow Spirit Tap** | 0/5/31 by 50 → 5/13/33 by 60 | Solo questing | Spirit Tap + Wand Spec + Shadowform pivot at 40; **fastest leveling Priest spec** |

**Engine spec-pick rule**: Leveling = always Shadow Spirit Tap. At 60, respec to role (Holy 23/30/0 if raid healer, Shadow 5/13/33 if raid utility).

## Recommended weapons by bracket

| Bracket | Weapon class | Best in bracket | Why |
|---|---|---|---|
| 1-15 | 1H mace + off-hand | Vendor mace | Wand-Specialization Disc allows wand-DPS even with mace |
| 15-30 | 1H + OH or staff | Quest staffs (Staff of Westfall A / Crescent Staff H) | Stat sticks for INT/Spi |
| 30-45 | Staff or 1H+OH | **Mograine's Might / Sword of Serenity** (SM Cathedral) | |
| 45-55 | Staff | **Truesilver Cleaver** (BS-crafted) / Stat-stick staffs | |
| 55-60 | Staff or 1H+OH | **Spire of Hakkar** (no — that's TBC era) — pre-raid: **Headmaster's Charge** (Scholo Darkmaster Gandling) — long staff with healing | |
| 60 (pre-raid) | Staff | **Hammer of Grace** (Scholo) for fast-cast 1H Healing / **Headmaster's Charge** for 2H spell-power staff | |
| 60 (post-MC) | Staff / 1H+OH | **Benediction** (class quest staff — see chain above) — class-defining BiS | |
| 60 (post-Anathema swap) | Anathema for Shadow runs | toggle right-click | Same item, dual-form |
| 60 (post-BWL) | Staff | **Lok'amir il Romathis** (BWL — wait, that's a 1H mace) / **Rivendare's Deathcharger** (no — that's a mount) / **Dragonfang Blade** | varies by drop |
| 60 (post-Naxx) | Staff | **Atiesh, Greatstaff of the Guardian** — legendary, requires 40 splinters from naxx | Cross-class legendary; Priest one of 5 eligible classes |

## Pre-raid BiS gear (Holy raid healer focus)

`[verify pass 3 for exact items]`

| Slot | Item | Source |
|---|---|---|
| Head | **Mind Carver** (no — that's caster head) → **Lightforge Helm** — wait that's Pal. Priest pre-raid: **Cassandra's Grace** (Scholo) / **Skullflame Shield** — n/a not head. Use **T0 Vestments of Prophecy** helm or PvP rank-7 cloth | |
| Neck | **Star of Mystaria** (DM E) | DM E |
| Shoulders | **Mantle of Lost Hope** (Strat UD Postmaster) | |
| Cloak | **Cape of the Cosmos** (Tailoring) / **Cloak of Healing** | |
| Chest | **Truefaith Vestments** (Tailoring BoP) — class-defining cloth chest | Tailoring 300, mats include Mooncloth + Pristine Hide of the Beast `[verify pass 3]` |
| Bracers | **Foreman's Gloves** — n/a; **Bracers of Prosperity** (Hide of the Wild crafted) `[verify pass 3]` | |
| Hands | **Hands of Power** (BoE) | |
| Belt | **Padre's Trousers** (no — that's legs) | |
| Legs | **Brightcloth Robe legs** (Tailoring) / Lightforge Legplates — n/a Pal | |
| Feet | **Whisperwalk Boots** — n/a Rogue / Priest-equivalent: **Boots of the Full Moon** | |
| Ring 1 | **Magni's Will** / Don Julio's Band (AV) | |
| Ring 2 | **Tarnished Elven Ring** | |
| Trinket 1 | **Briarwood Reed** (DM N) | DM N |
| Trinket 2 | **Eye of the Beast** (LBRS Beasts) | |
| Weapon 2H | **Headmaster's Charge** (Scholo Darkmaster Gandling) — staff | Scholo |
| OH (if 1H+OH) | **Tome of Shadow Force** | |
| Wand | **Acolyte's Wand** of Healing — early; **Wand of the Whispering Dead** (Strat) for Shadow | |

**Wand slot (Priest-only-relevant for non-Mage caster wands)**: Priests use wands for between-cast mana savings; pre-raid BiS wand has 33+ damage with stat bonuses. Engine should keep the highest-damage wand equipped for autoshot-equivalent leveling DPS.

## Tier set progression

| Tier | Set name (Priest) | Source | Notes |
|---|---|---|---|
| **T0 (Dungeon Set 1)** | **Vestments of Prophecy** | 8-piece, 60 5-mans | Drop-only, BoP |
| **T0.5 (Dungeon Set 2)** | **Vestments of Prophecy** upgraded set / equivalent | Quest upgrade chain (patch 1.10) | Bridge |
| **T1** | **Vestments of the Devout** | Molten Core (8-piece) `[verify pass 3]` | Set bonus: heal cost reduction + Spirit Tap-equivalent regen |
| **T2** | **Vestments of Transcendence** | BWL + Onyxia (8-piece) `[verify pass 3]` | Set bonuses: -mana cost on heals + Heal target gets Mana regen burst |
| **T2.5** | **Vestments of the Faith Healer** / **Stormcaller's Garb** (Holy/Shadow split) | AQ40 — token-based | |
| **T3** | **Vestments of Faith** | Naxx40 — 9-piece token-based | Set bonuses: massive healing throughput |

## Class trainer locations

| City | Faction | Priest trainer NPCs | Notes |
|---|---|---|---|
| **Stormwind** | Alliance | **Cathedral of Light** (shared with Paladin) — Priestess Anetta, Priestess Josetta, High Priestess Laurena | Shared building with Pal trainers |
| **Ironforge** | Alliance | **Hall of Mysteries / Mystic Ward** — High Priest Rohan, Branstock Khalder, Sister Aquinne | Same hall as Paladin chain (Rohan also handles the Paladin Charger Censer) |
| **Darnassus** | Alliance | **Temple of the Moon** — Priestess A'moora, Sister Elsington, Priestess Alathea `[verify pass 3]` | NE-only side |
| **Orgrimmar** | Horde | **Valley of Spirits** — Father Cobb, Brother Malach, Sister Elsington — wait that's Alliance. Horde Priest trainers: **Tai'jin** (Troll-side instructor in Cleft of Shadow or Valley of Spirits) `[verify pass 3]` | Trolls train here |
| Thunder Bluff | n/a | n/a — no Tauren Priests in 1.12.1 | |
| **Undercity** | Horde | **Magic Quarter** — High Priestess MacDonnell, Priestess Alathea, Father Lankester `[verify pass 3]` | Forsaken-only side |

## VMaNGOS / private server notes

- **Race-specific Priest spell quests** at lvl 10 and 20 are scripted on VMaNGOS but have occasional script issues (e.g., the Dwarf Fear Ward chain talking to a non-spawned NPC in Ironforge).
- **The Balance of Light and Shadow** event in EPL (50-peasant escort) is fully scripted but **timing-sensitive on VMaNGOS** — peasant pathing can occasionally get stuck on terrain, causing fail. Engine should plan 2-3 attempts. `[verify VMaNGOS scripting status pass 3]`
- **Lord Kazzak Eye of Shadow drop rate** — VMaNGOS uses default 1.12 drop tables; Eye of Shadow is high-drop (~60-80%) but contested with other classes who need it for their respective lvl-60 chains (Naxx attune crafted item also uses Eye of Shadow as a reagent on Eye of Shadow recipe).
- **Fear Ward is Dwarf-only on 1.12.1 servers** including VMaNGOS — Classic 2019 made Fear Ward baseline for all Priests in TBC but **kept it Dwarf-only for the 1.13 / Classic patch**. Engine should not assume Fear Ward availability for non-Dwarf Priests on a 1.12.1 server.
- **Benediction / Anathema swap** is correctly implemented as a right-click form-toggle on the staff. Both forms share durability/charges.

## Decision-Engine Rules

- **id:** `class.priest.race-lock` — IF `Class==Priest && Race NOT IN {Human, Dwarf, NightElf, Undead, Troll}` THEN engine error.
- **id:** `class.priest.racial-spell-quest-lvl10` — IF `Class==Priest && Level>=10 && !Spells.Contains(<racial-spell-1>)` THEN run race-specific class quest. Priority **920**.
- **id:** `class.priest.racial-spell-quest-lvl20` — IF `Class==Priest && Level>=20 && !Spells.Contains(<racial-spell-2>)` THEN run race-specific class quest. Priority **940** for **Dwarf Fear Ward** (raid-utility critical), **870** for other races.
- **id:** `class.priest.shackle-undead` — IF `Class==Priest && Level>=20 && !Spells.Contains(ShackleUndead)` THEN visit trainer at lvl 20. Priority **800** (gates Strat/Scholo CC value).
- **id:** `class.priest.benediction` — IF `Class==Priest && Level==60 && Items.Contains(EyeOfDivinity) && !Items.Contains(Benediction)` THEN run The Balance of Light and Shadow. Priority **970**. **Multi-week chain.** Engine plans MC raid (50%-Eye drop, contested with Hunters) → EPL escort event (1-2 attempts) → Lord Kazzak world boss kill.
- **id:** `class.priest.benediction-form-toggle` — IF `Class==Priest && Items.Contains(Benediction) && CurrentSpec.IsHoly && CurrentForm != BenedictionForm` THEN right-click swap. IF `CurrentSpec.IsShadow && CurrentForm != AnathemaForm` THEN swap to Anathema. Priority **600** (in-combat switch overhead).
- **id:** `class.priest.respec-holy-at-60` — IF `Class==Priest && Level==60 && Role==Healer && CurrentSpec != Holy` THEN respec to 23/30/0 Holy. Priority **750**.
- **id:** `class.priest.power-infusion-target` — IF `Class==Priest && Spec==Disc-or-Holy && PartyHasMage && Mage.IsAoEing && PowerInfusion.OnCooldown==false` THEN cast Power Infusion on Mage. Priority **800** (raid utility). Combat-time rule, not strategy-tier.
- **id:** `class.priest.fear-ward-target` — IF `Class==Priest && Race==Dwarf && Spells.Contains(FearWard) && Raid.HasFearMechanicIncoming(<2min)` AND target is Tank or off-tank THEN cast Fear Ward. Priority **850** (raid utility). Combat-time rule.
- **id:** `class.priest.ressurection-after-wipe` — IF `Class==Priest && Group.HasDeadMember && !IsInCombat && PriestMana > resCost` THEN cast Resurrection on dead member. Priority **700** (post-wipe recovery).
- **id:** `class.priest.wand-equipped` — IF `Class==Priest && Level>=15 && WandSlot.IsEmpty` THEN equip best available wand. Priority **600** (mid-cast mana savings).

## Snapshot Fields Needed

- `Class`, `Level`, `Race`, `Faction` (existing)
- `Spells` (planned) — racial Priest spells (Fear Ward, Devouring Plague, Hex of Weakness, Starshards, Touch of Weakness, Shadowguard, Elune's Grace, Desperate Prayer, Feedback), Shackle Undead, Mind Vision, Levitate, Power Infusion, Vampiric Embrace, Shadowform
- `Items.Contains(itemId)` (planned) — Eye of Divinity, Eye of Shadow, Splinter of Nordrassil, Benediction, Anathema
- `WandSlot.IsEmpty` / `WandSlot.WandDamage` (planned)
- `CurrentSpec` (planned, derivable from `TalentTreePoints`)
- `CurrentForm` (planned: Benediction or Anathema mode of staff)
- `Raid.HasFearMechanicIncoming(window)` (planned — derived from boss-encounter scripting; engine reads boss event flags)
- `PartyHasMage` / `Mage.IsAoEing` (planned — derived from party member scan)

## Cross-references

- [decision-engine/per-bracket-actions/01-l1-l10.md](../decision-engine/per-bracket-actions/01-l1-l10.md) — racial spell quest 1 at 10
- [decision-engine/per-bracket-actions/03-l20-l30.md](../decision-engine/per-bracket-actions/03-l20-l30.md) — racial spell quest 2 at 20 (Fear Ward for Dwarf)
- [decision-engine/per-bracket-actions/06-l55-l60.md](../decision-engine/per-bracket-actions/06-l55-l60.md) — Benediction chain at 60
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — class identity priority band
- [classes/hunter.md](hunter.md) — Eye of Divinity / Petrified Leaf shared MC drop
- [classes/paladin.md](paladin.md) — High Priest Rohan (also handles Pal Charger chain Censer)
- [raids/](../raids/) (pass 5) — Lord Kazzak world boss + MC Majordomo for Eye drops
- [professions/](../professions/) (pass 6) — Tailoring Truefaith Vestments
- [pvp/](../pvp/) (pass 8) — Shadow PvP / Shackle CC mechanics
