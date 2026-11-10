"""Turns OpenStreetMap + elevation tiles into one world_data.json for the Blender world builder.

Run with the media-tools Python:  ~/Documents/nova/.media-tools/bin/python world_data.py
Everything is in local metres: x = east, y = north, z = up, origin = WORLD_ORIGIN below.
Building heights come from buildings.csv. Edit that file (levels or height_m) and run again:
your edits are kept, new buildings get a default row.
"""
import csv
import json
import math
import os


HERE = os.path.dirname(os.path.abspath(__file__))
# OpenStreetMap download (Overpass, 55 MB, not in the repo): set CAMPUS_OSM or put it in osm/campus_raw.json
OSM_FILE = os.environ.get("CAMPUS_OSM") or next((p for p in (os.path.join(HERE, "osm", "campus_raw.json"),
                                                    os.path.expanduser("~/Documents/nova/campus-media/osm/campus_raw.json"))
                                               if os.path.exists(p)), os.path.join(HERE, "osm", "campus_raw.json"))
DEM_DIR = os.path.join(HERE, "dem")
OUT_FILE = os.path.join(HERE, "world_data.json")
CSV_FILE = os.path.join(HERE, "buildings.csv")
OVERRIDES_FILE = os.path.join(HERE, "footprint_overrides.json")   # hand corrections of OSM footprints

WORLD_ORIGIN = (38.6780, -9.3265)          # lat, lon of (0, 0). Never change it later: everything is placed from here.
BOUNDS = (-420.0, -270.0, 280.0, 240.0)    # min x, min y, max x, max y in metres (campus + tunnel + promenade + beach edge)
GRID_STEP = 5.0                            # metres between ground vertices
LEVEL_HEIGHT = 3.5                         # metres per storey
DEM_ZOOM = 15

PATH_WIDTH = {                             # metres, by OSM highway type
    "primary": 11, "primary_link": 6, "secondary": 9, "tertiary": 8, "residential": 6,
    "unclassified": 6, "service": 4.5, "pedestrian": 5, "footway": 2.5, "path": 2,
    "cycleway": 2.5, "steps": 2.5, "living_street": 5,
}
AREA_KIND = {                              # OSM tag -> ground class (painted onto the ground mesh)
    ("natural", "beach"): "sand", ("natural", "sand"): "sand",
    ("landuse", "grass"): "grass", ("leisure", "garden"): "grass", ("leisure", "park"): "grass",
    ("natural", "grassland"): "grass", ("natural", "scrub"): "grass", ("natural", "wood"): "wood",
    ("amenity", "parking"): "paved", ("leisure", "pitch"): "pitch",
    ("leisure", "swimming_pool"): "water", ("natural", "water"): "water",
}
AREA_ORDER = ["grass", "wood", "paved", "pitch", "sand", "water"]   # later classes win where areas overlap


def to_local(lat, lon):
    """Latitude/longitude -> metres east/north of WORLD_ORIGIN (good enough for 1 km)."""
    lat0, lon0 = WORLD_ORIGIN
    return ((lon - lon0) * 111320.0 * math.cos(math.radians(lat0)), (lat - lat0) * 110540.0)


def to_latlon(x, y):
    lat0, lon0 = WORLD_ORIGIN
    return (lat0 + y / 110540.0, lon0 + x / (111320.0 * math.cos(math.radians(lat0))))


# ---------- elevation ----------
_tiles = {}


