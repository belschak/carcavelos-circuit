"""The upper plaza, the terracotta stairs and the sunken courtyard (V10i, 2026-11-11), from plaza_plan.py.

Driving surfaces (collection Track_Surface, exported with the track, colliders in Unity):
  Track_PlazaDeck_NoCollide          the Floor-1 paving, one ochre surface from facade to facade, at road height
                                     (the road on the plaza IS this deck: env_track builds no road on zone "stairs" /
                                     "plaza deck"); five flush frosted-glass skylights and the library's floor are
                                     cut into the same mesh (no second surface anywhere)
  Track_PlazaLow                     Floor-0 paving: the courtyard floor, the voids beside the stairs, the landing
                                     of the stair slot and the underpass
  Track_PlazaStairs_NoCollide        what you see of the stairs: steps on the east half, seating tiers on the west
                                     half, and the steps in the slot. No collider (CampusWorldBuilder: "NoCollide")
  Track_PlazaDrive_ColliderOnly      what the kart drives on, ONE welded mesh: the deck, the stairs' smooth profile
                                     3.5 cm above the step noses with a rounded ripple of plaza_plan.JUDDER_M per
                                     step, and a strip at each end that passes through the road's surface (see
                                     build_drive: a kart must never roll over the border of a collider). Not drawn
                                     (CampusWorldBuilder: "ColliderOnly"; hidden from renders here, qa_geometry sees it)
  Track_PlazaSlotDrive_ColliderOnly  the smooth slope over the steps in the stair slot
World (collection World_Plaza, CampusWorld.fbx):
  World_PlazaWalls                   retaining walls round the courtyard and under the deck's south edge, the stairs'
                                     cheeks, the slot's walls, the underpass, the deck's edge to the lawn, planters,
                                     the planted barrier across the gap to Horizon Hall
  World_PlazaFences                  kerb, posts and rails (to look at)
  World_PlazaGuard_ColliderOnly      an upright strip behind every fence: no kart gets over or through a railing
"""
import math

import bmesh
import bpy
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt, tessellate_polygon

import env_common as E
import plaza_plan as P

TILE_OCHRE = 3.0            # m per texture tile (slabs 3 x 3 m)
TILE_GLASS = 2.5            # two 1.25 x 2.5 m panels
STEP_UNDER = 0.035          # m: the step noses lie this far under the driving surface
GUARD_H = 1.5


# ---------------------------------------------------------------------------------------------------------------
# Materials
# ---------------------------------------------------------------------------------------------------------------
def _textured(name, colour, base, roughness):
    import env_materials
    env_materials.MATTE.add("W_" + name)
    mat = E.material(name, colour, roughness)
    if not mat.get("v10i_textured") and env_materials.upgrade(mat, base, roughness):
        mat["v10i_textured"] = 1
    return mat


def mat(key):
    import env_track
    if key == "ochre":
        return _textured("T_plaza_ochre", "#E3BE86", "plaza_ochre", 0.85)
    if key == "glass":
        return _textured("T_skylight", "#9DBAC0", "skylight_glass", 0.35)
    if key == "terracotta":             # own material (the track's W_T_terracotta carries a roughness map)
        return _textured("plaza_terracotta", "#C8664A", "terracotta_steps", 0.85)
    if key == "floor":
        return env_track.surface_material("concrete_polished")
    if key == "white":
        return E.material("B_wall_white", "#EAEAEC", 0.85)
    if key == "steel":
        return E.material("plaza_rail", "#4A4E53", roughness=0.45, metallic=0.6)
    if key == "dark":
        return E.material("plaza_dark", "#3B3E42", 0.7)
    if key == "hedge":
        import env_streets
        return env_streets.mat("hedge")
    raise KeyError(key)


class Mesh:
    """Small mesh builder: named material slots, quads/tris with UVs."""

    def __init__(self, name, keys):
        self.name, self.keys = name, list(keys)
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def face(self, pts, key, uvs=None, tile=3.0):
        vs = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(vs)
        f.material_index = self.keys.index(key)
        for k, loop in enumerate(f.loops):
            if uvs:
                loop[self.uv].uv = uvs[k]
            else:
                co = loop.vert.co
                loop[self.uv].uv = (co.x / tile, co.y / tile)
        return f

    def wall(self, a, b, za, zb, bottom, facing, key="white", bottom_b=None):
        """Upright quad over the segment a-b, top heights za / zb, facing the horizontal direction `facing`."""
        bb = bottom if bottom_b is None else bottom_b
        if max(za - bottom, zb - bb) < 0.01:
            return
        pts = [(a[0], a[1], bottom), (b[0], b[1], bb), (b[0], b[1], zb), (a[0], a[1], za)]
        ex, ey = b[0] - a[0], b[1] - a[1]
        if (ey * facing[0] - ex * facing[1]) < 0:           # normal of this winding = (ey, -ex): flip if it looks away
            pts.reverse()
        d = math.hypot(ex, ey)
        s0 = (a[0] * ex + a[1] * ey) / (d or 1.0)
        uv = {(a[0], a[1]): s0, (b[0], b[1]): s0 + d}
        self.face(pts, key, [(uv[(p[0], p[1])] / 3.0, p[2] / 3.0) for p in pts])

    def prism(self, ring, z0, z1, key, top_key=None, bottom=False):
        """Upright prism over a convex counter-clockwise ring (z1 may be a function of (x, y))."""
        top = (lambda x, y: z1) if not callable(z1) else z1
        n = len(ring)
        for k in range(n):
            a, b = ring[k], ring[(k + 1) % n]
            self.wall(a, b, top(*a), top(*b), z0, (b[1] - a[1], -(b[0] - a[0])), key)
        self.face([(x, y, top(x, y)) for x, y in ring], top_key or key)
        if bottom:
            self.face([(x, y, z0) for x, y in reversed(ring)], key)

    def finish(self, col, hide=False):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(mesh)
        self.bm.free()
        for key in self.keys:
            mesh.materials.append(mat(key))
        obj = E.new_object(self.name, mesh, col)
        if hide:
            obj.hide_render = True
            obj["collider_only"] = 1
        return obj


