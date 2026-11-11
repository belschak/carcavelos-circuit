"""Backdrop: sea to the horizon, land beyond the world edge, the real buildings around it, and the fort.

- Sea: one water plane at z = 0 that covers only the sea side of the coast (coast.sea_polygon: the traced waterline,
  the far coast west of the world, the edge of the land skirts east of the cove), reaching SEA_UNDER_BEACH metres
  under the beach so it meets the sand exactly on the waterline. Nothing inland stands over water, so no hollow or
  gap in the ground can ever show the sea.
- Land skirt: the terrain edge is extended 600 m outwards (north, east) so the world has no end;
  on the south edge only land vertices are extended (SOUTH_SKIRT), sloping into the sea round the fort.
- Far west (build_far_west): Carcavelos beach goes on for about a kilometre beyond the world edge. A strip that
  follows the coast continues the world's own west-edge cross-section (sea, wet and dry sand, the promenade and road),
  narrowing towards Parede, with the ground colour map read from the world's edge column. From coast.FAR_X the coast
  is a straight line, which Sea.shader also uses for the foam beyond the shore map.
- Rock shelves (build_rocks): low irregular limestone slabs on the beach and in the swash (coast.ROCKS).
- Outer buildings: the real OSM buildings just outside the world bounds as simple blocks (low detail).
- Forte de Sao Juliao da Barra (OSM way 155316963) with its lighthouse (Farol de Sao Juliao, 24 m tower,
  light 39 m above the sea): the silhouette seen from the promenade in the videos.
"""
import math
import os
import sys

import bmesh
import bpy

import coast
import env_common as E

sys.path.insert(0, E.HERE)
from world_data import load_osm, outer_rings, polygon_area, to_local, way_points  # noqa: E402

SKIRT = 600.0
SEA_LEVEL = 0.0
FORT_ID = 155316963
FORT_TOP = 13.0           # rampart walk height above the sea (m), from the lighthouse data
LIGHTHOUSE_HEIGHT = 24.0


DISTANT_LAND_Z = 8.0      # height of the land beyond the skirt (the skirt slopes to it)
SOUTH_SKIRT = 60.0        # m: south of the world the land (headland to the fort) slopes from the edge into the sea
COAST_Y = -100.0          # rough line of the coast outside the world (the ocean is south of it)


SEA_UNDER_BEACH = 6.0     # m: the water plane reaches this far under the land along the whole coast


