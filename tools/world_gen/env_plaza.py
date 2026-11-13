"""The upper plaza, the terracotta stairs and the sunken courtyard (V10i, 2026-11-11), from plaza_plan.py.

Driving surfaces (collection Track_Surface, exported with the track, colliders in Unity):
  Track_PlazaDeck_NoCollide          the Floor-1 paving, one ochre surface from facade to facade, at road height
                                     (the road on the plaza IS this deck: env_track builds no road on zone "stairs" /
                                     "plaza deck"); five flush frosted-glass skylights and the library's floor are
                                     cut into the same mesh (no second surface anywhere)
  Track_PlazaLow                     Floor-0 paving: the voids beside the stairs, the landing of the stair slot,
                                     the paving under the Gallery deck
  Track_PlazaCourt_NoCollide         V14b / V20b: what you see of the sunken courtyard's and the light well's floors
  Track_ShortcutKicker_NoCollide     V14b: what you see of the shortcut's kicker (V20b: the shortcut jumps over the
                                     courtyard onto the deck; the jump pad is a BoostPad in Unity, track.json
                                     "boost_pads", no paint here)
  Track_PlazaStairs_NoCollide        what you see of the stairs: low pale-terracotta steps over the full width (V16),
                                     the steps in the slot. No collider (CampusWorldBuilder: "NoCollide")
  Track_PlazaDrive_ColliderOnly      what the kart drives on, ONE welded mesh: the deck, the stairs' smooth profile
                                     3.5 cm above the step noses with a rounded ripple of plaza_plan.JUDDER_M per
                                     step, a strip at each end that passes through the road's surface (see
                                     build_drive: a kart must never roll over the border of a collider), the kicker,
                                     the courtyard floors and the landing edge (shortcut_drive). Not drawn
                                     (CampusWorldBuilder: "ColliderOnly"; hidden from renders here, qa_geometry sees it)
  Track_PlazaSlotDrive_ColliderOnly  the smooth slope over the steps in the stair slot
World (collection World_Plaza, CampusWorld.fbx):
  World_PlazaWalls                   retaining walls round the courtyard, the light well and under the deck's south
                                     edge, the stairs' cheeks, the slot's walls, the deck's edge to the lawn, the planted
                                     barrier across the gap to Horizon Hall; V16: conical pots, the loggia's benches, the
                                     east strip's lamp columns; V20b: the tub of the olive moved off the library's wall
  World_PlazaFences                  pale kerb, flat-bar posts, top rail and the bar sheets (V16: satin steel)
  World_PlazaGuard_ColliderOnly      an upright strip behind every fence: no kart gets over or through a railing
"""
import math

import bmesh
import bpy
from mathutils import Vector
from mathutils.geometry import delaunay_2d_cdt, tessellate_polygon

import env_common as E
import plaza_plan as P

TILE_OCHRE = P.PAVE_BAY      # m per texture tile (V16: one saw-cut bay of 5 m; V10i: slabs 3 x 3 m)
TILE_GLASS = 2.5            # (V10i; V16 lays the glass pane by pane, glass_uv)
TILE_STEP = 1.5             # m across the steps per tile of the terracotta texture (one tread deep)
STEP_UNDER = 0.035          # m: the step noses lie this far under the driving surface
RAIL_COLOUR = P.RAIL_COLOUR
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
        return _textured("T_plaza_ochre", "#C9B388", "plaza_ochre", 0.85)      # V16 (survey V15); V10i: #E3BE86
    if key == "glass":
        return _textured("T_skylight", "#9DBAC0", "skylight_glass", 0.35)
    if key == "terracotta":             # own material (the track's W_T_terracotta carries a roughness map)
        return _textured("plaza_terracotta", "#DBC3A6", "plaza_terracotta", 0.85)   # V18g: pale peach stone; V18 walk #C6AA93; V16 #B08A72; V10i: #C8664A
    if key == "floor":
        return env_track.surface_material("concrete_polished")
    if key == "white":
        return E.material("B_wall_white", "#EAEAEC", 0.85)
    if key == "steel":
        return E.material("plaza_rail", RAIL_COLOUR, roughness=0.55, metallic=0.15)   # V16: satin, light (V10i: #4A4E53)
    if key == "bars":                   # V16: the campus' bar railing as an alpha-clipped sheet (shared material)
        import env_campus
        return env_campus.railing_material()
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
            elif key == "ochre":                                # V16: saw-cut bays on the building grid
                loop[self.uv].uv = pave_uv(loop.vert.co.x, loop.vert.co.y)
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


_PC, _PS = math.cos(P.PAVE_TURN), math.sin(P.PAVE_TURN)


def pave_uv(x, y):
    """Paving UVs: one tile = one bay of P.PAVE_BAY, the grid turned onto the building axes (bearing 340 / 70)."""
    dx, dy = x - P.PAVE_ORIGIN[0], y - P.PAVE_ORIGIN[1]
    return ((dx * _PC + dy * _PS) / P.PAVE_BAY, (-dx * _PS + dy * _PC) / P.PAVE_BAY)


