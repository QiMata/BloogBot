---
title: "Raid — Naxxramas (40)"
patch: "1.12.1 (Drums of War, Sept 2006); raid added in 1.11 'Shadow of the Necropolis' (June 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/Naxxramas_(original)
  - https://warcraft.wiki.gg/wiki/Atiesh,_Greatstaff_of_the_Guardian
crawl_date: 2026-05-01
---

# Naxxramas (40-man) — The Dread Citadel

The final and hardest pre-TBC raid. Floats above Stratholme in EPL. **15 bosses across 4 wings + Frostwyrm Lair**. Drops **Tier 3 set** ("Dreadnaught/Plagueheart/Frostfire/Bonescythe/etc.") and the **Atiesh legendary staff** (4-class). Released in 1.11 (June 2006), only ~3-4 months before TBC pushed players past it; **most vanilla guilds did not clear it**. AD rep gates the attune cost.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Raid size | 40-man |
| Bosses | 15 (4 wing-final + 11 wing-trash bosses + 2 Frostwyrm) |
| Lockout | 7 days (weekly reset Tuesday US / Wednesday EU) |
| Location | Above Stratholme (Eastern Plaguelands); summon via Necropolis Beacons inside Naxx itself, after attune |
| Attune | Argent Dawn rep + 60g (Honored) / 30g (Revered) / 0g (Exalted) + materials |
| Tier reward | **Tier 3** ("Dreadnaught/Plagueheart/Frostfire/Bonescythe/Cryptstalker/Earthshatterer/Vestments of Faith/Redemption/Dreamwalker") |
| Legendary | **Atiesh, Greatstaff of the Guardian** (4-class: Druid/Mage/Priest/Warlock) |
| Patch added | 1.11 ("Shadow of the Necropolis") — June 2006 |
| 1.12.1 status | Live; full content; ~5% of guilds cleared pre-TBC |

---

## Attune Chain ("The Dread Citadel — Naxxramas")

| Step | Action | Cost |
|------|--------|------|
| 1 | Reach AD Honored at Light's Hope Chapel via Scourgestone + Plagueland quests | 10-30 hours of /played |
| 2 | Speak to Argent Officer Pureheart (or equivalent NPC) at Light's Hope to start "The Dread Citadel — Naxxramas" | 0g |
| 3 | Hand in: 60g (or 30g if Revered, 0g if Exalted) + 5 Arcane Crystal + 2 Nexus Crystal + 1 Righteous Orb | varies |
| 4 | Receive **Naxxramas Attunement** (no item, server-side flag) | — |

**Decision-engine rule:** if `Class IN [any] && AD.Reputation >= Honored && Naxx.Attuned == false`, dispatch to Light's Hope Chapel for attune turn-in. Save Arcane Crystals from Tanaris Mining + Nexus Crystals from Enchanting disenchant of high-tier blues.

See [../attunements/naxxramas.md](../attunements/naxxramas.md) for the detailed attune chain.

---

## Wing 1: Arachnid Quarter (Spider Wing)

| Boss | Difficulty | Mechanic | Notable drops |
|------|-----------|----------|----------------|
| **Anub'Rekhan** | Easy | Locust Swarm AoE silence; chase pattern between 3 platforms; tank kite | T3 cloak (Greaves of Faith for Priest; Belt for some) `[verify pass 3]` |
| **Grand Widow Faerlina** | Medium | 4-Worshipper add control; Mind-Control Worshippers to dispel her enrage; poison bolt volley | **Cape of Faith / Cryptstalker Belt** T3; class trinkets |
| **Maexxna** | Medium-Hard | Web Spray (8s incapacitate every 40s); Necrotic Poison; web wrap; spawned spiderlings | **T3 belt slots**; **Wraith Blade** caster dagger; **Maexxna's Fang** offhand |

**Wing strategy:** typically the first wing cleared. Anub'Rekhan is a "loot pinata" boss in well-geared raids. Faerlina requires 4 dedicated Mind-Controllers (only Priests). Maexxna's Web Spray timing dictates raid healing rotation.

**Decision-engine cue:** Spider wing should be cleared first by all guilds — easiest gear acquisition path. Engine should prioritize Spider Wing T3 drops for healers.

---

## Wing 2: Plague Quarter

