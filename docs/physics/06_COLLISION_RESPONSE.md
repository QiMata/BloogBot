# Collision Response Mathematics
## collisionResponse Math – Reflecting and Decomposing Movement

### Goal

When a sweep detects a collision, PhysX computes a new direction and distance for the character to move (slide along the obstacle). The function responsible is `collisionResponse()`. This section breaks down the math behind it: reflecting the motion vector off the collision normal and scaling components by **bump** and **friction** factors.

---

## Reflection of Velocity

The core idea is to treat the incoming movement vector as a velocity hitting a surface and compute the outgoing velocity after an inelastic collision:

- **Perfect slide (no bounce):** The component of velocity into the surface is zeroed, and the component tangent to the surface remains. This is exactly what we want for a character gliding along walls. PhysX achieves this by using `bump = 0` (zero normal component) and `friction = 1` (full tangent component) for typical collisions.

**Function definition:** `computeReflexionVector(PxVec3& reflected, const PxVec3& incomingDir, const PxVec3& outwardNormal)`

This computes a mirror reflection of the incoming direction about the plane defined by the normal:

$$
\text{reflected} = \text{incomingDir} - 2(\text{incomingDir}\cdot \text{outwardNormal})\ \text{outwardNormal}.
$$

This formula reflects the vector as if the surface were perfectly elastic (like a mirror bounce).

If `outwardNormal` is normalized and pointing out of the obstacle, `incomingDir.dot(outwardNormal)` is the projection length onto the normal. Subtracting **2×** that projection flips the sign of that component, producing a reflection vector.

Examples:

- Incoming `(1, -1, 0)` hitting a horizontal surface with `outwardNormal (0, 1, 0)` reflects to `(1, 1, 0)` (vertical component inverted).
- For a vertical wall with normal `(1, 0, 0)`, an incoming `(1, -1, 0)` reflects to `(-1, -1, 0)` (horizontal component inverted).

PhysX stores this result in `reflectDir`.

---

## Decomposition into Normal and Tangent

PhysX then separates this reflected vector into two parts relative to the hit surface’s normal:

`decomposeVector(normalCompo, tangentCompo, reflectDir, hitNormal)`

This yields:

- **Normal component:** projection of `reflectDir` onto `hitNormal`  
  $$
  \text{normalCompo} = (\text{reflectDir}\cdot \text{hitNormal})\ \text{hitNormal}
  $$
- **Tangent component:** the component parallel to the surface  
  $$
  \text{tangentCompo} = \text{reflectDir} - \text{normalCompo}
  $$

In effect, if `reflectDir` was the true elastic bounce direction, `normalCompo` would be how much it’s still pointing outward or inward, and `tangentCompo` is purely along the surface.

Now, PhysX introduces **bump** and **friction** coefficients to scale these components:

- **Bump:** scales the component into/out of the surface (`normalCompo`).  
  - `bump = 1` keeps the full reflected normal component (full bounce).
  - `bump = 0` eliminates any outward push (no rebound).  
  PhysX sets `bump = 0` for character controller collisions to prevent bouncing.

- **Friction:** scales the tangent (sliding) component.  
  - `friction = 1` preserves full tangential velocity (no slowing down on contact).
  - `friction = 0` eliminates tangential movement (you’d stop dead on contact).  
  PhysX uses `friction = 1` for CCT to glide without losing speed on walls.

Given `bump = 0`, `friction = 1` in most cases, the formula simplifies:

- `normalCompo` is multiplied by `0` (discarded).
- `tangentCompo` is multiplied by `1` (kept fully).

---

## Code Path (as described)

```cpp
PxExtendedVec3 targetPosition = currentPosition;
if(bump != 0.0f) {
    if(normalize) normalCompo.normalize();
    add(targetPosition, normalCompo * bump * amplitude);
}
if(friction != 0.0f) {
    if(normalize) tangentCompo.normalize();
    add(targetPosition, tangentCompo * friction * amplitude);
}
```

Here:

