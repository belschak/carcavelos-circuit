"""North side of the campus (V16 visual pass, 2026-11-11): the drop-off forecourt, the avenue's entrance side and the
east park, after the photosphere survey V15 (atlas report V15-photosphere-survey.md: 3.1, 3.9, items 22 and 29,
Appendix A area N, Appendix I area ST; panoramas N01-N05, N15, ST01, ST02). Nothing is copied from the images:
positions, sizes and averaged colours only; no lettering, no logos, no number plates, no car makes.

Objects (collection World_North, exported with CampusWorld.fbx):
  North_Forecourt            pale granite setts on the drop-off forecourt (the entrance plaza's calcada material, rows
                             along the hall's north-east face), draped over the built terrain like the plaza sheet
                             (env_entrance.drape: 7 cm over the ground, down to 4 mm at the foot of the road's
                             shoulder, nothing within 4.95 m of the centreline). Layers of the same sheet: the flush
                             ring of darker setts (9.3 m across, centre (80.7, 177)), the buff concrete north of
                             Student Central, the pink-buff and ochre edge bands on the east side, two rust-brown
                             manhole covers. Cut out: the hall's outline (Noah's hall stands there in Unity), the
                             other buildings, the street layers, the stair and the waste station.
  North_ForecourtRing_NoCollide   only with RING_ON_ROAD = True (off by default, see there): the part of the dark ring
                             that crosses the race road: paint 5 cm over the road, only on its flat part (up to 3.5 m
                             from the centreline; the kerb and the shoulder between 3.5 and 4.95 m stay free), no
                             collider. The road itself is untouched.
  North_Structures           solid things: the 3-riser stair on the forecourt's east edge, the waste station (8
                             stainless cylinders on a low platform), the pylon at the avenue (a blank dark slab with a
                             blank board, 5 m), four plain flag poles, the seat ring of the east park
  North_Park_NoCollide       the east park's timber-sleeper path and the red-brown disc inside the seat ring (flush)
Dressing (dress(), after env_entrance.dress, as an explicit edit of the lists): the cafe tables, chairs, parasols and
planters on the forecourt go (survey: there are none; cars park there); parked cars in a row parallel to the
colonnade where they keep 9 m from the centreline; steel posts along the bus lay-by; whatever stood on the new
avenue paving, under the new solids or on the edge bands goes. Everything else stays as frozen.
The avenue's own paving (asphalt, bus lay-by, calcada strip) is street_plan.avenue_entrance().
Nothing solid is closer than CLEAR (6.6 m) to the race centreline: the build stops otherwise.
"""
import json
import math
import os

import campus_plan as C
import entrance_plan as P
import env_common as E

COLLECTION = "World_North"
CLEAR = 6.6                  # m from the race centreline: nothing solid of ours stands closer
CAR_CLEAR = 9.0              # m: parked cars (centre), as street_plan.CAR_CLEAR

# ----------------------------------------------------------------------------------------------------------------
# Plan (world metres, x east, y north). Sources: ortho_campus.jpg (3 px per metre), N02, N03 (2019/2020).
# ----------------------------------------------------------------------------------------------------------------
FACE_U = (-0.3654, 0.9309)   # along the hall's north-east face (76.7, 158.7) -> (66.6, 184.5): the sett rows
EDGE = [(101.8, 153.5), (94.5, 179.5)]          # east edge of the setts, south -> north (survey: x 95-97 at y 174)
# the sheet: from the hall's face (cut at its outline) to the east edge, north to the foot of the bank at y 193,
# with the wedge north of the lawn's corner where the waste station stands
FORECOURT = [(52.0, 176.0), (72.0, 156.5), (82.0, 153.0), (93.0, 149.5), (100.5, 148.0), EDGE[0], EDGE[1],
             (112.0, 186.7), (110.0, 193.0), (52.0, 193.0)]