def build_sea(col):
    """The water plane, clipped to the sea side of the coast (one polygon, triangulated)."""
    ring = coast.sea_polygon(inland=SEA_UNDER_BEACH)
    bm = bmesh.new()
    face = bm.faces.new([bm.verts.new((x, y, SEA_LEVEL)) for x, y in ring])
    bmesh.ops.triangulate(bm, faces=[face], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    for f in bm.faces:
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    mesh = bpy.data.meshes.new("Backdrop_Sea")
    bm.to_mesh(mesh)
    bm.free()
    mat = E.material("sea_water", "#2D8CD6", roughness=0.12, metallic=0.0)
    for key, value in sea_water_data().items():      # export_unity_bundle.py -> materials.json "water" -> Sea.shader
        mat[key] = value
    mesh.materials.append(mat)
    E.new_object("Backdrop_Sea", mesh, col)


SEA_SHADER = "CarcavelosCircuit/Sea"       # Assets/_Project/Visuals/Shaders/Sea.shader (flat URP Lit sea if missing)
SHORE_MAP = "sea_shore_data.png"           # R = distance to the shore in the sea, 0 .. SHORE_RANGE m (linear data)
SHORE_RANGE = 60.0
WAVE_NORMAL = "sea_waves_normal.png"       # make_textures.py sea_waves


def sea_water_data():
    """What the moving sea needs besides the mesh: shader name, its two textures, the world rectangle the shore map
    covers (x0, y0, x1, y1, see shore_rect), the straight coast line used east of that rectangle (y = COAST_Y) and
    the far coast west of it (x0, y0, metres north per metre west: coast.far_line_y)."""
    return {"water_shader": SEA_SHADER, "water_normal": WAVE_NORMAL, "water_shore_map": SHORE_MAP,
            "water_shore_rect": list(shore_rect()), "water_shore_range": SHORE_RANGE, "water_coast": COAST_Y,
            "water_coast_west": [float(coast.FAR_X), float(coast.FAR_Y), float(coast.FAR_SLOPE)]}


SHORE_MARGIN = (500.0, 300.0, 400.0)       # metres the shore map reaches beyond the world: west, east, south


def shore_rect():
    """The world plus SHORE_MARGIN; the west margin reaches past coast.FAR_X, so outside the map the far coast is
    the straight line Sea.shader knows."""
    west, east, south = SHORE_MARGIN
    return (E.X0 - west, E.Y0 - south, E.X1 + east, E.Y1)


def write_shore_map(size=1024):
    """textures/sea_shore_data.png: for every point of the world, how far it is from the shore, measured in the
    sea (0 on land and at the water's edge, 1 = SHORE_RANGE metres or more). Land is whatever a ray from the sky
    hits above SEA_LEVEL (terrain, land skirt, fort), sea is where it hits the water plane first: exactly the
    shoreline the 3D world shows. So call this when the ground and the backdrop exist (build() does). The map
    covers the world bounds plus SHORE_MARGIN metres to the west, east and south (shore_rect). The sea shader
    reads it by world position for the turquoise shallows and the foam band, so the sea needs no depth texture
    and no extra geometry. Row 0 of the image is the south edge."""
    import numpy as np
    from mathutils import Vector
    x0, y0, x1, y1 = shore_rect()
    dx, dy = (x1 - x0) / size, (y1 - y0) / size
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    scene, down = bpy.context.scene, Vector((0, 0, -1))
    land = np.zeros((size, size), bool)
    for j in range(size):
        y = y0 + (j + 0.5) * dy
        for i in range(size):
            hit, loc, _n, _i, _obj, _m = scene.ray_cast(deps, Vector((x0 + (i + 0.5) * dx, y, 500.0)), down)
            land[j, i] = hit and loc.z > SEA_LEVEL + 0.02
    dist = np.where(land, 0.0, 1e6)
    diag = math.hypot(dx, dy)
    steps = ((1, 0, dx), (-1, 0, dx), (0, 1, dy), (0, -1, dy), (1, 1, diag), (1, -1, diag), (-1, 1, diag), (-1, -1, diag))
    for _ in range(int(SHORE_RANGE / min(dx, dy)) + 2):          # grow the distance outwards from the land
        padded = np.pad(dist, 1, mode="edge")
        for sx, sy, cost in steps:
            dist = np.minimum(dist, padded[1 + sy:1 + sy + size, 1 + sx:1 + sx + size] + cost)
    value = np.clip((dist - 0.5 * min(dx, dy)) / SHORE_RANGE, 0.0, 1.0)
    rgba = np.repeat(value[:, :, None], 4, axis=2)
    rgba[:, :, 3] = 1.0
    os.makedirs(E.TEXTURES, exist_ok=True)
    img = bpy.data.images.new("sea_shore_data", size, size, alpha=False, is_data=True)
    img.pixels = rgba.ravel().tolist()                             # bpy rows run bottom-up: row 0 = south
    img.filepath_raw, img.file_format = os.path.join(E.TEXTURES, SHORE_MAP), "PNG"
    img.save()
    print(f"  shore map: {SHORE_MAP} {size} px, {dx:.2f} x {dy:.2f} m per pixel, {100.0 * (1.0 - land.mean()):.0f} % sea")
    return land


def build_skirt(heights, col, west_top):
    """North, east and south (land only) skirts from the world edge, the NE corner, and the far land to the horizon:
    one polygon from the top of the far-west strip (west_top, east to west) round the north, and the band east of
    the east skirt. The west edge belongs to build_far_west."""
    bm = bmesh.new()
    rows, cols = E.ROWS, E.COLS

    def v(i, j):
        return (E.X0 + i * E.STEP, E.Y0 + j * E.STEP)

    edges = []
    # (list of (i, j) along the edge, outward direction, distance)
    edges.append(([(cols - 1, j) for j in range(rows)], (1, 0), SKIRT))          # east
    edges.append(([(i, rows - 1) for i in range(cols)], (0, 1), SKIRT))          # north
    edges.append(([(i, 0) for i in range(cols)], (0, -1), SOUTH_SKIRT))          # south (land only)
    for cells, (dx, dy), dist in edges:
        for (i0, j0), (i1, j1) in zip(cells, cells[1:]):
            h0, h1 = heights[j0][i0], heights[j1][i1]
            if dy == -1 and (h0 < 0.5 or h1 < 0.5):
                continue
            a, b = v(i0, j0), v(i1, j1)
            land_z = -1.5 if dy == -1 else DISTANT_LAND_Z       # the headland slopes into the sea round the fort
            far_h0 = land_z if h0 > 0.5 else -1.5
            far_h1 = land_z if h1 > 0.5 else -1.5
            quad = [(a[0], a[1], h0), (b[0], b[1], h1), (b[0] + dx * dist, b[1] + dy * dist, far_h1),
                    (a[0] + dx * dist, a[1] + dy * dist, far_h0)]
            vs = [bm.verts.new(c) for c in quad]
            face = bm.faces.new(vs)
            face.normal_update()
            if face.normal.z < 0:
                face.normal_flip()
    # the north-east corner square (the north-west one is part of the far-west strip)
    h = heights[rows - 1][cols - 1]
    x, y = v(cols - 1, rows - 1)
    f = DISTANT_LAND_Z
    quad = [(x, y, h), (x + SKIRT, y, f), (x + SKIRT, y + SKIRT, f), (x, y + SKIRT, f)]
    face = bm.faces.new([bm.verts.new(c) for c in quad])
    face.normal_update()
    if face.normal.z < 0:
        face.normal_flip()
    # distant land to the horizon
    s, z = 4000.0, DISTANT_LAND_Z
    xb, yt = E.X1 + SKIRT, E.Y1 + SKIRT
    north = [(E.X0, yt)] + [(px, py) for px, py, _pz in west_top] + [(west_top[-1][0], s), (s, s), (s, yt)]
    for ring in (north, ((xb, COAST_Y), (s, COAST_Y), (s, yt), (xb, yt))):
        face = bm.faces.new([bm.verts.new((x, y, z)) for x, y in ring])
        if len(ring) > 4:
            bmesh.ops.triangulate(bm, faces=[face], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    for face in bm.faces:
        face.normal_update()
        if face.normal.z < 0:
            face.normal_flip()
    mesh = bpy.data.meshes.new("Backdrop_Land")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("backdrop_land", "#9DB58A", roughness=0.95))
    E.new_object("Backdrop_Land", mesh, col)


# ---------- the beach beyond the west edge ----------
FAR_COLUMNS = (20, 45, 75, 110, 150, 200, 260, 330, 410, 500, 600, 720, 880, 1130, 1480, 1980, 2700, 3580)  # m west
FAR_ROWS = (-80, -40, -15, -5, 0, 3, 8, 15, 25, 40, 60, 85, 115, 150, 190, 240, 300, 380, 480, 600, 750)  # m north
FAR_NARROW = (-1150.0, 0.55)    # by this x the beach and its hinterland are this share as deep as at the world edge
U_EDGE = 1.0 / 2048.0           # ground colour map: the world's west edge column (u of the first texels)
MAP_TOP_Y = 100.0               # world y at the edge up to which the colour map is used: the beach, promenade and road
_FAR = {}


def _far_setup(heights):
    yc0 = coast.coast_y_west(E.X0)
    h_top = heights[E.ROWS - 1][0]

    def edge_z(y):
        """The world's west edge cross-section at y; above the world it is the north skirt's slope to the far land."""
        if y >= E.Y1:
            return h_top + (DISTANT_LAND_Z - h_top) * min((y - E.Y1) / SKIRT, 1.0)
        f = min(max((y - E.Y0) / E.STEP, 0.0), E.ROWS - 1.001)
        j = int(f)
        return heights[j][0] * (1 - (f - j)) + heights[j + 1][0] * (f - j)

    _FAR.update(yc0=yc0, edge_z=edge_z, v_top=E.Y1 + SKIRT - yc0, edge_top=MAP_TOP_Y - yc0)


def _far_width(x):
    t = min(max((E.X0 - x) / (E.X0 - FAR_NARROW[0]), 0.0), 1.0)
    return 1.0 + (FAR_NARROW[1] - 1.0) * t


def far_west_point(x, v):
    """Point of the far-west strip v metres north of the waterline at x: ((x, y, z), source offset at the world
    edge). The cross-section is the world's west edge, squeezed to _far_width(x); below the waterline the seabed."""
    yc = coast.coast_y_west(x)
    if v <= 0:
        return (x, yc + v, coast.beach_z(v * coast.cos_west(x))), v
    src = v / _far_width(x)
    return (x, yc + v, _FAR["edge_z"](_FAR["yc0"] + src)), src


def far_west_ground(x, y):
    """Ground height of the far-west strip at (x, y) (x < X0)."""
    return far_west_point(x, y - coast.coast_y_west(x))[0][2]


def build_far_west(heights, col):
    """Carcavelos beach beyond the west edge: a strip of columns going west along the coast (FAR_COLUMNS) with rows
    at fixed distances from the waterline (FAR_ROWS), so the water's edge is one straight row of vertices at z = 0.
    The first column is the world's west edge itself (shared vertices, joined by a zipper of triangles), the last
    row meets the far land at DISTANT_LAND_Z. Ground-map faces read the world's west-edge column of the colour map
    at the matching distance from the water (sand, wet band, promenade, road); the rest is backdrop land.
    Returns the top row (east to west) for the far land polygon."""
    _far_setup(heights)
    v_top, edge_top, yc0 = _FAR["v_top"], _FAR["edge_top"], _FAR["yc0"]
    rows = [r for r in FAR_ROWS if r < v_top - 40.0] + [v_top]
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    span = E.Y1 - E.Y0

    def vert(co, src):
        vtx = bm.verts.new(co)
        return vtx, src

    # column 0: the world's west edge (every terrain vertex) and, above the world, the north skirt's west edge
    first = [vert((E.X0, E.Y0 + j * E.STEP, heights[j][0]), E.Y0 + j * E.STEP - yc0) for j in range(E.ROWS)]
    first += [vert((E.X0, yc0 + r, _FAR["edge_z"](yc0 + r)), r) for r in rows if yc0 + r > E.Y1 + 0.5]
    columns = [first] + [[vert(*far_west_point(E.X0 - c, r)) for r in rows] for c in FAR_COLUMNS]

    def face(corners):
        f = bm.faces.new([c[0] for c in corners])
        on_map = all(c[1] <= edge_top + 1e-6 for c in corners)
        f.material_index = 0 if on_map else 1
        for loop, (_v, src) in zip(f.loops, corners):
            loop[uv].uv = (U_EDGE, min(max((yc0 + src - E.Y0) / span, 0.0), 1.0))
        return f

    a, b = columns[0], columns[1]                    # zipper: walk both columns from south to north
    i = j = 0
    while i < len(a) - 1 or j < len(b) - 1:
        if j == len(b) - 1 or (i < len(a) - 1 and a[i + 1][0].co.y <= b[j + 1][0].co.y):
            face((a[i], a[i + 1], b[j]))
            i += 1
        else:
            face((a[i], b[j + 1], b[j]))
            j += 1
    for c0, c1 in zip(columns[1:], columns[2:]):
        for m in range(len(rows) - 1):
            face((c0[m], c1[m], c1[m + 1], c0[m + 1]))
    for f in bm.faces:
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    top = [tuple(c[-1][0].co) for c in columns[1:]]
    mesh = bpy.data.meshes.new("Backdrop_FarBeach")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("ground_map", "#A6AA82", roughness=0.95))
    mesh.materials.append(E.material("backdrop_land", "#9DB58A", roughness=0.95))
    E.new_object("Backdrop_FarBeach", mesh, col)
    print(f"  far west: beach strip {len(mesh.polygons)} faces to x = {E.X0 - FAR_COLUMNS[-1]:.0f} m")
    return top


