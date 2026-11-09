"""Backdrop: sea to the horizon, land beyond the world edge, the real buildings around it, and the fort.

- Sea: one large water plane at z = 0 south of the coast (the terrain's seabed sits below it).
- Land skirt: the terrain edge is extended 600 m outwards (west, north, east) so the world has no end;
  on the south edge only land vertices are extended a little, the sea stays open.
- Outer buildings: the real OSM buildings just outside the world bounds as simple blocks (low detail).
- Forte de Sao Juliao da Barra (OSM way 155316963) with its lighthouse (Farol de Sao Juliao, 24 m tower,
  light 39 m above the sea): the silhouette seen from the promenade in the videos.
"""
import math
import os
import sys

import bmesh
import bpy

import env_common as E

sys.path.insert(0, E.HERE)
from world_data import load_osm, outer_rings, polygon_area, to_local, way_points  # noqa: E402

SKIRT = 600.0
SEA_LEVEL = 0.0
FORT_ID = 155316963
FORT_TOP = 13.0           # rampart walk height above the sea (m), from the lighthouse data
LIGHTHOUSE_HEIGHT = 24.0


DISTANT_LAND_Z = 8.0      # height of the land beyond the skirt (the skirt slopes to it)
COAST_Y = -100.0          # rough line of the coast outside the world (the ocean is south of it)


def build_sea(col):
    mesh = bpy.data.meshes.new("Backdrop_Sea")
    s = 4000.0
    xa, xb = E.X0 - SKIRT, E.X1 + SKIRT
    verts = [(-s, -s, SEA_LEVEL), (s, -s, SEA_LEVEL), (s, COAST_Y, SEA_LEVEL), (-s, COAST_Y, SEA_LEVEL),
             (xa, COAST_Y, SEA_LEVEL), (xb, COAST_Y, SEA_LEVEL), (xb, 150.0, SEA_LEVEL), (xa, 150.0, SEA_LEVEL)]
    mesh.from_pydata(verts, [], [(0, 1, 2, 3), (4, 5, 6, 7)])
    mesh.materials.append(E.material("sea_water", E.PALETTE["sea"], roughness=0.12, metallic=0.0))
    E.new_object("Backdrop_Sea", mesh, col)


def build_skirt(heights, col):
    bm = bmesh.new()
    rows, cols = E.ROWS, E.COLS

    def v(i, j):
        return (E.X0 + i * E.STEP, E.Y0 + j * E.STEP)

    edges = []
    # (list of (i, j) along the edge, outward direction, distance)
    edges.append(([(0, j) for j in range(rows)], (-1, 0), SKIRT))                 # west
    edges.append(([(cols - 1, j) for j in range(rows)], (1, 0), SKIRT))          # east
    edges.append(([(i, rows - 1) for i in range(cols)], (0, 1), SKIRT))          # north
    edges.append(([(i, 0) for i in range(cols)], (0, -1), 45.0))                 # south (land only)
    for cells, (dx, dy), dist in edges:
        for (i0, j0), (i1, j1) in zip(cells, cells[1:]):
            h0, h1 = heights[j0][i0], heights[j1][i1]
            if dy == -1 and (h0 < 0.5 or h1 < 0.5):
                continue
            a, b = v(i0, j0), v(i1, j1)
            far_h0 = DISTANT_LAND_Z if h0 > 0.5 else -1.5
            far_h1 = DISTANT_LAND_Z if h1 > 0.5 else -1.5
            quad = [(a[0], a[1], h0), (b[0], b[1], h1), (b[0] + dx * dist, b[1] + dy * dist, far_h1),
                    (a[0] + dx * dist, a[1] + dy * dist, far_h0)]
            vs = [bm.verts.new(c) for c in quad]
            face = bm.faces.new(vs)
            face.normal_update()
            if face.normal.z < 0:
                face.normal_flip()
    # corners: fill the four corner squares so there is no gap
    for (ci, cj), (dx, dy) in (((0, rows - 1), (-1, 1)), ((cols - 1, rows - 1), (1, 1))):
        h = heights[cj][ci]
        x, y = v(ci, cj)
        f = DISTANT_LAND_Z
        quad = [(x, y, h), (x + dx * SKIRT, y, f), (x + dx * SKIRT, y + dy * SKIRT, f), (x, y + dy * SKIRT, f)]
        face = bm.faces.new([bm.verts.new(c) for c in quad])
        face.normal_update()
        if face.normal.z < 0:
            face.normal_flip()
    # distant land to the horizon: north of the coast, all around the world + skirt
    s, z = 4000.0, DISTANT_LAND_Z
    xa, xb, yt = E.X0 - SKIRT, E.X1 + SKIRT, E.Y1 + SKIRT
    for quad in (((-s, yt), (s, yt), (s, s), (-s, s)), ((-s, COAST_Y), (xa, COAST_Y), (xa, yt), (-s, yt)),
                 ((xb, COAST_Y), (s, COAST_Y), (s, yt), (xb, yt))):
        face = bm.faces.new([bm.verts.new((x, y, z)) for x, y in quad])
        face.normal_update()
        if face.normal.z < 0:
            face.normal_flip()
    mesh = bpy.data.meshes.new("Backdrop_Land")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("backdrop_land", "#9DB58A", roughness=0.95))
    E.new_object("Backdrop_Land", mesh, col)


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
        base = max(edge_height(heights, cx, cy), 0.5) - 2.0
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


def build(heights):
    col = E.collection("Backdrop")
    build_sea(col)
    build_skirt(heights, col)
    build_outer_buildings(heights, col)
    build_far_town(col)
    build_fort(col)
