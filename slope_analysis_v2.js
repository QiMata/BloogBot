const fs = require('fs');
const path = require('path');

const FORWARD = 0x1;
const BACKWARD = 0x2;
const STRAFE_LEFT = 0x4;
const STRAFE_RIGHT = 0x8;
const TURN_LEFT = 0x10;
const TURN_RIGHT = 0x20;
const JUMPING = 0x2000;
const FALLING_FAR = 0x4000;
const SWIMMING = 0x200000;
const FALLING = 0x800000;
const TELEPORT_TO_PLANE = 0x04000000;
const PENDING_STOP = 0x80000;
const AIRBORNE_MASK = JUMPING | FALLING_FAR | FALLING;

// Movement flags that indicate a Z-snap or teleport (not true slope traversal)
const SNAP_FLAGS = TELEPORT_TO_PLANE;

const recordingsDir = path.join(process.env.USERPROFILE, 'Documents', 'BloogBot', 'MovementRecordings');

// All Orgrimmar recordings
const files = fs.readdirSync(recordingsDir)
    .filter(f => f.endsWith('.json') && f.includes('Orgrimmar'))
    .sort()
    .reverse();

console.log('==========================================================');
console.log('REFINED SLOPE ANALYSIS V2 - Filtering Z-snaps and artifacts');
console.log('==========================================================\n');

const allCleanSegments = [];
const allSustainedRuns = [];
const allStalls = [];
const allFallTransitions = [];

