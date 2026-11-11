"""The cove's bank outside the track corridor (V10d, 2026-11-11, photos K04-K07).

The cove's sand lies coast.KERB_DROP (0.9 m) below the promenade at the granite kerb. Along the track the high-res
corridor carries that drop (env_corridor.snap_to_kerb); on the rest of the arc (the east arc, which the route does
not touch) the 5 m world grid could not: a cell that the kerb crosses has corners on the promenade and in the cove,
so the sand rose over up to 5 m to the kerb top. Here those cells are replaced by a 1 m lattice:

- cells: every 5 m cell whose centre lies within REACH of the kerb line and that the corridor does not touch (no
  corridor faces on a 1.25 m grid over it, nor CLEAR beyond) and that is not a portal hole; cells where nothing
  changes are left alone unless a changing cell is next to them (they carry the blend);
- height: the target is the promenade height at the kerb (env_terrain.kerb_height) on the land side and under the
  kerb blocks, and the cove's own sand (env_terrain.coast_height = coast.cove_z: 0.9 m under the kerb, then the
  bowl) on the sand side; it blends into the 5 m grid over BLEND metres towards the edge of the replaced area, and on
  that edge it is exactly the 5 m grid (lattice points on the cell edges, where the grid is linear), so no cracks;
- the corners of every lattice square that the kerb line crosses move onto the kerb block's two faces (0.22 m on
  the land side, 0.24 m on the sand side, at most 1.2 m), so the whole drop lies under the 0.4 m kerb blocks
  (env_beachfront.build_kerb) instead of on a slope of up to 1.4 m;
- E.ground_z returns the target surface inside the replaced cells (the kerb, props and dressing follow it).
"""
import math

import coast
import env_common as E
import env_corridor
import env_terrain

FINE = 1.0           # m lattice
SUB = int(round(E.STEP / FINE))
REACH = 6.6          # m from the kerb line to a cell centre: every point within 3 m of the kerb is in a replaced cell
BLEND = 2.0          # m over which the bank surface blends into the 5 m grid at the edge of the replaced cells
CLEAR = 0.5          # m: a replaced cell keeps this far from any corridor face
UNDER_KERB = 0.22    # m: up to this far on the sand side of the kerb line the promenade height holds (under the
                     # kerb block, KERB_W / 2 = 0.2), beyond it the sand
FACE_LAND, FACE_SAND = -0.22, 0.24   # m from the kerb line: where the lattice points of a crossing quad move to

_state = {"cells": set(), "z": {}, "snap": set()}
_KERB_SEGS = list(zip(coast.COVE_KERB, coast.COVE_KERB[1:]))
_ENDS = (coast.COVE_KERB[0], coast.COVE_KERB[-1])


def _cell(x, y):
    return int(math.floor((x - E.X0) / E.STEP)), int(math.floor((y - E.Y0) / E.STEP))


def covers_cell(cx, cy):
    """True if the 5 m cell centred at (cx, cy) is replaced by the bank lattice."""
    return _cell(cx, cy) in _state["cells"]


def _near_corridor(x0, y0):
    steps = [-CLEAR] + [k * 1.25 for k in range(5)] + [E.STEP + CLEAR]
    return any(env_corridor.has_face(x0 + ox, y0 + oy) for ox in steps for oy in steps)


def _kerb_frame(x, y):
    """(signed distance towards the sand, nearest kerb point, unit normal towards the sand) or None beyond the ends."""
    _d, (kx, ky), k = coast._closest(x, y, _KERB_SEGS)
    if (kx, ky) in _ENDS:
        return None
    (ax, ay), (bx, by) = _KERB_SEGS[k]
    tl = math.hypot(bx - ax, by - ay) or 1.0
    mx, my = -(by - ay) / tl, (bx - ax) / tl
    if mx * (coast.COVE_CENTRE[0] - kx) + my * (coast.COVE_CENTRE[1] - ky) < 0:
        mx, my = -mx, -my
    return (x - kx) * mx + (y - ky) * my, (kx, ky), (mx, my)


def _target(x, y, coarse):
    f = _kerb_frame(x, y)
    if f is None:
        return coarse                                    # beyond either end of the kerb: the grid as it was
    sd, (kx, ky), _m = f
    if sd < UNDER_KERB:
        return env_terrain.kerb_height(kx, ky)
    if not coast.in_cove(x, y):
        return coarse
    return env_terrain.coast_height(x, y, coarse)


def _edge_distance(x, y):
    """Metres from (x, y) to the nearest 5 m cell around it that is not replaced (at most BLEND)."""
    i, j = _cell(x, y)
    best = BLEND
    for di in (-1, 0, 1):
        for dj in (-1, 0, 1):
            if (i + di, j + dj) in _state["cells"]:
                continue
            x0, y0 = E.X0 + (i + di) * E.STEP, E.Y0 + (j + dj) * E.STEP
            dx = max(x0 - x, 0.0, x - (x0 + E.STEP))
            dy = max(y0 - y, 0.0, y - (y0 + E.STEP))
            best = min(best, math.hypot(dx, dy))
    return best


def _surface(x, y, blend_at=None):
    coarse = E.coarse_ground_z(x, y)
    bx, by = blend_at or (x, y)
    t = env_corridor.smoothstep(_edge_distance(bx, by) / BLEND)
    return coarse + t * (_target(x, y, coarse) - coarse)