def glass_uv(name):
    """UVs of a skylight: its own frame, one texture tile per pane (P.SKYLIGHT_PANES), so the glazing bars lie on
    the field's own edges whatever its shape (two of the four are parallelograms)."""
    p0, p1, _p2, p3 = P.SKYLIGHTS[name]
    e1, e2 = (p1[0] - p0[0], p1[1] - p0[1]), (p3[0] - p0[0], p3[1] - p0[1])
    det = e1[0] * e2[1] - e1[1] * e2[0]
    n1, n2 = P.SKYLIGHT_PANES[name]

    def uv(x, y):
        dx, dy = x - p0[0], y - p0[1]
        return ((dx * e2[1] - dy * e2[0]) / det * n1, (e1[0] * dy - e1[1] * dx) / det * n2)
    return uv


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
    tng = None if P.HALL_IN else tongue()                      # V14 + hall: the deck ends on the building's outline
    lip = [P.stair_point(P.STAIR_LEN + s, u) for s in (1.0, 2.0, 3.0, P.STAIR_REACH[1] - P.STAIR_LEN) for u in (-P.STAIR_HALF, P.STAIR_HALF)]
    rings = [P.DECK] + ([tng] if tng else []) + [P.STAIR_DOWN, P.LIB_GROUND, P.KICK_ON_DECK] + P.DECK_ISLANDS + list(P.SKYLIGHTS.values())
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
    if tng:
        verts += [Vector(q) for q in tongue_far(tng)[1:3]]                         # ... and the atrium strip's columns
    verts += [Vector(P.sc_point(P.KICK_START_S, u)) for u in KICK_COLS[1:-1]]      # V14b: the kicker's start
    verts += [Vector(p) for p in landing_edge()[1:-1]]                             # V20b: the landing edge's points
    verts += [Vector(p) for p in landing_grid()]                                  # ... and the lane: no slivers
    out_v, _e, out_f, *_ = delaunay_2d_cdt(verts, edges, [], 0, 1e-4)
    m = Mesh("Track_PlazaDeck_NoCollide", ["ochre", "glass", "floor"])
    tris = []
    count = {"ochre": 0, "glass": 0, "floor": 0}
    for f in out_f:
        tri = [out_v[i] for i in f]
        cx, cy = sum(v.x for v in tri) / 3, sum(v.y for v in tri) / 3
        if P.inside(cx, cy, P.STAIR_DOWN) or not (P.inside(cx, cy, P.DECK) or (tng and P.inside(cx, cy, tng))
                                                   or any(P.inside(cx, cy, i) for i in P.DECK_ISLANDS)):
            continue
        if P.inside(cx, cy, P.KICK_ON_DECK):                    # V14b: the kicker stands here (own surface, welded on)
            continue
        if abs((tri[1].x - tri[0].x) * (tri[2].y - tri[0].y) - (tri[1].y - tri[0].y) * (tri[2].x - tri[0].x)) < 2e-4:
            continue                                            # V20b: no flat triangles along a row of edge points
        key, sky = "ochre", None
        for name, r in P.SKYLIGHTS.items():
            if P.inside(cx, cy, r):
                key, sky = "glass", name
        if sky is None and P.inside(cx, cy, P.LIB_GROUND):
            key = "floor"
        pts = [(v.x, v.y, P.deck_z(v.x, v.y)) for v in tri]
        if (pts[1][0] - pts[0][0]) * (pts[2][1] - pts[0][1]) - (pts[1][1] - pts[0][1]) * (pts[2][0] - pts[0][0]) < 0:
            pts.reverse()
        uvs = [glass_uv(sky)(x, y) for x, y, _z in pts] if sky else None       # V16: pane by pane
        m.face(pts, key, uvs, tile={"ochre": TILE_OCHRE, "glass": TILE_GLASS, "floor": 2.0}[key])
        tris.append(pts)
        count[key] += 1
    m.finish(col)
    return count, tris, tng


STAIR_COLS = [P.STAIR_HALF * k / 4 for k in range(-4, 5)]       # 9 columns, 1.19 m apart: no sliver triangles
KICK_COLS = [P.KICK_U[0] + (P.KICK_U[1] - P.KICK_U[0]) * k / 4 for k in range(5)]   # 5 columns, 1.0 m apart
LAND_STEP = 1.0             # V20b: m between the points of the landing edge (the deck and its upright face share them)
GRID_STEP = 2.0             # V20b: m between the deck's inner points over the landing lane


def landing_edge():
    """V20b: points along the deck's edge across the courtyard, west (the hall) to east (the bridge's pier)."""
    w, e = P.LAND_W, P.LAND_E
    n = max(1, int(round(math.hypot(e[0] - w[0], e[1] - w[1]) / LAND_STEP)))
    return [(w[0] + (e[0] - w[0]) * k / n, w[1] + (e[1] - w[1]) * k / n) for k in range(n + 1)]


def landing_grid():
    """V20b: inner points of the deck over the landing lane (axis frame, from the landing edge to the door), so the
    surface a kart lands and rolls on is cut into even triangles, not long slivers between far-apart outline points.
    Points closer than 0.6 m to any outline of the deck mesh are left out (the CDT would make slivers there)."""
    rings = [P.DECK, P.KICK_ON_DECK, P.STAIR_DOWN, P.LIB_GROUND] + list(P.SKYLIGHTS.values())
    pts = []
    s = P.LAND_S + GRID_STEP
    while s < P.sc_frame(*P.SC_DOOR)[0] - 1.0:
        u = P.LANE_W
        while u <= 4.0:
            x, y = P.sc_point(s, u)
            if P.inside(x, y, P.DECK) and not P.inside(x, y, P.STAIR_DOWN) and min(P.poly_distance(x, y, r) for r in rings) > 0.6:
                pts.append((x, y))
            u += GRID_STEP
        s += GRID_STEP
    return pts
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
        if f.normal.z < -0.3:                                   # (upright faces come in with the right winding)
            f.normal_flip()
    far = tongue_far(tng) if tng else None                      # 4 points across the tongue's far end
    for tri in deck_tris:                                       # the deck; the tongue's far end 5 cm over the road
        face([(x, y, z + DRIVE_LIFT + (entry_rise(x, y, tng) if tng else 0.0)) for x, y, z in tri])
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
    prev = [(x, y, P.DECK_Z + DRIVE_LIFT + ENTRY_SEAM) for x, y in far] if tng else []   # strip over / through the road in the atrium
    for ahead, rel in zip((2, 3, 4, 5), reversed(P.SEAM_STEPS) if ENTRY_STRIP and tng else ()):
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
    return [[(*P.sc_point(P.KICK_START_S + P.KICK_LEN * k / KICK_ROWS, u), P.DECK_Z + P.kick_profile(P.KICK_LEN * k / KICK_ROWS))
             for u in KICK_COLS] for k in range(KICK_ROWS + 1)]


