"""Gergeti blockout. Run: blender -b --python spike/tools/build_church.py

Coordinates: metres, Z up, entrance centre at (0, 0, 0), front faces -Y.
Terrace walking surface is Z=0; surrounding ground is Z=-1.2.
The supplied component sizes contradict the requested overall bounds:
40x32 terrace and 30m annex cannot fit 37x27 maximum width/depth.
Default preserves component dimensions and reports this explicitly.
Optional -- --compact reduces terrace/annex to fit the requested bounds.
Only Blender's bundled modules and Python standard library are used.
"""

import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector


PARAMS = {
    "terrace_width": 40.0, "terrace_depth": 32.0, "terrace_height": 1.2,
    "step_count": 5, "step_width": 6.0, "step_run": 0.5,
    "nave_width": 20.0, "nave_depth": 12.0, "wall_height": 11.0,
    "wall_thickness": 0.6, "nave_roof_rise": 2.0,
    "door_width": 2.2, "door_height": 3.4,
    "window_width": 1.4, "window_height": 2.6, "window_sill": 5.0,
    "drum_sides": 12, "drum_radius": 5.2, "drum_height": 4.0,
    "dome_height": 6.0, "main_cross_height": 1.2,
    "tower_sides": 8, "tower_radius": 2.2, "tower_height": 14.0,
    "tower_offset": (9.0, -7.0), "tower_roof_height": 3.5,
    "tower_cross_height": 1.0, "tower_arch_width": 1.6,
    "tower_arch_height": 3.2, "tower_arch_sill": 10.0,
    "annex_width": 8.0, "annex_depth": 30.0, "annex_height": 5.0,
    "annex_gap": 1.0, "annex_roof_rise": 2.0,
    "arch_segments": 16,
}

ROOT = Path(__file__).resolve().parents[2]
BLEND = ROOT / "spike/blend/gergeti_church.blend"
FBX = ROOT / "spike/export/gergeti_church.fbx"
COMPACT = "--compact" in (sys.argv[sys.argv.index("--") + 1:]
                             if "--" in sys.argv else [])


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def material(name, hex_color):
    rgb = [int(hex_color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    linear = [v / 12.92 if v <= 0.04045 else ((v + .055) / 1.055) ** 2.4
              for v in rgb]
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*linear, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*linear, 1)
    shader.inputs["Roughness"].default_value = .85
    mat["source_srgb"] = "#" + hex_color
    return mat


def mesh(name, vertices, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    if mat:
        data.materials.append(mat)
    return obj


def box(name, centre, size, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=centre)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    activate(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    return obj


def cone(name, xy, bottom, height, radius, sides, mat, top_radius=0):
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=radius,
                                   radius2=top_radius, depth=height,
                                   location=(*xy, bottom + height / 2),
                                   rotation=(0, 0, math.pi / sides))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def arch(name, centre, width, height, depth, mat=None, rotation=0):
    """Closed semicircular arch prism, local X width/Y depth/Z height."""
    r = width / 2
    spring = height - r
    profile = [(-r, 0), (r, 0)]
    profile += [(r * math.cos(a), spring + r * math.sin(a))
                for a in [math.pi * i / PARAMS["arch_segments"]
                          for i in range(PARAMS["arch_segments"] + 1)]]
    n = len(profile)
    vertices = [(x, y, z) for y in (-depth / 2, depth / 2)
                for x, z in profile]
    faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))]
    faces += [(i, i + n, (i + 1) % n + n, (i + 1) % n)
              for i in range(n)]
    obj = mesh(name, vertices, faces, mat)
    obj.location = centre
    obj.rotation_euler.z = rotation
    return obj