def strip(a, b, half):
    """Counter-clockwise rectangle `half` to each side of the segment a-b."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    d = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / d * half, dx / d * half
    return [(a[0] - nx, a[1] - ny), (b[0] - nx, b[1] - ny), (b[0] + nx, b[1] + ny), (a[0] + nx, a[1] + ny)]


def ccw(ring):
    area = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(ring, ring[1:] + ring[:1]))
    return list(ring) if area > 0 else list(reversed(ring))


# ---------------------------------------------------------------------------------------------------------------
# Hooks for the terrain and the rest of the generator
# ---------------------------------------------------------------------------------------------------------------
def install():
    """From now on E.ground_z returns the plaza's own surfaces where it has them (props, pillars, tufts)."""
    prev = E._surface["fn"]

    def surface(x, y):
        z = P.surface_z(x, y)
        if z is not None:
            return z
        return prev(x, y) if prev else E.coarse_ground_z(x, y)
    E.set_surface(surface)


def shape_heights(heights):
    """env_terrain.apply: the 5 m nodes under and round the plaza (plaza_plan.terrain_z)."""
    xs = [p[0] for p in P.DECK] + [p[0] for c in P.LOWS for p in c]
    ys = [p[1] for p in P.DECK] + [p[1] for c in P.LOWS for p in c]
    reach = P.EDGE_BLEND + 6.0
    changed = 0
    for j in range(E.ROWS):
        y = E.Y0 + j * E.STEP
        if not min(ys) - reach <= y <= max(ys) + reach:
            continue
        for i in range(E.COLS):
            x = E.X0 + i * E.STEP
            if not min(xs) - reach <= x <= max(xs) + reach:
                continue
            z = P.terrain_z(x, y, heights[j][i])
            if z != heights[j][i]:
                heights[j][i] = z
                changed += 1
    print(f"  plaza: {changed} terrain nodes set under and round the upper plaza")


TONGUE_COLS = (4.9, 4.0, -4.0, -4.9)     # m left of the centreline: the strip's columns in the atrium


_TONGUE = {}


def tongue_far(_ring=None):
    """The four points across the tongue's far end (left to right), TONGUE_COLS from the centreline."""
    a, b = _TONGUE["far"]
    return [(a[0] + (b[0] - a[0]) * (4.9 - o) / 9.8, a[1] + (b[1] - a[1]) * (4.9 - o) / 9.8) for o in TONGUE_COLS]


def entry_rise(x, y, ring):
    """How far the collider lies over the deck at a point of the tongue: 0 at its start, ENTRY_SEAM at its far end,
    by the distance along the race line (first tests raised only the far end: where a vertex of the building's outline
    lay 0.7 m before it the ramp was 7 % steep and every kart hopped 20 cm)."""
    if not (P.inside(x, y, ring) or P.poly_distance(x, y, ring) < 0.02):
        return 0.0
    pts = E.route_points()
    i0, i1 = _TONGUE["span"]
    best = None
    for i in range(i0, i1):
        a, b = pts[i], pts[i + 1]
        dx, dy = b["x"] - a["x"], b["y"] - a["y"]
        t = min(1.0, max(0.0, ((x - a["x"]) * dx + (y - a["y"]) * dy) / (dx * dx + dy * dy)))
        d = math.hypot(x - a["x"] - dx * t, y - a["y"] - dy * t)
        if best is None or d < best[0]:
            best = (d, (i + t - i0) / (i1 - i0))
    t = best[1]
    return ENTRY_SEAM * t * t * (3 - 2 * t)


def tongue():
    """The deck's piece along the road into the atrium: from 10 m before the facade (the road cuts through the corner
    of the building there, whose floor lies 6 cm lower: without the tongue a kart dropped onto it and hit the deck's
    edge again) to the first road point inside the building. 4.9 m to each side of the centreline."""
    pts = E.route_points()
    n = len(pts)
    last = [i for i, p in enumerate(pts) if P.owns_route_point(p)][-1]
    left, right = [], []
    for i in range(last - 5, last + 2):
        a, b, p = pts[(i - 1) % n], pts[(i + 1) % n], pts[i % n]
        d = math.hypot(b["x"] - a["x"], b["y"] - a["y"])
        nx, ny = -(b["y"] - a["y"]) / d, (b["x"] - a["x"]) / d
        left.append((p["x"] + nx * 4.9, p["y"] + ny * 4.9))
        right.append((p["x"] - nx * 4.9, p["y"] - ny * 4.9))
    _TONGUE["far"] = (left[-1], right[-1])
    _TONGUE["span"] = (last - 5, last + 1)
    return left + right[::-1]


# ---------------------------------------------------------------------------------------------------------------
# The deck
# ---------------------------------------------------------------------------------------------------------------
def build_deck(col):
    tng = tongue()
    lip = [P.stair_point(P.STAIR_LEN + s, u) for s in (1.0, 2.0, 3.0, P.STAIR_REACH[1] - P.STAIR_LEN) for u in (-P.STAIR_HALF, P.STAIR_HALF)]
    rings = [P.DECK, tng, P.STAIR_DOWN, P.LIB_GROUND, P.KICK_ON_DECK] + P.DECK_ISLANDS + list(P.SKYLIGHTS.values())
    verts, edges = [], []
    for ring in rings:
        base = len(verts)
        verts += [Vector(p) for p in ring]
        edges += [(base + k, base + (k + 1) % len(ring)) for k in range(len(ring))]
    for k in range(0, len(lip), 2):                             # rows across the end of the stairs' crest curve
        base = len(verts)
        verts += [Vector(lip[k]), Vector(lip[k + 1])]
        edges.append((base, base + 1))
    for u in (-P.STAIR_HALF, P.STAIR_HALF):
        base = len(verts)
        verts += [Vector(P.stair_point(P.STAIR_LEN, u)), Vector(P.stair_point(P.STAIR_REACH[1], u))]
        edges.append((base, base + 1))
    verts += [Vector(P.stair_point(P.STAIR_LEN, u)) for u in STAIR_COLS[1:-1]]     # the stairs' columns end here
    verts += [Vector(q) for q in tongue_far(tng)[1:3]]                             # ... and the atrium strip's columns
    verts += [Vector(P.climb_point(P.CLIMB_LEN, u)) for u in CLIMB_COLS[1:-1]]     # V14b: the climb's columns end here
    verts += [Vector(P.climb_point(P.KICK_START_S, u)) for u in KICK_COLS[1:-1]]   # ... and the kicker's start here
    out_v, _e, out_f, *_ = delaunay_2d_cdt(verts, edges, [], 0, 1e-4)
    m = Mesh("Track_PlazaDeck_NoCollide", ["ochre", "glass", "floor"])
    tris = []
    count = {"ochre": 0, "glass": 0, "floor": 0}
    for f in out_f:
        tri = [out_v[i] for i in f]
        cx, cy = sum(v.x for v in tri) / 3, sum(v.y for v in tri) / 3
        if P.inside(cx, cy, P.STAIR_DOWN) or not (P.inside(cx, cy, P.DECK) or P.inside(cx, cy, tng)
                                                   or any(P.inside(cx, cy, i) for i in P.DECK_ISLANDS)):
            continue
        if P.inside(cx, cy, P.KICK_ON_DECK):                    # V14b: the kicker stands here (own surface, welded on)
            continue
        key = "ochre"
        if any(P.inside(cx, cy, r) for r in P.SKYLIGHTS.values()):
            key = "glass"
        elif P.inside(cx, cy, P.LIB_GROUND):
            key = "floor"
        pts = [(v.x, v.y, P.deck_z(v.x, v.y)) for v in tri]
        if (pts[1][0] - pts[0][0]) * (pts[2][1] - pts[0][1]) - (pts[1][1] - pts[0][1]) * (pts[2][0] - pts[0][0]) < 0:
            pts.reverse()
        m.face(pts, key, tile={"ochre": TILE_OCHRE, "glass": TILE_GLASS, "floor": 2.0}[key])
        tris.append(pts)
        count[key] += 1
    m.finish(col)
    return count, tris, tng