def court_floor(ring, face):
    """Floor-0 faces of a courtyard (inner points every 2.5 m: no long thin triangles), into the welded collider."""
    ring = ccw(ring)
    verts = [Vector(p) for p in ring]
    edges = [(k, (k + 1) % len(ring)) for k in range(len(ring))]
    xs, ys = [p[0] for p in ring], [p[1] for p in ring]
    y = min(ys) + 1.25
    while y < max(ys):
        x = min(xs) + 1.25
        while x < max(xs):
            if P.inside(x, y, ring) and P.poly_distance(x, y, ring) > 1.0:
                verts.append(Vector((x, y)))
            x += 2.5
        y += 2.5
    out_v, _e, out_f, *_ = delaunay_2d_cdt(verts, edges, [], 0, 1e-4)
    for f in out_f:
        tri = [out_v[i] for i in f]
        cx, cy = sum(v.x for v in tri) / 3, sum(v.y for v in tri) / 3
        area = abs((tri[1].x - tri[0].x) * (tri[2].y - tri[0].y) - (tri[1].y - tri[0].y) * (tri[2].x - tri[0].x)) / 2
        if area > 1e-4 and P.inside(cx, cy, ring):             # (the CDT also makes flat triangles along a row of points)
            face([(v.x, v.y, P.LOW_Z + DRIVE_LIFT) for v in tri])


def shortcut_drive(face):
    """V14b / V20b, the shortcut's driving surfaces, into the same welded collider as the deck and the stairs: the
    kicker (its first row = vertices of the deck, so there is no border where a kart rolls onto it; 0.5 x 1.0 m faces,
    cheeks and the lip's front looking outwards), the courtyard's and the light well's floors (what you see of them,
    Track_PlazaCourt_NoCollide, has no collider), and V20b's landing edge: an upright face from the deck's edge down to
    the courtyard floor, looking into the courtyard, whose top edge is the deck's own edge (LAND_STEP points), so the
    landing, the run-out and the deck are one surface and the edge a kart may touch is a welded corner, not a border."""
    grid = [[(x, y, z + DRIVE_LIFT) for x, y, z in row] for row in kick_grid()]
    for r0, r1 in zip(grid, grid[1:]):
        for c in range(len(KICK_COLS) - 1):
            face([r0[c], r0[c + 1], r1[c + 1], r1[c]])
        # cheeks, down into the deck, each looking outwards (first test: with the winding left to chance the lip's
        # front looked into the ramp and kicked 2 of 8 karts 2 to 4 m/s upwards as they left the lip)
        a, b = r1[0], r0[0]
        face([(a[0], a[1], P.DECK_Z - 0.3), (b[0], b[1], P.DECK_Z - 0.3), b, a])
        a, b = r0[-1], r1[-1]
        face([(a[0], a[1], P.DECK_Z - 0.3), (b[0], b[1], P.DECK_Z - 0.3), b, a])
    a, b = grid[-1][-1], grid[-1][0]                            # the lip's front, looking into the courtyard
    face([(a[0], a[1], P.DECK_Z - 0.3), (b[0], b[1], P.DECK_Z - 0.3), b, a])
    for ring in P.COURTS:                                       # the courtyard and the light well
        if ring is P.COURT_SOUTH:                               # the floor shares the landing face's foot points
            k = ring.index(P.LAND_E)
            ring = ring[:k + 1] + landing_edge()[-2:0:-1] + ring[k + 1:]
        court_floor(ring, face)
    edge = landing_edge()                                       # west -> east: the face looks back (-SC_DIR)
    top = P.DECK_Z + DRIVE_LIFT
    for a, b in zip(edge, edge[1:]):
        face([(a[0], a[1], P.LOW_Z + DRIVE_LIFT), (b[0], b[1], P.LOW_Z + DRIVE_LIFT), (b[0], b[1], top), (a[0], a[1], top)])


