#!/usr/bin/env py
"""
navmesh_view.py -- headless inspector for MmapGen --debug navmesh polymesh OBJ.

Lets an agent SEE + ANALYZE a baked navmesh without RecastDemo:
  * flood-fills poly connectivity (shared-edge) -> disjoint walkable "islands"
  * reports whether two query points (e.g. lip + deck) are in the SAME island,
    and if not, the nearest-approach 3D distance + Z gap between their islands
  * renders top-down (WoW X-Y, colored by height Z) and elevation (WoW X-Z)
    PNGs, plus a component-colored top-down that makes the break obvious.

OBJ axis convention (MmapGen): `v recastX recastY recastZ` == WoW (Y, Z, X).
So WoW X = obj col3, WoW Y = obj col1, WoW Z = obj col2.

Usage:
  py navmesh_view.py <navmesh.obj> --out <dir> --crop minX,minY,maxX,maxY
     [--query X,Y,Z ...] [--zclip minZ,maxZ]
"""
import sys, argparse, math
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection

def parse_obj(path):
    verts = []          # WoW (X,Y,Z)
    faces = []          # list of 0-based vert-index tuples
    with open(path) as fh:
        for ln in fh:
            if ln.startswith("v "):
                a, b, c = (float(x) for x in ln.split()[1:4])
                verts.append((c, a, b))          # WoW X=c, Y=a, Z=b
            elif ln.startswith("f "):
                idx = [int(t.split("/")[0]) - 1 for t in ln.split()[1:]]
                faces.append(tuple(idx))
    return np.asarray(verts, dtype=float), faces

def connected_components(verts, faces, quant=0.05):
    """Flood-fill faces that share an edge (>=2 vertices at the same 3D position)."""
    key = {}                      # quantized pos -> canonical id
    canon = np.empty(len(verts), dtype=np.int64)
    for i, p in enumerate(verts):
        k = (round(p[0] / quant), round(p[1] / quant), round(p[2] / quant))
        canon[i] = key.setdefault(k, len(key))
    edge_faces = {}               # (canonA,canonB) -> [face indices]
    for fi, f in enumerate(faces):
        cf = [canon[v] for v in f]
        n = len(cf)
        for e in range(n):
            a, b = cf[e], cf[(e + 1) % n]
            if a == b:
                continue
            ek = (a, b) if a < b else (b, a)
            edge_faces.setdefault(ek, []).append(fi)
    adj = [[] for _ in faces]
    for fl in edge_faces.values():
        for i in range(len(fl)):
            for j in range(i + 1, len(fl)):
                adj[fl[i]].append(fl[j]); adj[fl[j]].append(fl[i])
    comp = [-1] * len(faces)
    cid = 0
    for s in range(len(faces)):
        if comp[s] != -1:
            continue
        stack = [s]; comp[s] = cid
        while stack:
            u = stack.pop()
            for w in adj[u]:
                if comp[w] == -1:
                    comp[w] = cid; stack.append(w)
        cid += 1
    return np.asarray(comp), cid

