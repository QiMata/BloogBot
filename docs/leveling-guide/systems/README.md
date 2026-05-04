# Systems

> **Pass 1 placeholder.** Cross-cutting game systems. Pass 10.

## Planned files (pass 10)

| File | Topic |
|---|---|
| `talents.md` | 51-point trees in 1.12; 1 talent point per level from 10-60; 51-point capstones; respec cost progression (1g → 5g → 10g → … → 50g cap, decays 5g/30 days) |
| `weapon-skill.md` | 1 skill point per landed swing/parry/dodge; weapon-skill quests at racial-specific values; cap = 5 × level (300 at 60); off-hand = main-hand for purposes; ranged weapons separate skill bucket |
| `spell-ranks.md` | Roughly one rank per ~6 levels for most spells; trainer cost progression (~12s rank 1 → ~80s rank 7); ranks-vs-mana-efficiency for healers (downranking) |
| `mounts.md` | Apprentice riding 75 @ lvl 40 (60% speed); Journeyman riding 150 @ lvl 60 (100% speed). Costs: ~90s + 80s = ~170s at 40 (no rep discount); ~600g + 400g = ~1000g at 60. Faction discount tiers: Friendly 5%, Honored 10%, Revered 15%, Exalted 20%. Class epic mounts (Charger 1000g/quest, Dreadsteed 800g+/quest). |
| `flight-paths.md` | Per-faction flight master locations; cost; transitive paths (some FPs are gated behind rep — Aerie Peak Alliance, Revantusk Horde, Booty Bay neutral) |
| `tradeskill-flying-nodes.md` | Black Lotus spawns (Burning Steppes, EPL, Silithus, Winterspring); Arcane Crystal spawns (Un'Goro, Burning Steppes, Winterspring, Eastern Plaguelands); Ghost Mushroom (Maraudon); fishing pools (Stranglethorn, Silithus, Azshara, Felwood) |
| `mail-auction-bank.md` | Mail (1h delivery for cross-faction-side same-account; instant for same-faction; 30-day inactivity discard); Auction House (5% deposit, 5% cut, faction-specific ±neutral; goblin AH in Booty Bay/Gadgetzan/Everlook); Bank (24 slots + 6 bag slots) |
| `buff-system.md` | **Limit 16 buffs** in 1.12.1 (NOT 32 — that's TBC+); buff prioritization rules; consumables vs class buffs; world buffs (Onyxia head 2h, Nef head 2h, Rend Blackhand 2h, Songflower Serenade 1h, Dire Maul tribute Eldreth/Mol'dar/Slip'kik 2h) |
| `consumables.md` | Major Mana Potion, Major Healing Potion, Greater Stoneshield Potion, Flask of the Titans (Stamina), Flask of Distilled Wisdom (Mana), Flask of Supreme Power (SP), Flask of the Mongoose (AP+crit), Elixir of the Mongoose, Elixir of Greater Defense, Juju Power/Might/Flurry/Guile, Spirit of Zanza, Sheen of Zanza, Swiftness of Zanza, Mighty Rage Potion, Demonic Rune, Dark Rune, Goblin Sapper Charge, Thistle Tea (Rogue) |
| `resistance-gear.md` | **Fire** for MC/BWL Ragnaros/Vael; **Frost** for AQ40 Princess Huhuran (no — that's Nature; Frost is for Naxx Sapphiron); **Nature** for AQ40 Huhuran + Princess Yauj/Vem; **Shadow** for Naxx Loatheb. Per-school target res values (FR ~315 unbuffed, NR ~250, SR ~250). Crafted resistance gear sources. |
| `faction-city-services.md` | Per-city: auctioneer, banker, class trainers, profession trainers, weapon master, weapon-skill trainer, mailbox, mage portal trainer, riding trainer, hunter pet trainer |
| `dual-talent-and-respec.md` | No dual-spec in 1.12.1 — that's Wrath. Single spec, respec-cost decay (5g per 30 days, cap 50g). |
| `played-time-economy.md` | XP/hour curves, gold/hour curves, /played-to-60 baseline (~5-7 days /played for an experienced solo leveler), profession 1-300 mat-cost economics. **VMaNGOS-specific XP rates** if rates ≠ 1x. |

## Standard sections per system file (pass 10 contract)

1. **Mechanics primer** (the rules of the system)
2. **Per-bracket relevance** (when does this system matter?)
3. **Per-class relevance** (does this system have class-specific quirks?)
4. **Per-server-config divergence** (private servers tune some of these — flag them)
5. **Decision-Engine Rules**
6. **Snapshot Fields Needed**
