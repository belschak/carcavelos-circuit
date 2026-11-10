"""Buildings v2: facades instead of grey blocks.

One object per building, named B_<osm id>_<name> (a landmark model can replace it 1:1), with material slots:
wall, glass, roof, steel, glass roof. Campus buildings (from the walk photos): white walls, a glazed ground
floor, dark ribbon windows on each storey, a parapet. Landmarks along the route get their shape:
  Edificio Principal  glass atrium roof with steel ribs
  Grand Auditorium    white wedge, roof sloping along its long axis
  Horizon Hall        cantilevered roof slab
Other buildings: punched windows, Portuguese facade colours, terracotta hip roofs on houses.
"""
import math
import unicodedata

import bmesh
import bpy

import env_common as E

STOREY = 3.5
WALL, GLASS, ROOF, STEEL, GLASS_ROOF = range(5)
OTHER_WALLS = ["#EFE6D2", "#F3F0EA", "#E3C891", "#E9C9BE", "#DCD6CA"]
SPECIAL = {"w665606788": "atrium", "w496301621": "wedge", "w496301627": "cantilever"}


def ascii_name(text):
    return "".join(c for c in unicodedata.normalize("NFKD", text) if c.isalnum() and ord(c) < 128)[:40]


def materials(campus, index):
    wall = E.material("B_wall_white", "#EAEAEC", 0.85) if campus else \
        E.material(f"B_wall_{index % len(OTHER_WALLS)}", OTHER_WALLS[index % len(OTHER_WALLS)], 0.9)
    return [wall,
            E.material("B_glass", "#2E3A46", roughness=0.08, metallic=0.5),
            E.material("B_roof", "#D5D3CE", 0.9) if campus else E.material("B_roof_other", "#BDB8AE", 0.9),
            E.material("B_steel", "#A7ABB2", roughness=0.35, metallic=0.8),
            E.material("B_glass_roof", "#8FB2C9", roughness=0.05, metallic=0.3)]


def pca_axis(pts):
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    sxx = sum((p[0] - cx) ** 2 for p in pts)
    syy = sum((p[1] - cy) ** 2 for p in pts)
    sxy = sum((p[0] - cx) * (p[1] - cy) for p in pts)
    angle = 0.5 * math.atan2(2 * sxy, sxx - syy)
    return (cx, cy), (math.cos(angle), math.sin(angle))


def line_intervals(poly, origin, direction):
    """Parts of the infinite line origin + t*direction that lie inside the polygon, as (t0, t1) pairs."""
    ts = []
    ox, oy = origin
    dx, dy = direction
    for (ax, ay), (bx, by) in zip(poly, poly[1:] + poly[:1]):
        ex, ey = bx - ax, by - ay
        den = dx * ey - dy * ex
        if abs(den) < 1e-9:
            continue
        t = ((ax - ox) * ey - (ay - oy) * ex) / den
        u = ((ax - ox) * dy - (ay - oy) * dx) / den
        if 0 <= u < 1:
            ts.append(t)
    ts.sort()
    return [(ts[k], ts[k + 1]) for k in range(0, len(ts) - 1, 2)]


WALL_TILE_M = 3.0          # facade_white texture = 3 m x 3 m
WINDOW_TILE_M = 6.0        # window_band texture = 6 m wide, one band high


def apply_uvs(bm):
    """World-space UVs in metres: roofs from above, walls along the wall, window bands as 0..1 strips."""
    uv = bm.loops.layers.uv.verify()
    bm.normal_update()
    for f in bm.faces:
        n = f.normal
        if abs(n.z) > 0.7:
            for loop in f.loops:
                loop[uv].uv = (loop.vert.co.x / WALL_TILE_M, loop.vert.co.y / WALL_TILE_M)
            continue
        h = math.hypot(n.x, n.y) or 1.0
        tx, ty = -n.y / h, n.x / h                      # horizontal direction along the wall
        if f.material_index == GLASS:
            zs = [l.vert.co.z for l in f.loops]
            lo, hi = min(zs), max(zs)
            for loop in f.loops:
                co = loop.vert.co
                loop[uv].uv = ((co.x * tx + co.y * ty) / WINDOW_TILE_M, (co.z - lo) / max(hi - lo, 0.01))
        else:
            for loop in f.loops:
                co = loop.vert.co
                loop[uv].uv = ((co.x * tx + co.y * ty) / WALL_TILE_M, co.z / WALL_TILE_M)