- `amplitude` = magnitude of the remaining movement vector prior to collision response. Essentially, how far we still intended to move after reaching the contact. PhysX calculates it from the difference between the original target position (if no collision) and current position at impact. It’s basically the distance the character would have moved if not obstructed.

They reset `targetPosition` to `currentPosition` then add components:

- If `bump != 0`: add the normalized normal component times `(bump * amplitude)`.
- If `friction != 0`: add the normalized tangent component times `(friction * amplitude)`.

In our case `bump = 0`, `friction = 1`:

- The `if(bump != 0)` block is skipped entirely.
- The `if(friction != 0)` block executes: they optionally normalize `tangentCompo` (we’ll discuss the `normalize` flag shortly), then add `tangentCompo * 1 * amplitude` to `targetPosition`.

Effectively:

$$
\text{targetPosition} = \text{currentPosition} + \widehat{\text{tangentCompo}}\cdot \text{amplitude}
$$

(where $\widehat{\text{tangentCompo}}$ is either `tangentCompo` itself or its normalized form, depending on `normalize`).

That means the character will move the full remaining distance `amplitude`, but purely along the surface (tangent direction).

---

## Normalize Flag (`STF_NORMALIZE_RESPONSE`)

PhysX can pass `normalize = true` if `STF_NORMALIZE_RESPONSE` is set. This causes them to normalize `normalCompo` and `tangentCompo` before scaling.

Why? If the reflection vector is very small or if `amplitude` is the magnitude of the leftover motion, normalizing ensures they still move a meaningful amount along each component.

Example scenario: character almost parallel to wall – `reflectDir` is only slightly off original, `tangentCompo` might have nearly full magnitude equal to incoming, `normalCompo` tiny. If `amplitude` is small as well (due to `minDist` or near end of movement), not normalizing would result in moving a fraction of a fraction.

By normalizing, they ensure the direction is preserved but use the full `amplitude` in that direction.

It can cause the resultant move to exceed the original vector length if used indiscriminately, but they only use it in specific sticky cases to ensure progression.

For most cases, `normalize = false`, so they scale actual components:

- If you hit a surface at a steep angle, `reflectDir` will be almost the negative of incoming (if head-on to a wall, `reflectDir` goes backward). But `bump = 0` will zero out that reversed component, leaving only tangent which might be nearly zero. So `targetPosition` hardly moves beyond contact – essentially stopping.
- If you graze shallowly, `reflectDir` is only slightly deflected. `tangentCompo` will be almost as large as incoming, so you keep near full speed along surface.

---

## Summarizing Collision Outcomes

### Head-on collision (incoming aligned with normal)

- `reflectDir = -incoming`
- `normalCompo = reflectDir` (all normal), `tangentCompo = 0`
- `bump = 0` → drop all normal, `friction = 1` → nothing to add (tangent is zero)

**Result:** no further movement; character stops at the wall.

`CollisionFlags`: `Sides` (if wall), `Up` or `Down` (if ceiling/floor) would be set. The character effectively loses all velocity into that wall.

### Angled collision (incoming has normal and tangent parts)

Example: incoming velocity 45° into wall:

- `incomingDir ⋅ normal = cos(45°) ≈ 0.707`
- `reflectDir` has reversed normal part, tangent same sign.
- `bump = 0`: remove any outward push; `friction = 1`: keep tangent.

So character retains speed along wall. It will slide along it.

The actual speed vector after: magnitude = incoming speed * `|sin(angle)|`. At 45°, `sin(45°) = 0.707`, so ~70% of speed remains, directed along wall. The other ~30% (cos45) normal part is lost.

This matches intuition: hitting at 45°, you’ll slide with some speed (and you lost the inward component).

### Shallow graze (incoming nearly parallel to wall)

Only a small normal component in incoming.

`reflectDir` flips that tiny normal portion, tangent nearly same as incoming.

After `bump = 0`, `friction = 1`:

You basically continue moving with almost full speed, just slightly adjusted away from the wall.

