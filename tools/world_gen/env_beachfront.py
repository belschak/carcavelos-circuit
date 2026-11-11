"""Beachfront (V10b, 2026-11-10): the restaurants on the sand, the cove pavilion and the promenade's sea edge.

Evidence: DGT orthophoto 2023 (ortho_west / ortho_south, labels PA and CAP), the team's walk photos of 7 Nov 2026
(K02-K08) and OSM (promenade area w482695608, steps w479747079). Nothing here copies a photo; no signs, no logos.

Buildings (env_buildings.build_one hands these OSM ids over, the object names stay B_<id>_<name> + BD_ child):
  Pizzaria Capricciosa  w213238769  low white restaurant on a plinth: one storey (3.9 m) with a parapet, glazed sea
                                     front, timber deck on the sand with a pergola (light beams, timber slats) over its
                                     north-west part, a low white railing, rooftop plant room
  A Pastorinha          w213238771  its neighbour, lower (3.4 m) and plainer: glazed front behind a white canopy on
                                     slim posts over a stone terrace with a solid white balustrade, long plant band
  cove pavilion         w482697584  one storey, white walls, terracotta hip roof (ortho_south: orange roof by the cove)
  kiosk                 w482697583  small white hut beside it
  promenade house       w213239659  the tiled-roof house on the promenade's land side (ortho_west), same treatment
  fort outworks         w155316895, w228205972  (OSM barrier=city_wall: a ravelin and a wall in front of the Forte de
                                     Sao Juliao): low sloped stone ramparts instead of 10.5 m blocks with windows
The floor of each building is the highest ground under its footprint (the road side); towards the sand the plinth
shows. Everything is placed on E.ground_z at build time, so it follows the terrain if the shore changes.

Promenade (collection World_Promenade, exported with CampusWorld.fbx):
  Promenade_Kerb       granite kerb blocks along the promenade's sea edge: the cove arc and the straight promenade
                       west of it (OSM w482695608 sea edge), kept >= 0.45 m outside the road edge
  Promenade_Stairs     two flights down to the sand with stainless two-rail railings: the cove stair (K04/K05) running
                       along the kerb, and the steps at the west end of the promenade (OSM w479747079)
  Promenade_SeatSteps  two low white seating steps with stone caps on the land side of the cove promenade (K08)
deck_spots() tells env_props / env_vegetation where tables, parasols and palms stand on the restaurant terraces;
on_beach_side() keeps the street props off the sand. The stairs are built for the drop the photos show (about 1 m at
the cove); where the terrain lies higher the lowest steps are under the sand.
"""
import math

import bmesh
import bpy
from mathutils import Vector

import env_common as E

SEA_DIR = (-0.742, -0.670)            # towards the water at the restaurants (perpendicular to the beach there)
COLLECTION = "World_Promenade"

# material slots of the building meshes (unused slots are dropped per mesh)
WALL, GLASS, ROOF, STEEL, PLINTH, DECK, TILES, STONE, ROOF_DARK = range(9)


def building_materials():
    m = E.material
    return [m("B_wall_white", "#EAEAEC", 0.85),                       # same as the campus (textured facade)
            m("B_glass", "#2E3A46", roughness=0.08, metallic=0.5),    # window band texture
            m("beach_roof", "#EFEBE1", 0.9),
            m("B_steel", "#A7ABB2", roughness=0.35, metallic=0.8),
            m("concrete_sand", roughness=0.9),                          # same material as the bench plinths
            timber_material(),
            m("B_roof_tiles", "#C8664A", 0.85),
            stone_material(),
            m("B_roof_dark", "#4A4744", 0.8)]


def timber_material():
    return textured(E.material("timber_deck", "#A2825F", 0.85), "timber_deck", 0.85)


def stone_material():
    return textured(E.material("granite_kerb", "#8E9193", 0.8), "granite_kerb", 0.8)


def textured(mat, base, roughness):
    """Our textures go on right away (env_materials.upgrade), so env_materials.TEXTURED stays untouched."""
    if not mat.get("v10b_textured"):
        import env_materials
        if env_materials.upgrade(mat, base, roughness):
            mat["v10b_textured"] = 1
    return mat


# UV size of one texture tile (metres) per material slot, for the planar mapping
TILE = {WALL: 3.0, GLASS: 6.0, ROOF: 3.0, STEEL: 2.0, PLINTH: 2.0, DECK: 2.0, TILES: 2.0, STONE: 4.0}


# =====================================================================================================
# Mesh builder: faces with their own vertices (flat shading), winding chosen by an outward hint
# =====================================================================================================
class Geo:
    def __init__(self):
        self.verts, self.faces = [], []          # faces: (indices, slot, uvs or None)

    def face(self, corners, mat, out=None, uvs=None):
        pts = [Vector(c) for c in corners]
        if out is not None:
            n = Vector((0.0, 0.0, 0.0))
            for a, b in zip(pts, pts[1:] + pts[:1]):            # Newell normal (robust for any polygon)
                n.x += (a.y - b.y) * (a.z + b.z)
                n.y += (a.z - b.z) * (a.x + b.x)
                n.z += (a.x - b.x) * (a.y + b.y)
            if n.dot(Vector(out)) < 0:
                pts.reverse()
                uvs = uvs[::-1] if uvs else None
        base = len(self.verts)
        self.verts.extend(pts)
        self.faces.append((list(range(base, base + len(pts))), mat, uvs))

    def obox(self, c, u, half_u, half_v, z0, z1, mat, skip=("bottom",)):
        """Box centred at c (x, y), long axis u (unit, horizontal), half sizes along u and across, z0..z1."""
        ux, uy = u
        vx, vy = -uy, ux
        cx, cy = c

        def p(a, b, z):
            return (cx + ux * a + vx * b, cy + uy * a + vy * b, z)
        corners = {(a, b): (a * half_u, b * half_v) for a in (-1, 1) for b in (-1, 1)}
        sides = {"+u": ((1, -1), (1, 1), (ux, uy)), "-u": ((-1, 1), (-1, -1), (-ux, -uy)),
                 "+v": ((1, 1), (-1, 1), (vx, vy)), "-v": ((-1, -1), (1, -1), (-vx, -vy))}
        for key, (k0, k1, out) in sides.items():
            if key in skip:
                continue
            a, b = corners[k0], corners[k1]
            self.face([p(*a, z0), p(*b, z0), p(*b, z1), p(*a, z1)], mat, out=(out[0], out[1], 0.0))
        ring = [corners[k] for k in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        if "top" not in skip:
            self.face([p(a, b, z1) for a, b in ring], mat, out=(0, 0, 1))
        if "bottom" not in skip:
            self.face([p(a, b, z0) for a, b in ring], mat, out=(0, 0, -1))

    def beam(self, a, b, w, h, mat, ends=True):
        """Square-ish bar from a to b (3-D points, centre line), w wide, h high (h along world z for level bars,
        else perpendicular to the bar in its vertical plane)."""
        a, b = Vector(a), Vector(b)
        d = b - a
        flat = Vector((d.x, d.y, 0.0))
        side = Vector((-flat.y, flat.x, 0.0)).normalized() * (w / 2) if flat.length > 1e-6 else Vector((w / 2, 0, 0))
        up = d.cross(side).normalized() * (h / 2)
        if up.z < 0:
            up = -up
        quads = [((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))]
        for (s0, t0), (s1, t1) in quads:
            o0, o1 = side * s0 + up * t0, side * s1 + up * t1
            mid = (o0 + o1) / 2
            self.face([a + o0, a + o1, b + o1, b + o0], mat, out=mid)
        if ends:
            ring = [side * s + up * t for s, t in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            self.face([a + o for o in ring], mat, out=-d)
            self.face([b + o for o in ring], mat, out=d)

    def post(self, x, y, z0, z1, size, mat):
        self.obox((x, y), (1.0, 0.0), size / 2, size / 2, z0, z1, mat)

    def mesh(self, name, materials):
        """bpy mesh with only the material slots that are used; planar UVs in metres where no UVs were given."""
        used = sorted({f[1] for f in self.faces})
        remap = {s: k for k, s in enumerate(used)}
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in self.verts], [], [f[0] for f in self.faces])
        for s in used:
            mesh.materials.append(materials[s])
        mesh.polygons.foreach_set("material_index", [remap[f[1]] for f in self.faces])
        uv = mesh.uv_layers.new(name="UVMap")
        for poly, (idx, slot, uvs) in zip(mesh.polygons, self.faces):
            if uvs:
                for li, (u, v) in zip(poly.loop_indices, uvs):
                    uv.data[li].uv = (u, v)
                continue
            n = poly.normal
            t = TILE.get(slot, 2.0)
            h = math.hypot(n.x, n.y) or 1.0
            for li in poly.loop_indices:
                co = mesh.vertices[mesh.loops[li].vertex_index].co
                if abs(n.z) > 0.7:
                    uv.data[li].uv = (co.x / t, co.y / t)
                else:
                    uv.data[li].uv = ((co.x * -n.y / h + co.y * n.x / h) / t, co.z / t)
        mesh.validate()
        mesh.update()
        return mesh


