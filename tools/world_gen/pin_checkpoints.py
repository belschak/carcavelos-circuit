"""Put the race checkpoints on fixed places (Noah's positions) instead of respreading them along the lap.

checkpoint_anchors.json holds one x/z (Unity axes) per checkpoint. Each checkpoint becomes the index of the route
point nearest to its anchor; the order along the lap must stay the same (checked). Used by export_unity_bundle.py;
run it on its own after any later step that rewrites track.json (for example Noah's hall_route.py):

    python3 pin_checkpoints.py [path/to/track.json]
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ANCHORS = os.path.join(HERE, "checkpoint_anchors.json")


def load_anchors():
    if not os.path.exists(ANCHORS):
        return None
    return [(a["x"], a["z"]) for a in json.load(open(ANCHORS))["checkpoints"]]


def pin(points, anchors, start_index=0):
    """points: list of dicts with x and z (Unity axes). Returns (indices, distances in metres)."""
    n = len(points)
    cps, dist = [], []
    for ax, az in anchors:
        i = min(range(n), key=lambda k: (points[k]["x"] - ax) ** 2 + (points[k]["z"] - az) ** 2)
        cps.append(i)
        dist.append(math.hypot(points[i]["x"] - ax, points[i]["z"] - az))
    order = [(i - start_index) % n for i in cps]
    if order != sorted(order) or len(set(cps)) != len(cps):
        raise RuntimeError(f"pinned checkpoints are out of lap order: {cps}")
    return cps, dist


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        HERE, "..", "..", "Assets", "_Project", "Visuals", "CampusWorld", "Data", "track.json")
    track = json.load(open(path))
    anchors = load_anchors()
    if anchors is None:
        print("no checkpoint_anchors.json: nothing to do")
        return
    cps, dist = pin(track["points"], anchors, track.get("start_index", 0))
    track["checkpoints"] = cps
    json.dump(track, open(path, "w"), indent=0)
    print("checkpoints", cps, "max distance to anchor %.2f m" % max(dist),
          " ".join(f"{d:.1f}" for d in dist))


if __name__ == "__main__":
    main()
