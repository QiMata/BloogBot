---
title: "System — Spell Ranks (Per-Class Rank Progression)"
patch: "1.12.1 (Drums of War, Sept 2006)"
crawl_date: 2026-05-01
---

# Spell Ranks — Per-Class Rank Progression L1-60

Every class spell in 1.12.1 has multiple **ranks** (typically 3-9 per spell), each unlocked at specific character levels via class trainer purchase. Rank-ups scale damage/healing/duration based on class level. **Trainer cost scales** with rank tier (Apprentice ~10s → Master ~5-50g per rank). Some ranks create "**power spike** windows" — bracket-defining unlocks like **Hunter Aimed Shot** (L20), **Mage Polymorph** (L20), **Druid Travel Form** (L30), **Warlock Felhunter** (L30), **Plate Armor** (L40 Warrior/Paladin), **Mage Portal** (L40), **Priest Lightwell** (L60 Holy), **Class Epic** (L60). Engine should align spell-rank purchases with bracket-end gold reserves.

---

## Rank Cost Scaling

| Tier | Levels covered | Approximate cost per rank-up |
|------|---------------|------------------------------|
| Apprentice | 1-10 | 10s - 1g per rank |
| Journeyman | 10-20 | 1-5s per rank (lower tier) → 1-3g for high-tier rank-2/3 |
| Expert | 20-40 | 1-5g per rank-up |
| Artisan | 40-60 | 5-15g per rank-up |
| Master | 60+ | 10-50g per rank-up (raid spells) |

**Decision-engine rule:** at bracket transitions, ensure `Snapshot.Gold >= 1g (L1-20)`, `>= 5g (L20-40)`, `>= 25g (L40-60)` for rank-up reserve before talent rebinds.

---

## Spell Rank Power Spikes (Bracket-Defining)

### L1-10 (Starter Bracket)

| Class | Power spike spell | Notes |
|-------|-------------------|-------|
| Warrior | Heroic Strike rank 1 | Standard |
| Paladin | Holy Light rank 1 | Standard |
| Hunter | Auto Shot rank 1 | Pre-Tame Beast L10 |
| Rogue | Sinister Strike rank 1 (L4) | Combat opener |
| Priest | Smite rank 1 + Lesser Heal | Standard |
| Mage | Frostbolt rank 1 (L4) | Caster baseline |
| Warlock | Shadow Bolt rank 1 | Standard |
| Druid | Wrath rank 1 + Healing Touch rank 1 | Standard |
| Shaman | Lightning Bolt rank 1 | Standard |

### L10-20 (Class Identity Bracket)

| Class | Power spike spell |
|-------|-------------------|
| Hunter | **Tame Beast** L10 + pet |
| Warlock | **Voidwalker** L10 |
| Druid | **Bear Form** L10 (Moonglade chain) |
| Shaman | **Fire Totem** L10 |
| Paladin | **Verigan's Fist** L12 (2H mace) |
| Mage | **Polymorph** L20 (CC) |
| Hunter | **Aimed Shot** L20 |
| Priest | **Mind Control** L20+ (race-specific) |
| Warrior | **Berserker Stance** L30 (cross-bracket) |

### L20-30 (Mid-Bracket)

| Class | Power spike spell |
|-------|-------------------|
| Druid | **Travel Form** L30 (40% movement, indoor-ok) |
| Shaman | **Air Totem** L30 + **Ancestral Spirit (resurrection)** L30 |
| Warlock | **Felhunter** L30 |
| Warrior | **Berserker Stance** L30 |
| Hunter | **Aspect of the Pack** L44 (group speed buff) |
| Paladin | **Hammer of Wrath** L44 (sub-20% execute) |

### L30-40 (Mount Bracket)

| Class | Power spike spell |
|-------|-------------------|
| All | **Apprentice Riding 75 + Mount** L40 |
| Warrior/Paladin | **Plate Armor** L40 (auto-trained) |
| Hunter/Shaman | **Mail Armor** L40 (auto-trained) |
| Mage | **Portal** L40 (group teleport) |
| Paladin | **Warhorse** L40 (free mount via class quest) |
| Warlock | **Felsteed** L40 (free mount via class quest) |

### L40-50 (Specialty Bracket)

| Class | Power spike spell |
|-------|-------------------|
| Warlock | **Demonic Sacrifice** talent capstone (Demo 31-pt) |
| Druid | **Innervate** L40 (raid utility) |
| Hunter | **Aspect of the Wild** L40 (group nature res) |
| Paladin | **Holy Shield** L40 (Prot talent) |
| Mage | **Polymorph: Pig** book (Hillsbrad cosmetic) |
| Mage | **Stave of Equinex** L50 (Sunken Temple class quest) |
| Priest | **Eye of the Dead** L50 (Sunken Temple class trinket) |
| Druid | **Pristine Hide of the Beast** L50 (class quest) |

### L50-60 (Endgame Bracket)

