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
import env_beachfront           # V10b: beachfront restaurants, cove pavilion, promenade edge
import env_campus               # V10c2: the Nova SBE buildings on and west of the spine, each as itself

STOREY = 3.5
WALL, GLASS, ROOF, STEEL, GLASS_ROOF = range(5)
OTHER_WALLS = ["#EFE6D2", "#F3F0EA", "#E3C891", "#E9C9BE", "#DCD6CA"]
SPECIAL = {"w665606788": "atrium", "w496301621": "wedge", "w496301627": "cantilever", "w622493089": "residence"}
# V10c: the glass roof and trusses cover only the Hovione Atrium (north block of the main building, north of this
# local y); the long gallery spine south of it has a flat grey roof with solar arrays (ortho_campus.jpg, W08, A03).
ATRIUM_ROOF_Y = 112.0
# V10c: VivaGym has almost the same footprint as Horizon Hall (G) and sat inside / through its walls: not built
REDUNDANT = {"w1502611968"}
SOLAR = 5                  # extra material slot on the main building only


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
    if env_beachfront.owns(b):
        return env_beachfront.build_one(b, index)
    if env_campus.owns(b):                              # V10c2
        return env_campus.build_one(b, index)
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
    roof.material_index = ROOF
    if kind == "atrium":                                # glass only north of ATRIUM_ROOF_Y, grey roof south of it
        cut = bmesh.ops.bisect_plane(bm, geom=[roof] + list(roof.edges) + list(roof.verts), dist=0.001,
                                     plane_co=(0, ATRIUM_ROOF_Y, 0), plane_no=(0, 1, 0))
        bm.normal_update()
        for f in bm.faces:
            if f.normal.z > 0.9 and abs(f.calc_center_median().z - top_flat) < 0.01:
                f.material_index = GLASS_ROOF if f.calc_center_median().y > ATRIUM_ROOF_Y else ROOF
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
            if kind == "residence":
                add_residence_storey(bm, (ax, ay), (bx, by), seg, (ox, oy), z0, s, top, blocked)
            elif campus:
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
        add_roof_panels(bm, pts, (cx, cy), axis, top_flat)
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
        if kind == "atrium":
            mesh.materials.append(E.material("B_solar", "#243349", roughness=0.75, metallic=0.0))   # V10d: matte, no sky glare
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


def add_atrium_ribs(bm, pts, centre, axis, top, y_min=ATRIUM_ROOF_Y):
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
                if y_min is not None:                       # V10c: only the part of the rib north of y_min
                    if max(a[1], c[1]) < y_min:
                        continue
                    if a[1] < y_min:
                        f = (y_min - a[1]) / (c[1] - a[1])
                        a = (a[0] + (c[0] - a[0]) * f, y_min)
                    elif c[1] < y_min:
                        f = (y_min - c[1]) / (a[1] - c[1])
                        c = (c[0] + (a[0] - c[0]) * f, y_min)
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


def ccw(ring):
    """V10d: the ring counter-clockwise seen from above, so a cap faces up and walls built along it face out (the
    roof panels' boxes were clockwise: their tops faced down and were culled, the spine roof showed through)."""
    area = sum(ax * by - bx * ay for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1]))
    return ring if area > 0 else ring[::-1]