def build_low(col):
    m = Mesh("Track_PlazaLow", ["ochre"])
    c = Mesh("Track_PlazaCourt_NoCollide", ["ochre"])          # V14b: the court's collider is in the welded drive mesh (V20b: and the well's)
    landing = [P.slot_point(0, 0), P.slot_point(0, P.SLOT_W), P.slot_point(P.SLOT_LANDING, P.SLOT_W), P.slot_point(P.SLOT_LANDING, 0)]
    for ring in P.COURTS + P.LOWS + [landing]:                # (V20b: V10i's underpass is gone with the court's north part)
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
    """V14b: what you see of the shortcut's kicker. No collider (the welded drive mesh carries it, 2 mm higher).
    V18i (Luc: "the ramp is transparent"): ONE closed, welded solid with every face looking outwards by construction.
    Before, each face had its own vertices and recalc_face_normals() turned every island on its own, so about half
    the top strips and side pieces looked inwards and vanished in the game (Unity draws front faces only); the cheeks
    and the front also overlapped the pier's walls by 5 cm in the same plane. Now: striped top between two white edge
    lines, Boost Orange cheeks and lip face that start exactly at the deck / the pier's top (no overlap), a bottom."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = kick_grid()
    w = P.KICK_U[1] - P.KICK_U[0]
    line = 0.16                                                  # white edge line on both sides of the top
    cache = {}

    def vert(p):
        key = (round(p[0], 4), round(p[1], 4), round(p[2], 4))
        if key not in cache:
            cache[key] = bm.verts.new(p)
        return cache[key]

    def face(pts, uvs, slot, want):
        """want: the direction the face must look (its normal's dot product with it > 0); turned if it does not."""
        ring = []
        for p, t in zip(pts, uvs):                               # a cheek's first piece is a triangle (height 0)
            v = vert(p)
            if not ring or (v is not ring[-1][0] and v is not ring[0][0]):
                ring.append((v, t))
        if len(ring) < 3:
            return
        a, b, c = (Vector(v.co) for v, _t in ring[:3])
        if (b - a).cross(c - a).dot(Vector(want)) < 0:
            ring.reverse()
        f = bm.faces.new([v for v, _t in ring])
        f.material_index = slot
        for loop, (_v, t) in zip(f.loops, ring):
            loop[uv].uv = t

    def across(row, u):                                          # point of a row at u metres from its west end
        t = u / w
        return tuple(row[0][k] + (row[-1][k] - row[0][k]) * t for k in range(3))
    right, fwd, up = P.SC_RIGHT, P.SC_DIR, (0.0, 0.0, 1.0)
    for k, (r0, r1) in enumerate(zip(grid, grid[1:])):
        s0, s1 = P.KICK_LEN * k / KICK_ROWS, P.KICK_LEN * (k + 1) / KICK_ROWS
        for u0, u1, slot in ((0.0, line, 1), (line, w - line, 0), (w - line, w, 1)):
            face([across(r0, u0), across(r0, u1), across(r1, u1), across(r1, u0)],
                 [(u0 / 2, s0 / 2), (u1 / 2, s0 / 2), (u1 / 2, s1 / 2), (u0 / 2, s1 / 2)], slot, up)
        for a, b, out in ((r0[0], r1[0], (-right[0], -right[1], 0.0)), (r0[-1], r1[-1], (right[0], right[1], 0.0))):
            pts = [(a[0], a[1], P.DECK_Z), (b[0], b[1], P.DECK_Z), b, a]
            face(pts, [(0.05, p[2]) for p in pts], 0, out)
    a, b = grid[-1][0], grid[-1][-1]                             # the lip's face, looking into the courtyard
    pts = [(a[0], a[1], P.DECK_Z), a] + [across(grid[-1], u) for u in (line, w - line)] + [b, (b[0], b[1], P.DECK_Z)]
    face(pts, [(0.05, p[2]) for p in pts], 0, (fwd[0], fwd[1], 0.0))
    ring = [(r[0][0], r[0][1], P.DECK_Z) for r in grid] + [(r[-1][0], r[-1][1], P.DECK_Z) for r in reversed(grid)] \
        + [across(grid[0], u) for u in (w - line, line)]        # (the first row lies on the deck's level)
    face(ring, [(0.05, 0.0)] * len(ring), 0, (0.0, 0.0, -1.0))  # the bottom (on the deck's level, never seen)
    bm.normal_update()
    open_edges = sum(1 for e in bm.edges if len(e.link_faces) != 2)
    inward = sum(1 for e in bm.edges if not e.is_contiguous) + (bm.calc_volume(signed=True) <= 0)
    print(f"  plaza: shortcut kicker {len(bm.faces)} faces, edges not shared by exactly two faces: {open_edges}, "
          f"edges whose two faces disagree about the outside (+1 if the whole solid is inside out): {inward}")
    if open_edges or inward:
        raise RuntimeError("V18i: the shortcut kicker is not a closed solid with outward faces")
    mesh = bpy.data.meshes.new("Track_ShortcutKicker")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("T_kerb", "#FF8A1F", roughness=0.7))
    mesh.materials.append(E.material("edge_line_white", "#EDEDE8", roughness=0.6))
    E.new_object("Track_ShortcutKicker_NoCollide", mesh, col)


