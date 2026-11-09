"""layout.json (OSM, local metres) + hand-placed route -> NovaTrackData.json for NovaCampusBuilder.
Usage: python -I make_track_data.py <layout.json> <out NovaTrackData.json> [map-base.png out-overlay.png]
Axes: +X east, +Z north, Y up, 1 unit = 1 m. Origin = Praca EDP.
"""
import json, math, sys

LAY = json.load(open(sys.argv[1], encoding="utf-8"))
OUT = sys.argv[2]

CAMPUS_Y = 8.0
BEACH_Y = 3.5
TUNNEL_Y = 3.4
SEA_Z = -182.0

# Avenida Marginal centre line, sorted west -> east (builder needs one z per x).
MARGINAL = [(-560, 62), (-430, 37), (-330, -22), (-265, -40), (-232, -50), (-212, -58), (-190, -70),
            (-170, -78), (-128, -86), (-60, -87), (60, -86), (170, -86), (265, -84), (340, -70), (440, -38)]

# Control points in driving order: (x, z, y, width, section). y None = interpolate.
CTRL = [
    (-185, 127, CAMPUS_Y, 11, "01_start"),
    (-160, 135, CAMPUS_Y, 11, "02_founders"),
    (-140, 141, CAMPUS_Y, 10, "02_founders"),
    (-134, 155, CAMPUS_Y, 9, "03_milestone_gap"),
    (-135, 170, CAMPUS_Y, 8.5, "03_milestone_gap"),
    (-129, 183, CAMPUS_Y, 8, "03_milestone_gap"),
    (-112, 188.5, CAMPUS_Y, 8, "04_north_terrace"),
    (-92, 199, CAMPUS_Y, 9, "04_north_terrace"),
    (-72, 207, CAMPUS_Y, 9, "04_north_terrace"),
    (-56, 212, CAMPUS_Y, 9, "04_north_terrace"),
    (-42, 205, CAMPUS_Y, 9, "05_learning_street"),
    (-31, 190, CAMPUS_Y, 9, "05_learning_street"),
    (-22, 170, CAMPUS_Y, 9, "05_learning_street"),
    (-13, 145, CAMPUS_Y, 9, "05_learning_street"),
    (-6, 120, CAMPUS_Y, 9, "05_learning_street"),
    (0, 95, CAMPUS_Y, 9, "05_learning_street"),
    (8, 76, CAMPUS_Y, 9, "06_gym_bend"),
    (26, 66, CAMPUS_Y, 9, "06_gym_bend"),
    (42, 50, CAMPUS_Y, 9, "06_gym_bend"),
    (46, 33, CAMPUS_Y, 9, "06_gym_bend"),
    (37, 18, CAMPUS_Y, 10, "07_edp_plaza"),
    (22, 8, CAMPUS_Y, 10, "07_edp_plaza"),
    (9, 4, CAMPUS_Y, 8.5, "07_edp_plaza"),
    (-3, 2, CAMPUS_Y, 7, "07_edp_plaza"),
    (-14, -6, CAMPUS_Y, 7.5, "07_edp_plaza"),
    (-25, -15, CAMPUS_Y, 8, "08_beachway_ramp"),
    (-36, -26, None, 8, "08_beachway_ramp"),
    (-48, -37, None, 8, "08_beachway_ramp"),
    (-60, -48, TUNNEL_Y, 8, "09_tunnel"),
    (-76, -61, TUNNEL_Y, 8, "09_tunnel"),
    (-92, -75, TUNNEL_Y, 8, "09_tunnel"),
    (-108, -88, TUNNEL_Y, 8, "09_tunnel"),
    (-124, -101, TUNNEL_Y, 9, "10_promenade"),
    (-141, -113, BEACH_Y, 10, "10_promenade"),
    (-162, -121, BEACH_Y, 10, "10_promenade"),
    (-186, -124, BEACH_Y, 10, "10_promenade"),
    (-205, -119, BEACH_Y, 10, "11_marginal_climb"),
    (-217, -106, None, 9, "11_marginal_climb"),
    (-221, -89, None, 9, "11_marginal_climb"),
    (-217, -72, None, 9, "11_marginal_climb"),
    (-212, -57, CAMPUS_Y, 9, "11_marginal_climb"),
    (-210, -40, CAMPUS_Y, 9, "12_west_edge"),
    (-212, -15, CAMPUS_Y, 9, "12_west_edge"),
    (-214, 15, CAMPUS_Y, 9, "12_west_edge"),
    (-216, 45, CAMPUS_Y, 9, "12_west_edge"),
    (-218, 75, CAMPUS_Y, 9, "12_west_edge"),
    (-218, 98, CAMPUS_Y, 9, "12_west_edge"),
    (-212, 113, CAMPUS_Y, 10, "12_west_edge"),
    (-200, 121, CAMPUS_Y, 11, "01_start"),
]
SPACING = 5.0
N_CHECKPOINTS = 10


