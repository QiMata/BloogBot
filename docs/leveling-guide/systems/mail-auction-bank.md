---
title: "System — Mail / Auction House / Bank (Economy + Inventory Management)"
patch: "1.12.1 (Drums of War, Sept 2006)"
crawl_date: 2026-05-01
---

# Mail / Auction House / Bank — Economy + Inventory System

The 3-pillar inventory + economy management system. **Mail** sends items + gold between same-account characters (1h delivery, faction-restricted in 1.12). **Auction House** is faction-specific in capital cities (Alliance Stormwind/IF/Darnassus, Horde Org/UC/TB) + neutral cross-faction at goblin ports (Booty Bay/Ratchet/Gadgetzan/Everlook). AH commission ~5% on sale + small listing deposit. **Bank** is per-character 24-slot bottom row + up to 4 expansion bag slots (10s/50s/1g/25g escalating). Cross-character mail bridges bank between alts.

---

## Mail System

### Mechanics

| Field | Value |
|-------|-------|
| **Sender → Recipient** | Same-account characters allowed; opposite-faction characters BLOCKED in 1.12 |
| **Delivery time** | ~1 hour standard |
| **Returned mail** | After 30 days, mail returns to sender |
| **Mailbox locations** | All capital cities + most major hubs |
| **Item slots per mail** | 1 attached item per mail (multiple mails for batch sending) |
| **Gold transfer** | 1 gold transfer per mail attached |
| **Send fee** | ~30c base + per-item cost |
| **COD (Cash on Delivery)** | Yes — recipient pays specified amount on opening |

### Cross-Faction Restriction

Cross-faction mail (Alliance ↔ Horde) is **BLOCKED in 1.12**. Workaround:

| Method | Notes |
|--------|-------|
| **Neutral AH at goblin ports** (Booty Bay/Ratchet/Gadgetzan/Everlook) | Bridge cross-faction trades via AH (separate listings, different listing fees) |
| **Cross-account trading** | Two accounts (one Alliance, one Horde) — out-of-game coordination |

**Decision-engine rule:** for cross-faction trades, route via neutral goblin AH. Engine should encode AH listing strategy.

### Mail Cost Scaling

| Action | Cost |
|--------|------|
| **Send empty mail** (text only) | 30c |
| **Send mail with item** | 30c + per-item base cost |
| **COD setup** | Free for sender; recipient pays specified amount |
| **Returned mail** (after 30 days) | Auto-returned with all items + gold |

---

## Auction House

### Faction-Specific AH Locations

| Faction | AH Location | Coverage |
|---------|-------------|----------|
| **Alliance AH** | Stormwind Trade District, Ironforge Commons, Darnassus Tradesmen's Terrace | Cross-Alliance shared listings |
| **Horde AH** | Orgrimmar Valley of Strength, Undercity Trade Quarter, Thunder Bluff High Rise | Cross-Horde shared listings |
| **Neutral AH** | Booty Bay, Ratchet, Gadgetzan, Everlook | Cross-faction (separate listings, different fees) |

**Decision-engine cue:** Alliance/Horde AHs are **separate listings**. Cross-faction trades require neutral goblin AH (Booty Bay primary).

### Listing Mechanic

| Field | Value |
|-------|-------|
| **Listing duration** | 12 hours / 24 hours / 48 hours |
| **Listing fee** (deposit) | Small (~1-50s per item, scales with item value) |
| **Sale commission** | ~5% deducted on sale (Alliance/Horde standard); ~15% at neutral goblin AHs (deeper cut) |
| **Buyout vs Bid** | Both supported |
| **Listing cap** | No hard cap (effectively unlimited per character) |

### Auction Commission Comparison

| AH Tier | Commission |
|---------|------------|
| **Alliance/Horde city AH** | 5% commission |
| **Neutral goblin AH** | ~15% commission (higher for cross-faction trades) |
| **Steamwheedle Cartel rep** | Honored = 5% AH discount; Revered = better discount |