def add_roof_panels(bm, pts, centre, axis, top):
    """V10c: dark solar arrays on slightly raised flat boxes over the gallery spine (south of the atrium), with a
    few skylight strips between the rows. Rows sit across the spine every 9 m, two blocks per row with a service
    gap; every corner stays 1.5 m inside the outline."""
    across = (-axis[1], axis[0])
    proj = [(x - centre[0]) * axis[0] + (y - centre[1]) * axis[1] for x, y in pts]
    t = min(proj) + 6.0
    row = 0
    while t < max(proj) - 4.0:
        origin = (centre[0] + axis[0] * t, centre[1] + axis[1] * t)
        if origin[1] < ATRIUM_ROOF_Y - 8.0 and origin[1] > -26.0:
            for t0, t1 in line_intervals(pts, origin, across):
                if t1 - t0 < 12:
                    continue
                mid = (t0 + t1) / 2
                for lo, hi in ((t0 + 3.0, mid - 1.2), (mid + 1.2, t1 - 3.0)):
                    if hi - lo < 4:
                        continue
                    box = ccw([(origin[0] + across[0] * u + axis[0] * v, origin[1] + across[1] * u + axis[1] * v)
                               for u, v in ((lo, -2.6), (hi, -2.6), (hi, 2.6), (lo, 2.6))])
                    if not all(E.inside_polygon(x, y, pts) for x, y in box):
                        continue
                    z0, z1 = top + 0.02, top + 0.38
                    add_quad(bm, [(*c, z1) for c in box], SOLAR)
                    for k in range(4):
                        p, q = box[k], box[(k + 1) % 4]
                        add_quad(bm, [(*p, z0), (*q, z0), (*q, z1), (*p, z1)], STEEL)
                if row % 3 == 1:                       # skylight strip in the gap between two rows
                    sk = ccw([(origin[0] + across[0] * u + axis[0] * v, origin[1] + across[1] * u + axis[1] * v)
                              for u, v in ((mid - 0.8, 4.6), (mid + 0.8, 4.6), (mid + 0.8, 6.0), (mid - 0.8, 6.0))])
                    if all(E.inside_polygon(x, y, pts) for x, y in sk):
                        add_quad(bm, [(*c, top + 0.1) for c in sk], GLASS_ROOF)
        t += 9.0
        row += 1


def add_residence_storey(bm, a, b, seg, out, z0, s, top, blocked):
    """V10c, Milestone residence (J01): a white block with dark recessed balcony niches, a white slab under each
    niche, small punched windows in between, glazed ground floor. Niches alternate by storey (staggered)."""
    ax, ay = a
    bx, by = b
    ox, oy = out
    tx, ty = (bx - ax) / seg, (by - ay) / seg

    def at(u, off, z):
        return (ax + tx * u + ox * off, ay + ty * u + oy * off, z)
    if s == 0:                                          # ground floor: sliding glass doors
        u = 1.2
        while u + 2.2 < seg - 1.2:
            lo, hi = z0 + 0.1, z0 + 2.6
            if z0 + 2.6 < top - 0.3:
                for q0, q1 in split_around(at(u, 0.05, 0)[:2], at(u + 2.2, 0.05, 0)[:2], lo, hi, blocked):
                    add_quad(bm, [(*q0, lo), (*q1, lo), (*q1, hi), (*q0, hi)], GLASS)
            u += 4.6
        return
    lo, hi = z0 + 0.5, z0 + 2.9
    if hi > top - 0.6:
        return
    u = 1.5 + (3.0 if s % 2 else 0.0)
    while u + 2.4 < seg - 1.0:
        p0, p1 = at(u, 0.05, 0), at(u + 2.4, 0.05, 0)
        add_quad(bm, [(*p0[:2], lo), (*p1[:2], lo), (*p1[:2], hi), (*p0[:2], hi)], GLASS)
        # white slab under the niche, sticking out 0.6 m (5 faces)
        sl0, sl1, sh = z0 + 0.25, z0 + 0.5, 0.6
        c = [at(u - 0.2, 0.0, 0), at(u + 2.6, 0.0, 0), at(u + 2.6, sh, 0), at(u - 0.2, sh, 0)]
        add_quad(bm, [(*c[3][:2], sl1), (*c[2][:2], sl1), (*c[1][:2], sl1), (*c[0][:2], sl1)], WALL)
        add_quad(bm, [(*c[0][:2], sl0), (*c[1][:2], sl0), (*c[2][:2], sl0), (*c[3][:2], sl0)], WALL, flip=True)
        add_quad(bm, [(*c[3][:2], sl0), (*c[2][:2], sl0), (*c[2][:2], sl1), (*c[3][:2], sl1)], WALL)
        # small punched window next to the niche
        w0, w1 = at(u + 3.4, 0.05, 0), at(u + 4.6, 0.05, 0)
        if u + 4.8 < seg - 1.0:
            add_quad(bm, [(*w0[:2], z0 + 0.95), (*w1[:2], z0 + 0.95), (*w1[:2], z0 + 2.45), (*w0[:2], z0 + 2.45)], GLASS)
        u += 6.0


