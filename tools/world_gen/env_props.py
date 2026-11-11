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
              "Prop_Bollard": 40, "Prop_RailingSegment": 100, "Prop_Bin": 60, "Prop_Parasol": 100,
              # design pass 2026-11-09 (Deep-Research catalogue, Phase 1): cafe kit, planters, atrium
              "Prop_BistroTable": 112, "Prop_BistroChair": 120, "Prop_TroughBloom": 210, "Prop_PlanterRound": 180,
              "Prop_StudyTable": 96, "Prop_GrandPiano": 180,
              # beach pass V10b 2026-11-10: straw parasols, loungers, lifeguard post, stone heaps
              "Prop_ParasolThatch": 80, "Prop_SunLounger": 64, "Prop_LifeguardTower": 260, "Prop_StonePile": 330}
FOOTPRINT = {"Prop_LampCampus": 0.3, "Prop_LampRoad": 0.3, "Prop_Bench": 1.0, "Prop_LoungeChair": 0.6,
             "Prop_Bollard": 0.15, "Prop_RailingSegment": 0.0, "Prop_Bin": 0.3, "Prop_Parasol": 1.6,
             "Prop_BistroTable": 0.4, "Prop_BistroChair": 0.3, "Prop_TroughBloom": 0.85, "Prop_PlanterRound": 0.45,
             "Prop_StudyTable": 0.95, "Prop_GrandPiano": 1.2,
             "Prop_ParasolThatch": 1.4, "Prop_SunLounger": 0.9, "Prop_LifeguardTower": 1.6, "Prop_StonePile": 2.0}

# Placement rules (metres along the lap = route s, offsets from the centreline; + = left of the driving direction)
NO_PROP_ZONES = {"tunnel", "atrium"}                  # indoors / underground: nothing stands there
CAMPUS_LAMP_ZONES = {"upper plaza", "lower plaza", "lawn and paths"}
ROAD_LAMP_ZONES = {"coastal road", "promenade"}
CAMPUS_NEAR = 40.0                  # 'lawn and paths' counts as campus within this distance of a campus building
CAMPUS_LAMP_STEP, CAMPUS_LAMP_OFFSET = 30.0, 6.5
ROAD_LAMP_STEP, ROAD_LAMP_OFFSETS = 35.0, (6.5, 7.5, 8.5, 9.5)
RAILING_OFFSET, RAILING_LENGTH = 4.6, 2.0
JUMP_GAP = 12.0                     # no deck railing within 6 m of the jump take-off
# north tunnel ramp (railings on both sides) = the "ramp" points that follow the tunnel, whatever the route
SEAWALL_GROUND_Z = 4.0              # promenade railing where the route's ground_z is below this
SEAWALL_RAILING = False             # V10b: no blanket railing along the promenade (photos K03-K07: an open granite
                                    # kerb, railings only at the stairs, built by env_beachfront)
BENCH_STEP, BENCH_OFFSET = 25.0, 7.0
BOLLARD_OFFSETS = (6.5, 8.0, 9.5, 11.0, 12.5)   # a row of 5 on each side, outside the 8 m road
CHAIR_BAND = (7.0, 12.0)            # lounge chairs on the deck, this far from the centreline
DECK_EDGE_GAP = 0.6                 # ... and at least this far inside the deck (roof) edge
BEACH_BAR = (-75.0, -148.0)         # parasols near here, each >= PARASOL_CLEAR from the route (until V10b)
PARASOL_CLEAR = 8.0
# Beach pass V10b (2026-11-10), evidence: ortho_west / ortho_south, photos K02-K06
PAVILION_TERRACE = ((-44.3, -140.8), (0.968, -0.252), 6.3, (-6.0, -2.0, 2.0, 6.0))   # centre, long axis, out, tables
BEACH_GRID = dict(origin=(-351.7, -17.4), along=(0.854, -0.519), rows=4, per_row=9, step=3.7, row_step=5.8,
                  loungers_rows=2)      # straw parasols in rows on the sand west of A Pastorinha (ortho_west)
LIFEGUARD = (-321.0, -94.0)            # lifeguard post on the main beach, facing the water
STONE_PILES = ((-55.0, -161.0, 1.0), (-74.0, -159.8, 0.85), (-42.5, -171.0, 0.7))   # heaps on the cove sand (K05, K06)
BEACH_SEA = (-0.742, -0.670)           # towards the water on the main beach
BUILDING_GAP, ROAD_GAP = 2.0, 1.0
TREE_GAP = 1.2                      # props keep their footprint + this from the real (OSM) tree positions
ROAD_TYPES = {"primary", "primary_link", "secondary", "tertiary", "residential", "unclassified", "service",
              "living_street"}
REPORT = {}                         # filled by place(): what could not be placed and why

