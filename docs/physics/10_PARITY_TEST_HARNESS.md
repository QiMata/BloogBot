## Parity Checklist and Testing Guide for PhysX CCT Replication

To ensure your character controller matches PhysX’s behavior, use this checklist of features and a suite of tests.

---

## Feature Checklist

### Movement Pipeline

- [ ] **Decomposition** – Splitting input displacement into **Up**, **Side**, **Down** vectors.
- [ ] **Min Distance threshold** – Ignoring movements `<= minDist` to prevent jitter.
- [ ] **Step Offset application** – Adding `stepOffset` to **UpVector** only when appropriate (lateral move present, not already moving up). Cancel `stepOffset` if jumping/upwards or no horizontal input.
- [ ] **Upward Sweep** – Executing an upward sweep and clamping movement at first ceiling hit. Setting `eCOLLISION_UP` if upward motion was obstructed.
- [ ] **Lateral Sweep** – Iteratively sweeping and sliding along surfaces, reflecting velocity and zeroing normal component (`bump=0`, `friction=1`). Setting `eCOLLISION_SIDES` if any wall/steep slope contact.
- [ ] **Downward Sweep** – Sweeping down to ground, stopping at first contact. Setting `eCOLLISION_DOWN` on ground hit. Ensuring one or multiple iterations based on slope mode (force slide vs not).
- [ ] **Overlap Recovery** – Detecting overlaps (initial penetration) and computing MTD to push the controller out. Applying penetration correction either immediately (preferred) or via `mOverlapRecover` across frames. Ensuring no persistent interpenetration.
- [ ] **Moving Platform Adjustments** – Before sweeps, adjusting for base motion (only if base is flagged rideable and not user-override):
  - [ ] Upward base movement applied directly to controller position.
  - [ ] Downward base movement added to downward displacement.
  - [ ] Horizontal base movement added to lateral displacement.
  - [ ] Update cached base position and `standingOnMoving` flag for next frame.
- [ ] **Collision Flags** – Populating `collisionFlags` bitfield properly:
  - [ ] `Sides` when any lateral collision occurred (wall or steep slope impact).
  - [ ] `Up` when head/ceiling collision occurred (after upward sweep).
  - [ ] `Down` when grounded (after down sweep).
- [ ] **Cached State** – Maintaining between frames:
  - [ ] `mTouchedShape` / `mTouchedActor` for ground object.
  - [ ] `mTouchedObstacleHandle` for ground obstacle.
  - [ ] `mCachedStandingOnMoving` for platform logic between frames.
  - [ ] `mPreviousSceneTimestamp` to sync platform motion once.
  - [ ] `mDeltaXP` velocity of moving base.
  - [ ] Overlap recovery vector if used (carry to next frame).

---

## Slope and Step Handling

- [ ] **Slope limit logic** – Using `up ⋅ normal` vs `slopeLimit` to identify steep surfaces.
- [ ] **Prevent climbing mode** – If steep and requires upward move, do not climb:
  - [ ] Trigger second “walk experiment” pass, remove upward motion.
  - [ ] Character can stand on slope (no forced slide).
- [ ] **Force sliding mode** – If steep, slide off:
  - [ ] Allow multiple down iterations (`maxIterDown = maxIter`).
  - [ ] Character cannot remain perched; gravity will carry them off.
- [ ] **Step up conditions** – Only stepping when horizontal move exists and obstacle height `<= stepOffset`.
- [ ] **Ceiling slide prevention** – If configured, stopping horizontal movement on head hit.

---

## Misc

- [ ] **Filter callbacks** – (Optional) Honor user-defined behavior flags (ride on object, etc.).
- [ ] **Thread-safety considerations** – If using multi-thread, mimic locking around move updates like PhysX (though in a single-thread game, not needed).
- [ ] **Double-check all “magic constants”** – PhysX uses:
  - [ ] `MAX_ITER = 10` for sweep loop.
  - [ ] Some epsilon for `isAlmostZero` on delta (not explicit, but likely ~`1e-5` or `1e-6`) to avoid flicker.
  - [ ] `minDist` default (PxControllerDesc) typically `0.001` (1 mm) – use a similar threshold.

---

## Test Harness

Use controlled scenarios to compare your controller’s outcome to expected PhysX behavior.

---

## Level Ground and Walls

- Move forward on flat ground into a vertical wall. Expect:
  - `eCOLLISION_SIDES` set.
  - Character stops at the wall (no penetration).
  - No `eCOLLISION_DOWN` loss (still grounded).
- Slide along the wall: apply slight diagonal input into wall. Expect:
  - Character glides along it at reduced speed (wall’s normal component stripped).
- No oscillation or getting stuck on corners (the 10-iteration loop should handle acute corner smoothly).

---

## Stepping Up Obstacles

- Place a series of boxes:
  - One at `0.2m` height
  - One at `0.5m` if `stepOffset = 0.5m`
- Walk into the `0.2m` box: expect character steps onto it.
  - CollisionFlags: first frame `eCOLLISION_DOWN` when on top, no side collision as it was treated as step, **or** possibly a brief `eCOLLISION_SIDES` then `Down`.
- Walk into the `0.5m` box: expect can step exactly onto it, similar outcome.
- Try a box slightly taller than `0.5m` (e.g., `0.6m`): character should **NOT** step up. Expect:
  - `eCOLLISION_SIDES` (hit like a wall)
  - No up movement
