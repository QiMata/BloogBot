# Vanilla WoW (1.12) Physics/Movement Engine — Design Intent + Research Notes

> Goal: build a **client-side** movement + collision system that *feels like* the original **vanilla World of Warcraft 1.12** client, while remaining compatible with an authoritative server.  
> Secondary goal: use PhysX Character Controller (CCT) behavior as a *reference model* (sweeps, depenetration, slope handling), but **translate** the results into WoW-style movement state + networking.

---

## 1) Core assumptions (what “vanilla-like” means)

### Client prediction + server correction (pragmatic truth)
Vanilla-era WoW movement is **client-driven**: the client simulates motion locally and reports state (position, flags, timestamps, fall/jump info) to the server at a high frequency. The server can correct/deny movement when it detects invalid states (teleport, speed, etc.), but the gameplay feel depends on **local prediction**.

This project assumes:
- The **client** runs the primary simulation step (grounding, jump arcs, sliding, collision response).
- The **server** validates and may correct. Your engine must be able to **reconcile** (snap / smooth / re-sim) when server state differs.

---

## 2) The canonical movement state (what the network expects)

A major part of “vanilla movement” is not just the motion math—it's the **wire format** of the state the client reports.

### MovementInfo (client version 1.12)
Public reverse-engineered docs show `MovementInfo` for vanilla (1.12) includes:

- `flags` (bitfield)
- `timestamp`
- `position` (x,y,z)
- `orientation`
- optional `TransportInfo` when `ON_TRANSPORT` is set
- optional `pitch` when `SWIMMING` is set
- `fall_time`
- optional jump fields when `JUMPING` is set: `z_speed`, `cos_angle`, `sin_angle`, `xy_speed`
- optional `spline_elevation` when `SPLINE_ELEVATION` is set

Source: GTKer “wow_messages” documentation for MovementInfo (covers 1.12/2.4.3/3.3.5):  
- https://gtker.com/wow_messages/docs/movementinfo.html

### TransportInfo (client version 1.12)
Vanilla transport attachments are explicitly encoded:

`TransportInfo` (1.12) contains:
- transport `guid`
- `position` (offset/position while on transport)
- `orientation`
- `timestamp`

Source: GTKer TransportInfo (explicitly lists 1.12 layout):  
- https://gtker.com/wow_messages/docs/transportinfo.html

### Practical implication for your engine
Your simulation should maintain a “network snapshot” struct that can serialize exactly the above fields. In practice, that means you need your internal movement system to keep these **derived values** current:

- `fall_time`: accumulated time while falling (and/or in jump/fall states)
- `jump` fields: parameters describing the *initial/ongoing* jump/fall arc
- `flags`: a faithful representation of whether you're on ground, in water, on a transport, etc.
- `transport` attachment: the **only** state that should allow “standing on something moving.”

---

## 3) “You can only stand-on-moving on a Transport” (what to implement)

### The rule you want to reproduce
Vanilla feels like:
- You can stand still on a **boat/zeppelin/elevator** and the world moves under you.
- You generally do **not** get full “moving platform parenting” for arbitrary moving objects; the game treats “moving while you're standing” as a special-case: **transports**.

### Implement it as a hard invariant
When your controller is “grounded on something that moves,” only allow that behavior if your movement flags include `ON_TRANSPORT` and you have a valid `TransportInfo`. Otherwise, handle relative motion as *external forces* (push/impulse) or *collision correction*, not parenting.

**Grounded+moving platform behavior should be gated by:**
- `IsOnTransport == true` **and** transport GUID is valid
- A defined “transport local position/orientation” state
- A transport tick/timebase that updates your world-space position

Why this is consistent with public protocol docs:
- The vanilla `MovementInfo` format has an explicit conditional `TransportInfo` block only when the `ON_TRANSPORT` flag is set, implying “standing on moving” is encoded as a distinct state rather than a generic platform system.  
  (See the MovementInfo + TransportInfo docs linked above.)

---

## 4) Collision + grounded movement model (vanilla feel, PhysX-inspired)

You can use PhysX CCT logic as a *reference* (sweep → hit analysis → slide/step → resolve), but translate to WoW-like outputs:

### 4.1 Character shape and collision response
Recommended internal model:
- Use a **capsule or “lollipop”** style kinematic collider (capsule + foot bias) to mimic “stable standing” and reduce corner snagging.
- Continuous collision via **sweeps** (which you already have).
- For each move step:
  1) propose displacement
  2) sweep capsule
  3) depenetrate if needed
  4) slide along hit planes
  5) re-check ground