STAIR_COLS = [P.STAIR_HALF * k / 4 for k in range(-4, 5)]       # 9 columns, 1.19 m apart: no sliver triangles
CLIMB_COLS = [P.CLIMB_HALF * k / 3 for k in range(-3, 4)]       # V14b: 7 columns, 1 m apart, on the courtyard climb
KICK_COLS = [P.KICK_U[0] + (P.KICK_U[1] - P.KICK_U[0]) * k / 4 for k in range(5)]   # 5 columns, 1.1 m apart
KICK_ROWS = int(round(P.KICK_LEN / 0.5))
ENTRY_SEAM = P.SEAM         # m: the collider's tongue ends this far over the road's border in the atrium; it rises to that
                            # evenly over the tongue's 12 m (entry_rise) and then sinks through the road as a strip
ENTRY_STRIP = True
DRIVE_LIFT = 0.002          # m: the collider lies this far over what is drawn (which of two level surfaces a ray meets
                            # first is then defined)


def build_drive(col, deck_tris, tng):
    """The one welded collider of the stairs and the deck.

    Lesson from the first bot races: where a kart rolled from one mesh collider onto the next (road -> stairs,
    stairs -> deck, deck -> road in the atrium), level or with a small drop, it was kicked up to 1.5 m into the air
    (the ball meets the border edge of a mesh). So this is one mesh with shared vertices, and at both ends it does
    not stop at the road's end: it runs on along the road as a strip that passes through the road's surface at a flat
    angle (3 cm per 2 m, plaza_plan.SEAM_STEPS). The kart changes from one collider to the other where both are plain
    faces, and every border edge lies at least 5 cm under the surface the kart is on at that moment (PhysX makes
    contacts within 2 cm: a border 1.5 cm under the kart still kicked it). Thin sliver triangles kicked too: the
    stairs' rows are cut into 1.2 m columns."""
    pts = E.route_points()
    n = len(pts)
    bm = bmesh.new()

    def face(ring):
        f = bm.faces.new([bm.verts.new(p) for p in ring])
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    far = tongue_far(tng)                                       # 4 points across the tongue's far end
    for tri in deck_tris:                                       # the deck; the tongue's far end 5 cm over the road
        face([(x, y, z + DRIVE_LIFT + entry_rise(x, y, tng)) for x, y, z in tri])
    length, treads = P.STAIR_LEN, P.STAIR_RISERS - 1
    rows = treads * 4

    def zdrive(s):                                              # 5 cm over the road's end at the foot, easing out
        return P.stair_profile(s) + P.LIFT + P.judder(s) + DRIVE_LIFT + P.SEAM * max(0.0, 1 - s / 4.0) ** 2
    grid = [[(*P.stair_point(length * k / rows, u), zdrive(length * k / rows)) for u in STAIR_COLS] for k in range(rows + 1)]
    grid[-1] = [(x, y, P.deck_z(x, y) + DRIVE_LIFT) for x, y, _z in grid[-1]]              # the deck's own edge

    def frame(i):
        a, b, p = pts[(i - 1) % n], pts[(i + 1) % n], pts[i % n]
        d = math.hypot(b["x"] - a["x"], b["y"] - a["y"])
        return p, (-(b["y"] - a["y"]) / d, (b["x"] - a["x"]) / d)
    foot = next(i for i, p in enumerate(pts) if p["zone"] == "stairs")                 # the route point at the first riser
    lead = []                                                   # strip under / through the road before the stairs
    for back, rel in zip((4, 3, 2, 1), P.SEAM_STEPS):
        p, (nx, ny) = frame(foot - back)
        z = p["track_z"] + P.LIFT
        row = []
        for c, u in enumerate(STAIR_COLS):                      # u > 0 = right of the driving direction
            edge = c in (0, len(STAIR_COLS) - 1)
            off = (5.2 if edge else 4.0 * abs(u) / STAIR_COLS[-2]) * (1 if u > 0 else -1)
            row.append((p["x"] - nx * off, p["y"] - ny * off, z - 0.14 if edge else z + rel))
        lead.append(row)
    grid = lead + grid
    for r0, r1 in zip(grid, grid[1:]):
        for c in range(len(STAIR_COLS) - 1):
            face([r0[c], r0[c + 1], r1[c + 1], r1[c]])
    last = [i for i, p in enumerate(pts) if P.owns_route_point(p)][-1]
    prev = [(x, y, P.DECK_Z + DRIVE_LIFT + ENTRY_SEAM) for x, y in far]  # strip over / through the road in the atrium
    for ahead, rel in zip((2, 3, 4, 5), reversed(P.SEAM_STEPS) if ENTRY_STRIP else ()):
        p, (nx, ny) = frame(last + ahead)
        z = p["track_z"] + P.LIFT
        row = [(p["x"] + nx * o, p["y"] + ny * o, z - 0.14 if abs(o) > 4.5 else z + rel) for o in TONGUE_COLS]
        for c in range(len(row) - 1):
            face([prev[c], prev[c + 1], row[c + 1], row[c]])
        prev = row
    shortcut_drive(face)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.004)
    bm.normal_update()
    mesh = bpy.data.meshes.new("Track_PlazaDrive_ColliderOnly")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(mat("ochre"))
    obj = E.new_object("Track_PlazaDrive_ColliderOnly", mesh, col)
    obj.hide_render = True
    obj["collider_only"] = 1
    return len(mesh.polygons)