# Design pass 2026-11-09 (design reference: busy cafe terraces, planters lining the track, a furnished atrium)
CAFE_ZONES = {"upper plaza": 4, "promenade": 2, "lower plaza": 1}   # cafe groups per zone (at most)
CAFE_STEP = 38.0                    # m of route between cafe groups
CAFE_OFFSETS = (9.5, 10.5, 11.5, 12.5, 14.0)   # group centre from the route centreline
TROUGH_ZONES = {"upper plaza", "lower plaza", "promenade"}
TROUGH_STEP, TROUGH_OFFSET = 18.0, 6.6          # a pair of flower troughs every ~18 m on straights
HALL_ROAD_BAND = 5.6                # atrium: furniture stays this far from the centreline (1.6 m off the road)
HALL_WALL_BAND = 1.25               # ... and this far inside the hall wall (columns stand at wall - 0.6)
HALL_STEP = 6                       # route points (12 m) between furniture bays in the atrium
HALL_PROTOS = {"Prop_BistroTable", "Prop_BistroChair", "Prop_StudyTable", "Prop_GrandPiano", "Prop_PlanterRound",
               "Prop_TroughBloom"}       # may stand indoors (atrium bays), checked by the hall rules


# =====================================================================================================
# Geometry helpers
# =====================================================================================================
from env_vegetation import MeshBuilder, puff_lobe   # noqa: E402  same builder (flat or smooth "puffy" faces)


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


def bistro_table(name, top, metal, col):
    """Round cafe table (our own design), 0.76 m across, 0.755 m high: disc foot, slim pedestal, round top with
    a bevelled rim. The parasol of a cafe group stands through its centre."""
    mb = MeshBuilder()
    TOP, METAL = 0, 1
    lathe(mb, [(0.22, -0.02), (0.22, 0.03), (0.045, 0.07), (0.04, 0.70)], 6, METAL)
    lathe(mb, [(0.04, 0.70), (0.33, 0.70), (0.38, 0.715), (0.38, 0.75), (0.0, 0.755)], 10, TOP)
    return mb.build(name, [top, metal], col)


def bistro_chair(name, wood, metal, col):
    """Cafe chair (our own design): slim dark frame, wooden seat and a slightly raked two-slat back.
    0.45 m wide, 0.84 m high, seat at 0.48 m; the sitter looks along -y (back at +y)."""
    mb = MeshBuilder()
    WOOD, METAL = 0, 1
    for x in (-0.19, 0.19):
        box(mb, x - 0.018, x + 0.018, -0.208, -0.172, -0.02, 0.44, METAL, skip=("bottom", "top"))   # front legs
        box(mb, x - 0.018, x + 0.018, 0.172, 0.208, -0.02, 0.84, METAL, skip=("bottom",))         # back legs
    box(mb, -0.225, 0.225, -0.225, 0.2, 0.44, 0.48, WOOD)
    for z0, z1 in ((0.56, 0.64), (0.70, 0.80)):
        extrude(mb, [(0.165, z0), (0.19, z0), (0.19 + (z1 - z0) * 0.12, z1), (0.165 + (z1 - z0) * 0.12, z1)],
                "x", -0.2, 0.2, WOOD)
    return mb.build(name, [wood, metal], col)


def trough_bloom(name, body, rim, soil, leaf, bloom, bloom_light, col):
    """Long planter (1.6 x 0.55 m, 0.5 m high) in the event violet with a cream rim, full of round purple
    flower mounds on green leaves (design reference: planters lining the track). Runs along x."""
    rng = random.Random(name)
    mb = MeshBuilder()
    BODY, RIM, SOIL, LEAF, BLOOM, LIGHT = 0, 1, 2, 3, 4, 5
    profile = [(-0.27, -0.05), (0.27, -0.05), (0.27, 0.44), (0.235, 0.5), (-0.235, 0.5), (-0.27, 0.44)]
    extrude(mb, profile, "x", -0.8, 0.8, [BODY, BODY, RIM, SOIL, RIM, BODY, BODY])
    for k, x in enumerate((-0.58, -0.2, 0.2, 0.58)):
        c = (x + rng.uniform(-0.04, 0.04), rng.uniform(-0.04, 0.04), 0.52)
        puff_lobe(mb, c, (0.27, 0.22, 0.17), rng, lambda z: LEAF, (x, 0.0, 0.4), subdiv=1, floor=-0.4)
    for k, x in enumerate((-0.62, -0.3, 0.02, 0.33, 0.62)):
        c = (x + rng.uniform(-0.05, 0.05), rng.uniform(-0.08, 0.08), 0.66 + rng.uniform(-0.03, 0.04))
        tone = LIGHT if k % 2 == 0 else BLOOM            # whole heads alternate light / mid violet
        puff_lobe(mb, c, (0.15, 0.14, 0.12), rng, lambda z, t=tone: t, (x, 0.0, 0.55),
                  subdiv=1, floor=-0.6, blend=0.4)
    return mb.build(name, [body, rim, soil, leaf, bloom, bloom_light], col)


def planter_round(name, pot, soil, crown, crown_top, col):
    """Round cream planter (0.84 m across, 0.55 m high) with a clipped round bush (design reference: round,
    soft greens along the plazas)."""
    rng = random.Random(name)
    mb = MeshBuilder()
    POT, SOIL, CROWN, TOP = 0, 1, 2, 3
    lathe(mb, [(0.30, -0.05), (0.40, 0.50), (0.42, 0.55), (0.36, 0.55), (0.36, 0.49), (0.0, 0.49)], 10,
          [POT, POT, POT, POT, SOIL])
    puff_lobe(mb, (0.0, 0.0, 0.98), (0.46, 0.44, 0.44), rng, lambda z: TOP if z > -0.35 else CROWN, (0.0, 0.0, 0.9),
              floor=-0.75)
    return mb.build(name, [pot, soil, crown, crown_top], col)