Your end product should output:
- `grounded` state
- `ground normal`
- `horizontal velocity` (post-slide)
- collision flags similar to PhysX (sides/above/below), but mapped into WoW state/flags

### 4.2 Grounding rules
Grounding is the “secret sauce.” Vanilla-like grounding typically includes:
- A “snap to ground” distance (small) to prevent hovering on tiny lips
- A slope limit beyond which you **slide** instead of stand
- Special-case stairs/steps (“step-up”) behavior

Even if your geometry system differs from Blizzard’s, you can match *feel* by tuning:
- ground probe distance
- walkable slope angle
- step offset height
- friction-like damping during sliding

### 4.3 Jumping
In the packet format, vanilla jump/fall is described by:
- `fall_time`
- “jump info” values (`z_speed`, `cos_angle`, `sin_angle`, `xy_speed`) when `JUMPING` is set

That implies your jump system should:
- Be able to reconstruct or “explain” the jump arc in terms of those fields
- Keep a consistent `fall_time` while airborne
- Maintain a stable flag transition model (grounded ↔ jumping/falling)

---

## 5) Geometry and “what collision data exists” (client vs server ecosystem)

### 5.1 Map/Model collision as triangle meshes
Open-source server projects (and their extraction pipelines) treat collision geometry as:
- terrain height information extracted from ADTs (“maps”)
- building / static model triangles extracted from WMOs / M2s and assembled into VMAPs

A common description of VMAPs in the MaNGOS ecosystem:
- “Vmaps stands for MaNGOS WoW Vertex Map Physics Info” used for vertical clipping, line-of-sight, etc.
- Generated by exporting WMO assets and placement data from ADTs, then assembling into `.vmap` and `.vmdir`.

Reference: getmangos wiki page on VMAP files:
- https://www.getmangos.eu/wiki/referenceinfo/serverfiles/vmap-files-r20043/

### 5.2 Practical take-away for your client engine
Even if you’re building a *client* replication, these toolchains are useful because they demonstrate:
- How to organize WoW world geometry into **queryable collision data**
- How to do height/LOS/ray queries against WMO triangle meshes

The TrinityCore collision system (VMAP) includes a `WorldModel.cpp` with triangle intersection routines (ray/tri, etc.).  
Reference (doxygen mirror):  
- https://docs.huihoo.com/doxygen/trinitycore/db/d84/WorldModel_8cpp.html

---

## 6) “Translation layer”: PhysX CCT → WoW-style movement state

Think of PhysX CCT as the *solver*, and your WoW engine as the *state machine + wire protocol*.

### 6.1 Suggested internal layering
1. **Input layer**
   - movement intent (forward/strafe/turn)
   - jump pressed
   - external forces (knockback)
2. **State machine**
   - grounded, airborne, swimming, on-transport, rooted/stunned
3. **Kinematic solver**
   - sweep + slide + step + depenetration
4. **Post-process**
   - compute `fall_time`, jump info, pitch, transport offsets
   - produce `MovementInfo` snapshot for networking

### 6.2 Explicit invariants to match vanilla feel
- “Moving platform parenting” only when `ON_TRANSPORT` is true.
- Never let generic rigid bodies/scene objects become “parents” unless they are flagged and encoded as transport.
- Keep airborne motion deterministic enough to reconcile with server corrections.

---

## 7) Concrete “next steps” for your implementation

1. **Define your MovementInfo snapshot struct** (1.12-compatible fields), independent of your physics solver.
2. Implement a robust **ground probe** (down sweep / ray + snap) and slope rules.
3. Implement **jump state**:
   - store start time, fall_time accumulator
   - compute/track jump info fields
4. Implement **transport attachment** as a first-class mode:
   - local-space position on transport
   - transport GUID
   - attach/detach rules
5. Start validating by:
   - logging state transitions (grounded ↔ jumping ↔ falling ↔ landing)
   - comparing network snapshots against known packet layouts

---

## 8) Primary research references (direct links)

- MovementInfo (includes 1.12 layout + conditional Transport/Jump fields)  
  https://gtker.com/wow_messages/docs/movementinfo.html

- TransportInfo (includes 1.12 layout)  
  https://gtker.com/wow_messages/docs/transportinfo.html

- wow_messages (source repo that generates the message docs for multiple versions including 1.12)  
  https://github.com/gtker/wow_messages

- VMAP files overview (MaNGOS ecosystem; extraction + purpose)  
  https://www.getmangos.eu/wiki/referenceinfo/serverfiles/vmap-files-r20043/

- TrinityCore VMAP collision routines (WorldModel.cpp doxygen mirror)  
  https://docs.huihoo.com/doxygen/trinitycore/db/d84/WorldModel_8cpp.html
