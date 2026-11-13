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


_drawn = {}


def _stays(i, j):
    """True if the 5 m cell (i, j) is drawn as it is: not replaced by the lattice and not left out under the corridor
    (V16e: a lattice edge next to a cell that is not drawn has nothing to match; held to the 5 m grid it stood up
    as a fin through the corridor's sand)."""
    if (i, j) in _state["cells"]:
        return False
    if (i, j) not in _drawn:
        _drawn[(i, j)] = not env_corridor.covers_cell(E.X0 + (i + 0.5) * E.STEP, E.Y0 + (j + 0.5) * E.STEP)
    return _drawn[(i, j)]


def _edge_distance(x, y):
    """Metres from (x, y) to the nearest 5 m cell around it that is not replaced (at most BLEND)."""
    i, j = _cell(x, y)
    best = BLEND
    for di in (-1, 0, 1):
        for dj in (-1, 0, 1):
            if not _stays(i + di, j + dj):
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
    return any(_stays(*c) for c in cells)


def _lattice(a, b):
    """(x, y, z) of lattice point (a, b): moved onto a kerb face if close to the kerb, the 5 m grid on the edge."""
    key = (a, b)
    if key not in _state["z"]:
        x, y = E.X0 + a * FINE, E.Y0 + b * FINE
        if _on_edge(a, b):
            _state["z"][key] = (x, y, E.coarse_ground_z(x, y) - _edge_sink(a, b))
        else:
            px, py = x, y
            f = _kerb_frame(x, y) if key in _state["snap"] else None
            if f is not None:
                sd, _k, (mx, my) = f
                to = FACE_LAND if sd < 0.05 else FACE_SAND
                px, py = x + mx * (to - sd), y + my * (to - sd)
            _state["z"][key] = (px, py, _surface(px, py, blend_at=(x, y)) - _sink(x, y))
    return _state["z"][key]


def _sink(x, y):
    """V16e: under the corridor the lattice sinks like the 5 m ground (build_world.build_ground)."""
    return env_corridor.SINK_UNDER_CORRIDOR * env_corridor.smoothstep((env_corridor.inside_depth(x, y) - 0.5) / 2.0)


def _edge_sink(a, b):
    """Sink of a lattice point on the edge of the replaced area: the 5 m ground's own, linear between its nodes."""
    i, j = a // SUB, b // SUB
    node = lambda u, v: _sink(E.X0 + u * E.STEP, E.Y0 + v * E.STEP)                     # noqa: E731
    if a % SUB == 0 and b % SUB == 0:
        return node(i, j)
    if a % SUB == 0:
        t = (b % SUB) / SUB
        return node(i, j) * (1 - t) + node(i, j + 1) * t
    t = (a % SUB) / SUB
    return node(i, j) * (1 - t) + node(i + 1, j) * t


HOLD_FROM, HOLD_GAP, HOLD_MIN = 9.0, 1.5, 7.5


def hold_back(pts, width):
    """V16e (env_corridor.widths hook): where a corridor row would cross the cove kerb more than HOLD_FROM metres from
    the centreline, the row ends HOLD_GAP before the kerb, and its neighbours widen again by RAMP_TAPER per row (the
    same rule as V15b's hold-back at the car-park ramp). Out there the rows fan out and meet the kerb at a slant, so
    the drop zig-zagged between rows: sand ramps against the sea wall, 2-3 m high since the cove is a walled basin.
    The 1 m lattice of this module takes those cells over. Rows that cross the kerb close to the road (the north
    apex, where env_corridor.snap_to_kerb follows it cleanly) keep the kerb."""
    n = len(pts)
    cap = {}
    for p in pts:
        if math.hypot(p["x"] - coast.COVE_CENTRE[0], p["y"] - coast.COVE_CENTRE[1]) > 90.0:
            continue
        for side in (1, -1):
            k = env_corridor.kerb_crossing(p, side)
            if k is not None and k > HOLD_FROM:
                cap[(p["i"], side)] = max(HOLD_MIN, k - HOLD_GAP)
    span = int(env_corridor.CORRIDOR / env_corridor.RAMP_TAPER) + 3
    held = {}
    for (i, side), o in cap.items():
        for d in range(-span, span + 1):
            j = (i + d) % n
            w = min(o + env_corridor.RAMP_TAPER * max(0, abs(d) - 1), width[j][side])
            if w < held.get((j, side), width[j][side]):
                held[(j, side)] = w
    for (j, side), w in held.items():
        width[j][side] = w
        env_corridor.HELD_BACK[(j, side)] = min(w, env_corridor.HELD_BACK.get((j, side), w))
    if held:
        print(f"  cove bank: {len(cap)} corridor rows end before the cove kerb, {len(held)} rows held back in all")
    return width


def land_beside_cove(x, y, z):
    """V16e (env_corridor.profile_height hook): the 5 m terrain height the corridor blends into, on the promenade next
    to the cove's sea wall. The 5 m grid cannot hold a wall: between a node on the promenade and one on the sand it
    slopes, so the promenade sagged towards the kerb (by up to the wall's height, 2-3 m). On the land side, within
    5 m of the kerb line, the height is the terrain as it was before the cove was cut (env_terrain._SHAPED), fading
    back to the grid by 8 m."""
    if not env_corridor._near_cove(x, y):
        return z
    dk, _k, sand = coast.kerb_side(x, y)
    if sand or dk > 8.0 or coast.in_cove(x, y) or coast.in_beach(x, y):
        return z
    shaped = env_terrain._bilinear(env_terrain._SHAPED.get("h") or E.DATA["grid"]["heights"], x, y)
    t = env_corridor.smoothstep((dk - 5.0) / 3.0)
    return shaped * (1 - t) + z * t


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
            if env_corridor.covers_cell(cx, cy):         # V16e: cells the corridor only touches are replaced too (the
                continue                                 # lattice sinks under it like the 5 m ground does): with a sea
                                                         # wall of 2-3 m such a cell was a sand ramp against the wall
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

    def surface(x, y):                                   # V16e: where the corridor has faces, its own surface holds
        return bank_z(x, y) if _cell(x, y) in _state["cells"] and not env_corridor.has_face(x, y) else prev(x, y)
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
    # V20e (Luc, 13 Nov, beach café: "the ground looks broken"): on the land side of the kerb the lattice rose a few
    # cm through the promenade paving beside the cove stairs (jagged grey points). There it stays 5 cm under it.
    import env_coast
    clamped = 0
    edge = {k for (a, b), k in index.items() if _on_edge(a, b)}       # shared with the 5 m terrain: left alone
    for k, (x, y, z) in enumerate(verts):
        if k in edge or coast.in_cove(x, y):
            continue
        top = env_coast.paving_z(x, y) - 0.05
        if z > top:
            verts[k] = (x, y, top)
            clamped += 1
    if clamped:
        print(f"  cove bank: {clamped} land-side vertices kept under the promenade paving (V20e)")
    mesh = bpy.data.meshes.new("GroundCoveBank")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(E.material("ground_map", "#A6AA82", roughness=0.95))
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        co = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = ((co.x - E.X0) / (E.X1 - E.X0), (co.y - E.Y0) / (E.Y1 - E.Y0))
    E.new_object("World_GroundCoveBank", mesh, E.collection("World_Ground"))
    print(f"  cove bank: {len(faces)} quads ({2 * len(faces)} triangles)")