def study_table(name, top, metal, col):
    """Long study table for the atrium (1.8 x 0.8 m, 0.75 m high): light timber top on four slim dark legs.
    Runs along x."""
    mb = MeshBuilder()
    TOP, METAL = 0, 1
    box(mb, -0.9, 0.9, -0.4, 0.4, 0.70, 0.75, TOP, skip=())
    for x in (-0.82, 0.82):
        for y in (-0.32, 0.32):
            box(mb, x - 0.025, x + 0.025, y - 0.025, y + 0.025, -0.02, 0.70, METAL, skip=("bottom", "top"))
    return mb.build(name, [top, metal], col)


def grand_piano(name, gloss, keys, col):
    """Grand piano (our own simple outline, the atrium has one): wing-shaped case on three legs, lid propped
    open on the curved side, keyboard at the front. 1.5 m wide, 2.0 m deep; the player sits at -y."""
    mb = MeshBuilder()
    GLOSS, KEYS = 0, 1
    outline = [(-0.74, 0.0), (0.74, 0.0), (0.74, 0.35), (0.62, 0.75), (0.42, 1.05), (0.24, 1.35), (0.12, 1.68),
               (-0.04, 1.93), (-0.3, 2.03), (-0.58, 2.0), (-0.74, 1.86)]

    def slab(pts, z0, z1, lift=None):
        """Prism over the outline; lift(x, y, z) -> point lets the lid tilt about its hinge."""
        f = lift or (lambda x, y, z: (x, y, z))
        lo = [mb.v(f(x, y, z0)) for x, y in pts]
        hi = [mb.v(f(x, y, z1)) for x, y in pts]
        n = len(pts)
        for k in range(n):
            mb.f((lo[k], lo[(k + 1) % n], hi[(k + 1) % n], hi[k]), GLOSS)
        mb.f(lo[::-1], GLOSS)
        mb.f(hi, GLOSS)

    slab(outline, 0.60, 0.98)
    ang = math.radians(16)                                  # lid hinged along the straight long side (x = -0.74)

    def lid(x, y, z):
        dx, dz = x + 0.74, z - 0.98
        return (-0.74 + dx * math.cos(ang) - dz * math.sin(ang), y, 0.98 + dx * math.sin(ang) + dz * math.cos(ang))

    slab([(x, y) for x, y in outline if y >= 0.3] + [(-0.74, 0.3)], 0.985, 1.01, lift=lid)
    box(mb, 0.36, 0.39, 0.95, 0.98, 0.98, 0.98 + 1.13 * math.sin(ang), GLOSS, skip=("bottom",))    # lid stick
    box(mb, -0.72, 0.72, -0.28, 0.0, 0.66, 0.74, GLOSS, skip=("bottom", "top"))                    # key bed
    box(mb, -0.70, 0.70, -0.27, 0.0, 0.74, 0.76, KEYS, skip=("bottom",))                           # keys
    box(mb, -0.45, 0.45, 0.10, 0.13, 0.98, 1.24, GLOSS, skip=("bottom",))                          # music desk
    for x, y in ((-0.62, 0.14), (0.62, 0.14), (-0.42, 1.78)):
        box(mb, x - 0.05, x + 0.05, y - 0.05, y + 0.05, -0.02, 0.60, GLOSS, skip=("bottom", "top"))
    return mb.build(name, [gloss, keys], col)


def parasol_thatch(name, straw, pole, col):
    """Straw beach parasol (the dried-grass parasols in rows on Carcavelos beach, ortho_west): a slim timber pole
    and a ten-sided cone of straw, 2.9 m across, with a short ragged fringe and a flat underside. UVs for the
    straw texture (make_textures.py thatch): u around the parasol (two repeats), v from the tip to the fringe.
    One connected canopy; the UV seam is a doubled column of vertices, so the faces keep their winding (keep)."""
    mb = MeshBuilder()
    STRAW, POLE = 0, 1
    lathe(mb, [(0.06, -0.3), (0.05, 2.25)], 5, POLE)
    sides, r_rim, z_tip, z_rim, z_fringe = 10, 1.45, 2.85, 2.25, 2.08
    rr = r_rim + 0.06
    tip = mb.v((0, 0, z_tip), uv=(0.5, 0.0))
    hub = mb.v((0, 0, z_rim - 0.05), uv=(0.5, 0.9))
    rim, fringe = [], []
    for j in range(sides + 1):                      # column `sides` = column 0 again, with u = 2.0 (the seam)
        a = 2 * math.pi * (j % sides) / sides
        c, sn, u = math.cos(a), math.sin(a), 2.0 * j / sides
        dz = 0.03 if j % 2 else -0.03
        rim.append(mb.v((c * r_rim, sn * r_rim, z_rim), uv=(u, 0.82)))
        fringe.append(mb.v((c * rr, sn * rr, z_fringe + dz), uv=(u, 1.0)))
    for j in range(sides):
        mb.f((tip, rim[j], rim[j + 1]), STRAW, keep=True)
        mb.f((rim[j], fringe[j], fringe[j + 1], rim[j + 1]), STRAW, keep=True)
        mb.f((fringe[j + 1], fringe[j], hub), STRAW, keep=True)
    return mb.build(name, [straw, pole], col)