BUFF = [(82.0, 153.0), (83.0, 163.5), (99.0, 163.5), EDGE[0], (100.5, 148.0), (93.0, 149.5)]   # in-situ concrete at SC
BUFF_COLOUR, PINK_BUFF, OCHRE = "car_sand", "cream", "ochre"    # street palette cells (survey #998c61 shade, #e5d0b5, #ebd0a9)
BAND_PINK, BAND_OCHRE = 1.3, 1.0                                # m: widths of the two edge bands east of the setts
RING_C, RING_R, RING_BAND = (80.7, 177.0), 4.65, 0.35           # dark sett ring: outer radius, band width
# The ring crosses the race road (the centreline passes 1 m from its centre). Painting it on the road needs one line
# in qa_geometry.py: its 'covered' check allows only ALLOWED_ON_ROAD = {Track_Kerbs, Track_EdgeLines} over the road
# and reports this paint as two new 'covered' runs (s 1180, 1186). So it is OFF until the lead / Luc decide:
# set RING_ON_ROAD = True and add "North_ForecourtRing_NoCollide" to that set.
RING_ON_ROAD = False
RING_ROAD_REACH = 3.5        # m from the centreline: the ring's paint on the road keeps inside the edge lines and kerbs
RING_LIFT = 0.05             # m over the road (the edge lines lie at 4 cm; 2 cm flickered there)
MANHOLES = [(86.2, 176.6), (88.0, 174.4)]                       # round rust-brown covers, 0.7 m
STAIR_Y, STAIR_W, STAIR_RISE, STAIR_TREAD = 174.0, 4.0, 0.13, 0.4   # 3 risers at (95, 174) (real: down to the lawn)
WASTE_C, WASTE_U, WASTE_HALF = (100.5, 184.6), (0.9205, 0.3907), (4.3, 2.1)   # platform at (96..104, 181..187)
CARS = [(89.2, 164.6), (89.6, 167.3), (89.9, 170.0), (90.1, 172.7)]           # centres; nose towards the colonnade
CAR_BEARING = 255.0
CAFE = ("Prop_BistroTable", "Prop_BistroChair", "Prop_Parasol", "Prop_TroughBloom", "Prop_PlanterRound")
# avenue entrance side
POST_STEP, POST_OFF = 2.75, 0.6            # steel posts on the calcada, 0.6 m east of the bus lay-by
PYLON = (-86.0, 102.9)                     # survey: about (-86, 102), on the calcada
FLAGPOLES = [(-84.5, 99.5), (-82.0, 99.1), (-79.5, 98.7), (-77.0, 98.3)]      # behind it on the lawn, 8 m, no flags
# east park (low priority, 60-80 m from the race line)
PARK_PATH = [(117.0, 131.0), (153.0, 145.1)]     # timber sleepers 2.0 x 0.25 m laid across, every 0.75 m
SEAT_RING_C, SEAT_RING_R = (157.7, 147.0), 5.0   # cream concrete ring bench, outer diameter 10 m, four gaps

_REPORT = []                 # (what, least distance to the centreline)


def _clear(what, pts, limit=CLEAR):
    import street_plan as S
    d = min(S.route_distance(x, y) for x, y in pts)
    _REPORT.append((what, d))
    if d < limit:
        raise RuntimeError(f"north: '{what}' stands {d:.2f} m from the race centreline (limit {limit} m)")
    return d


def _circle(c, r, n):
    return [(c[0] + r * math.cos(2 * math.pi * k / n), c[1] + r * math.sin(2 * math.pi * k / n)) for k in range(n)]


def _edge_frame():
    """(unit vector along the east edge northwards, unit vector east across it)."""
    (ax, ay), (bx, by) = EDGE
    d = math.hypot(bx - ax, by - ay)
    u = ((bx - ax) / d, (by - ay) / d)
    return u, (u[1], -u[0])


def stair_ring():
    """Footprint of the stair: STAIR_W along the edge at y = STAIR_Y, from the sett edge 3 treads and a landing east."""
    u, e = _edge_frame()
    (ax, ay) = EDGE[0]
    t = (STAIR_Y - ay) / u[1]
    c = (ax + u[0] * t, ay + u[1] * t)
    depth = 3 * STAIR_TREAD + 1.2
    h = STAIR_W / 2
    return [(c[0] - u[0] * h, c[1] - u[1] * h), (c[0] - u[0] * h + e[0] * depth, c[1] - u[1] * h + e[1] * depth),
            (c[0] + u[0] * h + e[0] * depth, c[1] + u[1] * h + e[1] * depth), (c[0] + u[0] * h, c[1] + u[1] * h)], c, u, e


def waste_ring(pad=0.0):
    (cx, cy), u, (hu, hv) = WASTE_C, WASTE_U, WASTE_HALF
    v = (-u[1], u[0])
    return [(cx + u[0] * a * (hu + pad) + v[0] * b * (hv + pad), cy + u[1] * a * (hu + pad) + v[1] * b * (hv + pad))
            for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]


