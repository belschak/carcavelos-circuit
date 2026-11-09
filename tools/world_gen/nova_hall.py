"""The Nova SBE campus interior, rebuilt on its own (no track, no cut through the building):

  A  the entrance hall ("Hovione Atrium"): glass doors on the north side, polished concrete floor, dark blue
     partition boards with planter troughs, a reception desk, a timber-joist gallery on one side, a white wall
     block with a sign plate, a white wall with artwork and a pair of doors on the far side
  B  the long study hall: a low flat ceiling first, then a huge steel hall with a glass facade held by leaning
     white columns and diagonal rods, white tables, black chairs, two grand pianos, plants, a mezzanine box
  C  the exit to the terrace: sliding glass doors with exit signs, a mat, a few metres of concrete deck

Source: the team's walk video IMG_7722 (first 105 s), plus IMG_7725 for the entrance side. Everything is a number in
the section constants below; hall_kit.py has the parts. No Nova logo (docs/decisions.md): the big NOVA letters of the
real hall are replaced by an abstract white sculpture.

Run:  /Applications/Blender.app/Contents/MacOS/Blender -b -P nova_hall.py        ->  out/NovaHall.blend, out/NovaHall.fbx
"""
import math
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import env_common as E          # noqa: E402
import hall_kit as K            # noqa: E402

OUT = os.path.join(HERE, "out")

# ---------- A: entrance hall (u from the north doors) ----------
A_LEN, A_LEFT, A_RIGHT = 30.0, 15.0, 15.0     # length along the axis, half widths to the left / right (m)
A_ROOF = 10.0                                  # steel roof deck height
GALLERY_Z, GALLERY_U0, GALLERY_U1, GALLERY_O0 = 5.1, 4.0, 27.0, 8.5   # timber gallery on the left
# ---------- B: study hall ----------
B_LOW_END = 44.0                               # the first 14 m have a flat white ceiling at B_LOW_Z
B_LOW_Z = 4.6
B_END = 118.0
B_LEFT, B_RIGHT = 12.0, 13.0
B_ROOF = 11.0
FACADE_BAY = 12.0                              # distance between the leaning white columns on the glass facade
# ---------- C: terrace ----------
TERRACE_LEN = 14.0


def sealed_shell(m, f):
    """Floor, ceilings, outer walls. Windows are glass panels in front of dark openings."""
    K.floor_rect(m, f, 0, A_LEN, -A_RIGHT, A_LEFT, 0.0, "floor")
    K.floor_rect(m, f, A_LEN, B_END, -B_RIGHT, B_LEFT, 0.0, "floor")
    # outer walls (inside faces) -- entrance hall
    K.wall_o(m, f, A_LEFT, 0, A_LEN, 0, A_ROOF, "wall", facing=-1)
    K.wall_o(m, f, -A_RIGHT, 0, A_LEN, 0, A_ROOF, "wall", facing=1)
    K.wall_u(m, f, 0, -A_RIGHT, A_LEFT, 0, A_ROOF, "wall", facing=1)         # north wall, doors are cut by glass below
    # study hall left wall (solid white service side), right = glass facade (see facade())
    K.wall_o(m, f, B_LEFT, A_LEN, B_END, 0, B_ROOF, "wall", facing=-1)
    K.wall_u(m, f, B_END, -B_RIGHT, B_LEFT, 0, B_ROOF, "wall", facing=-1)