def sun_lounger(name, frame, cushion, col):
    """Beach lounger (our own design): low timber frame on four legs, a cushion and a back raised about 35
    degrees. 0.66 m wide, 1.95 m long along y; the back is at +y, the sitter looks along -y."""
    mb = MeshBuilder()
    FRAME, CUSHION = 0, 1
    for x in (-0.3, 0.3):
        for y in (-0.85, 0.55):
            box(mb, x - 0.03, x + 0.03, y - 0.03, y + 0.03, -0.05, 0.28, FRAME, skip=("bottom", "top"))
    box(mb, -0.33, 0.33, -0.95, 0.55, 0.28, 0.34, FRAME)
    box(mb, -0.3, 0.3, -0.92, 0.5, 0.34, 0.41, CUSHION, skip=("bottom",))
    ang = math.radians(35)
    back = [(0.5, 0.34), (0.5 + 0.85 * math.cos(ang), 0.34 + 0.85 * math.sin(ang)),
            (0.5 + 0.85 * math.cos(ang) - 0.07 * math.sin(ang), 0.34 + 0.85 * math.sin(ang) + 0.07 * math.cos(ang)),
            (0.5 - 0.07 * math.sin(ang), 0.34 + 0.07 * math.cos(ang))]
    extrude(mb, back, "x", -0.3, 0.3, CUSHION)
    return mb.build(name, [frame, cushion], col)


def lifeguard_tower(name, wood, white, red, flag, col):
    """Lifeguard post (our own design): four timber legs, a platform 2.1 m up with a ladder, a small white hut
    with a red band and a red mono-pitch roof, and a flag pole. Faces the water along -y."""
    mb = MeshBuilder()
    WOOD, WHITE, RED, FLAG = 0, 1, 2, 3
    for x in (-0.85, 0.85):
        for y in (-0.85, 0.85):
            box(mb, x - 0.07, x + 0.07, y - 0.07, y + 0.07, -0.3, 2.1, WOOD, skip=("bottom", "top"))
    box(mb, -1.1, 1.1, -1.25, 1.1, 2.1, 2.24, WOOD)
    box(mb, -0.8, 0.8, -0.55, 0.85, 2.24, 3.5, WHITE, skip=("bottom", "top"))
    box(mb, -0.82, 0.82, -0.57, 0.87, 3.05, 3.3, RED, skip=("bottom", "top"))
    box(mb, -0.84, 0.84, -0.42, 0.66, 2.95, 3.05, WOOD, skip=("bottom",))      # window ledge band
    roof = [(-0.75, 3.5), (1.05, 3.85), (1.05, 3.97), (-0.75, 3.62)]            # (y, z) mono-pitch, rising inland
    extrude(mb, roof, "x", -0.98, 0.98, RED)
    for x in (-0.25, 0.25):                                                      # ladder at the back
        tube(mb, [(x, 1.55, 0.0), (x, 1.15, 2.15)], [0.035, 0.035], 4, WOOD)
    for k in range(1, 5):
        t = k / 5
        box(mb, -0.25, 0.25, 1.55 - 0.4 * t - 0.025, 1.55 - 0.4 * t + 0.025, 2.15 * t - 0.02, 2.15 * t + 0.02, WOOD,
            skip=())
    box(mb, 0.92, 0.97, 0.92, 0.97, 2.24, 5.2, WOOD, skip=("bottom",))            # flag pole
    box(mb, 0.97, 1.75, 0.935, 0.955, 4.65, 5.15, FLAG, skip=())                  # flag
    return mb.build(name, [wood, white, red, flag], col)