def dem_height(lat, lon):
    """Height in metres from the Terrarium tiles (bilinear)."""
    from PIL import Image          # only needed here (Blender's Python can import this file without Pillow)
    n = 2 ** DEM_ZOOM
    fx = (lon + 180.0) / 360.0 * n * 256
    fy = (1 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2 * n * 256

    def px(ix, iy):
        tx, ty = ix // 256, iy // 256
        key = (tx, ty)
        if key not in _tiles:
            _tiles[key] = Image.open(os.path.join(DEM_DIR, f"{DEM_ZOOM}_{tx}_{ty}.png")).convert("RGB").load()
        r, g, b = _tiles[key][ix % 256, iy % 256]
        return r * 256 + g + b / 256 - 32768

    ix, iy = int(fx), int(fy)
    ax, ay = fx - ix, fy - iy
    top = px(ix, iy) * (1 - ax) + px(ix + 1, iy) * ax
    bottom = px(ix, iy + 1) * (1 - ax) + px(ix + 1, iy + 1) * ax
    return top * (1 - ay) + bottom * ay


def smooth(grid, passes):
    """Box blur, because the source data is about 30 m and shows steps."""
    rows, cols = len(grid), len(grid[0])
    for _ in range(passes):
        new = [row[:] for row in grid]
        for j in range(rows):
            for i in range(cols):
                total, count = 0.0, 0
                for dj in (-1, 0, 1):
                    for di in (-1, 0, 1):
                        if 0 <= j + dj < rows and 0 <= i + di < cols:
                            total += grid[j + dj][i + di]
                            count += 1
                new[j][i] = total / count
        grid = new
    return grid


# ---------- OSM ----------
def load_osm():
    elements = json.load(open(OSM_FILE))["elements"]
    nodes = {e["id"]: (e["lat"], e["lon"]) for e in elements if e["type"] == "node" and "lat" in e}
    # The download lists some ways twice (once with tags, once as bare geometry): keep the tagged copy.
    ways = {}
    for e in elements:
        if e["type"] == "way" and (e["id"] not in ways or "tags" in e):
            ways[e["id"]] = e
    relations = {}
    for e in elements:
        if e["type"] == "relation" and (e["id"] not in relations or "tags" in e):
            relations[e["id"]] = e
    elements = [e for e in elements if e["type"] == "node"] + list(ways.values()) + list(relations.values())
    return elements, nodes, ways


def way_points(way, nodes):
    return [to_local(*nodes[n]) for n in way.get("nodes", []) if n in nodes]


def inside_bounds(points, margin=0.0):
    x0, y0, x1, y1 = BOUNDS
    return any(x0 - margin <= x <= x1 + margin and y0 - margin <= y <= y1 + margin for x, y in points)


def polygon_area(points):
    area = 0.0
    for (xa, ya), (xb, yb) in zip(points, points[1:] + points[:1]):
        area += xa * yb - xb * ya
    return area / 2.0


def outer_rings(relation, ways, nodes):
    """Outer rings of a multipolygon relation (joins split outer ways end to end)."""
    parts = [ways[m["ref"]]["nodes"] for m in relation.get("members", [])
             if m["type"] == "way" and m.get("role") == "outer" and m["ref"] in ways]
    rings = []
    while parts:
        ring = list(parts.pop(0))
        changed = True
        while ring[0] != ring[-1] and changed:
            changed = False
            for k, p in enumerate(parts):
                if p[0] == ring[-1]:
                    ring += p[1:]
                elif p[-1] == ring[-1]:
                    ring += p[::-1][1:]
                else:
                    continue
                parts.pop(k)
                changed = True
                break
        if ring[0] == ring[-1]:
            rings.append([to_local(*nodes[n]) for n in ring if n in nodes])
    return rings


def default_levels(tags, on_campus):
    if "building:levels" in tags:
        try:
            return float(tags["building:levels"]), "osm"
        except ValueError:
            pass
    if tags.get("building") in ("roof", "storage_tank", "garage", "shed", "kiosk"):
        return 1.0, "default-small"
    if on_campus:
        return 3.0, "default-campus"
    if tags.get("building") in ("house", "detached"):
        return 2.0, "default"
    return 3.0, "default"


def main():
    from matplotlib.path import Path   # only needed here
    elements, nodes, ways = load_osm()

    # ground grid
    x0, y0, x1, y1 = BOUNDS
    cols = int((x1 - x0) / GRID_STEP) + 1
    rows = int((y1 - y0) / GRID_STEP) + 1
    heights = [[dem_height(*to_latlon(x0 + i * GRID_STEP, y0 + j * GRID_STEP)) for i in range(cols)] for j in range(rows)]
    heights = smooth(heights, 2)
    heights = [[max(h, -1.0) for h in row] for row in heights]

    # campus outline (decides which buildings count as campus)
    campus = next(w for w in ways.values() if w.get("tags", {}).get("amenity") == "university")
    campus_path = Path(way_points(campus, nodes))

    # buildings (ways + multipolygon relations)
    footprints = []
    for e in elements:
        tags = e.get("tags", {})
        if "building" not in tags or e["type"] == "node":
            continue
        rings = [way_points(e, nodes)] if e["type"] == "way" else outer_rings(e, ways, nodes)
        for k, ring in enumerate(rings):
            if len(ring) > 1 and ring[0] == ring[-1]:
                ring = ring[:-1]
            if len(ring) < 3 or not inside_bounds(ring) or abs(polygon_area(ring)) < 4:
                continue
            if polygon_area(ring) < 0:
                ring = ring[::-1]                      # counter-clockwise, so faces point up
            cx = sum(p[0] for p in ring) / len(ring)
            cy = sum(p[1] for p in ring) / len(ring)
            bid = f"{e['type'][0]}{e['id']}" + (f"_{k}" if k else "")
            footprints.append({"id": bid, "name": tags.get("name", ""), "osm_building": tags["building"],
                               "campus": bool(campus_path.contains_point((cx, cy))), "points": ring, "tags": tags})

    # hand corrections of footprints (footprint_overrides.json): replace the polygon, keep id and tags
    overrides = json.load(open(OVERRIDES_FILE)) if os.path.exists(OVERRIDES_FILE) else {}
    for f in footprints:
        o = overrides.get(f["id"])
        if o and o.get("polygon"):
            ring = [tuple(p) for p in o["polygon"]]
            f["points"] = ring if polygon_area(ring) > 0 else ring[::-1]
            f["footprint_override"] = o.get("why", "")
            print(f"footprint override: {f['id']} {f['name']}")

    # buildings.csv: keep the human edits, add new buildings with defaults
    rows_csv = {}
    if os.path.exists(CSV_FILE):
        for r in csv.DictReader(open(CSV_FILE)):
            rows_csv[r["id"]] = r
    for f in footprints:
        if f["id"] not in rows_csv:
            levels, source = default_levels(f["tags"], f["campus"])
            rows_csv[f["id"]] = {"id": f["id"], "name": f["name"], "campus": "yes" if f["campus"] else "no",
                                 "osm_building": f["osm_building"], "levels": levels, "height_m": "",
                                 "source": source, "note": "", "drive_through": "", "roof_z": "", "drive_on_roof": ""}
    with open(CSV_FILE, "w", newline="") as fh:
        fields = ["id", "name", "campus", "osm_building", "levels", "height_m", "roof_z", "source", "drive_through", "drive_on_roof", "note"]
        writer = csv.DictWriter(fh, fieldnames=fields)
        writer.writeheader()
        for r in sorted(rows_csv.values(), key=lambda r: (r["campus"] != "yes", r["name"] == "", r["id"])):
            writer.writerow({k: r.get(k, "") for k in fields})
    for f in footprints:
        r = rows_csv[f["id"]]
        f["height"] = float(r["height_m"]) if r["height_m"] else float(r["levels"]) * LEVEL_HEIGHT
        f["drive_through"] = r.get("drive_through", "") == "yes"     # route passes through: cut a passage
        f["roof_z"] = float(r["roof_z"]) if r.get("roof_z") else None   # absolute roof height (m), wins over height
        f["drive_on_roof"] = r.get("drive_on_roof", "") == "yes"     # route drives on this roof (terrace deck)
        del f["tags"]

    # ground classes, painted per ground vertex
    class_paths = {c: [] for c in AREA_ORDER}
    areas = []                                  # the area polygons themselves, for the ground colour map
    for e in elements:
        tags = e.get("tags", {})
        kind = next((AREA_KIND[(k, v)] for k, v in tags.items() if (k, v) in AREA_KIND), None)
        if not kind or e["type"] == "node":
            continue
        rings = [way_points(e, nodes)] if e["type"] == "way" else outer_rings(e, ways, nodes)
        for ring in rings:
            if len(ring) >= 3 and inside_bounds(ring, 50):
                class_paths[kind].append(Path(ring))
                areas.append({"kind": kind, "ring": [[round(x, 2), round(y, 2)] for x, y in ring]})
    ground_class = []
    for j in range(rows):
        row = []
        for i in range(cols):
            p = (x0 + i * GRID_STEP, y0 + j * GRID_STEP)
            c = "ground"
            for kind in AREA_ORDER:
                if any(path.contains_point(p) for path in class_paths[kind]):
                    c = kind
            if c == "ground" and heights[j][i] < 0.6:
                c = "sea"
            row.append(c)
        ground_class.append(row)

    # paths and roads
    paths = []
    for w in ways.values():
        tags = w.get("tags", {})
        hw = tags.get("highway")
        if hw not in PATH_WIDTH:
            continue
        pts = way_points(w, nodes)
        if len(pts) < 2 or not inside_bounds(pts, 20):
            continue
        paths.append({"id": f"w{w['id']}", "type": hw, "name": tags.get("name", ""), "width": PATH_WIDTH[hw],
                      "tunnel": tags.get("tunnel") in ("yes", "building_passage"), "bridge": "bridge" in tags,
                      "layer": int(tags.get("layer", 0)) if tags.get("layer", "0").lstrip("-").isdigit() else 0,
                      "points": pts})

    # trees and coastline
    trees = [to_local(e["lat"], e["lon"]) for e in elements
             if e["type"] == "node" and e.get("tags", {}).get("natural") == "tree"]
    trees = [p for p in trees if inside_bounds([p])]
    coast = [way_points(w, nodes) for w in ways.values() if w.get("tags", {}).get("natural") == "coastline"]
    coast = [c for c in coast if inside_bounds(c, 50)]

    data = {"origin_latlon": WORLD_ORIGIN, "bounds": BOUNDS, "grid": {"step": GRID_STEP, "cols": cols, "rows": rows,
            "heights": heights, "classes": ground_class}, "buildings": footprints, "paths": paths,
            "trees": trees, "coast": coast, "areas": areas,
            "credit": "Map data (c) OpenStreetMap contributors (ODbL). Elevation: AWS Terrain Tiles (Terrarium)."}
    json.dump(data, open(OUT_FILE, "w"))
    campus_n = sum(1 for f in footprints if f["campus"])
    print(f"grid {cols}x{rows}, height {min(map(min, heights)):.1f}..{max(map(max, heights)):.1f} m")
    print(f"buildings {len(footprints)} (campus {campus_n}), paths {len(paths)} "
          f"(tunnels {sum(p['tunnel'] for p in paths)}), trees {len(trees)}, coast lines {len(coast)}")
    counts = {}
    for row in ground_class:
        for c in row:
            counts[c] = counts.get(c, 0) + 1
    print("ground classes", counts)


if __name__ == "__main__":
    main()