def add_quad(bm, corners, mat, flip=False):
    vs = [bm.verts.new(c) for c in corners]
    face = bm.faces.new(vs[::-1] if flip else vs)
    face.material_index = mat
    return face


CANOPY_SLAB = 0.3          # m, roof slab of an open canopy (osm building=roof)
PILLAR = 0.3               # m, square pillar side
PILLAR_STEP = 7.0          # m, at most this far apart along the edge


def build_canopy(b, index):
    """Open canopy (osm building=roof): a thin roof slab on slim pillars, open underneath so a kart can pass.
    No pillar stands on the road: each keeps road half-width + 0.5 m from the route centreline."""
    pts = b["points"]
    grounds = [E.ground_z(x, y) for x, y in pts]
    top = b["roof_z"] if b.get("roof_z") is not None else max(grounds) + b["height"]
    low = top - CANOPY_SLAB
    vol = bmesh.new()
    bottom = [vol.verts.new((x, y, low)) for x, y in pts]
    tops = [vol.verts.new((x, y, top)) for x, y in pts]
    n = len(pts)
    for k in range(n):
        vol.faces.new((bottom[k], bottom[(k + 1) % n], tops[(k + 1) % n], tops[k])).material_index = WALL
    vol.faces.new(tops).material_index = ROOF
    vol.faces.new(bottom[::-1]).material_index = WALL
    cx, cy = sum(x for x, _ in pts) / n, sum(y for _, y in pts) / n
    spots = []
    for k in range(n):
        (ax, ay), (bx, by) = pts[k], pts[(k + 1) % n]
        steps = max(1, math.ceil(math.hypot(bx - ax, by - ay) / PILLAR_STEP))
        spots += [(ax + (bx - ax) * t / steps, ay + (by - ay) * t / steps) for t in range(steps)]
    det = bmesh.new()
    placed = 0
    for x, y in spots:
        d = math.hypot(cx - x, cy - y) or 1.0
        x, y = x + (cx - x) / d * 0.4, y + (cy - y) / d * 0.4          # 0.4 m inside the slab edge
        p = E.nearest_route_point(x, y)
        if p and E.distance_to_route(x, y) < E.half_width(p) + 0.5 + PILLAR / 2:
            continue
        h, g = PILLAR / 2, E.ground_z(x, y) - 0.2
        vs = [det.verts.new((x + sx * h, y + sy * h, z)) for z in (g, low) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        for k in range(4):
            det.faces.new((vs[k], vs[(k + 1) % 4], vs[4 + (k + 1) % 4], vs[4 + k])).material_index = STEEL
        placed += 1
    print(f"  canopy {b['id']}: slab at {low:.1f}-{top:.1f} m, {placed} pillars ({len(spots) - placed} left out for the road)")
    meshes = []
    for name, data in (("B_" + b["id"], vol), ("BD_" + b["id"], det)):
        apply_uvs(data)
        mesh = bpy.data.meshes.new(name)
        data.to_mesh(mesh)
        data.free()
        for m in materials(b["campus"], index):
            mesh.materials.append(m)
        meshes.append(mesh)
    return meshes


def build_one(b, index):
    if b.get("osm_building") == "roof":
        return build_canopy(b, index)
    pts = b["points"]
    grounds = [E.ground_z(x, y) for x, y in pts]
    base = min(grounds) - 1.0
    top_flat = b["roof_z"] if b.get("roof_z") is not None else max(grounds) + b["height"]
    kind = SPECIAL.get(b["id"], "")
    campus = b["campus"]
    (cx, cy), axis = pca_axis(pts)
    proj = [(x - cx) * axis[0] + (y - cy) * axis[1] for x, y in pts]
    pmin, pmax = min(proj), max(proj)

    def roof_z(x, y):
        if kind != "wedge":
            return top_flat
        t = ((x - cx) * axis[0] + (y - cy) * axis[1] - pmin) / max(pmax - pmin, 1.0)
        if axis[0] + axis[1] < 0:                 # high end towards the north-east (the campus side)
            t = 1 - t
        low = max(grounds) + b["height"] * 0.5
        return low + (top_flat - low) * t

    vol = bmesh.new()
    bm = vol
    # main volume: walls + roof (+ bottom), a closed shape the atrium passage can be cut from
    bottom = [bm.verts.new((x, y, base)) for x, y in pts]
    tops = [bm.verts.new((x, y, roof_z(x, y))) for x, y in pts]
    n = len(pts)
    for k in range(n):
        f = bm.faces.new((bottom[k], bottom[(k + 1) % n], tops[(k + 1) % n], tops[k]))
        f.material_index = WALL
    roof = bm.faces.new(tops)
    roof.material_index = GLASS_ROOF if kind == "atrium" else ROOF
    floor = bm.faces.new(bottom[::-1])
    floor.material_index = WALL

    bm = bmesh.new()                                    # details from here on
    blocked = passage_blockers(b)
    # windows on every facade segment
    levels_total = 0
    for k in range(n):
        (ax, ay), (bx, by) = pts[k], pts[(k + 1) % n]
        seg = math.hypot(bx - ax, by - ay)
        if seg < 4.0 or kind == "wedge":
            continue
        ox, oy = (by - ay) / seg, -(bx - ax) / seg          # outward normal (footprints are counter-clockwise)
        ground = E.ground_z((ax + bx) / 2 + ox * 1.5, (ay + by) / 2 + oy * 1.5)
        top = min(roof_z(ax, ay), roof_z(bx, by))
        storeys = int((top - ground) // STOREY)
        if storeys < 1:
            continue
        levels_total = max(levels_total, storeys)
        inset = 0.6 / seg
        off = 0.05
        for s in range(storeys):
            z0 = ground + s * STOREY
            if campus:
                bands = [(z0 + 0.25, z0 + 3.0)] if s == 0 else [(z0 + 0.95, z0 + 2.55)]
                for lo, hi in bands:
                    if hi > top - 0.3:
                        continue
                    p0 = (ax + (bx - ax) * inset + ox * off, ay + (by - ay) * inset + oy * off)
                    p1 = (bx - (bx - ax) * inset + ox * off, by - (by - ay) * inset + oy * off)
                    for q0, q1 in split_around(p0, p1, lo, hi, blocked):
                        add_quad(bm, [(*q0, lo), (*q1, lo), (*q1, hi), (*q0, hi)], GLASS)
            else:
                count = int((seg - 1.2) // 3.0)
                for w in range(count):
                    c = (0.6 + 1.5 + w * 3.0) / seg
                    half = 0.7 / seg
                    lo, hi = z0 + 0.95, z0 + 2.45
                    if hi > top - 0.3:
                        continue
                    p0 = (ax + (bx - ax) * (c - half) + ox * off, ay + (by - ay) * (c - half) + oy * off)
                    p1 = (ax + (bx - ax) * (c + half) + ox * off, ay + (by - ay) * (c + half) + oy * off)
                    for q0, q1 in split_around(p0, p1, lo, hi, blocked):
                        add_quad(bm, [(*q0, lo), (*q1, lo), (*q1, hi), (*q0, hi)], GLASS)

    # parapet band (flat roofs): a 0.6 m rim, outer face only
    # on a drive-on roof (CTT terrace deck) the rim opens where the route crosses it (it stood 0.6 m high
    # across the road at s 464)
    if kind not in ("wedge",) and b.get("osm_building") not in ("house", "roof"):
        on_roof = roof_route_points(b, top_flat)
        for k in range(n):
            (ax, ay), (bx, by) = pts[k], pts[(k + 1) % n]
            for (qx, qy), (rx, ry) in split_around((ax, ay), (bx, by), top_flat, top_flat + 0.6, on_roof):
                add_quad(bm, [(qx, qy, top_flat), (rx, ry, top_flat), (rx, ry, top_flat + 0.6), (qx, qy, top_flat + 0.6)], WALL)

    if kind == "atrium":
        add_atrium_ribs(bm, pts, (cx, cy), axis, top_flat)
    if kind == "cantilever":
        add_cantilever(bm, pts, (cx, cy), top_flat)
    if b.get("osm_building") in ("house", "detached"):
        add_hip_roof(bm, pts, (cx, cy), top_flat)

    meshes = []
    for name, data in (("B_" + b["id"], vol), ("BD_" + b["id"], bm)):
        apply_uvs(data)
        mesh = bpy.data.meshes.new(name)
        data.to_mesh(mesh)
        data.free()
        for m in materials(campus, index):
            mesh.materials.append(m)
        if b.get("osm_building") in ("house", "detached"):
            mesh.materials[ROOF] = E.material("B_roof_tiles", "#C8664A", 0.85)
        meshes.append(mesh)
    return meshes


def passage_blockers(b):
    """Route points where a drive-through passage opens this building's facades: (x, y, floor z)."""
    if not b.get("drive_through"):
        return []
    xs = [p[0] for p in b["points"]]
    ys = [p[1] for p in b["points"]]
    return [(p["x"], p["y"], p["track_z"]) for p in E.route_points()
            if min(xs) - 12 <= p["x"] <= max(xs) + 12 and min(ys) - 12 <= p["y"] <= max(ys) + 12]


def roof_route_points(b, top):
    """Route points that drive on this building's roof (buildings.csv drive_on_roof): (x, y, z)."""
    if not b.get("drive_on_roof"):
        return []
    xs = [p[0] for p in b["points"]]
    ys = [p[1] for p in b["points"]]
    return [(p["x"], p["y"], p["track_z"]) for p in E.route_points()
            if min(xs) - 12 <= p["x"] <= max(xs) + 12 and min(ys) - 12 <= p["y"] <= max(ys) + 12
            and abs(p["track_z"] - top) < 1.0]


def split_around(p0, p1, lo, hi, blocked, clear=E.ROUTE_WIDTH / 2 + 1.5):
    """Window strip p0-p1, minus the parts in front of a passage opening."""
    if not blocked:
        return [(p0, p1)]
    pieces, steps = [], 24
    run = None
    for k in range(steps + 1):
        t = k / steps
        q = (p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t)
        hit = any(math.hypot(q[0] - x, q[1] - y) < clear and z - 1 < hi and lo < z + 7.5 for x, y, z in blocked)
        if not hit and run is None:
            run = q
        if (hit or k == steps) and run is not None:
            end = q
            if math.hypot(end[0] - run[0], end[1] - run[1]) > 1.0:
                pieces.append((run, end))
            run = None
    return pieces


def add_atrium_ribs(bm, pts, centre, axis, top):
    """Steel ribs across the glass roof every 6 m (only where the roof is)."""
    across = (-axis[1], axis[0])
    proj = [(x - centre[0]) * axis[0] + (y - centre[1]) * axis[1] for x, y in pts]
    t = min(proj) + 3.0
    while t < max(proj) - 3.0:
        origin = (centre[0] + axis[0] * t, centre[1] + axis[1] * t)
        for t0, t1 in line_intervals(pts, origin, across):
            if t1 - t0 < 3:
                continue
            for side in (-0.12, 0.12):
                a = (origin[0] + across[0] * (t0 + 0.5) + axis[0] * side, origin[1] + across[1] * (t0 + 0.5) + axis[1] * side)
                c = (origin[0] + across[0] * (t1 - 0.5) + axis[0] * side, origin[1] + across[1] * (t1 - 0.5) + axis[1] * side)
                add_quad(bm, [(*a, top), (*c, top), (*c, top + 0.9), (*a, top + 0.9)], STEEL, flip=side > 0)
        t += 6.0


def add_cantilever(bm, pts, centre, top):
    """Roof slab that sticks out about 2.5 m around the building (Horizon Hall)."""
    r = sum(math.hypot(x - centre[0], y - centre[1]) for x, y in pts) / len(pts)
    s = 1 + 2.5 / max(r, 1.0)
    ring = [(centre[0] + (x - centre[0]) * s, centre[1] + (y - centre[1]) * s) for x, y in pts]
    lo, hi = top + 0.1, top + 0.7
    low = [bm.verts.new((x, y, lo)) for x, y in ring]
    high = [bm.verts.new((x, y, hi)) for x, y in ring]
    n = len(ring)
    for k in range(n):
        f = bm.faces.new((low[k], low[(k + 1) % n], high[(k + 1) % n], high[k]))
        f.material_index = WALL
    bm.faces.new(high).material_index = ROOF
    bm.faces.new(low[::-1]).material_index = WALL


def add_hip_roof(bm, pts, centre, top):
    """Simple pyramid-like hip roof: every eave vertex rises to a ridge point above the centre."""
    peak = bm.verts.new((centre[0], centre[1], top + 2.2))
    eaves = [bm.verts.new((x, y, top + 0.01)) for x, y in pts]
    n = len(eaves)
    for k in range(n):
        f = bm.faces.new((eaves[k], eaves[(k + 1) % n], peak))
        f.material_index = ROOF


def build():
    for index, b in enumerate(E.DATA["buildings"]):
        volume, details = build_one(b, index)
        name = "B_" + b["id"] + ("_" + ascii_name(b["name"]) if b["name"] else "")
        col = E.collection("World_Buildings_Campus" if b["campus"] else "World_Buildings_Other")
        obj = E.new_object(name, volume, col)
        obj["osm_id"] = b["id"]
        obj["height_m"] = b["height"]
        if details.polygons:
            det = E.new_object("BD_" + name[2:], details, col)
            det.parent = obj