def stone_pile(name, light, mid, dark, col):
    """Heap of loose stones on the sand (K05, K06): about 15 angular rocks of 0.25-0.7 m, about 3.5 m across and
    0.6 m high. Flat shaded, three stone tones."""
    rng = random.Random(name)
    mb = MeshBuilder()
    t = (1 + 5 ** 0.5) / 2
    ico_v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
             (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    ico_f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
             (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
             (8, 6, 7), (9, 8, 1)]
    rocks = 16
    for k in range(rocks):
        r = 1.6 * math.sqrt(rng.random())
        a = rng.uniform(0, 2 * math.pi)
        cx, cy = math.cos(a) * r, math.sin(a) * r * 0.75
        size = rng.uniform(0.2, 0.42) * (1.25 - 0.35 * r / 1.6)
        cz = size * 0.35 + (0.25 if r < 0.8 and rng.random() < 0.5 else 0.0)
        sx, sy, sz = size * rng.uniform(0.9, 1.4), size * rng.uniform(0.8, 1.2), size * rng.uniform(0.55, 0.8)
        yaw = rng.uniform(0, math.pi)
        cyaw, syaw = math.cos(yaw), math.sin(yaw)
        mat = rng.choice((0, 1, 1, 2))
        idx = []
        for vx, vy, vz in ico_v:
            j = rng.uniform(0.85, 1.12)
            x, y, z = vx / 1.902 * sx * j, vy / 1.902 * sy * j, vz / 1.902 * sz * j
            idx.append(mb.v((cx + x * cyaw - y * syaw, cy + x * syaw + y * cyaw, cz + z)))
        for f in ico_f:
            mb.f(tuple(idx[i] for i in f), mat)
    return mb.build(name, [light, mid, dark], col)


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
    steel = m("steel", roughness=0.3, metallic=0.85)
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
        "Prop_Parasol": parasol("Prop_Parasol", m("parasol_canvas", "#F2E3C8", roughness=0.9),
                                m("bark_light", roughness=0.8), m("concrete_light", roughness=0.9), col),
        "Prop_BistroTable": bistro_table("Prop_BistroTable", m("cafe_table_top", "#E9DFCB", roughness=0.7),
                                         m("cafe_metal", "#3E3B3A", roughness=0.5, metallic=0.5), col),
        "Prop_BistroChair": bistro_chair("Prop_BistroChair", m("cafe_wood", "#B98A5B", roughness=0.75),
                                         m("cafe_metal", "#3E3B3A", roughness=0.5, metallic=0.5), col),
        "Prop_TroughBloom": trough_bloom("Prop_TroughBloom", m("event_violet", "#5C377D", roughness=0.85),
                                         m("planter_cream", "#D9CFBB", roughness=0.85),
                                         m("soil", "#5E4A3A", roughness=1.0),
                                         m("bloom_leaf", "#536E49", roughness=0.9),
                                         m("bloom", "#80679B", roughness=0.85),
                                         m("bloom_light", "#BCA0D8", roughness=0.85), col),
        "Prop_PlanterRound": planter_round("Prop_PlanterRound", m("planter_cream", "#D9CFBB", roughness=0.85),
                                           m("soil", "#5E4A3A", roughness=1.0),
                                           m("olive_crown_b", "#5F7F4F", roughness=0.9),
                                           m("olive_crown_top_b", "#9DB06E", roughness=0.9), col),
        "Prop_StudyTable": study_table("Prop_StudyTable", m("study_wood", "#B18C67", roughness=0.7),
                                       m("cafe_metal", "#3E3B3A", roughness=0.5, metallic=0.5), col),
        "Prop_GrandPiano": grand_piano("Prop_GrandPiano", m("piano_gloss", "#1D1D1F", roughness=0.2),
                                       m("piano_keys", "#E8E2D2", roughness=0.5), col),
        # beach pass V10b
        "Prop_ParasolThatch": parasol_thatch("Prop_ParasolThatch", _textured(m("thatch", "#B39461", roughness=0.95),
                                                                              "thatch"),
                                             m("bark_light", roughness=0.8), col),
        "Prop_SunLounger": sun_lounger("Prop_SunLounger", m("bench_wood", "#A27E58", roughness=0.8),
                                       m("planter_cream", "#D9CFBB", roughness=0.85), col),
        "Prop_LifeguardTower": lifeguard_tower("Prop_LifeguardTower", m("bench_wood", "#A27E58", roughness=0.8),
                                               m("planter_cream", "#D9CFBB", roughness=0.85),
                                               m("B_roof_tiles", "#C8664A", 0.85),
                                               m("lifeguard_flag", "#E8C547", roughness=0.8), col),
        "Prop_StonePile": stone_pile("Prop_StonePile", m("concrete_light", roughness=0.9),
                                     m("fort_top", "#A99A7B", 0.95), m("fort_rock", "#8E7D59", 0.95), col),
    }
    for name, obj in protos.items():
        tris = triangles(obj)
        if tris > TRI_BUDGET[name]:
            raise ValueError(f"{name}: {tris} triangles, budget {TRI_BUDGET[name]}")
    return protos


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def _textured(mat, base):
    """Albedo texture on a prop material (make_textures.py), without touching env_materials.TEXTURED."""
    import env_materials
    if not mat.get("v10b_textured") and env_materials.upgrade(mat, base, 0.95):
        mat["v10b_textured"] = 1
    return mat


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
        if _beach_side(x, y):                     # V10b: street props stay on the promenade, off the sand
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


_BEACH_BOX = (-300.0, -235.0, 7.0, -46.0)     # the promenade kerb lines plus 40 m (env_beachfront.on_beach_side)


def _beach_side(x, y):
    if not (_BEACH_BOX[0] < x < _BEACH_BOX[2] and _BEACH_BOX[1] < y < _BEACH_BOX[3]):
        return False
    import env_beachfront as B
    return B.on_beach_side(x, y)


