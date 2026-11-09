"""The beach tunnel ("Sagres Beach Way" under the Avenida Marginal) as a structure around the route.

Cross-section from the walk videos: floor about 9 m wide (8 m road + footways), walls sloping outwards at
45 degrees, flat concrete ceiling about 3.9 m up. Walls carry an ORIGINAL mural texture (never the real
artwork). White concrete headwalls with the opening at both portals; a cap closes the terrain hole above
each portal (env_terrain.in_portal_hole).
"""
import math

import bmesh
import bpy

import env_common as E
import env_terrain

FLOOR_HALF = E.ROUTE_WIDTH / 2 + 0.6     # road + narrow footway each side
CEILING = 3.9                            # clear height
SLAB = 0.7
CEIL_HALF = FLOOR_HALF + CEILING         # 45 degree walls
MURAL_TILE_M = 12.0                      # one mural texture repeat along the wall
HEADWALL_HALF = 14.0


def tunnel_points():
    return [p for p in E.route_points() if p["tunnel"]]


def frame(pts, i):
    a, b = pts[max(i - 1, 0)], pts[min(i + 1, len(pts) - 1)]
    dx, dy = b["x"] - a["x"], b["y"] - a["y"]
    d = math.hypot(dx, dy) or 1.0
    return (dx / d, dy / d), (-dy / d, dx / d)


def at(p, nrm, side, z):
    return (p["x"] + nrm[0] * side, p["y"] + nrm[1] * side, p["track_z"] + z)


def build_shell(pts, col):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rings = []
    for i, p in enumerate(pts):
        _, nrm = frame(pts, i)
        rings.append((p, [bm.verts.new(at(p, nrm, s, z)) for s, z in (
            (FLOOR_HALF, 0.12), (CEIL_HALF, CEILING), (-CEIL_HALF, CEILING), (-FLOOR_HALF, 0.12))]))
    wall_len = math.hypot(CEIL_HALF - FLOOR_HALF, CEILING)
    for (pa, ra), (pb, rb) in zip(rings, rings[1:]):
        ua, ub = pa["s"] / MURAL_TILE_M, pb["s"] / MURAL_TILE_M
        # left wall (mural), ceiling, right wall (mural); faces point into the tunnel
        for k, mat in ((0, 0), (1, 1), (2, 0)):
            face = bm.faces.new((ra[k], rb[k], rb[k + 1], ra[k + 1]))
            face.material_index = mat
            if mat == 0:
                coords = ((ua, 0), (ub, 0), (ub, 1), (ua, 1)) if k == 0 else ((ua, 1), (ub, 1), (ub, 0), (ua, 0))
            else:
                coords = ((0, ua * 6), (0, ub * 6), (CEIL_HALF, ub * 6), (CEIL_HALF, ua * 6))
            for loop, c in zip(face.loops, coords):
                loop[uv].uv = c
    # footways (raised strips between road edge and wall foot)
    for (pa, ra), (pb, rb) in zip(rings, rings[1:]):
        for side in (1, -1):
            _, na = frame(pts, pts.index(pa))
            _, nb = frame(pts, pts.index(pb))
            vs = [bm.verts.new(at(pa, na, side * (E.ROUTE_WIDTH / 2), 0.12)), bm.verts.new(at(pa, na, side * FLOOR_HALF, 0.12)),
                  bm.verts.new(at(pb, nb, side * FLOOR_HALF, 0.12)), bm.verts.new(at(pb, nb, side * (E.ROUTE_WIDTH / 2), 0.12))]
            face = bm.faces.new(vs if side == -1 else vs[::-1])
            face.material_index = 1
    # vertex order above already makes walls and ceiling face into the tunnel (checked by hand)
    mesh = bpy.data.meshes.new("Tunnel_Shell")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("tunnel_mural", "#F2C14E", roughness=0.8))
    mesh.materials.append(E.material("tunnel_concrete", "#C9C3B6", roughness=0.9))
    return E.new_object("Tunnel_Shell", mesh, col)


def build_lights(pts, col):
    bm = bmesh.new()
    for i in range(2, len(pts) - 2, 4):
        p = pts[i]
        t, nrm = frame(pts, i)
        corners = [(p["x"] + t[0] * a + nrm[0] * s, p["y"] + t[1] * a + nrm[1] * s, p["track_z"] + CEILING - 0.04)
                   for a, s in ((-1.0, -0.15), (1.0, -0.15), (1.0, 0.15), (-1.0, 0.15))]
        bm.faces.new([bm.verts.new(c) for c in corners])
    bm.normal_update()
    for f in bm.faces:
        if f.normal.z > 0:
            f.normal_flip()
    mesh = bpy.data.meshes.new("Tunnel_Lights")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("tunnel_light", "#FFF4D6", roughness=0.5, emission="#FFF4D6"))
    return E.new_object("Tunnel_Lights", mesh, col)


def build_portal(p, inward, cover, name, col):
    """Headwall with the trapezoid opening, plus a cap over the terrain hole behind it."""
    dx, dy = inward
    nrm = (-dy, dx)
    top = cover + 1.0
    z0 = p["track_z"]

    def pt(side, z, along=0.0):
        return (p["x"] + nrm[0] * side + dx * along, p["y"] + nrm[1] * side + dy * along, z0 + z)

    bm = bmesh.new()
    V = lambda c: bm.verts.new(c)
    faces = [
        [pt(-HEADWALL_HALF, CEILING), pt(HEADWALL_HALF, CEILING), pt(HEADWALL_HALF, top), pt(-HEADWALL_HALF, top)],
        [pt(-HEADWALL_HALF, -0.5), pt(-FLOOR_HALF, -0.5), pt(-CEIL_HALF, CEILING), pt(-HEADWALL_HALF, CEILING)],
        [pt(FLOOR_HALF, -0.5), pt(HEADWALL_HALF, -0.5), pt(HEADWALL_HALF, CEILING), pt(CEIL_HALF, CEILING)],
        # top of the headwall (0.6 m thick) and the cap over the hole behind it
        [pt(-HEADWALL_HALF, top), pt(HEADWALL_HALF, top), pt(HEADWALL_HALF, top, 0.6), pt(-HEADWALL_HALF, top, 0.6)],
        [pt(-HEADWALL_HALF, cover + 0.02, 0.6), pt(HEADWALL_HALF, cover + 0.02, 0.6),
         pt(HEADWALL_HALF, cover + 0.02, 9.5), pt(-HEADWALL_HALF, cover + 0.02, 9.5)],
    ]
    for k, f in enumerate(faces):
        face = bm.faces.new([V(c) for c in f])
        face.material_index = 1 if k == 4 else 0
    # the front faces must face out of the tunnel (against `inward`)
    bm.normal_update()
    bm.faces.ensure_lookup_table()
    for face in bm.faces[:3]:
        n = face.normal
        if n.x * dx + n.y * dy > 0:
            face.normal_flip()
    for face in bm.faces[3:]:
        if face.normal.z < 0:
            face.normal_flip()
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("portal_concrete", "#ECEAE4", roughness=0.85))
    mesh.materials.append(E.material("lawn"))
    return E.new_object(name, mesh, col)


def build():
    pts = tunnel_points()
    if len(pts) < 3:
        return
    col = E.collection("Landmark_BeachTunnel")
    build_shell(pts, col)
    build_lights(pts, col)
    cover = env_terrain.PROFILE.get("tunnel_cover_m", 4.8)
    for (p, inward), name in zip(env_terrain.portals(), ("Tunnel_PortalSouth", "Tunnel_PortalNorth")):
        build_portal(p, inward, cover, name, col)