def _on_edge(a, b):
    """True if lattice point (a, b) lies on the edge of the replaced area (shared with a cell that stays)."""
    if a % SUB and b % SUB:
        return False
    i, j = a // SUB, b // SUB
    cells = {(i - (a % SUB == 0 and di), j - (b % SUB == 0 and dj)) for di in (0, 1) for dj in (0, 1)}
    return not all(c in _state["cells"] for c in cells)


def _lattice(a, b):
    """(x, y, z) of lattice point (a, b): moved onto a kerb face if close to the kerb, the 5 m grid on the edge."""
    key = (a, b)
    if key not in _state["z"]:
        x, y = E.X0 + a * FINE, E.Y0 + b * FINE
        if _on_edge(a, b):
            _state["z"][key] = (x, y, E.coarse_ground_z(x, y))
        else:
            px, py = x, y
            f = _kerb_frame(x, y) if key in _state["snap"] else None
            if f is not None:
                sd, _k, (mx, my) = f
                to = FACE_LAND if sd < 0.05 else FACE_SAND
                px, py = x + mx * (to - sd), y + my * (to - sd)
            _state["z"][key] = (px, py, _surface(px, py, blend_at=(x, y)))
    return _state["z"][key]


def bank_z(x, y):
    """Height of the bank surface at (x, y)."""
    return _surface(x, y)


def plan():
    """Choose the cells, install the bank surface into E.ground_z. Call after env_corridor.build_data()."""
    kerb = coast.COVE_KERB
    xs, ys = [p[0] for p in kerb], [p[1] for p in kerb]
    cand = set()
    for j in range(E.ROWS - 1):
        y0 = E.Y0 + j * E.STEP
        if not (min(ys) - REACH - E.STEP < y0 < max(ys) + REACH):
            continue
        for i in range(E.COLS - 1):
            x0 = E.X0 + i * E.STEP
            if not (min(xs) - REACH - E.STEP < x0 < max(xs) + REACH):
                continue
            cx, cy = x0 + E.STEP / 2, y0 + E.STEP / 2
            if coast.kerb_point(cx, cy)[0] > REACH or env_terrain.in_portal_hole(cx, cy):
                continue
            if env_corridor.covers_cell(cx, cy) or _near_corridor(x0, y0):
                continue
            cand.add((i, j))
    _state["cells"] = cand
    changing = set()
    for i, j in cand:                                    # where would the surface move by more than 3 cm?
        x0, y0 = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
        if any(abs(_target(x0 + u, y0 + v, E.coarse_ground_z(x0 + u, y0 + v)) - E.coarse_ground_z(x0 + u, y0 + v)) > 0.03
               for u in (0.5, 1.5, 2.5, 3.5, 4.5) for v in (0.5, 1.5, 2.5, 3.5, 4.5)):
            changing.add((i, j))
    _state["cells"] = {(i, j) for i, j in cand
                       if any((i + di, j + dj) in changing for di in (-1, 0, 1) for dj in (-1, 0, 1))}
    _state["z"] = {}
    snap = set()                                         # corners of the lattice squares the kerb line crosses
    for i, j in _state["cells"]:
        for a in range(i * SUB, (i + 1) * SUB):
            for b in range(j * SUB, (j + 1) * SUB):
                corners = [(a, b), (a + 1, b), (a + 1, b + 1), (a, b + 1)]
                frames = [_kerb_frame(E.X0 + u * FINE, E.Y0 + v * FINE) for u, v in corners]
                if any(f is None or abs(f[0]) > 1.5 for f in frames):
                    continue
                if len({f[0] < 0.05 for f in frames}) == 2:
                    snap.update(c for c in corners if not _on_edge(*c))
    _state["snap"] = snap
    prev = E._surface["fn"] or E.coarse_ground_z

    def surface(x, y):
        return bank_z(x, y) if _cell(x, y) in _state["cells"] else prev(x, y)
    E.set_surface(surface)
    print(f"  cove bank: {len(_state['cells'])} cells of the 5 m grid replaced by a {FINE} m lattice along the cove kerb "
          f"({len(changing)} change)")


def build_mesh():
    """World_GroundCoveBank: the lattice over the replaced cells, ground-map material, UVs = world position."""
    import bpy
    verts, index, faces = [], {}, []

    def vid(a, b):
        if (a, b) not in index:
            index[(a, b)] = len(verts)
            verts.append(_lattice(a, b))
        return index[(a, b)]
    for i, j in sorted(_state["cells"]):
        for a in range(i * SUB, (i + 1) * SUB):
            for b in range(j * SUB, (j + 1) * SUB):
                faces.append((vid(a, b), vid(a + 1, b), vid(a + 1, b + 1), vid(a, b + 1)))
    if not faces:
        return
    mesh = bpy.data.meshes.new("GroundCoveBank")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(E.material("ground_map", "#A6AA82", roughness=0.95))
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        co = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = ((co.x - E.X0) / (E.X1 - E.X0), (co.y - E.Y0) / (E.Y1 - E.Y0))
    E.new_object("World_GroundCoveBank", mesh, E.collection("World_Ground"))
    print(f"  cove bank: {len(faces)} quads ({2 * len(faces)} triangles)")
