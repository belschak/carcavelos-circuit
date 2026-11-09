"""Street props for the Carcavelos campus world: low-poly prototypes and the rules for where they stand.

Run inside Blender 5.2 (headless works), for example from another script:

    import sys
    sys.path.insert(0, "<this folder>")   # the folder with env_common.py
    import env_props as P
    protos = P.build_prototypes()               # {name: object} in collection Proto_Props
    placements = P.place()                      # list of dicts, also written to out/placements_props.json
    P.instance_for_preview(placements, protos)  # linked duplicates in World_Props, only for renders

Prototypes: one mesh object each, origin at the base centre (the railing: at its start), z up, metres, flat
shaded, triangulated, 2-3 flat colours. Parts that touch the ground reach 0.05-0.15 m below z = 0.
Local directions: lamp heads and arms point along +x (placed facing the road); bench, lounge chair: the
person sitting looks along -y; railing segment: 2.0 m along +x from its origin.

Placement entries (the game instances from out/placements_props.json, key "placements"):
    {"proto": "Prop_LampRoad", "x": .., "y": .., "z": .., "rot_z": .., "scale": ..}
World metres in the frame of route.json (x east, y north, z up); rot_z in radians, counter-clockwise seen from
above, about the prototype origin. Railing segments also carry "rot_y": the pitch (radians, about the
segment's local y axis, applied before rot_z) that makes them follow a slope; ignoring it leaves them level.
z is the terrain height at call time (E.ground_z: call place() after env_terrain.apply() and
env_corridor.build_data(), as build_world.py does); deck props, ramp and seawall railings use the route's track_z.
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import env_common as E  # noqa: E402

PROTO_COLLECTION = "Proto_Props"
WORLD_COLLECTION = "World_Props"
PLACEMENTS_FILE = os.path.join(E.OUT, "placements_props.json")
SEED = 20261108

TRI_BUDGET = {"Prop_LampCampus": 120, "Prop_LampRoad": 150, "Prop_Bench": 60, "Prop_LoungeChair": 150,
              "Prop_Bollard": 40, "Prop_RailingSegment": 100, "Prop_Bin": 60, "Prop_Parasol": 100}
FOOTPRINT = {"Prop_LampCampus": 0.3, "Prop_LampRoad": 0.3, "Prop_Bench": 1.0, "Prop_LoungeChair": 0.6,
             "Prop_Bollard": 0.15, "Prop_RailingSegment": 0.0, "Prop_Bin": 0.3, "Prop_Parasol": 1.6}

# Placement rules (metres along the lap = route s, offsets from the centreline; + = left of the driving direction)
NO_PROP_ZONES = {"tunnel", "atrium"}                  # indoors / underground: nothing stands there
CAMPUS_LAMP_ZONES = {"upper plaza", "lower plaza", "lawn and paths"}
ROAD_LAMP_ZONES = {"coastal road", "promenade"}
CAMPUS_NEAR = 40.0                  # 'lawn and paths' counts as campus within this distance of a campus building
CAMPUS_LAMP_STEP, CAMPUS_LAMP_OFFSET = 30.0, 6.5
ROAD_LAMP_STEP, ROAD_LAMP_OFFSETS = 35.0, (6.5, 7.5, 8.5, 9.5)
RAILING_OFFSET, RAILING_LENGTH = 4.6, 2.0
JUMP_GAP = 12.0                     # no deck railing within 6 m of the jump take-off
RAMP_S = (86.0, 150.0)              # north tunnel ramp, railings on both sides
SEAWALL_GROUND_Z = 4.0              # promenade railing where the route's ground_z is below this
BENCH_STEP, BENCH_OFFSET = 25.0, 7.0
BOLLARD_OFFSETS = (6.5, 8.0, 9.5, 11.0, 12.5)   # a row of 5 on each side, outside the 8 m road
CHAIR_BAND = (7.0, 12.0)            # lounge chairs on the deck, this far from the centreline
DECK_EDGE_GAP = 0.6                 # ... and at least this far inside the deck (roof) edge
BEACH_BAR = (-75.0, -148.0)         # parasols near here, each >= PARASOL_CLEAR from the route
PARASOL_CLEAR = 8.0
BUILDING_GAP, ROAD_GAP = 2.0, 1.0
TREE_GAP = 1.2                      # props keep their footprint + this from the real (OSM) tree positions
ROAD_TYPES = {"primary", "primary_link", "secondary", "tertiary", "residential", "unclassified", "service",
              "living_street"}
REPORT = {}                         # filled by place(): what could not be placed and why


# =====================================================================================================
# Geometry helpers
# =====================================================================================================
class MeshBuilder:
    """Collects vertices and faces (each face with a material slot) and turns them into one clean mesh."""

    def __init__(self):
        self.verts, self.faces, self.mats = [], [], []

    def v(self, co):
        self.verts.append(Vector((co[0], co[1], co[2])))
        return len(self.verts) - 1

    def f(self, idx, mat):
        self.faces.append(tuple(idx))
        self.mats.append(mat)

    def build(self, name, materials, col):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        for m in materials:
            mesh.materials.append(m)
        mesh.polygons.foreach_set("material_index", self.mats)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
        bm.to_mesh(mesh)
        bm.free()
        for poly in mesh.polygons:
            poly.use_smooth = False
        mesh.validate()
        mesh.update()
        return E.new_object(name, mesh, col)


def box(mb, x0, x1, y0, y1, z0, z1, mat, skip=("bottom",)):
    """Axis-aligned box. skip: faces to leave out (hidden ones): bottom, top, -x, +x, -y, +y."""
    c = [mb.v((x, y, z)) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)]
    faces = {"bottom": (c[0], c[2], c[3], c[1]), "top": (c[4], c[5], c[7], c[6]),
             "-y": (c[0], c[1], c[5], c[4]), "+y": (c[2], c[6], c[7], c[3]),
             "-x": (c[0], c[4], c[6], c[2]), "+x": (c[1], c[3], c[7], c[5])}
    for key, face in faces.items():
        if key not in skip:
            mb.f(face, mat)


def extrude(mb, profile, axis, start, end, mats, caps=True):
    """Prism: a 2-D profile extruded along x or y from `start` to `end`. axis 'x': profile points are (y, z);
    axis 'y': (x, z). mats: one material, or one per side face (profile edge k -> k+1) plus 'cap'."""
    def p3(u, v, w):
        return (w, u, v) if axis == "x" else (u, w, v)
    a = [mb.v(p3(u, v, start)) for u, v in profile]
    b = [mb.v(p3(u, v, end)) for u, v in profile]
    n = len(profile)
    for k in range(n):
        m = mats[k] if isinstance(mats, (list, tuple)) else mats
        mb.f((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]), m)
    if caps:
        cap = mats[-1] if isinstance(mats, (list, tuple)) else mats
        mb.f(a[::-1], cap)
        mb.f(b, cap)


def lathe(mb, profile, sides, mats, phase=0.0):
    """Solid of revolution from a (radius, z) profile, bottom to top. A radius of 0 at either end closes it with
    a cone/fan to a single point. mats: one material, or one per band."""
    rings = []
    for r, z in profile:
        if r <= 1e-6:
            rings.append([mb.v((0.0, 0.0, z))])
        else:
            rings.append([mb.v((math.cos(phase + 2 * math.pi * j / sides) * r,
                                math.sin(phase + 2 * math.pi * j / sides) * r, z)) for j in range(sides)])
    for k, (r0, r1) in enumerate(zip(rings, rings[1:])):
        m = mats[k] if isinstance(mats, (list, tuple)) else mats
        for j in range(sides):
            j1 = (j + 1) % sides
            if len(r0) == 1:
                mb.f((r0[0], r1[j1], r1[j]), m)
            elif len(r1) == 1:
                mb.f((r0[j], r0[j1], r1[0]), m)
            else:
                mb.f((r0[j], r0[j1], r1[j1], r1[j]), m)
    return rings


def tube(mb, path, radii, sides, mat):
    """Tube along a polyline (parallel-transport frames), ends open (hidden inside other parts)."""
    pts = [Vector(p) for p in path]
    tangents = [(pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized() for k in range(len(pts))]
    normal = Vector((0, 1, 0)).cross(tangents[0]).normalized()
    if normal.length < 0.5:
        normal = Vector((1, 0, 0))
    rings = []
    for p, t, r in zip(pts, tangents, radii):
        normal = (normal - t * normal.dot(t)).normalized()
        binormal = t.cross(normal)
        rings.append([mb.v(p + (normal * math.cos(2 * math.pi * j / sides) + binormal * math.sin(2 * math.pi * j / sides)) * r)
                      for j in range(sides)])
    for r0, r1 in zip(rings, rings[1:]):
        for j in range(sides):
            mb.f((r0[j], r0[(j + 1) % sides], r1[(j + 1) % sides], r1[j]), mat)
    return rings


# =====================================================================================================
# Prototypes
# =====================================================================================================
def lamp_campus(name, metal, base, light, col):
    """Slim modern campus lamp, 5 m: square concrete foot, octagonal pole, flat rectangular head that reaches
    1 m out along +x, glowing underside."""
    mb = MeshBuilder()
    METAL, BASE, LIGHT = 0, 1, 2
    box(mb, -0.17, 0.17, -0.17, 0.17, -0.15, 0.32, BASE)
    lathe(mb, [(0.085, 0.25), (0.075, 4.8)], 8, METAL, phase=math.pi / 8)
    profile = [(-0.16, 4.76), (0.16, 4.76), (0.16, 4.86), (0.12, 4.93), (-0.12, 4.93), (-0.16, 4.86)]
    extrude(mb, profile, "x", -0.22, 1.05, [LIGHT, METAL, METAL, METAL, METAL, METAL, METAL])
    return mb.build(name, [metal, base, light], col)


def lamp_road(name, metal, base, light, col):
    """Tall road / promenade lamp, 9.4 m: round foot, tapered pole that bends into a swan-neck arm reaching
    1.9 m along +x, and a flat luminaire with a glowing underside."""
    mb = MeshBuilder()
    METAL, BASE, LIGHT = 0, 1, 2
    lathe(mb, [(0.21, -0.15), (0.2, 0.8), (0.13, 1.0), (0.0, 1.02)], 8, [BASE, BASE, BASE])
    path = [(0, 0, 0.9), (0, 0, 7.8), (0.05, 0, 8.55), (0.38, 0, 9.12), (1.0, 0, 9.36), (1.75, 0, 9.27)]
    tube(mb, path, [0.135, 0.1, 0.09, 0.082, 0.075, 0.068], 6, METAL)
    profile = [(-0.17, 9.18), (0.17, 9.18), (0.17, 9.26), (0.1, 9.36), (-0.1, 9.36), (-0.17, 9.26)]
    extrude(mb, profile, "x", 1.5, 2.3, [LIGHT, METAL, METAL, METAL, METAL, METAL, METAL])
    return mb.build(name, [metal, base, light], col)


def bench(name, concrete, wood, col):
    """Concrete block bench (1.9 m) on a recessed plinth, with a timber seat board; sits along x."""
    mb = MeshBuilder()
    CONCRETE, WOOD = 0, 1
    box(mb, -0.82, 0.82, -0.17, 0.17, -0.12, 0.1, CONCRETE, skip=("bottom", "top"))
    profile = [(-0.26, 0.1), (0.26, 0.1), (0.26, 0.37), (0.22, 0.41), (-0.22, 0.41), (-0.26, 0.37)]
    extrude(mb, profile, "x", -0.95, 0.95, CONCRETE)
    box(mb, -0.88, 0.88, -0.2, 0.2, 0.41, 0.47, WOOD)
    return mb.build(name, [concrete, wood], col)


def lounge_chair(name, frame, cushion, col):
    """Grey angular outdoor lounge chair (our own design): two thick side panels that rise into a raked back
    support, a deep seat cushion and a leaning back cushion. 0.84 m wide; the sitter looks along -y."""
    mb = MeshBuilder()
    FRAME, CUSHION = 0, 1
    side = [(-0.42, 0.0), (0.36, 0.0), (0.47, 0.8), (0.33, 0.82), (0.2, 0.56), (-0.42, 0.56)]   # (y, z)
    for x0, x1 in ((-0.42, -0.32), (0.32, 0.42)):
        extrude(mb, side, "x", x0, x1, FRAME)
    w = 0.315                                       # cushions stop 5 mm short of the panels: no shared faces
    extrude(mb, [(-0.41, 0.17), (0.26, 0.17), (0.26, 0.4), (-0.37, 0.4), (-0.41, 0.36)], "x", -w, w, CUSHION)
    extrude(mb, [(0.15, 0.38), (0.34, 0.38), (0.43, 0.79), (0.27, 0.81)], "x", -w, w, CUSHION)
    return mb.build(name, [frame, cushion], col)


def bollard(name, steel, cap, col):
    """Short steel bollard, 0.9 m, with a darker domed cap."""
    mb = MeshBuilder()
    lathe(mb, [(0.1, -0.15), (0.1, 0.78), (0.078, 0.87), (0.0, 0.91)], 8, [0, 1, 1])
    return mb.build(name, [steel, cap], col)


def railing(name, steel, col):
    """Stainless railing segment, 2.0 m along +x from its origin: post at the start, top rail, bottom rail and
    seven vertical bars. Segments placed end to end make a continuous railing (each post hides a joint)."""
    mb = MeshBuilder()
    box(mb, -0.03, 0.03, -0.03, 0.03, -0.12, 1.1, 0)                       # post
    box(mb, 0.0, RAILING_LENGTH, -0.032, 0.032, 1.02, 1.08, 0, skip=())    # top rail (handrail)
    box(mb, 0.0, RAILING_LENGTH, -0.022, 0.022, 0.1, 0.145, 0, skip=())    # bottom rail
    for k in range(1, 8):                                                   # bars between the rails
        x = k * RAILING_LENGTH / 8
        box(mb, x - 0.018, x + 0.018, -0.018, 0.018, 0.145, 1.02, 0, skip=("bottom", "top"))
    return mb.build(name, [steel], col)


def litter_bin(name, body, lid, opening, col):
    """Round street bin, 0.95 m: body, flared steel lid band, dark opening on top."""
    mb = MeshBuilder()
    lathe(mb, [(0.22, -0.05), (0.22, 0.8), (0.25, 0.83), (0.25, 0.95), (0.0, 0.93)], 8, [0, 1, 1, 2],
          phase=math.pi / 8)
    return mb.build(name, [body, lid, opening], col)


def parasol(name, canopy, pole, base, col):
    """Beach-bar parasol, 2.75 m, 3 m across: weighted foot, slim pole, eight-panel canopy that sags slightly
    between the ribs, with a thick edge and a closed underside (reads from below and above)."""
    mb = MeshBuilder()
    CANOPY, POLE, BASE = 0, 1, 2
    lathe(mb, [(0.33, -0.05), (0.31, 0.12), (0.045, 0.17), (0.045, 2.32)], 6, [BASE, BASE, POLE])
    apex = mb.v((0, 0, 2.75))
    under = mb.v((0, 0, 2.3))
    mid = [mb.v((math.cos(a) * 0.78, math.sin(a) * 0.78, 2.43)) for a in (2 * math.pi * (j + 0.5) / 8 for j in range(8))]
    eave = [mb.v((math.cos(a) * 1.5, math.sin(a) * 1.5, 2.22)) for a in (2 * math.pi * j / 8 for j in range(8))]
    low = [mb.v((math.cos(a) * 1.48, math.sin(a) * 1.48, 2.14)) for a in (2 * math.pi * j / 8 for j in range(8))]
    for j in range(8):
        k = (j + 1) % 8
        mb.f((eave[j], eave[k], mid[j]), CANOPY)       # panel between two ribs, sagging at its middle
        mb.f((mid[j], eave[k], apex), CANOPY)
        mb.f((eave[j], mid[j], apex), CANOPY)
        mb.f((low[j], low[k], eave[k], eave[j]), CANOPY)   # thick edge
        mb.f((low[k], low[j], under), CANOPY)          # underside
    return mb.build(name, [canopy, pole, base], col)


def build_prototypes():
    """Creates every prop prototype in collection Proto_Props and returns {name: object}.
    Rebuilding replaces the old objects. Raises if a prototype breaks its triangle budget."""
    col = E.collection(PROTO_COLLECTION)
    for obj in list(col.objects):
        mesh = obj.data
        bpy.data.objects.remove(obj)
        if mesh and mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    m = E.material
    metal = m("lamp_metal", "#4A4E55", roughness=0.45, metallic=0.6)
    light = m("lamp_light", "#FFF3D6", roughness=0.5, emission="#FFE9B8")
    steel = m("steel", "#7A6C52", roughness=0.55, metallic=0.5)   # campus railings are bronze (walk video)
    protos = {
        "Prop_LampCampus": lamp_campus("Prop_LampCampus", metal, m("concrete_light", roughness=0.9), light, col),
        "Prop_LampRoad": lamp_road("Prop_LampRoad", metal, m("lamp_foot", "#3A3D42", roughness=0.6, metallic=0.4),
                                   light, col),
        "Prop_Bench": bench("Prop_Bench", m("concrete_sand", roughness=0.9), m("bench_wood", "#A27E58", roughness=0.8),
                            col),
        "Prop_LoungeChair": lounge_chair("Prop_LoungeChair", m("chair_frame", "#7C8189", roughness=0.6),
                                         m("chair_cushion", "#B7BBC1", roughness=0.9), col),
        "Prop_Bollard": bollard("Prop_Bollard", steel, m("bollard_cap", "#2E3136", roughness=0.4, metallic=0.5), col),
        "Prop_RailingSegment": railing("Prop_RailingSegment", steel, col),
        "Prop_Bin": litter_bin("Prop_Bin", m("bin_body", "#5D646B", roughness=0.6, metallic=0.3), steel,
                               m("gloss_black", roughness=0.5), col),
        "Prop_Parasol": parasol("Prop_Parasol", m("parasol_canvas", "#F2EEE6", roughness=0.9),
                                m("bark_light", roughness=0.8), m("concrete_light", roughness=0.9), col),
    }
    for name, obj in protos.items():
        tris = triangles(obj)
        if tris > TRI_BUDGET[name]:
            raise ValueError(f"{name}: {tris} triangles, budget {TRI_BUDGET[name]}")
    return protos


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# =====================================================================================================
# Placement
# =====================================================================================================
def _segment_distance(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    length2 = dx * dx + dy * dy
    t = 0.0 if length2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / length2))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


def _polygon_clearance(x, y, pts):
    """Distance to the polygon outline, negative inside."""
    d = min(_segment_distance(x, y, ax, ay, bx, by) for (ax, ay), (bx, by) in zip(pts, pts[1:] + pts[:1]))
    return -d if E.inside_polygon(x, y, pts) else d


class Site:
    """What the placement rules ask about a point: roads, buildings, campus, route frames."""

    def __init__(self):
        self.roads = []
        for p in E.DATA["paths"]:
            if p["type"] in ROAD_TYPES and not p.get("tunnel"):
                hw = p["width"] / 2
                self.roads += [(ax, ay, bx, by, hw) for (ax, ay), (bx, by) in zip(p["points"], p["points"][1:])]
        self.buildings = []
        for b in E.DATA["buildings"]:
            xs, ys = [q[0] for q in b["points"]], [q[1] for q in b["points"]]
            self.buildings.append((min(xs), min(ys), max(xs), max(ys), b))
        self.pts = E.route_points()
        self.trees = {}
        for tx, ty in E.DATA.get("trees", []):
            self.trees.setdefault((int(tx // 10), int(ty // 10)), []).append((tx, ty))

    def tree_clearance(self, x, y):
        """Distance to the nearest real (OSM) tree, which env_vegetation plants at or near that spot."""
        cx, cy = int(x // 10), int(y // 10)
        return min((math.hypot(tx - x, ty - y) for dx in (-1, 0, 1) for dy in (-1, 0, 1)
                    for tx, ty in self.trees.get((cx + dx, cy + dy), ())), default=float("inf"))

    def road_clearance(self, x, y):
        best = float("inf")
        for ax, ay, bx, by, hw in self.roads:
            if min(ax, bx) - 20 < x < max(ax, bx) + 20 and min(ay, by) - 20 < y < max(ay, by) + 20:
                best = min(best, _segment_distance(x, y, ax, ay, bx, by) - hw)
        return best

    def building_clearance(self, x, y, campus_only=False, reach=60.0):
        best = float("inf")
        for x0, y0, x1, y1, b in self.buildings:
            if campus_only and not b["campus"]:
                continue
            if x0 - reach < x < x1 + reach and y0 - reach < y < y1 + reach:
                best = min(best, _polygon_clearance(x, y, b["points"]))
        return best

    def ok(self, x, y, clear=E.KEEP_CLEAR, footprint=0.3):
        """The hard rules for a prop standing next to the route (not on the deck)."""
        if not (E.X0 + 1 < x < E.X1 - 1 and E.Y0 + 1 < y < E.Y1 - 1):
            return False
        if E.ground_class(x, y) in ("sea", "water"):
            return False
        if E.distance_to_route(x, y) < clear + footprint * 0.5:
            return False
        if self.building_clearance(x, y) < BUILDING_GAP:
            return False
        if self.road_clearance(x, y) <= ROAD_GAP:
            return False
        if self.tree_clearance(x, y) < footprint + TREE_GAP:
            return False
        near = E.nearest_route_point(x, y)
        return near.get("zone", "") not in NO_PROP_ZONES

    def frame(self, i):
        """Route point i, unit tangent and unit left normal (closed loop)."""
        n = len(self.pts)
        a, b = self.pts[(i - 1) % n], self.pts[(i + 1) % n]
        d = math.hypot(b["x"] - a["x"], b["y"] - a["y"]) or 1.0
        tx, ty = (b["x"] - a["x"]) / d, (b["y"] - a["y"]) / d
        return self.pts[i % n], (tx, ty), (-ty, tx)

    def offset(self, i, off):
        p, _t, (nx, ny) = self.frame(i)
        return p["x"] + nx * off, p["y"] + ny * off


def _prop_z(x, y):
    """Terrain height (E.ground_z: the shaped corridor surface once the world build installed it). If the terrain
    is still the raw elevation data, a prop within 8 m of the centreline stands on the driving height instead
    (route.json track_z, where the road bed will be) when that is less than 3 m away from the terrain."""
    z = E.ground_z(x, y)
    if E.distance_to_route(x, y) < 8.0:
        p = E.nearest_route_point(x, y)
        shaped = abs(E.ground_z(p["x"], p["y"]) - p["track_z"]) < 0.3
        indoor = p["tunnel"] or p.get("zone") in ("tunnel", "atrium", "terrace deck")
        if not shaped and not indoor and abs(p["track_z"] - z) < 3.0:
            return p["track_z"] - 0.05
    return z


def _entry(proto, x, y, z, rot, scale=1.0, **extra):
    e = {"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(z, 3), "rot_z": round(_wrap(rot), 4),
         "scale": round(scale, 3)}
    e.update({k: round(v, 4) + 0.0 for k, v in extra.items()})     # + 0.0 turns -0.0 into 0.0
    return e


def _wrap(a):
    return (a + math.pi) % (2 * math.pi) - math.pi


def _facing(dx, dy):
    """rot_z that turns local -y (the way a sitter looks) towards (dx, dy)."""
    return math.atan2(dx, -dy)


def _coast_distance(x, y):
    """Distance to the OSM coastline (world_data.json 'coast')."""
    return min((_segment_distance(x, y, ax, ay, bx, by) for line in E.DATA.get("coast", [])
                for (ax, ay), (bx, by) in zip(line, line[1:])), default=float("inf"))


def _sea_side(site, idx):
    """+1 if the sea is on the left of the driving direction along these route indices, else -1
    (votes: which side's point 15 m out is closer to the coastline)."""
    votes = 0
    for i in idx:
        p, _t, (nx, ny) = site.frame(i)
        left = _coast_distance(p["x"] + nx * 15, p["y"] + ny * 15)
        right = _coast_distance(p["x"] - nx * 15, p["y"] - ny * 15)
        votes += 1 if left < right else -1
    return 1 if votes >= 0 else -1


def _runs(indices):
    """Consecutive runs of route indices, e.g. [3,4,5,9,10] -> [[3,4,5],[9,10]]."""
    runs = []
    for i in indices:
        if runs and i == runs[-1][-1] + 1:
            runs[-1].append(i)
        else:
            runs.append([i])
    return runs


def _every(site, indices, step, start=None):
    """Route indices spaced about `step` metres apart (by route s) within a sorted list of indices."""
    out, next_s = [], None
    for i in indices:
        s = site.pts[i]["s"]
        if next_s is None:
            next_s = s + (step / 2 if start is None else start)
        if s >= next_s:
            out.append(i)
            next_s = s + step
    return out


def _railing_run(site, idx, side, z_of, report_key, own_roof=None):
    """Railing segments end to end along route indices `idx`, RAILING_OFFSET m to one side. Each segment is
    exactly RAILING_LENGTH long (3-D), turned along the offset line and pitched to follow its slope.
    A segment that would cut into a building (other than `own_roof`, the deck it stands on) is left out."""
    line = []
    for i in idx:
        p, _t, (nx, ny) = site.frame(i)
        line.append(Vector((p["x"] + nx * RAILING_OFFSET * side, p["y"] + ny * RAILING_OFFSET * side, z_of(p))))
    if len(line) < 2:
        return []
    dense = []                                         # 0.1 m steps along the offset line
    for a, b in zip(line, line[1:]):
        steps = max(1, int((b - a).length / 0.1))
        dense += [a.lerp(b, k / steps) for k in range(steps)]
    dense.append(line[-1])
    out, start, k = [], dense[0], 0
    while True:
        while k < len(dense) and (dense[k] - start).length < RAILING_LENGTH:
            k += 1
        if k >= len(dense):
            break                                      # the rest is shorter than one segment: leave it open
        end = dense[k]
        d = end - start
        flat = math.hypot(d.x, d.y)
        inside = any(_polygon_clearance(q.x, q.y, b["points"]) < 0.3 for q in (start, (start + end) / 2, end)
                     for *_box, b in site.buildings if b is not own_roof
                     and _box[0] - 3 < q.x < _box[2] + 3 and _box[1] - 3 < q.y < _box[3] + 3)
        if not inside:
            out.append(_entry("Prop_RailingSegment", start.x, start.y, start.z, math.atan2(d.y, d.x),
                              rot_y=-math.atan2(d.z, flat)))
        start = end
    REPORT.setdefault("railing_runs", []).append((report_key, len(out)))
    return out


def place(seed=SEED, write=True, avoid=()):
    """Computes all prop placements and writes out/placements_props.json.
    avoid: vegetation placements (env_vegetation.place()) to keep clear of, so no prop stands in a tree.

    Campus lamps every ~30 m, alternating sides, 6.5 m from the centreline on the upper and lower plaza and on
    the lawns near campus. Road lamps every ~35 m on the coastal road and the promenade, on the sea side.
    Railings 4.6 m out: both sides of the terrace deck (gap of 12 m at the jump), both sides of the north
    tunnel ramp (s 86-150), the sea side of the promenade (route ground_z < 4). Benches every ~25 m on the
    promenade's land side, a bin beside every second bench and every third campus lamp. A row of 5 bollards
    on each side across the tunnel plaza (south portal) and the lower plaza (top of the north ramp). Lounge
    chairs in small groups on the deck, 7-12 m from the centreline and inside the roof edge. Parasols at the
    beach bar. Nothing in the tunnel or the atrium; everything except railings and deck chairs keeps
    E.KEEP_CLEAR from the route, 2 m from buildings, off roads, off water and clear of the real tree spots.
    """
    rng = random.Random(seed)
    site = Site()
    pts = site.pts
    n = len(pts)
    placed = []
    REPORT.clear()

    import env_vegetation as V                      # same reach as the overlap rule in test_env_assets.py
    plants = [(v["x"], v["y"], V.KIND[V._kind(v["proto"])]["space"] * v["scale"]) for v in avoid]

    def free(x, y, proto):
        """No other prop (railings aside) closer than both footprints plus 0.3 m, and clear of the plants."""
        r = FOOTPRINT[proto]
        return (all(math.hypot(x - q["x"], y - q["y"]) >= r + FOOTPRINT[q["proto"]] + 0.3
                    for q in placed if q["proto"] != "Prop_RailingSegment") and
                all(math.hypot(x - px, y - py) >= r + pr for px, py, pr in plants))

    def fits(x, y, proto, clear=E.KEEP_CLEAR):
        return site.ok(x, y, clear=clear, footprint=FOOTPRINT[proto]) and free(x, y, proto)

    def try_spot(i, side, offsets, proto, shifts=(0, 1, -1, 2, -2, 3, -3)):
        """First valid spot near route index i on that side; returns (x, y, i, offset) or None."""
        for di in shifts:
            for off in offsets:
                x, y = site.offset(i + di, off * side)
                if fits(x, y, proto):
                    return x, y, (i + di) % n, off * side
        return None

    def toward_route(x, y, j):
        p = pts[j]
        return math.atan2(p["y"] - y, p["x"] - x)

    # campus lamps -------------------------------------------------------------------------------------------
    campus = [i for i, p in enumerate(pts) if p.get("zone") in CAMPUS_LAMP_ZONES and not p["building"]
              and (p["zone"] != "lawn and paths" or site.building_clearance(p["x"], p["y"], True) <= CAMPUS_NEAR)]
    side, lamps, missed = 1, 0, 0
    for i in _every(site, campus, CAMPUS_LAMP_STEP):
        spot = try_spot(i, side, (CAMPUS_LAMP_OFFSET, CAMPUS_LAMP_OFFSET + 1.0), "Prop_LampCampus") or \
            try_spot(i, -side, (CAMPUS_LAMP_OFFSET, CAMPUS_LAMP_OFFSET + 1.0), "Prop_LampCampus")
        if not spot:
            missed += 1
            continue
        x, y, j, off = spot
        placed.append(_entry("Prop_LampCampus", x, y, _prop_z(x, y), toward_route(x, y, j)))
        lamps += 1
        if lamps % 3 == 0:                             # a bin 1.3 m along the path from every third lamp
            _p, (tx, ty), _nrm = site.frame(j)
            bx, by = x + tx * 1.3, y + ty * 1.3
            if fits(bx, by, "Prop_Bin"):
                placed.append(_entry("Prop_Bin", bx, by, _prop_z(bx, by), rng.uniform(-math.pi, math.pi)))
        side = -1 if off > 0 else 1                    # alternate from the side actually used
    REPORT["campus_lamps_missed"] = missed

    # road lamps ---------------------------------------------------------------------------------------------
    missed = 0
    for zone_run in _runs([i for i, p in enumerate(pts) if p.get("zone") in ROAD_LAMP_ZONES]):
        for i in _every(site, zone_run, ROAD_LAMP_STEP):
            sea = _sea_side(site, range(i - 10, i + 11))       # decided locally: the coastal road turns
            spot = try_spot(i, sea, ROAD_LAMP_OFFSETS, "Prop_LampRoad")
            if not spot and pts[i]["zone"] != "promenade":     # the coastal road may use the land side
                spot = try_spot(i, -sea, ROAD_LAMP_OFFSETS, "Prop_LampRoad")
            if not spot:
                missed += 1
                continue
            x, y, j, _off = spot
            placed.append(_entry("Prop_LampRoad", x, y, _prop_z(x, y), toward_route(x, y, j)))
    REPORT["road_lamps_missed"] = missed

    # railings -----------------------------------------------------------------------------------------------
    jump = E.ROUTE["jump"]["index"]
    jump_s = pts[jump]["s"]
    deck_idx = [i for i, p in enumerate(pts) if p["level"]]
    deck_roof = next((b for *_box, b in site.buildings if deck_idx and b["name"] == pts[deck_idx[0]]["building"]),
                     None)                             # the building whose roof is the deck (CTT Student Hall)
    deck = [i for i in deck_idx if abs(pts[i]["s"] - jump_s) > JUMP_GAP / 2]
    for run in _runs(deck):
        for side in (1, -1):
            placed += _railing_run(site, run, side, lambda p: p["track_z"], f"deck {side:+d}", own_roof=deck_roof)
    ramp = [i for i, p in enumerate(pts) if RAMP_S[0] <= p["s"] <= RAMP_S[1] and not p["building"]
            and p.get("zone") not in NO_PROP_ZONES]
    for run in _runs(ramp):
        for side in (1, -1):
            placed += _railing_run(site, run, side, lambda p: p["track_z"], f"ramp {side:+d}")
    seawall = [i for i, p in enumerate(pts) if p["ground_z"] < SEAWALL_GROUND_Z and p.get("zone") not in NO_PROP_ZONES]
    for run in _runs(seawall):
        placed += _railing_run(site, run, _sea_side(site, run), lambda p: p["track_z"], "promenade sea side")

    # parasols at the beach bar --------------------------------------------------------------------------------
    centre = None
    for r in [0.0] + [0.5 * k for k in range(1, 41)]:
        for a in range(0, 360, 15):
            x = BEACH_BAR[0] + math.cos(math.radians(a)) * r
            y = BEACH_BAR[1] + math.sin(math.radians(a)) * r
            if site.ok(x, y, clear=PARASOL_CLEAR, footprint=FOOTPRINT["Prop_Parasol"]):
                centre = (x, y)
                break
        if centre:
            break
    parasols = 0
    if centre:
        j = min(range(n), key=lambda q: (pts[q]["x"] - centre[0]) ** 2 + (pts[q]["y"] - centre[1]) ** 2)
        _p, (tx, ty), _nrm = site.frame(j)
        for u, v in ((-1.9, -1.9), (1.9, -1.9), (-1.9, 1.9), (1.9, 1.9), (0.0, 0.0), (5.4, 0.0), (-5.4, 0.0)):
            if parasols >= 5:
                break
            x = centre[0] + tx * u - ty * v
            y = centre[1] + ty * u + tx * v
            if fits(x, y, "Prop_Parasol", clear=PARASOL_CLEAR):
                placed.append(_entry("Prop_Parasol", x, y, _prop_z(x, y), rng.uniform(-math.pi, math.pi)))
                parasols += 1
    REPORT["parasol_centre"] = centre and (round(centre[0], 1), round(centre[1], 1))
    REPORT["parasols"] = parasols

    # benches and bins on the promenade's land side --------------------------------------------------------------
    missed = 0
    for zone_run in _runs([i for i, p in enumerate(pts) if p.get("zone") == "promenade"]):
        land = -_sea_side(site, zone_run)
        for k, i in enumerate(_every(site, zone_run, BENCH_STEP)):
            spot = try_spot(i, land, (BENCH_OFFSET, BENCH_OFFSET + 1.0), "Prop_Bench", shifts=(0, 1, -1, 2, -2))
            if not spot:
                missed += 1
                continue
            x, y, j, off = spot
            p, (tx, ty), (nx, ny) = site.frame(j)
            to_route = (-nx * math.copysign(1, off), -ny * math.copysign(1, off))
            placed.append(_entry("Prop_Bench", x, y, _prop_z(x, y), _facing(*to_route)))
            if k % 2 == 1:
                bx, by = x + tx * 2.0, y + ty * 2.0
                if fits(bx, by, "Prop_Bin"):
                    placed.append(_entry("Prop_Bin", bx, by, _prop_z(bx, by), rng.uniform(-math.pi, math.pi)))
    REPORT["benches_missed"] = missed

    # bollards: a row across each portal plaza, five on each side outside the road -------------------------------
    def bollard_row(i):
        p, (tx, ty), (nx, ny) = site.frame(i)
        spots = [(p["x"] + nx * off * side, p["y"] + ny * off * side) for side in (1, -1) for off in BOLLARD_OFFSETS]
        return [(x, y) for x, y in spots if fits(x, y, "Prop_Bollard")], math.atan2(ty, tx)

    tunnel = [i for i, p in enumerate(pts) if p["tunnel"]]
    plazas = (("south portal (tunnel plaza)", [i for i, p in enumerate(pts) if p.get("zone") == "tunnel plaza"]),
              ("north ramp top (lower plaza)", [i for i, p in enumerate(pts) if p.get("zone") == "lower plaza"]))
    REPORT["bollard_rows"] = []
    for label, zone_idx in plazas:
        if not zone_idx and tunnel and label.startswith("north"):
            zone_idx = [tunnel[-1] + k for k in range(3, 9)]          # no plaza: just outside the north portal
        best = None
        for i in sorted(zone_idx, key=lambda q: -q if label.startswith("south") else q):   # nearest the portal first
            spots, heading = bollard_row(i)
            if best is None or len(spots) > len(best[1]):
                best = (i, spots, heading)
        if best:
            i, spots, heading = best
            for x, y in spots:
                placed.append(_entry("Prop_Bollard", x, y, _prop_z(x, y), heading))
            REPORT["bollard_rows"].append((label, round(pts[i]["s"]), len(spots)))

    # lounge chairs on the terrace deck (CTT Student Hall roof) -----------------------------------------------------
    chairs = 0
    if deck_roof:
        def on_deck(x, y):
            return (_polygon_clearance(x, y, deck_roof["points"]) <= -DECK_EDGE_GAP
                    and CHAIR_BAND[0] <= E.distance_to_route(x, y) <= CHAIR_BAND[1])

        taken = []
        for i in _every(site, deck_idx, 8.0, start=2.0):
            for side in (1, -1):
                for off in (8.6, 8.0, 9.4, 7.6, 10.4):
                    gx, gy = site.offset(i, off * side)
                    if not on_deck(gx, gy) or any(math.hypot(gx - a, gy - b) < 4.0 for a, b in taken):
                        continue
                    group = []
                    k = rng.choice((2, 3))
                    turn = rng.uniform(0, 2 * math.pi)
                    for q in range(k):
                        a = turn + 2 * math.pi * q / k
                        cx, cy = gx + math.cos(a) * 0.95, gy + math.sin(a) * 0.95
                        if on_deck(cx, cy):
                            group.append(_entry("Prop_LoungeChair", cx, cy, pts[i]["track_z"],
                                                _facing(gx - cx, gy - cy) + rng.uniform(-0.25, 0.25)))
                    if not group:                    # no room for a group: one chair looking away from the road
                        _p, _t, (nx, ny) = site.frame(i)
                        group.append(_entry("Prop_LoungeChair", gx, gy, pts[i]["track_z"],
                                            _facing(nx * side, ny * side) + rng.uniform(-0.3, 0.3)))
                    placed += group
                    chairs += len(group)
                    taken.append((gx, gy))
                    break
    REPORT["lounge_chairs"] = chairs

    if write:
        os.makedirs(E.OUT, exist_ok=True)
        with open(PLACEMENTS_FILE, "w") as fh:
            json.dump({
                "frame": "world metres like route.json: x east, y north, z up; rot_z radians counter-clockwise seen "
                         "from above; rot_y (railings only) = pitch about the segment's local y axis, applied before "
                         "rot_z; uniform scale; prototypes from env_props.py",
                "seed": seed,
                "counts": {name: sum(1 for p in placed if p["proto"] == name) for name in TRI_BUDGET},
                "placements": placed,
            }, fh, indent=1)
    return placed


def instance_for_preview(placements, protos):
    """Linked duplicates (they share the prototype mesh) in collection World_Props, for renders only.
    The prototype collection is hidden from renders, because the prototypes stand at the world origin."""
    col = E.collection(WORLD_COLLECTION)
    for obj in list(col.objects):
        bpy.data.objects.remove(obj)
    proto_col = bpy.data.collections.get(PROTO_COLLECTION)
    if proto_col:
        proto_col.hide_render = True
    objects = []
    for k, p in enumerate(placements):
        obj = bpy.data.objects.new(f"{p['proto']}.{k:04d}", protos[p["proto"]].data)
        obj.location = (p["x"], p["y"], p["z"])
        obj.rotation_euler = (0.0, p.get("rot_y", 0.0), p["rot_z"])
        obj.scale = (p["scale"],) * 3
        col.objects.link(obj)
        objects.append(obj)
    return objects
