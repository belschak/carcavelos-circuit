"""The promenade shortcut in the world (V19, 2026-11-12): paint on the ground, dressing off the lane, ground checked.

The lane itself is the ground that is already there (parking aisle and paving, with colliders in Unity), so this adds
no driving surface. It adds what makes the lane readable and drivable:
  Track_PromenadeShortcutPaint_NoCollide   white edge lines and three Boost Orange chevrons at the mouth (paint only,
                                           collection Track_Surface; Unity gives "NoCollide" objects no collider)
  dress()                                  removes every tree, car, bollard or sign closer than CLEAR to the lane
  ground check                             the build stops if the ground steps more than MAX_STEP between two points

Geometry and numbers: promenade_shortcut_plan.py (one source, tested without Blender).
"""
import json
import math
import os

import bmesh
import bpy

import env_common as E
import promenade_shortcut_plan as P

LINE_W = 0.14               # m, edge line width
PAINT_LIFT = 0.04           # m over the ground (as env_trackside.LINE_LIFT)
MAX_STEP = 0.6              # m: more height change between two lane points than this = a wall for the kart
CHEVRONS = 3                # at the split mouth, pointing along the lane
CHEVRON_GAP = 2.5           # m between chevrons
CHEVRON_ARM, CHEVRON_HALF, CHEVRON_THICK = 1.0, 1.4, 0.45


def _frame(line, k):
    """Unit direction along the lane at point k and the unit vector to its left."""
    a, b = line[max(k - 1, 0)], line[min(k + 1, len(line) - 1)]
    d = math.hypot(b[0] - a[0], b[1] - a[1])
    fx, fy = (b[0] - a[0]) / d, (b[1] - a[1]) / d
    return (fx, fy), (-fy, fx)


def _quad(bm, pts, mi):
    face = bm.faces.new([bm.verts.new((x, y, E.ground_z(x, y) + PAINT_LIFT)) for x, y in pts])
    face.normal_update()
    if face.normal.z < 0:
        face.normal_flip()
    face.material_index = mi


def ground_profile(line):
    """(lowest ground, highest ground, largest step between neighbours, where that step is)."""
    zs = [E.ground_z(x, y) for x, y in line]
    with open(os.path.join(E.OUT, "promenade_shortcut_profile.json"), "w") as f:   # for moving VIA points by hand
        json.dump([{"x": round(x, 2), "y": round(y, 2), "ground": round(z, 3)} for (x, y), z in zip(line, zs)], f, indent=0)
    steps = [(abs(b - a), line[k + 1]) for k, (a, b) in enumerate(zip(zs, zs[1:]))]
    worst = max(steps)
    return min(zs), max(zs), worst[0], worst[1]


def build():
    line = P.centreline()
    zmin, zmax, dz, at = ground_profile(line)
    if dz > MAX_STEP:
        raise RuntimeError(f"promenade shortcut: ground steps {dz:.2f} m at ({at[0]:.0f}, {at[1]:.0f})")
    bm = bmesh.new()
    half = P.WIDTH / 2
    for k, (a, b) in enumerate(zip(line, line[1:])):        # edge lines, inside the lane edge
        _, la = _frame(line, k)
        _, lb = _frame(line, k + 1)
        for side in (-1, 1):
            o0, o1 = side * (half - 0.25), side * (half - 0.25 - LINE_W)
            _quad(bm, [(a[0] + la[0] * o0, a[1] + la[1] * o0), (b[0] + lb[0] * o0, b[1] + lb[1] * o0),
                       (b[0] + lb[0] * o1, b[1] + lb[1] * o1), (a[0] + la[0] * o1, a[1] + la[1] * o1)], 0)
    (fx, fy), (lx, ly) = _frame(line, 2)                    # chevrons a few metres into the lane
    for c in range(CHEVRONS):
        tip = (line[2][0] + fx * (2.0 + c * CHEVRON_GAP), line[2][1] + fy * (2.0 + c * CHEVRON_GAP))
        for side in (-1, 1):
            arm = (tip[0] - fx * CHEVRON_ARM + lx * side * CHEVRON_HALF, tip[1] - fy * CHEVRON_ARM + ly * side * CHEVRON_HALF)
            _quad(bm, [tip, arm, (arm[0] - fx * CHEVRON_THICK, arm[1] - fy * CHEVRON_THICK),
                       (tip[0] - fx * CHEVRON_THICK, tip[1] - fy * CHEVRON_THICK)], 1)
    mesh = bpy.data.meshes.new("Track_PromenadeShortcutPaint")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("edge_line_white", "#EDEDE8", roughness=0.6))
    mesh.materials.append(E.material("T_kerb", "#FF8A1F", roughness=0.7))
    E.new_object("Track_PromenadeShortcutPaint_NoCollide", mesh, E.collection("Track_Surface"))
    print(f"  promenade shortcut: {P.length(line):.0f} m, ground {zmin:.2f}..{zmax:.2f} m, max step {dz:.2f} m")


def dress(veg, props, protos):
    """Removes every plant and prop closer than P.CLEAR to the lane; rewrites the two placement files like the
    other dress() steps. `protos` is unused (same call shape as env_capricciosa.dress)."""
    line = P.centreline()
    removed = [p for p in list(veg) + list(props) if P.distance(p["x"], p["y"], line) < P.CLEAR]
    ids = {id(p) for p in removed}
    veg2 = [p for p in veg if id(p) not in ids]
    props2 = [p for p in props if id(p) not in ids]
    for fname, items in (("placements_vegetation.json", veg2), ("placements_props.json", props2)):
        path = os.path.join(E.OUT, fname)
        data = json.load(open(path))
        data["placements"] = items
        counts = {}
        for it in items:
            counts[it["proto"]] = counts.get(it["proto"], 0) + 1
        data["counts"] = counts
        json.dump(data, open(path, "w"), indent=1)
    print("  promenade shortcut: dressing: removed "
          + (", ".join(sorted(f"{p['proto']}@{p['x']:.0f},{p['y']:.0f}" for p in removed)) or "nothing"))
    return veg2, props2
