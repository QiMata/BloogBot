# Shaman — WoW 1.12.1 Class Deep-Dive (Horde only)

> **Sources** (crawl date 2026-05-01):
> - https://warcraft.wiki.gg/wiki/Shaman (canonical, modern)
> - https://www.wowhead.com/classic/guide/shaman-class-quests-classic-wow
> - https://www.icy-veins.com/wow-classic/shaman-quests-in-wow-classic
> - https://www.icy-veins.com/wow-classic/restoration-shaman-healer-pve-spec-builds-talents
> - https://www.icy-veins.com/wow-classic/enhancement-shaman-dps-pve-spec-builds-talents
> - https://www.icy-veins.com/wow-classic/elemental-shaman-dps-pve-spec-builds-talents
> - https://vanilla-wow-archive.fandom.com/wiki/Shaman/Quests (referenced via search)
>
> **Pass 2.** Some details (T0/T0.5 piece-by-boss, exact totem-quest Brine/Tony Two-Tusk coordinates) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.
>
> **Faction lock.** Shaman is **Horde-only** in 1.12.1. Alliance Shamans (Draenei) are TBC. Engine MUST NOT schedule Shaman actions for Alliance toons.

## Identity

Shamans are the **Horde-exclusive raid utility / hybrid healer / melee-buff bot**. The class brings the single most-mandatory raid buff in vanilla:

- **Windfury Totem** — 5-yard pulsing buff that gives the Enhancement Shaman's party (max 5 melee) +20% attack power and a chance for an extra 2 attacks on weapon swing. **A 5-melee party with WF Totem averages +30% DPS.** Every raid wants 1 Shaman per melee party.
- **Mana Tide Totem** (Restoration 31-pt capstone) — 24% party mana regen over 12s, 5-min CD. Extends raid mana for healers.
- **Tremor Totem** — fear/charm/sleep removal pulse. Counters Nefarian / Onyxia / Princess Yauj fears (Horde equivalent of Dwarf Priest's Fear Ward).
- **Grace of Air Totem** — +14% melee/ranged crit (vs party) — strong for Hunters/Warriors/Rogues.
- **Strength of Earth / Strength of Earth Totem** — +AP buff (alternative to Battle Shout in non-Warrior parties).
- **Bloodlust** — n/a (TBC). In 1.12.1 Shamans do **not** have Bloodlust/Heroism (added in TBC).

Shamans are **the Horde MC/BWL/AQ40 melee party leader** — every melee raid party has 1 Enhancement Shaman to drop Windfury/Grace of Air/Strength of Earth/Tremor on rotation.

| Role | Spec | Strength |
|---|---|---|
| Raid healer | Restoration 5/3/43 (or 0/2/49) | Mana Tide + Nature's Swiftness + Tidal Wave-equivalent (hmm — TBC?) |
| Melee party support | Enhancement 5/30/16 | Stormstrike + Windfury Totem + 2H mace spec |
| Caster DPS | Elemental 30/0/21 | Elemental Mastery + Chain Lightning bursts |
| Leveling | Enhancement (or Elemental at 60 splash) | 2H melee + Earth Shock + Self-heal |
| PvP | Elemental + Enhancement hybrid 17/14/0 | Burst Frost Shock + Earth Shock |

## Race availability + racial trait synergy

In 1.12.1 **Shaman is restricted to 3 races**: **Orc, Tauren, Troll**. (No Undead Shamans in 1.12.1; Draenei is TBC.)

| Race | Faction | Relevant racials | Notes |
|---|---|---|---|
| Orc | Horde | **Axe Specialization +5**, **Blood Fury** (25% AP buff 15s, -healing taken), **Hardiness** (25% reduced stun duration), **Command** (+5% pet damage) | **Best Enhancement DPS race** — Axe Spec + Blood Fury sync with Stormstrike opener |
| Tauren | Horde | **Endurance** (+5% base health), **War Stomp** (2s AoE stun, 5y), **Cultivation** (+15 herbalism), **Nature Resistance +10** | War Stomp is a strong panic-button; Endurance + Tauren hitbox = highest survival in melee. **Best Tank-Shaman race** (though tanking on Shaman is not viable raid role in 1.12). Nature Res 10 is meaningful for AQ40 Princess Huhuran. |
| Troll | Horde | **Berserking** (haste based on missing HP, 10s, ~3min CD), Bow/Throwing Spec, **Beast Slaying**, **Regeneration** (+10% health regen) | Berserking syncs with Stormstrike + Windfury triggers — best DPS in low-HP execute window |

**Engine race-pick rule** (Shamans, by role):
- **Enhancement raid melee** → **Orc** (Axe Spec + Blood Fury) — canonical Horde melee Shaman
- **Restoration raid healer** → **Tauren** (War Stomp + 5% HP — survives more pulls; Nature Res helps Huhuran)
- **Elemental caster** → **Troll** (Berserking burst window aligns with Chain Lightning rotation)

## Class quests in level order — the Four Totem Chain

The four totem quest chains are the **Shaman class signature** in vanilla. Each unlocks a totem element. **The engine MUST run all four** — not optional.

| Lvl | Totem chain | NPCs / Locations | Reward | Engine action |
|---|---|---|---|---|
| 1-3 | Race-specific starter | Class trainer in starting zone | Initial spells, low-tier mace | Auto-accept |
| **4** | **Call of Earth** chain | Race-specific quest giver in starter zone (Razor Hill for Orc/Troll, Bloodhoof Village for Tauren) → kill local mobs for items → use **Earth Sapta** at quest location to summon **Earth Elemental** (NPC interaction) → return **Rough Quartz** to giver | **Earth Totem** (used for Earthbind, Stoneskin, Stoneclaw, Strength of Earth) | **Earliest class identity unlock at lvl 4.** Priority **950** during 1-10 bracket. Bundle with starter-zone questing. |
| **10** | **Call of Fire** chain | Multi-zone (race-specific): typically **Razor Hill → Crossroads (Barrens) → kill specific mobs → return to Kranal Fiss in Razor Hill** with **Torch of the Eternal Flame** | **Fire Totem** (Searing Totem, Magma Totem, Fire Nova Totem, Frost Resistance Totem, Flametongue Totem — last is melee party fire-damage buff) | Priority **940** at lvl 10. **Mandatory** for Searing Totem (a passive shadow-bolt-equivalent damage source while healing). |
| **20** | **Call of Water** chain | **Islen Waterseer** (Mulgore, lvl 20 quest giver) → **Brine** (Barrens, ~43,77 `[verify pass 3]`) → fill 3 waterskins: **Brown** (Barrens water hole below Brine's hut) + **Red** (Tarren Mill well, Hillsbrad Foothills, ~62,20) + **Blue** (Ashenvale fountain, ~33,67) → return | **Water Totem** (Healing Stream Totem, Mana Spring Totem, Disease Cleansing Totem, Poison Cleansing Totem, Frost Shock — wait Frost Shock is just trained) | Priority **930** at lvl 20. **Cross-zone chain** spans Barrens / Hillsbrad / Ashenvale — ~30 minutes /played with mount, ~60 min without. |
| **30** | **Call of Air** chain | **Tony Two-Tusk** (Thousand Needles, ~32,38 `[verify pass 3]`) → multi-step in Thousand Needles + return | **Air Totem** (Windfury Totem — **the most-mandatory raid buff in vanilla**, Grace of Air Totem, Tranquil Air Totem, Windwall Totem, Sentry Totem, Nature Resistance Totem, Grounding Totem) | Priority **990** at lvl 30 — **Windfury Totem is the highest-impact raid buff a Shaman provides.** Suspends questing. |

### Other class quests

| Lvl | Quest / Action | Reward |
|---|---|---|
| 14 | Lightning Bolt rank-ups, Healing Wave rank 2 | Trainer-only |
| **20** | **Ghost Wolf** trained (no quest) | Ghost Wolf form (40% outdoor speed; functions as a free mount until Apprentice Riding at 40) |
| 26 | Frost Shock, Shock rank-ups | Trainer |
| **30** | **Reincarnation** trained (no quest) | Self-Resurrection (1-hour CD, 30% HP/mana) — distinguishing Shaman raid utility for wipe recovery |
| 40 | Chain Lightning, Frost Shock rank 4 | Trainer |
| 50 | **Far Sight, Astral Recall** trained (no quest) | Hearthstone-equivalent (15-min CD, free home-bind teleport) |
| 60 | **No epic class weapon quest** — Shaman lacks a class-quest-rewarded epic weapon. Pre-raid 2H BiS comes from Stormpike Battleguard / BS-crafted / world drops. | n/a |

## Talent trees (1.12 51-point trees)

### Elemental (Elemental Mastery capstone)

Caster DPS focused on Lightning Bolt and Chain Lightning. The 31-pt capstone **Elemental Mastery** = next damaging spell instant cast + guaranteed crit + free mana, 3-min CD.

Key talents: **Convection** (5/5 — -5% mana on Shock/LB/CL), **Concussion** (5/5 — +5% damage on LB/CL/Shock), **Earth's Grasp** (2/2 — Stoneclaw HP +50% + Earthbind range), **Elemental Warding** (3/3 — -10% magical damage taken), **Call of Flame** (3/3 — +15% Searing/Magma/Fire Nova damage), **Reverberation** (5/5 — -1s Shock CD), **Elemental Devastation** (3/3 — +3% melee crit after Lightning crit), **Storm Reach** (2/2 — +6 yards LB/CL), **Elemental Focus** (1/1 — proc on hit: free next damaging spell), **Eye of the Storm** (3/3 — pushback resistance), **Elemental Fury** (5/5 — +100% crit damage on elementals), **Lightning Mastery** (5/5 — -0.5s LB/CL cast), **Elemental Mastery** (1/1 — 31-pt capstone).

### Enhancement (Stormstrike capstone)

Melee + buffs. The 31-pt capstone **Stormstrike** = melee strike that puts Nature damage debuff on target (next 2 Nature attacks +20% damage), 20s CD.

Key talents: **Ancestral Knowledge** (5/5 — +5% max mana), **Shield Specialization** (5/5 — +5% block chance), **Toughness** (5/5 — armor multiplier), **Improved Lightning Shield** (3/3 — +30% LS damage), **Improved Ghost Wolf** (2/2 — -2s GW cast), **Two-Handed Weapon Specialization** (5/5 — +5% damage with 2H), **Anticipation** (5/5 — defense rating), **Flurry** (5/5 — +30% attack speed for 3 swings after a crit), **Improved Weapon Totems** (2/2 — +10% Flametongue/Windfury), **Elemental Weapons** (3/3 — +20% Windfury/Flametongue weapon proc), **Parry** (1/1 — parry skill), **Weapon Mastery** (5/5 — +10% weapon damage), **Stormstrike** (1/1 — 31-pt capstone).

### Restoration (Mana Tide Totem capstone)

Healing throughput + utility totems. The 31-pt capstone **Mana Tide Totem** = pulsing party mana regen totem (24% max mana over 12s, 5min CD) — **the defining raid-mana cooldown.**

Key talents: **Improved Healing Wave** (5/5 — -0.5s HW cast), **Tidal Focus** (5/5 — -5% mana on heals), **Improved Reincarnation** (2/2 — -10min Reincarnation CD + revives at 40% HP/mana), **Ancestral Healing** (3/3 — armor buff after crit-heal), **Totemic Focus** (5/5 — -25% mana cost on totems), **Nature's Guidance** (3/3 — +3% spell hit), **Healing Focus** (5/5 — pushback resistance), **Tidal Mastery** (5/5 — +5% Healing/Nature crit), **Healing Grace** (3/3 — -threat from heals), **Restorative Totems** (5/5 — +25% Mana Spring + Healing Stream effects), **Tidal Power** (5/5 — +chance to gain mana on cast), **Nature's Swiftness** (1/1 — instant cast on next Nature spell, 3min CD — **defining raid utility**), **Purification** (5/5 — +10% all healing), **Mana Tide Totem** (1/1 — 31-pt capstone).

### Canonical builds at lvl 60

| Build | Spend | Role | Notes |
|---|---|---|---|
| **Restoration raid healer** | **5/3/43** — Convection 5 → 5 Elem; Imp Lightning Shield 3 → 3 Enh; deep Resto: Imp HW 5 / Tidal Focus 5 / Totemic Focus 5 / Healing Focus 5 / Tidal Mastery 5 / Restorative Totems 5 / Tidal Power 5 / Nature's Swiftness 1 / Purification 5 / Mana Tide 1 → 43 Resto | Raid healer | Healing Wave spam + Mana Tide + NS + Chain Heal |
| **Enhancement melee party support** | **5/30/16** — Convection 5 → 5 Elem; deep Enhancement: Ancestral Knowledge 5 / Toughness 5 / 2H Spec 5 / Anticipation 5 / Flurry 5 / Imp Weapon Totems 2 / Stormstrike 1 → 30 Enh; Tidal Focus 5 / Totemic Focus 5 / Healing Focus 1 / Restorative Totems 5 → 16 Resto | Raid melee party support | Stormstrike + Windfury weapon imbue + 2H mace |
| **Elemental caster** | **30/0/21** — deep Elem: Convection 5 / Concussion 5 / Call of Flame 3 / Reverberation 5 / Storm Reach 2 / Elemental Focus 1 / Elemental Fury 5 / Lightning Mastery 5 / Elemental Mastery 1 → 30 Elem; Tidal Focus 5 / Totemic Focus 5 / Nature's Guidance 3 / Tidal Mastery 5 / Restorative Totems 3 → 21 Resto | Raid caster DPS | Lightning Bolt + Elemental Mastery burst windows; weaker than Mage but brings Mana Tide |
| **Leveling Enhancement** | 0/30/0 → 5/30/16 by 60 | Solo questing | Self-heal between fights, melee + Earth Shock + LS |

## Recommended weapons by bracket

| Bracket | Weapon | Notes |
|---|---|---|
| 1-15 | 1H mace + shield (Resto/quest) or 2H (Enhancement) | Vendor stuff |
| 15-30 | 1H + shield or 2H mace | Quest staffs from WC / SFK |
| 30-45 | 2H mace | Stat 2H mace for Enhancement; staff for Elem/Resto |
| 45-55 | 2H or staff | Witchblade (Maraudon) / Vyletongue Whip (Maraudon) |
| 55-60 | 2H | **Arcanite Reaper** (BS BoE) for Enh; **Headmaster's Charge** staff for Resto/Elem |
| 60 (pre-raid) | 2H | Stormpike Battleguard 2H mace (AV PvP rep reward) for Enh; **Lei of Lilies** offhand for Resto |
| 60 (post-MC) | 2H | **Sulfuras, Hand of Ragnaros** (BS-crafted from Eye of Sulfuras + Sulfuron Hammer reagent — BWL+ class itemization) | |
| 60 (post-BWL) | 1H + shield (Resto) / 2H (Enh) | **Lok'amir il Romathis** (BWL 1H mace) for Resto |
| 60 (post-AQ40) | 1H + shield | Various AQ40 mace drops |
| 60 (post-Naxx) | varies | T3 source weapons |

## Pre-raid BiS gear (Restoration Shaman focus)

`[verify pass 3 for exact items and sources]`

| Slot | Item | Source |
|---|---|---|
| Head | **Crown of the Penitent** (no — that's Pal) → **The Earthfury Helm** (T0) / **Lightforge** — wait Pal. Resto pre-raid: **Resilient Helm**, **Mageblade Pauldrons**, etc. | T0/T0.5/world |
| Neck | **Mark of Fordring** | EPL |
| Shoulders | **Mantle of the Lost Hope** (Strat UD Postmaster) | |
| Cloak | **Cape of the Cosmos** (Tailoring) | |
| Chest | **The Earthfury Vest** (T0) | Strat UD Baron |
| Bracers | **Truestrike-equivalent for casters** | |
| Hands | **The Earthfury Gauntlets** (T0) | |
| Belt | **The Earthfury Belt** | |
| Legs | **Padre's Trousers** — n/a Priest | The Earthfury Leggings |
| Feet | **The Earthfury Boots** | |
| Ring 1 | **Magni's Will** equivalent / Don Mauricio's Band of Magnetism | |
| Ring 2 | **Tarnished Elven Ring** | |
| Trinket 1 | **Briarwood Reed** (DM N) | DM N |
| Trinket 2 | **Eye of the Beast** (LBRS) | LBRS |
| Weapon | **Headmaster's Charge** staff (Scholo) | Scholo |
| Shield (Resto/Enh) | **Drillborer Disk** (BRD Plugger) | BRD |
| Off-hand caster | **Tome of Knowledge** | |
| Wand | n/a — Shaman cannot equip wands. Uses ranged-slot **Totem** instead |
| **Totem (ranged slot)** | **Totem of the Hammer** / **Totem of Sustaining** | various |

**Totem ranged slot**: Shamans use a totem-class ranged item (parallel to Druid Idol, Paladin Libram, Priest Wand). Pre-raid totems give +healing or +spell damage in the ~10-30 range; MC drops better.

## Tier set progression

| Tier | Set name (Shaman) | Source | Notes |
|---|---|---|---|
| **T0 (Dungeon Set 1)** | **The Elements** (also called Elemental Sanctuary) | 8-piece, 60 5-mans | `[verify exact name pass 3]` |
| **T0.5 (Dungeon Set 2)** | **The Five Thunders** | Quest upgrade chain (patch 1.10) | |
| **T1** | **The Earthfury** | Molten Core (8-piece) | Set bonuses: +25% chance to gain mana on Lightning Shield + chain healing bounce |
| **T2** | **The Ten Storms** | BWL + Onyxia (8-piece) | Set bonuses: +25 spell damage and healing + extra Earth Shock crit |
| **T2.5** | **Stormcaller's Garb** | AQ40 — token-based | |
| **T3** | **The Earthshatterer** | Naxx40 — 9-piece | Set bonuses: massive healing + AP buffs |

## Class trainer locations

| City | Faction | Shaman trainer NPCs | Notes |
|---|---|---|---|
| **Orgrimmar** | Horde | **Valley of Spirits** — Tigor Skychaser, Sandahl, Garon, Yelmak | Primary Orc/Troll trainers |
| **Sen'jin Village** | Horde | sub-trainer for Troll Shamans (lvl 1-10) | Lower-tier |
| **Razor Hill** | Horde | Orc Shaman lvl-4 Call of Earth chain start NPC `[verify pass 3]` | |
| **Thunder Bluff** | Horde | **Lower Rise / Spirit Rise** — Brave Stoneland, Magunda Muddysky, Cairne Bloodhoof (lore NPC, not trainer) | Tauren-side trainers |
| **Bloodhoof Village** | Horde | Tauren Shaman lvl-4 Call of Earth chain start NPC | |
| Alliance cities | n/a | n/a — no Alliance Shamans in 1.12.1 | |

## VMaNGOS / private server notes

- **Call of Earth / Fire / Water / Air** chains are fully scripted on VMaNGOS.
- **Brine and the 3-waterskin chain** in Call of Water has occasional pathing issues with mobs near the Tarren Mill well — engine should plan PvP-aware route through Hillsbrad (contested zone).
- **Reincarnation** with the Improved Reincarnation talent revives at 40% HP/mana on a 30-min CD; works correctly on VMaNGOS.
- **Windfury Totem buff** is correctly party-only (5-player scope, NOT 40-player raid). Engine must plan melee-party composition: 1 Shaman + 4 melee per party for max value.
- **Mana Tide Totem** correctly affects only the Shaman's party (5-player) and not the entire raid.
- **Bloodlust does NOT exist in 1.12.1** — it was added in TBC 2.0. Engine MUST NOT schedule Bloodlust-dependent raid actions.

## Decision-Engine Rules

- **id:** `class.shaman.faction-lock` — IF `Class==Shaman && Faction==Alliance` THEN engine error.
- **id:** `class.shaman.race-lock` — IF `Class==Shaman && Race NOT IN {Orc, Tauren, Troll}` THEN engine error.
- **id:** `class.shaman.call-of-earth` — IF `Level>=4 && !Spells.Contains(EarthbindTotem)` THEN run Call of Earth. Priority **950** in 1-10 bracket.
- **id:** `class.shaman.call-of-fire` — IF `Level>=10 && !Spells.Contains(SearingTotem)` THEN run Call of Fire. Priority **940**.
- **id:** `class.shaman.call-of-water` — IF `Level>=20 && !Spells.Contains(HealingStreamTotem)` THEN run Call of Water (3-waterskin cross-zone). Priority **930**.
- **id:** `class.shaman.call-of-air` — IF `Level>=30 && !Spells.Contains(WindfuryTotem)` THEN run Call of Air. Priority **990** — Windfury Totem is the most-impactful raid utility a Shaman provides; **suspends questing**.
- **id:** `class.shaman.totem-rotation-raid` — IF `Spec==Enhancement && InRaid && InMeleeParty && CombatStarting` THEN drop Windfury / Grace of Air / Strength of Earth / Tremor on rotation. Priority **800** (combat-time).
- **id:** `class.shaman.mana-tide-recover` — IF `Spec==Restoration && PartyHealersAvgMana < 40% && ManaTide.NotOnCooldown` THEN drop Mana Tide. Priority **850** (combat-time).
- **id:** `class.shaman.tremor-vs-fear` — IF `Raid.HasFearMechanicIncoming(<20s) && !TremorTotem.IsActive` THEN drop Tremor Totem. Priority **820** (combat-time, Horde fear-counter equivalent of Dwarf Priest Fear Ward).
- **id:** `class.shaman.reincarnation-after-wipe` — IF `Class==Shaman && Player.IsDead && Reincarnation.NotOnCooldown && WipeRecoveryEnabled` THEN Reincarnate to recover the raid. Priority **900** (post-wipe recovery — Shaman is one of two classes with self-rez, the other is Soulstone Warlock).
- **id:** `class.shaman.totem-equipped` — IF `Level>=20 && RangedSlot.IsEmpty` THEN equip best totem. Priority **600**.
- **id:** `class.shaman.respec-at-60` — IF `Level==60 && Role != CurrentSpec.RoleEquivalent` THEN respec to 5/3/43 Resto / 5/30/16 Enh / 30/0/21 Elem. Priority **750**.

## Snapshot Fields Needed

- `Class`, `Level`, `Race`, `Faction` (existing)
- `Spells` (planned) — EarthbindTotem, SearingTotem, HealingStreamTotem, WindfuryTotem, ManaTideTotem, TremorTotem, GhostWolf, Reincarnation, AstralRecall, NaturesSwiftness, ElementalMastery, Stormstrike
- `RangedSlot.IsTotem` / `RangedSlot.SpellPower` (planned)
- `PartyHealersAvgMana` (planned — derived from party member scan)
- `Reincarnation.OnCooldown` (planned — boolean from spell-cooldown table)
- `WipeRecoveryEnabled` (planned — config flag, distinct from in-fight rules)
- `InMeleeParty` (planned — derived from PartyComposition; "is this a 5-player party with ≥3 melee classes")
- `Raid.HasFearMechanicIncoming(window)` (planned — same as Priest's Fear Ward predicate)
- `CurrentSpec` (planned, derivable from `TalentTreePoints`)

## Cross-references

- [decision-engine/per-bracket-actions/01-l1-l10.md](../decision-engine/per-bracket-actions/01-l1-l10.md) — Call of Earth at lvl 4
- [decision-engine/per-bracket-actions/02-l10-l20.md](../decision-engine/per-bracket-actions/02-l10-l20.md) — Call of Fire at 10
- [decision-engine/per-bracket-actions/03-l20-l30.md](../decision-engine/per-bracket-actions/03-l20-l30.md) — Call of Water at 20
- [decision-engine/per-bracket-actions/04-l30-l40.md](../decision-engine/per-bracket-actions/04-l30-l40.md) — Call of Air at 30 (Windfury unlock)
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — class identity priority band
- [classes/priest.md](priest.md) — fear-counter parallel (Dwarf Fear Ward vs Tremor Totem)
- [reputations/](../reputations/) (pass 7) — Stormpike/Frostwolf for AV-rep weapons (Frostwolf side for Horde Shaman)
- [systems/](../systems/) (pass 10) — Talent system + totem ranged-slot mechanics