def edge_height(heights, x, y):
    i = int(round(min(max((x - E.X0) / E.STEP, 0), E.COLS - 1)))
    j = int(round(min(max((y - E.Y0) / E.STEP, 0), E.ROWS - 1)))
    return heights[j][i]


def build_outer_buildings(heights, col):
    elements, nodes, ways = load_osm()
    bm = bmesh.new()
    count = 0
    for e in elements:
        tags = e.get("tags", {})
        if e["type"] != "way" or "building" not in tags or e["id"] == FORT_ID:
            continue
        ring = way_points(e, nodes)
        if len(ring) > 1 and ring[0] == ring[-1]:
            ring = ring[:-1]
        if len(ring) < 3 or abs(polygon_area(ring)) < 15:
            continue
        cx = sum(p[0] for p in ring) / len(ring)
        cy = sum(p[1] for p in ring) / len(ring)
        if E.X0 <= cx <= E.X1 and E.Y0 <= cy <= E.Y1:
            continue                                    # inside the world: env_buildings does those
        if polygon_area(ring) < 0:
            ring = ring[::-1]
        if cx < coast.WATERLINE[-1][0] and coast.signed_distance(cx, cy) < 25.0:
            continue                                    # nothing stands in the sea or on the beach west of the cove
        ground = far_west_ground(cx, cy) if cx < E.X0 else edge_height(heights, cx, cy)
        base = max(ground, 0.5) - 2.0
        levels = float(tags.get("building:levels", 3)) if tags.get("building:levels", "3").replace(".", "").isdigit() else 3.0
        top = base + 2.0 + levels * 3.2
        low = [bm.verts.new((x, y, base)) for x, y in ring]
        high = [bm.verts.new((x, y, top)) for x, y in ring]
        n = len(ring)
        for k in range(n):
            bm.faces.new((low[k], low[(k + 1) % n], high[(k + 1) % n], high[k]))
        bm.faces.new(high)
        count += 1
    mesh = bpy.data.meshes.new("Backdrop_Buildings")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("backdrop_building", "#E9E3D6", roughness=0.9))
    E.new_object("Backdrop_Buildings", mesh, col)
    print(f"  backdrop: {count} outer buildings")