def kick_grid():
    """Rows of the kicker's surface (start -> lip), KICK_COLS across, heights over the deck."""
    return [[(*P.climb_point(P.KICK_START_S + P.KICK_LEN * k / KICK_ROWS, u), P.DECK_Z + P.kick_profile(P.KICK_LEN * k / KICK_ROWS))
             for u in KICK_COLS] for k in range(KICK_ROWS + 1)]


def climb_lane_in_court():
    return [P.CLIMB_LANE[0], P.CLIMB_LANE[1], P.CLIMB_Q, P.CLIMB_QW]


def shortcut_drive(face):
    """V14b, the courtyard shortcut's driving surfaces, into the same welded collider as the deck and the stairs:
    the kicker (its first row = vertices of the deck, so there is no border where a kart rolls onto it), the
    courtyard floor (what you see of it, Track_PlazaCourt_NoCollide, has no collider), and the climb: a smooth
    surface over the visible steps with the stairs' ripple; its first row = vertices of the floor, its last row =
    vertices of the deck's edge. 0.28 x 1 m faces on the climb, 0.5 x 1.1 m on the kicker (no slivers)."""
    grid = [[(x, y, z + DRIVE_LIFT) for x, y, z in row] for row in kick_grid()]
    for r0, r1 in zip(grid, grid[1:]):
        for c in range(len(KICK_COLS) - 1):
            face([r0[c], r0[c + 1], r1[c + 1], r1[c]])
        for c in (0, len(KICK_COLS) - 1):                       # cheeks, down into the deck
            a, b = r0[c], r1[c]
            face([(a[0], a[1], P.DECK_Z - 0.3), (b[0], b[1], P.DECK_Z - 0.3), b, a])
    a, b = grid[-1][0], grid[-1][-1]                            # the lip's front
    face([(a[0], a[1], P.DECK_Z - 0.3), (b[0], b[1], P.DECK_Z - 0.3), b, a])
    # the courtyard floor, cut round the climb's foot
    lane = climb_lane_in_court()
    court = ccw(P.COURT_SOUTH)
    verts, edges = [], []
    for ring in (court, lane):
        base = len(verts)
        verts += [Vector(p) for p in ring]
        edges += [(base + k, base + (k + 1) % len(ring)) for k in range(len(ring))]
    verts += [Vector(P.climb_point(0.0, u)) for u in CLIMB_COLS[1:-1]]
    xs, ys = [p[0] for p in court], [p[1] for p in court]
    y = min(ys) + 1.5
    while y < max(ys):                                           # inner points every 2.5 m: no long thin triangles
        x = min(xs) + 1.5
        while x < max(xs):
            if (P.inside(x, y, court) and P.poly_distance(x, y, court) > 1.0 and not P.inside(x, y, lane)
                    and P.poly_distance(x, y, lane) > 1.0):
                verts.append(Vector((x, y)))
            x += 2.5
        y += 2.5
    out_v, _e, out_f, *_ = delaunay_2d_cdt(verts, edges, [], 0, 1e-4)
    for f in out_f:
        tri = [out_v[i] for i in f]
        cx, cy = sum(v.x for v in tri) / 3, sum(v.y for v in tri) / 3
        if P.inside(cx, cy, court) and not P.inside(cx, cy, lane):
            face([(v.x, v.y, P.LOW_Z + DRIVE_LIFT) for v in tri])
    rows = (P.CLIMB_RISERS - 1) * 4
    grid = [[(*P.climb_point(P.CLIMB_LEN * k / rows, u),
              P.climb_profile(P.CLIMB_LEN * k / rows) + P.LIFT + P.climb_judder(P.CLIMB_LEN * k / rows) + DRIVE_LIFT)
             for u in CLIMB_COLS] for k in range(rows + 1)]
    for r0, r1 in zip(grid, grid[1:]):
        for c in range(len(CLIMB_COLS) - 1):
            face([r0[c], r0[c + 1], r1[c + 1], r1[c]])


def build_low(col):
    m = Mesh("Track_PlazaLow", ["ochre"])
    c = Mesh("Track_PlazaCourt_NoCollide", ["ochre"])          # V14b: the court's collider is in the welded drive mesh
    landing = [P.slot_point(0, 0), P.slot_point(0, P.SLOT_W), P.slot_point(P.SLOT_LANDING, P.SLOT_W), P.slot_point(P.SLOT_LANDING, 0)]
    for ring in P.COURTS + P.LOWS + [P.UNDERPASS, landing]:
        target = c if ring in P.COURTS else m
        ring = ccw(ring)
        for tri in tessellate_polygon([[Vector((x, y, 0.0)) for x, y in ring]]):
            pts = [(ring[i][0], ring[i][1], P.LOW_Z) for i in tri]
            if (pts[1][0] - pts[0][0]) * (pts[2][1] - pts[0][1]) - (pts[1][1] - pts[0][1]) * (pts[2][0] - pts[0][0]) < 0:
                pts.reverse()
            target.face(pts, "ochre", tile=TILE_OCHRE)
    m.finish(col)
    c.finish(col)