def catmull(p0, p1, p2, p3, t):
    t2, t3 = t * t, t * t * t
    return tuple(0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3)
                 for a, b, c, d in zip(p0, p1, p2, p3))


def fill_heights(ctrl):
    """Linear y between the nearest known heights, by distance along the control polygon."""
    n = len(ctrl)
    cum = [0.0]
    for i in range(1, n):
        cum.append(cum[-1] + math.dist(ctrl[i - 1][:2], ctrl[i][:2]))
    ys = [c[2] for c in ctrl]
    out = list(ys)
    for i in range(n):
        if ys[i] is not None:
            continue
        a = i - 1
        while ys[a] is None:
            a -= 1
        b = i + 1
        while ys[b % n] is None:
            b += 1
        t = (cum[i] - cum[a]) / (cum[b] - cum[a])
        out[i] = ys[a] + (ys[b] - ys[a]) * t
    return [(c[0], out[i], c[1], c[3], c[4]) for i, c in enumerate(ctrl)]


def resample(ctrl):
    n = len(ctrl)
    pts = []
    for i in range(n):
        p0, p1, p2, p3 = (ctrl[(i + k) % n] for k in (-1, 0, 1, 2))
        seg = math.dist((p1[0], p1[2]), (p2[0], p2[2]))
        steps = max(1, round(seg / SPACING))
        for s in range(steps):
            t = s / steps
            x, y, z = catmull(p0[:3], p1[:3], p2[:3], p3[:3], t)
            w = p1[3] + (p2[3] - p1[3]) * t
            pts.append({"x": round(x, 2), "y": round(y, 2), "z": round(z, 2), "w": round(w, 2), "s": p1[4]})
    return pts


def marginal_z(x):
    m = MARGINAL
    for i in range(len(m) - 1):
        x0, z0 = m[i]
        x1, z1 = m[i + 1]
        if x0 <= x <= x1:
            return z0 + (z1 - z0) * (x - x0) / (x1 - x0)
    return m[0][1] if x < m[0][0] else m[-1][1]


def base_y(x, z):
    return CAMPUS_Y if z >= marginal_z(x) - 12 else BEACH_Y  # 12 m = Marginal embankment, same as the builder


def dist_seg(p, a, b):
    ax, az = a
    bx, bz = b
    px, pz = p
    dx, dz = bx - ax, bz - az
    L = dx * dx + dz * dz
    t = 0 if L == 0 else max(0, min(1, ((px - ax) * dx + (pz - az) * dz) / L))
    return math.hypot(px - (ax + t * dx), pz - (az + t * dz))


def inside(p, poly):
    x, z = p
    c = False
    for i in range(len(poly)):
        x0, z0 = poly[i]
        x1, z1 = poly[(i + 1) % len(poly)]
        if (z0 > z) != (z1 > z) and x < (x1 - x0) * (z - z0) / (z1 - z0) + x0:
            c = not c
    return c


route = resample(fill_heights(CTRL))
N = len(route)
XZ = [(p["x"], p["z"]) for p in route]


def clearance(x, z):
    """Distance from (x,z) to the road edge (negative = on the road)."""
    best = 1e9
    for i in range(N):
        d = dist_seg((x, z), XZ[i], XZ[(i + 1) % N]) - route[i]["w"] * 0.5
        best = min(best, d)
    return best


def poly_hits_road(poly, margin):
    for i in range(N):
        if inside(XZ[i], poly):
            return True
    for q in poly:
        if clearance(q[0], q[1]) < margin:
            return True
    return False


# ---------- checkpoints ----------
cum = [0.0]
for i in range(1, N):
    cum.append(cum[-1] + math.dist(XZ[i - 1], XZ[i]))
lap = cum[-1] + math.dist(XZ[-1], XZ[0])
checkpoints = []
for k in range(N_CHECKPOINTS):
    target = lap * k / N_CHECKPOINTS
    checkpoints.append(min(range(N), key=lambda i: abs(cum[i] - target)))

