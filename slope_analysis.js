const fs = require('fs');
const path = require('path');

const FORWARD = 0x1;
const BACKWARD = 0x2;
const JUMPING = 0x2000;
const FALLING_FAR = 0x4000;
const SWIMMING = 0x200000;
const FALLING = 0x800000;
const AIRBORNE_MASK = JUMPING | FALLING_FAR | FALLING;

const recordingsDir = path.join(process.env.USERPROFILE, 'Documents', 'BloogBot', 'MovementRecordings');

// List of Orgrimmar recordings to analyze
const files = fs.readdirSync(recordingsDir)
    .filter(f => f.endsWith('.json') && f.includes('Orgrimmar'))
    .sort()
    .reverse();

console.log('=== Orgrimmar Recording Files ===');
files.forEach(f => console.log('  ' + f + ' (' + (fs.statSync(path.join(recordingsDir, f)).size / 1024 / 1024).toFixed(1) + ' MB)'));
console.log('');

// Analyze all recordings
const allSlopeSegments = [];
const allTransitions = [];

for (const filename of files) {
    const filepath = path.join(recordingsDir, filename);
    console.log('\n========================================');
    console.log('Analyzing: ' + filename);
    console.log('========================================');

    const data = JSON.parse(fs.readFileSync(filepath, 'utf8'));
    const frames = data.frames;
    console.log('  Description: ' + data.description);
    console.log('  Frame count: ' + frames.length);
    console.log('  Duration: ' + (data.durationMs / 1000).toFixed(1) + 's');

    // Compute slopes between consecutive grounded forward-moving frames
    const slopeData = [];

    for (let i = 1; i < frames.length; i++) {
        const prev = frames[i - 1];
        const curr = frames[i];

        // Both frames must have Forward flag and no airborne flags
        const prevGrounded = (prev.movementFlags & FORWARD) !== 0
            && (prev.movementFlags & AIRBORNE_MASK) === 0
            && (prev.movementFlags & SWIMMING) === 0;
        const currGrounded = (curr.movementFlags & FORWARD) !== 0
            && (curr.movementFlags & AIRBORNE_MASK) === 0
            && (curr.movementFlags & SWIMMING) === 0;

        if (!prevGrounded || !currGrounded) continue;

        const dx = curr.position.x - prev.position.x;
        const dy = curr.position.y - prev.position.y;
        const dz = curr.position.z - prev.position.z;
        const dXY = Math.sqrt(dx * dx + dy * dy);

        // Skip frames with no movement (standing still with forward flag)
        if (dXY < 0.001) continue;

        const slopeAngle = Math.atan2(Math.abs(dz), dXY) * (180 / Math.PI);
        const slopeSign = dz > 0 ? 'uphill' : (dz < 0 ? 'downhill' : 'flat');
        const totalDist = Math.sqrt(dx * dx + dy * dy + dz * dz);

        slopeData.push({
            frameIndex: i,
            prevPos: prev.position,
            currPos: curr.position,
            dXY,
            dZ: dz,
            slopeAngle,
            slopeSign,
            totalDist,
            prevFlags: prev.movementFlags,
            currFlags: curr.movementFlags,
            facing: curr.facing,
            speed: curr.currentSpeed,
            filename
        });
    }

    console.log('  Ground forward frames with movement: ' + slopeData.length);

    if (slopeData.length === 0) continue;

    // Sort by slope angle descending
    const sortedSlopes = [...slopeData].sort((a, b) => b.slopeAngle - a.slopeAngle);

    // Top 10 steepest for this file
    console.log('\n  --- Top 10 Steepest Ground Slopes ---');
    for (let i = 0; i < Math.min(10, sortedSlopes.length); i++) {
        const s = sortedSlopes[i];
        console.log('    #' + (i+1) + ': Frame ' + s.frameIndex + ' | ' + s.slopeAngle.toFixed(2) + ' deg ' + s.slopeSign + ' | dXY=' + s.dXY.toFixed(4) + ' dZ=' + s.dZ.toFixed(4) + ' | pos=(' + s.currPos.x.toFixed(2) + ', ' + s.currPos.y.toFixed(2) + ', ' + s.currPos.z.toFixed(2) + ') | flags=0x' + s.currFlags.toString(16).padStart(8,'0') + ' | speed=' + s.speed);
    }

    allSlopeSegments.push(...slopeData);

    // Find slope transitions: grounded forward -> airborne
    for (let i = 1; i < frames.length; i++) {
        const prev = frames[i - 1];
        const curr = frames[i];

        const prevGroundedForward = (prev.movementFlags & FORWARD) !== 0
            && (prev.movementFlags & AIRBORNE_MASK) === 0
            && (prev.movementFlags & SWIMMING) === 0;
        const currAirborne = (curr.movementFlags & (FALLING_FAR | FALLING)) !== 0;

        if (prevGroundedForward && currAirborne) {
            const dx = curr.position.x - prev.position.x;
            const dy = curr.position.y - prev.position.y;
            const dz = curr.position.z - prev.position.z;
            const dXY = Math.sqrt(dx * dx + dy * dy);
            const slopeAngle = dXY > 0.001 ? Math.atan2(Math.abs(dz), dXY) * (180 / Math.PI) : 0;

            allTransitions.push({
                frameIndex: i,
                prevPos: prev.position,
                currPos: curr.position,
                dXY,
                dZ: dz,
                slopeAngle,
                prevFlags: prev.movementFlags,
                currFlags: curr.movementFlags,
                filename
            });
        }
    }

    // Slope stall detection
    console.log('\n  --- Slope Stall Detection (walking into wall/max slope) ---');
    let stallCount = 0;
    for (let i = 2; i < frames.length - 1; i++) {
        const prev2 = frames[i - 2];
        const prev = frames[i - 1];
        const curr = frames[i];

        const p2Grounded = (prev2.movementFlags & FORWARD) !== 0 && (prev2.movementFlags & AIRBORNE_MASK) === 0;
        const p1Grounded = (prev.movementFlags & FORWARD) !== 0 && (prev.movementFlags & AIRBORNE_MASK) === 0;
        const cGrounded = (curr.movementFlags & FORWARD) !== 0 && (curr.movementFlags & AIRBORNE_MASK) === 0;

        if (!p2Grounded || !p1Grounded || !cGrounded) continue;

        const dx1 = prev.position.x - prev2.position.x;
        const dy1 = prev.position.y - prev2.position.y;
        const dz1 = prev.position.z - prev2.position.z;
        const dXY1 = Math.sqrt(dx1*dx1 + dy1*dy1);

        const dx2 = curr.position.x - prev.position.x;
        const dy2 = curr.position.y - prev.position.y;
        const dz2 = curr.position.z - prev.position.z;
        const dXY2 = Math.sqrt(dx2*dx2 + dy2*dy2);

        // Was gaining Z (uphill), now not gaining Z or horizontal distance
        if (dz1 > 0.05 && dXY1 > 0.05 && dXY2 < 0.02 && Math.abs(dz2) < 0.02) {
            const angle1 = Math.atan2(Math.abs(dz1), dXY1) * (180/Math.PI);
            if (stallCount < 15) {
                console.log('    Frame ' + i + ': Was climbing at ' + angle1.toFixed(1) + ' deg (dZ=' + dz1.toFixed(3) + ', dXY=' + dXY1.toFixed(3) + ') -> STALLED (dZ=' + dz2.toFixed(3) + ', dXY=' + dXY2.toFixed(3) + ') | pos=(' + curr.position.x.toFixed(2) + ', ' + curr.position.y.toFixed(2) + ', ' + curr.position.z.toFixed(2) + ')');
            }
            stallCount++;
        }
    }
    if (stallCount > 15) console.log('    ... and ' + (stallCount - 15) + ' more stalls');
    if (stallCount === 0) console.log('    No slope stalls detected');
}