def build_kicker(col):
    """V14b: what you see of the shortcut's kicker: Boost Orange stripes across (the kerb texture, like the terrace
    kicker), solid sides, a front under the lip. No collider (the welded drive mesh carries it)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = kick_grid()
    w = P.KICK_U[1] - P.KICK_U[0]
    for k, (r0, r1) in enumerate(zip(grid, grid[1:])):
        s0, s1 = P.KICK_LEN * k / KICK_ROWS, P.KICK_LEN * (k + 1) / KICK_ROWS
        top = bm.faces.new([bm.verts.new(p) for p in (r0[0], r0[-1], r1[-1], r1[0])])
        for loop, c in zip(top.loops, ((0.0, s0 / 2), (w / 2, s0 / 2), (w / 2, s1 / 2), (0.0, s1 / 2))):
            loop[uv].uv = c
        for a, b, flip in ((r0[0], r1[0], True), (r0[-1], r1[-1], False)):
            vs = [bm.verts.new((a[0], a[1], P.DECK_Z - 0.05)), bm.verts.new((b[0], b[1], P.DECK_Z - 0.05)), bm.verts.new(b), bm.verts.new(a)]
            side = bm.faces.new(vs[::-1] if flip else vs)
            for loop in side.loops:
                loop[uv].uv = (0.05, loop.vert.co.z)
    a, b = grid[-1][0], grid[-1][-1]
    front = bm.faces.new([bm.verts.new((a[0], a[1], P.DECK_Z - 0.05)), bm.verts.new(a), bm.verts.new(b), bm.verts.new((b[0], b[1], P.DECK_Z - 0.05))])
    for loop in front.loops:
        loop[uv].uv = (0.05, loop.vert.co.z)
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new("Track_ShortcutKicker")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("T_kerb", "#FF8A1F", roughness=0.7))
    E.new_object("Track_ShortcutKicker_NoCollide", mesh, col)


PAINT_LIFT = 0.04           # m over the paving (as env_trackside.LINE_LIFT)


def build_paint(col):
    """V14b, paint only (no collider): the boost-pad area before the kicker (Boost Orange with three white chevrons;
    it does nothing, a pad of PR #72 belongs here), white lane lines on the courtyard floor from under the lip to the
    climb's foot, and two orange chevrons on the floor that point at the climb."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    def quad(pts, z, mi):
        f = bm.faces.new([bm.verts.new((x, y, z)) for x, y in pts])
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
        f.material_index = mi

    def chevron(s, u, half, arm, thick, z, mi):                 # a V that points up the axis, its tip at (s, u)
        for sign in (-1, 1):
            quad([P.climb_point(s, u), P.climb_point(s - arm, u + sign * half), P.climb_point(s - arm - thick, u + sign * half),
                  P.climb_point(s - thick, u)], z, mi)
    zd, zl = P.DECK_Z + PAINT_LIFT, P.LOW_Z + PAINT_LIFT
    (s0, s1), (u0, u1) = P.PAD_S, P.PAD_U
    quad([P.climb_point(s0, u0), P.climb_point(s0, u1), P.climb_point(s1, u1), P.climb_point(s1, u0)], zd, 0)
    um = (u0 + u1) / 2
    for k in range(3):
        chevron(s0 + 1.75 + 1.4 * k, um, 1.25, 0.9, 0.45, zd + 0.01, 1)
    a, b = P.KICK_LIP_S + 2.6, -0.4                             # lane lines: from behind the pier to the foot
    for u in P.KICK_U:
        quad([P.climb_point(a, u - 0.07), P.climb_point(a, u + 0.07), P.climb_point(b, u + 0.07), P.climb_point(b, u - 0.07)], zl, 1)
    for s in (-6.2, -2.6):
        chevron(s, (P.KICK_U[0] + P.KICK_U[1]) / 2, 1.5, 1.0, 0.5, zl, 0)
    bm.normal_update()
    mesh = bpy.data.meshes.new("Track_ShortcutPaint")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("sign_chevron", E.PALETTE["boost_orange"], roughness=0.6))
    mesh.materials.append(E.material("edge_line_white", "#EDEDE8", roughness=0.6))
    E.new_object("Track_ShortcutPaint_NoCollide", mesh, col)


# ---------------------------------------------------------------------------------------------------------------
# Stairs: what you see (steps, tiers) and what you drive on (smooth, with a ripple)
# ---------------------------------------------------------------------------------------------------------------
def build_steps(m, point, zfn, s0, s1, treads, u0, u1, z_before, z_after, key, tier=1, tile=1.0):
    """Steps between s0 and s1 across u0..u1: riser k at s0 + k d, tread k at zfn(riser k) - STEP_UNDER, so the
    driving surface (zfn) is never below a tread. tier = 2: one tread per two steps (seating tiers)."""
    d = (s1 - s0) / treads
    prev = z_before
    k = 0
    while k < treads:
        a, b = s0 + k * d, s0 + min(k + tier, treads) * d
        z = zfn(a) - STEP_UNDER
        if z > prev + 0.005:                                 # riser
            m.face([(*point(a, u0), prev), (*point(a, u1), prev), (*point(a, u1), z), (*point(a, u0), z)], key,
                   [(u0 / tile, prev / tile), (u1 / tile, prev / tile), (u1 / tile, z / tile), (u0 / tile, z / tile)])
        else:
            z = prev
        m.face([(*point(a, u0), z), (*point(a, u1), z), (*point(b, u1), z), (*point(b, u0), z)], key,
               [(u0 / tile, a / tile), (u1 / tile, a / tile), (u1 / tile, b / tile), (u0 / tile, b / tile)])
        prev = z
        k += tier
    if z_after > prev + 0.005:                               # last riser, up to the deck
        m.face([(*point(s1, u0), prev), (*point(s1, u1), prev), (*point(s1, u1), z_after), (*point(s1, u0), z_after)], key,
               [(u0 / tile, prev / tile), (u1 / tile, prev / tile), (u1 / tile, z_after / tile), (u0 / tile, z_after / tile)])


def tread_z(zfn, s0, s1, treads, s, tier=1):
    """Height of the visible step at s (as build_steps makes it)."""
    d = (s1 - s0) / treads
    k = int(min(max((s - s0) / d, 0), treads - 1e-6))
    k -= k % tier
    return zfn(s0 + k * d) - STEP_UNDER


