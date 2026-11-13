"""V18m: AI-made props replace the generator's simple prototypes (Luc, 13 Nov 2026).

Luc generated the models in Tripo Studio from our prompts (vault note "Nova Kart Asset-Prompts"); they arrive with
about 1.9 million triangles. The atlas script `scripts/ai_assets/ai_bake.py` turns each into a game version: a
voxel-remeshed closed shell, decimated, fresh UVs, colour and fine detail baked from the original into a 1024 px
albedo and normal map. `ai_prepare.py` / `ai_prepare_tex.py` write the compact copies kept here in ai_assets/
(mesh-only GLB, albedo JPEG, normal PNG, tinted albedo variants). No lettering, logos or plates on any of them.

The prototypes keep their names, so every placement (car parks, terraces, beach) stays as it was and Unity
instances them as before. Each prototype gets one material with its two textures (WorldLit, like the walls).
Tripo models look along -y; the prototypes look along +x (env_street_objects.car: "nose along +x").
"""
import math
import os
import shutil

import bpy
import bmesh
from mathutils import Matrix, Vector

import env_common as E

HERE = os.path.dirname(os.path.abspath(__file__))
AI = os.path.join(HERE, "ai_assets")
TEX = os.path.join(HERE, "textures")
TURN = Matrix.Rotation(math.radians(90.0), 4, "Z")          # Tripo nose -y -> prototype nose +x

# prototype: (model, albedo variant, how to size it). Sizes: ("width", m) across the nose axis, ("height", m),
# ("span", m) = the larger horizontal side. The models keep their own (toy-like, chunky) proportions; the cars are a
# little narrower than real ones, because the angled bays stand 2.15 m apart (chunky bodies touched there).
SWAPS = {
    "Prop_CarA": ("C1_hatchback", "silver", ("width", 1.60)),
    "Prop_CarB": ("C2_estate", "", ("width", 1.68)),
    "Prop_CarC": ("C3_suv", "", ("width", 1.70)),
    "Prop_CarD": ("C5_microcar", "", ("height", 1.75)),
    "Prop_CarE": ("C1_hatchback", "red", ("width", 1.60)),
    "Prop_CarF": ("C6_panelvan", "", ("width", 1.74)),
    "Prop_CarG": ("C4_camper", "", ("width", 1.72)),
    "Prop_CarH": ("C1_hatchback", "", ("width", 1.60)),
    "Prop_Parasol": ("B1_parasol", "", ("span", 2.6)),
    "Prop_SunLounger": ("B2_lounger", "", ("span", 2.0)),
    "Prop_LifeguardTower": ("B3_lifeguard", "", ("height", 5.2)),
}
# triangles of the near version (<= NEAR_M from the route) and of the far one (same textures, decimated further).
# 238 cars stand in the car parks: at 6,000 triangles each they alone would have quadrupled the world.
NEAR_M = 30.0
NEAR_TRIS = {"Prop_Car": 4000, "Prop_Parasol": 2000, "Prop_SunLounger": 1800, "Prop_LifeguardTower": 6000, "Prop_BikeRack": 9000}
FAR_TRIS = {"Prop_Car": 650, "Prop_Parasol": 400, "Prop_SunLounger": 300, "Prop_LifeguardTower": 1500, "Prop_BikeRack": 1500}
BIKES = (("", -0.78), ("teal", 0.0), ("orange", 0.78))
# New prototypes (Luc, 13 Nov: "food truck, kiosk and volleyball net on the beach"): model, size, turn the nose to +x.
NEW = {
    "Prop_AIFoodTruck": ("S3_foodtruck", ("width", 2.3), True),
    "Prop_AIKiosk": ("S4_kiosk", ("span", 3.0), True),
    "Prop_AIVolleyNet": ("B5_volleynet", ("box", (9.5, None, 2.55)), False),   # spans along x; a real net: 9.5 x 2.43 m
    "Prop_AISurfRack": ("B4_surfrack", ("height", 2.3), True),
    "Prop_AIBistroSet": ("S1_bistroset", ("span", 1.6), True),
    "Prop_AIBarStool": ("S2_barstool", ("height", 0.76), True),
    "Prop_AIScooter": ("V1_scooter", ("span", 1.8), True),
    "Prop_AIEScooter": ("P2_escooter", ("span", 1.1), True),
}
# Where they stand: on the cove beach south of the promenade (sand 13-17 m from the route, ground mask checked) and
# the food truck on the palm car park at the beach bar (paving, 13 m from the route). (x, y, yaw deg, ground)
NEW_PLACES = (
    ("Prop_AIVolleyNet", -89.0, -167.0, 0.0, "sand"),
    ("Prop_AIKiosk", -134.0, -164.0, 90.0, "sand"),                   # its counter faces the promenade (north)
    ("Prop_AIFoodTruck", -74.0, -133.0, 180.0, "paving"),
    # Luc, 13 Nov: the rest too. Surf rack beside the kiosk, three stools at its counter (sand), a scooter next to the
    # food truck, three hire e-scooters on the coastal road's pavement between its bike racks (11-13 m from the route)
    ("Prop_AISurfRack", -138.5, -164.0, 90.0, "sand"),
    ("Prop_AIBarStool", -135.0, -162.3, 0.0, "sand"),
    ("Prop_AIBarStool", -134.0, -162.3, 40.0, "sand"),
    ("Prop_AIBarStool", -133.0, -162.3, 75.0, "sand"),
    ("Prop_AIScooter", -79.0, -132.5, 70.0, "paving"),
    ("Prop_AIEScooter", -231.9, -33.7, 53.0, "paving"),
    ("Prop_AIEScooter", -231.4, -34.1, 58.0, "paving"),
    ("Prop_AIEScooter", -230.9, -34.5, 48.0, "paving"),
)
BEACH_BAR = (-46.0, -147.0, 9.0)       # the beach bar terrace: its tables and chairs become AI bistro sets (centre, radius)       # Prop_BikeRack: three bikes beside its hoops (x of each)
ROUGHNESS = {"C": 0.35, "B": 0.6, "P": 0.45}                  # paint a little glossy, beach wood and canvas matt