def subtract(target, cutter):
    activate(target)
    mod = target.modifiers.new("Applied_arch_cut", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.solver = "EXACT"
    mod.object = cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
    # Boolean may append an empty cutter material slot in Blender 5.x.
    assigned = target.data.materials[0]
    target.data.materials.clear()
    target.data.materials.append(assigned)
    for face in target.data.polygons:
        face.material_index = 0


def gable(name, cx, cy, width, depth, eave, rise, mat):
    # Solid triangular prism, ridge parallel to Y.
    verts = [(cx + x, cy + y, z)
             for y in (-depth / 2, depth / 2)
             for x, z in [(-width / 2, eave), (width / 2, eave),
                          (0, eave + rise)]]
    return mesh(name, verts, [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1),
                              (1, 4, 5, 2), (2, 5, 3, 0)], mat)


def cross(name, xy, bottom, height, mat):
    box(name + "_Stem", (*xy, bottom + height / 2),
        (height * .13, height * .13, height), mat)
    box(name + "_Arms", (*xy, bottom + height * .69),
        (height * .65, height * .13, height * .13), mat)


def prepare_meshes():
    objects = sorted(bpy.context.scene.objects, key=lambda o: o.name)
    for obj in objects:
        assert obj.type == "MESH" and len(obj.data.materials) == 1, obj.name
        activate(obj)
        # Baking location too makes every exported object share the entrance origin.
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.dissolve_degenerate(threshold=.000001)
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.mesh.quads_convert_to_tris(quad_method="BEAUTY", ngon_method="BEAUTY")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),
                                 island_margin=.02)
        bpy.ops.object.mode_set(mode="OBJECT")
        assert obj.data.uv_layers.active is not None
        assert len(obj.modifiers) == 0
        assert all(math.isfinite(v) for vert in obj.data.vertices for v in vert.co)
    return objects