// Global analysis
console.log('\n\n========================================');
console.log('GLOBAL ANALYSIS ACROSS ALL ORGRIMMAR RECORDINGS');
console.log('========================================');
console.log('Total ground forward segments: ' + allSlopeSegments.length);

// Top 20 steepest overall
const globalSorted = [...allSlopeSegments].sort((a, b) => b.slopeAngle - a.slopeAngle);
console.log('\n--- TOP 20 STEEPEST GROUND MOVEMENT SEGMENTS ---');
for (let i = 0; i < Math.min(20, globalSorted.length); i++) {
    const s = globalSorted[i];
    console.log('  #' + (i+1) + ': ' + s.slopeAngle.toFixed(2) + ' deg ' + s.slopeSign + ' | Frame ' + s.frameIndex + ' in ' + s.filename);
    console.log('        pos=(' + s.currPos.x.toFixed(3) + ', ' + s.currPos.y.toFixed(3) + ', ' + s.currPos.z.toFixed(3) + ') | dXY=' + s.dXY.toFixed(4) + ' dZ=' + s.dZ.toFixed(4) + ' | speed=' + s.speed + ' | flags=0x' + s.currFlags.toString(16).padStart(8,'0'));
}

// Distribution
console.log('\n--- SLOPE ANGLE DISTRIBUTION ---');
const bins = [0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90];
for (let b = 0; b < bins.length - 1; b++) {
    const count = allSlopeSegments.filter(s => s.slopeAngle >= bins[b] && s.slopeAngle < bins[b+1]).length;
    if (count > 0) {
        const bar = '#'.repeat(Math.min(count, 80));
        console.log('  ' + bins[b].toString().padStart(2) + '-' + bins[b+1].toString().padStart(2) + ' deg: ' + count.toString().padStart(5) + ' ' + bar);
    }
}

