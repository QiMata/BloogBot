---
title: "Profession — First Aid (Secondary)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - https://warcraft.wiki.gg/wiki/First_Aid
crawl_date: 2026-05-01
---

# First Aid — 1-300 Secondary Profession, Bandage Tiers, Triage Quest

**Secondary profession** — does NOT count toward 2-primary cap. All characters should have it. **No specialty in 1.12.** Pair-feeds character downtime healing via cloth bandages. Tier ladder: Linen → Wool → Silk → Mageweave → Runecloth (each with "Heavy" upgrade variant). Top bandage: **Heavy Runecloth Bandage** (~2000 HP heal in 8s). **Triage quest at skill 150** required to unlock Expert/Artisan path. **"Recently Bandaged" 60s debuff** prevents stacking. Crucial for raid downtime + soloing.

---

## Quick Facts

| Field | Value |
|-------|-------|
| Profession type | **Secondary** (doesn't count against 2-primary limit) |
| Tier caps | Apprentice 75 → Journeyman 150 → Expert 225 → Artisan 300 |
| Specialty | **None in 1.12** |
| Pair-supply | **Tailoring cloth** (Linen/Wool/Silk/Mageweave/Runecloth) — feeds bandage crafting |
| Top bandage | **Heavy Runecloth Bandage** (~2000 HP, 8s channel) |
| Triage quest | Required at skill 150 to unlock Expert (Doctor Gustaf VanHowzen / Doctor Gregory Victor) |
| Combat behavior | Bandage **breaks on damage** — out-of-combat / safe-zone use only |
| Recently Bandaged debuff | 60s lockout after applying — cannot stack |
| Trainer | Capital cities + Triage trainers in Theramore/Hammerfall |

---

## Skill Progression (1-300)

| Skill | Range | Bandage tier | HP heal (8s channel) |
|-------|-------|--------------|---------------------|
| 1-40 | Apprentice | **Linen Bandage** | ~66 HP |
| 40-80 | Apprentice → Journeyman (75 cap) | **Heavy Linen Bandage** | ~114 HP |
| 80-115 | Journeyman | **Wool Bandage** | ~161 HP |
| 115-150 | Journeyman | **Heavy Wool Bandage** | ~301 HP |
| 150 | **Triage Quest required** to advance | — | — |
| 150-180 | Expert (Expert quest at 125 + Triage at 150) | **Silk Bandage** | ~400 HP |
| 180-210 | Expert | **Heavy Silk Bandage** | ~640 HP |
| 210-240 | Expert | **Mageweave Bandage** | ~800 HP |
| 240-260 | Expert → Artisan (Artisan quest at 200) | **Heavy Mageweave Bandage** | ~1104 HP |
| 260-290 | Artisan | **Runecloth Bandage** | ~1360 HP |
| 290-300 | Artisan | **Heavy Runecloth Bandage** | **~2000 HP** |

**Decision-engine rule:** First Aid grind aligns with Tailoring cloth tier. Bandage 8-second channel is the standard out-of-combat heal mechanic. Engine should always-cast bandage when `Snapshot.Health < Snapshot.MaxHealth * 0.5 && InCombat == false && RecentlyBandaged == false`.

---

## Triage Quest (Skill 150 Gate)

**Required at skill 150** to unlock Expert/Artisan tiers.

| Step | Action | Source |
|------|--------|--------|
| 1 | Reach **First Aid skill 150** | Apprentice/Journeyman cap |
| 2 | Travel to: **Doctor Gustaf VanHowzen** (Theramore Isle, Dustwallow) for Alliance; **Doctor Gregory Victor** (Hammerfall, Arathi Highlands) for Horde | Bracket-aligned zone (L35+) |
| 3 | Receive **Triage Test** quest | One-time |
| 4 | Heal 5 patients (Heavy Mageweave Bandage usage) within 6 minutes | Skill check |
| 5 | Reward: **Triage** quest reward (~20 Heavy Runecloth Bandage starter pack + skill book) | Triage achievement |
| 6 | Now able to train Expert/Artisan First Aid via capital trainer | Skill cap unlocked |

**Decision-engine rule:** Triage quest is **mandatory** for First Aid 150+. Engine should plan visit to Theramore (A) or Hammerfall (H) at character L35+ alongside Dustwallow/Arathi questing.

---

## Anti-Venom Progression

| Recipe | Skill | Reagent | Effect |
|--------|-------|---------|--------|
| **Anti-Venom** | 80 | 1 Small Venom Sac (spiders) + 1 Linen Cloth | Cures 1 poison effect (level ≤25) |
| **Strong Anti-Venom** | 130 | 1 Large Venom Sac (lvl 30+ spiders) + 1 Mageweave Cloth | Cures 1 poison (level ≤45) |
| **Powerful Anti-Venom** | 290 | 1 Huge Venom Sac (lvl 60 spiders/silithid) + 1 Runecloth | **Cures any poison; reagent for some Alchemy recipes** |

**Decision-engine rule:** Anti-Venom is **the** poison-counter for raids vs. Naxx Spider Wing + Maexxna + Ouro encounters. Engine should stockpile Powerful Anti-Venom for raid weeks.

---

## "Recently Bandaged" Debuff (60s Lockout)

When a character applies a bandage:

- 60-second debuff prevents same-target re-bandage
- **NOT 60-second on caster** — the **debuff is on the target**, so a healer cannot bandage themselves twice in 60s
- Multi-character bandaging works (e.g., 5 raiders all bandage themselves on different targets)
- "Recently Bandaged" is the only Major-cooldown bandaging blocker

**Decision-engine rule:** engine should track `Snapshot.Buffs.RecentlyBandaged` per character; never attempt re-bandage during the 60s window.

---

## Bandage Mechanic in Combat

Bandages apply via **8-second channel**. The channel breaks on:
- Any damage taken (stops mid-channel)
- Spell interruption (e.g., Earth Shock)
- Movement
- Stuns/silences

**Practical use cases:**

| Scenario | Bandage advisable? |
|----------|-------------------|
| Out-of-combat downtime (between mobs) | Always |
| Safe-zone within combat (mage Sheep target) | Yes if untouchable |
| Soloing low-HP situations | Yes if mob can't reach you (Frost Trap, Charge knockback) |
| Raid downtime between bosses | Always — primary use case |
| Active raid combat | Rarely — channel-breaks on AoE |

**Decision-engine rule:** in-combat bandaging is rarely useful. Engine should reserve bandages for:
- Post-combat downtime (always-on rule)
- Pre-pull raid setup (every raider bandages to full HP before pull)
- Solo emergency (e.g., L60 Hunter pet-MIA situation)

---

## Trainer Locations

| Tier | Trainer | Location |
|------|---------|----------|
| Apprentice (1-75) | Multiple per faction | Stormwind / Ironforge / Darnassus / Org / Undercity / Thunder Bluff |
| Journeyman (50-150) | Same trainer | Capital cities |
| Triage Quest @ 150 | **Doctor Gustaf VanHowzen** (Theramore, Dustwallow) for Alliance; **Doctor Gregory Victor** (Hammerfall, Arathi) for Horde | Mid-game outdoor |
| Expert (post-Triage) (125-225) | Capital trainer | Capital cities |
| Artisan (200-300) | Capital trainer | Capital cities |
| Master | Drop / quest reward (none in 1.12) | — |

---

## Decision-Engine Rules

1. **First Aid is always-on**: secondary profession; engine should default First Aid on every character (no opportunity cost).
2. **Cloth-tier sync**: First Aid grind aligns with Tailoring cloth tier. Engine should reserve ~25% of cloth pile for bandage crafting.
3. **Triage quest at 150**: mandatory at skill 150; engine should plan visit at L35+ to Theramore (A) or Hammerfall (H).
4. **Bandage routine post-combat**: every kill where character HP ≤ 50% triggers post-combat bandage. Engine should auto-cast.
5. **Pre-raid prep bandage**: at raid pull -10s, all raiders should bandage to full HP. Engine should encode pre-pull bandage as raid invariant.
6. **Anti-Venom stockpile for Naxx**: Powerful Anti-Venom for Spider Wing + Maexxna. Engine should stockpile 30-50 Powerful Anti-Venom per raid week.
7. **"Recently Bandaged" tracking**: engine reads `Snapshot.Buffs.RecentlyBandaged` to gate bandage attempts.
8. **Class synergy**: every character benefits; **Hunters benefit most** (no self-heal aside from bandage). Engine should bias Hunter alts to First Aid 300 by L40.
9. **Cooking + First Aid + Fishing**: 3 secondary professions; engine should grind all 3 to 300 in parallel during downtime.

---

## Snapshot Fields Needed

```text
Snapshot.Profession.FirstAid.Skill              // 1-300 grind progression
Snapshot.Profession.FirstAid.RecipesLearned     // bandage-track per tier
Snapshot.Inventory.{LinenBandage, HeavyLinenBandage, WoolBandage, HeavyWoolBandage, SilkBandage, HeavySilkBandage, MageweaveBandage, HeavyMageweaveBandage, RunecothBandage, HeavyRunecothBandage}  // bandage stockpile
Snapshot.Inventory.{AntiVenom, StrongAntiVenom, PowerfulAntiVenom}  // poison-counter stockpile
Snapshot.QuestLog.Completed.Contains("TriageQuest")  // 150-skill gate
Snapshot.Buffs.RecentlyBandaged                  // 60s lockout state
Snapshot.Profession.Tailoring.Skill              // pair-supply for cloth
Snapshot.Class                                   // Hunter/Rogue (no self-heal) bias
Snapshot.RaidGroup.PreRaidPrep.BandageReady     // pre-pull invariant
Snapshot.Health / Snapshot.MaxHealth             // bandage-trigger threshold
Snapshot.InCombat                                // bandage-channel safety
```

---

## Cross-References

- Tailoring (cloth supply): [tailoring.md](tailoring.md)
- Cooking (sister secondary): [cooking.md](cooking.md)
- Fishing (sister secondary): [../professions/](../professions/) — Fishing file pending
- Naxxramas (Anti-Venom critical): [../raids/naxxramas.md](../raids/naxxramas.md)
- AQ40 (Anti-Venom for Ouro): [../raids/ahn-qiraj-temple.md](../raids/ahn-qiraj-temple.md)
- Triage quest in Dustwallow Marsh / Arathi Highlands: [../sections/04-l30-l40.md](../sections/04-l30-l40.md)
- Hunter (no self-heal class — First Aid heavy reliance): [../classes/hunter.md](../classes/hunter.md)
- Rogue (no self-heal — First Aid important): [../classes/rogue.md](../classes/rogue.md)
- Other professions: [alchemy.md](alchemy.md), [blacksmithing.md](blacksmithing.md), [enchanting.md](enchanting.md), [engineering.md](engineering.md), [tailoring.md](tailoring.md), [leatherworking.md](leatherworking.md), [mining.md](mining.md), [herbalism.md](herbalism.md), [skinning.md](skinning.md), [cooking.md](cooking.md)