| Boss | Difficulty | Mechanic | Notable drops |
|------|-----------|----------|----------------|
| **Noth the Plaguebringer** | Easy-Medium | Cripple debuff; teleport-add phase; Cursed/Banished plays | **T3 helm pieces** — Earthshatterer, Cryptstalker; **Ranseur of Hatred** polearm |
| **Heigan the Unclean** | Medium-Hard (positional) | **Dance** mechanic — 4-section floor pattern, slime erupts; everyone moves in sync, ~95% of wipes are dance failures | **The Eye of Heigan** trinket; **T3 boots** |
| **Loatheb** | Hard (raid healing) | 5-min Spore CD; **healers cannot heal** except after spore consumption; ranged DPS race | **T3 hands**; **Robe of Undead Cleansing** caster set; **Atiesh Splinter** (1% chance) |

**Wing strategy:** the **Heigan dance** is the iconic vanilla Naxx wipe-fest. Loatheb is the bottleneck — Spore management decides if heal-uptime sustains the kill.

**Decision-engine cue:** Heigan dance is **mandatory training** for raid bots. Engine should encode dance pattern as a 4-step waypoint sequence; failure mode = fail-fast wipe (all alive characters in slime instakill).

---

## Wing 3: Military Quarter (Death Knight Wing)

| Boss | Difficulty | Mechanic | Notable drops |
|------|-----------|----------|----------------|
| **Instructor Razuvious** | Hard | **2 Priest Mind-Controllers required** to control his 2 Death Knight understudies (who tank him); Razuvious's normal tank dies in 2 hits | **T3 shoulders for some**; **Glaive of the Pit**; **The Castigator** mace |
| **Gothik the Harvester** | Medium-Hard | **Phase 1**: 50/50 split — half raid Live side, half Dead side; both kill adds; **Phase 2**: combined boss fight after gate opens | **T3 boots**; **Gluth's Reins** (joke item)... actually **Polar Helmet** caster gear |
| **Four Horsemen** (Mograine + Korth'azz + Blaumeux + Zeliek) | **Hardest in vanilla Naxx** | **4 tanks required** (one per horseman); each horseman has a **Mark stack** that becomes lethal at 4+ stacks → tank rotation every 90 seconds; Mograine + Zeliek front-of-room; Korth'azz + Blaumeux back-of-room | **T3 shoulders** (per-horseman drop); **The Phoenix Gloves** caster; **Hammer of the Gathering Storm** Shaman; **Maexxna's Fang** offhand `[verify pass 3]` |

**Wing strategy:** Razuvious requires 2 Priests dedicated to MCing his understudies — bots without Priest in raid composition cannot run this wing. Gothik's gate opening must be timed precisely to avoid double-stacking adds. Four Horsemen is the **vanilla-Naxx skill check** — most pre-TBC guilds wiped here for weeks.

**Decision-engine cue:** Four Horsemen requires **4 Plate-tank Warriors/Paladins on rotation**, plus 4 healers per tank (16 healers minimum). Engine should fail-fast if `RaidComposition.Tanks < 4 && RaidComposition.Class.Plate < 4`.

---

## Wing 4: Construct Quarter (Abomination Wing)

| Boss | Difficulty | Mechanic | Notable drops |
|------|-----------|----------|----------------|
| **Patchwerk** | Easy (DPS race) | No mechanics; pure DPS check (~3-5 minute kill at vanilla gear); Hateful Strike on second-highest threat target | **Patchwerk's Power Soul** caster trinket; **T3 hands**; lots of cloth/leather upgrades; **DPS race threshold** = ~3500 raid DPS |
| **Grobbulus** | Medium | Mutating Injection debuff on random raid; AoE poison cloud spawned every ~20s; tank kite outside cloud | **The Wartorn Mantle** (T3 shoulders for some classes); **Boots of Pure Thought**; **Grobbulus' Reins** (joke item) |
| **Gluth** | Medium-Hard | Decimate (drops all targets to 5% HP every 105s); 8-9 zombie chow adds spawn from gates each Decimate; AoE warlock/mage cleave | **T3 boots/legs**; **The Plague Bearer** Warlock 2H; **Gluth's Reins** (joke) |
| **Thaddius** | Hard (precise positioning) | **Stalagg + Feugen** (positive/negative charge tanks must be killed simultaneously); Thaddius requires **+/- charge raid split** — half raid is + half is -, opposite charges damage each other; +/- jumps + Polarity Shift mechanic | **T3 helm slot**; **Belt of Never-Ending Agony** Warrior; **The Plagueheart Raiment legs** |

**Wing strategy:** Patchwerk is the **DPS race** — guilds gear up Spider/Plague drops first, then return for Patchwerk. Thaddius's Polarity Shift (random +/- swap on raid) is the wing-defining wipe mechanic.

**Decision-engine cue:** Construct Wing requires **3500+ raid DPS on Patchwerk** as gear-gate. Engine should fail-fast if `RaidGear.AvgItemLevel < 70` and `Patchwerk.AttemptKill == true`.

---

## Frostwyrm Lair (Final Wing — opens after all 4 wings cleared)

| Boss | Difficulty | Mechanic | Notable drops |
|------|-----------|----------|----------------|
| **Sapphiron** | Hard (raid-wide DPS + heal + frost res) | **Frost Resistance Aura** mandatory — 200+ FR per raid member; **Life Drain** (random raid 8-target chain heal-back); **Block of Ice** mid-fight (random raid frozen + air phase) → DPS race during ice break to interrupt Deep Breath; **300 FR raid average required** | **T3 legs** (universal); **Eye of the Beholder** trinket; **Splinter of Atiesh** (1% rate); **Sapphiron's Right Eye / Left Eye** trinkets |
| **Kel'Thuzad (KT)** | **Hardest fight in vanilla** | **Phase 1**: 4-min trash phase — 8 waves of skeletons + abominations; **Phase 2**: KT himself with Frostbolt + Frost Blast (1.5s cast — DPS interrupt!) + Mind Control (~10s on random raid); **Phase 3**: Guardians of Icecrown (4 elites at 35%); raid healing + interrupt rotation | **T3 helm + chest** (universal); **The Phoenix Pendant**; **Eye of Naxxramas** (Saph drop, not KT — verify); **Splinter of Atiesh** (~10% rate); **Frame of Atiesh** + **Staff Head of Atiesh** |

**Wing strategy:** Sapphiron's **Frost Resistance gear gate** is the hardest pre-fight prep in vanilla — every raider needs ~200+ FR (gem + enchant + gear). KT is the **vanilla-Naxx final boss** and the legendary boss of all vanilla raid endgame. KT's Frost Blast must be interrupted on every cast or the target dies; raid mechanics demand 2-3 dedicated interrupt rotations.

**Decision-engine cue:** Frostwyrm Lair requires **200+ FR per raid member** before pull (each member checks `Equipment.FrostResistance >= 200`). Engine should defer raid pull if `RaidComposition.MinFR < 150 || RaidComposition.AvgFR < 200`.

---

## Tier 3 Set — Per-Class Map

Tier 3 in 1.12.1 is **drop-only** (no token system). Each class set has 9 pieces, dropped from specific bosses.

| Class | Set name | Theme |
|-------|----------|-------|
| Warrior | **Dreadnaught's Battlegear** | Plate-tank (str/sta/def) |
| Paladin | **Redemption Armor** | Plate-healer (int/sp+heal/spirit) |
| Hunter | **Cryptstalker Armor** | Mail-DPS (agi/sta/+ranged AP) |
| Rogue | **Bonescythe Armor** | Leather-DPS (agi/sta/+atk power) |
| Priest | **Vestments of Faith** | Cloth-healer (int/sp+heal) |
| Shaman | **The Earthshatterer** | Mail-hybrid (varies by spec) |
| Mage | **Frostfire Regalia** | Cloth-caster (int/sp+dmg) |
| Warlock | **Plagueheart Raiment** | Cloth-caster-DPS (int/sp+shadow) |
| Druid | **Dreamwalker Raiment** | Leather-hybrid (varies by spec) |

### T3 piece-to-boss approximate map `[verify pass 3]`

| Slot | Drop boss(es) |
|------|---------------|
| Helm | Kel'Thuzad |
| Shoulders | Four Horsemen |
| Chest | Kel'Thuzad |
| Bracers | Sapphiron |
| Hands | Loatheb / Patchwerk |
| Belt | Maexxna |
| Legs | Sapphiron / Thaddius |
| Boots | Heigan / Gothik |
| Cape | Faerlina |

**Decision-engine rule:** T3 acquisition is a **multi-week multi-wing rotation**. Engine should track per-character T3 progression (`Equipment.T3Pieces` slot count) and prioritize wings with missing slots.

---

## Atiesh, Greatstaff of the Guardian (Legendary)

| Step | Action | Source |
|------|--------|--------|
| 1 | Collect **40 Splinters of Atiesh** | Random Naxx trash + boss drops; ~1% trash, ~10% Sapphiron/KT |
| 2 | Combine 40 Splinters → **Frame of Atiesh** | Auto-combine |
| 3 | Hand Frame to **Anachronos** (Caverns of Time entrance, Tanaris) | Requires **Brood of Nozdormu** rep Neutral (AQ40 chain start) |
| 4 | Anachronos sends to Naxxramas Frostwyrm Lair → kill **Kel'Thuzad** for **Staff Head of Atiesh** | KT 100% drop after Anachronos quest active |
| 5 | Travel to **AQ40 final fight** → kill **C'Thun** for **Base of Atiesh** | C'Thun 100% drop after Anachronos quest active |
| 6 | Return to Stratholme **Festival Lane** → defeat the demon **Atiesh** (lvl 60 elite) | Final purification fight |
| 7 | Receive **Atiesh, Greatstaff of the Guardian** | Cosmetic varies by class: green (Druid), red (Mage), white (Priest), blue (Warlock) |

**Eligible classes**: Druid, Mage, Priest, Warlock (cloth/leather casters with mana).

**Stats**: ~10% spell crit aura raid-wide; ~100+ spelldmg + spirit; raid utility from Caster auras emitting from staff.

**Decision-engine rule:** Atiesh is a **multi-raid multi-week chain** requiring Naxx + AQ40 progression simultaneously. Engine should track `Snapshot.Inventory.SplinterOfAtieshCount` per character and dispatch Frostwyrm + AQ40 raid runs accordingly.

---

## Reagent Crafting Costs (Naxx Attune Bundle)

For **Honored** AD rep:

| Reagent | Source | Estimated price |
|---------|--------|-----------------|
| 5x Arcane Crystal | Mining Thorium veins (1% chance per Thorium); AH | ~25-50g per Arcane (varies by server) |
| 2x Nexus Crystal | Enchanting disenchant of L60 epic-quality items; AH | ~30-80g per Nexus (rare) |
| 1x Righteous Orb | Stratholme Live boss drops (Postmaster + Cannon Master); AH | ~10-30g per Orb |
| 60g cash (Honored) / 30g (Revered) / 0g (Exalted) | Quest reward gold | — |
| **Total estimate** | — | **~250-400g** at Honored |

**Decision-engine rule:** if `Snapshot.Reputation.AD < Honored`, attune cost is **infinite** (cannot turn in). Engine should plan AD rep grind first.

If Revered, total cost ~125-200g (half cash + half reagent reduction).

If Exalted, total cost ~80-100g (reagent only).

---

## VMaNGOS / Server Reality Check

> Naxxramas is the **least scripting-complete** raid in vanilla private server land. Many private servers including VMaNGOS have partial implementations as of 2026.

Common boss-script gaps:

| Boss | Common scripting issue (server-dependent) |
|------|-------------------------------------------|
| Anub'Rekhan | Locust Swarm AoE timing; Crypt Guard add behavior |
| Faerlina | Worshipper Mind-Control mechanic (needed for enrage dispel) |
| Heigan | Dance pattern timing (eruption desync vs animation) |
| Razuvious | Death Knight Understudy MC mechanic (often broken) |
| Gothik | Live/Dead side gate timing + portal phase |
| Four Horsemen | Mark stack threat-rotation mechanic |
| Patchwerk | Hateful Strike second-target priority |
| Thaddius | Polarity Shift +/- charge mechanic |
| Sapphiron | Frost Aura damage scaling; Life Drain target selection; Ice Block air-phase |
| Kel'Thuzad | Frost Blast cast bar interrupt; Phase 2/3 transitions; Guardian summons |

**Decision-engine rule:** if `ServerCapabilities.NaxxBoss[<bossName>].Scripted == false`, engine should refuse-pull and emit `RaidGM.AlertNonScriptedBoss(<bossName>)`. The CLAUDE.md project root mentions this as a server-capability flag.

---

## Decision-Engine Rules

1. **Attune precondition**: `AD.Reputation >= Honored` is mandatory; Revered/Exalted reduces gold cost.
2. **Wing-clear order recommendation**: Spider → Plague → Construct → Military → Frostwyrm. Construct (Patchwerk DPS race) defers until raid is geared from Spider+Plague drops.
3. **Frost Resistance gate**: Sapphiron requires 200+ FR per raider. Engine should `Snapshot.Equipment.FrostResistance` per char before raid pull.
4. **Heigan dance encoding**: 4-section floor with 6-7 second eruption sequence. Engine should encode as deterministic waypoint sequence; failure = wipe.
5. **Razuvious MC requirement**: 2 Priests must dedicate MC slots. Engine should fail-fast if `RaidComposition.Class.Priest < 2` and Razuvious-attempt-pull.
6. **Four Horsemen 4-tank rotation**: 4 Plate tanks required (mix of Warrior/Paladin). Engine should encode tank-swap timing per Mark-stack threshold.
7. **Atiesh chain bookkeeping**: track Splinter count per character; dispatch C'Thun raid run when Splinter == 40 and Anachronos quest active.
8. **VMaNGOS Naxx-incomplete flag**: read `ServerCapabilities.NaxxBoss[N]` for each boss. Some bosses may require manual GM intervention (`.die` killscript) to advance the raid.
9. **T3 acquisition rotation**: per-character T3 slot tracking; prioritize wings with missing slots; do not skip wings even if no class drop expected (other classes may need that piece).
10. **Reset-week alignment**: Naxx lockout is 7 days; engine should align raid-clear order with weekly reset (Tuesday US / Wednesday EU). If `WeeklyReset.HoursUntil < 12`, defer Naxx pull to fresh-lockout week.
11. **Brood of Nozdormu rep**: AQ40 entry chain requires Nozdormu Neutral; feeds Atiesh chain step 3. Engine should plan AQ40 rep grind in parallel with Naxx attune.
12. **Pre-raid prep window**: Naxx requires the same world-buff stack as MC/BWL — Rallying Cry + Warchief's Blessing + Songflower + DM Tribute + Zanza. Engine should run pre-raid prep T-90 minutes before pull.

---

## Snapshot Fields Needed

```text
Snapshot.Level == 60                            // hard gate
Snapshot.Class                                  // T3 set + Atiesh class gate
Snapshot.Faction                                // raid composition mix
Snapshot.Attunements.Naxxramas                  // raid entry gate
Snapshot.Reputation.ArgentDawn                  // attune cost gate
Snapshot.Reputation.BroodOfNozdormu             // Atiesh chain step 3 gate
Snapshot.Inventory.Has("FrostResistanceGear")   // Sapphiron pre-pull
Snapshot.Equipment.FrostResistance              // Sapphiron gate >= 200
Snapshot.Inventory.SplinterOfAtieshCount        // Atiesh chain progress
Snapshot.Inventory.FrameOfAtiesh                // 40-Splinter combination signal
Snapshot.Inventory.StaffHeadOfAtiesh            // Anachronos+KT step
Snapshot.Inventory.BaseOfAtiesh                 // C'Thun step
Snapshot.QuestLog.Active.AtieshChain            // legendary chain progress
Snapshot.RaidGroup.Composition.Tanks            // 4-Horsemen 4-tank gate
Snapshot.RaidGroup.Composition.Priests          // Razuvious 2-MC gate
Snapshot.RaidGroup.Composition.AvgItemLevel     // Patchwerk DPS-race gate
Snapshot.WallClock.WeeklyResetTimer             // lockout alignment
Snapshot.WorldBuffs.Active                      // pre-raid buff stack
Snapshot.ServerCapabilities.NaxxBoss[<name>]    // VMaNGOS scripting flag
Snapshot.Equipment.T3Pieces                     // per-character set tracker
Snapshot.RaidGroup.PullState                    // Heigan dance sync state
```

---

## Cross-References

- Naxxramas attune detail: [../attunements/naxxramas.md](../attunements/naxxramas.md)
- Argent Dawn rep: [../reputations/argent-dawn.md](../reputations/argent-dawn.md)
- Other raids: [molten-core.md](molten-core.md), [onyxias-lair.md](onyxias-lair.md), [blackwing-lair.md](blackwing-lair.md), [zul-gurub.md](zul-gurub.md)
- AQ40 (for C'Thun + Base of Atiesh): not yet covered (pending)
- World buffs (pre-raid stack): [../systems/world-buffs.md](../systems/world-buffs.md)
- Consumables (Naxx-tier flasks): [../systems/consumables.md](../systems/consumables.md)
- Class-specific raid prep: [../classes/](../classes/) (per-class file)
- Final bracket leveling: [../sections/06-l50-l60.md](../sections/06-l50-l60.md)