// Uphill vs downhill
const uphillSegments = allSlopeSegments.filter(s => s.slopeSign === 'uphill');
const downhillSegments = allSlopeSegments.filter(s => s.slopeSign === 'downhill');
console.log('\n--- UPHILL TOP 10 STEEPEST ---');
const uphillSorted = uphillSegments.sort((a, b) => b.slopeAngle - a.slopeAngle);
for (let i = 0; i < Math.min(10, uphillSorted.length); i++) {
    const s = uphillSorted[i];
    console.log('  #' + (i+1) + ': ' + s.slopeAngle.toFixed(2) + ' deg | Frame ' + s.frameIndex + ' in ' + s.filename + ' | pos=(' + s.currPos.x.toFixed(2) + ', ' + s.currPos.y.toFixed(2) + ', ' + s.currPos.z.toFixed(2) + ') | dXY=' + s.dXY.toFixed(4) + ' dZ=' + s.dZ.toFixed(4));
}

console.log('\n--- DOWNHILL TOP 10 STEEPEST ---');
const downhillSorted = downhillSegments.sort((a, b) => b.slopeAngle - a.slopeAngle);
for (let i = 0; i < Math.min(10, downhillSorted.length); i++) {
    const s = downhillSorted[i];
    console.log('  #' + (i+1) + ': ' + s.slopeAngle.toFixed(2) + ' deg | Frame ' + s.frameIndex + ' in ' + s.filename + ' | pos=(' + s.currPos.x.toFixed(2) + ', ' + s.currPos.y.toFixed(2) + ', ' + s.currPos.z.toFixed(2) + ') | dXY=' + s.dXY.toFixed(4) + ' dZ=' + s.dZ.toFixed(4));
}

// Ground-to-airborne transitions
console.log('\n--- GROUND TO AIRBORNE TRANSITIONS ---');
console.log('Total transitions: ' + allTransitions.length);
allTransitions.sort((a, b) => b.slopeAngle - a.slopeAngle);
for (let i = 0; i < Math.min(15, allTransitions.length); i++) {
    const t = allTransitions[i];
    console.log('  #' + (i+1) + ': ' + t.slopeAngle.toFixed(2) + ' deg | Frame ' + t.frameIndex + ' in ' + t.filename);
    console.log('        prevFlags=0x' + t.prevFlags.toString(16).padStart(8,'0') + ' -> currFlags=0x' + t.currFlags.toString(16).padStart(8,'0'));
    console.log('        prevPos=(' + t.prevPos.x.toFixed(3) + ', ' + t.prevPos.y.toFixed(3) + ', ' + t.prevPos.z.toFixed(3) + ')');
    console.log('        currPos=(' + t.currPos.x.toFixed(3) + ', ' + t.currPos.y.toFixed(3) + ', ' + t.currPos.z.toFixed(3) + ')');
    console.log('        dXY=' + t.dXY.toFixed(4) + ' dZ=' + t.dZ.toFixed(4));
}