def _beachfront(site, rng, free, fits):
    """V10b beach dressing. Groups are checked as a whole against what already stands (free), then added.
    Restaurant and pavilion terraces: a round table with three chairs (some with a parasol), on the terrace
    height from env_beachfront; tagged site = "terrace" (they stand within 2 m of the building on purpose).
    Main beach: rows of straw parasols on the sand west of A Pastorinha (ortho_west), loungers under the two
    rows nearest the promenade, a lifeguard post nearer the water. Cove: three heaps of stones (K05, K06)."""
    import env_beachfront as B
    out, stats = [], {"terrace_tables": 0, "pavilion_tables": 0, "straw_parasols": 0, "loungers": 0,
                      "lifeguard": 0, "stone_piles": 0}

    def table_group(x, y, parasol):
        items = [("Prop_BistroTable", x, y, rng.uniform(-math.pi, math.pi))]
        if parasol:
            items.append(("Prop_Parasol", x, y, rng.uniform(-math.pi, math.pi)))
        a0 = rng.uniform(0, 2 * math.pi)
        for q in range(3):
            a = a0 + 2 * math.pi * q / 3 + rng.uniform(-0.15, 0.15)
            px, py = x + math.cos(a) * 0.64, y + math.sin(a) * 0.64
            items.append(("Prop_BistroChair", px, py, _facing(x - px, y - py) + rng.uniform(-0.25, 0.25)))
        return items

    def add(items, z_of, tag=None):
        """The table must be free; a chair that would touch a plant or another prop is left out."""
        if not free(items[0][1], items[0][2], items[0][0]):
            return False
        items = [it for it in items if it[0] in ("Prop_BistroTable", "Prop_Parasol") or free(it[1], it[2], it[0])]
        for pr, x, y, rot in items:
            e = _entry(pr, x, y, z_of(x, y), rot)
            if tag:
                e["site"] = tag
            out.append(e)
        return True

    def blocked(x, y, r):
        return any(math.hypot(x - q["x"], y - q["y"]) < r + FOOTPRINT[q["proto"]] + 0.3 for q in out)

    for bid in B.RESTAURANTS:                                         # restaurant terraces
        for kind, x, y, z, _rot in B.deck_spots(bid):
            if kind in ("table", "parasol") and not blocked(x, y, 1.0):
                if add(table_group(x, y, kind == "parasol"), lambda _x, _y, z=z: z, "terrace"):
                    stats["terrace_tables"] += 1
    (cx, cy), (ux, uy), off, ats = PAVILION_TERRACE                   # pavilion terrace (ortho_south, K02)
    vx, vy = -uy, ux
    for k, a in enumerate(ats):
        x, y = cx + ux * a - vx * off, cy + uy * a - vy * off
        group = table_group(x, y, True)
        if all(site.ok(px, py, clear=PARASOL_CLEAR if pr == "Prop_Parasol" else E.KEEP_CLEAR,
                       footprint=FOOTPRINT[pr]) for pr, px, py, _r in group) and add(group, _prop_z):
            stats["pavilion_tables"] += 1

    def on_sand(x, y, r):
        return (E.ground_class(x, y) == "sand" and E.ground_z(x, y) > 1.2 and site.building_clearance(x, y) > 3.0
                and E.distance_to_route(x, y) > 10.0 and not blocked(x, y, r))

    g = BEACH_GRID                                                    # straw parasols in rows
    (ox, oy), (dx, dy) = g["origin"], g["along"]
    ex, ey = -dy, dx
    if ex * BEACH_SEA[0] + ey * BEACH_SEA[1] < 0:
        ex, ey = -ex, -ey
    for r in range(g["rows"]):
        for k in range(g["per_row"]):
            x = ox + dx * k * g["step"] + ex * r * g["row_step"] + rng.uniform(-0.15, 0.15)
            y = oy + dy * k * g["step"] + ey * r * g["row_step"] + rng.uniform(-0.15, 0.15)
            if not on_sand(x, y, FOOTPRINT["Prop_ParasolThatch"]):
                continue
            items = [("Prop_ParasolThatch", x, y, rng.uniform(-math.pi, math.pi))]
            if r < g["loungers_rows"]:
                for sd in (-1, 1):
                    lx, ly = x + ex * 0.25 + dx * 0.45 * sd, y + ey * 0.25 + dy * 0.45 * sd
                    items.append(("Prop_SunLounger", lx, ly, _facing(ex, ey) + rng.uniform(-0.08, 0.08)))
            if all(free(px, py, pr) for pr, px, py, _r in items[:1]):
                for pr, px, py, rot in items:
                    out.append(_entry(pr, px, py, E.ground_z(px, py), rot))
                stats["straw_parasols"] += 1
                stats["loungers"] += len(items) - 1
    for rad in [0.0] + [1.0 * k for k in range(1, 13)]:               # lifeguard post
        spot = next(((LIFEGUARD[0] + math.cos(a) * rad, LIFEGUARD[1] + math.sin(a) * rad)
                     for a in (2 * math.pi * j / 12 for j in range(12))
                     if on_sand(LIFEGUARD[0] + math.cos(a) * rad, LIFEGUARD[1] + math.sin(a) * rad, 1.6)
                     and free(LIFEGUARD[0] + math.cos(a) * rad, LIFEGUARD[1] + math.sin(a) * rad,
                              "Prop_LifeguardTower")), None)
        if spot:
            out.append(_entry("Prop_LifeguardTower", spot[0], spot[1], E.ground_z(*spot) - 0.05, _facing(*BEACH_SEA)))
            stats["lifeguard"] = 1
            break
    for x, y, scale in STONE_PILES:                                   # stone heaps on the cove sand
        if B.on_beach_side(x, y) and free(x, y, "Prop_StonePile") and not blocked(x, y, 2.0):
            out.append(_entry("Prop_StonePile", x, y, E.ground_z(x, y) - 0.08, rng.uniform(-math.pi, math.pi), scale))
            stats["stone_piles"] += 1
    REPORT["beachfront"] = stats
    return out