# ---------- buildings ----------
KIND_BY_NAME = {
    "Jerónimo Martins Grand Auditorium": "auditorium",
    "Cascais Academic Hall": "hall",
    "Main Building": "wing",
    "Santander Academic Hall": "wing",
    "CTT Student Hall": "wing",
    "Horizon Hall": "wing",
    "Teresa e Alexandre Soares dos Santos Library": "wing",
    "Student Central": "wing",
    "Milestone Carcavelos Nova": "plain",
    "NCI Academy": "plain",
}
SKIP_IDS = {1502611968}          # VivaGym = same footprint as Horizon Hall
ENTRANCE_IDS = {496301634}       # building at the top of Sagres Beach Way: terracotta stairs + white columns
buildings = []
warnings = []
for b in LAY["buildings"]:
    if b["osm_id"] in SKIP_IDS:
        continue
    fp = b["footprint"][:-1] if b["footprint"][0] == b["footprint"][-1] else b["footprint"]
    cx = sum(p[0] for p in fp) / len(fp)
    cz = sum(p[1] for p in fp) / len(fp)
    if not (-340 <= cx <= 200 and -260 <= cz <= 250):
        continue
    name = b.get("name") or ""
    kind = KIND_BY_NAME.get(name, "other")
    if b["osm_id"] in ENTRANCE_IDS:
        kind = "entrance"
        name = name or "Main entrance (Sagres Beach Way)"
    if kind == "other":
        kind = "plain"
    if any(inside(XZ[i], fp) for i in range(N)):
        warnings.append("building ON the road, dropped: %s %s" % (b["osm_id"], name))
        continue
    gap = min(clearance(q[0], q[1]) for q in fp)
    if gap < 1.0:
        warnings.append("building close to the road edge (%.1f m): %s %s" % (gap, b["osm_id"], name))
    h = b["height_m"] or 8.0
    if kind == "wing":
        h = min(h, 10.0)
    buildings.append({"name": name or ("osm %d" % b["osm_id"]), "kind": kind, "baseY": base_y(cx, cz), "height": h,
                      "pts": [{"x": p[0], "y": 0, "z": p[1], "w": 0, "s": ""} for p in fp]})

# ---------- zones ----------
zones = []
SEA_PROMENADE_ID = None
for p in LAY["plazas"]:
    poly = p["polygon"][:-1] if p["polygon"][0] == p["polygon"][-1] else p["polygon"]
    if p.get("name") == "Sagres Beach Way":
        continue
    cx = sum(q[0] for q in poly) / len(poly)
    cz = sum(q[1] for q in poly) / len(poly)
    if not (-340 <= cx <= 200 and -260 <= cz <= 250):
        continue
    y = base_y(cx, cz)
    # skip flat zones that would float over the ramp/tunnel
    low = any(inside(XZ[i], poly) and route[i]["y"] < y - 0.3 for i in range(N))
    near_low = any(math.dist((q[0], q[1]), XZ[i]) < route[i]["w"] * 0.5 + 3 and route[i]["y"] < y - 0.3
                   for q in poly for i in range(N))
    if low or near_low:
        continue
    surf = p.get("surface") or ""
    kind = "calcada" if surf in ("sett", "paving_stones") or y == BEACH_Y else "concrete"
    if kind == "concrete" and -125 <= cx <= -55 and -60 <= cz <= 35:
        kind = "plaza"  # EDP Plaza: warm ochre concrete in the photos
    zones.append({"kind": kind, "y": y, "pts": [{"x": q[0], "y": 0, "z": q[1], "w": 0, "s": ""} for q in poly]})
for p in LAY["beach"]:
    poly = p["polygon"][:-1] if p["polygon"][0] == p["polygon"][-1] else p["polygon"]
    zones.append({"kind": "sand", "y": BEACH_Y + 0.04, "pts": [{"x": q[0], "y": 0, "z": q[1], "w": 0, "s": ""} for q in poly]})
for p in LAY["parking"]:
    poly = p["polygon"][:-1] if p["polygon"][0] == p["polygon"][-1] else p["polygon"]
    cx = sum(q[0] for q in poly) / len(poly)
    cz = sum(q[1] for q in poly) / len(poly)
    if not (-340 <= cx <= 200 and -260 <= cz <= 250):
        continue
    if poly_hits_road(poly, 0.3):
        continue
    zones.append({"kind": "parking", "y": base_y(cx, cz), "pts": [{"x": q[0], "y": 0, "z": q[1], "w": 0, "s": ""} for q in poly]})
# Sand strip between the promenade and the sea (OSM beach polygons are small): a wide band south of the route.
zones.append({"kind": "sand", "y": BEACH_Y + 0.03, "pts": [
    {"x": x, "y": 0, "z": z, "w": 0, "s": ""} for x, z in [(-340, -132), (-340, SEA_Z - 6), (-100, SEA_Z - 6), (-100, -132)]]})