def north_entrance(m, f):
    """Glass front with a sliding door in the middle (stainless frames), slim side lights, transom, white fins."""
    u = 0.05
    opening = 2.6
    zt = 4.3
    for o0, o1 in ((-A_RIGHT + 0.6, -opening), (opening, A_LEFT - 0.6)):
        K.wall_u(m, f, u, o0, o1, 0.05, zt, "glass", facing=1)
    K.wall_u(m, f, u, -A_RIGHT + 0.6, A_LEFT - 0.6, zt, A_ROOF - 0.3, "glass", facing=1)
    for o in (-opening, opening):                                              # door frame posts
        K.block(m, f, u - 0.1, u + 0.1, o - 0.07, o + 0.07, 0, zt, "steel_dark")
    K.block(m, f, u - 0.12, u + 0.12, -opening, opening, zt - 0.2, zt + 0.1, "steel_dark")      # header box with the door motor
    for k in range(12):                                                         # mullions
        o = -A_RIGHT + 0.6 + (A_LEFT + A_RIGHT - 1.2) * k / 11
        K.block(m, f, u - 0.06, u + 0.06, o - 0.04, o + 0.04, 0, A_ROOF - 0.3, "steel_white")
    K.block(m, f, u - 0.1, u + 0.1, -A_RIGHT, A_LEFT, A_ROOF - 0.3, A_ROOF, "steel_dark")
    for o in (-1.2, 1.2):                                                       # the two door leaves, slid apart
        K.block(m, f, u + 0.2, u + 0.26, o + (0.9 if o > 0 else -0.9) - 0.55, o + (0.9 if o > 0 else -0.9) + 0.55, 0.05, 4.0, "glass")
    K.floor_rect(m, f, 0.4, 3.0, -opening + 0.3, opening - 0.3, 0.02, "mat")
    for o in (-3.3, 3.3):                                                       # white fins outside the doors
        K.block(m, f, -1.5, 0.0, o - 0.2, o + 0.2, 0, 6.2, "wall")


def roof_a(m, f):
    """Steel roof over the entrance hall: grey deck, white trusses every 6 m, long beams, skylight strips."""
    K.floor_rect(m, f, 0, A_LEN, -A_RIGHT, A_LEFT, A_ROOF, "deck", up=False)
    for u in K.Frame().samples(1.5, A_LEN - 1.5, 6.0):
        K.truss(m, f, u, -A_RIGHT, A_LEFT, A_ROOF - 1.3, A_ROOF - 0.15, bays=10)
    for o in (-10.0, -5.0, 0.0, 5.0, 10.0):
        K.rod(m, f, (0, o, A_ROOF - 0.35), (A_LEN, o, A_ROOF - 0.35), 0.16, "steel_white", 0.3)
    for k, o in enumerate((-7.5, -2.5, 2.5, 7.5)):                              # skylight strips
        K.floor_rect(m, f, 2.0, A_LEN - 2.0, o - 0.9, o + 0.9, A_ROOF - 0.04, "light", up=False)


def gallery(m, f):
    """First-floor gallery on the left with the orange timber joists seen from below, glass+steel balustrade."""
    u0, u1, o0, o1, z = GALLERY_U0, GALLERY_U1, GALLERY_O0, A_LEFT, GALLERY_Z
    K.floor_rect(m, f, u0, u1, o0, o1, z, "wall", up=True)
    K.floor_rect(m, f, u0, u1, o0, o1, z - 0.42, "timber", up=False)
    for u in K.Frame().samples(u0, u1, 1.0):                                    # joists
        K.block(m, f, u - 0.07, u + 0.07, o0, o1, z - 0.42, z - 0.05, "timber")
    K.block(m, f, u0, u1, o0 - 0.15, o0 + 0.05, z - 0.5, z + 0.05, "wall")      # white edge beam
    K.wall_o(m, f, o0 - 0.1, u0, u1, z + 0.05, z + 1.15, "glass", facing=-1)
    K.block(m, f, u0, u1, o0 - 0.14, o0 - 0.06, z + 1.1, z + 1.16, "rail")
    for u in K.Frame().samples(u0, u1, 2.0):
        K.block(m, f, u - 0.03, u + 0.03, o0 - 0.14, o0 - 0.06, z, z + 1.12, "rail")
    for u in (u0 + 1.0, u0 + 7.0, u0 + 13.0, u0 + 19.0):                         # white round columns
        K.round_column(m, f, u, o0 - 0.4, 0, z, 0.2)
    # upper floor back wall glazing and a ceiling above the gallery
    K.wall_o(m, f, A_LEFT - 0.05, u0, u1, z + 0.1, A_ROOF - 0.5, "glass", facing=-1)