| Class | Power spike spell |
|-------|-------------------|
| All | **Class Epic Chain L60** (Pal Charger / Wlk Dreadsteed / Hun Rhok'delar / Pri Benediction / War Quel'Serrar / Mage Robe of the Archmage) |
| Mage | **Headmaster's Charge** Scholo class staff |
| Warrior | **Quel'Serrar** L60 epic 1H sword |
| Paladin | **Charger** L60 epic mount (free) |
| Warlock | **Dreadsteed** L60 epic mount (free) |
| Hunter | **Rhok'delar + Lok'delar** L60 epic bow + staff |
| Priest | **Benediction/Anathema** L60 epic staff |

---

## Per-Class Rank Ladder Examples

### Mage Frostbolt (Iconic Caster Rank Ladder)

| Rank | Level | Approximate damage |
|------|-------|---------------------|
| 1 | 4 | ~30 damage |
| 2 | 6 | ~50 |
| 3 | 12 | ~70 |
| 4 | 18 | ~120 |
| 5 | 24 | ~190 |
| 6 | 30 | ~270 |
| 7 | 36 | ~370 |
| 8 | 42 | ~480 |
| 9 | 48 | ~600 |
| 10 | 54 | ~750 |
| 11 | 60 | ~900 |

### Priest Lesser Heal → Heal → Greater Heal

| Spell | Rank 1 Level | Notes |
|-------|--------------|-------|
| Lesser Heal | L1 | Trained automatically |
| Heal | L16 | Mid-tier heal |
| Greater Heal | L40 | Top-tier heal at L60 (Greater Heal Rank 4 at L60) |
| **Lightwell** | L60 (Holy talent 31-pt) | Conditional caster heal ground spawn |

### Hunter Aimed Shot

| Rank | Level | Damage |
|------|-------|--------|
| 1 | 20 | ~80 damage |
| 2 | 28 | ~150 |
| 3 | 36 | ~270 |
| 4 | 44 | ~440 |
| 5 | 52 | ~600 |
| 6 | 60 | ~800 |

### Warlock Shadow Bolt

| Rank | Level | Notes |
|------|-------|-------|
| 1 | 1 | Default |
| 2 | 6 | Tier-up |
| 3-10 | Up to L60 | Top rank ~600+ damage |

---

## Trainer Cost Scaling (Approximate)

| Bracket | Ranks per bracket (typical) | Cumulative gold reserve recommendation |
|---------|------------------------------|---------------------------------------|
| L1-10 | 5-15 ranks | ~1g cumulative |
| L10-20 | 10-20 ranks | ~3-5g cumulative |
| L20-30 | 15-25 ranks | ~10-15g cumulative |
| L30-40 | 20-30 ranks | ~25-35g cumulative |
| L40-50 | 15-20 ranks | ~50-75g cumulative |
| L50-60 | 10-15 ranks | ~100-150g cumulative |

**Decision-engine rule:** before talent reset (~5-50g cost depending on prior resets), ensure rank-up costs are paid first. Engine should `Snapshot.Gold` check before talent action.

---

## Decision-Engine Rules

1. **Rank-up always-attempt at trainer**: every 3-5 levels, rank-up available spells. Engine should auto-rank during city visits.
2. **Power spike priority**: bracket-defining spells (Mage Polymorph L20, Druid Travel Form L30, Plate Armor L40, Class Epic L60) require dedicated quest+gold investment.
3. **Cost-scaled gold reserve**: enforce per-bracket gold reserves before rank-up + talent resets.
4. **Class quest spell unlocks**: Hunter Tame Beast L10, Warlock Voidwalker L10, Druid Bear Form L10, Shaman Fire Totem L10 — class quests, not trainer purchases.
5. **Auto-armor unlock**: Plate (Warrior/Pal L40) + Mail (Hunter/Sham L40) auto-trained, no quest. Engine should detect auto-unlock and re-equip.
6. **Spell book reagents**: Mage Polymorph: Pig (Hillsbrad chain), Mage Polymorph rank 4 trainer cost. Engine should track reagent + cost.
7. **Talent rebinds dependency**: spell ranks must be purchased before talent allocation if dependency exists.

---

## Snapshot Fields Needed

```text
Snapshot.Class                                    // class spell rank routing
Snapshot.Level                                    // bracket gate
Snapshot.Spells.RankPurchased                     // per-spell rank tracking
Snapshot.Gold                                     // rank-up cost reserve
Snapshot.Position.Zone == "<capital city>"        // trainer access signal
Snapshot.Talents.PointsSpent                      // talent dependency check
Snapshot.Equipment.PlateArmor.Equipped            // L40 auto-unlock signal
Snapshot.Equipment.MailArmor.Equipped             // L40 auto-unlock signal
Snapshot.Spells.Has("ClassEpicSpell")             // L60 epic chain status
```

---

## Cross-References

- All bracket sections: [../sections/](../sections/) per-bracket files
- Talents (cross-system): [talents.md](talents.md)
- Class quests (per-spell unlock): [../classes/](../classes/) per-class files
- Class trainers per city: [../zones/cities/](../zones/cities/) per-city files
- Mounts (L40+ spells trained): [mounts.md](mounts.md)