// Sustained steep climbs
console.log('\n--- SUSTAINED STEEP CLIMBS (3+ consecutive frames above 25 deg) ---');
const sustainedClimbs = [];
for (const filename of files) {
    const fileSegments = allSlopeSegments
        .filter(s => s.filename === filename && s.slopeSign === 'uphill')
        .sort((a, b) => a.frameIndex - b.frameIndex);

    let run = [];
    for (let i = 0; i < fileSegments.length; i++) {
        const seg = fileSegments[i];
        if (seg.slopeAngle >= 25) {
            if (run.length === 0 || seg.frameIndex <= run[run.length - 1].frameIndex + 2) {
                run.push(seg);
            } else {
                if (run.length >= 3) {
                    sustainedClimbs.push({ run: [...run], filename });
                }
                run = [seg];
            }
        } else {
            if (run.length >= 3) {
                sustainedClimbs.push({ run: [...run], filename });
            }
            run = [];
        }
    }
    if (run.length >= 3) {
        sustainedClimbs.push({ run: [...run], filename });
    }
}

sustainedClimbs.sort((a, b) => {
    const maxA = Math.max(...a.run.map(r => r.slopeAngle));
    const maxB = Math.max(...b.run.map(r => r.slopeAngle));
    return maxB - maxA;
});

for (let i = 0; i < Math.min(10, sustainedClimbs.length); i++) {
    const climb = sustainedClimbs[i];
    const angles = climb.run.map(r => r.slopeAngle);
    const maxAngle = Math.max(...angles);
    const avgAngle = angles.reduce((a, b) => a + b, 0) / angles.length;
    const totalDZ = climb.run.reduce((acc, r) => acc + r.dZ, 0);
    console.log('  Climb #' + (i+1) + ': ' + climb.run.length + ' frames, max=' + maxAngle.toFixed(1) + ' deg, avg=' + avgAngle.toFixed(1) + ' deg, totalDZ=' + totalDZ.toFixed(2) + ' | ' + climb.filename);
    console.log('    Frames ' + climb.run[0].frameIndex + '-' + climb.run[climb.run.length-1].frameIndex);
    console.log('    Start: (' + climb.run[0].prevPos.x.toFixed(2) + ', ' + climb.run[0].prevPos.y.toFixed(2) + ', ' + climb.run[0].prevPos.z.toFixed(2) + ')');
    console.log('    End:   (' + climb.run[climb.run.length-1].currPos.x.toFixed(2) + ', ' + climb.run[climb.run.length-1].currPos.y.toFixed(2) + ', ' + climb.run[climb.run.length-1].currPos.z.toFixed(2) + ')');
    console.log('    Angles: ' + angles.map(a => a.toFixed(1)).join(', '));
}

// Summary statistics
console.log('\n--- SUMMARY ---');
const allAngles = allSlopeSegments.map(s => s.slopeAngle);
allAngles.sort((a, b) => a - b);
console.log('  Total segments: ' + allAngles.length);
console.log('  Median slope: ' + allAngles[Math.floor(allAngles.length/2)].toFixed(2) + ' deg');
console.log('  90th percentile: ' + allAngles[Math.floor(allAngles.length * 0.90)].toFixed(2) + ' deg');
console.log('  95th percentile: ' + allAngles[Math.floor(allAngles.length * 0.95)].toFixed(2) + ' deg');
console.log('  99th percentile: ' + allAngles[Math.floor(allAngles.length * 0.99)].toFixed(2) + ' deg');
console.log('  Maximum: ' + allAngles[allAngles.length - 1].toFixed(2) + ' deg');
console.log('  Max uphill: ' + (uphillSorted.length > 0 ? uphillSorted[0].slopeAngle.toFixed(2) : 'N/A') + ' deg');
console.log('  Max downhill: ' + (downhillSorted.length > 0 ? downhillSorted[0].slopeAngle.toFixed(2) : 'N/A') + ' deg');