def build_stairs(col):
    half, length = P.STAIR_HALF, P.STAIR_LEN
    treads = P.STAIR_RISERS - 1
    zmain = lambda s: P.stair_profile(s) + P.LIFT                      # noqa: E731
    zslot = lambda s: P.slot_profile(s) + P.LIFT                       # noqa: E731
    vis = Mesh("Track_PlazaStairs_NoCollide", ["terracotta", "ochre"])
    # main stairs: steps on the east half, seating tiers (two steps each) on the west half
    build_steps(vis, P.stair_point, zmain, 0.0, length, treads, 0.0, half, P.LOW_Z, zmain(length), "terracotta")
    build_steps(vis, P.stair_point, zmain, 0.0, length, treads, -half, 0.0, P.LOW_Z, zmain(length), "terracotta", tier=2)
    d = length / treads
    for k in range(1, treads, 2):                             # the face between a tier and the higher step beside it
        a, b = k * d, (k + 1) * d
        lo, hi = tread_z(zmain, 0, length, treads, a + 0.01, 2), tread_z(zmain, 0, length, treads, a + 0.01, 1)
        if hi > lo + 0.005:
            vis.face([(*P.stair_point(a, 0.0), lo), (*P.stair_point(a, 0.0), hi), (*P.stair_point(b, 0.0), hi), (*P.stair_point(b, 0.0), lo)],
                     "terracotta", [(a, lo), (a, hi), (b, hi), (b, lo)])
    # the stair in the slot
    build_steps(vis, P.slot_point, zslot, P.SLOT_LANDING, P.SLOT_LEN, P.SLOT_RISERS - 1, 0.0, P.SLOT_W, P.LOW_Z, zslot(P.SLOT_LEN),
                "ochre", tile=TILE_OCHRE)
    # V14b: the climb out of the courtyard (the shortcut), terracotta like the main stairs
    zclimb = lambda s: P.climb_profile(s) + P.LIFT                      # noqa: E731
    build_steps(vis, P.climb_point, zclimb, 0.0, P.CLIMB_LEN, P.CLIMB_RISERS - 1, -P.CLIMB_HALF, P.CLIMB_HALF, P.LOW_Z,
                zclimb(P.CLIMB_LEN), "terracotta")
    vis.finish(col)

    drv = Mesh("Track_PlazaSlotDrive_ColliderOnly", ["terracotta"])
    drv.face([(*P.slot_point(P.SLOT_LANDING, 0.0), zslot(P.SLOT_LANDING)), (*P.slot_point(P.SLOT_LANDING, P.SLOT_W), zslot(P.SLOT_LANDING)),
              (*P.slot_point(P.SLOT_LEN, P.SLOT_W), zslot(P.SLOT_LEN)), (*P.slot_point(P.SLOT_LEN, 0.0), zslot(P.SLOT_LEN))], "terracotta")
    drv.finish(col, hide=True)


# ---------------------------------------------------------------------------------------------------------------
# Walls, fences, planters
# ---------------------------------------------------------------------------------------------------------------
def _on_main(a, b):
    return P.line_distance(*a, P.MAIN_EAST) < 0.35 and P.line_distance(*b, P.MAIN_EAST) < 0.35


def _centroid(ring):
    return (sum(p[0] for p in ring) / len(ring), sum(p[1] for p in ring) / len(ring))


def _inward(a, b, ring):
    """Horizontal direction from the edge a-b into the (possibly concave) polygon."""
    ex, ey = b[0] - a[0], b[1] - a[1]
    d = math.hypot(ex, ey) or 1.0
    nx, ny = -ey / d, ex / d
    mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
    return (nx, ny) if P.inside(mx + nx * 0.2, my + ny * 0.2, ring) else (-nx, -ny)


