"""Puts Noah's lanes through the Nova hall into the route data (task V12).

track.json (exported by export_unity_bundle.py) still has the old line through the Edificio Principal. Noah's hall
(nova_hall.py, help PR #45) has its own two lanes inside: west = main entrance -> long room -> terrace door,
east = entrance-room door -> round the staircase -> east facade. This script replaces every route point inside the
building's footprint with the matching lane, 2 m apart like the rest, so the checkpoints, the progress line (T8),
the bots, the minimap, the boost pads and the white fence all follow the lanes the karts really drive.

- Outside the footprint nothing changes; Role 2's HallDoorApproach still lines the road up with the doors.
- The lanes are read from nova_hall.py itself (constants + rounded_line, without Blender), the footprint from
  NovaHallInsert.cs, so there is one source for each.
- Safe to run again: it only ever replaces what is inside the footprint. Rerun it after every export_unity_bundle.py.

    python3 tools/world_gen/hall_route.py            # from the repo root, rewrites track.json in place
    python3 tools/world_gen/hall_route.py --check    # only prints what it would do
"""
import ast
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
TRACK = os.path.join(REPO, "Assets/_Project/Visuals/CampusWorld/Data/track.json")
INSERT_CS = os.path.join(REPO, "Assets/_Project/Visuals/Editor/NovaHallInsert.cs")
NOVA_HALL = os.path.join(HERE, "nova_hall.py")
STEP = 2.0              # metres between route points, as in export_unity_bundle.py
CHECKPOINTS = 12        # as in export_unity_bundle.py
HALL_ZONE = "atrium"    # WhiteGuardrailBuilder lets the hall's own fence take over in this zone


def hall_source():
    """NW/NE corner, the lanes and rounded_line() from nova_hall.py, without running it (it needs Blender)."""
    tree = ast.parse(open(NOVA_HALL).read())
    consts, funcs = {}, {}
    for node in tree.body:
        if isinstance(node, ast.Assign) and isinstance(node.targets[0], ast.Tuple):
            names = [t.id for t in node.targets[0].elts]
            if "NW_CORNER" in names:
                for name, value in zip(names, ast.literal_eval(node.value)):
                    consts[name] = value
        elif isinstance(node, ast.Assign) and getattr(node.targets[0], "id", None) == "ROADS":
            consts["ROADS"] = ast.literal_eval(node.value)
        elif isinstance(node, ast.FunctionDef) and node.name == "rounded_line":
            scope = {"math": math}
            exec(compile(ast.Module(body=[node], type_ignores=[]), NOVA_HALL, "exec"), scope)
            funcs["rounded_line"] = scope["rounded_line"]
    return consts["NW_CORNER"], consts["NE_CORNER"], consts["ROADS"], funcs["rounded_line"]


def to_world(nw, ne):
    """Hall coordinates (u = metres south from the north facade, o = metres east along it) -> world x/z.
    Same frame as nova_hall.building_frame(); checked against the door centres in HallDoors.json."""
    d = math.hypot(ne[0] - nw[0], ne[1] - nw[1])
    tx, tz = (ne[0] - nw[0]) / d, (ne[1] - nw[1]) / d
    return lambda u, o: (nw[0] + u * tz + o * tx, nw[1] - u * tx + o * tz)


def footprint():
    text = open(INSERT_CS).read()
    block = re.search(r"Footprint\s*=\s*\{(.*?)\};", text, re.S).group(1)
    nums = [float(v) for v in re.findall(r"-?\d+\.\d+", block)]
    return list(zip(nums[0::2], nums[1::2]))


def inside(poly, x, z):
    hit = False
    j = len(poly) - 1
    for i in range(len(poly)):
        (xi, zi), (xj, zj) = poly[i], poly[j]
        if (zi > z) != (zj > z) and x < (xj - xi) * (z - zi) / (zj - zi) + xi:
            hit = not hit
        j = i
    return hit


