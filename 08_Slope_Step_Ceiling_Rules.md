# Slope, Step, and Ceiling Rules – Non-Walkable Surfaces and Special Cases

This section consolidates the conditions under which the Character Controller (CCT) changes its behavior for slopes (steep surfaces), stepping up, and ceilings (vertical confinement). It provides a quick reference for **when each rule triggers**, distilled from the detailed analysis.

---

## Slope Limit and Non-Walkable Surfaces

`SlopeLimit = cos(maxSlopeAngle)` in the controller params (e.g., `0.707` for 45°). PhysX uses this to classify surfaces:

- **Walkable surface:** `dot(surfaceNormal, upDirection) >= slopeLimit`  
  The surface’s angle from horizontal is ≤ the max angle (e.g., `dot = 0.8` ≈ 36°, which is walkable if limit is 45°).  
  The CCT will treat this as ground when found in the down sweep, and (typically) allows upward progress onto it when encountered during motion.

- **Non-walkable (steep) surface:** `dot(surfaceNormal, upDirection) < slopeLimit`  
  Example: a 60° slope has `cos(60°)=0.5`; with `slopeLimit=0.707`, `0.5 < 0.707` triggers non-walkable logic.  
  This is treated like a wall in many respects: the character should not be able to ascend it.

### Behavior differences: sliding vs. standing

#### Mode: `PxControllerNonWalkableMode::ePREVENT_CLIMBING` (default)

- The character will **not climb** up a non-walkable slope, but **can stand** on it if they somehow get there (they won’t be forced down).
- **Implementation sketch:** when a side/up sweep hits a steep slope, PhysX sets `STF_HIT_NON_WALKABLE` and performs the second “walk experiment” move.
  - In that second attempt (`STF_WALK_EXPERIMENT` true, but not forcing slide):
    - **Upward motion is disabled** (no climbing).
    - **Down sweep is limited to one iteration**, which effectively lets the controller “settle” onto the contact without repeatedly iterating downward movement.
- **Result:** collision flags will include `eCOLLISION_SIDES` (side hit) and likely `eCOLLISION_DOWN` (ground contact in down sweep).  
  The character can remain perched on the slope, but attempting to move up it cancels the upward component; they may still move laterally along it or back down.

#### Mode: `PxControllerNonWalkableMode::ePREVENT_CLIMBING_AND_FORCE_SLIDING`

- The character will **slide down** non-walkable slopes automatically (**cannot stand** on them).
- **Implementation sketch:** after detecting the non-walkable slope (same trigger), they perform the second pass with `STF_WALK_EXPERIMENT` and allow **multiple iterations** in the down sweep.  
  This causes downward motion (gravity/down component) to keep iterating, forcing the character to slide off.
- **Result:** the character continues moving downward until off the slope or onto a flatter surface.  
  From a player perspective, steep slopes feel “slippery.”

### Triggers for `STF_HIT_NON_WALKABLE`

- **Side pass trigger (constrained mode only):**  
  In constrained climbing mode, during side motion, if the controller’s forward path collides with a slope that is too steep **and** the contact point is above the character’s current bottom `+ stepOffset`, the controller flags it non-walkable immediately.  
  - Requires `constrainedClimbingMode == true`.  
  - **Rationale:** in constrained mode, avoid any upward gain onto steep surfaces early; treat it like a wall.

- **Down pass trigger (all modes):**  
  After attempting movement, in the final down sweep, if the ground under the character is non-walkable and the vertical distance to the contact point is **greater than `stepOffset`**, it is marked non-walkable.  
  - If the vertical rise is `<= stepOffset`, it may **not** be flagged here, implying some edge cases where small ledges on steep geometry can behave “step-like” (especially in non-constrained mode).

### Walk Experiment (“second move call”) specifics

Detection of `STF_HIT_NON_WALKABLE` causes `Controller::move` to do a **two-call** approach:

1. **First move call:** normal movement attempt; detects the non-walkable condition; sets flags.
2. **Second move call:** performed with `STF_WALK_EXPERIMENT` enabled.

During the second call:

- `constrainedClimbingMode` is passed appropriately.
- `stepOffset` can be set to **0** if `STF_IS_MOVING_UP` was set and `standingOnMovingUp` is false, preventing “extra boost” stepping while already moving up.
- `maxIterDown` becomes:
  - **1** for prevent-climb (stand on slope)
  - **maxIter** for force-slide (allow iterative sliding)

### Summary table

