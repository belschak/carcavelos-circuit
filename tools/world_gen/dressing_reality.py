"""Dressing to reality (V16f, 2026-11-11): removes the plants and furniture that do not exist on the real campus and
adds the few that do. Source: photosphere survey V15 (atlas report), sections 1 (points 1, 8, 9), 3.1-3.9, 4 (fixes
6, 14, 24) and the planting / furniture paragraphs of its appendices.

Why a filter and not a new placement: env_vegetation.place() and env_props.place() walk the lap with one random
sequence each, so leaving a tree out there would move every later item (see dressing_freeze.py). Here the generic
placement stays exactly as approved; items are dropped AFTER placement by zone and kind (the way V10d dropped the
dune clumps), and the real ones are added as explicit positions. Every kept item is identical to the one before
(position, rotation, scale): `python3 dressing_reality.py --diff` proves it against out/placements_*_before.json.

Whose items: only the ones of the generic placement, the street section's dressing and the frozen state (everything
that left dressing_freeze.apply(), marked there). What env_campus / env_plaza / env_entrance / the promenade add
afterwards in their own dress steps belongs to those modules and passes through untouched.

    veg, props = dressing_reality.apply(veg, props)       # build_world.build_dressing(), last dress step

x east, y north, metres. Boxes are (x0, y0, x1, y1).
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
ROUTE_CLEAR = 6.6               # m: nothing solid closer to the race centreline (brief V16, hard rule 3)
MARGIN = 0.9                    # m: ... plus the item's own half size (crown, table)

TREES = ("Veg_Olive", "Veg_UmbrellaPine", "Veg_PlazaTree", "Veg_Sapling")
PALMS = ("Veg_Palm",)           # Veg_Palm, Veg_Palm_B (the tall fan and slim palms of the street section stay)
SHRUBS = ("Veg_Shrub",)

# ---------------------------------------------------------------------------------------------------------------
# Plants that do not exist. (zone, area, kinds). area = box, or a polygon (list of points).
# ---------------------------------------------------------------------------------------------------------------
SW_LAWN = [(-82.0, -4.0), (-82.0, -50.0), (-74.0, -58.0), (-62.0, -67.0), (-51.0, -73.0), (-40.0, -77.0),
           (-14.0, -85.0), (3.0, -91.0), (62.0, -91.0), (62.0, -72.0), (40.0, -60.0), (30.0, -40.0), (22.0, -22.0),
           (0.0, -10.0), (-20.0, -4.0)]
PLANT_ZONES = [
    # survey 3.2 + App. B 2.9: "only two" trees on the lawn west of the terrace, no shrubs, no flower beds
    ("west lawn", (-92.0, 14.0, -9.0, 63.0), TREES + SHRUBS),
    # survey 3.3 + App. C 2.1: "exactly one tree on this lawn", no shrubs; App. I 2.6: the bank to the Marginal is
    # smooth mown grass without trees
    ("south-west lawn and its bank", SW_LAWN, TREES + SHRUBS),
    # survey 3.9 + App. I 2.2: the avenue embankment: "no trees, no shrubs on it"
    ("avenue bank", (-95.0, -36.0, -74.0, 97.0), TREES + SHRUBS),
    # survey 3.2 / 3.3: courtyards hold rows of thin staked saplings (placed by the campus modules), not mature trees;
    # the court between D and E is paved, with parasols and cafe tables
    ("north courtyard (B/C)", (-10.0, 63.0, 33.0, 90.0), TREES),
    ("south courtyard (C/D)", (2.0, 29.0, 46.0, 51.0), TREES),
    ("court between D and E", (18.0, -22.0, 66.0, 12.0), TREES),
    # survey 3.9 + App. I 2.6: north side of the Marginal = lawn bank and lamps, no palms; that row also stood on the
    # tunnel's north portal deck (fix 14: "no palms on the deck")
    ("Marginal north side", (-10.0, -94.0, 400.0, -82.0), PALMS),
    ("Marginal north side", (-60.0, -75.0, -50.0, -68.0), PALMS),
    # survey 3.9: "a few small fan palms only behind the shrub belt south of the ring"
    ("roundabout", (-140.0, -80.0, -130.0, -70.0), PALMS),
]
# survey 3.9 + App. I 2.6: "thick Canary palms only at the roadside car park x 60..170 south of the Marginal",
# "about 15 m apart": the row south of the Marginal keeps every palm inside CANARY_X that stands at least CANARY_GAP
# east of the last kept one; the rest of the row goes
CANARY_ROW = (20.0, -123.0, 400.0, -112.0)
CANARY_X = (60.0, 170.0)
CANARY_GAP = 13.0
# Palms that stay but are too tall (Veg_Palm is 10 m at scale 1): the survey gives 5-7 m for the Canary palms at the
# car park and 4-6 m for the palm groups on the Marginal's sea side west of the tunnel. Same place and rotation, new
# scale; the diff lists them as removed + added by this module. (name, box, kinds, factor)
RESIZE = [
    ("resized: Canary palms at the roadside car park (5-7 m)", (60.0, -123.0, 170.0, -112.0), PALMS, 0.65),
    ("resized: palm groups on the Marginal's sea side (4-6 m)", (-80.0, -112.0, -20.0, -86.0), PALMS, 0.5),
]

# ---------------------------------------------------------------------------------------------------------------
# Furniture that does not exist. Kinds everywhere, then (zone, box, kinds).
# ---------------------------------------------------------------------------------------------------------------
PROP_KINDS = {
    "Prop_TroughBloom": "violet flower troughs (design pass 2026-11-09; none on the campus or the promenade)",
    "Prop_LoungeChair": "lounge chairs on the terrace deck (survey 3.2: the terrace is unfurnished)",
    "Prop_PlanterRound": "round cream planters (survey 3.4 / 3.5: no planters; the real pots are dark and conical)",
}
CAFE = ("Prop_BistroTable", "Prop_BistroChair", "Prop_Parasol")
PROP_ZONES = [
    # survey 3.1 gap 5 + fix 22: drop-off forecourt: parked cars, "no cafe tables"
    ("drop-off forecourt", (60.0, 150.0, 104.0, 198.0), CAFE),
    # App. B 2.9: "no lamps on the lawn"; App. C 2.1: no benches, no lamps
    ("west lawn", (-92.0, 14.0, -9.0, 63.0), ("Prop_LampCampus", "Prop_Bin", "Prop_Bench")),
    # fix 24: the lower plaza has the totem and two lamp columns (builder B's objects) where the generic lamp and
    # bollards stood
    ("lower plaza", (95.0, -45.0, 135.0, -8.0), ("Prop_LampCampus", "Prop_Bollard")),
]

# ---------------------------------------------------------------------------------------------------------------
# What is really there. Plants: (proto, x, y, scale, rot_z, what). A position closer than the clearance to the race
# line is moved straight away from it (the route crosses the west lawn where the two real trees stand).
# ---------------------------------------------------------------------------------------------------------------
PLANTS = [
    ("Veg_Olive", -69.0, 52.0, 0.8, 0.6, "west lawn: the old olive (App. B 2.9: (-69, 52) +-1.5, crown 3 m)"),
    ("Veg_PlazaTree", -27.0, 48.5, 0.55, 2.1, "west lawn: the young tree 3 m south of the pad (App. B 2.9)"),
    ("Veg_Olive_B", -60.5, -31.0, 0.62, 4.0, "south-west lawn: the one old olive (App. C 2.1)"),
    # a few small fan palms behind the shrub belt south of the ring (App. I 2.3: fan palms 4-6 m behind the belt)
    ("Veg_FanPalm", -119.0, -97.5, 0.45, 0.3, "ring: small fan palm behind the shrub belt"),
    ("Veg_FanPalm_B", -108.0, -99.5, 0.5, 1.9, "ring: small fan palm behind the shrub belt"),
    ("Veg_FanPalm", -98.5, -98.0, 0.42, 3.3, "ring: small fan palm behind the shrub belt"),
    # Marginal sea side between the ring and the hedge: clipped ball shrubs 1.5-2 m between the palm groups that
    # already stand there (App. C 2.5)
    ("Veg_Shrub", -84.5, -87.6, 1.3, 0.0, "Marginal sea side: clipped ball shrub"),
    ("Veg_Shrub_B", -72.0, -92.0, 1.35, 1.0, "Marginal sea side: clipped ball shrub"),
    ("Veg_Shrub", -59.6, -97.3, 1.25, 2.0, "Marginal sea side: clipped ball shrub"),
    ("Veg_Shrub_B", -46.2, -102.6, 1.3, 3.0, "Marginal sea side: clipped ball shrub"),
]
# Oleander hedge 2.5-3 m on the Marginal's south side at x -40..10 (App. I 2.6): big shrubs shoulder to shoulder on a
# line HEDGE_OFFSET m south of the eastbound roadway's centreline (OSM w19796755, behind the sidewalk)
HEDGE_LINE = [(-51.0, -93.0), (-39.0, -97.0), (-25.0, -101.0), (-14.0, -103.0), (2.0, -105.0), (35.0, -107.0)]
HEDGE_X = (-40.0, 10.0)
HEDGE_OFFSET = 8.6
HEDGE_STEP = 1.7
HEDGE_SCALE = (1.7, 2.1)                 # Veg_Shrub is 1.1 m high at scale 1: a hedge of about 2-2.3 m (real 2.5-3 m; bigger
                                         # copies of the low-poly shrub show their facets from the promenade)

# Props: (proto, x, y, rot_z or None, what). z = the surface under them.
POT = "Prop_PotOlive"
PROPS = [
    # survey 3.4 + App. D 2.5: a row of four thin olives in black conical pots at (65..70, 93..97) and one more at
    # the passage's south mouth (84, 62). The race line passes 2-6 m from those spots, so the row stands 4-5 m
    # further west, parallel to the line, and the single pot 4 m further west (all >= 7.5 m from the centreline)
    # V20b: the shortcut's landing lane runs here now (plaza_plan "V20b"), so the row stands along the hall, 5.6 m left
    # of the shortcut's axis, north of the light well (it was 2.5 to 4.4 m left of it, in the lane)
    (POT, 59.82, 98.92, None, "upper plaza: row of four potted olives"),
    (POT, 60.43, 97.23, None, "upper plaza: row of four potted olives"),
    (POT, 61.04, 95.53, None, "upper plaza: row of four potted olives"),
    (POT, 61.64, 93.84, None, "upper plaza: row of four potted olives"),
    (POT, 74.95, 60.15, None, "upper plaza: potted olive at the passage's south mouth"),   # V18i: (80.0, 59.6) stood ON the shortcut's kicker
    # survey 3.5 + App. E 2.5 / 2.6: dark conical pots in twos and threes at the stair top ((104, 8), (110, 5)) and
    # beside G's corner on the upper plaza. The stair top itself is the road here (the stairs are driven over their
    # full width), so one group of three stands beside it at the foot of G's west wall
    (POT, 108.5, 13.7, None, "stair top / G's corner on the upper plaza: three pots"),
    (POT, 107.8, 15.5, None, "stair top / G's corner on the upper plaza: three pots"),
    (POT, 107.2, 14.2, None, "stair top / G's corner on the upper plaza: three pots"),
    # fix 24 + App. E 2.1 / App. F 2.2: lower plaza: timber picnic tables scattered near the ramp and near G; black
    # tapered planters with the cafe set at G's corner (the cafe set itself is the generic placement's, kept)
    ("Prop_PicnicTable", 121.6, -23.6, 0.5, "lower plaza: picnic table"),
    ("Prop_PicnicTable", 127.0, -17.0, 2.0, "lower plaza: picnic table"),
    ("Prop_PicnicTable", 131.5, -11.5, 1.1, "lower plaza: picnic table"),
    ("Prop_PicnicTable", 139.5, -13.5, 2.7, "lower plaza: picnic table"),
    ("Prop_PicnicTable", 125.5, -28.5, 1.7, "lower plaza: picnic table"),
    ("Prop_PlanterTapered", 120.3, -11.2, None, "lower plaza: tapered planter at G's corner"),
    ("Prop_PlanterTapered", 125.4, -10.4, None, "lower plaza: tapered planter at G's corner"),
    ("Prop_PlanterTapered", 124.9, -16.6, None, "lower plaza: tapered planter at G's corner"),
]
# survey 3.3: court between D and E: pale paving, parasols, cafe tables. (centre x, y, turn of the chair ring)
COURT_CAFE = [(29.5, -9.5, 0.4), (37.5, -5.0, 1.7), (46.0, -1.5, 2.9)]

REPORT = {}


def _inside(x, y, area):
    if isinstance(area, tuple):
        return area[0] <= x <= area[2] and area[1] <= y <= area[3]
    hit = False
    for (ax, ay), (bx, by) in zip(area, area[1:] + area[:1]):
        if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
            hit = not hit
    return hit


def plant_zone(p, canary_keep):
    """Name of the zone that removes plant p, or None."""
    proto, x, y = p["proto"], p["x"], p["y"]
    for name, area, kinds in PLANT_ZONES:
        if proto.startswith(kinds) and _inside(x, y, area):
            return name
    if proto.startswith(PALMS) and _inside(x, y, CANARY_ROW) and (x, y) not in canary_keep:
        return "Marginal south side (only the car park's Canary palms stay)"
    return None


def canary_keep(plants):
    """The palms of the row south of the Marginal that stay: inside CANARY_X, at least CANARY_GAP apart."""
    keep, last = set(), -1e9
    for p in sorted((p for p in plants if p["proto"].startswith(PALMS) and _inside(p["x"], p["y"], CANARY_ROW)),
                    key=lambda p: p["x"]):
        if CANARY_X[0] <= p["x"] <= CANARY_X[1] and p["x"] - last >= CANARY_GAP:
            keep.add((p["x"], p["y"]))
            last = p["x"]
    return keep


def prop_zone(p):
    proto, x, y = p["proto"], p["x"], p["y"]
    if proto in PROP_KINDS:
        return "everywhere: " + proto
    for name, area, kinds in PROP_ZONES:
        if proto in kinds and _inside(x, y, area):
            return name
    return None


def _key(p):
    return json.dumps(p, sort_keys=True)


def _hedge():
    """Positions of the oleander hedge: every HEDGE_STEP m along HEDGE_LINE, HEDGE_OFFSET m to the south."""
    out, carry = [], 0.0
    for a, b in zip(HEDGE_LINE, HEDGE_LINE[1:]):
        d = math.dist(a, b)
        ux, uy = (b[0] - a[0]) / d, (b[1] - a[1]) / d
        s = carry
        while s < d:
            x, y = a[0] + ux * s + uy * HEDGE_OFFSET, a[1] + uy * s - ux * HEDGE_OFFSET
            if HEDGE_X[0] <= x <= HEDGE_X[1]:
                out.append((x, y))
            s += HEDGE_STEP
        carry = s - d
    return out


def apply(veg, props, stage=None):
    """Returns (veg, props) without the items that do not exist and with the real ones; rewrites the two placement
    files. stage: {"plants": keys, "props": keys} of the items the filter may touch (default: what left
    dressing_freeze.apply(); all items if that did not run)."""
    import env_campus
    import env_common as E
    import env_vegetation
    import dressing_freeze
    stage = stage or dressing_freeze.STAGE
    mine_v = (lambda p: _key(p) in stage["plants"]) if stage.get("plants") is not None else (lambda p: True)
    mine_p = (lambda p: _key(p) in stage["props"]) if stage.get("props") is not None else (lambda p: True)
    gone, veg2, props2 = {}, [], []
    keep = canary_keep([p for p in veg if mine_v(p)])
    for p in veg:
        zone = plant_zone(p, keep) if mine_v(p) else None
        if zone:
            gone.setdefault(zone, {}).setdefault(p["proto"], []).append(p)
        else:
            veg2.append(p)
    resized = []
    for name, area, kinds, factor in RESIZE:
        for p in [p for p in veg2 if mine_v(p) and p["proto"].startswith(kinds) and _inside(p["x"], p["y"], area)]:
            veg2.remove(p)
            gone.setdefault(name, {}).setdefault(p["proto"], []).append(p)
            resized.append(dict(p, scale=round(p["scale"] * factor, 3)))
    for p in props:
        zone = prop_zone(p) if mine_p(p) else None
        if zone:
            gone.setdefault(zone, {}).setdefault(p["proto"], []).append(p)
        else:
            props2.append(p)

    notes, new_v, new_p = [], list(resized), []

    def clear_spot(x, y, need):
        """(x, y) moved straight away from the race line until it is `need` m from it."""
        gap = env_campus.route_gap(x, y)
        if gap >= need:
            return x, y, gap, 0.0
        q = min(E.route_points(), key=lambda r: (r["x"] - x) ** 2 + (r["y"] - y) ** 2)
        d = math.hypot(x - q["x"], y - q["y"]) or 1.0
        ux, uy = (x - q["x"]) / d, (y - q["y"]) / d
        x0, y0 = x, y
        for _ in range(200):
            x, y = x + ux * 0.1, y + uy * 0.1
            if env_campus.route_gap(x, y) >= need:
                break
        return x, y, env_campus.route_gap(x, y), math.hypot(x - x0, y - y0)

    def near(x, y, items, r):
        return any(math.hypot(q["x"] - x, q["y"] - y) < r for q in items)

    for proto, x, y, scale, rot, what in PLANTS:
        solid = not proto.startswith(SHRUBS)                       # shrubs have no collider (the kart drives through)
        x, y, gap, moved = clear_spot(x, y, ROUTE_CLEAR + (1.6 if solid else 1.0))
        if solid and any(q["proto"].startswith(TREES) and math.hypot(q["x"] - x, q["y"] - y) < 8.0 for q in veg2):
            notes.append((what + ": ALREADY THERE (a tree of another module stands within 8 m)", proto, x, y, gap, moved))
            continue
        new_v.append({"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(env_vegetation._ground_z(x, y), 3),
                      "rot_z": rot, "scale": scale})
        notes.append((what, proto, x, y, gap, moved))
    hedge = 0
    for k, (x, y) in enumerate(_hedge()):
        if env_campus.route_gap(x, y) < ROUTE_CLEAR + 1.5 or near(x, y, veg2 + resized, 1.6):
            continue
        scale = HEDGE_SCALE[0] + (HEDGE_SCALE[1] - HEDGE_SCALE[0]) * ((k * 37) % 10) / 9.0
        new_v.append({"proto": "Veg_Shrub" if k % 2 else "Veg_Shrub_B", "x": round(x, 3), "y": round(y, 3),
                      "z": round(env_vegetation._ground_z(x, y) - 0.15, 3), "rot_z": round((k * 2.4) % 6.283, 4),
                      "scale": round(scale, 3)})
        hedge += 1

    def prop(proto, x, y, rot, what, k):
        gap = env_campus.route_gap(x, y)
        if gap < ROUTE_CLEAR + MARGIN:
            notes.append((what + ": LEFT OUT (too close to the race line)", proto, x, y, gap, 0.0))
            return False
        if near(x, y, props2 + new_p, 0.8):
            notes.append((what + ": LEFT OUT (another prop stands there)", proto, x, y, gap, 0.0))
            return False
        new_p.append({"proto": proto, "x": round(x, 3), "y": round(y, 3), "z": round(E.ground_z(x, y) + 0.02, 3),
                      "rot_z": round((k * 1.9) % 6.283 if rot is None else rot, 4), "scale": 1.0})
        notes.append((what, proto, x, y, gap, 0.0))
        return True

    for k, (proto, x, y, rot, what) in enumerate(PROPS):
        prop(proto, x, y, rot, what, k)
    for k, (gx, gy, turn) in enumerate(COURT_CAFE):
        if prop("Prop_BistroTable", gx, gy, 0.0, "court between D and E: cafe set", k):
            new_p.append(dict(new_p[-1], proto="Prop_Parasol"))
            for q in range(3):
                a = turn + 2 * math.pi * q / 3
                px, py = gx + math.cos(a) * 0.64, gy + math.sin(a) * 0.64
                new_p.append(dict(new_p[-2], proto="Prop_BistroChair", x=round(px, 3), y=round(py, 3),
                                  rot_z=round(math.atan2(gx - px, -(gy - py)), 4)))

    print("  dressing to reality (V16f): removed")
    for zone in sorted(gone):
        kinds = ", ".join(f"{len(v)} {k}" for k, v in sorted(gone[zone].items()))
        print(f"    {zone:<52s} {kinds}")
    print(f"  dressing to reality: added {len(new_v)} plants ({hedge} of them the oleander hedge, {len(resized)} resized "
          f"palms) and {len(new_p)} props")
    for what, proto, x, y, gap, moved in notes:
        print(f"    {proto:<20s} ({x:7.1f}, {y:7.1f})  {gap:5.1f} m from the race line"
              + (f", moved {moved:.1f} m away from it" if moved else "") + f"  {what}")
    REPORT.update(gone=gone, plants=new_v, props=new_p, notes=notes)
    veg2, props2 = veg2 + new_v, props2 + new_p
    for fname, items in (("placements_vegetation.json", veg2), ("placements_props.json", props2)):
        path = os.path.join(OUT, fname)
        if os.path.exists(path):
            data = json.load(open(path))
            data["placements"] = items
            counts = {}
            for it in items:
                counts[it["proto"]] = counts.get(it["proto"], 0) + 1
            data["counts"] = counts
            json.dump(data, open(path, "w"), indent=1)
    json.dump({"removed": {z: {k: [[p["x"], p["y"]] for p in v] for k, v in kinds.items()} for z, kinds in gone.items()},
               "added_plants": new_v, "added_props": new_p}, open(os.path.join(OUT, "dressing_reality.json"), "w"), indent=1)
    return veg2, props2


def diff(before_tag="_before"):
    """Proof that nothing else moved: compares out/placements_*.json with out/placements_*<before_tag>.json.
    Every item of the new files is either identical to an item before (all fields) or listed as added here;
    every item before is either still there or listed as removed here."""
    rep = json.load(open(os.path.join(OUT, "dressing_reality.json")))
    removed = {(k, x, y) for kinds in rep["removed"].values() for k, v in kinds.items() for x, y in v}
    added = {_key(p) for p in rep["added_plants"] + rep["added_props"]}
    ok = True
    for fname in ("placements_vegetation.json", "placements_props.json"):
        now = [_key(p) for p in json.load(open(os.path.join(OUT, fname)))["placements"]]
        old = [_key(p) for p in json.load(open(os.path.join(OUT, fname.replace(".json", before_tag + ".json"))))["placements"]]
        now_s, old_s = set(now), set(old)
        lost = [json.loads(k) for k in old_s - now_s]
        new = [k for k in now_s - old_s]
        lost_other = [p for p in lost if (p["proto"], p["x"], p["y"]) not in removed]
        new_other = [k for k in new if k not in added]
        print(f"{fname}: {len(old)} before, {len(now)} now; {len(old_s & now_s)} identical (position, height, rotation, "
              f"scale), {len(lost)} removed ({len(lost_other)} not by this filter), {len(new)} new "
              f"({len(new_other)} not from this module)")
        for p in lost_other[:12]:
            print("   lost elsewhere:", p)
        for k in new_other[:12]:
            print("   new elsewhere:", k)
        ok = ok and not lost_other and not new_other
    print("every kept item is identical; all changes are this module's" if ok else "DIFFERENCES outside this module")
    return ok


if __name__ == "__main__":
    sys.exit(0 if diff(sys.argv[sys.argv.index("--diff") + 1] if "--diff" in sys.argv and len(sys.argv) > sys.argv.index("--diff") + 1 else "_before") else 1)