def bounds(objects):
    pts = [obj.matrix_world @ Vector(p) for obj in objects for p in obj.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    return lo, hi


def build():
    p = dict(PARAMS)
    if COMPACT:
        p.update(terrace_width=34.0, terrace_depth=24.0, annex_depth=24.0)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    scene.render.engine = "BLENDER_EEVEE"
    scene["origin_convention"] = "Entrance centre, terrace surface Z=0, front=-Y"
    scene["dimension_policy"] = "compact" if COMPACT else "component_dimensions"
    for key, value in p.items():
        scene["param_" + key] = value
    stone = material("Stone", "C9A87C")
    roof = material("Roof", "A06B4A")
    wood = material("Wood", "6B4A32")
    cy = p["nave_depth"] / 2
    wh, t = p["wall_height"], p["wall_thickness"]
    box("Terrace", (0, cy, -p["terrace_height"] / 2),
        (p["terrace_width"], p["terrace_depth"], p["terrace_height"]), stone)
    front_edge = cy - p["terrace_depth"] / 2
    for i in range(p["step_count"]):
        # Five equal risers; highest tread meets the terrace.
        rise = p["terrace_height"] * (i + 1) / p["step_count"]
        run = p["step_run"] * (p["step_count"] - i)
        box("Entrance_Step_%02d" % (i + 1),
            (0, front_edge - run / 2, -p["terrace_height"] + rise / 2),
            (p["step_width"], run, rise), stone)
    w, d = p["nave_width"], p["nave_depth"]
    front = box("Nave_Front", (0, t / 2, wh / 2), (w, t, wh), stone)
    box("Nave_Back", (0, d - t / 2, wh / 2), (w, t, wh), stone)
    subtract(front, arch("Door_Cutter", (0, t / 2, -.01), p["door_width"],
                         p["door_height"] + .01, t + 1))
    door = arch("Entrance_Wood_Door", (0, .4, .02), p["door_width"] - .08,
                p["door_height"] - .06, .12, wood)
    door["note"] = "Recessed door inside a true arched opening"
    for side in (-1, 1):
        wall = box("Nave_" + ("West" if side < 0 else "East"),
                   (side * (w / 2 - t / 2), cy, wh / 2), (t, d - 2 * t, wh), stone)
        for index, y in enumerate((d / 3, d * 2 / 3)):
            subtract(wall, arch("Nave_Window_Cutter", (side * (w / 2 - t / 2),
                               y, p["window_sill"]), p["window_width"],
                               p["window_height"], t + 1, rotation=math.pi / 2))
    gable("Nave_Gabled_Roof", 0, cy, w + .4, d + .4, wh,
          p["nave_roof_rise"], roof)
    drum_base = wh + p["nave_roof_rise"]
    box("Drum_Square_Support", (0, cy, wh + p["nave_roof_rise"] / 2),
        (p["drum_radius"] * 2, p["drum_radius"] * 2,
         p["nave_roof_rise"]), stone)
    cone("Drum_12", (0, cy), drum_base, p["drum_height"], p["drum_radius"],
         p["drum_sides"], stone, p["drum_radius"])
    dome_base = drum_base + p["drum_height"]
    cone("Dome_12", (0, cy), dome_base, p["dome_height"], p["drum_radius"] + .2,
         p["drum_sides"], roof)
    cross("Dome_Cross", (0, cy), dome_base + p["dome_height"],
          p["main_cross_height"], stone)
    tx, ty = p["tower_offset"][0], cy + p["tower_offset"][1]
    tower = cone("BellTower_8", (tx, ty), 0, p["tower_height"],
                 p["tower_radius"], p["tower_sides"], stone, p["tower_radius"])
    # Remove the core in the belfry; four independent cutters open its cardinal faces.
    core_radius = p["tower_radius"] - .55
    core = cone("Belfry_Core_Cutter", (tx, ty), p["tower_arch_sill"],
                p["tower_height"] - p["tower_arch_sill"] + .1,
                core_radius, p["tower_sides"], stone, core_radius)
    subtract(tower, core)
    for side in range(4):
        a = side * math.pi / 2
        radius = p["tower_radius"] * .8
        subtract(tower, arch("Belfry_Arch_Cutter",
                  (tx + radius * math.sin(a), ty - radius * math.cos(a),
                   p["tower_arch_sill"]), p["tower_arch_width"],
                  p["tower_arch_height"], 2.0, rotation=a))
    cone("BellTower_Roof_8", (tx, ty), p["tower_height"],
         p["tower_roof_height"], p["tower_radius"] + .2, p["tower_sides"], roof)
    cross("BellTower_Cross", (tx, ty), p["tower_height"] + p["tower_roof_height"],
          p["tower_cross_height"], stone)
    ax = -w / 2 - p["annex_gap"] - p["annex_width"] / 2
    aw, ad, ah = p["annex_width"], p["annex_depth"], p["annex_height"]
    for side in (-1, 1):
        wall = box("Annex_" + ("West" if side < 0 else "East"),
                   (ax + side * (aw / 2 - t / 2), cy, ah / 2), (t, ad, ah), stone)
        # Four arch windows total, on the exposed west wall.
        if side < 0:
            for i in range(4):
                subtract(wall, arch("Annex_Window_Cutter",
                    (ax + side * (aw / 2 - t / 2), cy + ad * ((i + .5) / 4 - .5), 1.2),
                    1.4, 2.6, t + 1, rotation=math.pi / 2))
    for label, y in (("Front", cy - ad / 2 + t / 2),
                     ("Back", cy + ad / 2 - t / 2)):
        box("Annex_" + label, (ax, y, ah / 2), (aw - 2 * t, t, ah), stone)
    gable("Annex_Pitched_Roof", ax, cy, aw + .4, ad + .4, ah,
          p["annex_roof_rise"], roof)
    objects = prepare_meshes()
    lo, hi = bounds(objects)
    size = hi - lo
    triangles = 0
    for obj in objects:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    assert set(bpy.data.materials.keys()) == {"Stone", "Roof", "Wood"}
    for obj in objects:
        assert obj.matrix_world == obj.matrix_world.Identity(4), obj.name
    in_bounds = all(abs(actual - target) <= 3 for actual, target in
                    ((size.x, 34), (size.z, 27), (size.y, 24)))
    if COMPACT:
        assert in_bounds, tuple(size)
    else:
        print("[CHURCH] bbox_check=%s; component dimensions preserved; "
              "40x32m terrace / 30m annex conflict with overall limit" % in_bounds)
    print("[CHURCH] triangles=%d" % triangles)
    print("[CHURCH] bbox=%.3fx%.3fx%.3f" % (size.x, size.z, size.y))
    print("[CHURCH] objects=%d" % len(objects))
    scene["triangle_count"] = triangles
    scene["bbox_width_height_depth"] = [size.x, size.z, size.y]
    BLEND.parent.mkdir(parents=True, exist_ok=True)
    FBX.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    bpy.ops.export_scene.fbx(filepath=str(FBX), use_selection=True,
        object_types={"MESH"}, global_scale=1.0, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        use_space_transform=True, bake_space_transform=True,
        mesh_smooth_type="FACE", use_mesh_modifiers=True, use_triangles=True,
        add_leaf_bones=False, bake_anim=False, path_mode="AUTO")
    assert BLEND.is_file() and FBX.is_file()
    print("[CHURCH] saved=%s" % BLEND)
    print("[CHURCH] exported=%s" % FBX)


def verify():
    """Independent disk reload, opening probes, and FBX metre/UV round-trip."""
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    scene = bpy.context.scene
    objects = list(scene.objects)
    expected_count = len(objects)
    expected_triangles = scene["triangle_count"]
    expected_bounds = tuple(scene["bbox_width_height_depth"])
    assert scene.unit_settings.scale_length == 1
    assert set(bpy.data.materials.keys()) == {"Stone", "Roof", "Wood"}
    for obj in objects:
        assert obj.matrix_world.is_identity, obj.name
        assert obj.data.uv_layers.active and len(obj.data.materials) == 1

    def opening(name, start, direction, distance=1.5):
        assert not scene.objects[name].ray_cast(
            Vector(start), Vector(direction), distance=distance)[0], name

    opening("Nave_Front", (0, -.1, 1.5), (0, 1, 0), .8)
    for side in (-1, 1):
        for y in (4, 8):
            opening("Nave_West" if side < 0 else "Nave_East",
                    (side * 10.1, y, 6), (-side, 0, 0), .9)
    depth = scene["param_annex_depth"]
    for i in range(4):
        opening("Annex_West", (-19.1, 6 + depth * ((i + .5) / 4 - .5), 2),
                (1, 0, 0), .9)
    for start, direction in [((9, -3.4, 11), (0, 1, 0)),
                             ((11.4, -1, 11), (-1, 0, 0)),
                             ((9, 1.4, 11), (0, -1, 0)),
                             ((6.6, -1, 11), (1, 0, 0))]:
        opening("BellTower_8", start, direction)
    print("[VERIFY] blend: transforms, UV, three materials, 13 openings PASS")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(FBX))
    objects = list(scene.objects)
    assert len(objects) == expected_count
    assert all(o.data.uv_layers.active and len(o.data.materials) == 1 for o in objects)
    assert {o.data.materials[0].name.split(".")[0] for o in objects} == {
        "Stone", "Roof", "Wood"}
    lo, hi = bounds(objects)
    size = hi - lo
    assert all(abs(a - b) < .001 for a, b in zip(
        (size.x, size.z, size.y), expected_bounds))
    for obj in objects:
        obj.data.calc_loop_triangles()
    triangles = sum(len(o.data.loop_triangles) for o in objects)
    assert triangles == expected_triangles, (triangles, expected_triangles)
    print("[VERIFY] FBX round-trip: %d meshes, %d triangles, UV, metre bounds PASS"
          % (len(objects), triangles))


if __name__ == "__main__":
    try:
        if "--verify" in sys.argv:
            verify()
        else:
            build()
    except Exception:
        import traceback
        traceback.print_exc()
        # Blender normally returns zero for script errors unless --python-exit-code
        # is supplied. Make the required plain invocation fail reliably too.
        sys.stdout.flush()
        sys.stderr.flush()
        sys.exit(1)
