# PhysX Capsule Sweep Collision Detection Rules

> **Purpose**: This document defines the exact algorithm for capsule-vs-geometry sweep tests. These rules cover the collision detection primitives that feed into the CCT movement system.

---

## Table of Contents

1. [Capsule Geometry Fundamentals](#1-capsule-geometry-fundamentals)
2. [Sweep Test Types](#2-sweep-test-types)
3. [Broadphase Query](#3-broadphase-query)
4. [Capsule-Triangle Sweep](#4-capsule-triangle-sweep)
5. [Capsule-Triangle Overlap](#5-capsule-triangle-overlap)
6. [Capsule Region Classification](#6-capsule-region-classification)
7. [Normal Orientation Rules](#7-normal-orientation-rules)
8. [Contact Point Computation](#8-contact-point-computation)
9. [Penetration Depth Calculation](#9-penetration-depth-calculation)
10. [Multi-Geometry Collision (CollideGeoms)](#10-multi-geometry-collision-collidegeoms)
11. [Temporal Coherence and Caching](#11-temporal-coherence-and-caching)
12. [Coordinate Transform Rules](#12-coordinate-transform-rules)

---

## 1. Capsule Geometry Fundamentals

### 1.1 Capsule Definition

A capsule is defined as the Minkowski sum of a line segment and a sphere.

```
struct Capsule:
    p0: Vector3      # Center of bottom hemisphere
    p1: Vector3      # Center of top hemisphere  
    radius: float    # Radius of hemispheres and cylinder

Properties:
    axis = p1 - p0                           # Capsule axis vector
    height = magnitude(axis)                 # Cylinder height (segment length)
    totalHeight = height + 2 * radius        # Total capsule height
    halfHeight = totalHeight / 2             # Half total height
    center = (p0 + p1) / 2                   # Capsule center
    direction = normalize(axis)              # Unit axis direction
```

### 1.2 Capsule Construction from Feet Position

```
function buildCapsuleFromFeet(feetX: float, feetY: float, feetZ: float,
                               radius: float, totalHeight: float) -> Capsule:

STEP 1.2.1: Compute cylinder height
    cylinderHeight = totalHeight - 2 * radius
    
    # Guard against degenerate capsule
    IF cylinderHeight < 0:
        cylinderHeight = 0  # Becomes a sphere

STEP 1.2.2: Compute hemisphere centers
    # p0 is at feet + radius (bottom hemisphere center)
    p0 = Vector3(feetX, feetY, feetZ + radius)
    
    # p1 is at p0 + cylinderHeight (top hemisphere center)
    p1 = Vector3(feetX, feetY, feetZ + radius + cylinderHeight)

STEP 1.2.3: Return capsule
    RETURN Capsule(p0, p1, radius)
```

### 1.3 Capsule Bounds (AABB)

```
function getCapsuleBounds(capsule: Capsule) -> AABB:
    
STEP 1.3.1: Find min/max of endpoints
    minPt = min(capsule.p0, capsule.p1)
    maxPt = max(capsule.p0, capsule.p1)

STEP 1.3.2: Expand by radius
    bounds.min = minPt - Vector3(radius, radius, radius)
    bounds.max = maxPt + Vector3(radius, radius, radius)
    
STEP 1.3.3: Return bounds
    RETURN bounds
```

---

## 2. Sweep Test Types

### 2.1 Linear Sweep

Move capsule along a linear path and find first contact.

```
Input:
    capsule: Capsule          # Starting capsule
    direction: Vector3        # Normalized movement direction
    distance: float           # Maximum sweep distance
    
Output:
    hit: bool                 # Whether collision occurred
    time: float               # Time of impact [0, 1]
    distance: float           # Distance to impact
    point: Vector3            # World-space contact point
    normal: Vector3           # Surface normal at contact
```

> **⚠️ CRITICAL**: When calling sweep from the CCT movement system, the `distance`
> parameter should be `remainingDistance + contactOffset`, NOT just `remainingDistance`.
> This ensures collisions within skin-width are detected. The CCT then subtracts
> `contactOffset` when computing the safe advance distance.

### 2.2 Overlap Test

Test if capsule overlaps geometry at current position.

```
Input:
    capsule: Capsule          # Capsule to test
    
Output:
    overlapping: bool         # Whether overlap exists
    depth: float              # Penetration depth
    normal: Vector3           # Direction to resolve (points out of geometry)
    point: Vector3            # Deepest penetration point
```

### 2.3 Closest Points Query

Find closest points between capsule and geometry (for MTD).

```
Input:
    capsule: Capsule
    geometry: Geometry
    
Output:
    pointOnCapsule: Vector3
    pointOnGeometry: Vector3
    distance: float           # Negative if penetrating
```

---

## 3. Broadphase Query

### 3.1 Swept AABB Computation

```
function computeSweptAABB(capsule: Capsule, direction: Vector3, 
                          distance: float) -> AABB:

STEP 3.1.1: Get capsule bounds at start position
    startBounds = getCapsuleBounds(capsule)

STEP 3.1.2: Compute capsule at end position
    endCapsule.p0 = capsule.p0 + direction * distance
    endCapsule.p1 = capsule.p1 + direction * distance
    endCapsule.radius = capsule.radius
    endBounds = getCapsuleBounds(endCapsule)

STEP 3.1.3: Union bounds
    sweptBounds.min = min(startBounds.min, endBounds.min)
    sweptBounds.max = max(startBounds.max, endBounds.max)

STEP 3.1.4: Inflate for numerical safety
    inflation = max(0.001, capsule.radius * 0.01)
    sweptBounds.min -= Vector3(inflation, inflation, inflation)
    sweptBounds.max += Vector3(inflation, inflation, inflation)

STEP 3.1.5: Return swept bounds
    RETURN sweptBounds
```

### 3.2 Broadphase Triangle Query

```
function queryTrianglesInBounds(bounds: AABB, scene: Scene) -> TriangleList:

STEP 3.2.1: Query spatial acceleration structure (BVH/Octree)
    candidates = scene.bvh.query(bounds)

STEP 3.2.2: Filter by collision mask (if applicable)
    filtered = []
    FOR EACH tri IN candidates:
        IF tri.collisionMask & desiredMask:
            filtered.append(tri)

STEP 3.2.3: Return candidate list
    RETURN filtered
```

### 3.3 Vertical Window Filter (Optimization)

```
function filterByVerticalWindow(triangles: TriangleList, capsule: Capsule,
                                 direction: Vector3, distance: float) -> TriangleList:

STEP 3.3.1: Compute capsule Z window
    capsuleMinZ = min(capsule.p0.z, capsule.p1.z) - capsule.radius
    capsuleMaxZ = max(capsule.p0.z, capsule.p1.z) + capsule.radius

STEP 3.3.2: Extend window based on sweep direction
    IF direction.z < -0.5:
        # Downward sweep - include step-down range
        capsuleMinZ -= STEP_DOWN_HEIGHT
    
    IF direction.z > 0.5:
        # Upward sweep - extend top
        capsuleMaxZ += distance

STEP 3.3.3: Add epsilon tolerance
    epsilon = 0.05
    capsuleMinZ -= epsilon
    capsuleMaxZ += epsilon

STEP 3.3.4: Filter triangles
    result = []
    FOR EACH tri IN triangles:
        triMinZ = min(tri.a.z, tri.b.z, tri.c.z)
        triMaxZ = max(tri.a.z, tri.b.z, tri.c.z)
        
        # Keep if Z ranges overlap
        IF triMaxZ >= capsuleMinZ AND triMinZ <= capsuleMaxZ:
            result.append(tri)

STEP 3.3.5: Return filtered list
    RETURN result
```

---

## 4. Capsule-Triangle Sweep

### 4.1 Overview

The capsule-triangle sweep is decomposed into:
1. Sweep sphere (radius = capsule.radius) against extruded triangle prism
2. Sweep segment (capsule axis) against triangle

### 4.2 Full Algorithm

```
function sweepCapsuleTriangle(capsule: Capsule, velocity: Vector3, 
                               triangle: Triangle, OUT toi: float,
                               OUT normal: Vector3, OUT point: Vector3) -> bool:

STEP 4.2.1: Early rejection - back-face culling
    triNormal = computeTriangleNormal(triangle)
    IF dot(velocity, triNormal) >= 0:
        # Moving away from or parallel to triangle
        RETURN FALSE

STEP 4.2.2: Compute capsule segment
    segStart = capsule.p0
    segEnd = capsule.p1
    segDir = segEnd - segStart
    segLen = magnitude(segDir)
    IF segLen > EPSILON:
        segDir = segDir / segLen

STEP 4.2.3: Sweep sphere against triangle plane
    # Find time when sphere first touches infinite plane
    planeDist = signedDistanceToPlane(capsule.p0, triangle)
    planeVel = dot(velocity, triNormal)
    
    IF abs(planeVel) < EPSILON:
        # Moving parallel to plane
        IF abs(planeDist) > capsule.radius:
            RETURN FALSE  # No intersection
        tPlane = 0  # Already within plane slab
    ELSE:
        # Time to reach plane (accounting for radius)
        IF planeDist > 0:
            tPlane = (planeDist - capsule.radius) / (-planeVel)
        ELSE:
            tPlane = (planeDist + capsule.radius) / (-planeVel)

STEP 4.2.4: Test against triangle interior
    tBest = INFINITY
    bestNormal = Vector3(0, 0, 0)
    bestPoint = Vector3(0, 0, 0)
    
    IF tPlane >= 0 AND tPlane <= 1:
        # Check if contact point is inside triangle
        contactPt = capsule.center + velocity * tPlane - triNormal * capsule.radius
        IF pointInTriangle(contactPt, triangle):
            IF tPlane < tBest:
                tBest = tPlane
                bestNormal = triNormal
                bestPoint = contactPt

STEP 4.2.5: Sweep against triangle edges
    edges = [
        (triangle.a, triangle.b),
        (triangle.b, triangle.c),
        (triangle.c, triangle.a)
    ]
    
    FOR EACH (edgeStart, edgeEnd) IN edges:
        # Sweep capsule against edge (cylinder-edge test)
        hitEdge, tEdge, nEdge, pEdge = sweepCapsuleEdge(capsule, velocity, 
                                                         edgeStart, edgeEnd)
        IF hitEdge AND tEdge >= 0 AND tEdge < tBest:
            tBest = tEdge
            bestNormal = nEdge
            bestPoint = pEdge

STEP 4.2.6: Sweep against triangle vertices
    vertices = [triangle.a, triangle.b, triangle.c]
    
    FOR EACH vertex IN vertices:
        # Sweep capsule against vertex (sphere-point test)
        hitVert, tVert, nVert = sweepCapsuleVertex(capsule, velocity, vertex)
        IF hitVert AND tVert >= 0 AND tVert < tBest:
            tBest = tVert
            bestNormal = nVert
            bestPoint = vertex

STEP 4.2.7: Return result
    IF tBest <= 1.0:
        toi = tBest
        normal = bestNormal
        point = bestPoint
        RETURN TRUE
    ELSE:
        RETURN FALSE
```

### 4.3 Sweep Capsule Against Edge

```
function sweepCapsuleEdge(capsule: Capsule, velocity: Vector3,
                          edgeA: Vector3, edgeB: Vector3) -> (bool, float, Vector3, Vector3):

STEP 4.3.1: Compute edge direction
    edgeDir = edgeB - edgeA
    edgeLen = magnitude(edgeDir)
    IF edgeLen < EPSILON:
        RETURN (FALSE, 0, Vector3(0,0,0), Vector3(0,0,0))
    edgeDir = edgeDir / edgeLen

STEP 4.3.2: Find closest points between capsule segment and edge
    # This is a segment-segment closest point problem
    closestOnCapsule, closestOnEdge, segT, edgeT = closestPointsSegmentSegment(
        capsule.p0, capsule.p1, edgeA, edgeB)

STEP 4.3.3: Compute initial separation
    separation = closestOnEdge - closestOnCapsule
    sepDist = magnitude(separation)
    
    IF sepDist < EPSILON:
        # Degenerate case - use edge normal
        sepDir = cross(edgeDir, velocity)
        IF magnitude(sepDir) < EPSILON:
            sepDir = Vector3(0, 0, 1)  # Fallback
        sepDir = normalize(sepDir)
    ELSE:
        sepDir = separation / sepDist

STEP 4.3.4: Solve quadratic for sphere-cylinder intersection
    # Relative velocity
    relVel = velocity  # Edge is static
    
    # Project to 2D perpendicular to edge
    velPerp = relVel - edgeDir * dot(relVel, edgeDir)
    posPerp = closestOnCapsule - edgeA
    posPerp = posPerp - edgeDir * dot(posPerp, edgeDir)
    
    # Quadratic coefficients: |posPerp + t*velPerp|² = r²
    a = dot(velPerp, velPerp)
    b = 2 * dot(posPerp, velPerp)
    c = dot(posPerp, posPerp) - capsule.radius * capsule.radius
    
    discriminant = b*b - 4*a*c

STEP 4.3.5: Solve quadratic
    IF discriminant < 0:
        RETURN (FALSE, 0, Vector3(0,0,0), Vector3(0,0,0))
    
    sqrtDisc = sqrt(discriminant)
    t1 = (-b - sqrtDisc) / (2 * a)
    t2 = (-b + sqrtDisc) / (2 * a)
    
    t = t1  # Use earliest intersection
    IF t < 0:
        t = t2
    IF t < 0 OR t > 1:
        RETURN (FALSE, 0, Vector3(0,0,0), Vector3(0,0,0))

STEP 4.3.6: Verify edge parameter is within [0,1]
    hitPoint = closestOnCapsule + velocity * t
    # Project hit point onto edge
    edgeParam = dot(hitPoint - edgeA, edgeDir) / edgeLen
    IF edgeParam < 0 OR edgeParam > 1:
        RETURN (FALSE, 0, Vector3(0,0,0), Vector3(0,0,0))

STEP 4.3.7: Compute contact normal
    contactOnEdge = edgeA + edgeDir * (edgeParam * edgeLen)
    contactNormal = normalize(hitPoint - contactOnEdge)

STEP 4.3.8: Return result
    RETURN (TRUE, t, contactNormal, contactOnEdge)
```

### 4.4 Sweep Capsule Against Vertex

```
function sweepCapsuleVertex(capsule: Capsule, velocity: Vector3,
                            vertex: Vector3) -> (bool, float, Vector3):

STEP 4.4.1: Find closest point on capsule segment to vertex
    closestOnCapsule = closestPointOnSegment(capsule.p0, capsule.p1, vertex)

STEP 4.4.2: Compute relative position and velocity
    relPos = closestOnCapsule - vertex
    relVel = velocity  # Vertex is static

STEP 4.4.3: Solve sphere-point sweep (quadratic)
    # |relPos + t*relVel|² = r²
    a = dot(relVel, relVel)
    b = 2 * dot(relPos, relVel)
    c = dot(relPos, relPos) - capsule.radius * capsule.radius
    
    IF abs(a) < EPSILON:
        RETURN (FALSE, 0, Vector3(0,0,0))
    
    discriminant = b*b - 4*a*c
    IF discriminant < 0:
        RETURN (FALSE, 0, Vector3(0,0,0))

STEP 4.4.4: Get earliest positive root
    sqrtDisc = sqrt(discriminant)
    t = (-b - sqrtDisc) / (2 * a)
    IF t < 0:
        t = (-b + sqrtDisc) / (2 * a)
    
    IF t < 0 OR t > 1:
        RETURN (FALSE, 0, Vector3(0,0,0))

STEP 4.4.5: Compute contact normal
    hitPos = closestOnCapsule + velocity * t
    contactNormal = normalize(hitPos - vertex)

STEP 4.4.6: Return result
    RETURN (TRUE, t, contactNormal)
```

---

## 5. Capsule-Triangle Overlap

### 5.1 Overview

Test if capsule intersects triangle at current position. Returns penetration depth and resolution direction.

### 5.2 Full Algorithm

```
function overlapCapsuleTriangle(capsule: Capsule, triangle: Triangle,
                                 OUT hit: bool, OUT depth: float,
                                 OUT normal: Vector3, OUT point: Vector3):

STEP 5.2.1: Find closest points between capsule segment and triangle
    closestOnSeg, closestOnTri = closestPointsSegmentTriangle(
        capsule.p0, capsule.p1, triangle)

STEP 5.2.2: Compute separation vector
    separation = closestOnTri - closestOnSeg
    distance = magnitude(separation)

STEP 5.2.3: Check for overlap
    IF distance >= capsule.radius:
        # No overlap
        hit = FALSE
        RETURN

STEP 5.2.4: Compute penetration
    hit = TRUE
    depth = capsule.radius - distance

STEP 5.2.5: Compute normal (direction to push capsule out)
    IF distance > EPSILON:
        normal = separation / distance  # Points from capsule toward triangle
        normal = -normal                 # Flip to point OUT of triangle
    ELSE:
        # Degenerate case - use triangle normal
        normal = computeTriangleNormal(triangle)
        # Ensure normal points away from capsule center
        toCenter = capsule.center - closestOnTri
        IF dot(normal, toCenter) < 0:
            normal = -normal

STEP 5.2.6: Compute contact point
    point = closestOnTri
```

### 5.3 Closest Points: Segment to Triangle

```
function closestPointsSegmentTriangle(segA: Vector3, segB: Vector3,
                                       triangle: Triangle) -> (Vector3, Vector3):

STEP 5.3.1: Project segment endpoints to triangle plane
    triNormal = computeTriangleNormal(triangle)
    planeD = -dot(triNormal, triangle.a)
    
    distA = dot(triNormal, segA) + planeD
    distB = dot(triNormal, segB) + planeD

STEP 5.3.2: Check if segment crosses plane
    IF distA * distB < 0:
        # Segment crosses plane - find intersection point
        t = distA / (distA - distB)
        planePoint = segA + (segB - segA) * t
        
        IF pointInTriangle(planePoint, triangle):
            # Intersection is inside triangle
            closestOnTri = planePoint
            closestOnSeg = planePoint
            RETURN (closestOnSeg, closestOnTri)

STEP 5.3.3: Test segment against triangle interior
    # Project segment to plane and test
    projA = segA - triNormal * distA
    projB = segB - triNormal * distB
    
    # Find closest point on projected segment to triangle
    # (Complex - involves Voronoi regions)

STEP 5.3.4: Test segment against triangle edges
    edges = [(triangle.a, triangle.b), (triangle.b, triangle.c), (triangle.c, triangle.a)]
    
    bestDistSq = INFINITY
    bestSegPt = segA
    bestTriPt = triangle.a
    
    FOR EACH (edgeA, edgeB) IN edges:
        segPt, edgePt = closestPointsSegmentSegment(segA, segB, edgeA, edgeB)
        distSq = magnitudeSquared(edgePt - segPt)
        IF distSq < bestDistSq:
            bestDistSq = distSq
            bestSegPt = segPt
            bestTriPt = edgePt

STEP 5.3.5: Test segment against triangle vertices
    FOR EACH vertex IN [triangle.a, triangle.b, triangle.c]:
        segPt = closestPointOnSegment(segA, segB, vertex)
        distSq = magnitudeSquared(vertex - segPt)
        IF distSq < bestDistSq:
            bestDistSq = distSq
            bestSegPt = segPt
            bestTriPt = vertex

STEP 5.3.6: Test triangle interior against segment endpoints
    FOR EACH endpoint IN [segA, segB]:
        triPt = closestPointOnTriangle(endpoint, triangle)
        distSq = magnitudeSquared(triPt - endpoint)
        IF distSq < bestDistSq:
            bestDistSq = distSq
            bestSegPt = endpoint
            bestTriPt = triPt

STEP 5.3.7: Return best pair
    RETURN (bestSegPt, bestTriPt)
```

---

## 6. Capsule Region Classification

### 6.1 Purpose

Classify which part of the capsule made contact: top hemisphere, bottom hemisphere, or cylindrical side.

### 6.2 Algorithm

```
function classifyCapsuleRegion(capsule: Capsule, contactPoint: Vector3) -> CapsuleRegion:

STEP 6.2.1: Project contact point onto capsule axis
    segDir = capsule.p1 - capsule.p0
    segLen = magnitude(segDir)
    
    IF segLen < EPSILON:
        # Degenerate capsule (sphere)
        RETURN CapsuleRegion.SPHERE

STEP 6.2.2: Compute parameter t along segment
    segDirNorm = segDir / segLen
    toContact = contactPoint - capsule.p0
    t = dot(toContact, segDirNorm) / segLen  # Normalized to [0, 1]

STEP 6.2.3: Classify based on t value
    # Threshold for hemisphere vs cylinder (as fraction of radius relative to segment)
    threshold = capsule.radius / segLen
    
    IF t < -threshold:
        RETURN CapsuleRegion.BOTTOM_HEMISPHERE
    ELSE IF t > 1.0 + threshold:
        RETURN CapsuleRegion.TOP_HEMISPHERE
    ELSE IF t < threshold:
        RETURN CapsuleRegion.BOTTOM_HEMISPHERE  # Near bottom cap
    ELSE IF t > 1.0 - threshold:
        RETURN CapsuleRegion.TOP_HEMISPHERE     # Near top cap
    ELSE:
        RETURN CapsuleRegion.SIDE               # Cylindrical region
```

### 6.3 Alternative: Parameter-Based Classification

```
function classifyByParameter(t: float) -> CapsuleRegion:
    # t is the projection parameter onto capsule axis [0, 1]
    # Extended to negative and >1 for hemispheres
    
    bottomThreshold = 0.1   # 10% of segment length
    topThreshold = 0.9      # 90% of segment length
    
    IF t <= bottomThreshold:
        RETURN CapsuleRegion.BOTTOM_HEMISPHERE
    ELSE IF t >= topThreshold:
        RETURN CapsuleRegion.TOP_HEMISPHERE
    ELSE:
        RETURN CapsuleRegion.SIDE
```

### 6.4 Region Usage

| Region | Typical Meaning | Movement Impact |
|--------|-----------------|-----------------|
| `BOTTOM_HEMISPHERE` | Ground contact | Triggers grounded state |
| `TOP_HEMISPHERE` | Ceiling contact | Blocks upward movement |
| `SIDE` | Wall contact | Triggers slide behavior |

---

## 7. Normal Orientation Rules

### 7.1 Sweep Normal Orientation

For sweep tests, the normal must oppose the movement direction.

```
function orientNormalForSweep(surfaceNormal: Vector3, sweepDir: Vector3) -> Vector3:

STEP 7.1.1: Check if normal opposes movement
    dotProduct = dot(surfaceNormal, sweepDir)

STEP 7.1.2: Flip if necessary
    IF dotProduct > 0:
        # Normal points in movement direction - flip it
        RETURN -surfaceNormal
    ELSE:
        RETURN surfaceNormal
```

### 7.2 Overlap Normal Orientation

For overlap tests, the normal should point from geometry toward capsule (push-out direction).

```
function orientNormalForOverlap(surfaceNormal: Vector3, capsuleCenter: Vector3,
                                 closestPointOnGeom: Vector3) -> Vector3:

STEP 7.2.1: Compute direction from geometry to capsule
    toCenter = capsuleCenter - closestPointOnGeom
    
    IF magnitude(toCenter) < EPSILON:
        # Degenerate - use surface normal
        RETURN surfaceNormal

STEP 7.2.2: Check alignment
    IF dot(surfaceNormal, toCenter) < 0:
        # Normal points toward geometry - flip it
        RETURN -surfaceNormal
    ELSE:
        RETURN surfaceNormal
```

### 7.3 Ground Normal Special Case

For ground detection, ensure normal has positive Z (points upward).

```
function ensureUpwardNormal(normal: Vector3, upDirection: Vector3) -> Vector3:
    IF dot(normal, upDirection) < 0:
        RETURN -normal
    ELSE:
        RETURN normal
```

### 7.4 DO NOT Force Upward for Walls/Ceilings

```
# IMPORTANT: Unlike ground normals, wall and ceiling normals should
# NOT be forced upward. This would break slide behavior.

# WRONG:
wallNormal = ensureUpwardNormal(hitNormal, up)  # DON'T DO THIS

# CORRECT:
wallNormal = orientNormalForSweep(hitNormal, moveDir)
```

---

## 8. Contact Point Computation

### 8.1 Sweep Contact Point

```
function computeSweepContactPoint(capsule: Capsule, velocity: Vector3,
                                   toi: float, normal: Vector3) -> Vector3:

STEP 8.1.1: Move capsule to time of impact
    hitCapsule.p0 = capsule.p0 + velocity * toi
    hitCapsule.p1 = capsule.p1 + velocity * toi

STEP 8.1.2: Find point on capsule surface in normal direction
    # Project normal onto capsule axis to find closest segment point
    segDir = hitCapsule.p1 - hitCapsule.p0
    segLen = magnitude(segDir)
    
    IF segLen > EPSILON:
        segDirNorm = segDir / segLen
        # Component of -normal along segment
        t = dot(-normal, segDirNorm)
        t = clamp(t * segLen, 0, segLen)
        closestOnAxis = hitCapsule.p0 + segDirNorm * t
    ELSE:
        closestOnAxis = hitCapsule.p0

STEP 8.1.3: Offset by radius in normal direction
    contactPoint = closestOnAxis - normal * capsule.radius

STEP 8.1.4: Return contact point
    RETURN contactPoint
```

### 8.2 Overlap Contact Point

```
function computeOverlapContactPoint(closestOnGeom: Vector3) -> Vector3:
    # For overlaps, the contact point is simply the closest point on geometry
    RETURN closestOnGeom
```

---

## 9. Penetration Depth Calculation

### 9.1 From Closest Points

```
function computePenetrationDepth(capsule: Capsule, closestOnSeg: Vector3,
                                  closestOnGeom: Vector3) -> float:

STEP 9.1.1: Compute distance between closest points
    distance = magnitude(closestOnGeom - closestOnSeg)

STEP 9.1.2: Compute penetration (positive when overlapping)
    penetration = capsule.radius - distance
    
    IF penetration < 0:
        penetration = 0  # Not penetrating

STEP 9.1.3: Return penetration depth
    RETURN penetration
```

### 9.2 Scale Correction for Transformed Geometry

```
function scalePenetrationDepth(localDepth: float, instanceScale: float) -> float:
    # When geometry is scaled, penetration depth must be scaled too
    RETURN localDepth * instanceScale
```

---

## 10. Multi-Geometry Collision (CollideGeoms)

### 10.1 Purpose

Test capsule against all touched geometry and find the closest contact.

### 10.2 Algorithm

```
function collideGeoms(sweepTest: SweepTest, volume: SweptVolume,
                      geomStream: GeomStream, position: Vector3,
                      direction: Vector3, OUT contact: SweptContact) -> bool:

STEP 10.2.1: Initialize
    contact.distance = INFINITY
    foundHit = FALSE

STEP 10.2.2: Iterate all touched geometry
    FOR EACH geom IN geomStream:
        
        # Select appropriate sweep function based on geometry type
        SWITCH geom.type:
            CASE MESH:
                hit = sweepCapsuleMesh(sweepTest, volume, geom, position, direction, contact)
            CASE BOX:
                hit = sweepCapsuleBox(sweepTest, volume, geom, position, direction, contact)
            CASE SPHERE:
                hit = sweepCapsuleSphere(sweepTest, volume, geom, position, direction, contact)
            CASE USER_BOX:
                hit = sweepCapsuleUserBox(sweepTest, volume, geom, position, direction, contact)
            CASE USER_CAPSULE:
                hit = sweepCapsuleUserCapsule(sweepTest, volume, geom, position, direction, contact)
        
        IF hit:
            foundHit = TRUE
            # contact is updated in-place with closest hit

STEP 10.2.3: Return result
    RETURN foundHit
```

### 10.3 Mesh Collision (Multiple Triangles)

```
function sweepCapsuleMesh(sweepTest: SweepTest, volume: SweptVolume,
                          mesh: TouchedMesh, position: Vector3,
                          direction: Vector3, INOUT contact: SweptContact) -> bool:

STEP 10.3.1: Get cached triangles for this mesh
    triangles = sweepTest.worldTriangles[mesh.indexStart : mesh.indexStart + mesh.numTris]

STEP 10.3.2: Use cached triangle index hint (temporal coherence)
    cachedIndex = sweepTest.cachedTriIndex[sweepTest.cachedTriIndexIndex]
    IF cachedIndex >= mesh.numTris:
        cachedIndex = 0

STEP 10.3.3: Build capsule geometry
    capsule = buildCapsuleFromSweptVolume(volume, position, mesh.offset)

STEP 10.3.4: Sweep against all triangles
    foundHit = FALSE
    
    # Test cached triangle first (likely to hit again)
    IF testTriangle(cachedIndex):
        foundHit = TRUE
    
    # Test remaining triangles
    FOR i = 0 TO mesh.numTris - 1:
        IF i == cachedIndex:
            CONTINUE  # Already tested
        
        IF testTriangle(i):
            foundHit = TRUE

STEP 10.3.5: Return result
    RETURN foundHit

# Helper function
function testTriangle(triIndex: int) -> bool:
    triangle = triangles[triIndex]
    
    toi, normal, point = sweepCapsuleTriangle(capsule, direction * contact.distance, triangle)
    
    IF toi >= 0 AND toi * contact.distance < contact.distance:
        contact.distance = toi * contact.distance
        contact.worldNormal = normal
        contact.worldPos = point
        contact.internalIndex = mesh.indexStart + triIndex
        contact.triangleIndex = sweepTest.triangleIndices[mesh.indexStart + triIndex]
        contact.geom = mesh
        
        # Update cache hint
        sweepTest.cachedTriIndex[sweepTest.cachedTriIndexIndex] = triIndex
        
        RETURN TRUE
    
    RETURN FALSE
```

---

## 11. Temporal Coherence and Caching

### 11.1 Triangle Cache Structure

```
struct TriangleCache:
    worldTriangles: TriangleArray     # Transformed triangles in world space
    triangleIndices: IndexArray       # Original mesh triangle indices
    cachedTriIndex: int[3]            # Per-pass cache hints (UP/SIDE/DOWN)
    cachedTriIndexIndex: int          # Current pass index (0=UP, 1=SIDE, 2=DOWN)
    
    cacheBounds: AABB                 # Bounds of cached region
    nbCachedStatic: int               # Count of static geometry in cache
    nbCachedTriangles: int            # Total cached triangle count
    sceneTimestamp: uint32            # Scene modification counter
```

### 11.2 Cache Validity Check

```
function isCacheValid(newBounds: AABB, sceneTimestamp: uint32) -> bool:

STEP 11.2.1: Check scene modification
    IF sceneTimestamp != cache.sceneTimestamp:
        RETURN FALSE  # Scene changed - invalidate cache

STEP 11.2.2: Check spatial containment
    IF NOT newBounds.isInside(cache.cacheBounds):
        RETURN FALSE  # Moved outside cached region

STEP 11.2.3: Cache is still valid
    RETURN TRUE
```

### 11.3 Cache Update Strategy

```
function updateCache(position: Vector3, direction: Vector3, 
                     userObstacles: ObstacleList, filters: Filters):

STEP 11.3.1: Compute new temporal bounds
    temporalBounds = computeTemporalBox(position, direction)

STEP 11.3.2: Check if cache is valid
    IF isCacheValid(temporalBounds, currentSceneTimestamp):
        IF isFirstUpdateThisFrame:
            # Reuse static geometry, refresh dynamic only
            cache.worldTriangles.resize(cache.nbCachedStatic)
            cache.triangleIndices.resize(cache.nbCachedStatic)
            queryDynamicGeometry(temporalBounds, filters)
            queryUserObstacles(userObstacles, temporalBounds)
            isFirstUpdateThisFrame = FALSE
        RETURN  # Using cached data

STEP 11.3.3: Full cache rebuild required
    # Expand bounds for better cache hit rate
    cache.cacheBounds = scale(temporalBounds, VOLUME_GROWTH)
    
    # Bias expansion in movement direction
    IF magnitude(sideVector) > EPSILON:
        biasOffset = computeDirectionalBias(cache.cacheBounds, normalize(sideVector))
        cache.cacheBounds.translate(biasOffset)
    
    # Clear and rebuild
    cache.worldTriangles.clear()
    cache.triangleIndices.clear()
    cache.cachedTriIndex = [0, 0, 0]
    
    # Query static geometry
    queryStaticGeometry(cache.cacheBounds, filters)
    cache.nbCachedStatic = cache.worldTriangles.size()
    cache.nbCachedTriangles = cache.nbCachedStatic
    
    # Query dynamic geometry
    queryDynamicGeometry(cache.cacheBounds, filters)
    
    # Query user obstacles
    queryUserObstacles(userObstacles, cache.cacheBounds)
    
    # Update timestamp
    cache.sceneTimestamp = currentSceneTimestamp
    isFirstUpdateThisFrame = FALSE
```

---

## 12. Coordinate Transform Rules

### 12.1 World to Local Transform

```
function worldToLocal(worldPos: Vector3, instance: ModelInstance) -> Vector3:

STEP 12.1.1: Translate to instance origin
    translated = worldPos - instance.position

STEP 12.1.2: Apply inverse rotation
    rotated = instance.inverseRotation * translated

STEP 12.1.3: Apply inverse scale
    scaled = rotated * instance.inverseScale

STEP 12.1.4: Return local position
    RETURN scaled
```

### 12.2 Local to World Transform

```
function localToWorld(localPos: Vector3, instance: ModelInstance) -> Vector3:

STEP 12.2.1: Apply scale
    scaled = localPos * instance.scale

STEP 12.2.2: Apply rotation
    rotated = instance.rotation * scaled

STEP 12.2.3: Translate to world
    translated = rotated + instance.position

STEP 12.2.4: Return world position
    RETURN translated
```

### 12.3 Direction Transform (No Translation)

```
function worldDirToLocal(worldDir: Vector3, instance: ModelInstance) -> Vector3:
    # Directions don't translate, only rotate and scale
    rotated = instance.inverseRotation * worldDir
    RETURN rotated * instance.inverseScale

function localDirToWorld(localDir: Vector3, instance: ModelInstance) -> Vector3:
    scaled = localDir * instance.scale
    RETURN instance.rotation * scaled
```

### 12.4 Normal Transform (Inverse Transpose)

```
function localNormalToWorld(localNormal: Vector3, instance: ModelInstance) -> Vector3:
    # Normals use inverse-transpose of the model matrix
    # For uniform scale, this simplifies to just rotation
    IF instance.hasUniformScale:
        RETURN normalize(instance.rotation * localNormal)
    ELSE:
        # Full inverse-transpose required
        inverseTranspose = transpose(inverse(instance.modelMatrix))
        RETURN normalize(inverseTranspose * localNormal)
```

### 12.5 WoW-Specific: Internal to World

```
# WoW uses a different coordinate system internally

function internalToWorld(internalPos: Vector3) -> Vector3:
    # WoW internal: X=North, Y=West, Z=Up
    # World: X=East, Y=North, Z=Up
    RETURN Vector3(-internalPos.y, internalPos.x, internalPos.z)

function worldToInternal(worldPos: Vector3) -> Vector3:
    RETURN Vector3(worldPos.y, -worldPos.x, worldPos.z)

function internalDirToWorld(internalDir: Vector3) -> Vector3:
    RETURN Vector3(-internalDir.y, internalDir.x, internalDir.z)

function worldDirToInternal(worldDir: Vector3) -> Vector3:
    RETURN Vector3(worldDir.y, -worldDir.x, worldDir.z)
```

---

## Appendix A: Utility Functions

### A.1 Point in Triangle Test

```
function pointInTriangle(point: Vector3, triangle: Triangle) -> bool:
    # Barycentric coordinate test
    v0 = triangle.c - triangle.a
    v1 = triangle.b - triangle.a
    v2 = point - triangle.a
    
    dot00 = dot(v0, v0)
    dot01 = dot(v0, v1)
    dot02 = dot(v0, v2)
    dot11 = dot(v1, v1)
    dot12 = dot(v1, v2)
    
    invDenom = 1 / (dot00 * dot11 - dot01 * dot01)
    u = (dot11 * dot02 - dot01 * dot12) * invDenom
    v = (dot00 * dot12 - dot01 * dot02) * invDenom
    
    RETURN (u >= 0) AND (v >= 0) AND (u + v <= 1)
```

### A.2 Closest Point on Segment

```
function closestPointOnSegment(segA: Vector3, segB: Vector3, point: Vector3) -> Vector3:
    segDir = segB - segA
    segLenSq = dot(segDir, segDir)
    
    IF segLenSq < EPSILON:
        RETURN segA  # Degenerate segment
    
    t = dot(point - segA, segDir) / segLenSq
    t = clamp(t, 0, 1)
    
    RETURN segA + segDir * t
```

### A.3 Triangle Normal

```
function computeTriangleNormal(triangle: Triangle) -> Vector3:
    edge1 = triangle.b - triangle.a
    edge2 = triangle.c - triangle.a
    normal = cross(edge1, edge2)
    
    len = magnitude(normal)
    IF len < EPSILON:
        RETURN Vector3(0, 0, 1)  # Degenerate triangle - return up
    
    RETURN normal / len
```

### A.4 Signed Distance to Plane

```
function signedDistanceToPlane(point: Vector3, triangle: Triangle) -> float:
    normal = computeTriangleNormal(triangle)
    RETURN dot(point - triangle.a, normal)
```

---

## Appendix B: Common Pitfalls

### B.1 Forgetting Contact Offset

```
# WRONG: Moving exactly to contact point causes jitter
newPos = pos + dir * hitDistance

# CORRECT: Maintain skin width separation
newPos = pos + dir * (hitDistance - CONTACT_OFFSET)
```

### B.2 Sweep Distance Missing Contact Offset

```
# WRONG: May miss collisions within skin-width distance
sweepDist = remainingDistance
SweepCapsule(capsule, dir, sweepDist, hits)

# CORRECT: Include contact offset in sweep distance
sweepDist = remainingDistance + CONTACT_OFFSET
SweepCapsule(capsule, dir, sweepDist, hits)

# Then when processing:
safeAdvance = max(0.0f, hit.distance - CONTACT_OFFSET)
```

### B.3 Not Normalizing Direction After Modification

```
# WRONG: Using non-unit direction for sweep
modifiedDir = originalDir - someOffset
result = sweep(capsule, modifiedDir, distance)

# CORRECT: Always normalize direction
modifiedDir = normalize(originalDir - someOffset)
result = sweep(capsule, modifiedDir, distance)
```

### B.4 Incorrect Scale Application to Penetration

```
# WRONG: Using local-space depth directly
depth = localHit.depth

# CORRECT: Scale depth to world space
depth = localHit.depth * instance.scale
```

### B.5 Back-Face Culling Mistakes

```
# WRONG: Culling based on world up vector
IF triNormal.z < 0: skip  # This breaks walls!

# CORRECT: Cull based on movement direction
IF dot(triNormal, moveDir) >= 0: skip
```

### B.6 Insufficient Iteration Count

```
# WRONG: Too few iterations causes stuck characters
MAX_ITERATIONS = 4

# CORRECT: PhysX uses 10 iterations
MAX_ITERATIONS = 10
```

### B.7 Missing Sensor Sweep After Side Pass

```
# WRONG: Only doing sensor sweep before UP pass
function ExecuteUpPass():
    if shouldDoSensorSweep:
        PerformClimbingSensorSweep()  # Wrong location!

# CORRECT: Sensor sweep happens AFTER side pass
function ExecuteSidePass():
    # Normal side movement first
    result = CollideAndSlide(sideVector)
    
    # THEN sensor sweep if movement was small
    if sideMagnitude < capsuleRadius:
        PerformConstrainedClimbingSensorSweep()
```

### B.8 Ignoring Contact Point Height in Slope Validation

```
# WRONG: Only checking normal angle
walkable = (normal.z >= 0.707)

# CORRECT: Also check contact height in constrained mode
walkable = (normal.z >= 0.707)
if constrainedClimbing:
    contactHeight = dot(contactPoint, upDir)
    if contactHeight > originalBottomZ + stepOffset:
        walkable = false  # Too high to climb
```

### B.9 Fixed DOWN Pass Iterations

```
# WRONG: Always using 1 iteration
ExecuteDownPass(state, decomposed, maxIter=1)

# CORRECT: Variable based on walk experiment mode
if walkExperiment and mode == PREVENT_CLIMBING_AND_FORCE_SLIDING:
    maxIterDown = 10  # Allow sliding
else:
    maxIterDown = 1   # Just find ground
ExecuteDownPass(state, decomposed, maxIterDown)
```

---

*Document Version: 1.0*
*Based on: PhysX 4.x Geometry Queries*
*Target: World of Warcraft Headless Physics Service*