def hovione_wall(m, f):
    """The white wall block on the right with the sign plate, and the far white wall with artwork and doors."""
    K.block(m, f, 15.0, 27.5, -A_RIGHT, -A_RIGHT + 6.5, 0, 9.0, "wall")
    K.block(m, f, 15.0 - 0.04, 15.0 + 0.02, -A_RIGHT + 1.0, -A_RIGHT + 5.5, 4.0, 5.6, "table")   # sign plate (no text)
    K.block(m, f, 15.0 - 0.06, 15.0 - 0.02, -A_RIGHT + 1.8, -A_RIGHT + 4.7, 4.5, 4.8, "steel_dark")
    # far wall at u = A_LEN: white, artwork, two door pairs, a 4 m opening towards the study hall
    u = A_LEN
    opening = (-2.0, 2.0)
    K.wall_u(m, f, u, -A_RIGHT, opening[0], 0, A_ROOF, "wall", facing=-1)
    K.wall_u(m, f, u, opening[1], A_LEFT, 0, A_ROOF, "wall", facing=-1)
    K.wall_u(m, f, u, opening[0], opening[1], B_LOW_Z, A_ROOF, "wall", facing=-1)
    K.block(m, f, u - 0.4, u + 0.4, opening[0] - 0.4, opening[0], 0, B_LOW_Z, "wall")
    for o0 in (opening[1] + 0.4, opening[1] + 3.2):                            # two double doors on the left
        K.block(m, f, u - 0.1, u, o0, o0 + 2.4, 0.0, 2.3, "wall")
        K.block(m, f, u - 0.12, u - 0.1, o0 + 1.2 - 0.01, o0 + 1.2 + 0.01, 0.0, 2.3, "steel_dark")
        K.block(m, f, u - 0.12, u - 0.1, o0 + 0.2, o0 + 0.3, 1.0, 1.1, "rail")
    for o0 in (-9.5, -6.5, -3.5):                                              # coloured artwork pieces
        for k, (c, rot) in enumerate((("red", 0.3), ("blue", -0.2), ("yellow", 0.1))):
            K.block(m, f, u - 0.07, u - 0.01, o0 + k * 0.7, o0 + k * 0.7 + 0.55, 2.6 + (k % 2) * 0.6, 2.75 + (k % 2) * 0.6, c)
    for o in (-4.0, 4.5):                                                      # white square columns with black badge
        K.square_column(m, f, u - 1.2, o, 0, B_LOW_Z, 0.8)
    K.block(m, f, u - 1.62, u - 1.6, -4.15, -3.85, 1.4, 1.7, "black")


def entrance_furniture(m, f):
    K.desk(m, f, 4.0, 10.8, 6.0)                                               # reception desk under the gallery edge
    for k, o in enumerate((-7.0, -3.0, 1.0, 5.0)):                              # blue partition boards with planter troughs
        K.board(m, f, 14.0 + (k % 2) * 3.0, o, 2.2)
    for k in range(9):
        K.planter(m, f, 16.5, -9.5 + k * 2.4, 3 + k, palm=(k % 2 == 0))
    for k in range(5):
        K.planter(m, f, 5.0 + k * 3.4, -12.2, 30 + k, palm=(k % 2 == 1))
    for k, (u, o) in enumerate(((8.0, -9.0), (9.5, -12.0), (22.0, -9.5))):      # tables near the right wall
        K.round_table(m, f, u, o, 4)
    # abstract white sculpture where the real hall has the big logo letters
    K.block(m, f, 10.2, 10.8, -5.2, -4.0, 0, 2.2, "wall")
    t, n = f.tangent(), f.left()                                                 # an upright ring (the "O"), facing the doors
    m.ring(f.at(10.2, -1.6, 1.2), 1.15, 0.65, 0.6, "wall", basis=((0, 0, 1), (n[0], n[1], 0), (t[0], t[1], 0)))
    K.rod(m, f, (10.5, 2.4, 0.0), (10.5, 3.3, 2.2), 0.55, "wall")
    K.rod(m, f, (10.5, 4.2, 0.0), (10.5, 3.3, 2.2), 0.55, "wall")
    for k, o in enumerate((8.8, 10.5, 12.2)):                                  # lounge seats under the gallery
        K.block(m, f, 18.0, 19.2, o - 0.5, o + 0.5, 0.0, 0.45, "grey")


