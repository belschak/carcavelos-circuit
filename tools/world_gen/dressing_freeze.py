"""Keeps the dressing (plants and props) where it was outside the sections whose route changed (V10i, 2026-11-11).

Why: env_vegetation.place() and env_props.place() walk the lap in order with one random sequence each (every n-th
metre, alternating sides, "is this spot still free"). When a section override makes a stretch shorter, every later
item gets another spot or variant: V10g's tunnel-exit change moved about 75 props and 145 plants elsewhere on the map,
in places Luc had already approved by eye.

How: dressing_frozen.json holds the placements of the last approved state (V10f, commit 3f40c7e, after the street
section's own dressing) and the "refresh zones": a list of polylines with a radius (the old and the new line of each
changed section, the kicker). apply() keeps the frozen item everywhere outside the zones and the freshly computed one
inside them; a fresh item closer than GAP to a frozen one is dropped. Outside the zones the result is identical to the
frozen state to the last digit (check: python3 dressing_freeze.py).

To accept a new overall dressing (after changing placement rules): build once with DRESSING_FREEZE=0, then
python3 dressing_freeze.py --freeze, and commit dressing_frozen.json.
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
FILE = os.path.join(HERE, "dressing_frozen.json")
OUT = os.path.join(HERE, "out")
GAP = 0.7                   # m: a fresh item this close to a frozen one is left out
STAGE = {"plants": None, "props": None}     # V16f: the items that left apply() (keys), for dressing_reality.py


def _seg(x, y, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    l2 = dx * dx + dy * dy
    t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((x - a[0]) * dx + (y - a[1]) * dy) / l2))
    return math.hypot(x - a[0] - t * dx, y - a[1] - t * dy)


def in_zone(x, y, zones):
    for z in zones:
        x0, y0, x1, y1 = z["box"]
        if x0 <= x <= x1 and y0 <= y <= y1 and any(_seg(x, y, a, b) <= z["radius"] for a, b in zip(z["line"], z["line"][1:])):
            return True
    return False


def load():
    if not os.path.exists(FILE) or os.environ.get("DRESSING_FREEZE") == "0":
        return None
    data = json.load(open(FILE))
    for z in data["zones"]:
        xs, ys, r = [p[0] for p in z["line"]], [p[1] for p in z["line"]], z["radius"]
        z["box"] = (min(xs) - r, min(ys) - r, max(xs) + r, max(ys) + r)
    return data


def merge(fresh, frozen, zones):
    keep = [it for it in frozen if not in_zone(it["x"], it["y"], zones)]
    cells = {}
    for it in keep:
        cells.setdefault((int(it["x"] // 4), int(it["y"] // 4)), []).append(it)
    new, dropped = [], 0
    for it in fresh:
        if not in_zone(it["x"], it["y"], zones):
            continue
        cx, cy = int(it["x"] // 4), int(it["y"] // 4)
        near = any(math.hypot(q["x"] - it["x"], q["y"] - it["y"]) < GAP for dx in (-1, 0, 1) for dy in (-1, 0, 1)
                   for q in cells.get((cx + dx, cy + dy), ()) if q["proto"] != "Prop_RailingSegment")
        if near and it["proto"] != "Prop_RailingSegment":
            dropped += 1
            continue
        new.append(it)
    return keep + new, len(keep), len(new), dropped


def apply(veg, props):
    """Returns (veg, props) with the frozen items outside the refresh zones; rewrites the two placement files."""
    data = load()
    if data is None:
        print("  dressing: not frozen (no dressing_frozen.json, or DRESSING_FREEZE=0)")
        _mark(veg, props)
        return veg, props
    out = []
    for name, fresh, fname in (("plants", veg, "placements_vegetation.json"), ("props", props, "placements_props.json")):
        items, kept, new, dropped = merge(fresh, data[name], data["zones"])
        print(f"  dressing: {name}: {kept} kept as frozen ({data['from']}), {new} fresh in the changed sections"
              + (f", {dropped} fresh left out next to a frozen one" if dropped else ""))
        path = os.path.join(OUT, fname)
        if os.path.exists(path):
            doc = json.load(open(path))
            doc["placements"] = items
            counts = {}
            for it in items:
                counts[it["proto"]] = counts.get(it["proto"], 0) + 1
            doc["counts"] = counts
            json.dump(doc, open(path, "w"), indent=1)
        out.append(items)
    _mark(out[0], out[1])
    return out[0], out[1]


def _mark(veg, props):
    """V16f: remembers which items came out of the generic placement / the frozen state. dressing_reality.py removes
    only among these; what the area modules add in their later dress steps is theirs."""
    STAGE["plants"] = {json.dumps(it, sort_keys=True) for it in veg}
    STAGE["props"] = {json.dumps(it, sort_keys=True) for it in props}


def check():
    """Compares out/placements_*.json with the frozen state outside the zones."""
    data = load()
    ok = True
    for name, fname in (("plants", "placements_vegetation.json"), ("props", "placements_props.json")):
        now = json.load(open(os.path.join(OUT, fname)))["placements"]
        key = lambda it: json.dumps(it, sort_keys=True)                    # noqa: E731
        a = {key(it) for it in data[name] if not in_zone(it["x"], it["y"], data["zones"])}
        b = {key(it) for it in now if not in_zone(it["x"], it["y"], data["zones"])}
        inside = sum(1 for it in now if in_zone(it["x"], it["y"], data["zones"]))
        print(f"{name}: {len(now)} now ({inside} in the changed sections); outside them {len(b)} now, {len(a)} frozen, "
              f"{len(a - b)} missing, {len(b - a)} extra")
        ok = ok and a == b
    print("outside the changed sections: identical to the frozen state" if ok else "DIFFERENT outside the changed sections")
    return ok


def freeze(note, zones):
    data = {"note": "Frozen dressing, see dressing_freeze.py. x east, y north, metres.", "from": note, "zones": zones}
    for name, fname in (("plants", "placements_vegetation.json"), ("props", "placements_props.json")):
        data[name] = json.load(open(os.path.join(OUT, fname)))["placements"]
    with open(FILE, "w") as fh:
        fh.write("{\n")
        for k in ("note", "from", "zones"):
            fh.write(f' "{k}": {json.dumps(data[k])},\n')
        for name in ("plants", "props"):
            fh.write(f' "{name}": [\n' + ",\n".join("  " + json.dumps(it) for it in data[name]) + "\n ]" + ("," if name == "plants" else "") + "\n")
        fh.write("}\n")


if __name__ == "__main__":
    if "--freeze" in sys.argv:
        old = json.load(open(FILE)) if os.path.exists(FILE) else {"zones": []}
        freeze(sys.argv[sys.argv.index("--freeze") + 1] if len(sys.argv) > sys.argv.index("--freeze") + 1 else "manual", old["zones"])
    else:
        sys.exit(0 if check() else 1)