def face_centroids(verts, faces):
    return np.asarray([verts[list(f)].mean(axis=0) for f in faces])

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("obj")
    ap.add_argument("--out", required=True)
    ap.add_argument("--crop", default=None, help="minX,minY,maxX,maxY (WoW)")
    ap.add_argument("--zclip", default=None, help="minZ,maxZ (WoW)")
    ap.add_argument("--query", action="append", default=[], help="X,Y,Z WoW; repeatable")
    args = ap.parse_args()

    verts, faces = parse_obj(args.obj)
    comp, ncomp = connected_components(verts, faces)
    cent = face_centroids(verts, faces)
    print(f"[navmesh_view] {args.obj}: verts={len(verts)} faces={len(faces)} components={ncomp}")

    # component stats
    sizes = np.bincount(comp)
    order = np.argsort(sizes)[::-1]
    print("  top components (id: faces | WoW X[..] Y[..] Z[..]):")
    for c in order[:10]:
        m = comp == c
        cz = cent[m]
        print(f"    c{c:>4}: {sizes[c]:>5} | X[{cz[:,0].min():7.1f},{cz[:,0].max():7.1f}] "
              f"Y[{cz[:,1].min():8.1f},{cz[:,1].max():8.1f}] Z[{cz[:,2].min():6.2f},{cz[:,2].max():6.2f}]")

    # query points -> nearest face's component
    qpts = [tuple(float(x) for x in q.split(",")) for q in args.query]
    qcomp = []
    for q in qpts:
        d = np.linalg.norm(cent - np.asarray(q), axis=1)
        fi = int(d.argmin())
        qcomp.append(comp[fi])
        print(f"  query {q}: nearest face d={d[fi]:.2f} -> component c{comp[fi]} "
              f"(faceZ={cent[fi][2]:.2f})")
    # pairwise: same component? else nearest approach + Z gap
    for i in range(len(qpts)):
        for j in range(i + 1, len(qpts)):
            ci, cj = qcomp[i], qcomp[j]
            if ci == cj:
                print(f"  CONNECTED: query{i} and query{j} share component c{ci}")
            else:
                A = cent[comp == ci]; B = cent[comp == cj]
                # nearest approach (subsample if huge)
                if len(A) * len(B) > 4_000_000:
                    A = A[np.random.choice(len(A), 2000, replace=False)] if len(A) > 2000 else A
                    B = B[np.random.choice(len(B), 2000, replace=False)] if len(B) > 2000 else B
                dmat = np.linalg.norm(A[:, None, :] - B[None, :, :], axis=2)
                mi = np.unravel_index(dmat.argmin(), dmat.shape)
                a, b = A[mi[0]], B[mi[1]]
                print(f"  BROKEN: query{i}(c{ci}) and query{j}(c{cj}) NOT connected. "
                      f"nearest approach {dmat.min():.2f}y between A{tuple(round(x,1) for x in a)} "
                      f"and B{tuple(round(x,1) for x in b)}; dZ={abs(a[2]-b[2]):.2f}y dXY={math.hypot(a[0]-b[0],a[1]-b[1]):.2f}y")

    # crop filter for rendering
    crop = [float(x) for x in args.crop.split(",")] if args.crop else None
    zclip = [float(x) for x in args.zclip.split(",")] if args.zclip else None
    def keep(fi):
        c = cent[fi]
        if crop and not (crop[0] <= c[0] <= crop[2] and crop[1] <= c[1] <= crop[3]):
            return False
        if zclip and not (zclip[0] <= c[2] <= zclip[1]):
            return False
        return True
    fkeep = [fi for fi in range(len(faces)) if keep(fi)]
    print(f"  rendering {len(fkeep)} faces in crop")

    def polys(proj):  # proj: indices into WoW (x,y,z) for (horiz, vert)
        return [verts[list(faces[fi])][:, proj] for fi in fkeep]

    # --- render 1: top-down, colored by WoW Z (height) ---
    zc = np.asarray([cent[fi][2] for fi in fkeep])
    fig, ax = plt.subplots(figsize=(12, 10))
    pc = PolyCollection(polys((0, 1)), array=zc, cmap="viridis",
                        edgecolors="black", linewidths=0.15)
    ax.add_collection(pc); ax.autoscale()
    ax.set_aspect("equal"); ax.set_xlabel("WoW X"); ax.set_ylabel("WoW Y")
    ax.set_title("navmesh top-down (color = WoW Z height)")
    fig.colorbar(pc, ax=ax, label="WoW Z")
    for q in qpts: ax.plot(q[0], q[1], "r*", ms=16, mec="white")
    fig.savefig(f"{args.out}/nav_topdown_z.png", dpi=110, bbox_inches="tight"); plt.close(fig)

    # --- render 2: top-down, colored by connected component (the break) ---
    fig, ax = plt.subplots(figsize=(12, 10))
    cc = np.asarray([comp[fi] for fi in fkeep])
    uniq = list(dict.fromkeys(cc.tolist()))
    cmap = plt.get_cmap("tab20")
    colmap = {c: cmap(i % 20) for i, c in enumerate(uniq)}
    cols = [colmap[c] for c in cc]
    pc = PolyCollection(polys((0, 1)), facecolors=cols, edgecolors="black", linewidths=0.15)
    ax.add_collection(pc); ax.autoscale()
    ax.set_aspect("equal"); ax.set_xlabel("WoW X"); ax.set_ylabel("WoW Y")
    ax.set_title(f"navmesh top-down (color = connected component; {len(uniq)} in crop)")
    for q in qpts: ax.plot(q[0], q[1], "r*", ms=16, mec="white")
    fig.savefig(f"{args.out}/nav_topdown_components.png", dpi=110, bbox_inches="tight"); plt.close(fig)

    # --- render 3: elevation WoW X (horiz) vs WoW Z (vert), colored by component ---
    fig, ax = plt.subplots(figsize=(14, 7))
    pc = PolyCollection(polys((0, 2)), facecolors=cols, edgecolors="black", linewidths=0.15, alpha=0.8)
    ax.add_collection(pc); ax.autoscale()
    ax.set_xlabel("WoW X"); ax.set_ylabel("WoW Z (height)")
    ax.set_title("navmesh elevation X-Z (color = connected component) -- shows vertical lip->deck gap")
    for q in qpts: ax.plot(q[0], q[2], "r*", ms=16, mec="white")
    fig.savefig(f"{args.out}/nav_elevation_xz.png", dpi=110, bbox_inches="tight"); plt.close(fig)

    print(f"[navmesh_view] wrote nav_topdown_z.png, nav_topdown_components.png, nav_elevation_xz.png to {args.out}")

if __name__ == "__main__":
    main()