def place(seed=SEED, write=True, avoid=()):
    """Computes all prop placements and writes out/placements_props.json.
    avoid: vegetation placements (env_vegetation.place()) to keep clear of, so no prop stands in a tree.

    Campus lamps every ~30 m, alternating sides, 6.5 m from the centreline on the upper and lower plaza and on
    the lawns near campus. Road lamps every ~35 m on the coastal road and the promenade, on the sea side.
    Railings 4.6 m out: both sides of the terrace deck (gap of 12 m at the jump), both sides of the north
    tunnel ramp (the ramp points right after the tunnel), the sea side of the promenade (route ground_z < 4). Benches every ~25 m on the
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
            if not spot:                                       # else the land side (V10b: not on the beach)
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
    ramp, after_tunnel = [], False
    for i in list(range(n)) + list(range(n)):
        zone = pts[i % n].get("zone")
        if zone == "tunnel":
            after_tunnel = True
        elif zone == "ramp" and after_tunnel:
            if i % n not in ramp and not pts[i % n]["building"]:
                ramp.append(i % n)
        elif after_tunnel and ramp:
            break
        else:
            after_tunnel = after_tunnel and zone == "tunnel"
    for run in _runs(ramp):
        for side in (1, -1):
            placed += _railing_run(site, run, side, lambda p: p["track_z"], f"ramp {side:+d}")
    seawall = [i for i, p in enumerate(pts) if p["ground_z"] < SEAWALL_GROUND_Z and p.get("zone") not in NO_PROP_ZONES]
    for run in _runs(seawall) if SEAWALL_RAILING else ():
        placed += _railing_run(site, run, _sea_side(site, run), lambda p: p["track_z"], "promenade sea side")

    # beachfront (V10b): restaurant terraces, pavilion terrace, straw parasols, lifeguard post, stone heaps -----
    placed += _beachfront(site, random.Random(seed + 10), free, fits)
    for _ in range(3):
        rng.uniform(-math.pi, math.pi)    # the three beach-bar parasols drew these: later props keep their old values

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

    # cafe groups beside the track (design reference: busy terraces) -------------------------------------------
    def group_fits(items, clear=E.KEEP_CLEAR):
        """Every member passes the site rules and keeps clear of earlier props and the plants."""
        return all(site.ok(x, y, clear=clear, footprint=FOOTPRINT[pr]) and free(x, y, pr) for pr, x, y, _r in items)

    def cafe_layout(gx, gy, tx, ty, nx, ny, parasols=True, trough=True):
        """One cafe group around (gx, gy): two round tables with three chairs each (and a parasol through each
        table outdoors), a flower trough between them and the road, a round planter at one end.
        u along the route (tx, ty), v away from it (nx, ny)."""
        def at(u, v):
            return gx + tx * u + nx * v, gy + ty * u + ny * v
        items = []
        for u, v in ((-1.15, 0.0), (1.15, 0.55)):
            cx, cy = at(u, v)
            items.append(("Prop_BistroTable", cx, cy, rng.uniform(-math.pi, math.pi)))
            if parasols:
                items.append(("Prop_Parasol", cx, cy, rng.uniform(-math.pi, math.pi)))
            a0 = rng.uniform(0, 2 * math.pi)
            for q in range(3):
                a = a0 + 2 * math.pi * q / 3 + rng.uniform(-0.15, 0.15)
                px, py = cx + math.cos(a) * 0.64, cy + math.sin(a) * 0.64
                items.append(("Prop_BistroChair", px, py, _facing(cx - px, cy - py) + rng.uniform(-0.25, 0.25)))
        if trough:
            rx, ry = at(0.0, -1.95)
            items.append(("Prop_TroughBloom", rx, ry, math.atan2(ty, tx)))
        px, py = at(2.9, 0.2)
        items.append(("Prop_PlanterRound", px, py, rng.uniform(-math.pi, math.pi)))
        return items

    cafes = []
    for zone, limit in CAFE_ZONES.items():
        idx = [i for i, p in enumerate(pts) if p.get("zone") == zone and not p["building"]]
        side, made = 1, 0
        for i in _every(site, idx, CAFE_STEP):
            if made >= limit:
                break
            land = -_sea_side(site, range(i - 10, i + 11)) if zone == "promenade" else None
            spot = None
            for sd in ((land,) if land else (side, -side)):
                for di in (0, 2, -2, 4, -4, 6, -6):
                    for off in CAFE_OFFSETS:
                        p, (tx, ty), (nx, ny) = site.frame(i + di)
                        gx, gy = p["x"] + nx * off * sd, p["y"] + ny * off * sd
                        items = cafe_layout(gx, gy, tx, ty, nx * sd, ny * sd)
                        if group_fits(items):
                            spot = (sd, items, (i + di) % n)
                            break
                    if spot:
                        break
                if spot:
                    break
            if not spot:
                continue
            sd, items, j = spot
            for pr, x, y, rot in items:
                placed.append(_entry(pr, x, y, _prop_z(x, y), rot))
            cafes.append((zone, round(pts[j]["s"])))
            made += 1
            side = -sd
    REPORT["cafe_groups"] = cafes

    # pairs of flower troughs on the straights of the plazas and the promenade ---------------------------------
    def straight(i, span=4, limit=0.25):
        _p, (ax, ay), _n = site.frame(i - span)
        _p, (bx, by), _n = site.frame(i + span)
        return abs(_wrap(math.atan2(by, bx) - math.atan2(ay, ax))) < limit

    troughs, side = 0, 1
    for zone_run in _runs([i for i, p in enumerate(pts) if p.get("zone") in TROUGH_ZONES and not p["building"]]):
        for i in _every(site, zone_run, TROUGH_STEP):
            if not straight(i):
                continue
            p, (tx, ty), (nx, ny) = site.frame(i)
            for sd in (side, -side):
                pair = [("Prop_TroughBloom", p["x"] + nx * TROUGH_OFFSET * sd + tx * u,
                         p["y"] + ny * TROUGH_OFFSET * sd + ty * u, math.atan2(ty, tx)) for u in (-0.95, 0.95)]
                if group_fits(pair):
                    for pr, x, y, rot in pair:
                        placed.append(_entry(pr, x, y, _prop_z(x, y), rot))
                    troughs += 2
                    side = -sd
                    break
    REPORT["trough_pairs"] = troughs // 2

    # the atrium: cafe tables, study tables and the piano in the bays of the wide hall (env_interior) ----------
    import env_interior as I
    atrium = {"piano": 0, "cafe": 0, "study": 0}
    for piece in I.passages():
        if piece["building"]["name"] not in I.WIDE_HALLS:
            continue
        P, W = piece["points"], piece["widths"]
        inside = [k for k, q in enumerate(P) if q["building"] == piece["building"]["name"]]
        if len(inside) < 10:
            continue

        def kframe(k):
            a, b = P[max(k - 1, 0)], P[min(k + 1, len(P) - 1)]
            d = math.hypot(b["x"] - a["x"], b["y"] - a["y"]) or 1.0
            return ((b["x"] - a["x"]) / d, (b["y"] - a["y"]) / d), (-(b["y"] - a["y"]) / d, (b["x"] - a["x"]) / d)

        def in_hall(x, y, proto):
            k = min(inside, key=lambda q: (P[q]["x"] - x) ** 2 + (P[q]["y"] - y) ** 2)
            _t, (nx, ny) = kframe(k)
            v = (x - P[k]["x"]) * nx + (y - P[k]["y"]) * ny
            half = W[k][0] if v > 0 else W[k][1]
            r = FOOTPRINT[proto] * 0.5
            return (HALL_ROAD_BAND + r <= abs(v) <= half - HALL_WALL_BAND - r
                    and E.distance_to_route(x, y) >= HALL_ROAD_BAND + r and free(x, y, proto))

        def band(k, side, span=2):
            ks = range(max(k - span, 0), min(k + span, len(P) - 1) + 1)
            return min(W[q][0 if side == 1 else 1] for q in ks) - HALL_WALL_BAND - HALL_ROAD_BAND

        bays = [(k, side) for k in inside[2:-2:HALL_STEP] for side in (1, -1) if band(k, side) >= 2.4]
        order = ["piano", "cafe", "study"]
        bays.sort(key=lambda b: -band(*b))
        piano_bay = bays[0] if bays and band(*bays[0]) >= 3.0 and not atrium["piano"] else None   # one piano
        plan = ([(piano_bay, "piano")] if piano_bay else []) + [
            (b, order[1 + n_ % 2]) for n_, b in enumerate(sorted((b for b in bays if b != piano_bay), key=lambda b: b[0]))]
        for (k, side), kind in plan:
            p = P[k]
            (tx, ty), (nx, ny) = kframe(k)
            nx, ny = nx * side, ny * side
            vc = HALL_ROAD_BAND + band(k, side) / 2 + 0.2
            gx, gy = p["x"] + nx * vc, p["y"] + ny * vc
            if kind == "piano":
                rot = _facing(-nx, -ny) + math.radians(30) * side
                items = [("Prop_GrandPiano", gx, gy, rot)]
                bx, by = gx - nx * 1.2 + tx * 0.2, gy - ny * 1.2 + ty * 0.2       # the player's seat
                items.append(("Prop_BistroChair", bx, by, _facing(gx - bx, gy - by)))
                items.append(("Prop_PlanterRound", gx + tx * 2.4 + nx * 0.6, gy + ty * 2.4 + ny * 0.6, 0.0))
            elif kind == "cafe":
                items = cafe_layout(gx, gy, tx, ty, nx, ny, parasols=False, trough=False)
            else:
                items = []
                for u in (-1.0, 1.0):
                    items.append(("Prop_StudyTable", gx + tx * u, gy + ty * u, math.atan2(ty, tx)))
                for u in (-1.45, -0.55, 0.55, 1.45):
                    for sv in (1, -1):
                        cx, cy = gx + tx * u + nx * 0.68 * sv, gy + ty * u + ny * 0.68 * sv
                        items.append(("Prop_BistroChair", cx, cy, _facing(-nx * sv, -ny * sv) + rng.uniform(-0.2, 0.2)))
            ok = [it for it in items if in_hall(it[1], it[2], it[0])]
            core = {"piano": "Prop_GrandPiano", "cafe": "Prop_BistroTable", "study": "Prop_StudyTable"}[kind]
            if sum(1 for it in ok if it[0] == core) < sum(1 for it in items if it[0] == core):
                continue                                 # the main pieces must fit, else skip the whole bay
            for pr, x, y, rot in ok:
                placed.append(_entry(pr, x, y, p["track_z"], rot))
            atrium[kind] += 1
            # a flower trough between the bay and the road
            rx, ry = p["x"] + nx * (HALL_ROAD_BAND + 0.55), p["y"] + ny * (HALL_ROAD_BAND + 0.55)
            if in_hall(rx, ry, "Prop_TroughBloom"):
                placed.append(_entry("Prop_TroughBloom", rx, ry, p["track_z"], math.atan2(ty, tx)))
    REPORT["atrium"] = atrium

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
