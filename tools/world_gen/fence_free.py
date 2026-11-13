"""Where Noah's white fence (Role 2, #63) must NOT stand in the V14 world, as data (V14 + Nova hall, 2026-11-11).

WhiteGuardrailBuilder puts the fence along both road edges of the whole lap (0.3 m outside the edge, with an invisible
1 m wall behind it), except in the tunnel, the hall and the zone "kicker". V14 has places where that is wrong:

  jump        V14's route has no zone "kicker" any more (the kicker is a ramp on the terrace deck), so the fence would
              hang across the 4.4 m drop behind the terrace lip, and beside the 6 m ramp up to 0.7 m over the deck
  stairs      the terracotta stairs have their own railings (0.15 m inside the stair block's edge) and guard colliders
  plaza       left side only: the plaza deck is the road; on its left are the courtyard's own fence, the way to the
              shortcut's kicker and, at the atrium door, the shortcut's exit. A fence there closes the shortcut.
  ramp        the straight tunnel ramp (V10g): 45 degree walls start at the road edge, railings stand on top of them
              (agreed with Noah, 2026-11-11)
  promenade   sea side only: kerb, cove stair and beach steps; the view of the beach stays open (agreed with Noah)
  median gap  where the line crosses the Avenida Marginal's median (agreed with Noah)

Output: Data/fence_free.json for the Unity build (FenceFreeZones.cs trims the fence instance in the campus world and
closes each cut with an end post; Noah's prefab and builder are untouched). A piece is the fence between route points
k and k + 1; "from"/"to" list the pieces from <= k < to; side +1 = right of the driving direction, -1 = left, 0 = both.
Indices are those of the final track.json: run this after hall_route.py and hall_join.py.

    python3 tools/world_gen/fence_free.py
"""
import json
import math
import os

import coast
import promenade_shortcut_plan
import street_plan

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
TRACK = os.path.join(REPO, "Assets/_Project/Visuals/CampusWorld/Data/track.json")
OUT = os.path.join(REPO, "Assets/_Project/Visuals/CampusWorld/Data/fence_free.json")
EDGE = 0.3                  # WhiteGuardrailBuilder.EdgeOffset
SEA_REACH = 12.0            # m: a promenade edge counts as the sea side when the beach's back edge is this close to it
NO_FENCE = True             # Luc, 12 Nov 2026 (after his V18 drive): no white fence and no wall behind it from the terrace
                            # ramp to the finish; up to the ramp it stays, the hall keeps its own rails. Markers
                            # and run-off limits come next, place by place. False = the zones below.


def zone_runs(pts, zone):
    runs, start = [], None
    for i, p in enumerate(pts + [{"zone": None}]):
        if p["zone"] == zone and start is None:
            start = i
        elif p["zone"] != zone and start is not None:
            runs.append((start, i - 1))
            start = None
    return runs


def right_of(pts, i):
    """Unit vector to the right of the driving direction at point i (WhiteGuardrailBuilder.Forward: over 4 points)."""
    n = len(pts)
    a, b = pts[(i - 2) % n], pts[(i + 2) % n]
    dx, dz = b["x"] - a["x"], b["z"] - a["z"]
    d = math.hypot(dx, dz) or 1.0
    return dz / d, -dx / d


def fence_point(pts, i, side):
    rx, rz = right_of(pts, i)
    off = side * (pts[i]["width"] / 2 + EDGE)
    return pts[i]["x"] + rx * off, pts[i]["z"] + rz * off


def main():
    track = json.load(open(TRACK))
    pts = track["points"]
    n = len(pts)
    free = []

    def add(a, b, side, why):
        free.append({"from": max(0, a), "to": min(n - 1, b), "side": side, "why": why})

    j = track["jump_index"]
    if NO_FENCE:            # Luc: the fence up to the terrace ramp stays (start plaza, atrium, deck), all of it after goes
        free.append({"from": j - 3, "to": n, "side": 0, "why": "Luc 12 Nov: no white fence after the terrace ramp (markers come place by place)"})
    add(j - 3, j + 3, 0, "terrace jump: the kicker ramp (the deck beside it stays 0.65 m lower), the drop behind the lip, the first landing metres")
    for a, b in zone_runs(pts, "stairs"):
        add(a - 1, b + 1, 0, "terracotta stairs: own railings and guard colliders")
    for a, b in zone_runs(pts, "plaza deck"):
        add(a - 1, b + 1, -1, "upper plaza, left side: courtyard fence, the shortcut's kicker and its exit at the atrium door")
    for a, b in zone_runs(pts, "ramp"):
        add(a - 1, b + 1, 0, "straight tunnel ramp: 45 degree walls at the road edge, railings on top (agreed with Noah)")
    # promenade: the side whose fence line runs along the beach (its back edge within SEA_REACH; the zone's west
    # half lies 16 to 33 m inland and keeps its fence)
    for a, b in zone_runs(pts, "promenade"):
        for side in (1, -1):
            sea = [i for i in range(a, b + 1)
                   if coast.backshore_distance(*fence_point(pts, i, side)) < min(SEA_REACH, coast.backshore_distance(*fence_point(pts, i, -side)))]
            start = prev = None
            for i in sea + [None]:
                if start is None:
                    start = i
                elif i is None or i - prev > 6:                   # a break of up to 12 m does not get a fence island
                    add(start, prev + 1, side, "promenade, sea side: kerb, stairs and the open beach (agreed with Noah)")
                    start = i
                prev = i
    gap = [i for i, p in enumerate(pts) if p["zone"] == "coastal road" and street_plan.GAP_X[0] <= p["x"] <= street_plan.GAP_X[1]
           and abs(p["z"] - street_plan.med_pt(p["x"], 0.0)[1]) < 12.0]
    if gap:
        add(min(gap) - 1, max(gap) + 2, 0, "gap in the Avenida Marginal's median (agreed with Noah)")
    # V19: the promenade shortcut leaves and rejoins on the left; every left fence piece within its CLEAR stays out
    lane = promenade_shortcut_plan.centreline()
    for a, b in promenade_shortcut_plan.free_runs(n - 1, lambda k: fence_point(pts, k, -1), lane):
        add(a, b, -1, "V19 promenade shortcut mouth: the straight lane along the parking aisle leaves or joins here")
    out = {"note": "Pieces of the white fence that the campus world leaves out (tools/world_gen/fence_free.py). "
                   "A piece = fence between route points k and k + 1, from <= k < to; side +1 right, -1 left, 0 both.",
           "points": n, "free": free}
    with open(OUT, "w") as fh:
        json.dump(out, fh, indent=1)
    for f in free:
        a, b = pts[f["from"]], pts[f["to"] % n]
        print(f"  {f['from']:4d}-{f['to']:4d} side {f['side']:+d}  ({a['x']:.0f}, {a['z']:.0f}) .. ({b['x']:.0f}, {b['z']:.0f})  {f['why']}")
    print("written:", os.path.relpath(OUT, REPO))


if __name__ == "__main__":
    main()