If angle was super shallow, you may not even perceptibly deviate (if collision detection even reported a hit at all; often a glancing graze might not register if below skin).

---

## Ceiling or Floor Collisions

They are handled similarly:

### Jumping up into a ceiling

- Incoming is upward, reflect vector points downward.
- Treated like a wall: `bump = 0` (no bounce downwards), `friction = 1` (full tangent; tangent here is horizontal if you hit at an angle).

If you jump straight up into a flat ceiling:

- `incoming = (0, 1, 0)`
- ceiling normal (pointing downward) = `(0, -1, 0)`
- `reflectDir = (0, -1, 0)`
- `normalCompo = reflectDir`, `tangentCompo = 0`
- `bump = 0` → drop down component; `friction = 1` → tangent is 0

**Result:** no movement after collision (stop at ceiling). `eCOLLISION_UP` triggers. The character’s upward motion is zeroed out instantly.

If running and jumping, hitting ceiling at angle: you'll slide along it (i.e., your remaining movement is horizontal). This is often prevented by game design (the `preventVerticalSlidingAgainstCeiling` flag which forces vertical motion stop so you don’t weirdly slide along ceilings).

### Floor (downward collisions)

Normally you don’t “bounce” upward when hitting ground (CCT is not a bouncing ball).

Down sweeps typically use `friction = 1`, `bump = 0` for ground as well (thus no rebound, full tangent). Tangent to ground means horizontal movement continues, vertical is zero (so you stay on ground).

That ensures when you land, you immediately stick (no bounce).

If you had forward velocity, tangent to ground = forward velocity, so you keep moving forward on ground seamlessly.

---

## Putting it Together with an Example

Scenario: Character is moving northeast (45°) and hits a north-facing wall:

- `incomingDir (unit) ≈ (0.707, 0, 0.707)`  
  (`x` positive = east, `z` positive = north towards wall)
- `outwardNormal` of wall = `(0, 0, -1)`  
  (points out of the wall, toward south)

Compute reflection:

- `incomingDir ⋅ normal = 0.707*0 + 0*0 + 0.707*(-1) = -0.707`
- `reflected = incoming - 2 * (-0.707) * normal`  
  `= (0.707, 0, 0.707) - (-1.414)*(0, 0, -1)`  
  `= (0.707, 0, 0.707) - (0, 0, 1.414)`  
  `= (0.707, 0, -0.707)`

So `reflectDir ≈ (0.707, 0, -0.707)` (mirror direction, as if it bounced directly off).

Decompose relative to wall normal:

- `reflectDir ⋅ normal = 0.707*0 + 0*0 + (-0.707)*(-1) = 0.707`
- `normalCompo = 0.707 * (0, 0, -1) = (0, 0, -0.707)`
- `tangentCompo = reflectDir - normalCompo = (0.707, 0, -0.707) - (0, 0, -0.707) = (0.707, 0, 0)`

So tangent points directly east along the wall, which makes sense: slide east along wall.

- `bump = 0` → drop `normalCompo` (no movement in `-Z`)
- `friction = 1` → keep `tangentCompo` fully
- `amplitude` = leftover distance (e.g., you intended to move 1m, hit after 0.5m → `amplitude ≈ 0.5m`)

If `normalize = true`, `tangentCompo` becomes `(1, 0, 0)`.

Then:

- `targetPosition = currentPosition + (1, 0, 0) * 0.5`

**Conclusion:** Character slides east along wall for the remaining 0.5m of movement. `CollisionFlags`: side collision true.

This matches expectation: if you run into a wall at 45°, you end up sliding along it.

---

## Special Cases

- **Corner with two collisions quickly:** PhysX handles them one at a time. If you slide into another obstacle within the same move, a second iteration will process it. If the character ends up stuck in a corner (two perpendicular walls), iteration might handle first wall then slide into second wall and stop.
- **Stopping conditions:** If the reflected direction is super small after one collision (meaning the movement mostly canceled), they break to avoid jittering with tiny moves.

---