def resample(line, step):
    """Points every `step` metres along a polyline (first point kept, last one only if it is a full step on)."""
    out = [line[0]]
    carry = 0.0
    for a, b in zip(line, line[1:]):
        seg = math.hypot(b[0] - a[0], b[1] - a[1])
        pos = step - carry
        while pos <= seg:
            t = pos / seg
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
            pos += step
        carry = (carry + seg) % step
    return out


def runs(flags):
    """(first, last) of every run of True, in order (the route is closed, but no run wraps here)."""
    result, start = [], None
    for i, f in enumerate(flags + [False]):
        if f and start is None:
            start = i
        elif not f and start is not None:
            result.append((start, i - 1))
            start = None
    return result


def splice(track):
    nw, ne, roads, rounded_line = hall_source()
    world = to_world(nw, ne)
    poly = footprint()
    pts = track["points"]
    old_runs = runs([inside(poly, p["x"], p["z"]) for p in pts])

    lanes = []
    for road in roads:
        line = [world(u, o) for u, o in rounded_line(road["points"], road["radii"])]
        dense = resample(line, STEP)
        flags = [inside(poly, x, z) for x, z in dense]
        first, last = flags.index(True), len(flags) - 1 - flags[::-1].index(True)
        lanes.append({"name": road["name"], "width": road["width"], "points": dense[first:last + 1]})

    # Each old run inside the building gets the lane whose ends lie closest to the run's ends.
    def gap(run, lane):
        a, b = pts[run[0]], pts[run[1]]
        return (math.hypot(a["x"] - lane["points"][0][0], a["z"] - lane["points"][0][1]) +
                math.hypot(b["x"] - lane["points"][-1][0], b["z"] - lane["points"][-1][1]))

    plan = []
    for run in old_runs:
        lane = min(lanes, key=lambda ln: gap(run, ln))
        plan.append((run, lane, gap(run, lane)))

    new_pts, index_map, report = [], {}, []
    i = 0
    for run, lane, g in plan + [((len(pts), len(pts)), None, 0)]:
        while i < run[0]:
            index_map[i] = len(new_pts)
            new_pts.append(dict(pts[i]))
            i += 1
        if lane is None:
            break
        y = pts[run[0]]["y"]                                   # one even hall floor at the height of the door
        for k in range(run[0], run[1] + 1):
            index_map[k] = len(new_pts)                        # old points inside map onto the lane's start
        for x, z in lane["points"]:
            new_pts.append({"x": round(x, 3), "y": y, "z": round(z, 3), "width": lane["width"], "s": 0.0,
                            "zone": HALL_ZONE, "tunnel": False})
        report.append(f"{lane['name']} lane: old points {run[0]}-{run[1]} ({run[1] - run[0] + 1}) -> "
                      f"{len(lane['points'])} lane points, ends {g:.1f} m from the old ones")
        i = run[1] + 1

    n = len(new_pts)
    for k, p in enumerate(new_pts):
        p["s"] = round(k * STEP, 1)

    # Indices as export_unity_bundle.py makes them: start stays, checkpoints evenly spread, never just after the jump.
    start = index_map[track["start_index"]]
    jump = index_map[track["jump_index"]]
    step = n / CHECKPOINTS
    cps = []
    for c in range(CHECKPOINTS):
        k = int(round(start + c * step)) % n
        while (k - jump) % n < 10 or (jump - k) % n < 3:
            k = (k + 1) % n
        cps.append(k)
    out = dict(track)
    out.update({"length_m": n * STEP, "jump_index": jump, "start_index": start, "spawn_index": (start - 4) % n,
                "checkpoints": cps, "points": new_pts})
    report.append(f"route {len(pts)} -> {n} points, lap {track['length_m']:.0f} -> {n * STEP:.0f} m, "
                  f"jump index {track['jump_index']} -> {jump}")
    return out, report


def main():
    track = json.load(open(TRACK))
    out, report = splice(track)
    print("\n".join(report))
    if "--check" in sys.argv:
        return
    with open(TRACK, "w") as fh:
        json.dump(out, fh, indent=0)
    print("written:", os.path.relpath(TRACK, REPO))


if __name__ == "__main__":
    main()
