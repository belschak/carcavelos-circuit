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
FOOTWAY_INNER = 0.065                    # footway height at the road edge: flush with the road (6 cm lift)
CEILING = 3.9                            # clear height
SLAB = 0.7
CEIL_HALF = FLOOR_HALF + CEILING         # 45 degree walls
MURAL_TILE_M = 12.0                      # one mural texture repeat along the wall
HEADWALL_HALF = 14.0

# V16e (survey V15, 3.3 / Appendix C 2.7 and 3.6): the tunnel floor is pale beige in-situ concrete, the ceiling flat
# board-marked concrete, the ramp's walls and the north portal the same sand beige (#c8b890). Colour only: the
# materials keep their names, they get these textures (make_coast_textures.py) through env_materials.TEXTURED.
import env_materials                                                            # noqa: E402
env_materials.TEXTURED["W_T_tunnel_floor"] = ("concrete_sand", 0.8)
env_materials.TEXTURED["W_tunnel_concrete"] = ("coast_boardmarked", 0.9)
env_materials.TEXTURED["W_T_cut_wall"] = ("coast_render_beige", 0.9)
env_materials.MATTE.add("W_T_tunnel_floor")
LIGHT_STEP, LIGHT_LEN = 3.0, 1.2          # linear battens along both wall tops, 3 m apart (W28_h135)


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
    # footways (strips between road edge and wall foot). They start flush with the road and rise gently to the
    # wall foot: the old 12 cm step at the road edge threw karts that drove close to the wall.
    for (pa, ra), (pb, rb) in zip(rings, rings[1:]):
        for side in (1, -1):
            _, na = frame(pts, pts.index(pa))
            _, nb = frame(pts, pts.index(pb))
            vs = [bm.verts.new(at(pa, na, side * (E.ROUTE_WIDTH / 2), FOOTWAY_INNER)), bm.verts.new(at(pa, na, side * FLOOR_HALF, 0.12)),
                  bm.verts.new(at(pb, nb, side * FLOOR_HALF, 0.12)), bm.verts.new(at(pb, nb, side * (E.ROUTE_WIDTH / 2), FOOTWAY_INNER))]
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
    """V16e: linear white battens (1.2 m) fixed to the top of both walls just under the ceiling, 3 m apart."""
    bm = bmesh.new()
    count = 0
    s0, s1 = pts[1]["s"], pts[-2]["s"]
    k = 0
    while s0 + k * LIGHT_STEP <= s1:
        s = s0 + k * LIGHT_STEP
        i = max(j for j in range(len(pts) - 1) if pts[j]["s"] <= s)
        a, b = pts[i], pts[i + 1]
        f = (s - a["s"]) / max(b["s"] - a["s"], 1e-6)
        x, y, z = a["x"] + (b["x"] - a["x"]) * f, a["y"] + (b["y"] - a["y"]) * f, a["track_z"] + (b["track_z"] - a["track_z"]) * f
        t, nrm = frame(pts, i)
        for side in (1, -1):
            # on the sloping wall, 0.12 to 0.30 m under the ceiling, 3 cm in front of it
            lo, hi = CEILING - 0.30, CEILING - 0.12
            corners = [(x + t[0] * al + nrm[0] * side * (FLOOR_HALF + h - 0.04), y + t[1] * al + nrm[1] * side * (FLOOR_HALF + h - 0.04),
                        z + h - 0.02) for al, h in ((-LIGHT_LEN / 2, lo), (LIGHT_LEN / 2, lo), (LIGHT_LEN / 2, hi), (-LIGHT_LEN / 2, hi))]
            face = bm.faces.new([bm.verts.new(c) for c in corners])
            face.normal_update()
            if face.normal.x * nrm[0] * side + face.normal.y * nrm[1] * side > 0:      # look into the tunnel
                face.normal_flip()
            count += 1
        k += 1
    mesh = bpy.data.meshes.new("Tunnel_Lights")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("tunnel_light", "#FFF4D6", roughness=0.5, emission="#FFF4D6"))
    print(f"  tunnel: {count} wall-top light battens, {LIGHT_STEP} m apart")
    return E.new_object("Tunnel_Lights", mesh, col)


