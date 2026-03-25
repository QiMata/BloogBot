# 0x6367B0 Grounded Wall/Corner Driver — Binary Decompilation

## Function Signature
```c
int GroundedWallDriver(CMovement* this, int moveVecPacked, float totalDist, float* moveDir)
```

## Constants
- `0x7FF9D8` = 1.0f (distance threshold)
- `0x8026BC` = ~9.54e-7 (FP epsilon for zero-dist check)

## Pseudocode (from disassembly at 0x6367B0)

```c
int GroundedWallDriver(CMovement* this, int unused, float totalDist, float* moveDir) {
    // Early exit: if |totalDist| < epsilon, skip
    if (fabsf(totalDist) < 9.54e-7f)
        return /* jump to 0x637129 exit */;

    // Initialize 7 contact slots
    Contact contacts[7];
    for (int i = 0; i < 7; i++) InitContact(&contacts[i]);

    // Setup
    float remainingDist = totalDist;       // ebp-0xc
    float originalDist = totalDist;        // ebp-0x60
    float accumulatedDist = 0.0f;          // ebp-0x28
    vec3 moveVec = { moveDir[0], moveDir[1], 0 };  // ebp-0x18/0x14/0x10
    int numContacts = 0;                   // ebp-8
    int contactIter = 0;                   // ebx

    // If flags & 0x40000000 (on transport?), do initial swept AABB with down-normal
    if (this->flags & 0x40000000) {
        vec3 downNormal = { 0, 0, -1.0f };
        numContacts = TestTerrain(this, &global_scene, &downNormal, 1.0f,
                                  contacts, &numContacts, &walkableFlags, 0);
        if (numContacts == 0) return /* exit */;
    }

    // Main SweepAABB with move direction
    numContacts = TestTerrain(this, &global_scene, &moveVec, totalDist,
                              contacts, &numContacts, &sweepDist, 0);
    if (numContacts == 0) return /* exit */;

    // Compute resolved movement = moveVec * sweepDist
    vec3 resolvedMove = moveVec * sweepDist;  // via 0x4549A0

    // Apply to position
    this->pos += resolvedMove;

    // Check if we hit the contact array boundary (all contacts consumed)
    if (numContacts >= global_maxContacts) {
        // No contacts to process — just accumulate distance
        accumulatedDist += remainingDist;
        float left = originalDist - accumulatedDist;
        if (left < 1.0f)
            goto finalize;  // 0x636DCD

        // Scale remaining distance
        remainingDist = left;
        remainingDist = (left / originalDist) * totalDist;
        // Reset move vector to original direction with Z=0
        moveVec = { moveDir[0], moveDir[1], 0 };
        goto loop_body;  // 0x636D90
    }

    // === CONTACT PROCESSING (0x636993) ===
    // Fraction of distance consumed by sweep
    float hitFraction = sweepDist / remainingDist;

    // Update remaining distance
    remainingDist -= sweepDist;

    // Compute 2D displacement of resolved move
    float resolvedXY = sqrt(resolvedMove.x² + resolvedMove.y²);
    float xyBudgetLeft = originalXYBudget - resolvedXY;

    if (xyBudgetLeft >= 1.0f) {
        contactIter = 1;  // reset iteration counter
    } else {
        contactIter++;
        if (contactIter > 5)
            goto exit_max_iterations;  // 0x636F2D
        contactIter = 1;  // reset for this pass
    }

    // Accumulate and check remaining
    accumulatedDist += hitFraction * remainingDist;  // approximate
    float left = originalDist - accumulatedDist;
    if (left <= 1.0f)
        goto finalize;

    // Read contact normal from contact array
    vec3 contactNormal = contactArray[numContacts];

    // Check if walkable surface was hit
    int walkableContact = CheckWalkable(this, numContacts, &walkableState);

    if (walkableContact) {
        // === WALKABLE CONTACT PATH ===
        // Call 0x635C00 (vertical correction) directly
        float oldDist = remainingDist;
        vec3 correction = VerticalCorrection_635C00(this, &moveVec, &remainingDist,
                                                     contacts, contactNormal,
                                                     blockerMerged, &walkableContact);
        result = correction;
    } else {
        // === NON-WALKABLE CONTACT PATH ===
        // Call blocker-axis merge (0x636610)
        int mergeResult = MergeBlockerAxes_636610(&contacts, numContactsUsed,
                                                   &merged, contactNormal);

        // Call branch gate (0x636100) with merged blocker + contact normal
        int gateResult = BranchGate_636100(this, &moveVecPacked, contactNormal,
                                            remainingDist, slopeAngle,
                                            mergeResult, &merged);

        if (gateResult == 0)
            return /* exit — no valid slide */;

        if (gateResult == 2) {
            // === VERTICAL CORRECTION PATH ===
            this->flags |= 0x04000000;  // Set grounded-wall flag
            float oldDist = remainingDist;
            vec3 correction = VerticalCorrection_635C00(this, &moveVec, &remainingDist,
                                                         contacts, contactNormal,
                                                         merged, &mergeResult);
            // Distance fraction: remainingDist = (newDist / oldDist) * remainingDist
            remainingDist = (remainingDist / oldDist) * remainingDist;
            result = correction;
        } else {
            // === HORIZONTAL CORRECTION PATH (gateResult == 1) ===
            vec3 correction = HorizontalEpsilon_635D80(this, &moveVec, remainingDist,
                                                        contactNormal);
            result = correction;
        }
    }

    // After correction: apply result and loop back
    // ... (continues at 0x636C20 with updated moveVec and remainingDist)

    // Loop back to main SweepAABB (0x636D90)
}
```

## Key Findings

1. **Loop structure**: Up to 5 iterations (`cmp ebx, 5` at 0x6369F1), each re-queries contacts with TestTerrain
2. **Distance bookkeeping**: After each iteration, `remainingDist` is decremented by `sweepDist`, and a fraction `(newDist/oldDist)` is applied
3. **Branch gate return codes**: 0x636100 returns {0, 1, 2}:
   - 0 = exit (no slide possible)
   - 1 = horizontal correction (0x635D80)
   - 2 = vertical correction (0x635C00) + set flag 0x04000000
4. **0x04000000 flag**: Only set when vertical correction path is taken
5. **Exit condition**: `originalDist - accumulatedDist < 1.0f` — stops when less than 1 yard remains
6. **Two distinct contact paths**: walkable contacts go directly to 0x635C00; non-walkable go through 0x636610 merge → 0x636100 gate