# =====================================================================================================
# Small geometry helpers
# =====================================================================================================
def edges(pts):
    n = len(pts)
    for k in range(n):
        a, b = pts[k], pts[(k + 1) % n]
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        if length < 1e-6:
            continue
        u = ((b[0] - a[0]) / length, (b[1] - a[1]) / length)
        yield k, a, b, length, u, (u[1], -u[0])        # outward normal of a counter-clockwise polygon


def offset_polygon(pts, d):
    """Polygon moved d metres outwards (negative: inwards), mitred corners (clamped)."""
    n = len(pts)
    out = []
    for k in range(n):
        p0, p1, p2 = pts[k - 1], pts[k], pts[(k + 1) % n]
        n0 = norm((p1[1] - p0[1], -(p1[0] - p0[0])))
        n1 = norm((p2[1] - p1[1], -(p2[0] - p1[0])))
        m = norm((n0[0] + n1[0], n0[1] + n1[1]))
        cos = max(m[0] * n1[0] + m[1] * n1[1], 0.35)
        out.append((p1[0] + m[0] * d / cos, p1[1] + m[1] * d / cos))
    return out


def norm(v):
    length = math.hypot(v[0], v[1]) or 1.0
    return (v[0] / length, v[1] / length)


def lerp2(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def seg_distance(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    l2 = dx * dx + dy * dy
    t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / l2))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


def route_clearance(x, y):
    """Metres from (x, y) to the nearest road edge (centreline segments minus the half width); inf far away."""
    pts = E.route_points()
    n = len(pts)
    best = float("inf")
    for i, p in enumerate(pts):
        if abs(p["x"] - x) > 25 or abs(p["y"] - y) > 25:
            continue
        q = pts[(i + 1) % n]
        best = min(best, seg_distance(x, y, p["x"], p["y"], q["x"], q["y"]) - E.half_width(p))
    return best


def ground(x, y):
    return E.ground_z(x, y)


# =====================================================================================================
# Restaurants
# =====================================================================================================
RESTAURANTS = {
    "w213238769": dict(kind="capricciosa", storey=3.9, parapet=0.3),      # V16g: parapets 0.45 / 0.4 -> 0.3 / 0.35
    "w213238771": dict(kind="pastorinha", storey=3.4, parapet=0.35),
}
# V16g (survey 3.7): Capricciosa is clad in white horizontal boards (the street section's slat texture, 4 boards per
# 0.85 m), A Pastorinha is cream-yellow with a white scalloped parapet. Two more material slots, restaurants only.
BOARDS, PAL = 9, 10
TILE[BOARDS] = 0.85


def restaurant_materials():
    import env_streets
    return building_materials() + [env_streets.mat("slats"), env_streets.mat("palette")]


def pal(colour, n=4):
    """UVs that put a face on one cell of the street palette."""
    import env_streets
    return [env_streets.pal_uv(colour)] * n
SMALL = {
    "w482697584": dict(kind="pavilion", walls=3.1, eave=0.35, pitch=0.55),
    # V10f (survey F): the beach bar stands on promenade level, one level below the car park: from the aisle only its
    # dark low-pitched roof shows (eave 1.3 m, ridge 2.5 m above the car park), not a white house with a tiled roof
    "w213239659": dict(kind="pavilion", walls=3.4, eave=0.5, pitch=0.27, low=1.3, dark_roof=True),
    "w482697583": dict(kind="kiosk", walls=2.7),
    # outworks of the Forte de Sao Juliao (OSM barrier=city_wall, a ravelin and a wall): low stone ramparts, not
    # 10.5 m blocks with windows (they stood in front of the fort in every view from the cove)
    "w155316895": dict(kind="rampart", rise=2.6, talus=1.2, max_top=11.0),
    "w228205972": dict(kind="rampart", rise=1.8, talus=0.4),
}
# V10f (survey E 40/41): from the forecourt the restaurants are plain white rendered walls and parapets behind the
# hedge, no row of windows; the way in is the slatted portal with its stair (env_street_objects)
V10F_PLAIN_LAND_WALLS = True
DECK_DEPTH, DECK_DROP = 6.0, 0.12        # Capricciosa: timber deck depth (m) and how far below the floor its top is
PERGOLA_TO = 15.5                         # ... the pergola covers the deck from its north-west end to here (m)
CANOPY_DEPTH = 4.5                        # A Pastorinha: canopy / terrace depth
_cache = {}


def owns(b):
    return b["id"] in RESTAURANTS or b["id"] in SMALL


def footprint(bid):
    return next(b for b in E.DATA["buildings"] if b["id"] == bid)


def floor_z(pts):
    return max(ground(x, y) for x, y in pts) + 0.05


def sea_front(pts):
    """The longest edge facing the water: (a, length, u along the edge, n outwards)."""
    best = max((e for e in edges(pts) if e[5][0] * SEA_DIR[0] + e[5][1] * SEA_DIR[1] > 0.5), key=lambda e: e[3])
    _k, a, _b, length, u, n = best
    return a, length, u, n


def shared(a, b, others):
    """Edge a-b lies on another restaurant's outline (the party wall)."""
    mid = lerp2(a, b, 0.5)
    return any(min(seg_distance(mid[0], mid[1], *p, *q) for _k, p, q, *_r in edges(o)) < 0.6 for o in others)


def restaurant_layout(bid):
    """Floor height, sea front and terrace geometry of a restaurant (cached: the props use it too)."""
    if bid in _cache:
        return _cache[bid]
    b = footprint(bid)
    pts = b["points"]
    spec = dict(RESTAURANTS[bid])
    # V10f (survey 4.2): the two restaurants stand 1.5-2 m below the forecourt on their own terrace; from the car
    # park only low white roof edges show over the hedge. Floor and storey height come from the street plan.
    import street_plan
    floor = street_plan.RESTAURANT_FLOOR
    spec["storey"] = street_plan.RESTAURANT_STOREY[spec["kind"]]
    a, length, u, n = sea_front(pts)
    lay = dict(points=pts, floor=floor, top=floor + spec["storey"], a=a, length=length, u=u, n=n, spec=spec)
    if spec["kind"] == "capricciosa":
        lay["deck"] = (0.5, length - 3.0, DECK_DEPTH, floor - DECK_DROP)       # t0, t1, depth, top z
    else:
        lay["deck"] = (17.5, length - 0.4, CANOPY_DEPTH, floor - 0.1)
    _cache[bid] = lay
    return lay