# ---------- things ----------
things = []


def add(kind, x, z, yaw=0.0, size=1.0, y=None, keep_off=1.5):
    if keep_off is not None and clearance(x, z) < keep_off:
        return False
    things.append({"kind": kind, "x": round(x, 2), "y": round(base_y(x, z) if y is None else y, 2), "z": round(z, 2),
                   "yaw": round(yaw, 1), "size": size})
    return True


def near_any_building(x, z, margin):
    for b in buildings:
        poly = [(p["x"], p["z"]) for p in b["pts"]]
        if inside((x, z), poly):
            return True
        for i in range(len(poly)):
            if dist_seg((x, z), poly[i], poly[(i + 1) % len(poly)]) < margin:
                return True
    return False


# OSM trees: olives on campus, palms along the Marginal, umbrella pines on the sea side.
for t in LAY["trees"]:
    x, z = t
    if not (-340 <= x <= 200 and -260 <= z <= 250):
        continue
    if near_any_building(x, z, 1.0):
        continue
    mz = marginal_z(x)
    if abs(z - mz) < 16:
        kind = "palm"
    elif z < mz:
        kind = "pine"
    else:
        kind = "olive"
    add(kind, x, z, (x * 37) % 360, 1.0 + ((abs(x * 13 + z * 7)) % 30) / 100.0, keep_off=2.5)

# Extra umbrella pines along the promenade (sea side of the road) and palms along the Marginal near the climb.
for i in range(N):
    p = route[i]
    if "promenade" not in p["s"] or i % 4:
        continue
    j = (i + 1) % N
    fx, fz = XZ[j][0] - XZ[i][0], XZ[j][1] - XZ[i][1]
    L = math.hypot(fx, fz) or 1
    rx, rz = fz / L, -fx / L   # right of driving direction
    side = -1 if (rz < 0) == (-1 < 0) else 1
    # sea is south: pick the side with the lower z
    s = 1 if rz < 0 else -1
    add("pine", p["x"] + rx * s * (p["w"] * 0.5 + 6), p["z"] + rz * s * (p["w"] * 0.5 + 6), i * 23 % 360, 1.15, keep_off=3)

# Lamps every ~30 m, alternating sides, not in the tunnel. Bollards along the sea side of the promenade.
for i in range(0, N, 6):
    p = route[i]
    if "tunnel" in p["s"] or "ramp" in p["s"]:
        continue
    j = (i + 1) % N
    fx, fz = XZ[j][0] - XZ[i][0], XZ[j][1] - XZ[i][1]
    L = math.hypot(fx, fz) or 1
    rx, rz = fz / L, -fx / L
    side = 1 if (i // 6) % 2 == 0 else -1
    off = p["w"] * 0.5 + 1.4
    yaw = math.degrees(math.atan2(-rx * side, -rz * side))  # head leans over the road
    add("lamp", p["x"] + rx * side * off, p["z"] + rz * side * off, yaw, 1.0, keep_off=0.8)
for i in range(N):
    p = route[i]
    if "promenade" not in p["s"]:
        continue
    j = (i + 1) % N
    fx, fz = XZ[j][0] - XZ[i][0], XZ[j][1] - XZ[i][1]
    L = math.hypot(fx, fz) or 1
    rx, rz = fz / L, -fx / L
    s = 1 if rz < 0 else -1
    for k in (0.0, 2.5):
        bx = p["x"] + fx / L * k + rx * s * (p["w"] * 0.5 + 0.9)
        bz = p["z"] + fz / L * k + rz * s * (p["w"] * 0.5 + 0.9)
        add("bollard", bx, bz, 0, 1.0, keep_off=0.5)

# No beach parasols: the photos are from November (empty beach).

# Lounge blocks + benches on the plaza north of the Main Building and on Walk of Founders.
for x, z, yaw in [(-66, 222, 20), (-60, 228, 200), (-48, 224, 110), (-80, 218, 20), (-36, 214, 290)]:
    add("lounge", x, z, yaw, 1.0, keep_off=2)
for x, z, yaw in [(-180, 118, 18), (-160, 125, 18), (-150, 144, 18), (-175, 139, 18)]:
    add("bench", x, z, yaw, 1.0, keep_off=1.5)

# Static cars on the Marginal, away from the race crossing at x ~ -212.
for k, x in enumerate([-170, -145, -110, -40, 0, 35, 90, 140, -300, -360]):
    mz = marginal_z(x)
    lane = 4.5 if k % 2 == 0 else -4.5
    add("car", x, mz + lane, 90 if lane > 0 else 270, 1.0, y=CAMPUS_Y, keep_off=3)

# Sao Juliao da Barra fort on the shore to the south.
fort = LAY["fort"]["footprint"]
fx = sum(p[0] for p in fort) / len(fort)
fz = sum(p[1] for p in fort) / len(fort)
add("fort", fx, fz, 35, 1.6, y=BEACH_Y - 0.8, keep_off=None)
# The fort's headland: convex hull of the fort footprint joined to the shore, so the fort stands on land, not in the sea.
def hull(points):
    pts = sorted(set(points))
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for q in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], q) <= 0:
            lower.pop()
        lower.append(q)
    for q in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], q) <= 0:
            upper.pop()
        upper.append(q)
    return lower[:-1] + upper[:-1]