def build_fort(col):
    elements, nodes, ways = load_osm()
    fort = next(e for e in elements if e["type"] == "way" and e["id"] == FORT_ID)
    ring = way_points(fort, nodes)
    if ring[0] == ring[-1]:
        ring = ring[:-1]
    if polygon_area(ring) < 0:
        ring = ring[::-1]
    cx = sum(p[0] for p in ring) / len(ring)
    cy = sum(p[1] for p in ring) / len(ring)
    bm = bmesh.new()
    n = len(ring)
    # sloped bastion walls (talus): the top ring is pulled 1.5 m inwards
    low = [bm.verts.new((x, y, -1.5)) for x, y in ring]
    high = []
    for x, y in ring:
        d = math.hypot(x - cx, y - cy) or 1.0
        high.append(bm.verts.new((x - (x - cx) / d * 1.5, y - (y - cy) / d * 1.5, FORT_TOP)))
    for k in range(n):
        f = bm.faces.new((low[k], low[(k + 1) % n], high[(k + 1) % n], high[k]))
        f.material_index = 0
    top = bm.faces.new(high)
    top.material_index = 1
    # parapet rim
    rim = [bm.verts.new((v.co.x, v.co.y, FORT_TOP + 1.2)) for v in high]
    for k in range(n):
        f = bm.faces.new((high[k], high[(k + 1) % n], rim[(k + 1) % n], rim[k]))
        f.material_index = 0
    # lighthouse (real position from OSM): white tower, red lantern
    light = next(e for e in elements if e["type"] == "node" and e.get("tags", {}).get("man_made") == "lighthouse")
    lx, ly = to_local(light["lat"], light["lon"])
    tower = bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=2.6, radius2=2.0, depth=LIGHTHOUSE_HEIGHT)
    bmesh.ops.translate(bm, verts=tower["verts"], vec=(lx, ly, FORT_TOP + LIGHTHOUSE_HEIGHT / 2))
    for f in {f for v in tower["verts"] for f in v.link_faces}:
        f.material_index = 2
    lantern = bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=1.6, radius2=1.1, depth=2.4)
    bmesh.ops.translate(bm, verts=lantern["verts"], vec=(lx, ly, FORT_TOP + LIGHTHOUSE_HEIGHT + 1.2))
    for f in {f for v in lantern["verts"] for f in v.link_faces}:
        f.material_index = 3
    bm.normal_update()
    mesh = bpy.data.meshes.new("Landmark_Fort")
    bm.to_mesh(mesh)
    bm.free()
    for m in (E.material("fort_stone", "#D9CDB2", 0.95), E.material("fort_top", "#BFB59E", 0.95),
              E.material("lighthouse_white", "#F2F0EA", 0.7), E.material("lighthouse_red", "#C8363A", 0.5, emission="#FF5A4E")):
        mesh.materials.append(m)
    E.new_object("Landmark_FortSaoJuliao", mesh, col)