for (const filename of files) {
    const filepath = path.join(recordingsDir, filename);
    const data = JSON.parse(fs.readFileSync(filepath, 'utf8'));
    const frames = data.frames;
    console.log('--- ' + filename + ' (' + frames.length + ' frames, ' + (data.durationMs/1000).toFixed(1) + 's) ---');

    // PASS 1: Compute per-frame slope, filtering out snaps and artifacts
    const segments = [];
    for (let i = 1; i < frames.length; i++) {
        const prev = frames[i - 1];
        const curr = frames[i];

        // Both frames: Forward flag, no airborne, no swimming
        const prevOk = (prev.movementFlags & FORWARD) !== 0
            && (prev.movementFlags & AIRBORNE_MASK) === 0
            && (prev.movementFlags & SWIMMING) === 0;
        const currOk = (curr.movementFlags & FORWARD) !== 0
            && (curr.movementFlags & AIRBORNE_MASK) === 0
            && (curr.movementFlags & SWIMMING) === 0;

        if (!prevOk || !currOk) continue;

        // EXCLUDE frames with TELEPORT_TO_PLANE flag (Z-snap)
        if ((prev.movementFlags & SNAP_FLAGS) !== 0 || (curr.movementFlags & SNAP_FLAGS) !== 0) continue;

        const dx = curr.position.x - prev.position.x;
        const dy = curr.position.y - prev.position.y;
        const dz = curr.position.z - prev.position.z;
        const dXY = Math.sqrt(dx * dx + dy * dy);

        // Skip no-movement frames
        if (dXY < 0.01) continue;

        const slopeAngle = Math.atan2(Math.abs(dz), dXY) * (180 / Math.PI);
        const direction = dz > 0.001 ? 'uphill' : (dz < -0.001 ? 'downhill' : 'flat');

        // Additional artifact filter: if dZ > 0.5 in a single 16ms frame, that is a Z-snap
        // At run speed 7, max horizontal in 16ms = 7 * 0.016 = 0.112 units
        // Even at 60 degrees slope, dZ would be at most 0.112 * tan(60) = 0.194
        // So dZ > 0.3 in a single frame is almost certainly a snap
        const isZSnap = Math.abs(dz) > 0.3;

        segments.push({
            frameIndex: i,
            prevPos: prev.position,
            currPos: curr.position,
            dXY, dZ: dz,
            slopeAngle, direction,
            prevFlags: prev.movementFlags,
            currFlags: curr.movementFlags,
            speed: curr.currentSpeed,
            filename,
            isZSnap
        });
    }

    const cleanSegments = segments.filter(s => !s.isZSnap);
    const snapSegments = segments.filter(s => s.isZSnap);

    console.log('  Total ground-forward segments: ' + segments.length);
    console.log('  Clean (no Z-snap): ' + cleanSegments.length);
    console.log('  Z-snap filtered: ' + snapSegments.length);

    if (snapSegments.length > 0) {
        console.log('  Z-snap examples:');
        for (let i = 0; i < Math.min(5, snapSegments.length); i++) {
            const s = snapSegments[i];
            console.log('    Frame ' + s.frameIndex + ': dZ=' + s.dZ.toFixed(3) + ' dXY=' + s.dXY.toFixed(4) + ' angle=' + s.slopeAngle.toFixed(1) + ' deg | flags=0x' + s.currFlags.toString(16).padStart(8,'0'));
        }
    }

    allCleanSegments.push(...cleanSegments);

    // PASS 2: Find sustained slope runs (3+ consecutive clean frames with angle > threshold)
    for (const threshold of [25, 30, 35, 40, 45]) {
        const uphillClean = cleanSegments.filter(s => s.direction === 'uphill').sort((a,b) => a.frameIndex - b.frameIndex);
        let run = [];
        for (const seg of uphillClean) {
            if (seg.slopeAngle >= threshold) {
                if (run.length === 0 || seg.frameIndex <= run[run.length-1].frameIndex + 2) {
                    run.push(seg);
                } else {
                    if (run.length >= 3) {
                        allSustainedRuns.push({ run: [...run], filename, threshold });
                    }
                    run = [seg];
                }
            } else {
                if (run.length >= 3) {
                    allSustainedRuns.push({ run: [...run], filename, threshold });
                }
                run = [];
            }
        }
        if (run.length >= 3) {
            allSustainedRuns.push({ run: [...run], filename, threshold });
        }
    }

    // PASS 3: Stall detection (walking into non-walkable slope)
    for (let i = 2; i < frames.length - 1; i++) {
        const prev2 = frames[i - 2];
        const prev = frames[i - 1];
        const curr = frames[i];

        const p2Ok = (prev2.movementFlags & FORWARD) !== 0 && (prev2.movementFlags & AIRBORNE_MASK) === 0
            && (prev2.movementFlags & SNAP_FLAGS) === 0;
        const p1Ok = (prev.movementFlags & FORWARD) !== 0 && (prev.movementFlags & AIRBORNE_MASK) === 0
            && (prev.movementFlags & SNAP_FLAGS) === 0;
        const cOk = (curr.movementFlags & FORWARD) !== 0 && (curr.movementFlags & AIRBORNE_MASK) === 0;

        if (!p2Ok || !p1Ok || !cOk) continue;

        const dx1 = prev.position.x - prev2.position.x;
        const dy1 = prev.position.y - prev2.position.y;
        const dz1 = prev.position.z - prev2.position.z;
        const dXY1 = Math.sqrt(dx1*dx1 + dy1*dy1);

        const dx2 = curr.position.x - prev.position.x;
        const dy2 = curr.position.y - prev.position.y;
        const dz2 = curr.position.z - prev.position.z;
        const dXY2 = Math.sqrt(dx2*dx2 + dy2*dy2);

        // Was climbing (uphill + actual horizontal movement), now stalled
        if (dz1 > 0.03 && dXY1 > 0.03 && dXY2 < 0.015 && Math.abs(dz2) < 0.015) {
            const angle1 = Math.atan2(Math.abs(dz1), dXY1) * (180/Math.PI);
            allStalls.push({
                frameIndex: i,
                slopeAngle: angle1,
                dZ: dz1,
                dXY: dXY1,
                pos: curr.position,
                filename
            });
        }
    }

    // PASS 4: Ground-to-fall transitions
    // Include JUMPING transitions too -- when walking forward on steep ground,
    // the character may transition to jumping/falling
    for (let i = 1; i < frames.length; i++) {
        const prev = frames[i - 1];
        const curr = frames[i];

        const prevGrounded = (prev.movementFlags & FORWARD) !== 0
            && (prev.movementFlags & AIRBORNE_MASK) === 0
            && (prev.movementFlags & SWIMMING) === 0;

        // Current frame is airborne (falling, falling far, or has PENDING_STOP with no forward)
        const currFalling = (curr.movementFlags & FALLING_FAR) !== 0;
        const currJumpFall = (curr.movementFlags & JUMPING) !== 0 && (curr.movementFlags & FORWARD) !== 0;

        if (prevGrounded && (currFalling || ((curr.movementFlags & FALLING) !== 0))) {
            const dx = curr.position.x - prev.position.x;
            const dy = curr.position.y - prev.position.y;
            const dz = curr.position.z - prev.position.z;
            const dXY = Math.sqrt(dx * dx + dy * dy);

            // Look back a few frames to get the slope leading up to the fall
            let approachAngle = 0;
            if (i >= 3) {
                const f3 = frames[i-3];
                const f1 = frames[i-1];
                const adx = f1.position.x - f3.position.x;
                const ady = f1.position.y - f3.position.y;
                const adz = f1.position.z - f3.position.z;
                const adXY = Math.sqrt(adx*adx + ady*ady);
                if (adXY > 0.01) {
                    approachAngle = Math.atan2(Math.abs(adz), adXY) * (180/Math.PI);
                }
            }

            allFallTransitions.push({
                frameIndex: i,
                prevPos: prev.position,
                currPos: curr.position,
                dXY, dZ: dz,
                slopeAngle: dXY > 0.01 ? Math.atan2(Math.abs(dz), dXY) * (180/Math.PI) : 0,
                approachAngle,
                prevFlags: prev.movementFlags,
                currFlags: curr.movementFlags,
                filename
            });
        }
    }
}