# =====================================================================================================
# The forecourt sheet
# =====================================================================================================
def build_forecourt(col):
    import env_campus
    import env_entrance as EN
    import street_plan as S
    t = P.CALCADA_TILE
    uv = ((0.0, 0.0), FACE_U, t, t)
    layers = [{"name": "forecourt setts", "ring": list(FORECOURT), "mat": "calcada", "uv": uv},
              {"name": "buff concrete at Student Central", "ring": list(BUFF), "mat": "pal:" + BUFF_COLOUR, "uv": None},
              {"name": "pink-buff edge band", "ring": S.ribbon(EDGE, -BAND_PINK, 0.0), "mat": "pal:" + PINK_BUFF, "uv": None},
              {"name": "ochre edge band", "ring": S.ribbon(EDGE, -BAND_PINK - BAND_OCHRE, -BAND_PINK), "mat": "pal:" + OCHRE,
               "uv": None},
              {"name": "dark sett ring", "ring": _circle(RING_C, RING_R, 48), "mat": "pal:setts_dark", "uv": None},
              {"name": "setts inside the ring", "ring": _circle(RING_C, RING_R - RING_BAND, 48), "mat": "calcada", "uv": uv}]
    for c in MANHOLES:
        layers.append({"name": "manhole cover", "ring": _circle(c, 0.35, 10), "mat": "pal:wood_dark", "uv": None})
    hall = next([tuple(p) for p in b["points"]] for b in E.DATA["buildings"] if b["id"] == C.MAIN_ID)
    box = [(50.0, 146.0), (114.0, 146.0), (114.0, 195.0), (50.0, 195.0)]
    cutters = EN._cutters(ring=box, main=env_campus.grow(env_campus.ccw(hall), 0.2))
    cutters += [("Student Central", env_campus.grow(env_campus.ccw(list(C.SC_RING)), 0.1)),
                ("stair", stair_ring()[0]), ("waste station", waste_ring(0.02))]
    EN.drape("North_Forecourt", layers, cutters, col, soften=(50.0, 146.0, 114.0, 195.0))


def build_ring_on_road(col):
    """The dark ring where it crosses the race road: flat paint RING_LIFT over the road surface, no collider."""
    import env_street_objects as SO
    import env_track as T
    import street_plan as S
    g = SO.Proto()
    steps, built = 96, 0
    for k in range(steps):
        a0, a1 = 2 * math.pi * k / steps, 2 * math.pi * (k + 1) / steps
        quad = [(RING_C[0] + r * math.cos(a), RING_C[1] + r * math.sin(a)) for r, a in
                ((RING_R - RING_BAND, a0), (RING_R, a0), (RING_R, a1), (RING_R - RING_BAND, a1))]
        if max(S.route_distance(*p) for p in quad) > RING_ROAD_REACH:
            continue
        g.face([(*p, T.road_z(E.nearest_route_point(*p)) + RING_LIFT) for p in quad], "palette", (0, 0, 1), "setts_dark")
        built += 1
    if built:
        E.new_object("North_ForecourtRing_NoCollide", g.mesh("North_ForecourtRing_NoCollide"), col)
    print(f"  north: dark ring on the road: {built} of {steps} segments ({RING_LIFT * 100:.0f} cm over the road, up to "
          f"{RING_ROAD_REACH} m from the centreline; the rest of the ring is part of the sett sheet beyond 4.95 m)")