| Condition | Mode setting | Internal flag / behavior | Outcome |
|---|---|---|---|
| Hit a slope with `dot(n, up) < slopeLimit` and must go upward to continue (contact above `stepOffset`) | Constrained climb (capsule mode) | `STF_HIT_NON_WALKABLE` set during side pass; upward motion blocked | Treated like a wall: stops upward progress; may slide/stand depending on non-walkable mode |
| End of move finds character partly up a steep slope (ground contact and climb > `stepOffset`) | All modes | `STF_HIT_NON_WALKABLE` set in down pass; triggers walk experiment | Second call executes: stand (prevent) or slide (force-slide) |
| Character is on a steep slope after move | Prevent climbing | Down sweep uses **1** iteration | Can stand on slope; `eCOLLISION_DOWN` true; won’t slide unless input or other forces push them |
| Character is on a steep slope after move | Force slide | Down sweep uses **multiple** iterations | Continues sliding down until stable; cannot rest on slope |
| Hits a ceiling while moving (head collision) | `preventVerticalSlidingAgainstCeiling = true` | `preventVerticalMotion = true`; UpVector canceled in side pass | No “ceiling surfing”; vertical motion stops and horizontal motion is effectively halted for the frame; `eCOLLISION_UP` set |
| Hits a ceiling while moving | `preventVerticalSlidingAgainstCeiling = false` (default) | No special ceiling constraint beyond response | Can still slide along ceiling while pressed up; `eCOLLISION_UP` set |

---

## Step Rules Recap

The controller attempts to step up an obstacle of height `<= stepOffset` **only if there is lateral motion**. If the character is not moving horizontally, `stepOffset` is not applied.

### Stepping sequence (Up → Side → Down)

1. **Up pass:** raise by `stepOffset`.
2. **Side pass:** move forward horizontally at the raised height.
3. **Down pass:** sweep down to land on the top surface.

### When step-up is canceled

- If the character is already moving upward (jumping / being pushed upward / going up a slope), do **not** apply `stepOffset` on top of that.
- If `sideVectorIsZero` (no horizontal movement and not on a moving platform), do **not** apply `stepOffset`.
- If the obstacle is taller than `stepOffset`, the controller collides and does not step.

### Edge case: stepping onto steep slopes

Stepping can allow the controller onto a small ledge of steep geometry if it is within `stepOffset`. Once settled, the next frame’s down pass may flag it non-walkable (depending on the shape/extent), leading to standing/sliding behavior as configured.

---

## Ceiling Constraints Recap

If the controller’s top is pressed against a ceiling:

- **Default behavior:** horizontal movement can continue (sliding) if the player pushes forward and there is lateral space.
- **With `preventVerticalSlidingAgainstCeiling = true`:**
  - When an upward collision is detected (often in side pass), PhysX sets `preventVerticalMotion`.
  - The move cancels remaining UpVector components and effectively prevents further movement while constrained by the ceiling.
  - Result: the character hits their head, upward velocity is zeroed, and horizontal movement is also stopped for that frame until they fall free.

Whether to enable this flag is “feel” dependent:

- Platformers / FPS corridor jumps often prefer **ON** (no weird ceiling-gliding).
- Other scenarios may prefer **OFF** to allow sliding under ledges.

---

## Recap checklist

- **Stepping up:** implement the `stepOffset`-based Up→Side→Down sequence, only when lateral input exists, and never double-boost when already moving up.
- **Slope limit:** compute `dot(normal, up)` against `slopeLimit`.
  - Prevent climbing: cancel upward gain but allow standing.
  - Force slide: allow down-iteration to push the character off steep slopes.
- **Ceiling collisions:** if you want to prevent “ceiling surfing,” when an upward collision is detected, stop remaining movement for the frame.

---

## Quick “when it triggers” reference

- **Will the CCT climb this?**  
  If obstacle height `<= stepOffset` and the surface underfoot is walkable (or it’s a vertical ledge), yes (treat as step).  
  If `> stepOffset` or the surface is steep and requires climbing, no (treated as wall / triggers non-walkable logic).

- **Will the CCT slide down this slope?**  
  If slope angle exceeds `slopeLimit` and force-slide mode is ON, yes (down pass multi-iterations).  
  If force-slide is OFF, the controller won’t climb it, but it can remain perched.

- **What happens hitting a low ceiling?**  
  If ceiling sliding prevention is ON, horizontal motion is stopped on contact.  
  If OFF, the controller may continue sliding laterally while constrained by the ceiling.

---

## Sources / audit notes

- Slope angle checks: `dot(normal, up)` vs `slopeLimit`.
- `stepOffset` usage and conditions.
- `preventVerticalSlidingAgainstCeiling` behavior and where it gates movement.

Search terms (for code audit):
- `slopeLimit` `PxControllerNonWalkableMode`
- `preventVerticalSlidingAgainstCeiling`
- `stepOffset` conditions