// ============ GLOBAL RESULTS ============

console.log('\n\n==========================================================');
console.log('GLOBAL RESULTS (Z-snap filtered)');
console.log('==========================================================');
console.log('Total clean ground-forward segments: ' + allCleanSegments.length);

// TOP 20 CLEAN
const sortedClean = [...allCleanSegments].sort((a,b) => b.slopeAngle - a.slopeAngle);
console.log('\n--- TOP 30 STEEPEST CLEAN GROUND SEGMENTS ---');
for (let i = 0; i < Math.min(30, sortedClean.length); i++) {
    const s = sortedClean[i];
    console.log('  #' + (i+1).toString().padStart(2) + ': ' + s.slopeAngle.toFixed(2) + ' deg ' + s.direction.padEnd(8) + ' | Frame ' + s.frameIndex + ' | dXY=' + s.dXY.toFixed(4) + ' dZ=' + s.dZ.toFixed(4) + ' | pos=(' + s.currPos.x.toFixed(2) + ',' + s.currPos.y.toFixed(2) + ',' + s.currPos.z.toFixed(2) + ') | ' + path.basename(s.filename, '.json').substring(0, 40));
}

// UPHILL only
const uphillClean = allCleanSegments.filter(s => s.direction === 'uphill').sort((a,b) => b.slopeAngle - a.slopeAngle);
console.log('\n--- TOP 20 STEEPEST UPHILL (CLEAN) ---');
for (let i = 0; i < Math.min(20, uphillClean.length); i++) {
    const s = uphillClean[i];
    console.log('  #' + (i+1).toString().padStart(2) + ': ' + s.slopeAngle.toFixed(2) + ' deg | Frame ' + s.frameIndex + ' | dXY=' + s.dXY.toFixed(4) + ' dZ=' + s.dZ.toFixed(4) + ' | pos=(' + s.currPos.x.toFixed(2) + ',' + s.currPos.y.toFixed(2) + ',' + s.currPos.z.toFixed(2) + ') | ' + path.basename(s.filename, '.json').substring(0, 40));
}

// DISTRIBUTION
console.log('\n--- CLEAN SLOPE ANGLE DISTRIBUTION ---');
const bins = [0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90];
for (let b = 0; b < bins.length - 1; b++) {
    const count = allCleanSegments.filter(s => s.slopeAngle >= bins[b] && s.slopeAngle < bins[b+1]).length;
    if (count > 0) {
        const bar = '#'.repeat(Math.min(Math.ceil(count / 10), 80));
        console.log('  ' + bins[b].toString().padStart(2) + '-' + bins[b+1].toString().padStart(2) + ' deg: ' + count.toString().padStart(5) + ' ' + bar);
    }
}