# ---------------------------------------------------------------------------------------------------------------
# Stairs: what you see (steps, tiers) and what you drive on (smooth, with a ripple)
# ---------------------------------------------------------------------------------------------------------------
def build_steps(m, point, zfn, s0, s1, treads, u0, u1, z_before, z_after, key, tier=1, tile=1.0):
    """Steps between s0 and s1 across u0..u1: riser k at s0 + k d, tread k at zfn(riser k) - STEP_UNDER, so the
    driving surface (zfn) is never below a tread. tier = 2: one tread per two steps (seating tiers)."""
    d = (s1 - s0) / treads
    prev = z_before
    k = 0
    terra = key == "terracotta"            # V16: the terracotta texture is one tread deep (a dark line at each nosing)
    ua, ub = u0 / (TILE_STEP if terra else tile), u1 / (TILE_STEP if terra else tile)

    def riser(s, lo, hi):
        uv = [(ua, 0.42), (ub, 0.42), (ub, 0.58), (ua, 0.58)] if terra else \
             [(ua, lo / tile), (ub, lo / tile), (ub, hi / tile), (ua, hi / tile)]
        m.face([(*point(s, u0), lo), (*point(s, u1), lo), (*point(s, u1), hi), (*point(s, u0), hi)], key, uv)
    while k < treads:
        a, b = s0 + k * d, s0 + min(k + tier, treads) * d
        z = zfn(a) - STEP_UNDER
        if z > prev + 0.005:                                 # riser
            riser(a, prev, z)
        else:
            z = prev
        uv = [(ua, 0.0), (ub, 0.0), (ub, 1.0), (ua, 1.0)] if terra else \
             [(ua, a / tile), (ub, a / tile), (ub, b / tile), (ua, b / tile)]
        m.face([(*point(a, u0), z), (*point(a, u1), z), (*point(b, u1), z), (*point(b, u0), z)], key, uv)
        prev = z
        k += tier
    if z_after > prev + 0.005:                               # last riser, up to the deck
        riser(s1, prev, z_after)


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
    # main stairs. V16 (survey V15, section 4 item 2): low steps over the full width, 21 risers of about 0.16 m
    # (V10i: steps on the east half, seating tiers of two steps each on the west half). The steps keep their place under
    # the driving surface: tread k lies STEP_UNDER below the profile at its own riser, as before.
    # V18g (walk 7 Nov 2026, ANTWORT A10): the flight is two halves, offset lengthwise, with a joint down the middle.
    # East half as before; the west half's risers stand half a tread further up (its treads still lie STEP_UNDER below
    # the profile at their own riser, so the driving surface is never below a step). Visual only.
    build_steps(vis, P.stair_point, zmain, 0.0, length, treads, 0.0, half, P.LOW_Z, zmain(length), "terracotta")
    d = length / treads
    ua, ub = -half / TILE_STEP, 0.0

    def west_tread(a, b, z):
        t = (b - a) / d
        vis.face([(*P.stair_point(a, -half), z), (*P.stair_point(a, 0.0), z), (*P.stair_point(b, 0.0), z),
                  (*P.stair_point(b, -half), z)], "terracotta", [(ua, 0.0), (ub, 0.0), (ub, t), (ua, t)])

    def west_z(s):
        return P.LOW_Z + 0.004 if s < d / 2 else zmain(d / 2 + int((s - d / 2) / d) * d) - STEP_UNDER
    west_tread(0.0, d / 2, P.LOW_Z + 0.004)                             # the landing strip before the first riser
    build_steps(vis, P.stair_point, zmain, d / 2, length - d / 2, treads - 1, -half, 0.0, P.LOW_Z + 0.004,
                zmain(length - d / 2) - STEP_UNDER, "terracotta")
    west_tread(length - d / 2, length, zmain(length - d / 2) - STEP_UNDER)
    vis.face([(*P.stair_point(length, -half), zmain(length - d / 2) - STEP_UNDER), (*P.stair_point(length, 0.0), zmain(length - d / 2) - STEP_UNDER),
              (*P.stair_point(length, 0.0), zmain(length)), (*P.stair_point(length, -half), zmain(length))], "terracotta",
             [(ua, 0.42), (ub, 0.42), (ub, 0.58), (ua, 0.58)])
    for j in range(2 * treads):                                         # the joint's cheeks (sawtooth between the halves)
        a, b = j * d / 2, (j + 1) * d / 2
        ze, zw = tread_z(zmain, 0.0, length, treads, (a + b) / 2), west_z((a + b) / 2)
        if abs(ze - zw) < 0.005:
            continue
        face = P.STAIR_RIGHT if zw > ze else (-P.STAIR_RIGHT[0], -P.STAIR_RIGHT[1])
        vis.wall(P.stair_point(a, 0.0), P.stair_point(b, 0.0), max(ze, zw), max(ze, zw), min(ze, zw), face, key="terracotta")
    # the stair in the slot
    build_steps(vis, P.slot_point, zslot, P.SLOT_LANDING, P.SLOT_LEN, P.SLOT_RISERS - 1, 0.0, P.SLOT_W, P.LOW_Z, zslot(P.SLOT_LEN),
                "ochre", tile=TILE_OCHRE)
    # (V20b: V14b's climb out of the courtyard is gone, the shortcut jumps over the court)
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
    for court in P.COURTS:                                     # courtyard and light-well walls (the hall's on the west)
        for a, b in zip(court, court[1:] + court[:1]):
            if _on_main(a, b):
                continue
            m.wall(a, b, P.deck_z(*a), P.deck_z(*b), low, _inward(a, b, court))
    # under the deck's south edge, both sides of the stairs
    for a, b, ring in ((P.EDGE_W, P.TOP_W, P.LOW_WEST), (P.TOP_E, P.EDGE_E, P.LOW_EAST)):
        m.wall(a, b, P.deck_z(*a), P.deck_z(*b), low, _inward(a, b, ring))
    for a, b, ring in P.UNDER_ENDS:                           # V10 integration: ends of the paving under the Gallery deck
        m.wall(a, b, P.UNDER_TOP, P.UNDER_TOP, low, _inward(a, b, ring))
    # the stairs' cheeks (terracotta, following the steps) and the solid under the west tiers' ends
    treads = P.STAIR_RISERS - 1
    zmain = lambda s: P.stair_profile(s) + P.LIFT                       # noqa: E731
    d = P.STAIR_LEN / treads
    for u, tier, out in ((P.STAIR_HALF, 1, P.STAIR_RIGHT), (-P.STAIR_HALF, 1, (-P.STAIR_RIGHT[0], -P.STAIR_RIGHT[1]))):   # V16: steps on both halves
        for k in range(0, treads, tier):
            a, b = k * d, min(k + tier, treads) * d
            z = max(tread_z(zmain, 0, P.STAIR_LEN, treads, a + 0.01, tier), P.LOW_Z)
            m.wall(P.stair_point(a, u), P.stair_point(b, u), z, z, low, out, "terracotta")
    # the pier under the kicker's lip (V14b)
    pier = P.KICK_PIER
    for a, b in ((pier[0], pier[3]), (pier[3], pier[2]), (pier[2], pier[1])):
        m.wall(a, b, P.DECK_Z, P.DECK_Z, low, tuple(-c for c in _inward(a, b, pier)))
    # the slot: west and east walls down to the steps, the south end
    zslot = lambda s: P.slot_profile(s) + P.LIFT                        # noqa: E731
    n = 8
    for u, out in ((0.0, P.SLOT_RIGHT), (P.SLOT_W, (-P.SLOT_RIGHT[0], -P.SLOT_RIGHT[1]))):
        for k in range(n):
            a, b = P.SLOT_LEN * k / n, P.SLOT_LEN * (k + 1) / n
            for a2, b2 in ((a, min(b, P.SLOT_LANDING)), (max(a, P.SLOT_LANDING), b)):
                if b2 - a2 < 1e-6:
                    continue
                pa, pb = P.slot_point(a2, u), P.slot_point(b2, u)
                if u == 0.0 and b2 <= P.SLOT_LANDING + 1e-6:     # V20b: closed (V10i's underpass mouth is gone)
                    m.wall(pa, pb, P.DECK_Z, P.DECK_Z, low, out)
                else:
                    m.wall(pa, pb, P.DECK_Z, P.DECK_Z, zslot(a2) - STEP_UNDER - 0.05, out, bottom_b=zslot(b2) - STEP_UNDER - 0.05)
    m.wall(P.slot_point(0, 0), P.slot_point(0, P.SLOT_W), P.DECK_Z, P.DECK_Z, low, P.SLOT_DIR)
    # the deck's edge to the lawn (east, north-east)
    for a, b in P.OPEN_EDGES:
        m.wall(a, b, P.DECK_Z, P.DECK_Z, P.DECK_Z - 1.0, tuple(-c for c in _inward(a, b, P.DECK)))
    # planted barrier across the gap between the library and Horizon Hall
    ring = strip(*P.GAP_BARRIER, P.BARRIER_HALF)
    m.prism(ring, P.DECK_Z - 0.05, P.DECK_Z + P.BARRIER_H, "white")
    m.prism(strip(*P.GAP_BARRIER, P.BARRIER_HALF - 0.15), P.DECK_Z + P.BARRIER_H - 0.05, P.DECK_Z + P.BARRIER_H + 0.55, "hedge")
    import env_vegetation as V                                 # V18n: leaf cards over the hedge boxes
    V.hedge_leaves("Plaza_BarrierHedgeLeaves", V.hedge_box_faces(
        list(P.GAP_BARRIER), 2 * (P.BARRIER_HALF - 0.15), lambda x, y: P.DECK_Z + P.BARRIER_H,
        lambda x, y: P.DECK_Z + P.BARRIER_H + 0.55), "hedge", "broad", card=0.5, cover=5.0, seed=70, fins=0.4, foot=0.0)
    # V16 (survey V15: no hedges, planters or loungers on the plaza): V10i's two hedge planters and three lounge blocks
    # are gone; the hedge boxes of the shortcut's funnel stay exactly (they guide the yellow route).
    # (V20b: V14b's hedge funnel at the climb's exit is gone with the climb)
    build_furniture(m)
    m.finish(col)