def at(lay, t, s, z=None):
    """Point t metres along the sea front and s metres out from it (towards the sand)."""
    a, u, n = lay["a"], lay["u"], lay["n"]
    x, y = a[0] + u[0] * t + n[0] * s, a[1] + u[1] * t + n[1] * s
    return (x, y) if z is None else (x, y, z)


def build_one(b, index):
    """(volume mesh, details mesh) for one of our buildings."""
    if b["id"] in RESTAURANTS:
        return build_restaurant(b)
    spec = SMALL[b["id"]]
    if spec["kind"] == "rampart":
        return build_rampart(b, spec)
    return build_kiosk(b, spec) if spec["kind"] == "kiosk" else build_pavilion(b, spec)


def build_restaurant(b):
    lay = restaurant_layout(b["id"])
    spec, pts, floor, top = lay["spec"], lay["points"], lay["floor"], lay["top"]
    others = [footprint(k)["points"] for k in RESTAURANTS if k != b["id"]]
    base = min(ground(x, y) for x, y in pts) - 0.8
    mats = restaurant_materials()
    cream = spec["kind"] == "pastorinha"
    skin = PAL if cream else BOARDS                     # V16g: cream-yellow render / white boards
    skin_uv = pal("cream") if cream else None

    vol = Geo()                                         # closed main volume: plinth band, wall band, roof, bottom
    for _k, p, q, _l, _u, n in edges(pts):
        out = (n[0], n[1], 0.0)
        vol.face([(*p, base), (*q, base), (*q, floor), (*p, floor)], PLINTH, out=out)
        vol.face([(*p, floor), (*q, floor), (*q, top), (*p, top)], skin, out=out, uvs=skin_uv)
    vol.face([(*p, top) for p in pts], ROOF, out=(0, 0, 1))
    vol.face([(*p, base) for p in pts], WALL, out=(0, 0, -1))

    det = Geo()
    # parapet: a 0.25 m thick rim (outer face, top, inner face)
    inner = offset_polygon(pts, -0.25)
    ptop = top + spec["parapet"]
    m = len(pts)
    for k in range(m):
        p, q, pi, qi = pts[k], pts[(k + 1) % m], inner[k], inner[(k + 1) % m]
        nrm = norm((q[1] - p[1], -(q[0] - p[0])))
        det.face([(*p, top), (*q, top), (*q, ptop), (*p, ptop)], WALL if cream else BOARDS, out=(nrm[0], nrm[1], 0))
        if cream and math.hypot(q[0] - p[0], q[1] - p[1]) > 5.0 and not shared(p, q, others):
            scallops(det, p, q, ptop, nrm)
        det.face([(*p, ptop), (*q, ptop), (*qi, ptop), (*pi, ptop)], ROOF, out=(0, 0, 1))
        det.face([(*pi, top + 0.02), (*qi, top + 0.02), (*qi, ptop), (*pi, ptop)], WALL, out=(-nrm[0], -nrm[1], 0))

    # glazing: tall glass on the sea front and the ends that see the water, punched windows and a door landward
    glass_top = floor + min(3.0, spec["storey"] - 0.7)
    door_done = False
    land_edges = sorted((e for e in edges(pts) if e[5][0] * SEA_DIR[0] + e[5][1] * SEA_DIR[1] < -0.3
                         and not shared(e[1], e[2], others)), key=lambda e: -e[3])
    door_edge = land_edges[0][0] if land_edges else None
    for k, p, q, length, u, n in edges(pts):
        if length < 3.0 or shared(p, q, others):
            continue
        facing = n[0] * SEA_DIR[0] + n[1] * SEA_DIR[1]
        off = (n[0] * 0.04, n[1] * 0.04)
        if facing > -0.3:
            t0, t1 = 0.6, length - 0.6
            glass(det, p, u, off, t0, t1, floor + 0.15, glass_top)
            for t in frange(t0 + 3.0, t1 - 0.5, 3.0):                  # slim white fins between the panes
                c = (p[0] + u[0] * t + n[0] * 0.09, p[1] + u[1] * t + n[1] * 0.09)
                det.obox(c, u, 0.06, 0.09, floor + 0.1, glass_top + 0.05, WALL)
        elif not V10F_PLAIN_LAND_WALLS:
            mid = length / 2
            for t in frange(1.8, length - 1.8, 4.5):
                if k == door_edge and not door_done and abs(t - mid) < 2.3:
                    continue
                glass(det, p, u, off, t - 0.75, t + 0.75, floor + 0.95, floor + 2.45)
            if k == door_edge and not door_done:
                door_done = True
                glass(det, p, u, off, mid - 1.2, mid + 1.2, floor + 0.02, floor + 2.6)
                c = (p[0] + u[0] * mid + n[0] * 0.95, p[1] + u[1] * mid + n[1] * 0.95)
                det.obox(c, u, 2.2, 0.95, floor + 2.75, floor + 2.97, WALL, skip=())     # entrance canopy

    if spec["kind"] == "capricciosa":
        capricciosa_terrace(det, lay)
        roof_boxes(det, pts, top, [(0.40, 0.0, 6.0, 3.6, 1.5), (0.70, 0.0, 3.5, 2.6, 1.1)], lay, steel=3)
    else:
        pastorinha_terrace(det, lay)
        roof_boxes(det, pts, top, [(0.45, 0.0, 12.0, 2.6, 1.2)], lay, steel=2)
    print(f"  beachfront {b['name']}: floor {floor:.1f} m, roof {top:.1f} m (+{spec['parapet']} parapet), "
          f"sea front {lay['length']:.1f} m")
    return vol.mesh("B_" + b["id"], mats), det.mesh("BD_" + b["id"], mats)


def frange(a, b, step):
    t = a
    while t <= b + 1e-6:
        yield t
        t += step


def glass(g, p, u, off, t0, t1, z0, z1):
    """Glass pane on a wall: from t0 to t1 metres along the edge starting at p, UVs for the window band texture."""
    a = (p[0] + u[0] * t0 + off[0], p[1] + u[1] * t0 + off[1])
    b = (p[0] + u[0] * t1 + off[0], p[1] + u[1] * t1 + off[1])
    out = (off[0], off[1], 0.0)
    w = (t1 - t0) / TILE[GLASS]
    g.face([(*a, z0), (*b, z0), (*b, z1), (*a, z1)], GLASS, out=out, uvs=[(0, 0), (w, 0), (w, 1), (0, 1)])


def roof_boxes(g, pts, top, boxes, lay, steel):
    """Rooftop plant rooms: (fraction along the sea front, landward shift, length, depth, height). Each is pulled
    landwards until it lies inside the roof (0.8 m from the parapet); `steel` small grey units beside the first."""
    u, n = lay["u"], lay["n"]
    inner = offset_polygon(pts, -0.8)
    first = None
    for frac, _shift, length, depth, height in boxes:
        placed = None
        for s in [-(depth / 2 + 1.0) - k * 0.5 for k in range(40)]:
            c = at(lay, lay["length"] * frac, s)
            corners = [(c[0] + u[0] * a + n[0] * d, c[1] + u[1] * a + n[1] * d)
                       for a in (-length / 2, length / 2) for d in (-depth / 2, depth / 2)]
            if all(E.inside_polygon(x, y, inner) for x, y in corners):
                placed = c
                break
        if not placed:
            continue
        g.obox(placed, u, length / 2, depth / 2, top, top + height, WALL, skip=("bottom", "top"))
        g.obox(placed, u, length / 2 + 0.08, depth / 2 + 0.08, top + height, top + height + 0.12, ROOF)
        first = first or (placed, length, depth)
    if first:
        (cx, cy), length, depth = first
        for k in range(steel):
            t = (k - (steel - 1) / 2) * 1.9
            c = (cx + u[0] * t + n[0] * (depth / 2 + 1.2), cy + u[1] * t + n[1] * (depth / 2 + 1.2))
            if E.inside_polygon(c[0], c[1], inner):
                g.obox(c, u, 0.6, 0.45, top, top + 0.9, STEEL)