**Decision-engine rule:** for in-faction trades, prefer Alliance/Horde city AH for lower commission. Cross-faction trades require goblin AH (15% cost).

### Listing Strategy

| Tier | Recommended duration | Notes |
|------|----------------------|-------|
| **High-volume reagent (Mooncloth, Black Lotus)** | 24h | Peak sales window |
| **Mid-tier crafted gear** | 48h | Slower turnover |
| **Low-volume rare items** | 48h | Maximize visibility |
| **Daily auction-flip** | 12h | Quick turnover for AH-flippers |

---

## Bank System

### Bank Slots

| Section | Slots | Cost |
|---------|-------|------|
| **Default bottom row** | 24 slots | Free |
| **Bag slot 1** | +1 expansion bag | 10s (one-time) |
| **Bag slot 2** | +1 expansion bag | 50s (one-time) |
| **Bag slot 3** | +1 expansion bag | 1g (one-time) |
| **Bag slot 4** | +1 expansion bag | 25g (one-time) |
| **Total maximum** | 24 + 4 bags = up to ~50-70+ slots (with high-tier bags) | ~26.5g cumulative |

**Decision-engine cue:** at L40+ with first mount fund, allocate ~30g for full bank slot expansion. Engine should plan bag slot purchases per character.

### Bank Locations

| Location | Faction |
|----------|---------|
| **Stormwind, Ironforge, Darnassus** | Alliance |
| **Orgrimmar, Undercity, Thunder Bluff** | Horde |
| **Booty Bay, Ratchet, Gadgetzan, Everlook** | Neutral cross-faction |

### Cross-Character Bank Bridging

The **mail system bridges banks between same-account alts**:

| Step | Action |
|------|--------|
| 1 | Character A sends item to Character B via mail (1h delivery) |
| 2 | Character B opens mail at any mailbox |
| 3 | Effective cross-character bank transfer |

**Decision-engine rule:** mail-bridge cross-character bank for inventory rotation. Engine should plan ~5-15 mails per multi-alt session for crafting reagent distribution.

---

## Bag Tier Progression (Bag Size Upgrades)

Bags increase character + bank inventory capacity beyond default 16-slot starter bags.

| Tier | Bag size | Source |
|------|----------|--------|
| **6-slot bags** | Vendor-bought (~50c) | Default starter |
| **8-slot bags** | Quest reward / vendor (~50s) | Mid-game |
| **10-slot bags** | Vendor / drop (~5g) | Mid-game |
| **12-slot bags** | Tailoring crafted (~Mooncloth required) | Tailoring profession |
| **14-slot bags** | Tailoring crafted | Mid-game tailor |
| **16-slot bags** (Mooncloth Bag) | **Tailoring 280** + Mooncloth + Cured Rugged Hide | Top non-specialty |
| **18-slot bags** (Onyxia Hide Backpack) | Onyxia raid drop (Onyxia Hide reagent) | Raid-tier |
| **30-slot Soul Pouch / Felcloth Bag** | Tailoring (Felcloth Bag) — **Warlock-only** soul shard bag | Specialty |
| **20-slot Herb Pouch / Wizard Oil Bag** | Various profession-themed bags | Profession-specific |

**Decision-engine rule:** at L60 raid-readiness, target 18-slot Onyxia Hide Backpack + 16-slot Mooncloth Bag combo. Engine should plan multi-week raid week + Tailoring 280+ progression.

---

## Mailbox + AH + Bank Strategy (Decision-Engine)

### Per-Character Daily Routine

| Time | Action |
|------|--------|
| **Login** | Check mail at mailbox (~30s) |
| **AH check** | Browse AH listings for crafting reagents + price arbitrage |
| **Bank rotation** | Move L60-irrelevant items to bank (clear bag for daily content) |
| **Daily mail send** | Send raid reagents to crafting alts |