## Why Not a Simpler Approach?

One might think: for sliding, why not just zero the normal component of the original velocity?

For example:

$$
\text{newDir} = \text{incoming} - (\text{incoming}\cdot n)\ n
$$

That directly projects the incoming direction onto the surface (tangent). This is effectively what the `bump = 0`, `friction = 1` behavior produces.

PhysX keeps the reflection + decomposition method because it’s general and supports cases where:

- `bump > 0` (bounce / restitution-like behavior)
- `friction < 1` (tangent damping)

For CCT specifically, `bump = 0` always, so the simplified projection approach is fine.

---

## Ceilings and Preventing Slide

One more detail: `preventVerticalSlidingAgainstCeiling` toggles behavior when hitting a ceiling.

Normally, `collisionResponse` would allow friction along the ceiling (so if you had any horizontal component, you'd slide sideways).

If this flag is on, PhysX sets `preventVerticalMotion = true` if a side-pass contact normal dot up `< 0` (meaning it’s a ceiling). Then in `moveCharacter`, if `preventVerticalMotion`, they do something like:

- cancel out certain vertical response components so you don’t “slide” along ceilings.

In practice: if you jump and your head hits a ceiling while moving forward, instead of sliding forward along the ceiling, you stop vertical and likely also stop horizontal (or severely limit it), depending on the higher-level composition of the pass vectors.

That flag is a design choice to avoid weird sticky ceiling behavior.

Without it, their collisionResponse math would make you slide along the ceiling with your horizontal velocity (like walking on underside of something until you clear it). Usually undesired.

For replication:

- If a collision normal points downward (ceiling) and this flag is enabled, you can disallow friction movement in that case (e.g., set `friction = 0` for that collision), or handle it at a higher level: detect head collision and zero out horizontal velocity.

---

## Summary

`collisionResponse` in PhysX:

- Takes an incoming direction and full remaining distance (`amplitude`).
- Splits the movement into a component perpendicular to surface (discarded when `bump = 0`) and a component parallel to surface (kept with `friction = 1`).
- Reconstructs the target move by adding scaled components.

For CCT, this results in pure slide along surfaces with no bounce.

### Key takeaways for your engine

- Implement sliding by removing velocity into the surface and keep velocity along the surface.
- Ensure no energy gain: total speed after collision should be ≤ speed before (with `bump = 0`, `friction = 1` it's equal or less).
- Usually set `friction = 1` to maintain responsiveness (no slow down on walls).
- Keep `bump = 0` unless you intentionally want bouncy characters (rarely wanted in controllers).
- Consider numeric stability: if the resulting slide vector is extremely small, you might force normalization to ensure at least some movement or just stop (PhysX sometimes normalizes to avoid stuck on geometry edges).

### Porting notes

To slide:

$$
\text{vel} = \text{vel} - (\text{vel}\cdot n)\ n
$$

If you want to incorporate restitution (bounce), use the reflection formula and then scale.

Be cautious with floating point:

- The dot and normalization should be high precision if possible (double) to avoid small errors causing a tiny negative component.
- When nearly parallel, `vel·n` might be almost 0 but negative due to float error, causing a tiny reflected component reversed. If not handled, that tiny reversed normal could cause the character to think it should move slightly into wall on next iteration (fighting between collisions).
- PhysX likely mitigates by: after first collision, if new velocity is nearly parallel to surface, they might set `STF_NORMALIZE_RESPONSE` to true so that they ignore the tiny normal component and just take the tangent fully normalized, preventing oscillation.
- Consider implementing a threshold: if `|vel·n|` is below some epsilon (meaning almost parallel), just zero out normal (that’s effectively what `bump = 0` does anyway).

Finally, verify visually: run into walls and slopes and ensure the character slides smoothly without popping or sticking.

---

### Sources (as noted in the original text)

- PhysX `computeReflexionVector`
- PhysX `collisionResponse` implementation

Search terms: PhysX character reflection vector, collisionResponse formula, slide along wall physics