def build_portal(p, inward, cover, name, col, beige=False, closed=False):
    """Headwall with the trapezoid opening, plus a cap over the terrain hole behind it. V16e: `beige` = the north
    portal, sand-beige render like the ramp's walls (survey 3.6); the south one stays white concrete.
    V18h: `closed` = the structure is a closed abutment: the parapet has a back, the headwall has ends, and a wall
    hangs from both side edges of the cap down into the ground. The terrain is open under the cap (in_portal_hole)
    and lies up to 2 m lower beside it, so one looked under the cap into the hole and at the tunnel's back."""
    dx, dy = inward
    nrm = (-dy, dx)
    top = cover + 1.0
    z0 = p["track_z"]

    def pt(side, z, along=0.0):
        return (p["x"] + nrm[0] * side + dx * along, p["y"] + nrm[1] * side + dy * along, z0 + z)

    # V18h: the terrain's hole reaches to 12 m behind the south mouth (cells are dropped whole); the cap ended at
    # 9.5 m, so from the N6's sidewalk one looked through a 5 x 2.5 m gap onto the tunnel's ceiling from behind
    far = 13.0 if closed else 9.5
    bm = bmesh.new()
    V = lambda c: bm.verts.new(c)
    faces = [
        [pt(-HEADWALL_HALF, CEILING), pt(HEADWALL_HALF, CEILING), pt(HEADWALL_HALF, top), pt(-HEADWALL_HALF, top)],
        [pt(-HEADWALL_HALF, -0.5), pt(-FLOOR_HALF, -0.5), pt(-CEIL_HALF, CEILING), pt(-HEADWALL_HALF, CEILING)],
        [pt(FLOOR_HALF, -0.5), pt(HEADWALL_HALF, -0.5), pt(HEADWALL_HALF, CEILING), pt(CEIL_HALF, CEILING)],
        # top of the headwall (0.6 m thick) and the cap over the hole behind it
        [pt(-HEADWALL_HALF, top), pt(HEADWALL_HALF, top), pt(HEADWALL_HALF, top, 0.6), pt(-HEADWALL_HALF, top, 0.6)],
        [pt(-HEADWALL_HALF, cover + 0.02, 0.6), pt(HEADWALL_HALF, cover + 0.02, 0.6),
         pt(HEADWALL_HALF, cover + 0.02, far), pt(-HEADWALL_HALF, cover + 0.02, far)],
    ]
    extra = []                                           # (corners, outward direction in plan)
    if closed:
        low, H = -1.6, HEADWALL_HALF                     # 1.6 m under the road: under every ground beside the portal
        extra.append(([pt(-H, cover + 0.02, 0.6), pt(H, cover + 0.02, 0.6), pt(H, top, 0.6), pt(-H, top, 0.6)], (dx, dy)))
        for s in (-1, 1):
            out = (nrm[0] * s, nrm[1] * s)
            extra.append(([pt(s * H, low, 0.0), pt(s * H, low, 0.6), pt(s * H, top, 0.6), pt(s * H, top, 0.0)], out))
            extra.append(([pt(s * H, low, 0.6), pt(s * H, low, far), pt(s * H, cover + 0.02, far), pt(s * H, cover + 0.02, 0.6)], out))
    for k, f in enumerate(faces):
        face = bm.faces.new([V(c) for c in f])
        face.material_index = 1 if k == 4 else 0
    for f, out in extra:
        face = bm.faces.new([V(c) for c in f])
        face.material_index = 0
        face.normal_update()
        if face.normal.x * out[0] + face.normal.y * out[1] < 0:
            face.normal_flip()
    # the front faces must face out of the tunnel (against `inward`)
    bm.normal_update()
    bm.faces.ensure_lookup_table()
    for face in bm.faces[:3]:
        n = face.normal
        if n.x * dx + n.y * dy > 0:
            face.normal_flip()
    for face in bm.faces[3:5]:
        if face.normal.z < 0:
            face.normal_flip()
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    uv = mesh.uv_layers.new(name="UVMap")              # the ramp walls' mapping (2 m tiles), for the beige render
    for loop in mesh.loops:
        co = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = ((co.x * nrm[0] + co.y * nrm[1]) / 2.0, co.z / 2.0)
    mesh.materials.append(portal_material(beige))
    mesh.materials.append(E.material("lawn"))
    return E.new_object(name, mesh, col)


def portal_material(beige):
    if beige:
        return E.material("T_cut_wall", CUT_WALL_COLOUR, roughness=0.9)
    return E.material("portal_concrete", "#ECEAE4", roughness=0.85)


