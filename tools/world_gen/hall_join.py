"""Joins the plaza line to Noah's east hall lane at the atrium door D4 (V14 + Nova hall, 2026-11-11).

hall_route.py (task V12) replaces the route points inside the Edificio Principal with Noah's hall lanes and leaves
everything outside alone. V14's line over the upper plaza (library passage, neck beside the courtyard) still ends
where the OLD drive-through began, at (66.3, 111.6): east of the hall door and behind its facade line. So the raw
track.json ran past the door, jumped 5.8 m back to the lane's first point and turned 133 degrees there. In the build
Role 2's HallDoorApproach lines the road up with the door, but everything that reads track.json itself (bots,
TrackProgress, minimap, the fence, the self-test's "road under every point") got the zigzag.

This script re-lays only the last metres of the plaza line in front of that one door:
  - the points behind the door's facade line are dropped,
  - from BACK_POINTS points before them a smooth curve (cubic Hermite, tangents = the line's own direction there and
    the lane's first direction) runs into the lane's first point, cut into 2 m steps like the rest.
The plaza there is one paved deck (no road strip), so no geometry has to follow. Heights, width and zone are taken
from the old points. Nothing else moves; the points after the join are renumbered (the script prints by how many).

V20c (Luc's request, 2026-11-13): the same at the exit door D5. The route outside D5 (Noah's line from route.json)
starts 2 m south of the lane's last point and swings back north (a zigzag of 2.1 m sideways in track.json, there
since V12). From the lane's last point a Hermite curve (tangents: the lane's direction and the route's own direction
EXIT_AHEAD points on) runs into that route point; the points between are replaced, 2 m apart. In Unity
HallDoorApproach still lines the first metres up with the door, so the road there does not change.
The doors D1 and D3 lie on Noah's own line and are his to decide; they are left alone.

V21c (Luc's request p4 "straight up to the entrance", item from the integrator, 2026-11-13): the same kind of fix
at the main entrance D1 (join_west). The start straight aimed 1.3 m south of the door's middle: its last outside
point lay 0.97 m south of the door axis (3.78 m from the south jamb's line), then the line jumped 1.15 m back north
onto the hall lane. Package B's Nova Kart walls run straight from route point 2 to the two jambs, so the 8 m road
reached through the south wall there. From point WEST_KEEP on, the outside points now lie on ONE straight line that
ends in the door's middle; they keep their distance along the door axis (so the 2 m step, the point count, the
heights and every index stay). The line turns 2.8 degrees at point WEST_KEEP; points 0..WEST_KEEP do not move.
Why not all of them on the door axis itself: the walls are straight from point 2 to the jambs, so the only centre
line that keeps an 8 m road inside both walls is the straight one from point 2 to the door's middle. A line that
swings onto the axis earlier (say from point 6) would put the road's south edge up to 3.5 m outside the south wall.

Run order after every bundle export:  hall_route.py -> hall_join.py -> pin_checkpoints.py
Safe to run again: once the line ends on the lane it finds nothing behind the facade and changes nothing.

    python3 tools/world_gen/hall_join.py            # rewrites track.json in place
    python3 tools/world_gen/hall_join.py --check    # only prints what it would do
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
TRACK = os.path.join(REPO, "Assets/_Project/Visuals/CampusWorld/Data/track.json")
DOORS = os.path.join(REPO, "Assets/_Project/Visuals/CampusWorld/Data/HallDoors.json")
DOOR = "D4_entrance_room"
HALL_ZONE = "atrium"        # zone hall_route.py gives the lane points
STEP = 2.0                  # metres between route points
BACK_POINTS = 8             # the curve starts this many points before the first dropped one (about 16 m)
TANGENT = 1.3               # Hermite tangent length as a share of the chord: 1.3 gave the widest bend (10 m radius)
BEHIND_M = -1.0             # points closer than this to the facade line (or behind it) are dropped
EXIT_DOOR = "D5_out_north_east"
EXIT_AHEAD = 8              # V20c: the exit curve joins the route this many points after the lane's last point (16 m)
EXIT_TANGENT = 1.3          # Hermite tangent length as a share of the chord: 1.3 gave the widest bend (6.1 m radius)


def hermite(p0, t0, p1, t1, n=400):
    out = []
    for k in range(n + 1):
        s = k / n
        h00, h10, h01, h11 = 2 * s ** 3 - 3 * s ** 2 + 1, s ** 3 - 2 * s ** 2 + s, -2 * s ** 3 + 3 * s ** 2, s ** 3 - s ** 2
        out.append((h00 * p0[0] + h10 * t0[0] + h01 * p1[0] + h11 * t1[0],
                    h00 * p0[1] + h10 * t0[1] + h01 * p1[1] + h11 * t1[1]))
    return out


def unit(a, b):
    d = math.hypot(b[0] - a[0], b[1] - a[1])
    return ((b[0] - a[0]) / d, (b[1] - a[1]) / d)


def join(track):
    door = next(d for d in json.load(open(DOORS))["doors"] if d["name"] == DOOR)
    pts = track["points"]
    n = len(pts)
    xz = lambda i: (pts[i]["x"], pts[i]["z"])
    # the lane's first point: the hall point nearest the door whose predecessor is not a hall point
    firsts = [i for i in range(n) if pts[i]["zone"] == HALL_ZONE and pts[i - 1]["zone"] != HALL_ZONE]
    lane = min(firsts, key=lambda i: math.hypot(pts[i]["x"] - door["x"], pts[i]["z"] - door["z"]))
    depth = lambda i: (pts[i]["x"] - door["x"]) * door["nx"] + (pts[i]["z"] - door["z"]) * door["nz"]
    drop = lane
    while depth(drop - 1) > BEHIND_M:                        # outside points behind (or on) the facade line
        drop -= 1
    turn = abs((math.atan2(*unit(xz(lane - 1), xz(lane))) - math.atan2(*unit(xz(lane - 2), xz(lane - 1))) + math.pi)
               % (2 * math.pi) - math.pi)
    if drop == lane and math.dist(xz(lane - 1), xz(lane)) < 1.5 * STEP and turn < math.radians(25):
        return None, [f"{DOOR}: the line already ends on the lane (point {lane}), nothing to do"]
    a = drop - BACK_POINTS                                   # last point that stays where it is
    p0, p1 = xz(a), xz(lane)
    d0, d1 = unit(xz(a - 1), xz(a + 1)), unit(xz(lane), xz(lane + 1))
    chord = math.dist(p0, p1)
    line = hermite(p0, (d0[0] * chord * TANGENT, d0[1] * chord * TANGENT), p1, (d1[0] * chord * TANGENT, d1[1] * chord * TANGENT))
    length = sum(math.dist(u, v) for u, v in zip(line, line[1:]))
    count = int(round(length / STEP)) - 1                    # new points strictly between a and the lane's first point
    step = length / (count + 1)
    new_xy, walked, want = [], 0.0, step
    for u, v in zip(line, line[1:]):
        seg = math.dist(u, v)
        while walked + seg >= want and len(new_xy) < count:
            f = (want - walked) / seg
            new_xy.append((u[0] + (v[0] - u[0]) * f, u[1] + (v[1] - u[1]) * f))
            want += step
        walked += seg
    radius = min(_radius(line[k - 20], line[k], line[k + 20]) for k in range(20, len(line) - 20))
    template = pts[a]
    new = [dict(template, x=round(x, 3), z=round(z, 3)) for x, z in new_xy]
    old_count = lane - a - 1
    out_pts = [dict(p) for p in pts[:a + 1]] + new + [dict(p) for p in pts[lane:]]
    shift = count - old_count
    m = len(out_pts)
    for k, p in enumerate(out_pts):
        p["s"] = round(k * STEP, 1)
    remap = lambda i: i if i <= a else max(a + 1, i + shift) if i >= lane else min(a + 1 + count - 1, i)
    out = dict(track)
    start = remap(track["start_index"])
    out.update({"points": out_pts, "length_m": m * STEP, "start_index": start, "jump_index": remap(track["jump_index"]),
                "spawn_index": (start - 4) % m, "checkpoints": [remap(c) for c in track["checkpoints"]]})
    report = [f"{DOOR}: points {a + 1}-{lane - 1} ({old_count}, {lane - drop} of them behind the facade line) -> {count} points "
              f"on a {length:.1f} m curve into the lane's first point ({p1[0]:.2f}, {p1[1]:.2f}), step {step:.3f} m, "
              f"tightest bend {radius:.1f} m radius",
              f"route {n} -> {m} points, lap {track['length_m']:.0f} -> {m * STEP:.0f} m; points after the join renumbered by {shift:+d}"]
    return out, report


def _radius(a, b, c):
    h1 = math.atan2(b[1] - a[1], b[0] - a[0])
    h2 = math.atan2(c[1] - b[1], c[0] - b[0])
    d = abs((h2 - h1 + math.pi) % (2 * math.pi) - math.pi)
    return math.dist(a, b) / d if d > 1e-6 else 1e9


def resample_curve(line, count):
    """count points strictly between the ends of a dense polyline, evenly spaced along it."""
    length = sum(math.dist(u, v) for u, v in zip(line, line[1:]))
    step = length / (count + 1)
    new_xy, walked, want = [], 0.0, step
    for u, v in zip(line, line[1:]):
        seg = math.dist(u, v)
        while walked + seg >= want and len(new_xy) < count:
            f = (want - walked) / seg
            new_xy.append((u[0] + (v[0] - u[0]) * f, u[1] + (v[1] - u[1]) * f))
            want += step
        walked += seg
    return new_xy, length, step


def join_exit(track):
    """V20c: from the east lane's last point (the hall point before the exit door) a smooth curve into the route
    EXIT_AHEAD points on; the points between are replaced (see the module text)."""
    door = next(d for d in json.load(open(DOORS))["doors"] if d["name"] == EXIT_DOOR)
    pts = track["points"]
    n = len(pts)
    xz = lambda i: (pts[i]["x"], pts[i]["z"])
    lasts = [i for i in range(n) if pts[i]["zone"] == HALL_ZONE and pts[(i + 1) % n]["zone"] != HALL_ZONE]
    a = min(lasts, key=lambda i: math.hypot(pts[i]["x"] - door["x"], pts[i]["z"] - door["z"]))
    b = a + EXIT_AHEAD
    d0, d1 = unit(xz(a - 1), xz(a)), unit(xz(b - 1), xz(b + 1))
    # already smooth? (every step between a and b turns less than 25 degrees and is about 2 m long)
    heads = [math.atan2(*unit(xz(i), xz(i + 1))) for i in range(a - 1, b)]
    turns = [abs((h1 - h0 + math.pi) % (2 * math.pi) - math.pi) for h0, h1 in zip(heads, heads[1:])]
    if max(turns) < math.radians(25) and all(abs(math.dist(xz(i), xz(i + 1)) - STEP) < 0.3 for i in range(a, b)):
        return None, [f"{EXIT_DOOR}: the route already leaves the lane smoothly (point {a}), nothing to do"]
    p0, p1 = xz(a), xz(b)
    chord = math.dist(p0, p1)
    line = hermite(p0, (d0[0] * chord * EXIT_TANGENT, d0[1] * chord * EXIT_TANGENT), p1,
                   (d1[0] * chord * EXIT_TANGENT, d1[1] * chord * EXIT_TANGENT))
    length = sum(math.dist(u, v) for u, v in zip(line, line[1:]))
    count = int(round(length / STEP)) - 1
    new_xy, length, step = resample_curve(line, count)
    radius = min(_radius(line[k - 20], line[k], line[k + 20]) for k in range(20, len(line) - 20))
    new = [dict(pts[b], x=round(x, 3), z=round(z, 3)) for x, z in new_xy]
    old_count = b - a - 1
    out_pts = [dict(p) for p in pts[:a + 1]] + new + [dict(p) for p in pts[b:]]
    shift = count - old_count
    m = len(out_pts)
    for k, p in enumerate(out_pts):
        p["s"] = round(k * STEP, 1)
    remap = lambda i: i if i <= a else i + shift if i >= b else min(a + count, i)
    out = dict(track)
    start = remap(track["start_index"])
    out.update({"points": out_pts, "length_m": m * STEP, "start_index": start, "jump_index": remap(track["jump_index"]),
                "spawn_index": (start - 4) % m, "checkpoints": [remap(c) for c in track["checkpoints"]]})
    report = [f"{EXIT_DOOR}: points {a + 1}-{b - 1} ({old_count}, the largest turn between them {math.degrees(max(turns)):.0f} deg) "
              f"-> {count} points on a {length:.1f} m curve from the lane's last point ({p0[0]:.2f}, {p0[1]:.2f}) into "
              f"route point {b} ({p1[0]:.2f}, {p1[1]:.2f}), step {step:.3f} m, tightest bend {radius:.1f} m radius",
              f"route {n} -> {m} points, lap {track['length_m']:.0f} -> {m * STEP:.0f} m; points after the exit renumbered by {shift:+d}"]
    return out, report


WEST_DOOR = "D1_main_entrance"
WEST_KEEP = 2               # V21c: route points up to this one stay (start line = 0; package B's walls start at 2)


def west_clearance(track):
    """Per outside point before D1: (index, metres before the facade, metres south of the door axis, distance to the
    north jamb's line, distance to the south jamb's line)."""
    door = next(d for d in json.load(open(DOORS))["doors"] if d["name"] == WEST_DOOR)
    pts = track["points"]
    firsts = [i for i in range(len(pts)) if pts[i]["zone"] == HALL_ZONE and pts[i - 1]["zone"] != HALL_ZONE]
    lane = min(firsts, key=lambda i: math.hypot(pts[i]["x"] - door["x"], pts[i]["z"] - door["z"]))
    rows = []
    for i in range(WEST_KEEP, lane):
        rx, rz = pts[i]["x"] - door["x"], pts[i]["z"] - door["z"]
        depth = rx * door["nx"] + rz * door["nz"]
        south = rx * door["nz"] - rz * door["nx"]                 # along the facade, + = right of the way in (south)
        rows.append((i, -depth, south, door["width"] / 2 + south, door["width"] / 2 - south))
    return lane, rows


def join_west(track):
    """V21c: the outside points before the main entrance D1 on one straight line from point WEST_KEEP into the
    door's middle (see the module text). Only x and z of points WEST_KEEP + 1 .. lane - 1 change."""
    door = next(d for d in json.load(open(DOORS))["doors"] if d["name"] == WEST_DOOR)
    lane, before = west_clearance(track)
    _, depth0, south0, _, _ = before[0]                           # point WEST_KEEP: stays
    out = dict(track)
    out["points"] = [dict(p) for p in track["points"]]
    moved = 0.0
    for i, depth, south, _, _ in before[1:]:
        want = south0 * depth / depth0                            # on the line from point WEST_KEEP to the door's middle
        shift = want - south
        p = out["points"][i]
        p["x"] = round(p["x"] + door["nz"] * shift, 3)
        p["z"] = round(p["z"] - door["nx"] * shift, 3)
        moved = max(moved, abs(shift))
    _, after = west_clearance(out)
    report = [f"{WEST_DOOR}: points {WEST_KEEP + 1}-{lane - 1} on the straight line from point {WEST_KEEP} into the door's "
              f"middle, largest sideways move {moved:.2f} m; the line turns "
              f"{math.degrees(math.atan2(before[1][2] - south0, depth0 - before[1][1]) - math.atan2(-south0, depth0)):.1f} deg at point {WEST_KEEP}",
              "  point  before the door  south of axis (was)  to north jamb line  to south jamb line (was)"]
    for (i, depth, south, _, to_s), (_, _, south2, to_n2, to_s2) in zip(before, after):
        report.append(f"  {i:5d}  {depth:12.2f} m  {south2:+8.2f} ({south:+.2f})  {to_n2:14.2f} m  {to_s2:13.2f} m ({to_s:.2f})")
    if moved < 0.005:
        return None, [f"{WEST_DOOR}: the line already runs straight into the door's middle, nothing to do"] + report[1:]
    return out, report


def main():
    track = json.load(open(TRACK))
    out, report = join(track)
    print("\n".join(report))
    if out is not None:
        track = out
    out2, report2 = join_exit(track)          # V20c
    print("\n".join(report2))
    if out2 is not None:
        out = track = out2
    out3, report3 = join_west(track)          # V21c
    print("\n".join(report3))
    if out3 is not None:
        out = out3
    if out is None or "--check" in sys.argv:
        return
    with open(TRACK, "w") as fh:
        json.dump(out, fh, indent=0)
    print("written:", os.path.relpath(TRACK, REPO))


if __name__ == "__main__":
    main()