fxs = [p[0] for p in fort]
head = [(p[0] * 1.08 - fx * 0.08, p[1] * 1.08 - fz * 0.08) for p in fort]  # 8 % margin around the walls
head += [(min(fxs) + 30, SEA_Z + 2), (max(fxs) - 30, SEA_Z + 2)]
zones.append({"kind": "headland", "y": BEACH_Y - 0.6, "pts": [{"x": round(q[0], 2), "y": 0, "z": round(q[1], 2), "w": 0, "s": ""} for q in hull(head)]})

data = {
    "attribution": "Map data (c) OpenStreetMap contributors, ODbL 1.0, https://www.openstreetmap.org/copyright",
    "campusY": CAMPUS_Y,
    "beachY": BEACH_Y,
    "seaZ": SEA_Z,
    "marginal": [{"x": x, "y": CAMPUS_Y, "z": z, "w": 18, "s": ""} for x, z in MARGINAL],
    "route": route,
    "checkpoints": checkpoints,
    "spawn": (checkpoints[0] + 1) % N,
    "buildings": buildings,
    "zones": zones,
    "things": things,
}
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, ensure_ascii=False, indent=1)


# ---------- report ----------
def radius(a, b, c):
    ab, bc, ca = math.dist(a, b), math.dist(b, c), math.dist(c, a)
    cross = abs((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]))
    return 1e9 if cross < 1e-6 else ab * bc * ca / (2 * cross)


rmin = min((radius(XZ[i - 1], XZ[i], XZ[(i + 1) % N]), i) for i in range(N))
smax = max((abs(route[(i + 1) % N]["y"] - route[i]["y"]) / max(0.01, math.dist(XZ[i], XZ[(i + 1) % N])) * 100, i)
           for i in range(N))
print("points %d, lap %.0f m, tightest radius %.1f m at %d (%s), steepest %.1f %% at %d (%s)" % (
    N, lap, rmin[0], rmin[1], route[rmin[1]]["s"], smax[0], smax[1], route[smax[1]]["s"]))
print("checkpoints", checkpoints, "spawn", data["spawn"])
print("buildings %d, zones %d, things %d" % (len(buildings), len(zones), len(things)))
from collections import Counter
print(Counter(t["kind"] for t in things))
for w in warnings:
    print("WARN", w)

# ---------- overlay ----------
if len(sys.argv) > 4:
    from PIL import Image, ImageDraw
    XMIN, ZMAX, S, PAD = -330, 230, 2.0, 40

    def P(x, z):
        return (PAD + (x - XMIN) * S, PAD + 70 + (ZMAX - z) * S)

    im = Image.open(sys.argv[3]).convert("RGB")
    dr = ImageDraw.Draw(im, "RGBA")
    for i in range(N):
        a, b = route[i], route[(i + 1) % N]
        col = (40, 70, 200, 120) if "tunnel" in a["s"] else ((150, 60, 190, 120) if "promenade" in a["s"] else (220, 30, 30, 110))
        dr.line([P(a["x"], a["z"]), P(b["x"], b["z"])], fill=col, width=int(a["w"] * S))
    for k, c in enumerate(checkpoints):
        x, y = P(route[c]["x"], route[c]["z"])
        dr.ellipse([x - 9, y - 9, x + 9, y + 9], fill=(255, 255, 255, 240), outline=(0, 0, 0, 255), width=2)
        dr.text((x - 4, y - 6), str(k), fill=(0, 0, 0))
    for t in things:
        x, y = P(t["x"], t["z"])
        col = {"lamp": (250, 200, 0), "bollard": (120, 80, 40), "pine": (20, 90, 20), "palm": (90, 160, 40)}.get(t["kind"], (0, 0, 0))
        dr.ellipse([x - 2, y - 2, x + 2, y + 2], fill=col + (255,))
    im.save(sys.argv[4])
    print("overlay", sys.argv[4])