def deck_slab(g, lay, t0, t1, depth, ztop, top_mat, skirt_mat):
    """Terrace slab in front of the sea front: top face, outer and end faces down into the ground."""
    corners = [at(lay, t0, 0.0), at(lay, t1, 0.0), at(lay, t1, depth), at(lay, t0, depth)]
    uvs = [(0.0, 0.0), ((t1 - t0) / 2.0, 0.0), ((t1 - t0) / 2.0, depth / 2.0), (0.0, depth / 2.0)]
    g.face([(*c, ztop) for c in corners], top_mat, out=(0, 0, 1), uvs=uvs)
    n, u = lay["n"], lay["u"]
    for p, q, out in ((corners[1], corners[2], u), (corners[2], corners[3], n), (corners[3], corners[0], (-u[0], -u[1]))):
        zp, zq = ground(*p) - 0.3, ground(*q) - 0.3
        g.face([(*p, min(zp, ztop - 0.2)), (*q, min(zq, ztop - 0.2)), (*q, ztop), (*p, ztop)], skirt_mat,
               out=(out[0], out[1], 0))


def railing_line(g, pts3, mat, post=0.07, rails=(0.5, 0.95), rail=0.05, step=2.0, posts=True):
    """Railing along a polyline of 3-D foot points: posts every <= step m, rails at the given heights."""
    for (a, b) in zip(pts3, pts3[1:]):
        a, b = Vector(a), Vector(b)
        length = (b - a).length
        k = max(1, math.ceil(length / step))
        for j in range(k if posts else 0):
            p = a.lerp(b, j / k)
            g.post(p.x, p.y, p.z - 0.1, p.z + max(rails) + 0.03, post, mat)
        for h in rails:
            g.beam(a + Vector((0, 0, h)), b + Vector((0, 0, h)), rail, rail, mat, ends=False)
    last = Vector(pts3[-1])
    if posts:
        g.post(last.x, last.y, last.z - 0.1, last.z + max(rails) + 0.03, post, mat)