def _mesh(model):
    """The model's mesh (one object's data, transforms applied), as imported from ai_assets/<model>.glb."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(AI, model + ".glb"))
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    bm = bmesh.new()
    for o in meshes:
        tmp = o.data.copy()
        tmp.transform(o.matrix_world)
        bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    data = bpy.data.meshes.new(model)
    bm.to_mesh(data)
    bm.free()
    for o in new:
        old = o.data if o.type == "MESH" else None
        bpy.data.objects.remove(o)
        if old is not None and old.users == 0:
            bpy.data.meshes.remove(old)
    return data


def _fit(data, how, turn=True):
    """Turn the nose to +x, size it, stand it on z = 0 and centre it on x/y."""
    if turn:
        data.transform(TURN)
    co = [v.co for v in data.vertices]
    lo = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    hi = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    size = hi - lo
    kind, metres = how
    data.transform(Matrix.Translation(Vector((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z))))
    if kind == "box":                                 # per axis (None = keep the x scale): for a flat thing like a net
        sx = metres[0] / size.x
        k = Vector((sx, metres[1] / size.y if metres[1] else sx, metres[2] / size.z))
        data.transform(Matrix.Diagonal((k.x, k.y, k.z, 1.0)))
        return Vector((size.x * k.x, size.y * k.y, size.z * k.z))
    s = metres / {"width": size.y, "height": size.z, "span": max(size.x, size.y)}[kind]
    data.transform(Matrix.Scale(s, 4))
    return size * s


def _material(model, variant):
    """W_ai_<model>[_<variant>]: albedo and normal map from ai_assets, copied into textures/ for the Unity bundle."""
    name = "W_ai_" + model + ("_" + variant if variant else "")
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    os.makedirs(TEX, exist_ok=True)
    files = {}
    for key, fname in (("albedo", f"{model}_albedo{'_' + variant if variant else ''}.jpg"), ("normal", f"{model}_normal.png")):
        shutil.copy2(os.path.join(AI, fname), os.path.join(TEX, fname))
        files[key] = os.path.join(TEX, fname)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    b = nt.nodes["Principled BSDF"]
    t_alb = nt.nodes.new("ShaderNodeTexImage")
    t_alb.image = bpy.data.images.load(files["albedo"], check_existing=True)
    t_nrm = nt.nodes.new("ShaderNodeTexImage")
    t_nrm.image = bpy.data.images.load(files["normal"], check_existing=True)
    t_nrm.image.colorspace_settings.name = "Non-Color"
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(t_alb.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(t_nrm.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = ROUGHNESS.get(model[0], 0.55)
    b.inputs["Metallic"].default_value = 0.0
    return mat


def _budget(name, table):
    return next((v for k, v in table.items() if name.startswith(k)), None)


def _decimate(data, tris):
    """Collapse-decimate a mesh to about `tris` triangles; its UVs (clean islands from ai_bake) are kept."""
    have = sum(len(p.vertices) - 2 for p in data.polygons)
    if not tris or have <= tris:
        return data
    tmp = bpy.data.objects.new("_ai_tmp", data)
    bpy.context.scene.collection.objects.link(tmp)
    mod = tmp.modifiers.new("dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = tris / have
    mod.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bm = bmesh.new()                                  # the collapse can leave loose vertices; Unity drops them on
    bm.from_mesh(out)                                 # import, so they would only skew Blender's bounds
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(out)
    bm.free()
    for m in data.materials:
        out.materials.append(m)
    bpy.data.objects.remove(tmp)
    return out


def _replace(obj, data):
    old = obj.data
    obj.data = data
    data.name = obj.name
    if old.users == 0:
        bpy.data.meshes.remove(old)


def swap(protos):
    """Give the prototypes in `protos` (name -> object) their AI models. Returns a short report."""
    report = []
    for name, (model, variant, how) in SWAPS.items():
        obj = protos.get(name)
        if obj is None or not os.path.exists(os.path.join(AI, model + ".glb")):
            continue
        data = _mesh(model)
        size = _fit(data, how)
        data.materials.clear()
        data.materials.append(_material(model, variant))
        data = _decimate(data, _budget(name, NEAR_TRIS))
        _replace(obj, data)
        report.append(f"{name}={model}{'/' + variant if variant else ''} {size.x:.2f}x{size.y:.2f}x{size.z:.2f} m "
                      f"{sum(len(p.vertices) - 2 for p in data.polygons)} tris")
    home = next(iter(protos["Prop_CarA"].users_collection)) if "Prop_CarA" in protos else E.collection("Proto_Props")
    for name, (model, how, turn) in NEW.items():
        if name in protos or not os.path.exists(os.path.join(AI, model + ".glb")):
            continue
        data = _mesh(model)
        size = _fit(data, how, turn=turn)
        data.materials.clear()
        data.materials.append(_material(model, ""))
        obj = bpy.data.objects.new(name, data)
        data.name = name
        home.objects.link(obj)
        protos[name] = obj
        report.append(f"{name}={model} {size.x:.2f}x{size.y:.2f}x{size.z:.2f} m {sum(len(p.vertices) - 2 for p in data.polygons)} tris")
    rack = protos.get("Prop_BikeRack")
    if rack is not None and os.path.exists(os.path.join(AI, "P1_bicycle.glb")):
        bm = bmesh.new()
        bm.from_mesh(rack.data)                           # the three steel hoops stay
        mats = list(rack.data.materials)
        for variant, x in BIKES:
            bike = _mesh("P1_bicycle")
            _fit(bike, ("span", 1.72), turn=False)        # the bike's long side stays along x ...
            bike.transform(Matrix.Rotation(math.radians(90.0), 4, "Z"))    # ... and goes through the rack along y
            bike.transform(Matrix.Translation(Vector((x + 0.12, 0.0, 0.0))))
            mat = _material("P1_bicycle", variant)
            if mat not in mats:
                mats.append(mat)
            k = mats.index(mat)
            for p in bike.polygons:
                p.material_index = k
            bm.from_mesh(bike)
            bpy.data.meshes.remove(bike)
        data = bpy.data.meshes.new("Prop_BikeRack")
        bm.to_mesh(data)
        bm.free()
        for m in mats:
            data.materials.append(m)
        _replace(rack, _decimate(data, _budget("Prop_BikeRack", NEAR_TRIS)))
        report.append(f"Prop_BikeRack=hoops+3 P1_bicycle {sum(len(p.vertices) - 2 for p in data.polygons)} tris")
    print("  ai props (V18m): " + "; ".join(report))
    return report


def _ground_hit(x, y, z_from):
    """Height of the built ground (not a prop or plant) under (x, y), from z_from down; None if nothing."""
    dg = bpy.context.evaluated_depsgraph_get()
    sc = bpy.context.scene
    z = z_from
    for _ in range(6):
        hit, loc, _n, _i, ob, _m = sc.ray_cast(dg, Vector((x, y, z)), Vector((0.0, 0.0, -1.0)), distance=40.0)
        if not hit:
            return None
        if not ob.name.startswith(("Prop_", "Veg_", "Proto")):
            return loc.z
        z = loc.z - 0.01
    return None


def _wheel_ground(proto, p):
    """A car stands on the lowest of its four wheel points: on sloping ground the old placement height (at its
    centre) left one end in the air (Luc, 13 Nov: "the cars partly float")."""
    if proto is None:
        return p["z"]
    xs = [v.co.x for v in proto.data.vertices]
    ys = [v.co.y for v in proto.data.vertices]
    hx, hy = 0.36 * (max(xs) - min(xs)), 0.38 * (max(ys) - min(ys))
    c, s_ = math.cos(p["rot_z"]), math.sin(p["rot_z"])
    zs = []
    for ax, ay in ((hx, hy), (hx, -hy), (-hx, hy), (-hx, -hy)):
        z = _ground_hit(p["x"] + c * ax - s_ * ay, p["y"] + s_ * ax + c * ay, p["z"] + 2.5)
        if z is not None and abs(z - p["z"]) < 0.6:
            zs.append(z)
    # at most 8 cm below the old height: a wheel point beyond a kerb hits the lower ground there (up to 29 cm)
    return round(max(min(zs), p["z"] - 0.08), 3) if zs else p["z"]


def far_variants(props, protos):
    """Placements more than NEAR_M from the route use '<proto>_Far': the same model and textures, decimated to
    FAR_TRIS. Adds those prototypes to `protos` and the Proto_Props collection. Returns the new props list."""
    import street_plan as S
    bx, by, br = BEACH_BAR
    bar_tables = []
    if "Prop_AIBistroSet" in protos:
        kept = []
        for p in props:
            at_bar = math.hypot(p["x"] - bx, p["y"] - by) < br
            if at_bar and p["proto"] == "Prop_BistroTable":
                bar_tables.append(p)
            elif not (at_bar and p["proto"] == "Prop_BistroChair"):
                kept.append(p)
        props = kept + [dict(t, proto="Prop_AIBistroSet") for t in bar_tables]
    out, far, dupes, cars = [], 0, 0, []
    for p in props:
        if p["proto"].startswith("Prop_Car"):          # two dressing steps put a car on the same bay twice (2 spots)
            if any(math.hypot(p["x"] - q[0], p["y"] - q[1]) < 0.8 for q in cars):
                dupes += 1
                continue
            cars.append((p["x"], p["y"]))
            p = dict(p, z=_wheel_ground(protos.get(p["proto"]), p))
        name = p["proto"]
        if name in protos and (name in SWAPS or name == "Prop_BikeRack") and S.route_distance(p["x"], p["y"]) > NEAR_M:
            fname = name + "_Far"
            if fname not in protos:
                data = _decimate(protos[name].data.copy(), _budget(name, FAR_TRIS))
                obj = bpy.data.objects.new(fname, data)
                data.name = fname
                for c in protos[name].users_collection:
                    c.objects.link(obj)
                protos[fname] = obj
            p = dict(p, proto=fname)
            far += 1
        out.append(p)
    import env_coast
    for name, x, y, yaw, ground in NEW_PLACES:
        if name not in protos:
            continue
        clear = S.route_distance(x, y)
        assert clear > 6.6 + 3.0, f"{name} at ({x}, {y}) only {clear:.1f} m from the route"
        dg = bpy.context.evaluated_depsgraph_get()            # the built ground (sand or paving) right under it
        hit, loc, _n, _i, _o, _m = bpy.context.scene.ray_cast(dg, Vector((x, y, 200.0)), Vector((0.0, 0.0, -1.0)))
        z = loc.z if hit else (env_coast.paving_z(x, y) if ground == "paving" else E.ground_z(x, y))
        out.append({"proto": name, "x": x, "y": y, "z": round(z, 3), "rot_z": math.radians(yaw), "scale": 1.0})
    import json
    path = os.path.join(E.OUT, "placements_props.json")         # what export_unity_bundle hands to Unity
    data = json.load(open(path))
    data["placements"] = out
    counts = {}
    for it in out:
        counts[it["proto"]] = counts.get(it["proto"], 0) + 1
    data["counts"] = counts
    json.dump(data, open(path, "w"), indent=1)
    print(f"  ai props (V18m): {far} placements further than {NEAR_M:.0f} m from the route use the _Far versions; "
          f"{dupes} doubled cars removed; {len(bar_tables)} beach bar tables are bistro sets")
    return out