def build_walls(col):
    m = Mesh("World_PlazaWalls", ["white", "terracotta", "hedge", "dark"])
    low = P.LOW_Z - 0.05
    mouth = {tuple(P.UNDERPASS_COURT[0]), tuple(P.UNDERPASS_COURT[1])}
    for court in P.COURTS:                                     # courtyard walls (the Gallery's own wall on the west)
        for a, b in zip(court, court[1:] + court[:1]):
            if _on_main(a, b) or {tuple(a), tuple(b)} == {tuple(P.COURT_OPEN_EDGE[0]), tuple(P.COURT_OPEN_EDGE[1])}:
                continue
            bottom = P.UNDERPASS_CEILING if {tuple(a), tuple(b)} == mouth else low
            m.wall(a, b, P.deck_z(*a), P.deck_z(*b), bottom, _inward(a, b, court))
    # under the deck's south edge, both sides of the stairs
    for a, b, ring in ((P.EDGE_W, P.TOP_W, P.LOW_WEST), (P.TOP_E, P.EDGE_E, P.LOW_EAST)):
        m.wall(a, b, P.deck_z(*a), P.deck_z(*b), low, _inward(a, b, ring))
    for a, b, ring in P.UNDER_ENDS:                           # V10 integration: ends of the paving under the Gallery deck
        m.wall(a, b, P.UNDER_TOP, P.UNDER_TOP, low, _inward(a, b, ring))
    # the stairs' cheeks (terracotta, following the steps) and the solid under the west tiers' ends
    treads = P.STAIR_RISERS - 1
    zmain = lambda s: P.stair_profile(s) + P.LIFT                       # noqa: E731
    d = P.STAIR_LEN / treads
    for u, tier, out in ((P.STAIR_HALF, 1, P.STAIR_RIGHT), (-P.STAIR_HALF, 2, (-P.STAIR_RIGHT[0], -P.STAIR_RIGHT[1]))):
        for k in range(0, treads, tier):
            a, b = k * d, min(k + tier, treads) * d
            z = max(tread_z(zmain, 0, P.STAIR_LEN, treads, a + 0.01, tier), P.LOW_Z)
            m.wall(P.stair_point(a, u), P.stair_point(b, u), z, z, low, out, "terracotta")
    # V14b, the climb: terracotta cheeks beside its foot in the court (following the steps), white slot walls from
    # the steps up to the deck further up, and the pier under the kicker's lip
    zclimb = lambda s: P.climb_profile(s) + P.LIFT                      # noqa: E731
    ct = P.CLIMB_RISERS - 1
    dc = P.CLIMB_LEN / ct
    for u, s_end, out in ((P.CLIMB_HALF, P.CLIMB_Q_S, P.SC_RIGHT), (-P.CLIMB_HALF, P.CLIMB_LW_S, (-P.SC_RIGHT[0], -P.SC_RIGHT[1]))):
        for k in range(ct):
            a, b = k * dc, min((k + 1) * dc, s_end)
            if b - a < 1e-6:
                break
            z = max(tread_z(zclimb, 0, P.CLIMB_LEN, ct, a + 0.01), P.LOW_Z)
            m.wall(P.climb_point(a, u), P.climb_point(b, u), z, z, low, out, "terracotta")
    for u, s_from, out in ((P.CLIMB_HALF, P.CLIMB_Q_S, (-P.SC_RIGHT[0], -P.SC_RIGHT[1])), (-P.CLIMB_HALF, P.CLIMB_Q_S, P.SC_RIGHT)):
        n = 10
        for k in range(n):
            a, b = s_from + (P.CLIMB_LEN - s_from) * k / n, s_from + (P.CLIMB_LEN - s_from) * (k + 1) / n
            m.wall(P.climb_point(a, u), P.climb_point(b, u), P.DECK_Z, P.DECK_Z, zclimb(a) - STEP_UNDER - 0.25, out,
                   bottom_b=zclimb(b) - STEP_UNDER - 0.25)
    pier = P.KICK_PIER
    for a, b in ((pier[0], pier[3]), (pier[3], pier[2]), (pier[2], pier[1])):
        m.wall(a, b, P.DECK_Z, P.DECK_Z, low, tuple(-c for c in _inward(a, b, pier)))
    # the slot: west and east walls down to the steps, the south end; the underpass mouth stays open on the west
    zslot = lambda s: P.slot_profile(s) + P.LIFT                        # noqa: E731
    n = 8
    for u, out in ((0.0, P.SLOT_RIGHT), (P.SLOT_W, (-P.SLOT_RIGHT[0], -P.SLOT_RIGHT[1]))):
        for k in range(n):
            a, b = P.SLOT_LEN * k / n, P.SLOT_LEN * (k + 1) / n
            for a2, b2 in ((a, min(b, P.SLOT_LANDING)), (max(a, P.SLOT_LANDING), b)):
                if b2 - a2 < 1e-6:
                    continue
                pa, pb = P.slot_point(a2, u), P.slot_point(b2, u)
                if u == 0.0 and b2 <= P.SLOT_LANDING + 1e-6:
                    m.wall(pa, pb, P.DECK_Z, P.DECK_Z, P.UNDERPASS_CEILING, out)
                else:
                    m.wall(pa, pb, P.DECK_Z, P.DECK_Z, zslot(a2) - STEP_UNDER - 0.05, out, bottom_b=zslot(b2) - STEP_UNDER - 0.05)
    m.wall(P.slot_point(0, 0), P.slot_point(0, P.SLOT_W), P.DECK_Z, P.DECK_Z, low, P.SLOT_DIR)
    # the underpass: two walls and a ceiling
    up = ccw(P.UNDERPASS)
    slot_side = {tuple(P.slot_point(0, 0)), tuple(P.slot_point(P.SLOT_LANDING, 0))}
    for a, b in zip(up, up[1:] + up[:1]):
        if {tuple(a), tuple(b)} in (mouth, slot_side):
            continue
        m.wall(a, b, P.UNDERPASS_CEILING, P.UNDERPASS_CEILING, low, _inward(a, b, up))
    m.face([(x, y, P.UNDERPASS_CEILING) for x, y in reversed(up)], "white")
    # the deck's edge to the lawn (east, north-east)
    for a, b in P.OPEN_EDGES:
        m.wall(a, b, P.DECK_Z, P.DECK_Z, P.DECK_Z - 1.0, tuple(-c for c in _inward(a, b, P.DECK)))
    # planted barrier across the gap between the library and Horizon Hall
    ring = strip(*P.GAP_BARRIER, P.BARRIER_HALF)
    m.prism(ring, P.DECK_Z - 0.05, P.DECK_Z + P.BARRIER_H, "white")
    m.prism(strip(*P.GAP_BARRIER, P.BARRIER_HALF - 0.15), P.DECK_Z + P.BARRIER_H - 0.05, P.DECK_Z + P.BARRIER_H + 0.55, "hedge")
    # long planters with a clipped hedge and the dark faceted lounge blocks
    for a, b in PLANTERS:
        m.prism(strip(a, b, 0.55), P.DECK_Z - 0.05, P.DECK_Z + 0.5, "white")
        m.prism(strip(a, b, 0.42), P.DECK_Z + 0.45, P.DECK_Z + 0.95, "hedge")
    for (x, y), turn in LOUNGERS:
        c, s = math.cos(turn), math.sin(turn)
        base = [(-1.0, -0.45), (1.0, -0.45), (1.0, 0.45), (-1.0, 0.45)]
        ring = [(x + px * c - py * s, y + px * s + py * c) for px, py in base]
        back = lambda qx, qy: P.DECK_Z + 0.42 + 0.38 * max(0.0, ((qx - x) * c + (qy - y) * s) / 1.0)   # noqa: E731
        m.prism(ring, P.DECK_Z - 0.05, back, "dark")
    m.finish(col)


# planters (hedge boxes) on the plaza side of the fence where the road is far enough away, and round the stair slot
PLANTERS = [((72.7, 88.0), (71.65, 92.7)),             # along the fence in the pocket north of the courtyard's corner
            ((92.3, 96.0), (87.0, 102.0))]            # east side of the stair slot
LOUNGERS = [((104.3, 22.3), 0.35), ((106.6, 26.6), 0.35), ((109.8, 30.2), 0.35)]    # survey: three south of the library


def fence_lines():
    """[(polyline of (x, y, z), closed)] for every railing."""
    dz = lambda pts: [(x, y, P.deck_z(x, y)) for x, y in pts]            # noqa: E731
    lines = [dz(P.FENCE_SOUTH), dz(P.FENCE_SOUTH_W), dz(P.FENCE_NORTH), dz(P.FENCE_CLIMB_W),      # V14b
             dz([P.EDGE_W, P.TOP_W]), dz([P.TOP_E, P.EDGE_E]),
             dz([P.slot_point(P.SLOT_LEN, 0), P.slot_point(0, 0), P.slot_point(0, P.SLOT_W), P.slot_point(P.SLOT_LEN, P.SLOT_W)])]
    lines += [dz(line) for line in P.FENCE_GALLERY]          # V10 integration: on the Gallery deck's edge over the court
    treads = P.STAIR_RISERS - 1
    for u in (-P.STAIR_HALF + 0.04, P.STAIR_HALF - 0.04):
        lines.append([(*P.stair_point(P.STAIR_LEN * k / treads, u), P.stair_profile(P.STAIR_LEN * k / treads) + P.LIFT)
                      for k in range(treads + 1)])
    return lines