def scallops(g, p, q, z, nrm, pitch=1.5, radius=0.6):
    """White scallops on a parapet (A Pastorinha, survey C06 / C08): flat half-rounds standing on the rim."""
    length = math.hypot(q[0] - p[0], q[1] - p[1])
    k = max(1, int(length // pitch))
    u = ((q[0] - p[0]) / length, (q[1] - p[1]) / length)
    start = (length - k * pitch) / 2
    arc = [(-radius, 0.0), (-radius * 0.72, radius * 0.5), (0.0, radius * 0.72), (radius * 0.72, radius * 0.5), (radius, 0.0)]
    for j in range(k):
        c = start + (j + 0.5) * pitch
        for side in (1.0, -1.0):
            off = 0.02 if side > 0 else 0.23
            g.face([(p[0] + u[0] * (c + a) - nrm[0] * off, p[1] + u[1] * (c + a) - nrm[1] * off, z - 0.02 + h) for a, h in arc],
                   WALL, out=(nrm[0] * side, nrm[1] * side, 0))


def capricciosa_terrace(g, lay):
    """V16g (survey 3.7, Appendix G 2.4 and 2.7). North-west part: the covered terrace, a dark stained deck under a
    cream awning on white steel rafters that fall from the roof edge to an outer beam on posts; its north-west edge a
    white wall with a row of openings. South-east part: an open timber deck with white pergola frames. Along the sea
    wall (capricciosa_plan.SEA_LINE, built by env_capricciosa) a white post-and-rail balustrade."""
    import capricciosa_plan as CP
    import street_plan
    zd = lay["deck"][3]
    u, n = lay["u"], lay["n"]
    (t0, t1), depth = CP.COVERED["t"], CP.COVERED["depth"]
    c = [at(lay, t0, 0.0), at(lay, t1, 0.0), at(lay, t1, depth), at(lay, t0, depth)]
    g.face([(*p, zd) for p in c], PAL, out=(0, 0, 1), uvs=pal("wood_dark"))
    for p, q, out in ((c[1], c[2], u), (c[2], c[3], n), (c[3], c[0], (-u[0], -u[1]))):
        g.face([(*p, zd - 0.6), (*q, zd - 0.6), (*q, zd), (*p, zd)], PLINTH, out=(out[0], out[1], 0))
    zb, ze = lay["top"] - 0.12, zd + CP.COVERED["eave"]
    edge = depth - 0.3
    k = max(1, round((t1 - t0 - 0.4) / CP.COVERED["rafter"]))
    for j in range(k + 1):                                             # white steel rafters, about 1 m apart
        t = t0 + 0.2 + (t1 - t0 - 0.4) * j / k
        g.beam(at(lay, t, 0.05, zb), at(lay, t, edge, ze), 0.06, 0.14, WALL, ends=False)
    g.beam(at(lay, t0, edge, ze - 0.09), at(lay, t1, edge, ze - 0.09), 0.14, 0.2, WALL)
    k = max(1, round((t1 - t0) / CP.COVERED["post_step"]))
    for j in range(k + 1):
        x, y = at(lay, t0 + 0.1 + (t1 - t0 - 0.2) * j / k, edge)
        g.post(x, y, zd, ze - 0.18, 0.12, WALL)
    a = [at(lay, t0 + 0.1, 0.05), at(lay, t1 - 0.1, 0.05), at(lay, t1 - 0.1, edge + 0.25), at(lay, t0 + 0.1, edge + 0.25)]
    zs = [zb + 0.09, zb + 0.09, ze + 0.08, ze + 0.08]
    g.face([(*p, z) for p, z in zip(a, zs)], PAL, out=(0, 0, 1), uvs=pal("cream"))       # the awning, top and underside
    g.face([(*p, z - 0.015) for p, z in zip(a, zs)], PAL, out=(0, 0, -1), uvs=pal("cream"))
    # the white wall with a row of openings on the terrace's north-west edge
    tw, s0, s1 = t0 - 0.2, 0.3, depth
    wu = n
    bays = max(1, int((s1 - s0 - 0.75) // 1.95))
    pier = (s1 - s0 - bays * 1.2) / (bays + 1)
    for z0, z1 in ((zd - 0.3, zd + 0.75), (zd + 1.55, zd + 2.2)):
        g.obox(at(lay, tw, (s0 + s1) / 2), wu, (s1 - s0) / 2, 0.2, z0, z1, WALL, skip=())
    for j in range(bays + 1):
        sc = s0 + pier / 2 + j * (pier + 1.2)
        g.obox(at(lay, tw, sc), wu, pier / 2, 0.2, zd + 0.75, zd + 1.55, WALL, skip=("top", "bottom"))
    # the open deck south-east of it: timber, white pergola frames
    (o0, o1), od = CP.OPEN_DECK["t"], CP.OPEN_DECK["depth"]
    deck_slab(g, lay, o0, o1, od, zd, DECK, PLINTH)
    zp = lay["top"] - 0.15
    stops = [o0 + 1.0 + (o1 - o0 - 2.0) * j / 3 for j in range(4)]
    for t in stops:
        x, y = at(lay, t, od - 0.4)
        g.post(x, y, zd, zp - 0.1, 0.13, WALL)
        g.beam(at(lay, t, 0.05, zp), at(lay, t, od - 0.2, zp), 0.12, 0.2, WALL)
    g.beam(at(lay, stops[0] - 0.3, od - 0.4, zp), at(lay, stops[-1] + 0.3, od - 0.4, zp), 0.14, 0.2, WALL)
    # white balustrade on the sea wall's outer edge, with returns at both ends
    line, off = CP.SEA_LINE, CP.SEA_FACE - 0.22
    zt = street_plan.TERRACE_Z + CP.SEA_TOP
    total, kink = CP.line_length(line), CP.line_length(line[:2])
    pts = [CP.side_pt(line, 0.0, -CP.SEA_BACK + 0.1)] + [CP.side_pt(line, sx, off) for sx in (0.0, kink, total)] \
        + [CP.side_pt(line, total, -CP.SEA_BACK + 0.1)]
    railing_line(g, [(*p, zt) for p in pts], WALL, post=0.08, rails=(0.5, 1.0), rail=0.06, step=2.3)


def pastorinha_terrace(g, lay):
    """V16g (survey 2.7, 2.8): a stone terrace with a solid white balustrade carrying glazed windbreak panels (about
    1.6 m in all) under a white pergola on slim posts."""
    t0, t1, depth, zt = lay["deck"]
    deck_slab(g, lay, t0, t1, depth, zt, PLINTH, PLINTH)
    n, u = lay["n"], lay["u"]
    # solid balustrade on the outer edge and the north-west end
    for (ta, sa), (tb, sb) in (((t0, 0.1), (t0, depth)), ((t0, depth), (t1, depth))):
        a, b = at(lay, ta, sa - 0.1 if sa > 1 else sa), at(lay, tb, sb - 0.1)
        g.beam((*a, zt + 0.43), (*b, zt + 0.43), 0.2, 0.86, WALL)
    zp = lay["floor"] + 2.5
    k = max(1, round((t1 - t0) / 4.5))
    stops = [t0 + 0.4 + (t1 - t0 - 0.7) * j / k for j in range(k + 1)]
    for t in stops:
        x, y = at(lay, t, depth - 0.2)
        g.post(x, y, zt, zp - 0.1, 0.13, WALL)
        g.beam(at(lay, t, 0.05, zp), at(lay, t, depth + 0.2, zp), 0.12, 0.2, WALL)
    for s in (depth - 0.2, depth / 2):
        g.beam(at(lay, t0, s, zp + 0.16), at(lay, t1, s, zp + 0.16), 0.1, 0.12, WALL)
    for ta, tb in zip(stops, stops[1:]):                                # glass between the posts, seen from both sides
        a, b = at(lay, ta + 0.1, depth - 0.2), at(lay, tb - 0.1, depth - 0.2)
        w = (tb - ta) / TILE[GLASS]
        for side in (1.0, -1.0):
            g.face([(*a, zt + 0.86), (*b, zt + 0.86), (*b, zt + 1.62), (*a, zt + 1.62)], GLASS,
                   out=(n[0] * side, n[1] * side, 0), uvs=[(0, 0.1), (w, 0.1), (w, 0.6), (0, 0.6)])
        g.beam((*a, zt + 1.64), (*b, zt + 1.64), 0.05, 0.05, WALL, ends=False)
    _ = u


def deck_spots(bid):
    """Where env_props puts furniture on a terrace: list of (kind, x, y, z, rot) with kind 'table' (a table and
    chairs), 'parasol' (table, chairs and a parasol) or 'palm' (env_vegetation). Deterministic."""
    lay = restaurant_layout(bid)
    t0, t1, depth, zd = lay["deck"]
    u = lay["u"]
    rot = math.atan2(u[1], u[0])
    spots = []
    if lay["spec"]["kind"] == "capricciosa":
        for t in frange(t0 + 2.2, PERGOLA_TO - 1.5, 3.3):
            for s in (1.8, 4.3):
                spots.append(("table", *at(lay, t + (0.8 if s > 3 else 0.0), s), zd, rot))
        spots.append(("table", *at(lay, PERGOLA_TO + 1.3, 4.2), zd, rot))
        for k, t in enumerate(frange(PERGOLA_TO + 3.5, t1 - 1.0, 3.0)):      # palms and parasols alternate
            if k % 2 == 0:
                spots.append(("palm", *at(lay, t, 2.3), zd, rot))        # >= 2 m from the glass front
            else:
                spots.append(("parasol", *at(lay, t, 4.2), zd, rot))
    else:
        for t in frange(t0 + 1.8, t1 - 1.6, 3.2):
            spots.append(("table", *at(lay, t, 2.3), zd, rot))
    return spots


# =====================================================================================================
# Pavilion, promenade house, kiosk
# =====================================================================================================
def rect_frame(pts):
    """Centre, long axis, half length and half width of a (near) rectangular footprint."""
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    _k, a, b, length, u, _n = max(edges(pts), key=lambda e: e[3])
    v = (-u[1], u[0])
    pu = [(p[0] - cx) * u[0] + (p[1] - cy) * u[1] for p in pts]
    pv = [(p[0] - cx) * v[0] + (p[1] - cy) * v[1] for p in pts]
    ccx = cx + u[0] * (max(pu) + min(pu)) / 2 + v[0] * (max(pv) + min(pv)) / 2
    ccy = cy + u[1] * (max(pu) + min(pu)) / 2 + v[1] * (max(pv) + min(pv)) / 2
    return (ccx, ccy), u, (max(pu) - min(pu)) / 2, (max(pv) - min(pv)) / 2


def build_pavilion(b, spec):
    """White walls, windows and a door on the long sides, terracotta hip roof with a small eave."""
    pts = b["points"]
    floor = floor_z(pts)
    base = min(ground(x, y) for x, y in pts) - 0.8
    top = floor + spec["walls"]
    tiles = ROOF_DARK if spec.get("dark_roof") else TILES
    if spec.get("low"):                 # floor on the low (promenade) side, eave `low` m above the high (car park) side
        top = max(ground(x, y) for x, y in pts) + spec["low"]
        floor = min(min(ground(x, y) for x, y in pts) + 0.15, top - 2.4)
    mats = building_materials()
    vol = Geo()
    for _k, p, q, _l, _u, n in edges(pts):
        out = (n[0], n[1], 0.0)
        vol.face([(*p, base), (*q, base), (*q, floor), (*p, floor)], PLINTH, out=out)
        vol.face([(*p, floor), (*q, floor), (*q, top), (*p, top)], WALL, out=out)
    vol.face([(*p, top) for p in pts], WALL, out=(0, 0, 1))
    vol.face([(*p, base) for p in pts], WALL, out=(0, 0, -1))
    # hip roof over the eave rectangle
    (cx, cy), u, hl, hw = rect_frame(pts)
    v = (-u[1], u[0])
    e = spec["eave"]
    L, W = hl + e, hw + e
    rise = W * spec["pitch"]
    ridge = max(L - W, 0.3)

    def P(a, s, z):
        return (cx + u[0] * a + v[0] * s, cy + u[1] * a + v[1] * s, z)
    ze, zr = top + 0.05, top + 0.05 + rise
    c = [P(-L, -W, ze), P(L, -W, ze), P(L, W, ze), P(-L, W, ze)]
    r0, r1 = P(-ridge, 0, zr), P(ridge, 0, zr)
    vol.face([c[0], c[1], r1, r0], tiles, out=(-v[0], -v[1], 1))
    vol.face([c[2], c[3], r0, r1], tiles, out=(v[0], v[1], 1))
    vol.face([c[1], c[2], r1], tiles, out=(u[0], u[1], 1))
    vol.face([c[3], c[0], r0], tiles, out=(-u[0], -u[1], 1))
    vol.face(c, WALL, out=(0, 0, -1))                                  # soffit
    det = Geo()
    for k in range(4):                                                  # fascia under the tiles
        p, q = c[k], c[(k + 1) % 4]
        mid = ((p[0] + q[0]) / 2 - cx, (p[1] + q[1]) / 2 - cy, 0)
        det.face([(p[0], p[1], ze - 0.16), (q[0], q[1], ze - 0.16), (q[0], q[1], ze), (p[0], p[1], ze)], WALL, out=mid)
    # windows on both long sides, a wide door on the side facing the water (south)
    for side in (1, -1):
        n = (v[0] * side, v[1] * side)
        p = (cx - u[0] * hl + n[0] * (hw + 0.04), cy - u[1] * hl + n[1] * (hw + 0.04))
        off = (n[0] * 0.0001, n[1] * 0.0001)
        door = n[1] < 0
        count = max(2, int(hl * 2 // 3.6))
        for j in range(count):
            t = (j + 0.5) * 2 * hl / count
            if door and j == count // 2:
                glass(det, p, u, off, t - 1.1, t + 1.1, floor + 0.02, floor + 2.5)
            else:
                glass(det, p, u, off, t - 0.6, t + 0.6, floor + 0.9, floor + 2.3)
    for side in (1, -1):                                                # small windows on the ends
        n = (u[0] * side, u[1] * side)
        p = (cx + n[0] * (hl + 0.04) - v[0] * hw * side, cy + n[1] * (hl + 0.04) - v[1] * hw * side)
        along = (v[0] * side, v[1] * side)
        glass(det, p, along, (n[0] * 1e-4, n[1] * 1e-4), hw - 0.5, hw + 0.5, floor + 0.9, floor + 2.2)
    print(f"  beachfront {b['name'] or b['id']}: pavilion, floor {floor:.1f} m, eave {top:.1f} m, ridge {zr:.1f} m")
    return vol.mesh("B_" + b["id"], mats), det.mesh("BD_" + b["id"], mats)


def build_rampart(b, spec):
    """Fort outwork: sloped stone walls (talus) rising `rise` m above the highest ground, a 0.9 m parapet."""
    pts = b["points"]
    base = min(ground(x, y) for x, y in pts) - 1.0
    top = min(max(ground(x, y) for x, y in pts) + spec["rise"], spec.get("max_top", 99.0))
    stone = E.material("fort_stone", "#C4B08A", 0.95)
    mats = [stone, E.material("fort_top", "#A99A7B", 0.95)]
    inner = offset_polygon(pts, -spec["talus"])
    rim = offset_polygon(pts, -spec["talus"] - 0.6)
    vol = Geo()
    m = len(pts)
    for k in range(m):
        j = (k + 1) % m
        p, q, pi, qi, ri, si = pts[k], pts[j], inner[k], inner[j], rim[k], rim[j]
        nrm = norm((q[1] - p[1], -(q[0] - p[0])))
        vol.face([(*p, base), (*q, base), (*qi, top), (*pi, top)], 0, out=(nrm[0], nrm[1], 0.3))
        vol.face([(*pi, top), (*qi, top), (*qi, top + 0.9), (*pi, top + 0.9)], 0, out=(nrm[0], nrm[1], 0))
        vol.face([(*pi, top + 0.9), (*qi, top + 0.9), (*si, top + 0.9), (*ri, top + 0.9)], 1, out=(0, 0, 1))
        vol.face([(*ri, top + 0.9), (*si, top + 0.9), (*si, top), (*ri, top)], 0, out=(-nrm[0], -nrm[1], 0))
    vol.face([(*p, top) for p in rim], 1, out=(0, 0, 1))
    vol.face([(*p, base) for p in pts], 0, out=(0, 0, -1))
    print(f"  beachfront {b['id']}: fort outwork, {top - base:.1f} m of sloped wall")
    return vol.mesh("B_" + b["id"], mats), Geo().mesh("BD_" + b["id"], mats)


def build_kiosk(b, spec):
    pts = b["points"]
    floor = floor_z(pts)
    base = min(ground(x, y) for x, y in pts) - 0.6
    top = floor + spec["walls"]
    mats = building_materials()
    vol = Geo()
    for _k, p, q, _l, _u, n in edges(pts):
        out = (n[0], n[1], 0.0)
        vol.face([(*p, base), (*q, base), (*q, top), (*p, top)], WALL, out=out)
    vol.face([(*p, top) for p in pts], WALL, out=(0, 0, 1))
    vol.face([(*p, base) for p in pts], WALL, out=(0, 0, -1))
    det = Geo()
    (cx, cy), u, hl, hw = rect_frame(pts)
    det.obox((cx, cy), u, hl + 0.35, hw + 0.35, top, top + 0.18, ROOF, skip=())
    v = (-u[1], u[0])
    for side in (1, -1):
        n = (v[0] * side, v[1] * side)
        p = (cx - u[0] * hl + n[0] * (hw + 0.03), cy - u[1] * hl + n[1] * (hw + 0.03))
        glass(det, p, u, (n[0] * 1e-4, n[1] * 1e-4), 0.4, 2 * hl - 0.4, floor + 1.0, floor + 2.1)
    return vol.mesh("B_" + b["id"], mats), det.mesh("BD_" + b["id"], mats)


# =====================================================================================================
# Promenade: kerb, stairs, seating steps
# =====================================================================================================
# Sea edge of the OSM promenade area w482695608 (checked against the ortho): the cove arc from its south-east end
# round to the Fonte dos Ingleses ramp, and the straight promenade from that ramp to its west end by Capricciosa.
KERB_LINES = [
    dict(name="cove", sea=("point", (-66.0, -186.0)),
         points=[(-34.0, -194.2), (-33.1, -183.7), (-36.7, -172.2), (-47.2, -160.2), (-54.6, -155.5), (-63.6, -152.9),
                 (-71.7, -152.1), (-80.0, -153.5), (-89.1, -157.5), (-96.6, -164.2), (-102.3, -171.6), (-104.8, -180.5)]),
    dict(name="west", sea=("dir", SEA_DIR),
         points=[(-126.4, -156.6), (-147.7, -150.0), (-167.7, -133.3), (-177.4, -127.7), (-184.1, -125.4),
                 (-220.4, -102.1), (-249.1, -86.3), (-249.0, -89.9), (-259.6, -90.2)]),
]
KERB_W = 0.4                  # m, width of the kerb blocks
KERB_ROAD_GAP = 0.45          # m, the kerb's inner face stays this far outside the road edge
STAIR_RUN, STAIR_RISE = 0.32, 0.15
STAIRS = [   # name, kerb line, arc position of the top (nearest point to `top`), width, min drop, landing length
    dict(name="cove", line="cove", top=(-59.0, -153.4), width=2.1, min_drop=1.0, landing=2.6),
]
WEST_STEPS = dict(top=(-264.6, -89.6), foot=(-264.7, -93.6), width=2.3, min_drop=0.6)    # OSM w479747079
SEAT_STEPS = [(-34.1, -160.8), (-29.9, -167.0), (-27.3, -172.5), (-25.0, -181.7), (-24.2, -188.5)]   # K08


def catmull(points, step=1.0):
    """Smooth polyline through the points (Catmull-Rom), sampled about every `step` metres."""
    P = [points[0]] + list(points) + [points[-1]]
    out = []
    for k in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[k - 1], P[k], P[k + 1], P[k + 2]
        seg = math.hypot(p2[0] - p1[0], p2[1] - p1[1])
        n = max(1, int(seg / step))
        for j in range(n):
            t = j / n
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[i]) + (-p0[i] + p2[i]) * t + (2 * p0[i] - 5 * p1[i] + 4 * p2[i] - p3[i]) * t2
                                    + (-p0[i] + 3 * p1[i] - 3 * p2[i] + p3[i]) * t3) for i in range(2)))
    out.append(tuple(points[-1]))
    return out


def kerb_samples(line):
    """Kerb centre line: [(x, y, mx, my, s)] with m = unit normal towards the sand and s = arc length; pushed out
    where the road would touch it (the route crosses the OSM edge at the top of the cove)."""
    pts = catmull(line["points"])
    out = []
    for k, (x, y) in enumerate(pts):
        a, b = pts[max(k - 1, 0)], pts[min(k + 1, len(pts) - 1)]
        t = norm((b[0] - a[0], b[1] - a[1]))
        m = (-t[1], t[0])
        kind, ref = line["sea"]
        toward = norm((ref[0] - x, ref[1] - y)) if kind == "point" else ref
        if m[0] * toward[0] + m[1] * toward[1] < 0:
            m = (-m[0], -m[1])
        for _ in range(4):
            gap = route_clearance(x, y) - KERB_W / 2 - KERB_ROAD_GAP
            if gap >= 0:
                break
            x, y = x + m[0] * (-gap + 0.05), y + m[1] * (-gap + 0.05)
        out.append([x, y, m[0], m[1]])
    s = 0.0
    for k, q in enumerate(out):
        if k:
            s += math.hypot(q[0] - out[k - 1][0], q[1] - out[k - 1][1])
        q.append(s)
    return out


def kerb_at(samples, s):
    """Interpolated (x, y, mx, my) at arc length s."""
    for a, b in zip(samples, samples[1:]):
        if a[4] <= s <= b[4]:
            t = (s - a[4]) / max(b[4] - a[4], 1e-6)
            m = norm((a[2] + (b[2] - a[2]) * t, a[3] + (b[3] - a[3]) * t))
            return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, m[0], m[1])
    q = samples[0] if s < samples[0][4] else samples[-1]
    return (q[0], q[1], q[2], q[3])


def kerb_top(x, y, mx, my):
    """Kerb top: 5 cm above the promenade on its land side (the road surface where the road runs along it)."""
    return max(ground(x - mx * 0.7, y - my * 0.7), ground(x, y)) + 0.05


def build_kerb(g, samples, skip):
    """Granite kerb strip; `skip` = arc ranges left open (where a stair cuts the edge)."""
    h = KERB_W / 2
    rows = []
    for x, y, mx, my, s in samples:
        zt = kerb_top(x, y, mx, my)
        zo = min(ground(x + mx * (h + 0.3), y + my * (h + 0.3)), zt - 0.3) - 0.25
        zi = min(ground(x - mx * (h + 0.05), y - my * (h + 0.05)), zt) - 0.12
        rows.append(((x - mx * h, y - my * h), (x + mx * h, y + my * h), zt, zo, zi, s, (mx, my)))
    for r0, r1 in zip(rows, rows[1:]):
        smid = (r0[5] + r1[5]) / 2
        if any(a <= smid <= b for a, b in skip):
            continue
        (i0, o0, t0, b0, c0, s0, m0), (i1, o1, t1, b1, c1, s1, m1) = r0, r1
        u0, u1 = s0 / 4.0, s1 / 4.0
        m = norm((m0[0] + m1[0], m0[1] + m1[1]))
        g.face([(*i0, t0), (*i1, t1), (*o1, t1), (*o0, t0)], STONE, out=(0, 0, 1),
               uvs=[(u0, 0.0), (u1, 0.0), (u1, 0.33), (u0, 0.33)])
        g.face([(*o0, t0), (*o1, t1), (*o1, b1), (*o0, b0)], STONE, out=(m[0], m[1], 0),
               uvs=[(u0, 0.36), (u1, 0.36), (u1, 1.0), (u0, 1.0)])
        g.face([(*i0, c0), (*i1, c1), (*i1, t1), (*i0, t0)], STONE, out=(-m[0], -m[1], 0),
               uvs=[(u0, 0.6), (u1, 0.6), (u1, 0.36), (u0, 0.36)])
    return rows


def band(samples, s0, s1, w0, w1, z, zlow, g, mat, faces=("top", "front", "sides")):
    """A solid piece between arc positions s0..s1 and offsets w0..w1 (towards the sand), top at z, sides down to
    zlow. 'front' = the face at s1 (the riser of a step going down with s)."""
    a, b = kerb_at(samples, s0), kerb_at(samples, s1)

    def P(q, w, zz):
        return (q[0] + q[2] * w, q[1] + q[3] * w, zz)
    d = norm((b[0] - a[0], b[1] - a[1]))
    if "top" in faces:
        g.face([P(a, w0, z), P(b, w0, z), P(b, w1, z), P(a, w1, z)], mat, out=(0, 0, 1))
    if "front" in faces:
        g.face([P(b, w0, z), P(b, w1, z), P(b, w1, zlow), P(b, w0, zlow)], mat, out=(d[0], d[1], 0))
    if "back" in faces:
        g.face([P(a, w0, z), P(a, w1, z), P(a, w1, zlow), P(a, w0, zlow)], mat, out=(-d[0], -d[1], 0))
    if "sides" in faces:
        g.face([P(a, w1, z), P(b, w1, z), P(b, w1, zlow), P(a, w1, zlow)], mat, out=(a[2], a[3], 0))


def build_cove_stair(g, samples, spec):
    """Stone flight along the kerb on the sand side (K04/K05): a landing at promenade level at its top (east) end,
    then the steps down towards the west; a level two-rail railing on the kerb along the whole flight (and 3 m
    past its foot), a sloping one on its sand side. Built for at least `min_drop` metres (the photos show about
    1 m); where the sand lies higher, the lowest steps are simply under it."""
    s_top = min(samples, key=lambda q: math.hypot(q[0] - spec["top"][0], q[1] - spec["top"][1]))[4]
    q = kerb_at(samples, s_top)
    z_top = kerb_top(*q) - 0.03
    w0, w1 = KERB_W / 2, KERB_W / 2 + spec["width"]
    land = spec["landing"]
    foot_probe = kerb_at(samples, s_top + land + 6 * STAIR_RUN)
    z_sand = ground(foot_probe[0] + foot_probe[2] * (w0 + w1) / 2, foot_probe[1] + foot_probe[3] * (w0 + w1) / 2)
    drop = max(z_top - z_sand, spec["min_drop"])
    n = max(2, round(drop / STAIR_RISE))
    rise = drop / n
    zlow = min(z_sand, z_top - drop) - 0.3
    band(samples, s_top, s_top + land, -KERB_W / 2, w1, z_top, zlow, g, STONE, faces=("top", "back", "sides"))
    s = s_top + land
    for k in range(n - 1):
        z = z_top - (k + 1) * rise
        band(samples, s, s + STAIR_RUN, w0, w1, z, zlow, g, STONE)
        s += STAIR_RUN
    s_foot = s
    # railings: level on the kerb (promenade side) from the landing to 3 m past the foot; sloping on the sand side
    level = [kerb_at(samples, t) for t in frange(s_top + land, s_foot + 3.0, 1.0)]
    railing_line(g, [(p[0] + p[2] * 0.12, p[1] + p[3] * 0.12, kerb_top(*p)) for p in level], STEEL_SLOT,
                 post=0.05, rails=(0.5, 1.0), rail=0.045, step=1.8)
    slope = []
    for t in frange(s_top, s_foot, (s_foot - s_top) / 6):
        p = kerb_at(samples, t)
        k = (t - s_top - land) / STAIR_RUN
        z = z_top if k <= 0 else z_top - min(k + 1, n) * rise
        slope.append((p[0] + p[2] * (w1 - 0.08), p[1] + p[3] * (w1 - 0.08), z))
    railing_line(g, slope, STEEL_SLOT, post=0.05, rails=(0.5, 1.0), rail=0.045, step=1.8)
    print(f"  beachfront cove stair: top {z_top:.2f} m, sand {z_sand:.2f} m, {n} risers of {rise:.2f} m")
    return (s_top - 0.05, s_top + land)               # the kerb stays open where the landing is


STEEL_SLOT = 8               # stainless steel railings (promenade object material list)
TILE[STEEL_SLOT] = 2.0


def build_west_steps(g, spec):
    """Straight flight from the west end of the promenade down to the main beach (OSM w479747079), railings on
    both sides."""
    (tx, ty), (fx, fy) = spec["top"], spec["foot"]
    d = norm((fx - tx, fy - ty))
    length = math.hypot(fx - tx, fy - ty)
    side = (-d[1], d[0])
    z_top = ground(tx - d[0] * 0.8, ty - d[1] * 0.8) + 0.05
    z_sand = ground(fx + d[0] * 0.8, fy + d[1] * 0.8)
    drop = max(z_top - z_sand, spec["min_drop"])
    n = max(2, round(drop / STAIR_RISE))
    rise = drop / n
    run = min(STAIR_RUN, length / max(n - 1, 1))
    zlow = min(z_sand, z_top - drop) - 0.3
    hw = spec["width"] / 2
    for k in range(n - 1):
        a = (tx + d[0] * k * run, ty + d[1] * k * run)
        c = (a[0] + d[0] * run / 2, a[1] + d[1] * run / 2)
        g.obox(c, d, run / 2, hw, zlow, z_top - (k + 1) * rise, STONE)
    end = (tx + d[0] * (n - 1) * run, ty + d[1] * (n - 1) * run)
    for sgn in (1, -1):
        o = (side[0] * sgn * (hw - 0.06), side[1] * sgn * (hw - 0.06))
        pts = [(tx - d[0] * 0.6 + o[0], ty - d[1] * 0.6 + o[1], z_top),
               (tx + o[0], ty + o[1], z_top),
               (end[0] + o[0], end[1] + o[1], z_top - (n - 1) * rise)]
        railing_line(g, pts, STEEL_SLOT, post=0.05, rails=(0.5, 1.0), rail=0.045, step=1.8)
    print(f"  beachfront west steps: top {z_top:.2f} m, sand {z_sand:.2f} m, {n} risers of {rise:.2f} m")


def build_seat_steps(g):
    """Two low seating steps (white risers, grey stone caps) on the land side of the cove promenade (K08)."""
    pts = catmull(SEAT_STEPS, 1.0)
    rows = []
    for k, (x, y) in enumerate(pts):
        a, b = pts[max(k - 1, 0)], pts[min(k + 1, len(pts) - 1)]
        t = norm((b[0] - a[0], b[1] - a[1]))
        m = (-t[1], t[0])
        if m[0] * 1.0 + m[1] * 0.0 < 0:                # towards the land (east)
            m = (-m[0], -m[1])
        z0 = ground(x - m[0] * 0.8, y - m[1] * 0.8)
        rows.append((x, y, m, z0, ground(x + m[0] * 1.6, y + m[1] * 1.6)))
    for (x0, y0, m0, z0, zb0), (x1, y1, m1, z1, zb1) in zip(rows, rows[1:]):
        for tier, (w0, w1) in enumerate(((0.0, 0.6), (0.6, 1.2))):
            h0, h1 = z0 + 0.45 * (tier + 1), z1 + 0.45 * (tier + 1)
            f0 = z0 + 0.45 * tier - (0.1 if tier == 0 else 0.0)
            f1 = z1 + 0.45 * tier - (0.1 if tier == 0 else 0.0)
            A0, A1 = (x0 + m0[0] * w0, y0 + m0[1] * w0), (x1 + m1[0] * w0, y1 + m1[1] * w0)
            B0, B1 = (x0 + m0[0] * w1, y0 + m0[1] * w1), (x1 + m1[0] * w1, y1 + m1[1] * w1)
            g.face([(*A0, f0), (*A1, f1), (*A1, h1 - 0.06), (*A0, h0 - 0.06)], WALL, out=(-m0[0], -m0[1], 0))
            g.face([(*A0, h0 - 0.06), (*A1, h1 - 0.06), (*A1, h1), (*A0, h0)], STONE, out=(-m0[0], -m0[1], 0))
            g.face([(*A0, h0), (*A1, h1), (*B1, h1), (*B0, h0)], STONE, out=(0, 0, 1))
        back0, back1 = (x0 + m0[0] * 1.2, y0 + m0[1] * 1.2), (x1 + m1[0] * 1.2, y1 + m1[1] * 1.2)
        g.face([(*back0, min(zb0, z0 + 0.9) - 0.2), (*back1, min(zb1, z1 + 0.9) - 0.2), (*back1, z1 + 0.9),
                (*back0, z0 + 0.9)], WALL, out=(m0[0], m0[1], 0))
    for (x, y, m, z, zb), nb in ((rows[0], rows[1]), (rows[-1], rows[-2])):          # end faces
        t = (x - nb[0], y - nb[1])
        quad = [(x, y, z - 0.1), (x + m[0] * 1.2, y + m[1] * 1.2, z - 0.1),
                (x + m[0] * 1.2, y + m[1] * 1.2, z + 0.9), (x + m[0] * 0.6, y + m[1] * 0.6, z + 0.9),
                (x + m[0] * 0.6, y + m[1] * 0.6, z + 0.45), (x, y, z + 0.45)]
        g.face(quad, WALL, out=(t[0], t[1], 0))


def promenade_materials():
    return building_materials() + [E.material("steel", roughness=0.3, metallic=0.85)]   # same call as env_props


def build_promenade():
    """Kerb, stairs and seating steps along the promenade (collection World_Promenade)."""
    col = E.collection(COLLECTION)
    mats = promenade_materials()
    kerb, stairs = Geo(), Geo()
    total = 0
    for line in KERB_LINES:
        samples = kerb_samples(line)
        skip = []
        for spec in STAIRS:
            if spec["line"] == line["name"]:
                skip.append(build_cove_stair(stairs, samples, spec))
        build_kerb(kerb, samples, skip)
        total += samples[-1][4]
    build_west_steps(stairs, WEST_STEPS)
    seats = Geo()
    build_seat_steps(seats)
    for name, g in (("Promenade_Kerb", kerb), ("Promenade_Stairs", stairs), ("Promenade_SeatSteps", seats)):
        E.new_object(name, g.mesh(name, mats), col)
    print(f"  beachfront promenade: kerb {total:.0f} m, {len(STAIRS) + 1} stairs, seating steps")


def kerb_lines():
    """The kerb centre lines as built (for env_props / env_vegetation keep-clear rules): list of [(x, y), ...]."""
    if "kerb" not in _cache:
        _cache["kerb"] = [[(q[0], q[1]) for q in kerb_samples_cached(line)] for line in KERB_LINES]
    return _cache["kerb"]


def on_beach_side(x, y):
    """True if (x, y) lies on the sand side of the promenade kerb (within 40 m of it), i.e. on the beach."""
    best, side = float("inf"), False
    for line in KERB_LINES:
        for q in kerb_samples_cached(line):
            d = math.hypot(q[0] - x, q[1] - y)
            if d < best:
                best = d
                side = (x - q[0]) * q[2] + (y - q[1]) * q[3] > 0
    return best < 40.0 and side


def kerb_samples_cached(line):
    key = "samples_" + line["name"]
    if key not in _cache:
        _cache[key] = kerb_samples(line)
    return _cache[key]