- Ensure transitions are smooth (no popping up too early or late; PhysX uses the temporal box broadphase to detect step—so if you see a delay, adjust how you detect the obstacle for stepping).

---

## Slopes

- Create a slope just below the limit (e.g., `40°` if limit is `45°`). Expect:
  - Character walks up without issues, treat it as walkable ground.
  - CollisionFlags: `eCOLLISION_DOWN` while on it, no `eCOLLISION_SIDES`.
- Create a slope just above the limit (e.g., `50°`).

**Prevent climb mode:**
- Character should not be able to walk up. Expect:
  - `eCOLLISION_SIDES` triggers when hitting the slope
  - `eCOLLISION_DOWN` once they land on it
- They should come to rest on the slope (no further upward progress).
- If you release input, they should stay put (assuming static friction).

**Force slide mode:**
- Place character on slope (or have them walk a bit up). Expect:
  - They start sliding down on their own.
  - `eCOLLISION_DOWN` stays true as they slide.
  - Position over time moves downward; they should not stick.
- Test standing on slope with no input (force slide): they should still slide slowly due to gravity being continuously applied.
- In prevent mode, they remain stationary.

**Jump and land on steep slope:**
- Prevent mode: character lands and stays (`eCOLLISION_DOWN`).
- Slide mode: character lands then immediately starts sliding down.

---

## Ceilings

- Make a low ceiling. Jump straight up into it. Expect:
  - `eCOLLISION_UP` flag set.
- If `preventVerticalSlide=true`, horizontal velocity zeroed out:
  - If you also had forward input, character essentially stops under the ceiling.
- If the flag is false:
  - With forward input, character moves forward while constrained by the ceiling (gliding until out from under).
- Move while crouching under a low ceiling:
  - If you simulate by setting `upDirection` differently mid-run (or just test with a tall character in a low tunnel), ensure no jitter—this mostly tests overlap recovery if the head intersects roof.
- Platform pushes character into ceiling (see moving platforms below).

---

## Moving Platforms

- **Horizontal platform:** Character standing on it should move along.
  - Drop markers or log character X position over time to ensure it matches platform movement exactly.
- **Vertical platform up:** Character should ride up without lag.
  - If platform goes into a ceiling, verify character collides with ceiling properly (likely `eCOLLISION_UP` triggers when hit; if platform keeps pushing, overlap recovery should gently push character out to avoid penetration).
- **Vertical platform down:** Character should follow down (no floating as platform descends).
  - If platform descends faster than gravity, character should still stay on it due to extra downward disp push.

**Platform start/stop:**
- If platform suddenly starts moving under character, next frame the character should pick up that velocity (maybe a one-frame slip until detection kicks in, but ideally none).
- If platform stops, character should not drift strangely (should just be standing as if on static ground).

**Rotating platform:**
- Tricky: if you implement local point logic, test that character rotates around with the platform appropriately (maintaining roughly the same local position).

**Leaving platform:**
- Step off or jump off—ensure `mTouchedShape` is cleared and platform motion no longer applied.

---

## Overlaps and Recovery

- Teleport the character so its lower half intersects a wall (simulate initial overlap).
  - Controller should be pushed out.
  - Ends up just outside wall (no tunneling through, no remaining stuck).
- Put the character between two close walls (narrow doorway) so it overlaps both slightly at rest.
  - Overlap recovery should find a resolution (likely pushing out of one side or balancing).
  - Ideally no infinite jitter; PhysX might pop them out one side.
- Platform suddenly raising into the character (elevator hits from below):
  - If platform collider moves into character in one frame, overlap recovery should push character up on top of it.
- Inverse: ceiling coming down on character:
  - Overlap recovery should push them down or aside.
  - No crouch in CCT, so likely forced out horizontally if possible.
- After overlap resolution, collision flags should make sense:
  - If pushed up by platform, `eCOLLISION_DOWN` remains, etc.

---

## Precision / No Oscillation

- Idle on flat ground: character should not drift or jitter (`minDist` prevents micro-movements).
- Press against a wall: no small vibrations (10-iteration slide with `minDist` ensures that).
- Corners: run into an interior corner (two walls).
  - Character slides along one then stops at the vertex.
  - Should not get stuck toggling between walls; iterations should handle it.
- Staircases (step up + slope):
  - Walk up a flight of small steps—motion should be smooth (maybe slight vertical pops, but minimal if `stepOffset` is properly sized).

---

## Frame-Rate Consistency

- Ensure no significant frame-rate dependency:
  - Try at low vs high frame rate.
  - Behavior should remain consistent (PhysX CCT tends to, as long as `minDist` scaled accordingly).
- Example: `30Hz` vs `120Hz`
  - It shouldn’t climb slopes it can’t at 30 or vice versa.
  - If you see differences, adjust `minDist` or iteration counts.

---

## Conclusion

By verifying each of the above and adjusting as needed, you should achieve parity with PhysX’s Character Controller. It’s often useful to log key events (collisions, slope decisions, overlap corrections, base movement applied) and compare them with PhysX debug output if available, to ensure your logic branches align.

With thorough testing—walking, jumping, sliding on slopes, interacting with moving objects—you can be confident your controller replicates PhysX CCT’s nuanced behavior line-by-line.

**Happy testing!**

Remember: the devil is in the details—slight differences (like skipping a tiny overlap push due to `minDist`, or mis-ordering platform updates) can lead to noticeable divergences (like sticky corners or sinking into moving floors). Use the above tests to catch those, and refer back to cited code lines to adjust your implementation to match the PhysX reference precisely.