def _bar(m, a, b, half_w, z0a, z0b, h, key):
    """Sloped box along a-b (3-D ends), half_w wide to each side, from z0 to z0 + h."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    d = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / d * half_w, dx / d * half_w
    lo = [(a[0] - nx, a[1] - ny, z0a), (b[0] - nx, b[1] - ny, z0b), (b[0] + nx, b[1] + ny, z0b), (a[0] + nx, a[1] + ny, z0a)]
    hi = [(x, y, z + h) for x, y, z in lo]
    m.face(hi, key)
    m.face([lo[0], lo[1], hi[1], hi[0]], key)
    m.face([lo[2], lo[3], hi[3], hi[2]], key)


def build_fences(col):
    m = Mesh("World_PlazaFences", ["white", "steel"])
    g = Mesh("World_PlazaGuard_ColliderOnly", ["white"])
    total = 0.0
    for line in fence_lines():
        for a, b in zip(line, line[1:]):
            length = math.hypot(b[0] - a[0], b[1] - a[1])
            total += length
            _bar(m, a, b, 0.13, a[2] - 0.02, b[2] - 0.02, P.KERB_H, "white")                 # concrete kerb
            _bar(m, a, b, 0.03, a[2] + P.FENCE_H - 0.05, b[2] + P.FENCE_H - 0.05, 0.05, "steel")   # top rail
            for h in (0.48, 0.78):
                _bar(m, a, b, 0.012, a[2] + h, b[2] + h, 0.03, "steel")
            posts = max(1, int(round(length / 1.5)))
            for k in range(posts + 1):
                t = k / posts
                x, y, z = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t
                ring = [(x - 0.03, y - 0.03), (x + 0.03, y - 0.03), (x + 0.03, y + 0.03), (x - 0.03, y + 0.03)]
                for p, q in zip(ring, ring[1:] + ring[:1]):
                    m.face([(p[0], p[1], z + P.KERB_H - 0.02), (q[0], q[1], z + P.KERB_H - 0.02),
                            (q[0], q[1], z + P.FENCE_H - 0.05), (p[0], p[1], z + P.FENCE_H - 0.05)], "steel")
            g.face([(a[0], a[1], a[2] - 0.3), (b[0], b[1], b[2] - 0.3), (b[0], b[1], b[2] + GUARD_H), (a[0], a[1], a[2] + GUARD_H)], "white")
    m.finish(col)
    g.finish(col, hide=True)
    return total


def build():
    track = E.collection("Track_Surface")
    world = E.collection("World_Plaza")
    count, tris, tng = build_deck(track)
    faces = build_drive(track, tris, tng)
    build_low(track)
    build_kicker(track)
    build_paint(track)
    build_stairs(track)
    build_walls(world)
    metres = build_fences(world)
    print(f"  plaza: deck {sum(count.values())} triangles ({count['glass']} glass in 5 skylights), stairs "
          f"{P.STAIR_LEN:.1f} m, slope {P.STAIR_SLOPE * 100:.1f} %, ripple {P.JUDDER_M * 100:.1f} cm; {metres:.0f} m of railing")


# ---------------------------------------------------------------------------------------------------------------
# Dressing: nothing generic on the plaza; a row of olives east of the library, olives in the courtyard whose crowns
# show over the fence, round planters by the Gallery, cafe tables north of the library
# ---------------------------------------------------------------------------------------------------------------
TREES_COURT = [(78.4, 79.6)]          # V14b: by the east wall, clear of the shortcut's landing and lane
POTS = [(89.2, 23.6), (86.4, 30.8), (80.2, 44.2), (77.6, 51.6), (111.5, 26.2), (99.0, 86.5), (104.0, 87.2)]
CAFE = [(97.5, 93.0), (102.5, 96.0), (107.0, 92.0), (99.5, 100.5)]


def dress(veg, props):
    """Drops the generated plants and props that stand on the plaza, the courtyard or the stairs; adds ours.
    Rewrites the two placement files (like env_street_objects.dress)."""
    import json
    import os
    import random
    rng = random.Random(20261111)
    kept_v = [p for p in veg if not P.area(p["x"], p["y"], 0.6)]
    kept_p = [p for p in props if not P.area(p["x"], p["y"], 0.6)]
    plants, ours = [], []

    def plant(proto, x, y, z, scale):
        plants.append({"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(z, 3),
                       "rot_z": round(rng.uniform(0, 2 * math.pi), 4), "scale": round(scale, 3)})

    def prop(proto, x, y, rot=None):
        if E.distance_to_route(x, y) < E.KEEP_CLEAR + 0.3:
            return
        ours.append({"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(P.DECK_Z, 3),
                     "rot_z": round(rng.uniform(-math.pi, math.pi) if rot is None else rot, 4), "scale": 1.0})
    (ax, ay), (bx, by) = P.TREE_ROW
    n = 11
    for k in range(n):                                          # the tree row east of the library, 2.5 m inside the edge
        t = (k + 0.5) / n
        x, y = ax + (bx - ax) * t + 1.2, ay + (by - ay) * t + 0.4
        if P.on_deck(x, y) and E.distance_to_route(x, y) > 9.0:
            plant("Veg_Olive" if k % 2 else "Veg_Olive_B", x, y, P.DECK_Z, rng.uniform(0.82, 0.95))
    for x, y in TREES_COURT:
        if P.in_court(x, y):
            plant("Veg_Olive", x, y, P.LOW_Z, rng.uniform(0.95, 1.05))
    for x, y in POTS:
        prop("Prop_PlanterRound", x, y)
    for gx, gy in CAFE:
        prop("Prop_BistroTable", gx, gy)
        a0 = rng.uniform(0, 2 * math.pi)
        for q in range(3):
            a = a0 + 2 * math.pi * q / 3
            px, py = gx + math.cos(a) * 0.64, gy + math.sin(a) * 0.64
            prop("Prop_BistroChair", px, py, math.atan2(gx - px, -(gy - py)))
    print(f"  plaza: dressing: {len(veg) - len(kept_v)} plants and {len(props) - len(kept_p)} props removed from the plaza, "
          f"{len(plants)} trees and {len(ours)} props of its own")
    veg2, props2 = kept_v + plants, kept_p + ours
    for fname, items in (("placements_vegetation.json", veg2), ("placements_props.json", props2)):
        path = os.path.join(E.OUT, fname)
        data = json.load(open(path))
        data["placements"] = items
        counts = {}
        for it in items:
            counts[it["proto"]] = counts.get(it["proto"], 0) + 1
        data["counts"] = counts
        json.dump(data, open(path, "w"), indent=1)
    return veg2, props2