// SUSTAINED CLIMBS - only threshold=30
console.log('\n--- SUSTAINED UPHILL CLIMBS (3+ frames above 30 deg, clean) ---');
const sustained30 = allSustainedRuns.filter(r => r.threshold === 30)
    .sort((a,b) => {
        const maxA = Math.max(...a.run.map(r => r.slopeAngle));
        const maxB = Math.max(...b.run.map(r => r.slopeAngle));
        return maxB - maxA;
    });
for (let i = 0; i < Math.min(15, sustained30.length); i++) {
    const climb = sustained30[i];
    const angles = climb.run.map(r => r.slopeAngle);
    const maxAngle = Math.max(...angles);
    const avgAngle = angles.reduce((a,b) => a+b,0) / angles.length;
    const totalDZ = climb.run.reduce((acc, r) => acc + r.dZ, 0);
    console.log('  Climb #' + (i+1) + ': ' + climb.run.length + ' frames | max=' + maxAngle.toFixed(1) + ' deg | avg=' + avgAngle.toFixed(1) + ' deg | totalDZ=' + totalDZ.toFixed(3));
    console.log('    File: ' + path.basename(climb.filename));
    console.log('    Frames ' + climb.run[0].frameIndex + '-' + climb.run[climb.run.length-1].frameIndex);
    console.log('    Start: (' + climb.run[0].prevPos.x.toFixed(3) + ', ' + climb.run[0].prevPos.y.toFixed(3) + ', ' + climb.run[0].prevPos.z.toFixed(3) + ')');
    console.log('    End:   (' + climb.run[climb.run.length-1].currPos.x.toFixed(3) + ', ' + climb.run[climb.run.length-1].currPos.y.toFixed(3) + ', ' + climb.run[climb.run.length-1].currPos.z.toFixed(3) + ')');
    console.log('    Per-frame angles: ' + angles.map(a => a.toFixed(1)).join(', '));
}

// SUSTAINED at 25 deg
console.log('\n--- SUSTAINED UPHILL CLIMBS (3+ frames above 25 deg, clean) ---');
const sustained25 = allSustainedRuns.filter(r => r.threshold === 25)
    .sort((a,b) => {
        const avgA = a.run.reduce((acc,r)=>acc+r.slopeAngle,0)/a.run.length;
        const avgB = b.run.reduce((acc,r)=>acc+r.slopeAngle,0)/b.run.length;
        return avgB - avgA;
    });
for (let i = 0; i < Math.min(15, sustained25.length); i++) {
    const climb = sustained25[i];
    const angles = climb.run.map(r => r.slopeAngle);
    const maxAngle = Math.max(...angles);
    const avgAngle = angles.reduce((a,b) => a+b,0) / angles.length;
    const totalDZ = climb.run.reduce((acc, r) => acc + r.dZ, 0);
    console.log('  Climb #' + (i+1) + ': ' + climb.run.length + ' frames | max=' + maxAngle.toFixed(1) + ' deg | avg=' + avgAngle.toFixed(1) + ' deg | totalDZ=' + totalDZ.toFixed(3));
    console.log('    File: ' + path.basename(climb.filename));
    console.log('    Frames ' + climb.run[0].frameIndex + '-' + climb.run[climb.run.length-1].frameIndex);
    console.log('    Per-frame angles: ' + angles.map(a => a.toFixed(1)).join(', '));
}

// STALLS
console.log('\n--- SLOPE STALLS (walking into max slope) ---');
console.log('Total stalls: ' + allStalls.length);
const stallAngles = allStalls.map(s => s.slopeAngle).sort((a,b) => b - a);
if (stallAngles.length > 0) {
    console.log('  Stall slope angles (sorted desc): ' + stallAngles.slice(0,30).map(a => a.toFixed(1)).join(', '));
    console.log('  Max stall angle: ' + stallAngles[0].toFixed(2) + ' deg');
    console.log('  Avg stall angle: ' + (stallAngles.reduce((a,b)=>a+b,0)/stallAngles.length).toFixed(2) + ' deg');
    console.log('  Median stall angle: ' + stallAngles[Math.floor(stallAngles.length/2)].toFixed(2) + ' deg');
}