# =====================================================================================================
# Solid structures and flush paint
# =====================================================================================================
def build_structures(col):
    import env_street_objects as SO
    import env_streets as ST
    g = SO.Proto()
    # --- the stair on the forecourt's east edge: 3 risers, pink-buff. The real one goes DOWN 0.5 m to the lawn;
    # our terrain (DEM) rises to the east here, so it goes up to the lawn instead ---
    ring, c, u, e = stair_ring()
    gs = [ST.ground(*p) for p in ring]
    z0 = min(gs) - 0.3
    foot = ST.ground(*c) + 0.07                                   # the sett sheet's height at the edge
    for k in range(3):
        d0 = k * STAIR_TREAD
        d1 = 3 * STAIR_TREAD + 1.2 if k == 2 else d0 + STAIR_TREAD
        mid = (c[0] + e[0] * (d0 + d1) / 2, c[1] + e[1] * (d0 + d1) / 2)
        g.obox(mid, e, (d1 - d0) / 2, STAIR_W / 2, z0, foot + (k + 1) * STAIR_RISE, "palette", colour=PINK_BUFF)
    _clear("stair on the forecourt's east edge", ring)
    # --- waste station: low platform with a pale kerb, 8 stainless cylinders in two rows of four ---
    wr = waste_ring()
    top = max(ST.ground(*p) for p in wr) + 0.14
    g.obox(WASTE_C, WASTE_U, WASTE_HALF[0], WASTE_HALF[1], min(ST.ground(*p) for p in wr) - 0.3, top, "palette", colour="kerb")
    v = (-WASTE_U[1], WASTE_U[0])
    for a in (-2.85, -0.95, 0.95, 2.85):
        for b in (-0.85, 0.85):
            x, y = WASTE_C[0] + WASTE_U[0] * a + v[0] * b, WASTE_C[1] + WASTE_U[1] * a + v[1] * b
            g.cyl(0.35, 0.35, top, top + 1.1, 8, "steel_grey", x=x, y=y)
    _clear("waste station", wr)
    # --- pylon at the avenue: a blank dark slab with a blank board at its top (shape only), facing the avenue ---
    px, py = PYLON
    pz = ST.ground(px, py)
    g.obox(PYLON, (0.0, 1.0), 0.45, 0.14, pz - 0.2, pz + 5.0, "palette", colour="black")
    g.obox(PYLON, (0.0, 1.0), 1.0, 0.17, pz + 2.5, pz + 5.0, "palette", skip=(), colour="roof_dark")
    _clear("pylon at the avenue", [PYLON])
    # --- flag poles on the lawn behind it: plain, no flags ---
    for x, y in FLAGPOLES:
        z = ST.ground(x, y)
        g.cyl(0.055, 0.035, z - 0.2, z + 8.0, 6, "mast", x=x, y=y)
    _clear("flag poles", FLAGPOLES)
    # --- east park: the cream concrete seat ring (four arcs with gaps) ---
    for q in range(4):
        for j in range(4):
            a = math.radians(q * 90.0 + 14.0 + (j + 0.5) * 62.0 / 4)
            r = SEAT_RING_R - 0.35
            mid = (SEAT_RING_C[0] + r * math.cos(a), SEAT_RING_C[1] + r * math.sin(a))
            z = ST.ground(*mid)
            g.obox(mid, (-math.sin(a), math.cos(a)), r * math.radians(62.0 / 4) / 2 + 0.04, 0.35, z - 0.25, z + 0.45,
                   "palette", colour="cream")
    _clear("seat ring of the east park", _circle(SEAT_RING_C, SEAT_RING_R, 12))
    obj = E.new_object("North_Structures", g.mesh("North_Structures"), col)
    # --- flush paint without a collider: the sleeper path and the red-brown disc inside the seat ring ---
    f = SO.Proto()
    (ax, ay), (bx, by) = PARK_PATH
    length = math.hypot(bx - ax, by - ay)
    ux, uy = (bx - ax) / length, (by - ay) / length
    n = int(length // 0.75)
    for k in range(n + 1):
        cx, cy = ax + ux * k * 0.75, ay + uy * k * 0.75
        quad = [(cx + ux * a - uy * b, cy + uy * a + ux * b) for a, b in ((-0.125, -1.0), (0.125, -1.0), (0.125, 1.0), (-0.125, 1.0))]
        f.face([(*p, ST.ground(*p) + 0.06) for p in quad], "palette", (0, 0, 1), "timber")
    disc = _circle(SEAT_RING_C, SEAT_RING_R - 0.75, 16)
    zc = ST.ground(*SEAT_RING_C) + 0.07
    for p, q in zip(disc, disc[1:] + disc[:1]):
        f.face([(*SEAT_RING_C, zc), (*p, ST.ground(*p) + 0.07), (*q, ST.ground(*q) + 0.07)], "palette", (0, 0, 1), "brick_red")
    E.new_object("North_Park_NoCollide", f.mesh("North_Park_NoCollide"), col)
    print(f"  north: structures {sum(len(p.vertices) - 2 for p in obj.data.polygons)} triangles (stair, waste station, "
          f"pylon, {len(FLAGPOLES)} flag poles, seat ring); park paint: {n + 1} sleepers and the disc")


def build():
    """Call after env_entrance.build() (the terrain, the buildings and the street paving are built then)."""
    col = E.collection(COLLECTION)
    build_forecourt(col)
    if RING_ON_ROAD:
        build_ring_on_road(col)
    else:
        print("  north: the dark ring is built only beside the road (RING_ON_ROAD = False, see env_north.py)")
    build_structures(col)


# =====================================================================================================
# Dressing
# =====================================================================================================
def _posts():
    """Steel posts on the calcada along the bus lay-by: [(x, y)]."""
    import street_plan as S
    y0, y1 = S.AV_LAYBY
    a, b = S.offset(S.av_entrance_line(y0 + 1.0, y1 - 1.0), S.AV_SLIP[0] - POST_OFF)
    length = math.hypot(b[0] - a[0], b[1] - a[1])
    n = int(length // POST_STEP)
    return [(a[0] + (b[0] - a[0]) * k / n, a[1] + (b[1] - a[1]) * k / n) for k in range(n + 1)]


def dress(veg, props, protos):
    """Edits the dressing lists after env_entrance.dress and rewrites the two placement files."""
    import env_street_objects as SO
    import env_streets as ST
    import street_plan as S
    avenue = [lay["ring"] for lay in S.LAYERS if lay["name"].startswith("B2 ")]
    solids = [waste_ring(0.8), stair_ring()[0], _circle(SEAT_RING_C, SEAT_RING_R + 0.6, 16),
              S.ribbon(PARK_PATH, -1.6, 1.6), S.ribbon(EDGE, -BAND_PINK - BAND_OCHRE - 1.1, 0.0)]

    def gone(p):
        x, y = p["x"], p["y"]
        if p["proto"] in CAFE and P.inside(x, y, FORECOURT):
            return "cafe furniture on the forecourt"
        if any(P.inside(x, y, r) for r in solids):
            return "under a new solid or on the edge bands"
        if 99.0 <= y <= 149.5 and any(P.inside(x, y, r) for r in avenue):
            return "on the avenue's new paving"
        return None
    removed = {}
    veg2, props2 = [], []
    for items, out in ((veg, veg2), (props, props2)):
        for p in items:
            why = gone(p)
            if why:
                removed.setdefault(why, []).append(f"{p['proto']}@{p['x']:.0f},{p['y']:.0f}")
            else:
                out.append(p)
    added = []
    cars = [n for n, _c, _k in SO.CAR_KINDS if n in protos]
    u = (math.sin(math.radians(CAR_BEARING)), math.cos(math.radians(CAR_BEARING)))
    for k, (x, y) in enumerate(CARS):
        if not cars or S.route_distance(x, y) < CAR_CLEAR:
            continue
        _REPORT.append(("parked cars on the forecourt (centre)", S.route_distance(x, y)))
        added.append({"proto": cars[(k * 3 + 1) % len(cars)], "x": x, "y": y, "z": round(ST.ground(x, y) + 0.07, 3),
                      "rot_z": round(math.atan2(u[1], u[0]) + (0.06 if k % 2 else -0.04), 4), "scale": 1.0})
    n_cars = len(added)
    if "Prop_BollardSlim" in protos:
        for x, y in _posts():
            _clear("steel posts along the bus lay-by", [(x, y)])
            added.append({"proto": "Prop_BollardSlim", "x": round(x, 3), "y": round(y, 3), "z": round(SO.surface(x, y), 3),
                          "rot_z": 0.0, "scale": 1.05})
    props2 += added
    for fname, items in (("placements_vegetation.json", veg2), ("placements_props.json", props2)):
        path = os.path.join(E.OUT, fname)
        if not os.path.exists(path):
            continue
        data = json.load(open(path))
        data["placements"] = items
        counts = {}
        for it in items:
            counts[it["proto"]] = counts.get(it["proto"], 0) + 1
        data["counts"] = counts
        json.dump(data, open(path, "w"), indent=1)
    print(f"  north dressing: {n_cars} parked cars on the forecourt, {len(added) - n_cars} steel posts along the bus lay-by")
    for why, names in sorted(removed.items()):
        print(f"  north dressing: removed ({why}): {', '.join(sorted(names))}")
    low = {}
    for what, d in _REPORT:
        low[what] = min(d, low.get(what, 1e9))
    for what, d in sorted(low.items()):
        print(f"  north clearance: {what}: " + (f"{d:.2f} m" if d < 1e6 else "more than 60 m"))
    return veg2, props2