BEAM_HALF = HEADWALL_HALF - 0.4              # V10c: broad flat lintel over each portal (S06, S07)
BEAM_Z0, BEAM_Z1, BEAM_OUT = CEILING, CEILING + 1.4, 0.9
BEAM_OUT, BEAM_CHAMFER = 1.5, 0.8        # V16e: a deep fascia whose underside slopes up outwards (B01_h000, B02 zoom)
CUT_WALL_COLOUR = "#C8B890"              # V16e: sand beige (survey 3.6; was V10c's warm ochre #DAC399)


def box_faces(bm, corners_lo, corners_hi):
    """Closed box from two rectangles (4 corners each, same order, counter-clockwise seen from above)."""
    lo = [bm.verts.new(c) for c in corners_lo]
    hi = [bm.verts.new(c) for c in corners_hi]
    bm.faces.new(lo[::-1])
    bm.faces.new(hi)
    for k in range(4):
        bm.faces.new((lo[k], lo[(k + 1) % 4], hi[(k + 1) % 4], hi[k]))


def build_portal_beam(p, inward, name, col, beige=False):
    """Deep fascia in front of the headwall opening: a slab whose underside is chamfered, rising outwards from the
    clear height (so nothing is lower than the opening: the drive corridor and the chase camera stay as they were)."""
    dx, dy = inward
    nrm = (-dy, dx)
    z0 = p["track_z"]

    def pt(side, z, along):
        return (p["x"] + nrm[0] * side + dx * along, p["y"] + nrm[1] * side + dy * along, z0 + z)
    bm = bmesh.new()
    profile = [(0.0, BEAM_Z0), (-BEAM_OUT, BEAM_Z0 + BEAM_CHAMFER), (-BEAM_OUT, BEAM_Z1), (0.0, BEAM_Z1)]   # (along, z)
    left = [bm.verts.new(pt(-BEAM_HALF, z, al)) for al, z in profile]
    right = [bm.verts.new(pt(BEAM_HALF, z, al)) for al, z in profile]
    bm.faces.new(left)
    bm.faces.new(right[::-1])
    for k in range(4):
        bm.faces.new((left[k], left[(k + 1) % 4], right[(k + 1) % 4], right[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        co = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = ((co.x * nrm[0] + co.y * nrm[1]) / 2.0, co.z / 2.0)
    mesh.materials.append(portal_material(beige))
    return E.new_object(name, mesh, col)


WING = dict(inner=6.75, reach=12.0, out=9.6)   # V16e: the south portal's east wing wall (B01_h045, h090, h135)


def build_wing_facet(p, inward, cover, name, col):
    """One folded white wing wall on the east side of the south portal (right of the driving direction): two
    triangular facets from the deck's corner down to the ground WING['reach'] metres in front of the mouth. It stands
    on the cutting's bank, WING['inner'] m and more from the centreline (the opening and the bank are untouched).

    V18h: a closed body. The facets were three loose triangles: their lower edge hung up to 1.2 m over the bank and
    from the road one looked under them into a dark hollow (Luc, 12 Nov: "you look into the void"). Now a wall hangs
    from every free edge down to WING_FOOT under the road level (below any ground there), the side against the
    headwall is closed too, and every face looks outwards."""
    dx, dy = inward
    nrm = (-dy, dx)
    z0 = p["track_z"]

    def pt(side, z, along):
        return (p["x"] + nrm[0] * side + dx * along, p["y"] + nrm[1] * side + dy * along, z)
    foot_near = pt(-WING["inner"], z0 + 1.2, -0.05)
    fx, fy, _z = pt(-WING["out"], 0.0, -WING["reach"])
    foot_far = (fx, fy, E.ground_z(fx, fy) - 0.3)
    top = pt(-(CEIL_HALF + 0.15), z0 + BEAM_Z1, -0.05)
    back = pt(-HEADWALL_HALF, z0 + BEAM_Z1, -0.05)
    hx, hy, _z = pt(-HEADWALL_HALF, 0.0, -0.05)
    back_foot = (hx, hy, E.ground_z(hx, hy) - 0.3)
    low = z0 - WING_FOOT
    centre = [sum(c[k] for c in (foot_near, foot_far, back_foot)) / 3.0 for k in range(2)]
    bm = bmesh.new()
    for tri in ((foot_near, foot_far, top), (top, foot_far, back), (back, foot_far, back_foot)):
        f = bm.faces.new([bm.verts.new(c) for c in tri])
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()

    def skirt(a, b, steps):
        """Upright wall under the edge a-b, down to `low`; it looks away from the wing's middle."""
        for k in range(steps):
            q0 = [a[j] + (b[j] - a[j]) * k / steps for j in range(3)]
            q1 = [a[j] + (b[j] - a[j]) * (k + 1) / steps for j in range(3)]
            f = bm.faces.new([bm.verts.new(c) for c in ((q0[0], q0[1], low), (q1[0], q1[1], low), q1, q0)])
            f.normal_update()
            mid = ((q0[0] + q1[0]) / 2 - centre[0], (q0[1] + q1[1]) / 2 - centre[1])
            if f.normal.x * mid[0] + f.normal.y * mid[1] < 0:
                f.normal_flip()
    skirt(foot_near, foot_far, 6)                        # the side towards the road: was open underneath
    skirt(foot_far, back_foot, 6)                        # the outer side, towards the plaza
    f = bm.faces.new([bm.verts.new(c) for c in ((foot_near[0], foot_near[1], low), foot_near, top, back,
                                                (back_foot[0], back_foot[1], low))])   # against the headwall
    f.normal_update()
    if f.normal.x * dx + f.normal.y * dy < 0:
        f.normal_flip()
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(portal_material(False))
    return E.new_object(name, mesh, col)


WING_FOOT = 1.6        # m under the road level at the mouth: the wing's walls end here, under the ground


def build_portal_jambs(p, inward, name, col):
    """Closes the slit beside the mouth of a portal that opens onto a walled ramp (V10g's straight ramp, north end).

    The tunnel's walls start 0.6 m outside the road edge (footway), the ramp's 45 degree walls right at the road edge:
    two parallel planes 0.6 m apart. From inside, looking forward along the tunnel wall, the eye went between them,
    out through the headwall's opening and behind the ramp wall, where nothing is drawn (Luc, 2026-11-11: "where the
    tunnel walls on the left and right stop, you can see into nothing"). One upright strip per side in the portal's
    plane, from the ramp wall's plane to behind the tunnel wall's, seen from both sides."""
    dx, dy = inward
    nrm = (-dy, dx)
    z0 = p["track_z"]
    half = E.half_width(p)

    def pt(side, z, along=0.03):
        return (p["x"] + nrm[0] * side + dx * along, p["y"] + nrm[1] * side + dy * along, z0 + z)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    lo, hi = -0.4, CEILING + 0.05
    for s in (1, -1):
        # inner edge 5 cm behind the ramp wall's plane (road edge + height), so it never shows as a fin in front of it
        ring = [pt(s * (half + lo + 0.10), lo), pt(s * (FLOOR_HALF + lo + 0.25), lo),
                pt(s * (FLOOR_HALF + hi + 0.25), hi), pt(s * (half + hi + 0.10), hi)]
        for corners in (ring, ring[::-1]):
            face = bm.faces.new([bm.verts.new(c) for c in corners])
            for loop in face.loops:                              # the ramp walls' own mapping (env_corridor), so it reads as wall
                loop[uv].uv = (loop.vert.co.x / 2.0, loop.vert.co.z / 2.0)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(E.material("T_cut_wall", CUT_WALL_COLOUR, roughness=0.9))
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
        north = name.endswith("North")
        build_portal(p, inward, cover, name, col, beige=north, closed=not north)   # V18h: the south abutment is closed
        build_portal_beam(p, inward, name.replace("Portal", "PortalBeam"), col, beige=north)
        if not north:
            build_wing_facet(p, inward, cover, "Tunnel_PortalWingSouth", col)
        if any(c["from_index"] <= p["i"] <= c["to_index"] for c in (E.ROUTE or {}).get("wall_cuts", [])):
            build_portal_jambs(p, inward, name.replace("Portal", "PortalJambs") + "_NoCollide", col)
    wall = E.material("T_cut_wall", CUT_WALL_COLOUR, roughness=0.9)        # V10c: ochre render (texture in env_materials)
    wall.diffuse_color = E.hex_rgba(CUT_WALL_COLOUR)
    bsdf = wall.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = E.hex_rgba(CUT_WALL_COLOUR)