### Cross-Account Multi-Char Strategy

1. **Hauling alt** (low-level character with Tailoring/Engineering) for AH-flipping
2. **Bank alt** (parked at Stormwind/Org for max bank slot access)
3. **Cross-faction alt** (Booty Bay/Ratchet/Gadgetzan/Everlook for cross-faction trades)
4. **Crafting alts** (per-profession specialty alt for AH supply)

**Decision-engine rule:** at L60, optimize multi-alt economy via:
- Hauling alt for AH visibility
- Bank alt for storage
- Crafting alts for profession output
- Mail-bridge for cross-alt transfers

---

## Decision-Engine Rules

1. **Mail cross-faction restriction**: Alliance ↔ Horde mail BLOCKED in 1.12. Engine should encode neutral goblin AH as cross-faction trade route.
2. **AH commission scaling**: 5% Alliance/Horde city AH vs 15% neutral goblin AH. Engine should optimize listing location.
3. **Bank slot expansion**: ~26.5g cumulative for full 4-slot expansion. Engine should plan ~30g reserve at L40+.
4. **Bag tier progression**: 16-slot Mooncloth Bag (Tailoring 280) + 18-slot Onyxia Hide Backpack (Onyxia raid) for endgame. Engine should plan Tailoring 280+ + Onyxia raid week.
5. **Cross-character bank bridge**: mail system bridges alts (1h delivery). Engine should plan multi-mail batches.
6. **Listing duration optimization**: 24h for high-volume; 48h for slow-turnover; 12h for daily flip.
7. **Steamwheedle Cartel rep**: Honored unlocks 5% AH discount. Engine should plan rep grind for AH-active characters.
8. **Multi-alt economy strategy**: hauling/bank/crafting alts for endgame economy. Engine should pre-flag alt roles.

---

## Snapshot Fields Needed

```text
Snapshot.Mail.Inbox.Count                         // pending mail count
Snapshot.Mail.Inbox.Items                         // attached items list
Snapshot.Bank.SlotsUsed                           // current usage
Snapshot.Bank.BagSlotsExpanded                    // 0-4 expansion bags purchased
Snapshot.Bank.TotalCapacity                       // calculated total
Snapshot.AH.ActiveListings                        // outgoing AH listings
Snapshot.AH.PendingSales                          // sold items awaiting collection
Snapshot.Inventory.BagSlots                       // character bag inventory
Snapshot.Inventory.BagSizes                       // each equipped bag size
Snapshot.Reputation.SteamwheedleCartel.Honored    // 5% AH discount signal
Snapshot.Faction                                  // determines Alliance/Horde AH access
Snapshot.Position.Zone == "<capital city>" OR == "BootyBay/Ratchet/Gadgetzan/Everlook"  // mailbox/AH/bank access signal
Snapshot.Gold                                     // bag slot expansion + AH listing fee reserve
Snapshot.Profession.Tailoring.Skill               // Mooncloth Bag crafting signal
Snapshot.Inventory.Has("OnyxiaHideBackpack")      // raid-tier bag signal
Snapshot.Inventory.Has("FelclothBag")             // Warlock soul shard bag signal
```

---

## Cross-References

- Faction cities (AH/Bank locations): [../zones/cities/](../zones/cities/)
- Goblin neutral hubs (AH access): [../zones/cities/](../zones/cities/) (pending)
- Tailoring (Mooncloth Bag crafting): [../professions/tailoring.md](../professions/tailoring.md)
- Onyxia Hide Backpack: [../raids/onyxias-lair.md](../raids/onyxias-lair.md)
- Steamwheedle Cartel rep (5% AH discount): [../reputations/steamwheedle-cartel.md](../reputations/steamwheedle-cartel.md)
- Flight Paths (mailbox + AH + bank in capitals): [flight-paths.md](flight-paths.md)
- Mounts (gold reserve allocation): [mounts.md](mounts.md)
