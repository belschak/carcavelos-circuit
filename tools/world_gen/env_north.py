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
                             stainless cylinders on a low platform), the pylon at the avenue (a slim dark post with a
                             blank board, 5 m), four plain flag poles, the seat ring of the east park
  North_Park_NoCollide       the east park's timber-sleeper path and the red-brown disc inside the seat ring (flush)
Dressing (dress(), after env_entrance.dress, as an explicit edit of the lists): the cafe tables, chairs, parasols and
planters on the forecourt go (survey: there are none; cars park there); parked cars in a row parallel to the
colonnade where they keep 9 m from the centreline; steel posts along the bus lay-by; bike racks on the plank band
along B; saplings on Milestone's lawn; whatever stood on the new avenue paving, under the new solids or on the edge
bands goes. Everything else stays as frozen.
The avenue's own paving (asphalt, bus lay-by, calcada strip) is street_plan.avenue_entrance().
Nothing solid is closer than CLEAR (6.6 m) to the race centreline: the build stops otherwise.

V18j-n (2026-11-12, Luc drove it: "the ground rises and the kart gets stuck; in reality it is one flat level and a
flat street that simply continues west"; plan and sources: north_plan.py):
  level_nodes()              one call at the end of env_terrain.apply: the 5 m terrain nodes of the forecourt, the
                             street behind the hall, its lawn and the path north of it go to the road's bed
                             (14.45 m); the east lawn lies 0.5 m below the forecourt
  North_Forecourt            now ONE flat sheet at 14.564 m (4 mm over the road, from 4.02 m beside the centreline)
                             from the start plaza's corner to the east rim: the street's setts in the start plaza's
                             own UV frame, the buff concrete apron along the hall, steel drain gratings across the
                             street, the rust-brown steel strip where the setts meet the lawn-look road, the path
                             north of the lawn, the first metres of Rua da Holanda; a hem under the ground at its
                             free edges
  North_DoorApron_*          setts 3.2 cm under the road round the hall's open-air doors (D1, D5): Unity cuts the old
                             road away there and lays a narrower door road; what lay bare beside it is now paved
  North_Street               the louvre screen and the basement stair with its tube railing at the facade's east
                             end, two grey utility boxes on the lawn, concrete bench blocks, the bus stop post
  road_look()                env_track.ROAD_LOOK hook: the race road shows setts on the real street and the terrain's
                             own lawn where it runs over the real lawn

V20d (Luc's playtest 13 Nov 2026, s20: "odd black markings on the square", "on Google Maps the houses on the right side
are much further away"; plan and sources: north_plan.py, section V20d):
  the ring                   ONE row of dark basalt cubes, 0.14 m (was a flat dark band of 0.25 m): two half rings of
                             the textured band (mat "basalt", polar UVs ring_uv), continued across the race road by
                             env_trackside.plaza_paint with ring_paint_rows()
  build_house()              env_buildings.build_one hook (owns_house): the villas north of the dry field as two-storey
                             houses and terraced pairs with orange hip roofs, ochre or white render, windows with
                             white surrounds, a chimney (were generic 10.5 m flat-roofed blocks)
  North_Gardens              the tall hedge closing the gardens to the field, the hedges between the plots, the pools;
                             North_GardenHedgeLeaves_NoCollide: leaf cards over the hedges
  North_Street (more)        the low white seat wall of the path beside the field, the blank black sign slab
  dress (more)               Rua da Holanda: cars nose-in in the bays, one along the east kerb, the P sign, the stone
                             block bollard, slim bollards, two scooters, two street lamps; the umbrella pine at the
                             apartment block's corner, trees in the gardens
  build_far_housing()        (from env_backdrop) the apartment blocks along Rua da Holanda beyond the world's edge as
                             white 4-5 storey blocks (env_campus.build_housing), not plain boxes
"""
import json
import math
import os

import campus_plan as C
import entrance_plan as P
import env_common as E
import north_plan as NP

COLLECTION = "World_North"
CLEAR = 6.6                  # m from the race centreline: nothing solid of ours stands closer
CAR_CLEAR = 9.0              # m: parked cars (centre), as street_plan.CAR_CLEAR

# ----------------------------------------------------------------------------------------------------------------
# Plan (world metres, x east, y north). Sources: ortho_campus.jpg (3 px per metre), N02, N03 (2019/2020).
# ----------------------------------------------------------------------------------------------------------------
FACE_U = (-0.3654, 0.9309)   # along the hall's north-east face (76.7, 158.7) -> (66.6, 184.5): the sett rows
# V18g (Google satellite 21z): the edge runs (100.8, 156.8) - (92.5, 177.3), then the lawn's north edge turns
# east-north-east (bearing 69) towards (117, 187). Was (101.8, 153.5) - (94.5, 179.5).
EDGE = list(NP.EDGE)                            # east edge of the setts, south -> north (V18j: on to y 147.6)
# V18j: the sheet is north_plan.SHEET: street, path, forecourt, the band east of the waste station (was: from the
# hall's face to the east edge, north to the foot of a bank at y 193 that is not there)
FORECOURT = list(NP.SHEET)
BUFF = [(82.0, 153.0), (83.0, 163.5), (98.1, 163.5), EDGE[0], (93.0, 149.5)]    # in-situ concrete at Student Central
BUFF_TILE = 6.0                                 # m per tile of north_buff (make_entrance_textures.make_buff)
GRATING_TILE = 0.4                              # m per tile of north_grating along a drain's run (8 bars)
PINK_BUFF, OCHRE = "cream", "ochre"             # street palette cells (survey #e5d0b5, #ebd0a9)
BAND_PINK, BAND_OCHRE = 1.3, 1.0                                # m: widths of the two edge bands east of the setts
RING_C, RING_R = (80.9, 176.3), 4.62   # the dark ring's centre and outer radius. V20d: fitted on the DGT orthophoto (the
                                       # frame the generator and track.json share): band centre line r 4.55 round (80.9,
                                       # 176.3). V18g had (80.3, 176.1), r 4.45 from the Google 21z tile, which sits about
                                       # 0.6 m west of the orthophoto here; the survey V15 had (80.7, 177), 9.3 m across
# V20d (Luc's playtest 13 Nov 2026, s20: "odd black markings on the square"): the ring was a flat dark palette band of
# 0.25 m that read as a thick black line. Real (Street View N02, Jun 2019, from inside the ring; Google 21z: a line
# of 1-2 px): ONE row of dark basalt cubes, 10-12 cm, in the pale setts' sand joints. The band is now 0.14 m of the
# textured basalt band (make_entrance_textures.make_basalt, mat "basalt"), its stones laid along the arc (polar UVs,
# RING_TILES tiles round the circle so the texture closes on itself); env_trackside.plaza_paint continues it across
# the race road with ring_paint_rows().
RING_BAND = 0.14
RING_TILES = 19              # tiles of north_basalt (1.5 m) round the ring: 2 pi x 4.55 m = 28.6 m
# The ring crosses the race road (the centreline passes 1 m from its centre). Painting it on the road needs one line
# in qa_geometry.py: its 'covered' check allows only ALLOWED_ON_ROAD = {Track_Kerbs, Track_EdgeLines} over the road
# and reports this paint as two new 'covered' runs (s 1180, 1186). So it is OFF until the lead / Luc decide:
# set RING_ON_ROAD = True and add "North_ForecourtRing_NoCollide" to that set.
RING_ON_ROAD = False
RING_ROAD_REACH = 3.5        # m from the centreline: the ring's paint on the road keeps inside the edge lines and kerbs
RING_LIFT = 0.05             # m over the road (the edge lines lie at 4 cm; 2 cm flickered there)
MANHOLES = [(89.5, 178.2), (91.1, 176.8), (96.5, 163.2)]        # round rust-brown covers, 0.7 m (V18g: satellite 21z)
GRATES = [(86.5, 179.7), (89.7, 161.9)]                         # square dark gratings, 1.0 m
STAIR_Y, STAIR_W, STAIR_TREAD = 174.0, 4.0, 0.4   # 3 risers at (95, 174), DOWN to the east lawn (V18j: 0.5 m in all)
RIM_WALL = 0.3               # m: the pale kerb wall under the forecourt's east rim and along the lawn's north edge
RIM_WALL_END = 121.0         # x: the wall ends where the lawn has come up to the paved band
# V18g (satellite 21z): 12 units in two rows of six (bearing 65), each a square concrete lid with a steel post
WASTE_C, WASTE_U, WASTE_HALF = (99.4, 183.3), (0.9063, 0.4226), (5.0, 1.75)
WASTE_ROWS = ((-4.15, -2.49, -0.83, 0.83, 2.49, 4.15), (-0.85, 0.85))
CARS = [(89.2, 164.6), (89.6, 167.3), (89.9, 170.0), (90.1, 172.7)]           # centres; nose towards the colonnade
CAR_BEARING = 255.0
CAFE = ("Prop_BistroTable", "Prop_BistroChair", "Prop_Parasol", "Prop_TroughBloom", "Prop_PlanterRound")
# avenue entrance side
POST_STEP, POST_OFF = 2.75, 0.6            # steel posts on the calcada, 0.6 m east of the bus lay-by
PYLON = (-86.0, 102.9)                     # survey: about (-86, 102), on the calcada
FLAGPOLES = [(-84.5, 99.5), (-82.0, 99.1), (-79.5, 98.7), (-77.0, 98.3)]      # behind it on the lawn, 8 m, no flags
# start plaza and Milestone's lawn (N10, N06, N14): bike racks on the planks along B, a few saplings on the lawn
BIKE_RACK_T, BIKE_RACK_S = (47.0, 51.5, 53.5, 55.5, 60.0), 6.8      # V18g: east of B's planted strip (satellite: t 51-55, s 7)
SAPLINGS = list(P.J_SAPLINGS)                                       # V18g: as on the satellite view
# east park (low priority, 60-80 m from the race line)
PARK_PATH = [(117.0, 131.0), (153.0, 145.1)]     # timber sleepers 2.0 x 0.25 m laid across, every 0.75 m
SEAT_RING_C, SEAT_RING_R = (157.7, 147.0), 5.0   # cream concrete ring bench, outer diameter 10 m, four gaps

_REPORT = []                 # (what, least distance to the centreline)


# V18 (team walk 7 Nov 2026): the lawn strip north of the loop behind A carries a row of young staked trees and two
# grey galvanised boxes. V18j: the real row stands 2 m behind the street's edge, under our race road; the trees stand
# in the free corner of the lawn north of the road (north_plan.TREES), the boxes are built there (build_street; the
# two cabinet props that stood north of the path are gone)
NORTH_LOOP_TREES = list(NP.TREES)
NORTH_LOOP_CABINETS = []
# V18g (satellite 21z): the path north of the loop's lawn (y 189.6-194.3) has a low pale wall on its north side with
# woodland behind it. Our road lies on that path east of x = 47, so the wall is built only west of it (V18j: 1.05 m
# high, survey 1.3: 1.0-1.1 m)
NORTH_WALL = list(NP.NORTH_WALL)
NORTH_WALL_SIZE = NP.NORTH_WALL_SIZE            # thickness, height
NORTH_WOOD = [(41.6, 197.6, 1.25), (22.0, 200.5, 1.1), (31.0, 201.5, 1.3)]     # trees behind the wall: x, y, scale


def _clear(what, pts, limit=CLEAR):
    import street_plan as S
    d = min(S.route_distance(x, y) for x, y in pts)
    _REPORT.append((what, d))
    if d < limit:
        raise RuntimeError(f"north: '{what}' stands {d:.2f} m from the race centreline (limit {limit} m)")
    return d


def _circle(c, r, n):
    return [(c[0] + r * math.cos(2 * math.pi * k / n), c[1] + r * math.sin(2 * math.pi * k / n)) for k in range(n)]


def ring_arc(a0, a1, step=3.0):
    """V20d: the basalt band from angle a0 to a1 (degrees, counter-clockwise from east), as one polygon."""
    m = max(1, int(math.ceil((a1 - a0) / step)))
    r0, r1 = RING_R - RING_BAND, RING_R
    angles = [math.radians(a0 + (a1 - a0) * k / m) for k in range(m + 1)]
    inner = [(RING_C[0] + r0 * math.cos(a), RING_C[1] + r0 * math.sin(a)) for a in angles]
    outer = [(RING_C[0] + r1 * math.cos(a), RING_C[1] + r1 * math.sin(a)) for a in angles]
    return inner + outer[::-1]


def ring_uv(a0):
    """V20d: polar UVs of the basalt band for the half ring that starts at angle a0 (degrees): u along the ring
    (RING_TILES tiles round it, from east counter-clockwise; angles are taken from a0 on, so the half ring from 180
    to 360 degrees ends on u = RING_TILES, not 0), v across it (0 at the inner edge, 1 at the outer)."""
    lo = math.radians(a0) - 1e-6

    def uv(x, y):
        dx, dy = x - RING_C[0], y - RING_C[1]
        a = math.atan2(dy, dx)
        while a < lo:
            a += 2.0 * math.pi
        return (a / (2.0 * math.pi) * RING_TILES, (math.hypot(dx, dy) - (RING_R - RING_BAND)) / RING_BAND)
    return uv


def ring_paint_rows(step=2.0):
    """V20d (for env_trackside.plaza_paint, which continues the ring across the race road): rows [(inner point,
    outer point)] every `step` degrees round the whole ring, their uvs [(uv inner, uv outer)] and the material."""
    import env_entrance as EN
    r0, r1 = RING_R - RING_BAND, RING_R
    rows, uvs = [], []
    for k in range(int(round(360.0 / step)) + 1):
        a = math.radians(k * step)
        rows.append(((RING_C[0] + r0 * math.cos(a), RING_C[1] + r0 * math.sin(a)),
                     (RING_C[0] + r1 * math.cos(a), RING_C[1] + r1 * math.sin(a))))
        u = k * step / 360.0 * RING_TILES
        uvs.append(((u, 0.0), (u, 1.0)))
    return rows, uvs, EN.materials()["basalt"]


def _edge_frame():
    """(unit vector along the east edge northwards, unit vector east across it)."""
    (ax, ay), (bx, by) = EDGE
    d = math.hypot(bx - ax, by - ay)
    u = ((bx - ax) / d, (by - ay) / d)
    return u, (u[1], -u[0])


def stair_ring():
    """Footprint of the stair down to the east lawn: STAIR_W along the bands' outer rim at y = STAIR_Y, two treads
    and a landing east of it (V18j: it started at the sett edge and went up)."""
    u, e = _edge_frame()
    (ax, ay) = NP.RIM[0]
    t = (STAIR_Y - ay) / u[1]
    c = (ax + u[0] * t, ay + u[1] * t)
    depth = 2 * STAIR_TREAD + 1.2
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
_BEFORE = {}                 # the height field before level_nodes (dress: how far the ground moved under an item)


def level_nodes(heights):
    """V18j (one call at the end of env_terrain.apply): the north side on one level. The 5 m terrain nodes of
    north_plan.ZONE go to the road's bed (north_plan.GROUND), returning to the elevation model over BLEND metres
    outside it; the east lawn (north_plan.EAST) is lowered to 0.5 m below the forecourt (never raised: south of it
    the ground falls away anyway). Nothing changes west of x = -14 (the start plaza has its own level) and in the
    pool. Before: the forecourt climbed 0.6-1.7 m to the north-east and dipped 0.3-0.6 m at Student Central."""
    _BEFORE["h"] = [row[:] for row in heights]
    n, up, down = 0, 0.0, 0.0
    for j in range(E.ROWS):
        for i in range(E.COLS):
            x, y = E.X0 + i * E.STEP, E.Y0 + j * E.STEP
            z, w = NP.level_target(x, y)
            if w <= 0.0:
                continue
            h = heights[j][i]
            if z < NP.GROUND - 1e-6:
                z = min(z, h)                               # the east lawn: lowered only
            new = h * (1.0 - w) + z * w
            if abs(new - h) > 0.005:
                heights[j][i] = new
                n += 1
                up, down = max(up, new - h), max(down, h - new)
    _BEFORE["after"] = [row[:] for row in heights]
    print(f"  north (V18j): {n} terrain nodes on one level ({NP.GROUND} m under the street and the forecourt, the east "
          f"lawn {NP.LAWN_DROP} m below the paving): raised by up to {up:.2f} m, lowered by up to {down:.2f} m")


def ground_shift(x, y):
    """Metres the terrain moved at (x, y) in level_nodes (0 where it did not)."""
    import env_terrain
    if "h" not in _BEFORE:
        return 0.0
    return env_terrain._bilinear(_BEFORE["after"], x, y) - env_terrain._bilinear(_BEFORE["h"], x, y)


def on_sheet(x, y):
    """True on the flat paving: the sheet's outline and the edge bands, without the hall (the road counts: its bed is
    level)."""
    if "hall" not in _BEFORE:
        _BEFORE["hall"] = next([tuple(p) for p in b["points"]] for b in E.DATA["buildings"] if b["id"] == C.MAIN_ID)
    return (NP.inside(x, y, NP.SHEET) or any(NP.inside(x, y, r) for r in _band_rings())) and not NP.inside(x, y, _BEFORE["hall"])


def _band_rings():
    import street_plan as S
    return [S.ribbon(EDGE, -BAND_PINK - BAND_OCHRE, 0.0)]


def _world_uv(x, y):
    """The terrain's own UVs (build_world.build_ground): the ground colour map covers the world once."""
    return ((x - E.X0) / (E.X1 - E.X0), (y - E.Y0) / (E.Y1 - E.Y0))


def _rect(c, u, hu, hv):
    v = (-u[1], u[0])
    return [(c[0] + u[0] * a + v[0] * b, c[1] + u[1] * a + v[1] * b) for a, b in ((-hu, -hv), (hu, -hv), (hu, hv), (-hu, hv))]


def well_ring(pad=0.0):
    """The basement stair's well at the facade (north_plan.STAIR_A, STAIR_O), with `pad` metres round it."""
    (a0, a1), (o0, o1) = NP.STAIR_A, NP.STAIR_O
    return [NP.pt(a0 - pad, o0 - pad), NP.pt(a1 + pad, o0 - pad), NP.pt(a1 + pad, o1 + pad), NP.pt(a0 - pad, o1 + pad)]


def _cut_rings():
    """What the north sheets leave free: the hall (8 cm inside its outline: the sheet ends under the hall's walls),
    Student Central's own outline, the other buildings, the start plaza's sheet, env_campus' keep-out rings (pool)."""
    import env_campus
    hall = next([tuple(p) for p in b["points"]] for b in E.DATA["buildings"] if b["id"] == C.MAIN_ID)
    rings = [("hall", env_campus.grow(env_campus.ccw(hall), -0.08)),
             ("Student Central", env_campus.grow(env_campus.ccw(list(C.SC_RING)), 0.1)),
             ("start plaza", list(P.RING))]
    for b in E.DATA["buildings"]:
        pts = [tuple(p) for p in b["points"]]
        if b["id"] != C.MAIN_ID and b.get("name") != "Student Central" and max(p[0] for p in pts) > -20.0 \
                and min(p[0] for p in pts) < 130.0 and max(p[1] for p in pts) > 140.0 and min(p[1] for p in pts) < 205.0:
            rings.append(("building " + b["id"], pts))
    for r in env_campus.KEEP_OUT:
        if max(p[0] for p in r) > -20.0 and min(p[0] for p in r) < 130.0 and max(p[1] for p in r) > 140.0 and min(p[1] for p in r) < 205.0:
            rings.append(("campus keep-out", list(r)))
    return rings


def lawn_look_line():
    """The race line's points beside which the road reads as lawn (plus one point either side)."""
    pts = E.ROUTE["points"]
    idx = [i for i in range(NP.LOOK_I[0], NP.LOOK_I[1] + 1)
           if NP.road_on_lawn((pts[i]["x"] + pts[i + 1]["x"]) / 2, (pts[i]["y"] + pts[i + 1]["y"]) / 2)]
    return [(pts[i]["x"], pts[i]["y"]) for i in range(idx[0], idx[-1] + 2)] if idx else []


# =====================================================================================================
# The sheet: forecourt, street, path (V18j: one flat level)
# =====================================================================================================
def build_forecourt(col):
    import env_entrance as EN
    import street_plan as S
    t = P.CALCADA_TILE
    uv = ((0.0, 0.0), P.ROW_DIR, t, t)          # the start plaza's frame: one sett field from the plaza to the forecourt
    buff_uv = (NP.NW, NP.U, BUFF_TILE, BUFF_TILE)
    face_a, face_b = (76.70, 158.71), (66.58, 184.49)               # the hall's north-east face (its outline)
    fu = FACE_U
    fe = (fu[1], -fu[0])                                            # outwards (east) across that face
    layers = [{"name": "north setts", "ring": list(NP.SHEET), "mat": "calcada", "uv": uv},
              {"name": "apron along the hall's north face", "mat": "buff", "uv": buff_uv,
               "ring": [NP.pt(0.0, -0.3), NP.pt(NP.FACE_LEN, -0.3), NP.pt(NP.FACE_LEN, NP.APRON), NP.pt(0.0, NP.APRON)]},
              {"name": "drip-line strip at the north-east face", "mat": "buff", "uv": (face_a, fu, BUFF_TILE, BUFF_TILE),
               "ring": [(face_a[0] - fe[0] * 0.3, face_a[1] - fe[1] * 0.3), (face_a[0] + fe[0] * 0.5, face_a[1] + fe[1] * 0.5),
                        (face_b[0] + fe[0] * 0.5, face_b[1] + fe[1] * 0.5), (face_b[0] - fe[0] * 0.3, face_b[1] - fe[1] * 0.3)]},
              {"name": "buff concrete at Student Central", "ring": list(BUFF), "mat": "buff", "uv": buff_uv},
              {"name": "pink-buff edge band", "ring": S.ribbon(EDGE, -BAND_PINK, 0.0), "mat": "pal:" + PINK_BUFF, "uv": None},
              {"name": "ochre edge band", "ring": S.ribbon(EDGE, -BAND_PINK - BAND_OCHRE, -BAND_PINK), "mat": "pal:" + OCHRE,
               "uv": None},
              # V20d: the ring of basalt cubes as two half rings (each with continuous polar UVs)
              {"name": "basalt ring, north half", "ring": ring_arc(0.0, 180.0), "mat": "basalt", "uv": None, "uvf": ring_uv(0.0)},
              {"name": "basalt ring, south half", "ring": ring_arc(180.0, 360.0), "mat": "basalt", "uv": None, "uvf": ring_uv(180.0)},
              # Rua da Holanda's first metres: asphalt between two pale kerb lines
              {"name": "R. da Holanda", "ring": list(NP.HOLANDA), "mat": "asphalt", "uv": ((0.0, 0.0), (0.0, 1.0), 2.0, 2.0)}]
    (hx0, hy0), (hx1, _), (_, hy1) = NP.HOLANDA[0], NP.HOLANDA[1], NP.HOLANDA[2]
    for ring in ([(hx0 - 0.15, hy0 - 0.15), (hx1 + 0.15, hy0 - 0.15), (hx1 + 0.15, hy0), (hx0 - 0.15, hy0)],
                 [(hx0 - 0.15, hy0), (hx0, hy0), (hx0, hy1), (hx0 - 0.15, hy1)],
                 [(hx1, hy0), (hx1 + 0.15, hy0), (hx1 + 0.15, hy1), (hx1, hy1)]):
        layers.append({"name": "kerb line at R. da Holanda", "ring": ring, "mat": "pal:kerb", "uv": None})
    # the steel grating strip in the apron and the drains across the setts (bars across the run)
    for a0, a1 in NP.APRON_GRATE:
        layers.append({"name": "grating strip in the apron", "mat": "grating", "uv": (NP.pt(a0, 0.55), NP.U, GRATING_TILE, 0.5),
                       "ring": [NP.pt(a0, 0.55), NP.pt(a1, 0.55), NP.pt(a1, 1.05), NP.pt(a0, 1.05)]})
    for a, o, length, width in NP.DRAINS:
        rows = max(1, int(round(width / 0.45)))
        layers.append({"name": "drain across the street", "mat": "grating", "ring": _rect(NP.pt(a, o), NP.N, length / 2, width / 2),
                       "uv": (NP.pt(a + width / 2, o - length / 2), NP.N, GRATING_TILE, width / rows)})
    for c in MANHOLES + list(NP.MANHOLES):
        layers.append({"name": "manhole cover", "ring": _circle(c, 0.35, 10), "mat": "pal:iron", "uv": None})
    for cx, cy in GRATES + list(NP.PATH_COVERS):
        layers.append({"name": "grating", "ring": [(cx - 0.5, cy - 0.5), (cx + 0.5, cy - 0.5), (cx + 0.5, cy + 0.5),
                                                   (cx - 0.5, cy + 0.5)], "mat": "grating", "uv": ((cx - 0.5, cy - 0.5), (1.0, 0.0), 0.5, 0.5)})
    # the rust-brown steel strip between the setts and the lawn: along the south edge of the road where the road
    # reads as lawn (the real strip, 2-4 m further north, lies under the race road)
    strip = lawn_look_line()
    if strip:
        layers.append({"name": "steel strip at the lawn", "ring": S.ribbon(strip, NP.KEEP_OUT - 0.01, NP.KEEP_OUT + NP.STRIP),
                       "mat": "rust", "uv": (strip[0], NP.U, 1.5, 1.5)})
    for line in NP.HEM_LINES:                   # the hem: the terrain's own material, going down under the ground
        layers.append({"name": "hem", "ring": NP.hem_ring(line), "mat": "ground_map", "uv": None, "uvf": _world_uv})
    cutters = _cut_rings() + [("basement stair", well_ring())]
    S.KEEP_OUT_STD = S.KEEP_OUT
    EN.drape("North_Forecourt", layers, cutters, col, keep_out=NP.KEEP_OUT, level=lambda x, y, d, z: NP.LEVEL, sink=NP.hem_drop)
    print(f"  north (V18j): the sheet lies flat at {NP.LEVEL} m from {NP.KEEP_OUT} m beside the centreline; "
          f"{len(NP.DRAINS)} drains across the street, the steel strip along {max(len(strip) - 1, 0) * 2} m of the road's "
          f"south edge, {len(NP.HEM_LINES)} hems")


def build_door_aprons(col):
    """V18j: setts under the road round the hall's open-air doors. In Unity NovaHallInsert cuts the old road away
    in front of every hall door (5.5 m either side of the old line) and lays a straight, narrower door road along the
    door's axis; between that road and our paving the terrain lay bare, 13 cm lower (Luc's dark strip at D5).
    The apron is the hull of that cut and of the old road's last metres, north_plan.UNDER_ROAD under the road: in
    the generator's world it is hidden under the road, in Unity it shows wherever nothing else is laid."""
    import env_entrance as EN
    import street_plan as S
    t = P.CALCADA_TILE
    S.KEEP_OUT_STD = S.KEEP_OUT
    cutters = [c for c in _cut_rings() if c[0] != "start plaza"]
    aprons = NP.door_aprons(E.ROUTE["points"])
    for name, ring in aprons:
        layers = [{"name": "door apron " + name, "ring": ring, "mat": "calcada", "uv": ((0.0, 0.0), P.ROW_DIR, t, t)}]
        EN.drape("North_DoorApron_" + name.split("_")[0], layers, cutters, col, keep_out=0.001,
                 level=lambda x, y, d, z: NP.ROAD_Z - NP.UNDER_ROAD)
    if not aprons:
        print("  north (V18j): track.json / HallDoors.json not found: no door aprons (the strips beside the door roads stay bare)")


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
    import street_plan as S
    g = SO.Proto()
    # --- the stair on the forecourt's east rim: 3 risers DOWN to the lawn, 0.5 m in all, pink-buff (V18j: the
    # terrain is level now and the east lawn lies 0.5 m lower, as in reality; it used to go up) ---
    ring, c, u, e = stair_ring()
    z0 = NP.EAST_LAWN - 0.45
    rise = NP.LAWN_DROP / 3
    for k in range(3):
        d0 = k * STAIR_TREAD
        d1 = 2 * STAIR_TREAD + 1.2 if k == 2 else d0 + STAIR_TREAD
        mid = (c[0] + e[0] * (d0 + d1) / 2, c[1] + e[1] * (d0 + d1) / 2)
        top = NP.EAST_LAWN + 0.02 if k == 2 else NP.LEVEL - (k + 1) * rise          # the third riser lands on a slab
        g.obox(mid, e, (d1 - d0) / 2, STAIR_W / 2, z0, top, "palette", colour=PINK_BUFF)
    _clear("stair on the forecourt's east edge", ring)
    # --- V18j: the pale kerb wall under the forecourt's east rim and along the lawn's north edge: the sheet ends on
    # it, the lawn lies 0.5 m below (the 5 m terrain cannot make that step) ---
    walls = []
    (r0, r1) = NP.RIM
    ts = ((c[0] - r0[0]) * u[0] + (c[1] - r0[1]) * u[1])
    length = math.hypot(r1[0] - r0[0], r1[1] - r0[1])
    for t0, t1 in ((-0.3, ts - STAIR_W / 2), (ts + STAIR_W / 2, length)):
        walls.append(((r0[0] + u[0] * t0, r0[1] + u[1] * t0), (r0[0] + u[0] * t1, r0[1] + u[1] * t1), e))
    line = [r1] + list(NP.LAWN_N[1:])
    for pa, pb in zip(line, line[1:]):
        if pb[0] > RIM_WALL_END:
            f = (RIM_WALL_END - pa[0]) / (pb[0] - pa[0])
            pb = (pa[0] + (pb[0] - pa[0]) * f, pa[1] + (pb[1] - pa[1]) * f)
        d = math.hypot(pb[0] - pa[0], pb[1] - pa[1])
        if d > 0.1:
            walls.append((pa, pb, ((pb[1] - pa[1]) / d, -(pb[0] - pa[0]) / d)))          # the lawn lies to the right
    for pa, pb in zip(NP.SOUTH_EDGE, NP.SOUTH_EDGE[1:]):          # ... and round the south end of the buff concrete
        d = math.hypot(pb[0] - pa[0], pb[1] - pa[1])
        out = ((pb[1] - pa[1]) / d, -(pb[0] - pa[0]) / d)
        if out[0] * (95.0 - pa[0]) + out[1] * (160.0 - pa[1]) > 0:              # away from the forecourt
            out = (-out[0], -out[1])
        walls.append((pa, pb, out))
    for pa, pb, out in walls:
        d = math.hypot(pb[0] - pa[0], pb[1] - pa[1])
        wu = ((pb[0] - pa[0]) / d, (pb[1] - pa[1]) / d)
        mid = ((pa[0] + pb[0]) / 2 + out[0] * (RIM_WALL / 2 - 0.02), (pa[1] + pb[1]) / 2 + out[1] * (RIM_WALL / 2 - 0.02))
        g.obox(mid, wu, d / 2 + 0.01, RIM_WALL / 2, NP.EAST_LAWN - 0.5, NP.LEVEL - 0.006, "palette", colour="kerb")
    _clear("kerb wall under the forecourt's east rim", [p for w in walls for p in w[:2]])
    # --- waste station (V18g): 12 square concrete lids in two rows of six, a stainless post on each ---
    wr = waste_ring()
    v = (-WASTE_U[1], WASTE_U[0])
    for a in WASTE_ROWS[0]:
        for b in WASTE_ROWS[1]:
            x, y = WASTE_C[0] + WASTE_U[0] * a + v[0] * b, WASTE_C[1] + WASTE_U[1] * a + v[1] * b
            top = NP.LEVEL + 0.16                          # V18j: on the flat sheet (the terrain lay 1-1.4 m higher)
            g.obox((x, y), WASTE_U, 0.7, 0.7, NP.LEVEL - 0.06, top, "palette", colour="kerb")
            g.cyl(0.3, 0.3, top, top + 1.0, 8, "steel_grey", x=x, y=y)
    _clear("waste station", wr)
    # --- pylon at the avenue (ST02 h090-h180): a slim dark post with a blank dark board of 2 x 2.5 m at its top
    # (shape only: no screen content), facing the avenue ---
    px, py = PYLON
    pz = ST.ground(px, py)
    g.obox(PYLON, (0.0, 1.0), 0.13, 0.13, pz - 0.2, pz + 2.5, "palette", colour="black")
    g.obox(PYLON, (0.0, 1.0), 1.0, 0.16, pz + 2.5, pz + 5.0, "palette", skip=(), colour="roof_dark")
    _clear("pylon at the avenue", [PYLON])
    # --- V18g (Street View May 2026): the road sign at the driveway: two blank boards on two grey posts ---
    sb = P.SIGN_BOARD
    a = math.radians(sb["bearing"])
    along = (math.cos(a), -math.sin(a))
    sz = ST.ground(*sb["at"])
    for side in (-0.8, 0.8):
        g.cyl(0.05, 0.05, sz - 0.2, sz + sb["top"], 6, "steel_grey", x=sb["at"][0] + along[0] * side, y=sb["at"][1] + along[1] * side)
    for z0, z1 in ((sb["top"] - 2.05, sb["top"] - 1.1), (sb["top"] - 1.0, sb["top"] - 0.05)):
        g.obox(sb["at"], along, sb["width"] / 2, 0.035, sz + z0, sz + z1, "palette", skip=(), colour="grey_back")
    _clear("sign boards at the driveway", [sb["at"]])
    # --- V18g: the low pale wall north of the path behind the north loop ---
    (wx0, wy0), (wx1, wy1) = NORTH_WALL
    n = max(1, int(math.hypot(wx1 - wx0, wy1 - wy0) // 4.0))
    wu = ((wx1 - wx0) / math.hypot(wx1 - wx0, wy1 - wy0), (wy1 - wy0) / math.hypot(wx1 - wx0, wy1 - wy0))
    for k in range(n):
        pa = (wx0 + (wx1 - wx0) * k / n, wy0 + (wy1 - wy0) * k / n)
        pb = (wx0 + (wx1 - wx0) * (k + 1) / n, wy0 + (wy1 - wy0) * (k + 1) / n)
        mid = ((pa[0] + pb[0]) / 2, (pa[1] + pb[1]) / 2)
        zs = [ST.ground(*pa), ST.ground(*pb)]
        g.obox(mid, wu, math.hypot(pb[0] - pa[0], pb[1] - pa[1]) / 2 + 0.01, NORTH_WALL_SIZE[0] / 2, min(zs) - 0.3,
               max(zs) + NORTH_WALL_SIZE[1], "palette", colour="render_white")
    _clear("low wall north of the loop", list(NORTH_WALL))
    # --- V18g: the pale concrete edging of Milestone's raised lawn (0.5 m wide, a low kerb of 0.15 m), in pieces of
    # about 3 m; pieces closer than CLEAR to the race line are left out (the line cuts the lawn's south-east corner) ---
    lawn = list(P.J_LAWN)
    n_edge = 0
    for pa, pb in zip(lawn, lawn[1:]):
        d = math.hypot(pb[0] - pa[0], pb[1] - pa[1])
        u = ((pb[0] - pa[0]) / d, (pb[1] - pa[1]) / d)
        out = (u[1], -u[0])                                           # the lawn lies to the left of pa -> pb
        m = max(1, int(round(d / 3.0)))
        for k in range(m):
            a0, a1 = d * k / m - (0.25 if k == 0 else 0.0), d * (k + 1) / m + (0.25 if k == m - 1 else 0.0)
            c = (pa[0] + u[0] * (a0 + a1) / 2 + out[0] * P.J_LAWN_EDGING / 2, pa[1] + u[1] * (a0 + a1) / 2 + out[1] * P.J_LAWN_EDGING / 2)
            ends = [(pa[0] + u[0] * a, pa[1] + u[1] * a) for a in (a0, a1)]
            if min(S.route_distance(*q) for q in ends + [c]) < CLEAR + 0.4:
                continue
            zs = [ST.ground(*q) for q in ends]
            g.obox(c, u, (a1 - a0) / 2, P.J_LAWN_EDGING / 2, min(zs) - 0.25, max(zs) + 0.15, "palette", colour="kerb")
            _REPORT.append(("edging of Milestone's lawn", min(S.route_distance(*q) for q in ends)))
            n_edge += 1
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


# =====================================================================================================
# V18j: what stands along the street behind the hall and on the forecourt
# =====================================================================================================
def build_street(col):
    """North_Street (solid): the grey metal louvre screen on the hall's north face and the basement stair beside it
    (a well inside a white upstand with a galvanised tube railing; the survey saw no more than that: the well is a
    dark void with three steps at its top), the two grey galvanised boxes on the lawn, the grey concrete bench
    blocks on the forecourt, the bus stop's post with a blank plate."""
    import env_entrance as EN
    import env_streets as ST
    m = EN.Faces()

    def pal(pts, colour, towards):
        m.face(pts, "palette", towards, [ST.pal_uv(colour)] * len(pts))

    def pbox(c, u, hu, hv, z0, z1, colour, top=True):
        ring = _rect(c, u, hu, hv)
        for a, b in zip(ring, ring[1:] + ring[:1]):
            pal([(*a, z0), (*b, z0), (*b, z1), (*a, z1)], colour, (b[1] - a[1], a[0] - b[0], 0.0))
        if top:
            pal([(*p, z1) for p in ring], colour, (0, 0, 1))
        return ring

    def bar(a, b, half, colour):
        """Square bar between two 3D points."""
        from mathutils import Vector
        a, b = Vector(a), Vector(b)
        d = (b - a).normalized()
        side = d.cross(Vector((0, 0, 1)))
        side = side.normalized() if side.length > 1e-6 else Vector((1, 0, 0))
        up = side.cross(d).normalized()
        for p, q in ((side, up), (up, -side), (-side, -up), (-up, side)):
            pal([a + (p - q) * half, b + (p - q) * half, b + (p + q) * half, a + (p + q) * half], colour, p)

    z = NP.LEVEL
    # --- the louvre screen: horizontal galvanised blades, 12 cm proud of the wall ---
    a0, a1 = NP.LOUVRE_A
    front = [NP.pt(a0, 0.14), NP.pt(a1, 0.14)]
    m.face([(*front[0], z + 0.1), (*front[1], z + 0.1), (*front[1], z + NP.LOUVRE_H), (*front[0], z + NP.LOUVRE_H)], "grating",
           (NP.N[0], NP.N[1], 0.0), [(0.1, 0.0), (0.1, (a1 - a0) / 2.0), (NP.LOUVRE_H, (a1 - a0) / 2.0), (NP.LOUVRE_H, 0.0)])
    for a in (a0, a1):                                       # its two ends, the top and the plinth under it
        pal([(*NP.pt(a, -0.05), z), (*NP.pt(a, 0.14), z), (*NP.pt(a, 0.14), z + NP.LOUVRE_H), (*NP.pt(a, -0.05), z + NP.LOUVRE_H)],
            "steel_grey", (NP.U[0] * (1 if a == a1 else -1), NP.U[1] * (1 if a == a1 else -1), 0.0))
    pal([(*NP.pt(a0, -0.05), z + NP.LOUVRE_H), (*NP.pt(a1, -0.05), z + NP.LOUVRE_H), (*front[1], z + NP.LOUVRE_H),
         (*front[0], z + NP.LOUVRE_H)], "steel_grey", (0, 0, 1))
    pal([(*front[0], z - 0.05), (*front[1], z - 0.05), (*front[1], z + 0.1), (*front[0], z + 0.1)], "grey_back", (NP.N[0], NP.N[1], 0.0))
    _clear("louvre screen on the hall's north face", front)
    # --- the basement stair: the well (a dark void under three steps at its east end), a white upstand on its
    # north side and its west end, the tube railing on the upstand ---
    (s0, s1), (o0, o1) = NP.STAIR_A, NP.STAIR_O
    ring = well_ring()
    floor = NP.GROUND + 0.035
    m.face([(*p, floor) for p in ring], "void", (0, 0, 1))
    for a, b in zip(ring, ring[1:] + ring[:1]):               # the well's walls, seen from inside
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        cx, cy = NP.pt((s0 + s1) / 2, (o0 + o1) / 2)
        m.face([(*a, floor), (*b, floor), (*b, z), (*a, z)], "white", (cx - mid[0], cy - mid[1], 0.0))
    for k in range(3):                                        # the top three steps, each 3 cm lower (the rest is dark)
        c = NP.pt(s1 - 0.15 - 0.3 * k, (o0 + o1) / 2)
        pbox(c, NP.U, 0.15, (o1 - o0) / 2 - 0.01, floor, z - 0.03 * (k + 1), "concrete")
    w = NP.WELL_WALL
    up = [(NP.pt((s0 + s1) / 2 - w / 2, o1 + w / 2), NP.U, (s1 - s0) / 2 + w / 2, w / 2),          # north side
          (NP.pt(s0 - w / 2, (o0 + o1) / 2), NP.N, (o1 - o0) / 2, w / 2)]                         # west end
    for c, u, hu, hv in up:
        for a, b in zip(*(lambda r: (r, r[1:] + r[:1]))(_rect(c, u, hu, hv))):
            m.face([(*a, z - 0.05), (*b, z - 0.05), (*b, z + NP.WELL_H), (*a, z + NP.WELL_H)], "white", (b[1] - a[1], a[0] - b[0], 0.0))
        m.face([(*p, z + NP.WELL_H) for p in _rect(c, u, hu, hv)], "white", (0, 0, 1))
    rail = [NP.pt(s1, o1 + w / 2), NP.pt(s0 - w / 2, o1 + w / 2), NP.pt(s0 - w / 2, o0 + 0.05)]
    top = z + NP.WELL_H + NP.RAIL_H
    for pa, pb in zip(rail, rail[1:]):
        d = math.hypot(pb[0] - pa[0], pb[1] - pa[1])
        n = max(1, int(round(d / 1.2)))
        for k in range(n + 1):
            px, py = pa[0] + (pb[0] - pa[0]) * k / n, pa[1] + (pb[1] - pa[1]) * k / n
            bar((px, py, z + NP.WELL_H), (px, py, top), 0.022, "steel_grey")
        for h in (top, top - 0.5):
            bar((*pa, h), (*pb, h), 0.022, "steel_grey")
    _clear("basement stair at the hall's north face", well_ring(NP.WELL_WALL))
    # --- two grey galvanised boxes on the lawn: plinth, body with a blade panel towards the street, a lid ---
    bl, bw, bh = NP.BOX_SIZE
    for x, y in NP.BOXES:
        g = E.ground_z(x, y)
        pbox((x, y), NP.U, bl / 2 - 0.04, bw / 2 - 0.04, g - 0.2, g + 0.1, "grey_back", top=False)
        ring = pbox((x, y), NP.U, bl / 2, bw / 2, g + 0.1, g + bh, "steel_grey", top=False)
        pbox((x, y), NP.U, bl / 2 + 0.05, bw / 2 + 0.05, g + bh, g + bh + 0.05, "steel_light" if "steel_light" in ST.PALETTE else "steel_grey")
        fa, fb = ring[0], ring[1]                              # the long side towards the street
        ins = (NP.N[0] * -0.012, NP.N[1] * -0.012)
        qa = (fa[0] + NP.U[0] * 0.2 + ins[0], fa[1] + NP.U[1] * 0.2 + ins[1])
        qb = (fb[0] - NP.U[0] * 0.2 + ins[0], fb[1] - NP.U[1] * 0.2 + ins[1])
        m.face([(*qa, g + 0.25), (*qb, g + 0.25), (*qb, g + bh - 0.15), (*qa, g + bh - 0.15)], "grating", (-NP.N[0], -NP.N[1], 0.0),
               [(0.0, 0.0), (0.0, (bl - 0.4) / 1.3), ((bh - 0.4) / 0.8, (bl - 0.4) / 1.3), ((bh - 0.4) / 0.8, 0.0)])
        _clear("grey boxes on the street's lawn", _rect((x, y), NP.U, bl / 2 + 0.05, bw / 2 + 0.05))
    # --- grey concrete bench blocks on the forecourt (2.5 x 0.5 x 0.45 m), with a shadow gap at the foot ---
    sl, sw, sh = NP.BENCH_SIZE
    for x, y, bearing in NP.BENCHES:
        u = (math.sin(math.radians(bearing)), math.cos(math.radians(bearing)))
        pbox((x, y), u, sl / 2 - 0.03, sw / 2 - 0.03, z - 0.02, z + 0.04, "grey_back", top=False)
        pbox((x, y), u, sl / 2, sw / 2, z + 0.04, z + sh, "concrete")
        _clear("bench blocks on the forecourt", _rect((x, y), u, sl / 2, sw / 2))
    # --- the bus stop: a slim post with a blank plate (no lettering, no timetable) ---
    bx, by = NP.BUS_STOP
    bar((bx, by, z - 0.1), (bx, by, z + 2.7), 0.035, "steel_grey")
    for side in (1, -1):
        pal([(bx - 0.03 * side, by - 0.25, z + 2.1), (bx - 0.03 * side, by + 0.25, z + 2.1), (bx - 0.03 * side, by + 0.25, z + 2.7),
             (bx - 0.03 * side, by - 0.25, z + 2.7)], "pay_blue", (-side, 0, 0))
    pal([(bx - 0.03, by - 0.18, z + 1.2), (bx - 0.03, by + 0.18, z + 1.2), (bx - 0.03, by + 0.18, z + 1.7), (bx - 0.03, by - 0.18, z + 1.7)],
        "grey_back", (-1, 0, 0))
    pal([(bx + 0.03, by - 0.18, z + 1.2), (bx + 0.03, by + 0.18, z + 1.2), (bx + 0.03, by + 0.18, z + 1.7), (bx + 0.03, by - 0.18, z + 1.7)],
        "grey_back", (1, 0, 0))
    _clear("bus stop post", [NP.BUS_STOP])
    # --- V20d: the low white seat wall of the path beside the dry field (N01: 0.45 x 0.5 m, along the field's
    # south edge and north-north-east beside R. da Holanda), and the blank black sign slab at its corner ---
    k1 = NP.HOLANDA_KERBS[1] + NP.STRIP_EAST + NP.PATH_W                 # the path's east edge = the wall's west face
    sw, sh = NP.SEAT_WALL_W, NP.SEAT_WALL_H
    legs = [[(NP.SEAT_WALL_EAST_X, NP.SEAT_WALL_SOUTH_Y + sw / 2), (k1 + sw / 2, NP.SEAT_WALL_SOUTH_Y + sw / 2)]]
    ys = [NP.SEAT_WALL_SOUTH_Y + sw] + [y for y in (NP.HOLANDA_BEND, 230.0, NP.HOLANDA_ROW[1]) if y > NP.SEAT_WALL_SOUTH_Y]
    legs.append([(k1 + sw / 2 + NP.holanda_dx(y), y) for y in ys])
    wall_pts = []
    for line in legs:
        for pa, pb in zip(line, line[1:]):
            d = math.hypot(pb[0] - pa[0], pb[1] - pa[1])
            n = max(1, int(math.ceil(d / 6.0)))
            wu = ((pb[0] - pa[0]) / d, (pb[1] - pa[1]) / d)
            for k in range(n):
                qa = (pa[0] + (pb[0] - pa[0]) * k / n, pa[1] + (pb[1] - pa[1]) * k / n)
                qb = (pa[0] + (pb[0] - pa[0]) * (k + 1) / n, pa[1] + (pb[1] - pa[1]) * (k + 1) / n)
                g = min(E.ground_z(*qa), E.ground_z(*qb))
                pbox(((qa[0] + qb[0]) / 2, (qa[1] + qb[1]) / 2), wu, d / n / 2 + 0.005, sw / 2, g - 0.25, max(g, z) + sh,
                     "render_white")
                wall_pts += [qa, qb]
    _clear("seat wall beside the field (V20d)", wall_pts)
    sx, sy = NP.SIGN_SLAB
    g = E.ground_z(sx, sy)
    pbox((sx, sy), (0.0, 1.0), 0.8, 0.15, g - 0.2, g + 1.3, "black")
    _clear("black sign slab at the field (V20d)", [NP.SIGN_SLAB])
    obj = m.build("North_Street", col, EN.materials())
    print(f"  north (V18j): street objects {sum(len(p.vertices) - 2 for p in obj.data.polygons)} triangles (louvre screen, "
          f"basement stair and railing, {len(NP.BOXES)} boxes, {len(NP.BENCHES)} bench blocks, bus stop post)")


# =====================================================================================================
# V20d: the villas north of the dry field, their gardens, and the apartment blocks along Rua da Holanda beyond the
# world's edge (plan and sources: north_plan.py, section V20d)
# =====================================================================================================
def owns_house(b):
    """env_buildings.build_one hook: True for the villas that build_house makes."""
    return b["id"] in NP.HOUSES


def _part_rect(part):
    """An exact rectangle from four measured corners (counter-clockwise): (origin, u along edge 0, length, v, width)."""
    c0, c1, c2, c3 = part
    la, lb = math.hypot(c1[0] - c0[0], c1[1] - c0[1]), math.hypot(c2[0] - c3[0], c2[1] - c3[1])
    u = ((c1[0] - c0[0]) / la, (c1[1] - c0[1]) / la)
    v = (-u[1], u[0])
    w = ((c3[0] - c0[0]) * v[0] + (c3[1] - c0[1]) * v[1] + (c2[0] - c1[0]) * v[0] + (c2[1] - c1[1]) * v[1]) / 2
    return c0, u, (la + lb) / 2, v, w


def build_house(b, index):
    """V20d: a two-storey villa or terraced pair on its OSM footprint (north_plan.HOUSES): rendered walls (white or
    ochre per unit), a hip roof of orange tiles with HOUSE_EAVE overhang and a fascia, punched windows with white
    surrounds on both storeys, a front door towards the garden, a chimney. Returns (volume, details) meshes like
    env_buildings.build_one (it used to be a generic 10.5 m flat-roofed block in the OSM default colours)."""
    import env_campus as EC
    vol, det = EC.Mesh(), EC.Mesh()
    spec = NP.HOUSES[b["id"]]
    ochre = EC.UMBER                       # a slot that houses never use; finish() maps it to the ochre render below
    colour = {"white": EC.WALL, "ochre": ochre}
    tris_before = 0
    for k, (part, walls) in enumerate(zip(spec["parts"], spec["walls"])):
        o, u, length, v, width = _part_rect(part)
        corners = [o, (o[0] + u[0] * length, o[1] + u[1] * length),
                   (o[0] + u[0] * length + v[0] * width, o[1] + u[1] * length + v[1] * width),
                   (o[0] + v[0] * width, o[1] + v[1] * width)]
        g = min(E.ground_z(*c) for c in corners)
        z0, top = g - 0.6, g + spec["storeys"] * NP.HOUSE_STOREY + 0.3
        units = len(walls)

        def at(a, s_):
            return (o[0] + u[0] * a + v[0] * s_, o[1] + u[1] * a + v[1] * s_)
        long_u = length >= width
        # walls round the counter-clockwise ring (0,0) -> (L,0) -> (L,W) -> (0,W); the long sides split into units,
        # the ends in the colour of the unit they belong to (Mesh.wall: visible from the right of a -> b)
        if long_u:
            cuts = [length * j / units for j in range(units + 1)]
            for j in range(units):
                vol.wall(at(cuts[j], 0.0), at(cuts[j + 1], 0.0), z0, top, colour[walls[j]])
                vol.wall(at(cuts[j + 1], width), at(cuts[j], width), z0, top, colour[walls[j]])
            vol.wall(at(length, 0.0), at(length, width), z0, top, colour[walls[-1]])
            vol.wall(at(0.0, width), at(0.0, 0.0), z0, top, colour[walls[0]])
        else:
            cuts = [width * j / units for j in range(units + 1)]
            for j in range(units):
                vol.wall(at(length, cuts[j]), at(length, cuts[j + 1]), z0, top, colour[walls[j]])
                vol.wall(at(0.0, cuts[j + 1]), at(0.0, cuts[j]), z0, top, colour[walls[j]])
            vol.wall(at(0.0, 0.0), at(length, 0.0), z0, top, colour[walls[0]])
            vol.wall(at(length, width), at(0.0, width), z0, top, colour[walls[-1]])
        # hip roof over the rectangle grown by the eave
        e = NP.HOUSE_EAVE
        la, wa = (length, width) if long_u else (width, length)
        ax_u = u if long_u else v
        ax_v = v if long_u else u
        cx, cy = at(length / 2, width / 2)
        hl, hw = la / 2 + e, wa / 2 + e
        rise = hw * math.tan(math.radians(NP.HOUSE_PITCH))
        ridge = max(hl - hw, 0.0)

        def p3(a, s_, z):
            return (cx + ax_u[0] * a + ax_v[0] * s_, cy + ax_u[1] * a + ax_v[1] * s_, z)
        ez = top
        eaves = [p3(-hl, -hw, ez), p3(hl, -hw, ez), p3(hl, hw, ez), p3(-hl, hw, ez)]
        r0, r1 = p3(-ridge, 0.0, ez + rise), p3(ridge, 0.0, ez + rise)
        vol.up([eaves[0], eaves[1], r1, r0], EC.TILES)
        vol.up([eaves[2], eaves[3], r0, r1], EC.TILES)
        vol.up([eaves[1], eaves[2], r1], EC.TILES)
        vol.up([eaves[3], eaves[0], r0], EC.TILES)
        fascia = [p3(-hl, -hw, ez - 0.18), p3(hl, -hw, ez - 0.18), p3(hl, hw, ez - 0.18), p3(-hl, hw, ez - 0.18)]
        for a_, b_, fa, fb in zip(eaves, eaves[1:] + eaves[:1], fascia, fascia[1:] + fascia[:1]):
            mid = ((a_[0] + b_[0]) / 2, (a_[1] + b_[1]) / 2)
            vol.towards([fa, fb, b_, a_], EC.WALL, (2 * mid[0] - cx, 2 * mid[1] - cy, ez - 0.09))      # the fascia
        inner = [p3(-hl + e, -hw + e, ez - 0.18), p3(hl - e, -hw + e, ez - 0.18), p3(hl - e, hw - e, ez - 0.18),
                 p3(-hl + e, hw - e, ez - 0.18)]
        for a_, b_, fa, fb in zip(fascia, fascia[1:] + fascia[:1], inner, inner[1:] + inner[:1]):
            vol.down([a_, b_, fb, fa], EC.WALL)                                           # the soffit under the eave
        # windows with white surrounds, two storeys; a front door in the middle of each unit on the garden side
        # (the side facing south-ish: the long side whose outward normal has the larger -y component)
        sides = [((0.0, 0.0), (length, 0.0)), ((length, 0.0), (length, width)), ((length, width), (0.0, width)),
                 ((0.0, width), (0.0, 0.0))]
        for (a0, s0), (a1, s1) in sides:
            pa, pb = at(a0, s0), at(a1, s1)
            seg = math.hypot(pb[0] - pa[0], pb[1] - pa[1])
            nx, ny = (pb[1] - pa[1]) / seg, -(pb[0] - pa[0]) / seg                          # outward (ring is ccw)
            mx, my = (pa[0] + pb[0]) / 2, (pa[1] + pb[1]) / 2
            if nx * (mx - cx) + ny * (my - cy) < 0:
                nx, ny = -nx, -ny
            count = int((seg - 1.4) // 3.1)
            if count < 1:
                continue
            front = ny < -0.6
            for s_ in range(spec["storeys"]):
                zb = g + s_ * NP.HOUSE_STOREY
                for w in range(count):
                    t = (0.7 + 1.55 + w * 3.1 + (seg - 1.4 - count * 3.1) / 2) / seg
                    door = front and s_ == 0 and w == count // 2
                    half, lo, hi = (0.55, zb + 0.05, zb + 2.25) if door else (0.55, zb + 0.95, zb + 2.25)
                    q0 = (pa[0] + (pb[0] - pa[0]) * t - (pb[0] - pa[0]) / seg * half, pa[1] + (pb[1] - pa[1]) * t - (pb[1] - pa[1]) / seg * half)
                    q1 = (pa[0] + (pb[0] - pa[0]) * t + (pb[0] - pa[0]) / seg * half, pa[1] + (pb[1] - pa[1]) * t + (pb[1] - pa[1]) / seg * half)
                    for off, grow_, slot in ((0.025, 0.12, EC.WALL), (0.045, 0.0, EC.DARK)):
                        ga = (q0[0] - (pb[0] - pa[0]) / seg * grow_ + nx * off, q0[1] - (pb[1] - pa[1]) / seg * grow_ + ny * off)
                        gb = (q1[0] + (pb[0] - pa[0]) / seg * grow_ + nx * off, q1[1] + (pb[1] - pa[1]) / seg * grow_ + ny * off)
                        det.towards([(*ga, lo - grow_), (*gb, lo - grow_), (*gb, hi + grow_ * 0.6), (*ga, hi + grow_ * 0.6)],
                                    slot, (ga[0] + nx * 5, ga[1] + ny * 5, lo))
        if k == 0:                                                                          # a chimney on the ridge
            ch = p3(ridge * 0.45 if ridge > 0.5 else 0.0, hw * 0.25, 0.0)
            det.box((ch[0], ch[1]), ax_u, 0.35, 0.3, ez + rise * 0.6, ez + rise + 0.75, EC.WALL, EC.DARKROOF)
        EC.note_clear(f"villa {b['id']} part {k}", corners)
    volume, details = vol.finish("B_" + b["id"]), det.finish("BD_" + b["id"])
    ochre_mat = E.material("B_wall_2", "#E3C891", 0.9)                  # env_buildings.OTHER_WALLS[2], the ochre render
    for mesh in (volume, details):
        for i, m in enumerate(mesh.materials):
            if m and m.name == "W_B_wall_umber":
                mesh.materials[i] = ochre_mat
    print(f"  north (V20d): villa {b['id']}: {len(spec['parts'])} parts, {spec['storeys']} storeys, "
          f"{sum(len(p.vertices) - 2 for p in volume.polygons)} + {sum(len(p.vertices) - 2 for p in details.polygons)} triangles")
    return volume, details


def build_gardens(col):
    """V20d: North_Gardens: the tall hedge that closes the villas' gardens to the dry field, the hedges between the
    plots, the pools with a pale coping (orthophoto); leaf cards over the hedges (env_vegetation.hedge_leaves)."""
    import env_streets as ST
    import env_vegetation as V
    m = EN_Faces()
    d = NP.HEDGE_DEPTH / 2
    (ax, ay), (bx, by) = NP.GARDEN_LINE
    L = math.hypot(bx - ax, by - ay)
    gu = ((bx - ax) / L, (by - ay) / L)
    gn = (-gu[1], gu[0])                                       # into the gardens (north)
    hedges = [(list(NP.GARDEN_LINE), NP.HEDGE_H)]
    for x in NP.GARDEN_DIVIDERS:
        y = NP.garden_y(x)
        end = None
        for t in [0.5 * k for k in range(1, 60)]:              # up to 0.6 m before the next house wall
            px, py = x + gn[0] * t, y + gn[1] * t
            if any(_in_house(px + gn[0] * 0.6, py + gn[1] * 0.6, h) for h in NP.HOUSES):
                end = t
                break
        hedges.append(([(x + gn[0] * 0.5, y + gn[1] * 0.5), (x + gn[0] * (end or 12.0), y + gn[1] * (end or 12.0))], NP.DIVIDER_H))
    leaves = []
    for line, h in hedges:
        for pa, pb in zip(line, line[1:]):
            seg = math.hypot(pb[0] - pa[0], pb[1] - pa[1])
            uu = ((pb[0] - pa[0]) / seg, (pb[1] - pa[1]) / seg)
            n = max(1, int(math.ceil(seg / 6.0)))
            for k in range(n):
                c = (pa[0] + (pb[0] - pa[0]) * (k + 0.5) / n, pa[1] + (pb[1] - pa[1]) * (k + 0.5) / n)
                g0 = min(E.ground_z(pa[0] + (pb[0] - pa[0]) * k / n, pa[1] + (pb[1] - pa[1]) * k / n),
                         E.ground_z(pa[0] + (pb[0] - pa[0]) * (k + 1) / n, pa[1] + (pb[1] - pa[1]) * (k + 1) / n))
                m.box(c, uu, seg / n / 2 + 0.01, d, g0 - 0.3, g0 + h - 0.15, "hedge")
        leaves += V.hedge_box_faces(line, NP.HEDGE_DEPTH, lambda x, y: E.ground_z(x, y), lambda x, y, hh=h: E.ground_z(x, y) + hh)
    _clear("garden hedges north of the field", [p for line, _h in hedges for p in line], 20.0)
    for x0, y0, x1, y1 in NP.POOLS:
        g = E.ground_z((x0 + x1) / 2, (y0 + y1) / 2)
        cop = 0.35
        ring_o = [(x0 - cop, y0 - cop), (x1 + cop, y0 - cop), (x1 + cop, y1 + cop), (x0 - cop, y1 + cop)]
        ring_i = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
        for a_, b_, ia, ib in zip(ring_o, ring_o[1:] + ring_o[:1], ring_i, ring_i[1:] + ring_i[:1]):
            m.face([(*a_, g + 0.08), (*b_, g + 0.08), (*ib, g + 0.08), (*ia, g + 0.08)], "palette", (0, 0, 1),
                   [ST.pal_uv("kerb")] * 4)
        m.face([(*p, g + 0.03) for p in ring_i], "palette", (0, 0, 1), [ST.pal_uv("bin_blue")] * 4)
    mats = dict(EN_materials())
    mats["hedge"] = ST.mat("hedge")
    obj = m.build("North_Gardens", col, mats)
    leaf = V.hedge_leaves("North_GardenHedgeLeaves", leaves, "hedge", "broad", card=1.1, cover=2.6, seed=2020, fins=0.45)
    if leaf:                    # into World_North: env_coast seeds its hedges by the count of World_HedgeLeaves' objects
        for c in list(leaf.users_collection):
            c.objects.unlink(leaf)
        col.objects.link(leaf)
    print(f"  north (V20d): gardens {len(obj.data.polygons)} faces ({len(hedges)} hedges, {len(NP.POOLS)} pools), hedge "
          f"leaves {len(leaf.data.polygons) if leaf else 0} faces")


def _in_house(x, y, hid):
    for part in NP.HOUSES[hid]["parts"]:
        if NP.inside(x, y, list(part)):
            return True
    return False


def EN_Faces():
    import env_entrance as EN
    return EN.Faces()


def EN_materials():
    import env_entrance as EN
    return EN.materials()


def build_far_housing(col):
    """V20d (called from env_backdrop.build_outer_buildings, which leaves these out): the apartment blocks along Rua
    da Holanda north of the world's edge as the two at the forecourt (env_campus.build_housing), storeys per block."""
    import env_campus as EC
    import campus_plan as CP
    keep = CP.HOUSING_STOREYS
    n = 0
    try:
        from world_data import load_osm, way_points, polygon_area
        elements, nodes, _ways = load_osm()
        for e in elements:
            wid = "w" + str(e.get("id"))
            if e["type"] != "way" or wid not in NP.FAR_HOUSING:
                continue
            ring = way_points(e, nodes)
            if len(ring) > 1 and ring[0] == ring[-1]:
                ring = ring[:-1]
            if polygon_area(ring) < 0:
                ring = ring[::-1]
            CP.HOUSING_STOREYS = NP.FAR_HOUSING[wid]
            vol, det = EC.build_housing({"id": wid, "points": [list(p) for p in ring]})
            obj = E.new_object("B_" + wid + "_far", vol, col)
            if det.polygons:
                E.new_object("BD_" + wid + "_far", det, col).parent = obj
            n += 1
    finally:
        CP.HOUSING_STOREYS = keep
    print(f"  north (V20d): {n} apartment blocks along R. da Holanda beyond the world's edge as white 4-5 storey blocks")
    return n


def road_look_lawn():
    """The road surface 'north_lawn': the terrain's own material with the terrain's UVs (lawn_road.py uses it too)."""
    import env_streets as ST
    import env_track as T
    looks = T._looks()
    if "north_lawn" not in looks:
        looks["north_lawn"] = (ST.mat("ground_map"), _world_uv)
    return "north_lawn"


def road_look(x, y):
    """env_track.ROAD_LOOK hook for route points north_plan.LOOK_I: what the race road shows at (x, y) (the middle of a
    road quad or of a shoulder). On the real lawn of the street (north_plan.LAWN) it is the terrain's own material
    with the terrain's UVs: the ground colour map is lawn under these quads (north_plan.ground_paint), so the road
    there IS the lawn, not a second street. Everywhere else: the setts, in the start plaza's UV frame like the sheet
    beside the road."""
    import env_streets as ST
    import env_track as T
    looks = T._looks()
    if "north_lawn" not in looks:
        looks["north_lawn"] = (ST.mat("ground_map"), _world_uv)
    return "north_lawn" if NP.road_on_lawn(x, y) else "entrance_calcada"


def build():
    """Call after env_entrance.build() (the terrain, the buildings and the street paving are built then)."""
    col = E.collection(COLLECTION)
    build_forecourt(col)
    build_door_aprons(col)
    build_street(col)
    if RING_ON_ROAD:
        build_ring_on_road(col)
    else:
        print("  north: the dark ring is built only beside the road (RING_ON_ROAD = False, see env_north.py)")
    build_structures(col)
    build_gardens(col)                     # V20d: the villas' hedges and pools (the villas: env_buildings -> build_house)


# =====================================================================================================
# Dressing
# =====================================================================================================
def _posts():
    """V18g (Street View May 2026): timber-coloured posts along the kerb of the slip lane and in two rows beside the
    driveway to the car-park ramp: [(x, y)] (entrance_plan.kerb_posts)."""
    return P.kerb_posts()


def _holanda_dressing(protos, cars):
    """V20d: what stands in Rua da Holanda (north_plan, section V20d): cars nose-in in the bays west of the asphalt
    (BAY_SHARE of them, a fixed draw), one car along the east kerb, the blue P sign with the stone block bollard at
    the corner, slim steel bollards along the kerb, two scooters at the seat wall, two street lamps. Placements."""
    import random
    import street_plan as S
    rng = random.Random(2020)
    out = []

    def z_at(x, y):
        return round(NP.LEVEL if on_sheet(x, y) else E.ground_z(x, y), 3)

    def add(proto, x, y, rot, scale=1.0, dz=0.0, limit=CLEAR):
        if proto not in protos:
            return
        _clear(f"R. da Holanda: {proto}", [(x, y)], limit)
        out.append({"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(z_at(x, y) + dz, 3),
                    "rot_z": round(rot, 4), "scale": scale})
    if cars:
        for k, (x, y, nose) in enumerate(NP.holanda_bays()):
            if rng.random() > NP.BAY_SHARE:
                continue
            add(cars[rng.randrange(len(cars))], x + rng.uniform(-0.15, 0.15), y + rng.uniform(-0.12, 0.12),
                math.atan2(nose[1], nose[0]) + math.radians(rng.uniform(-3.0, 3.0)), limit=CAR_CLEAR)
        add(cars[5 % len(cars)], NP.LAY_BY_CAR[0], NP.LAY_BY_CAR[1], math.radians(92.0), limit=CAR_CLEAR)
    add("Prop_SignParking", NP.PARKING_SIGN[0], NP.PARKING_SIGN[1], -math.pi)              # its face (+y) to the south
    add("Prop_BollardStone", NP.STONE_BOLLARD[0], NP.STONE_BOLLARD[1], math.radians(12.0), 1.4)
    for x, y in NP.SLIM_BOLLARDS:
        add("Prop_BollardSlim", x, y, 0.0, 1.0, 0.04)
    for x, y, bearing in NP.SCOOTERS:
        name = "Prop_AIScooter" if "Prop_AIScooter" in protos else "Prop_AIEScooter"
        add(name, x, y, math.radians(90.0 - bearing))
    for x, y in NP.STREET_LAMPS:
        add("Prop_LampSidewalk", x + NP.holanda_dx(y), y, math.radians(180.0))             # the arm over the street
    return out


def dress(veg, props, protos):
    """Edits the dressing lists after env_entrance.dress and rewrites the two placement files."""
    import env_street_objects as SO
    import env_streets as ST
    import street_plan as S
    avenue = [lay["ring"] for lay in S.LAYERS if lay["name"].startswith("B2 ")]
    solids = [waste_ring(0.8), stair_ring()[0], _circle(SEAT_RING_C, SEAT_RING_R + 0.6, 16),
              S.ribbon(PARK_PATH, -1.6, 1.6), S.ribbon(EDGE, -BAND_PINK - BAND_OCHRE - 1.1, 0.0)]
    solids += [well_ring(0.6), _circle(NP.BUS_STOP, 0.8, 8)]                 # V18j: the street's and the forecourt's new solids
    k0, k1 = NP.HOLANDA_KERBS                                                 # V20d: the garden hedge, R. da Holanda's
    kw = k1 + NP.STRIP_EAST + NP.PATH_W                                       # bays and asphalt, the seat wall
    solids += [S.ribbon(NP.GARDEN_LINE, -1.0, 1.0), NP.holanda_band(k0 - NP.BAY_DEPTH - 0.3, k1 + 0.3),
               NP.holanda_band(kw - 0.4, kw + NP.SEAT_WALL_W + 0.4, NP.SEAT_WALL_SOUTH_Y - 0.4),
               [(kw - 0.4, NP.SEAT_WALL_SOUTH_Y - 0.4), (NP.SEAT_WALL_EAST_X + 0.4, NP.SEAT_WALL_SOUTH_Y - 0.4),
                (NP.SEAT_WALL_EAST_X + 0.4, NP.SEAT_WALL_SOUTH_Y + NP.SEAT_WALL_W + 0.4),
                (kw - 0.4, NP.SEAT_WALL_SOUTH_Y + NP.SEAT_WALL_W + 0.4)]]
    solids += [_rect(c, NP.U, NP.BOX_SIZE[0] / 2 + 0.6, NP.BOX_SIZE[1] / 2 + 0.6) for c in NP.BOXES]
    solids += [_rect((x, y), (math.sin(math.radians(b)), math.cos(math.radians(b))), NP.BENCH_SIZE[0] / 2 + 0.5, NP.BENCH_SIZE[1] / 2 + 0.5)
               for x, y, b in NP.BENCHES]
    n_frozen = (len(veg), len(props))

    def gone(p):
        x, y = p["x"], p["y"]
        if p["proto"] in CAFE and on_sheet(x, y):
            return "cafe furniture on the forecourt"
        if p["proto"] in CAFE and NP.out_distance(x, y, NP.LAWN) < 3.0:
            return "planters on the street's lawn"
        if any(P.inside(x, y, r) for r in solids):
            return "under a new solid or on the edge bands"
        if 99.0 <= y <= 173.0 and any(P.inside(x, y, r) for r in avenue):
            return "on the avenue's new paving"
        if p["proto"] in ("Prop_BollardSlim", "Prop_BikeRack") and x < -20.0 and y > 95.0 and (P.in_planted(x, y, 0.3) or x < -75.0):
            return "V16 posts and racks where Google shows lawn or the new kerb"
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
        added.append({"proto": cars[(k * 3 + 1) % len(cars)], "x": x, "y": y, "z": round(NP.LEVEL, 3),      # V18j: on the flat sheet
                      "rot_z": round(math.atan2(u[1], u[0]) + (0.06 if k % 2 else -0.04), 4), "scale": 1.0})
    for k, (x, y, bearing) in enumerate(P.LAYBY_CARS):           # V18g (satellite): cars in the lay-by at the avenue
        if cars and S.route_distance(x, y) >= CAR_CLEAR:
            cu = (math.sin(math.radians(bearing)), math.cos(math.radians(bearing)))
            added.append({"proto": cars[(k * 2 + 4) % len(cars)], "x": x, "y": y, "z": round(SO.surface(x, y), 3),
                          "rot_z": round(math.atan2(cu[1], cu[0]), 4), "scale": 1.0})
    n_cars = len(added)
    post = "Prop_TimberPost" if "Prop_TimberPost" in protos else "Prop_BollardSlim"
    if post in protos:
        for x, y in _posts():
            _clear("posts along the kerb and the driveway", [(x, y)])
            added.append({"proto": post, "x": round(x, 3), "y": round(y, 3), "z": round(SO.surface(x, y) + 0.15, 3),
                          "rot_z": 0.0, "scale": 1.3 if post == "Prop_TimberPost" else 1.05})
    n_posts = len(added) - n_cars
    if "Prop_SignRound" in protos:                        # V18g: the blue round sign where the cycle track ends
        x, y = P.CYCLE_SIGN
        added.append({"proto": "Prop_SignRound", "x": x, "y": y, "z": round(SO.surface(x, y), 3),
                      "rot_z": round(math.radians(170.0), 4), "scale": 1.0})
    if "Prop_LampSidewalk" in protos:                     # V18g: tall grey lamps with one arm on the sidewalk
        for x, y in P.KERB_LAMPS:
            added.append({"proto": "Prop_LampSidewalk", "x": x, "y": y, "z": round(SO.surface(x, y), 3),
                          "rot_z": round(math.radians(180.0), 4), "scale": 1.0})
    n_posts = len(added) - n_cars
    # bike racks on the planks along B (three steel hoops each; the real ones are timber post-and-rail racks)
    stands = [(p["x"], p["y"]) for p in veg2 + props2 if -100.0 < p["x"] < 20.0 and 95.0 < p["y"] < 150.0]
    if "Prop_BikeRack" in protos:
        for t in BIKE_RACK_T:
            for dt in (0.0, 0.9, -0.9, 1.8, -1.8):
                x, y = P.band_pt(t + dt, BIKE_RACK_S)
                if S.route_distance(x, y) >= CLEAR + 1.2 and all(math.hypot(sx - x, sy - y) >= 1.9 for sx, sy in stands):
                    _REPORT.append(("bike racks on the plank band", S.route_distance(x, y) - 1.0))
                    added.append({"proto": "Prop_BikeRack", "x": round(x, 3), "y": round(y, 3), "z": round(ST.ground(x, y) + 0.07, 3),
                                  "rot_z": round(math.atan2(P.BAND_U[1], P.BAND_U[0]), 4), "scale": 1.0})
                    stands.append((x, y))
                    break
    n_racks = len(added) - n_cars - n_posts
    # V18j: the ground moved under the dressing that was there before (level_nodes). What stands on the flat paving
    # stands at the sheet's level; everything else moves with the ground under it
    n_rez = 0
    for items in (veg2, props2):
        for k, p in enumerate(items):
            if on_sheet(p["x"], p["y"]) and S.route_distance(p["x"], p["y"]) >= NP.KEEP_OUT:
                z = NP.LEVEL
            else:
                dz = ground_shift(p["x"], p["y"])
                z = p["z"] + dz if abs(dz) >= 0.01 else p["z"]
            if abs(z - p["z"]) > 0.005:
                items[k] = dict(p, z=round(z, 3))
                n_rez += 1
    n_v20 = len(added)
    added += _holanda_dressing(protos, cars)                    # V20d: Rua da Holanda's parking row and corner
    n_v20 = len(added) - n_v20
    props2 += added
    n_saplings = 0
    if "Veg_PlazaTree" in protos:                         # Milestone's west lawn: a few staked saplings (N06, N14):
        for k, (x, y) in enumerate(SAPLINGS):             # the plaza's young tree at 0.56-0.68 (3.1-3.8 m tall)
            b, r = P.loop_polar(x, y)                     # V18h: none on the grass ring behind the curve plaza's rim:
            if r < P.FAN_R + P.FAN_LAWN_GAP + 1.0:        # out onto the raised lawn, or dropped at Milestone's wall
                x, y = (round(v, 2) for v in P.loop_pt(b, P.FAN_R + P.FAN_LAWN_GAP + 1.0))
                if y > 161.5 - 0.0464 * (x + 40.8) - 2.0:
                    continue
            if S.route_distance(x, y) < E.KEEP_CLEAR + 1.5:
                continue
            _REPORT.append(("saplings on Milestone's lawn", S.route_distance(x, y) - 0.8))
            veg2.append({"proto": "Veg_PlazaTree_B" if k % 2 else "Veg_PlazaTree", "x": x, "y": y,
                         "z": round(ST.ground(x, y), 3), "rot_z": round(1.3 * k, 4), "scale": round(0.56 + 0.06 * (k % 3), 3)})
            n_saplings += 1
    # V18 (team walk 7 Nov 2026, video IMG_7725 6:00-6:35): along the north loop behind A the lawn strip on the right
    # carries a row of young trees, and grey technical cabinets stand in its east part (orthophoto 2023 positions)
    for k, (x, y) in enumerate(NORTH_LOOP_TREES):
        if "Veg_PlazaTree" in protos and S.route_distance(x, y) >= E.KEEP_CLEAR + 1.5:
            _REPORT.append(("young trees on the north loop's lawn", S.route_distance(x, y) - 0.8))
            veg2.append({"proto": "Veg_PlazaTree" if k % 2 else "Veg_PlazaTree_B", "x": x, "y": y,
                         "z": round(ST.ground(x, y), 3), "rot_z": round(0.9 * k, 4), "scale": round(0.62 + 0.06 * (k % 3), 3)})
            n_saplings += 1
    for k, (x, y) in enumerate(NORTH_LOOP_CABINETS):
        if "Prop_CabinetGrey" in protos and S.route_distance(x, y) >= CLEAR:
            _REPORT.append(("grey cabinets on the north loop's lawn", S.route_distance(x, y) - 0.5))
            props2.append({"proto": "Prop_CabinetGrey", "x": x, "y": y, "z": round(ST.ground(x, y), 3),
                           "rot_z": round(math.radians(-25.0), 4), "scale": 1.0})
    if "Veg_Olive" in protos:                             # V18g: the larger tree on Milestone's lawn, the wood behind the wall
        for k, (x, y, sc) in enumerate([(P.J_TREE[0], P.J_TREE[1], 0.8)] + list(NORTH_WOOD)):
            if S.route_distance(x, y) >= E.KEEP_CLEAR + 3.5:
                _REPORT.append(("trees on Milestone's lawn and behind the north wall", S.route_distance(x, y) - 2.5))
                veg2.append({"proto": "Veg_Olive_B" if k % 2 and "Veg_Olive_B" in protos else "Veg_Olive", "x": x, "y": y,
                             "z": round(ST.ground(x, y), 3), "rot_z": round(1.1 * k, 4), "scale": sc})
    # V20d: the umbrella pine at the apartment block's corner (real crown r 4.5 m over the street; at scale 0.8 the
    # crown starts 4.1 m up, so its trunk may stand 7.0 m from the line, 3 m outside the road), the garden trees
    for x, y, proto, sc in [(NP.CORNER_PINE[0], NP.CORNER_PINE[1], "Veg_UmbrellaPine", 0.8)] + list(NP.GARDEN_TREES):
        if proto in protos and S.route_distance(x, y) >= (7.0 if (x, y) == NP.CORNER_PINE else E.KEEP_CLEAR + 2.5):
            _REPORT.append(("V20d trees at R. da Holanda and in the gardens", S.route_distance(x, y) - 2.5))
            veg2.append({"proto": proto, "x": x, "y": y, "z": round(E.ground_z(x, y), 3),
                         "rot_z": round(0.7 * x % 6.283, 4), "scale": sc})
    print(f"  north dressing (V20d): {n_v20} parked cars, signs, bollards, scooters and lamps at R. da Holanda")
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
    print(f"  north dressing (V18j): {n_rez} plants and props of the frozen dressing set on the levelled ground "
          f"(of {n_frozen[0]} + {n_frozen[1]})")
    print(f"  north dressing: {n_cars} parked cars on the forecourt, {n_posts} posts, signs and lamps along the kerb "
          f"and across the plaza's south-west corner, {n_racks} bike racks on the plank band, {n_saplings} saplings on "
          f"Milestone's lawn")
    for why, names in sorted(removed.items()):
        print(f"  north dressing: removed ({why}): {', '.join(sorted(names))}")
    low = {}
    for what, d in _REPORT:
        low[what] = min(d, low.get(what, 1e9))
    for what, d in sorted(low.items()):
        print(f"  north clearance: {what}: " + (f"{d:.2f} m" if d < 1e6 else "more than 60 m"))
    return veg2, props2
