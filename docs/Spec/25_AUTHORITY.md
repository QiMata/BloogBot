# Spec 25 — Authority Model

> Schema source: [`docs/methodology/08_server_vs_client_authority.md`](../../../docs/methodology/08_server_vs_client_authority.md)
> (monorepo-root methodology) + [`docs/PCE_STANDARD.md`](../../../docs/PCE_STANDARD.md) §15.
> Read before designing snapshot cadence, optimistic-action policy, or any
> retry/resync branch. The classification here cascades into every later
> decision about how a Task proves completion.

## Dominant model: CLIENT-authoritative (Vanilla WoW 1.12.1)

**The vmangos/MaNGOS server owns world state, but the WoW 1.12.1 client is
trusted with the common-case combat and movement math.** The client computes
swing tables, dodge/parry/miss, threat, and position locally and *announces*
the result; the server validates, broadcasts to other clients, and corrects
only on the exception path (rubber-band, anti-cheat). For the cases the bot
cares about, the client's in-memory state is correct the moment the client
decides to act — the bot does **not** wait for a confirming server packet
before reading the outcome.

This is the inverse of FFXI (server-authoritative, packet-gated) and of D2
single-player (shared in-process struct). The identification test from the
methodology §3 resolves at question 2: *for combat actions, the client's
in-memory HP / target / buff state updates before any inbound server packet is
observed.* → **client-authoritative**.

Practical baseline (per methodology §4):

- **FG memory is the source of truth.** The FG Object Manager exposes live HP,
  threat, target, buff, and position; the bot reads these directly. BG must
  replay enough client-side math to match those reads (PCE rule R15 / `Spec/00`
  invariant "FG is ground truth", see [`07_PHYSICS.md`](07_PHYSICS.md)).
- **Optimistic actions are the default.** A Task is complete when local client
  state reflects the action — no roundtrip required. Long-running effects
  (cast bar, channel) are the pessimistic exception, gated on the local cast
  state / expected-duration elapse.
- **DecisionEngine ticks on time, not on packets.** A fixed 100–200 ms cadence
  against live Object Manager reads is the natural model. "Wait for server
  confirmation" is an anti-pattern here — it stalls the bot. (Contrast the WoW
  snapshot tick already specified at 10 Hz in [`01_ARCHITECTURE.md`](01_ARCHITECTURE.md).)
- **Server correction is the recovery branch, not the main path.** Rubber-band
  / forced-position / forced-state packets trigger resync logic framed as
  exception handling.
- **Anti-cheat (Warden) is part of the threat model** precisely *because* the
  client is trusted with state — the server's defence is to scan the client.

WoW Vanilla is, strictly, a **hybrid**: position/movement is the loudest
client-authoritative case (the client announces moves via `MSG_MOVE_*`), while
inventory, loot, trade, and the *authoritative* combat outcome are validated
and committed server-side. The per-action-class table below is the binding
breakdown; each Task type carries a single `OPTIMISTIC` / `PESSIMISTIC` tag and
the BotRunner routes completion-detection accordingly.

## Per-action-class authority table

| Action class | Authoritative side | Task tag | Practical consequence for the bot |
|---|---|---|---|
| **Movement** | client (server reconciles) | `OPTIMISTIC` | Client-side prediction: FG/BG integrate position locally and announce via `MSG_MOVE_*` (C→S state announcements). Position is correct in client memory immediately. Server can correct via `MSG_MOVE_TELEPORT_ACK` (16-byte teleport ACK, distinct from speed-change ACK) — handle as a resync branch, not the steady state. PhysicsEngine constants (gravity, jump, capsule) must match FG. |
| **Combat (rotation / outcome)** | client-computed, **server-rolled & validated** | `OPTIMISTIC` | The client applies swing tables, dodge/parry/miss, and HP deltas to local memory; the rotation reads HP + threat after each swing and decides the next ability without waiting for a packet. The *authoritative roll* is the server's — on divergence (server says miss/resist), treat as a correction, not the common case. |
| **Spellcast** | client-initiated, server-validated | `PESSIMISTIC` (cast/channel) | Instant casts behave optimistically (local state applies). Casts with a cast bar / channel are pessimistic: complete the Task only after the expected cast duration elapses and local state (spell applied, HP buffer adjusted, aura present) reflects it. Interrupt / line-of-sight / range failures surface as local client failure state. |
| **Targeting** | client (local selection) | `OPTIMISTIC` | Target selection is a local client operation; the Object Manager's current-target field updates immediately. No packet wait. Server-side target validity (e.g. evade, despawn) shows up as the target field clearing on a subsequent snapshot. |
| **Loot** | **server** | `PESSIMISTIC` | Loot is server-resolved: the bot requests the loot; bag contents change only after the server confirms (loot-response packet → inventory delta). Gate completion on the inventory/bag state change, never on the click. Roll/need/greed outcomes are server-decided. |
| **Inventory** | **server** | `PESSIMISTIC` | All bag/equip/bank/destroy mutations are committed server-side. The bot verifies the post-state via the inventory snapshot (item moved/equipped/consumed). No optimistic assumption that the move took. |
| **Trade** | **server** (shared-struct trade window, server-committed) | `PESSIMISTIC` | The trade window is a shared, both-parties-must-confirm structure; the exchange commits only when the server finalizes both sides. Verify item/gold transfer through the post-trade inventory + money snapshot. |
| **Chat** | client-emit, server-relay | `OPTIMISTIC` | Outbound chat (whispers, group invites, channel/trade messages, GM commands) is fire-and-relay: the client sends, the server broadcasts. Treat as sent on local emit; do **not** gate downstream state on the chat line appearing (chat can lead or lag the state-changing packet — gate on the actual snapshot field, per `Spec/00` invariant "No blind sequences"). |

## Consequences already encoded elsewhere

- **Snapshot cadence** — time-driven 10 Hz bot→StateManager snapshot tick is
  the client-authoritative recipe (methodology §4.2); see
  [`01_ARCHITECTURE.md`](01_ARCHITECTURE.md) "Snapshot tick (every bot, 10 Hz)".
- **FG/BG roles** — FG memory is primary; BG simulates client-side resolution
  to match FG reads. See [`07_PHYSICS.md`](07_PHYSICS.md) and `Spec/00`
  invariant 2 ("FG is ground truth").
- **Resync** — teleport / forced-position / forced-state packets are the
  recovery trigger; framed in [`08_PROTOCOLS.md`](08_PROTOCOLS.md) and the
  recovery activity family ([`Plan/Activities/recovery.md`](../Plan/Activities/recovery.md)).

## Minimum viable evidence (methodology §7)

- [x] This file exists with the per-action-class table.
- [ ] Classification is consistent with `00_VISION.md` (client-auth / FG-truth).
- [ ] At least one Task demonstrates the optimistic-local-read pattern
  (combat rotation reads HP/threat post-swing without a packet wait).
- [ ] At least one server-authoritative Task (loot / inventory / trade)
  demonstrates the pessimistic verify-via-snapshot pattern.
- [ ] DecisionEngine tick cadence in code matches the time-driven recipe (§4.5).
- [ ] Each Task type is tagged `OPTIMISTIC` or `PESSIMISTIC` per the table above.
