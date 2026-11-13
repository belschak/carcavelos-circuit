"""Automated geometry QA along the lap: ray casts against the built world (no rendering).

Run:  Blender -b -P qa_geometry.py -- [blend] [out_json]
Defaults: out/CampusWorld.blend, out/geometry_report.json

For every route point (2 m apart):
  clearance   rays from the centreline to both sides at 0.35, 1.0 and 2.0 m above the road. Anything closer
              than the road half-width (4 m) is an 'intrusion'; 4-5 m is 'tight' (expected for tunnel walls
              and hall glazing, reported separately).
  overhead    rays straight up at 7 lateral offsets: anything lower than 3.0 m above the road.
  surface     rays down at 15 lateral offsets: 'hole' (nothing below), 'covered' (terrain, a path or a
              building above the road surface), 'height' (road more than 12 cm off its profile height),
              'coplanar' (a second surface less than 3 cm under the road: z-fighting risk).
  edge        just outside both road edges: 'edge_drop' (ground more than 0.35 m below the road edge, the road
              floats) and 'edge_rise' (ground more than 0.35 m above it right at the edge, a step/wall).
  chase_cam   the chase camera (6.5 m behind, 2.3 m up) cannot see the kart: a wall is in between.
Consecutive route points with the same problem are merged into one record (s_from..s_to, worst value).
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BLEND = argv[0] if len(argv) > 0 and argv[0] else os.path.join(HERE, "out", "CampusWorld.blend")
OUT = argv[1] if len(argv) > 1 and argv[1] else os.path.join(HERE, "out", "geometry_report.json")

bpy.ops.wm.open_mainfile(filepath=BLEND)
sys.path.insert(0, HERE)
import env_common as E  # noqa: E402

SKIP_COLLECTIONS = {"Track_RouteGuide", "Hero", "Render_Lights", "Tmp_Cutters"}
ROAD = {"Track_Road", "Track_JumpKickerDrive_ColliderOnly", "Track_PlazaDrive_ColliderOnly", "Track_PlazaLow"}
ALLOWED_ON_ROAD = {"Track_Kerbs", "Track_EdgeLines"}   # painted on top of the road on purpose
EXPECTED_TIGHT = ("Tunnel_Shell", "Interior_Passages", "Tunnel_")
TERRAIN = ("World_Ground", "World_GroundCorridor")
UP, DOWN = Vector((0, 0, 1)), Vector((0, 0, -1))


def build_bvh():
    deps = bpy.context.evaluated_depsgraph_get()
    verts, polys, owner, names = [], [], [], []
    for obj in bpy.data.objects:
        if obj.type != "MESH" or (obj.hide_render and not obj.get("collider_only")):   # V10i: colliders count
            continue
        if any(c.name in SKIP_COLLECTIONS for c in obj.users_collection):
            continue
        ev = obj.evaluated_get(deps)
        me = ev.to_mesh()
        mw = obj.matrix_world
        base = len(verts)
        verts.extend(mw @ v.co for v in me.vertices)
        k = len(names)
        names.append(obj.name)
        for p in me.polygons:
            polys.append([base + i for i in p.vertices])
            owner.append(k)
        ev.to_mesh_clear()
    print(f"BVH: {len(names)} objects, {len(polys)} faces")
    return BVHTree.FromPolygons(verts, polys, epsilon=0.0), owner, names


BVH, OWNER, NAMES = build_bvh()


def cast(origin, direction, dist):
    loc, _nrm, idx, d = BVH.ray_cast(origin, direction, dist)
    if loc is None:
        return None
    return loc, NAMES[OWNER[idx]], d


def hits_down(x, y, z_top, depth=8.0, max_hits=3):
    out, origin, left = [], Vector((x, y, z_top)), depth
    for _ in range(max_hits):
        h = cast(origin, DOWN, left)
        if not h:
            break
        out.append((h[0].z, h[1]))
        origin = h[0] + Vector((0, 0, -0.0005))
        left -= h[2] + 0.0005
        if left <= 0:
            break
    return out


def frame(pts, i):
    n = len(pts)
    a, b = pts[(i - 1) % n], pts[(i + 1) % n]
    dx, dy = b["x"] - a["x"], b["y"] - a["y"]
    d = math.hypot(dx, dy) or 1.0
    return (dx / d, dy / d), (-dy / d, dx / d)


def main():
    pts = E.route_points()
    raw = []                                    # (check, i, side, object, value, detail)
    for i, p in enumerate(pts):
        t, nrm = frame(pts, i)
        x, y, z = p["x"], p["y"], p["track_z"]
        HALF = E.half_width(p)                         # narrower in the alley
        drop_zone = abs(pts[(i + 1) % len(pts)]["track_z"] - z) > 1.0 or abs(pts[i - 1]["track_z"] - z) > 1.0
        # clearance
        for side in (1, -1):
            d2 = Vector((nrm[0] * side, nrm[1] * side, 0))
            for h in (0.35, 1.0, 2.0):
                hit = cast(Vector((x, y, z + h)), d2, HALF + 1.0)
                if not hit or hit[1] in ROAD or hit[1] in ALLOWED_ON_ROAD:
                    continue
                d = hit[2]
                if hit[1].startswith(TERRAIN) and d >= HALF:
                    continue
                if d < HALF:
                    raw.append(("intrusion", i, side, hit[1], round(d, 2), f"h={h}"))
                elif not hit[1].startswith(EXPECTED_TIGHT):
                    raw.append(("tight", i, side, hit[1], round(d, 2), f"h={h}"))
        # overhead
        for o in (-3.5, -2.0, -1.0, 0.0, 1.0, 2.0, 3.5):
            if abs(o) > HALF - 0.3:
                continue
            hit = cast(Vector((x + nrm[0] * o, y + nrm[1] * o, z + 0.2)), UP, 2.8)
            if hit and hit[1] not in ROAD:
                raw.append(("overhead", i, 0, hit[1], round(hit[0].z - z, 2), f"offset={o}"))
        # surface
        if not drop_zone:
            for k in range(-7, 8):
                o = k * 0.5
                if abs(o) > HALF - 0.4:                  # stay on the road where it is narrower
                    continue
                hx, hy = x + nrm[0] * o, y + nrm[1] * o
                hs = hits_down(hx, hy, z + 1.5)
                if not hs:
                    raw.append(("hole", i, 0, "-", None, f"offset={o}"))
                    continue
                (z1, n1) = hs[0]
                if n1 in ALLOWED_ON_ROAD:
                    hs = hs[1:] or hs
                    (z1, n1) = hs[0]
                if n1 not in ROAD:
                    raw.append(("covered", i, 0, n1, round(z1 - z, 2), f"offset={o}"))
                elif abs(z1 - z) > 0.12:
                    raw.append(("height", i, 0, n1, round(z1 - z, 2), f"offset={o}"))
                if len(hs) > 1 and hs[0][0] - hs[1][0] < 0.03 and "ColliderOnly" not in hs[0][1] + hs[1][1]:   # not drawn
                    raw.append(("coplanar", i, 0, f"{hs[0][1]} / {hs[1][1]}", round(hs[0][0] - hs[1][0], 3), f"offset={o}"))
            # edges
            for side in (1, -1):
                o = side * (HALF + 0.3)
                hs = hits_down(x + nrm[0] * o, y + nrm[1] * o, z + 3.0, depth=12.0, max_hits=1)
                if not hs:
                    raw.append(("edge_drop", i, side, "-", None, "nothing below"))
                    continue
                dz = hs[0][0] - z
                if dz < -0.35 and not p.get("building"):
                    raw.append(("edge_drop", i, side, hs[0][1], round(dz, 2), ""))
                elif dz > 0.35 and not hs[0][1].startswith(EXPECTED_TIGHT):
                    raw.append(("edge_rise", i, side, hs[0][1], round(dz, 2), ""))
        # chase camera
        back = pts[(i - 3) % len(pts)]                      # about 6 m back along the route
        cam = Vector((back["x"], back["y"], max(back["track_z"], z) + 2.3))
        kart = Vector((x, y, z + 0.8))
        v = kart - cam
        hit = cast(cam, v.normalized(), v.length - 0.3)
        if hit and hit[1] not in ROAD:
            raw.append(("chase_cam", i, 0, hit[1], round(hit[2], 2), "wall between camera and kart"))

    # merge consecutive points with the same check/side/object
    runs = []
    raw.sort(key=lambda r: (r[0], r[2], r[3], r[1]))
    for r in raw:
        check, i, side, obj, val, detail = r
        last = runs[-1] if runs else None
        if last and last["check"] == check and last["side"] == side and last["object"] == obj and i - last["_i1"] <= 2:
            last["_i1"] = i
            last["count"] += 1
            if val is not None and (last["worst"] is None or
                                    (check in ("intrusion", "tight", "overhead") and val < last["worst"]) or
                                    (check not in ("intrusion", "tight", "overhead") and abs(val) > abs(last["worst"]))):
                last["worst"] = val
            continue
        runs.append({"check": check, "side": {1: "left", -1: "right", 0: ""}[side], "object": obj, "worst": val,
                     "detail": detail, "count": 1, "_i0": i, "_i1": i})
    for r in runs:
        r["s_from"], r["s_to"] = pts[r.pop("_i0")]["s"], pts[r.pop("_i1")]["s"]
        r["zone"] = pts[min(range(len(pts)), key=lambda k: abs(pts[k]["s"] - r["s_from"]))]["zone"]
    order = ["hole", "intrusion", "covered", "overhead", "chase_cam", "coplanar", "height", "edge_drop", "edge_rise", "tight"]
    runs.sort(key=lambda r: (order.index(r["check"]), r["s_from"]))
    summary = {c: sum(1 for r in runs if r["check"] == c) for c in order}
    report = {"blend": BLEND, "points_checked": len(pts), "road_half_width_m": "per point (route.json width / 2)",
              "legend": {"side": "left/right of the driving direction", "worst": "metres (distance, height above road, or gap)"},
              "summary_runs": summary, "issues": runs}
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    json.dump(report, open(OUT, "w"), indent=1, ensure_ascii=False)
    print("summary:", summary)
    print(f"report: {OUT}")


main()