def _round(m, x, y, r0, r1, z0, z1, key, sides=8, top=True):
    """Upright frustum (radius r0 at z0, r1 at z1), looking outwards."""
    for k in range(sides):
        a0, a1 = 2 * math.pi * k / sides, 2 * math.pi * (k + 1) / sides
        m.face([(x + math.cos(a0) * r0, y + math.sin(a0) * r0, z0), (x + math.cos(a1) * r0, y + math.sin(a1) * r0, z0),
                (x + math.cos(a1) * r1, y + math.sin(a1) * r1, z1), (x + math.cos(a0) * r1, y + math.sin(a0) * r1, z1)], key)
    if top:
        m.face([(x + math.cos(2 * math.pi * k / sides) * r1, y + math.sin(2 * math.pi * k / sides) * r1, z1) for k in range(sides)], key)


def loggia_point(u, v):
    """u metres east along the library's south front from its south-west corner, v metres into the loggia."""
    sw, se = P.LIB_SOUTH_WING[0], P.LIB_SOUTH_WING[1]
    tx, ty, _d = P._unit(*sw, *se)
    return (sw[0] + tx * u - ty * v, sw[1] + ty * u + tx * v), (tx, ty)


def east_lamps():
    """Positions of the slender lamp columns in the east strip's tree line, between the trees."""
    (ax, ay), (bx, by) = P.EAST_ROW
    return [(ax + (bx - ax) * (k + 0.5) / P.EAST_LAMPS, ay + (by - ay) * (k + 0.5) / P.EAST_LAMPS) for k in range(P.EAST_LAMPS)]