def build():
    for index, b in enumerate(E.DATA["buildings"]):
        if b["id"] in REDUNDANT:
            continue
        volume, details = build_one(b, index)
        name = "B_" + b["id"] + ("_" + ascii_name(b["name"]) if b["name"] else "")
        col = E.collection("World_Buildings_Campus" if b["campus"] else "World_Buildings_Other")
        obj = E.new_object(name, volume, col)
        obj["osm_id"] = b["id"]
        obj["height_m"] = b["height"]
        if details.polygons:
            det = E.new_object("BD_" + name[2:], details, col)
            det.parent = obj
    env_beachfront.build_promenade()
    env_campus.build_extras()                           # V10c2: pools, courtyard walls, seat steps, berms
    # build_roundabout_island()   V10c: replaced in V10f by env_streets (radius 14.3 m, sett apron, steel wedges)


# ----------------------------------------------------------------------------------------------
# V10c: roundabout island at the west campus edge (GAME-03, ortho_west marker 01): raised kerb ring, dark planted
# centre with pale gravel stripes. The ground colour map only paints a light disc there.
# ----------------------------------------------------------------------------------------------
ISLAND_CENTRE = (-107.2, -63.5)
ISLAND_R = 13.4                      # outer kerb edge (the light disc in the ground map is r about 13.8)
KERB_W = 0.6


def build_roundabout_island():
    cx, cy = ISLAND_CENTRE
    n = 48
    gz = E.ground_z
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()

    def ring(r, dz):
        return [bm.verts.new((cx + r * math.cos(2 * math.pi * k / n), cy + r * math.sin(2 * math.pi * k / n),
                              gz(cx + r * math.cos(2 * math.pi * k / n), cy + r * math.sin(2 * math.pi * k / n)) + dz)) for k in range(n)]
    outer_lo, outer_hi = ring(ISLAND_R, -0.15), ring(ISLAND_R, 0.28)
    inner_hi, inner_lo = ring(ISLAND_R - KERB_W, 0.28), ring(ISLAND_R - KERB_W, 0.16)
    for k in range(n):
        j = (k + 1) % n
        bm.faces.new((outer_lo[k], outer_lo[j], outer_hi[j], outer_hi[k])).material_index = 0      # outer wall
        bm.faces.new((outer_hi[k], outer_hi[j], inner_hi[j], inner_hi[k])).material_index = 0      # kerb top
        bm.faces.new((inner_hi[k], inner_hi[j], inner_lo[j], inner_lo[k])).material_index = 0      # inner wall
    # planted centre: three rings so it follows the ground
    r_in = ISLAND_R - KERB_W
    mid = ring(r_in * 0.5, 0.2)
    rim = ring(r_in, 0.2)
    c = bm.verts.new((cx, cy, gz(cx, cy) + 0.2))
    for k in range(n):
        j = (k + 1) % n
        bm.faces.new((c, mid[k], mid[j])).material_index = 1
        bm.faces.new((mid[k], rim[k], rim[j], mid[j])).material_index = 1
    # gravel stripes: chords across the island, 1.1 m wide, every 4.2 m, tilted 25 degrees
    ang = math.radians(25)
    ux, uy = math.cos(ang), math.sin(ang)
    for d in (-8.4, -4.2, 0.0, 4.2, 8.4):
        half = math.sqrt(max(r_in - 0.8, 0) ** 2 - d * d)
        steps = max(2, int(2 * half / 2.5))
        pts_l, pts_r = [], []
        for t in range(steps + 1):
            u = -half + 2 * half * t / steps
            for w, bucket in ((-0.55, pts_l), (0.55, pts_r)):
                x = cx + ux * u - uy * (d + w)
                y = cy + uy * u + ux * (d + w)
                bucket.append(bm.verts.new((x, y, gz(x, y) + 0.24)))
        for t in range(steps):
            bm.faces.new((pts_l[t], pts_l[t + 1], pts_r[t + 1], pts_r[t])).material_index = 2
    bm.normal_update()
    for f in bm.faces:
        if f.normal.z < 0 and f.material_index in (1, 2):
            f.normal_flip()
    mesh = bpy.data.meshes.new("RoundaboutIsland")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("island_kerb", "#CFC8BA", 0.85))
    mesh.materials.append(E.material("island_planting", "#3E4B34", 0.95))
    mesh.materials.append(E.material("island_gravel", "#B7A98C", 0.9))
    E.new_object("World_RoundaboutIsland", mesh, E.collection("World_Dressing"))
    print(f"  roundabout island: {len(mesh.polygons)} faces")
