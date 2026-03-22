#!/usr/bin/env python3
"""Analyze FG/BG transform parity recordings."""
import csv, math, sys, glob, os

RECORDINGS_DIR = "C:/Users/lrhod/AppData/Local/WWoW/PhysicsRecordings"

def load(f):
    rows = []
    with open(f) as fh:
        for r in csv.DictReader(fh):
            rows.append({
                'ms': int(r['ElapsedMs']),
                'x': float(r['PosX']), 'y': float(r['PosY']), 'z': float(r['PosZ']),
                'flags': r['MoveFlags'], 'speed': float(r['RunSpeed'])
            })
    return rows

def analyze(timestamp):
    fg_file = f"{RECORDINGS_DIR}/transform_TESTBOT1_{timestamp}.csv"
    bg_file = f"{RECORDINGS_DIR}/transform_TESTBOT2_{timestamp}.csv"

    if not os.path.exists(fg_file) or not os.path.exists(bg_file):
        print(f"Files not found for timestamp {timestamp}")
        return

    fg = load(fg_file)
    bg = load(bg_file)

    print(f"FG: {len(fg)} frames, {fg[-1]['ms']-fg[0]['ms']}ms  |  BG: {len(bg)} frames, {bg[-1]['ms']-bg[0]['ms']}ms")
    print(f"FG: ({fg[0]['x']:.1f},{fg[0]['y']:.1f},{fg[0]['z']:.1f}) -> ({fg[-1]['x']:.1f},{fg[-1]['y']:.1f},{fg[-1]['z']:.1f})")
    print(f"BG: ({bg[0]['x']:.1f},{bg[0]['y']:.1f},{bg[0]['z']:.1f}) -> ({bg[-1]['x']:.1f},{bg[-1]['y']:.1f},{bg[-1]['z']:.1f})")

    # Time-aligned divergence
    max_xy = 0; max_z = 0; max_xy_ms = 0; max_z_ms = 0
    for f in fg:
        t = f['ms']
        c = min(bg, key=lambda b: abs(b['ms'] - t))
        dx = f['x'] - c['x']; dy = f['y'] - c['y']
        xy = math.sqrt(dx*dx + dy*dy)
        dz = abs(f['z'] - c['z'])
        if xy > max_xy:
            max_xy = xy; max_xy_ms = t
        if dz > max_z:
            max_z = dz; max_z_ms = t

    fdx = fg[-1]['x'] - bg[-1]['x']
    fdy = fg[-1]['y'] - bg[-1]['y']
    final_xy = math.sqrt(fdx*fdx + fdy*fdy)
    final_z = abs(fg[-1]['z'] - bg[-1]['z'])

    fg_spd = [f['speed'] for f in fg if int(f['flags'], 16) & 1]
    bg_spd = [f['speed'] for f in bg if int(f['flags'], 16) & 1]
    avg_fg = sum(fg_spd)/len(fg_spd) if fg_spd else 0
    avg_bg = sum(bg_spd)/len(bg_spd) if bg_spd else 0

    print(f"\nSpeed: FG={avg_fg:.2f}  BG={avg_bg:.2f}")
    print(f"Max XY: {max_xy:.2f}y @{max_xy_ms}ms  |  Max Z: {max_z:.2f}y @{max_z_ms}ms")
    print(f"Final XY: {final_xy:.2f}y  |  Final Z: {final_z:.2f}y")
    print(f"FG flags: {sorted(set(f['flags'] for f in fg))}")
    print(f"BG flags: {sorted(set(f['flags'] for f in bg))}")

    # Flag transitions
    print("\nFG flag transitions:")
    prev = ''
    for f in fg:
        if f['flags'] != prev:
            print(f"  {f['ms']:6d}ms  {f['flags']}  z={f['z']:.2f}")
            prev = f['flags']

    print("\nBG flag transitions:")
    prev = ''
    for f in bg:
        if f['flags'] != prev:
            print(f"  {f['ms']:6d}ms  {f['flags']}  z={f['z']:.2f}")
            prev = f['flags']

    # Z trajectory comparison (sampled)
    print("\nZ trajectory (time-aligned):")
    sample_times = range(fg[0]['ms'], fg[-1]['ms'], max(1, (fg[-1]['ms']-fg[0]['ms'])//12))
    for t in sample_times:
        fg_f = min(fg, key=lambda f: abs(f['ms'] - t))
        bg_f = min(bg, key=lambda f: abs(f['ms'] - t))
        dz = fg_f['z'] - bg_f['z']
        dx = fg_f['x'] - bg_f['x']; dy = fg_f['y'] - bg_f['y']
        dxy = math.sqrt(dx*dx + dy*dy)
        print(f"  {t:6d}ms  FG z={fg_f['z']:6.2f}  BG z={bg_f['z']:6.2f}  dZ={dz:+.2f}  dXY={dxy:.2f}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        analyze(sys.argv[1])
    else:
        # Find latest
        files = sorted(glob.glob(f"{RECORDINGS_DIR}/transform_TESTBOT1_*.csv"))
        if files:
            ts = os.path.basename(files[-1]).replace("transform_TESTBOT1_", "").replace(".csv", "")
            print(f"=== Latest recording: {ts} ===\n")
            analyze(ts)