def build_furniture(m):
    """V16, what the survey shows on the plaza: dark conical pots, long low dark benches in the library's loggia,
    slender black lamp columns in the east strip's tree line. Solid (World_PlazaWalls has a collider), so each is
    checked against the race line (keep_clear stops the build under P.SOLID_CLEAR)."""
    for (x, y), _olive in P.POTS:
        keep_clear("pots", x, y, P.POT_R)
        z = P.surface_z(x, y)
        _round(m, x, y, P.POT_R * 0.62, P.POT_R, z - 0.02, z + P.POT_H, "dark", top=False)
        _round(m, x, y, P.POT_R - 0.05, P.POT_R - 0.05, z + P.POT_H - 0.07, z + P.POT_H - 0.07, "dark")    # the soil
    x, y = P.TUB_OLIVE                                          # V20b: the old tub olive's tub, clear of the walls
    keep_clear("olive tub", x, y, P.TUB_R)
    _round(m, x, y, P.TUB_R * 0.85, P.TUB_R, P.DECK_Z - 0.02, P.DECK_Z + P.TUB_H, "dark", sides=10, top=False)
    _round(m, x, y, P.TUB_R - 0.05, P.TUB_R - 0.05, P.DECK_Z + P.TUB_H - 0.08, P.DECK_Z + P.TUB_H - 0.08, "dark", sides=10)
    length, depth, height = P.BENCH
    for u, v, turn in P.BENCHES:
        (x, y), (tx, ty) = loggia_point(u, v)
        if turn:
            tx, ty = -ty, tx
        keep_clear("benches", x, y, length / 2)
        ring = [(x + tx * sx * length / 2 - ty * sy * depth / 2, y + ty * sx * length / 2 + tx * sy * depth / 2)
                for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        m.prism(ring, P.DECK_Z - 0.02, P.DECK_Z + height, "dark")
    for x, y in east_lamps():                                    # square, 0.12 m, no arm, a short light head
        keep_clear("lamp columns", x, y)
        z = E.ground_z(x, y)
        for half, z0, z1 in ((0.06, z - 0.3, z + P.LAMP_H), (0.09, z + P.LAMP_H - 0.45, z + P.LAMP_H + 0.02)):
            m.prism([(x - half, y - half), (x + half, y - half), (x + half, y + half), (x - half, y + half)], z0, z1, "dark")


def fence_lines():
    """[(polyline of (x, y, z), closed)] for every railing."""
    dz = lambda pts: [(x, y, P.deck_z(x, y)) for x, y in pts]            # noqa: E731
    lines = [dz(P.FENCE_SOUTH), dz(P.FENCE_SOUTH_W), dz(P.FENCE_WELL),                         # V14b / V20b
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


def clear_m(x, y):
    """Distance to the race line: route.json, and the line into the hall's atrium door (hall_join.py)."""
    return min(E.distance_to_route(x, y), P.line_distance(x, y, P.HALL_JOIN))


_CLEAR = {}


def keep_clear(what, x, y, reach=0.0):
    """Stops the build when a solid piece of the plaza's dressing stands closer than P.SOLID_CLEAR to the line."""
    d = clear_m(x, y) - reach
    if d < P.SOLID_CLEAR:
        raise RuntimeError(f"plaza: {what} at ({x:.1f}, {y:.1f}) is {d:.2f} m from the race line (needs {P.SOLID_CLEAR})")
    _CLEAR[what] = min(d, _CLEAR.get(what, 1e9))
    return d


def _void_side(a, b):
    """+1 if the lower ground lies to the left of a -> b, else -1: the kerb's width goes to that side, its face on
    the road's side stays where V10i's kerb had it."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    d = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / d, dx / d
    mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2

    def level(sign):
        z = P.surface_z(mx + nx * 0.6 * sign, my + ny * 0.6 * sign)
        return P.DECK_Z if z is None else z
    left, right = level(1), level(-1)
    if abs(left - right) > 0.02:
        return 1 if left < right else -1
    return 1 if E.distance_to_route(mx + nx, my + ny) > E.distance_to_route(mx - nx, my - ny) else -1


def _kerb(m, a, b, side, drop):
    """The pale kerb under a railing: P.KERB_IN towards the road, P.KERB_OUT towards the void, P.KERB_H high; `drop`
    = how far it reaches down (on the stairs it goes down into the steps: the sloping white upstand)."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    d = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / d * side, dx / d * side                     # towards the void
    lo = [(p[0] + nx * o, p[1] + ny * o, p[2] - drop) for p, o in ((a, -P.KERB_IN), (b, -P.KERB_IN), (b, P.KERB_OUT), (a, P.KERB_OUT))]
    hi = [(x, y, z + drop + P.KERB_H) for x, y, z in lo]
    m.face(hi, "white")
    m.face([lo[0], lo[1], hi[1], hi[0]], "white")
    m.face([lo[2], lo[3], hi[3], hi[2]], "white")
    if drop > 0.1:                                             # its underside shows from the voids beside the stairs
        m.face(lo[::-1], "white")
    return lo, hi


def build_fences(col):
    """V16 (survey V15, section 1 point 1 and E1 2.4): light satin steel, 1.1 m, close vertical bars, flat-bar posts
    every 2 m, on a pale kerb 0.35 m wide and 0.12 m high. The bars are the campus' railing sheet (env_campus.
    railing_material, one quad per bay, 12.5 cm bar spacing), top rail and posts are geometry. V10i had dark tube
    rails with two horizontal bars on a 0.25 m kerb. The guard collider is built exactly as before."""
    m = Mesh("World_PlazaFences", ["white", "steel", "bars"])
    g = Mesh("World_PlazaGuard_ColliderOnly", ["white"])
    total = 0.0
    lines = fence_lines()
    for n_line, line in enumerate(lines):
        on_stairs = n_line >= len(lines) - 2                    # the two railings beside the terracotta stairs
        for n_seg, (a, b) in enumerate(zip(line, line[1:])):
            length = math.hypot(b[0] - a[0], b[1] - a[1])
            total += length
            side = _void_side(a, b)
            lo, hi = _kerb(m, a, b, side, 0.3 if on_stairs else 0.02)
            for end, quad in ((n_seg == 0, (lo[3], lo[0], hi[0], hi[3])), (n_seg == len(line) - 2, (lo[1], lo[2], hi[2], hi[1]))):
                if end:                                         # close the kerb's two ends
                    m.face(list(quad), "white")
            top = P.FENCE_H - 0.04
            _bar(m, a, b, 0.025, a[2] + top, b[2] + top, 0.04, "steel")                     # flat top rail
            bays = max(1, int(round(length / P.POST_STEP)))
            ex, ey = (b[0] - a[0]) / (length or 1.0), (b[1] - a[1]) / (length or 1.0)
            for k in range(bays + 1):
                t = k / bays
                x, y, z = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t
                if k or n_seg == 0:                             # flat-bar post, 5 cm along the railing, 1.6 cm thick
                    ring = [(x - ex * 0.025 + ey * 0.008, y - ey * 0.025 - ex * 0.008), (x + ex * 0.025 + ey * 0.008, y + ey * 0.025 - ex * 0.008),
                            (x + ex * 0.025 - ey * 0.008, y + ey * 0.025 + ex * 0.008), (x - ex * 0.025 - ey * 0.008, y - ey * 0.025 + ex * 0.008)]
                    for p, q in zip(ring, ring[1:] + ring[:1]):
                        m.face([(p[0], p[1], z + P.KERB_H - 0.01), (q[0], q[1], z + P.KERB_H - 0.01),
                                (q[0], q[1], z + top), (p[0], p[1], z + top)], "steel")
                if k < bays:                                    # the bars: one sheet per bay, bars 12.5 cm apart
                    t1 = (k + 1) / bays
                    p = (x, y, z)
                    q = (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1, a[2] + (b[2] - a[2]) * t1)
                    u0, u1 = BAR_U0, BAR_U0 + math.hypot(q[0] - p[0], q[1] - p[1]) / BAR_MODULE
                    m.face([(p[0], p[1], p[2] + P.KERB_H), (q[0], q[1], q[2] + P.KERB_H), (q[0], q[1], q[2] + P.FENCE_H), (p[0], p[1], p[2] + P.FENCE_H)],
                           "bars", [(u0, 0.0), (u1, 0.0), (u1, 1.0), (u0, 1.0)])
            g.face([(a[0], a[1], a[2] - 0.3), (b[0], b[1], b[2] - 0.3), (b[0], b[1], b[2] + GUARD_H), (a[0], a[1], a[2] + GUARD_H)], "white")
    m.finish(col)
    g.finish(col, hide=True)
    return total


BAR_MODULE = 3.0            # m of railing per tile of textures/railing_cutout_albedo.png (24 bars)
BAR_U0 = 0.03               # the sheet starts just after the texture's own end post (the posts are geometry here)


def build():
    track = E.collection("Track_Surface")
    world = E.collection("World_Plaza")
    count, tris, tng = build_deck(track)
    faces = build_drive(track, tris, tng)
    build_low(track)
    build_kicker(track)                                         # (V20b: V14b's painted pad is gone: a real BoostPad)
    build_stairs(track)
    build_walls(world)
    metres = build_fences(world)
    print(f"  plaza: deck {sum(count.values())} triangles ({count['glass']} glass in 5 skylights), stairs "
          f"{P.STAIR_LEN:.1f} m, slope {P.STAIR_SLOPE * 100:.1f} %, ripple {P.JUDDER_M * 100:.1f} cm; {metres:.0f} m of railing")
    if _CLEAR:
        print("  plaza: nearest solid dressing to the race line: " + ", ".join(f"{k} {v:.1f} m" for k, v in sorted(_CLEAR.items())))


# ---------------------------------------------------------------------------------------------------------------
# Dressing: nothing generic on the plaza. V16: young staked trees in the lawn's rows east of the library, one olive in
# the courtyard whose crown shows over the fence, two potted young olives, cafe tables under the pergola
# ---------------------------------------------------------------------------------------------------------------
OLIVE_CROWN_R = 2.95                  # m: crown radius of Veg_Olive at scale 1 (5.7 x 6.0 m prototype)
WALLS_NEAR_TUB = None                 # filled by dress(): (what, distance from the crown's edge) of the nearest wall
CAFE = [(1, 1), (2, 1), (3, 1), (1, 2), (2, 2), (3, 2)]     # V16: cafe groups under the pergola (bay column, row)


def pergola_point(u, v):
    (ax, ay), (bx, by) = P.PERGOLA_FRONT
    tx, ty, _d = P._unit(ax, ay, bx, by)
    return (ax + tx * u - ty * v, ay + ty * u + tx * v)


def _hash01(x, y, salt=0):
    v = math.sin(x * 12.9898 + y * 78.233 + salt * 37.719) * 43758.5453
    return v - math.floor(v)


def dress(veg, props):
    """Drops the generated plants and props that stand on the plaza, the courtyard or the stairs; adds ours.
    Rewrites the two placement files (like env_street_objects.dress).
    V16 (survey V15): the plaza is nearly empty. No tree row on the deck, no round planters; the olives of the lawn's
    two rows along the plaza's east edge become young staked trees where they stand (nothing moves); young olives in
    the conical pots of the pocket; the cafe groups stand under the pergola."""
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
        if clear_m(x, y) < P.SOLID_CLEAR + 0.4:
            return
        ours.append({"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(P.DECK_Z, 3),
                     "rot_z": round(rng.uniform(-math.pi, math.pi) if rot is None else rot, 4), "scale": 1.0})
    swapped = 0
    lamps = east_lamps()
    for k, p in enumerate(kept_v):                              # the east strip: staked saplings instead of old olives
        if p["proto"] in ("Veg_Olive", "Veg_Olive_B") and P.seg_distance(p["x"], p["y"], *P.EAST_ROW) < P.EAST_ROW_REACH:
            if any(math.hypot(p["x"] - lx, p["y"] - ly) < 1.2 for lx, ly in lamps):
                kept_v[k] = None                                # (a lamp column stands here)
                continue
            kept_v[k] = dict(p, proto="Veg_PlazaTree" if _hash01(p["x"], p["y"]) < 0.5 else "Veg_PlazaTree_B",
                             scale=round(0.78 + 0.2 * _hash01(p["x"], p["y"], 1), 3))
            swapped += 1
    kept_v = [p for p in kept_v if p is not None]
    # V20b (Luc, s15: "the tree clips into the wall"): V14b's court olive at (78.4, 79.6) stood 1.1 m from the library's
    # north block with a 6 m crown. It moves into a tub on the deck (P.TUB_OLIVE); the build stops if its crown reaches
    # a wall: the library, the bridge's pier, the hall's outline, the slot's walls
    x, y = P.TUB_OLIVE
    crown = OLIVE_CROWN_R * P.TUB_OLIVE_SCALE
    gaps = {"library": P.poly_distance(x, y, P.LIB_GROUND), "pier": P.poly_distance(x, y, P.PIER),
            "hall": P.line_distance(x, y, P.MAIN_EAST), "stair slot": P.poly_distance(x, y, P.STAIR_DOWN)}
    what, gap = min(gaps.items(), key=lambda kv: kv[1])
    if gap - crown < 0.3:
        raise RuntimeError(f"plaza: the tub olive's crown reaches the {what} ({gap - crown:.2f} m)")
    global WALLS_NEAR_TUB
    WALLS_NEAR_TUB = (what, round(gap - crown, 2))
    plant("Veg_Olive", x, y, P.DECK_Z + P.TUB_H - 0.1, P.TUB_OLIVE_SCALE)
    for (x, y), olive in P.POTS:                                # thin young olives (2.3 m with the pot) in the pocket's pots
        if olive:
            plant("Veg_Olive_B", x, y, P.surface_z(x, y) + P.POT_H - 0.1, 0.36)
    span = P._unit(*P.PERGOLA_FRONT[0], *P.PERGOLA_FRONT[1])[2]
    for cu, cv in CAFE:
        gx, gy = pergola_point(span * (cu + 0.5) / (P.PERGOLA_COLS - 1), P.PERGOLA_DEPTH * (cv - 0.5) / (P.PERGOLA_ROWS - 1) * 0.9)
        if not P.on_deck(gx, gy):
            continue
        prop("Prop_BistroTable", gx, gy)
        a0 = rng.uniform(0, 2 * math.pi)
        for q in range(3):
            a = a0 + 2 * math.pi * q / 3
            px, py = gx + math.cos(a) * 0.64, gy + math.sin(a) * 0.64
            prop("Prop_BistroChair", px, py, math.atan2(gx - px, -(gy - py)))
    print(f"  plaza: dressing: {len(veg) - len(kept_v)} plants and {len(props) - len(kept_p)} props removed from the plaza, "
          f"{len(plants)} trees and {len(ours)} props of its own, {swapped} olives of the east rows are young staked trees now; "
          f"tub olive's crown {WALLS_NEAR_TUB[1]} m from the {WALLS_NEAR_TUB[0]}")
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