// Stall details top 10
allStalls.sort((a,b) => b.slopeAngle - a.slopeAngle);
console.log('\n  Top 15 stalls:');
for (let i = 0; i < Math.min(15, allStalls.length); i++) {
    const s = allStalls[i];
    console.log('    #' + (i+1) + ': ' + s.slopeAngle.toFixed(1) + ' deg | Frame ' + s.frameIndex + ' | dZ=' + s.dZ.toFixed(3) + ' dXY=' + s.dXY.toFixed(3) + ' | pos=(' + s.pos.x.toFixed(2) + ',' + s.pos.y.toFixed(2) + ',' + s.pos.z.toFixed(2) + ') | ' + path.basename(s.filename, '.json').substring(0,40));
}

// FALL TRANSITIONS
console.log('\n--- GROUND TO FALL TRANSITIONS ---');
console.log('Total: ' + allFallTransitions.length);
allFallTransitions.sort((a,b) => b.approachAngle - a.approachAngle);
for (let i = 0; i < Math.min(10, allFallTransitions.length); i++) {
    const t = allFallTransitions[i];
    console.log('  #' + (i+1) + ': approach=' + t.approachAngle.toFixed(1) + ' deg | transition=' + t.slopeAngle.toFixed(1) + ' deg | Frame ' + t.frameIndex);
    console.log('      flags: 0x' + t.prevFlags.toString(16).padStart(8,'0') + ' -> 0x' + t.currFlags.toString(16).padStart(8,'0'));
    console.log('      pos: (' + t.currPos.x.toFixed(2) + ',' + t.currPos.y.toFixed(2) + ',' + t.currPos.z.toFixed(2) + ') | ' + path.basename(t.filename, '.json').substring(0,40));
}

// SUMMARY STATISTICS (clean)
console.log('\n--- CLEAN SUMMARY STATISTICS ---');
const cleanAngles = allCleanSegments.map(s => s.slopeAngle).sort((a,b) => a - b);
console.log('  Total clean segments: ' + cleanAngles.length);
console.log('  Median: ' + cleanAngles[Math.floor(cleanAngles.length/2)].toFixed(2) + ' deg');
console.log('  90th pct: ' + cleanAngles[Math.floor(cleanAngles.length * 0.90)].toFixed(2) + ' deg');
console.log('  95th pct: ' + cleanAngles[Math.floor(cleanAngles.length * 0.95)].toFixed(2) + ' deg');
console.log('  99th pct: ' + cleanAngles[Math.floor(cleanAngles.length * 0.99)].toFixed(2) + ' deg');
console.log('  99.5th pct: ' + cleanAngles[Math.floor(cleanAngles.length * 0.995)].toFixed(2) + ' deg');
console.log('  Maximum: ' + cleanAngles[cleanAngles.length - 1].toFixed(2) + ' deg');

// KEY INSIGHT: Where do most frames cluster around the suspected 50-60 deg max?
console.log('\n--- FINE-GRAINED DISTRIBUTION 25-60 deg ---');
for (let angle = 25; angle < 60; angle += 2) {
    const count = allCleanSegments.filter(s => s.slopeAngle >= angle && s.slopeAngle < angle + 2).length;
    if (count > 0) {
        const bar = '#'.repeat(Math.min(count, 60));
        console.log('  ' + angle.toString().padStart(2) + '-' + (angle+2).toString().padStart(2) + ' deg: ' + count.toString().padStart(4) + ' ' + bar);
    }
}

// Check: how many clean segments exist above various thresholds
console.log('\n--- SEGMENT COUNTS ABOVE THRESHOLDS ---');
for (const thresh of [30, 35, 40, 45, 50, 55, 60]) {
    const count = allCleanSegments.filter(s => s.slopeAngle >= thresh).length;
    const uphillCount = allCleanSegments.filter(s => s.slopeAngle >= thresh && s.direction === 'uphill').length;
    console.log('  >= ' + thresh + ' deg: ' + count + ' total, ' + uphillCount + ' uphill');
}