def build_far_town(col, count=900, seed=11):
    """Low town blocks on the distant land (north of the coast, beyond the skirt): Carcavelos/Oeiras are built up."""
    import random
    rng = random.Random(seed)
    bm = bmesh.new()
    placed = 0
    while placed < count:
        r = rng.uniform(SKIRT * 0.8, 2600.0)
        ang = rng.uniform(math.radians(-15), math.radians(195))           # north half only
        x, y = (E.X0 + E.X1) / 2 + r * math.cos(ang), (E.Y0 + E.Y1) / 2 + r * math.sin(ang)
        if y < COAST_Y + 40 or (E.X0 - SKIRT * 0.5 < x < E.X1 + SKIRT * 0.5 and y < E.Y1 + SKIRT * 0.5):
            continue
        if coast.signed_distance(x, y) < 220.0:                         # not in the sea or on the far beach
            continue
        w, d, h = rng.uniform(10, 30), rng.uniform(8, 18), rng.choice((7, 9, 10, 12, 15, 18))
        a = rng.uniform(0, math.pi)
        ca, sa = math.cos(a), math.sin(a)
        corners = [(x + ca * u - sa * v, y + sa * u + ca * v) for u, v in ((-w, -d), (w, -d), (w, d), (-w, d))]
        low = [bm.verts.new((cx, cy, DISTANT_LAND_Z - 1)) for cx, cy in corners]
        high = [bm.verts.new((cx, cy, DISTANT_LAND_Z + h)) for cx, cy in corners]
        for k in range(4):
            bm.faces.new((low[k], low[(k + 1) % 4], high[(k + 1) % 4], high[k])).material_index = placed % 3
        bm.faces.new(high).material_index = 3
        placed += 1
    mesh = bpy.data.meshes.new("Backdrop_FarTown")
    bm.to_mesh(mesh)
    bm.free()
    for m in (E.material("far_white", "#ECE7DD", 0.9), E.material("far_cream", "#E6D9BC", 0.9),
              E.material("far_ochre", "#DCC08E", 0.9), E.material("far_roof", "#B9705A", 0.9)):
        mesh.materials.append(m)
    E.new_object("Backdrop_FarTown", mesh, col)