def low_ceiling_b(m, f):
    """The first 14 m of the study hall: flat white ceiling with vents and downlights, square columns."""
    u0, u1 = A_LEN, B_LOW_END
    K.floor_rect(m, f, u0, u1, -B_RIGHT, B_LEFT, B_LOW_Z, "soffit", up=False)
    for u in (u0 + 3.0, u0 + 9.0):
        for o in (-8.0, 3.0, 9.0):
            K.downlight(m, f, u, o, B_LOW_Z)
    for u, o in ((u0 + 6.0, -2.0), (u0 + 6.0, 6.0)):
        m.cylinder(f.at(u, o, B_LOW_Z - 0.03), 0.28, 0.03, "wall", 18)         # round vents
    for u in (u0 + 4.0, u0 + 11.0):
        for o in (-5.0, 5.0):
            K.square_column(m, f, u, o, 0, B_LOW_Z, 0.7)
    # right side: solid white until the facade starts
    K.wall_o(m, f, -B_RIGHT, u0, u1, 0, B_ROOF, "wall", facing=1)
    K.block(m, f, u0 + 4.0, u0 + 9.0, -B_RIGHT, -B_RIGHT + 0.1, 0.8, 2.4, "board")


def steel_hall_b(m, f):
    """The big steel hall: roof deck + trusses, glass facade with diagonal rods and leaning columns."""
    u0, u1 = B_LOW_END, B_END
    K.floor_rect(m, f, u0, u1, -B_RIGHT, B_LEFT, B_ROOF, "deck", up=False)
    for u in K.Frame().samples(u0 + 3.0, u1 - 2.0, 6.0):
        K.truss(m, f, u, -B_RIGHT, B_LEFT, B_ROOF - 1.6, B_ROOF - 0.2, bays=10)
    for o in (-11.0, -6.5, -2.0, 2.5, 7.0, 10.5):
        K.rod(m, f, (u0, o, B_ROOF - 0.45), (u1, o, B_ROOF - 0.45), 0.2, "steel_white", 0.38)
    K.floor_rect(m, f, u0 + 4.0, u1 - 4.0, -9.0, -7.0, B_ROOF - 0.05, "light", up=False)
    K.floor_rect(m, f, u0 + 4.0, u1 - 4.0, 2.0, 4.0, B_ROOF - 0.05, "light", up=False)
    # glass facade on the right
    o_f = -B_RIGHT
    K.wall_o(m, f, o_f + 0.1, u0, u1, 0.1, B_ROOF, "glass", facing=1)
    for u in K.Frame().samples(u0, u1, 3.0):                                    # mullions
        K.block(m, f, u - 0.04, u + 0.04, o_f + 0.04, o_f + 0.16, 0, B_ROOF, "steel_white")
    for z in (3.0, 6.0, 9.0):
        K.block(m, f, u0, u1, o_f + 0.04, o_f + 0.14, z - 0.03, z + 0.03, "steel_white")
    bays = int((u1 - u0) // FACADE_BAY)
    for k in range(bays):                                                       # leaning columns and X rods per bay
        ua = u0 + k * FACADE_BAY
        ub = ua + FACADE_BAY
        for u in (ua, ub):
            K.rod(m, f, (u, o_f + 0.35, 0.0), (u, o_f + 2.6, B_ROOF - 0.3), 0.5, "wall")
        K.rod(m, f, (ua, o_f + 0.2, 0.3), (ub, o_f + 0.2, B_ROOF - 0.5), 0.07, "steel_white")
        K.rod(m, f, (ua, o_f + 0.2, B_ROOF - 0.5), (ub, o_f + 0.2, 0.3), 0.07, "steel_white")
        K.rod(m, f, (ua, o_f + 0.35, B_ROOF - 0.4), (ua + FACADE_BAY / 2, o_f + 2.6, B_ROOF - 0.15), 0.1, "steel_white")


def left_side_b(m, f):
    """White service volumes on the left with dark brown recessed walls and a mezzanine box near the end."""
    for u0 in (52.0, 86.0):
        K.block(m, f, u0, u0 + 12.0, B_LEFT - 3.2, B_LEFT, 0, 3.8, "brown")                      # dark recess
        K.block(m, f, u0 - 1.0, u0 + 13.0, B_LEFT - 4.5, B_LEFT, 3.8, 6.2, "wall")               # white box over it
        for u in (u0 + 1.0, u0 + 9.5):
            K.round_column(m, f, u, B_LEFT - 3.8, 0, 3.8, 0.28)
        K.block(m, f, u0 + 3.0, u0 + 3.1, B_LEFT - 3.2, B_LEFT - 1.0, 1.2, 1.8, "steel_dark")    # info panel
    K.block(m, f, 102.0, B_END - 1.0, B_LEFT - 7.0, B_LEFT, 4.2, 8.2, "wall")                    # mezzanine box with balcony
    K.wall_o(m, f, B_LEFT - 7.05, 102.0, B_END - 1.0, 4.4, 5.5, "glass", facing=-1)
    K.floor_rect(m, f, 102.0, B_END - 1.0, B_LEFT - 7.0, B_LEFT, 4.2, "wall", up=False)
    for u in (104.0, 114.0):
        K.round_column(m, f, u, B_LEFT - 6.6, 0, 4.2, 0.28)


def furniture_b(m, f):
    for k, u in enumerate(K.Frame().samples(50.0, 112.0, 4.8)):                  # long tables on the facade side
        K.long_table(m, f, u, -8.2, seats=3)
    for k, (u, o) in enumerate(((48, -2), (54, -4), (60, 0), (66, -3), (72, 1), (78, -4), (84, 0), (90, -2), (96, 2),
                                (102, -4), (108, 0), (50, 3), (64, 4), (92, 4))):
        K.round_table(m, f, float(u), float(o), 4 if k % 2 == 0 else 3)
    K.grand_piano(m, f, 68.0, -10.5, 90)
    K.grand_piano(m, f, 76.0, -10.5, 90)
    for k, (u, o) in enumerate(((47.0, -11.0), (80.0, -11.2), (58.0, 7.0), (98.0, 6.0))):
        K.big_plant(m, f, u, o, 5 + k)
    for k, (u, o) in enumerate(((46.0, 5.0), (62.0, 9.0), (100.0, 1.0))):
        K.info_stand(m, f, u, o, k)


def exit_c(m, f):
    """Sliding glass doors with exit signs, a mat, and a short stretch of terrace."""
    u = B_END
    zt = 4.0
    opening = 1.8
    K.wall_u(m, f, u - 0.06, -B_RIGHT + 0.2, -opening, 0.05, B_ROOF - 0.3, "glass", facing=-1)
    K.wall_u(m, f, u - 0.06, opening, B_LEFT - 0.2, 0.05, B_ROOF - 0.3, "glass", facing=-1)
    K.wall_u(m, f, u - 0.06, -opening, opening, zt, B_ROOF - 0.3, "glass", facing=-1)
    for o in (-opening, opening, -opening - 3.0, opening + 3.0):
        K.block(m, f, u - 0.12, u, o - 0.05, o + 0.05, 0, B_ROOF - 0.3, "steel_dark")
    K.block(m, f, u - 0.14, u, -opening, opening, zt - 0.1, zt + 0.1, "steel_dark")
    for o in (-opening + 0.2, opening - 0.2):                                    # exit signs
        K.block(m, f, u - 0.25, u - 0.1, o - 0.35, o + 0.35, zt + 0.2, zt + 0.55, "exit")
    K.floor_rect(m, f, u - 3.0, u - 0.4, -opening, opening, 0.02, "mat")
    # terrace
    K.floor_rect(m, f, u, u + TERRACE_LEN, -B_RIGHT, B_LEFT, -0.05, "concrete")
    for o in (-B_RIGHT, B_LEFT):
        K.block(m, f, u, u + TERRACE_LEN, o - 0.05, o + 0.05, 0, 1.1, "rail")
    K.block(m, f, u + TERRACE_LEN - 0.05, u + TERRACE_LEN + 0.05, -B_RIGHT, B_LEFT, 0, 1.1, "rail")


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    f = K.Frame()
    col = E.collection("Nova_Hall")
    for name, parts in (("Hall_Shell", (sealed_shell, north_entrance, roof_a, low_ceiling_b, steel_hall_b, exit_c)),
                        ("Hall_Structure", (gallery, hovione_wall, left_side_b)),
                        ("Hall_Furniture", (entrance_furniture, furniture_b))):
        m = K.Mesh()
        for part in parts:
            part(m, f)
        obj = m.to_object(name, col)
        print(f"  {name}: {obj['triangles']} faces")
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "NovaHall.blend"))
    for o in bpy.data.objects:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "NovaHall.fbx"), object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", use_selection=True, use_triangles=True, mesh_smooth_type="FACE")
    print("done")


if __name__ == "__main__":
    build()