ROCK_STEP = 2.0       # m between outline points of a rock shelf


def _ring_ccw(ring):
    area = sum(ax * by - bx * ay for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1]))
    return ring if area > 0 else ring[::-1]


def _densify(ring, step):
    out = []
    for (ax, ay), (bx, by) in zip(ring, ring[1:] + ring[:1]):
        n = max(1, int(math.hypot(bx - ax, by - ay) / step))
        out += [(ax + (bx - ax) * k / n, ay + (by - ay) * k / n) for k in range(n)]
    return out


def build_rocks(col, seed=505):
    """Low limestone shelves and ribs (coast.ROCKS, K06/K07, marker 05). Per rock: the traced outline made ragged
    by a smooth wobble, and four rings going in and up: 0.35 m under the sand, a ledge at half height, a shoulder
    and a crest, whose heights wander along the outline (one end of a rib lower than the other), then a cap. So the
    shelves step up from the sand in layers like the real ones. They follow the beach slope and may stand in the
    swash (the foam wraps round them). Ground-map material with world UVs: the colour map paints the rock colour
    under and on them, so no new material and no extra draw call."""
    import random
    rng = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for rock in coast.ROCKS:
        h = rock["height"]
        outline = _ring_ccw(rock["ring"])
        per = sum(math.hypot(bx - ax, by - ay) for (ax, ay), (bx, by) in zip(outline, outline[1:] + outline[:1]))
        area = 0.5 * sum(ax * by - bx * ay for (ax, ay), (bx, by) in zip(outline, outline[1:] + outline[:1]))
        width = 2.0 * area / per                                  # about the rock's width
        ring = _densify(outline, ROCK_STEP)
        n = len(ring)
        phases = [rng.uniform(0, 2 * math.pi) for _ in range(4)]

        def wave(k, a, b):                                         # smooth noise along the outline, -1 .. 1
            t = 2 * math.pi * k / n
            return 0.6 * math.sin(a * t + phases[b]) + 0.4 * math.sin((a * 2 + 1) * t + phases[b + 1])
        # (inset m, share of the height, jitter of that share)
        rings = ((0.0, None, 0.0), (0.25, 0.45, 0.12), (min(0.9, 0.2 * width), 0.82, 0.1), (min(1.9, 0.38 * width), 1.0, 0.12))
        loops = []
        for inset, share, jit in rings:
            loop = []
            for k, (x, y) in enumerate(ring):
                (ax, ay), (bx, by) = ring[k - 1], ring[(k + 1) % n]
                tl = math.hypot(bx - ax, by - ay) or 1.0
                ix, iy = -(by - ay) / tl, (bx - ax) / tl          # inward normal (counter-clockwise outline)
                rag = 0.45 * wave(k, 3, 0)                         # ragged outline
                px, py = x + ix * (inset - rag), y + iy * (inset - rag)
                if share is None:
                    z = E.ground_z(px, py) - 0.35
                else:
                    lift = h * (0.8 + 0.3 * wave(k, 1, 2))         # one end higher than the other
                    z = E.ground_z(px, py) + lift * (share + rng.uniform(-jit, jit))
                loop.append(bm.verts.new((px, py, z)))
            loops.append(loop)
        for a, b in zip(loops, loops[1:]):
            for k in range(n):
                bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
        cap = bm.faces.new(loops[-1])
        bmesh.ops.triangulate(bm, faces=[cap], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    for f in bm.faces:                                    # counter-clockwise outlines: all faces already face out
        for loop in f.loops:
            co = loop.vert.co
            loop[uv].uv = ((co.x - E.X0) / (E.X1 - E.X0), (co.y - E.Y0) / (E.Y1 - E.Y0))
    mesh = bpy.data.meshes.new("Coast_Rocks")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("ground_map", "#A6AA82", roughness=0.95))
    E.new_object("Coast_Rocks", mesh, col)
    print(f"  coast rocks: {len(coast.ROCKS)} shelves, {sum(len(p.vertices) - 2 for p in mesh.polygons)} triangles")


def build(heights):
    col = E.collection("Backdrop")
    build_sea(col)
    west_top = build_far_west(heights, col)
    build_skirt(heights, col, west_top)
    build_rocks(col)
    build_outer_buildings(heights, col)
    build_far_town(col)
    build_fort(col)
    write_shore_map()                      # last: it looks at the finished ground and backdrop from above
