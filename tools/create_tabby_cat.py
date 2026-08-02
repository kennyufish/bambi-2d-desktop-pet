import math
import os

import bpy
from mathutils import Matrix, Vector


OUTPUT = os.path.abspath(
    "unity-client/Assets/StandardCat/Source/cat_v2_tabby_plush.fbx"
)


def material(name, color, roughness=0.72):
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*color, 1.0)
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    return result


FUR = material("TabbyFur", (0.36, 0.21, 0.10))
DARK = material("TabbyStripe", (0.055, 0.035, 0.025))
WHITE = material("TabbyWhite", (0.88, 0.84, 0.76))
GREEN = material("EyeGreen", (0.32, 0.62, 0.18), 0.3)
BLACK = material("EyeBlack", (0.008, 0.006, 0.004), 0.22)
PINK = material("NosePink", (0.78, 0.35, 0.31), 0.42)


def preserve_world_parent(obj, parent):
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world


def bone(name, location, parent=None):
    result = bpy.data.objects.new(name, None)
    result.empty_display_type = "PLAIN_AXES"
    result.empty_display_size = 0.12
    result.location = location
    bpy.context.collection.objects.link(result)
    if parent is not None:
        preserve_world_parent(result, parent)
    return result


def smooth(obj):
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def join_and_remesh(objects, name, parent, voxel_size=0.04):
    for item in objects:
        world = item.matrix_world.copy()
        item.parent = None
        item.matrix_world = world
        item.select_set(False)

    for item in objects:
        item.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()

    obj = bpy.context.object
    obj.name = name
    modifier = obj.modifiers.new("SmoothUnion", "REMESH")
    modifier.mode = "VOXEL"
    modifier.voxel_size = voxel_size
    modifier.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    smooth(obj)

    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    preserve_world_parent(obj, parent)
    return obj


def sphere(name, location, scale, mat, parent, segments=32, rings=20):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments, ring_count=rings, location=location
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    smooth(obj)
    preserve_world_parent(obj, parent)
    return obj


def segment(name, start, end, radius, mat, parent, vertices=24):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=max(vertices, 24),
        ring_count=16,
        location=midpoint,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = (radius, radius, direction.length * 0.62)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0.0, 0.0, 1.0)).rotation_difference(direction)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    smooth(obj)
    preserve_world_parent(obj, parent)
    return obj


def surface_patch(name, x, y, z, width, height, parent, angle=0.0):
    patch = sphere(name, (x, y, z), (width, 0.035, height), DARK, parent, 24, 14)
    patch.rotation_euler.y = angle
    return patch


def tail_ring(name, location, direction, radius, parent):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=radius,
        minor_radius=0.022,
        major_segments=28,
        minor_segments=8,
        location=location,
    )
    ring = bpy.context.object
    ring.name = name
    ring.rotation_mode = "QUATERNION"
    ring.rotation_quaternion = Vector((0.0, 0.0, 1.0)).rotation_difference(Vector(direction))
    ring.data.materials.append(DARK)
    smooth(ring)
    preserve_world_parent(ring, parent)


def curve_tube(name, points, radius, parent, point_radii=None):
    curve = bpy.data.curves.new(name + "_Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 14
    curve.bevel_depth = radius
    curve.bevel_resolution = 4
    curve.use_fill_caps = True
    curve.resolution_u = 16

    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for index, (point, coordinate) in enumerate(zip(spline.bezier_points, points)):
        point.co = coordinate
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
        if point_radii is not None:
            point.radius = point_radii[index]

    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.object
    obj.data.materials.append(FUR)
    smooth(obj)
    preserve_world_parent(obj, parent)
    return obj


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

model = bone("Model", (0.0, 0.0, 0.0))
root = bone("root", (0.0, 0.0, 1.05), model)
butt = bone("butt", (0.78, 0.0, 1.15), root)
belly = bone("belly", (0.0, 0.0, 1.16), butt)
chest = bone("chest", (-0.78, 0.0, 1.3), belly)
neck = bone("Neck", (-1.04, 0.0, 1.72), chest)
head = bone("Head", (-1.18, 0.0, 2.05), neck)

body_mesh = sphere("BodyUnion", (0.04, 0.0, 1.23), (1.12, 0.52, 0.57), FUR, belly, 48, 30)
chest_mesh = sphere("ChestUnion", (-0.72, 0.0, 1.38), (0.55, 0.54, 0.65), FUR, chest, 40, 26)
haunch_mesh = sphere("HaunchUnion", (0.7, 0.0, 1.2), (0.58, 0.53, 0.62), FUR, butt, 40, 26)
join_and_remesh(
    [body_mesh, chest_mesh, haunch_mesh],
    "Fur_Torso",
    belly,
    0.035,
)
sphere("White_Chest", (-1.04, -0.505, 1.25), (0.27, 0.045, 0.43), WHITE, chest, 32, 20)
sphere("Fur_NeckRuff", (-0.99, 0.0, 1.66), (0.36, 0.47, 0.4), FUR, neck, 40, 26)
sphere("Fur_Head", (-1.2, 0.0, 1.98), (0.52, 0.47, 0.51), FUR, head, 48, 30)
sphere("White_MuzzleL", (-1.63, -0.13, 1.86), (0.17, 0.18, 0.14), WHITE, head, 32, 20)
sphere("White_MuzzleR", (-1.63, 0.13, 1.86), (0.17, 0.18, 0.14), WHITE, head, 32, 20)
sphere("Nose", (-1.79, 0.0, 1.93), (0.065, 0.08, 0.052), PINK, head, 24, 14)

for side, y in (("L", -0.445), ("R", 0.445)):
    sphere(f"EyeGreen_{side}", (-1.53, y, 2.12), (0.105, 0.032, 0.125), GREEN, head, 32, 20)
    sphere(f"EyeBlack_{side}", (-1.56, y * 1.1, 2.12), (0.036, 0.016, 0.082), BLACK, head, 24, 16)
    sphere(f"EyeHighlight_{side}", (-1.58, y * 1.13, 2.17), (0.014, 0.009, 0.019), WHITE, head, 16, 10)

for side, y in (("L", -0.3), ("R", 0.3)):
    bpy.ops.mesh.primitive_cone_add(
        vertices=3,
        radius1=0.27,
        radius2=0.035,
        depth=0.58,
        location=(-1.2, y, 2.43),
        rotation=(0.0, 0.08 if side == "L" else -0.08, 0.0),
    )
    ear = bpy.context.object
    ear.name = f"Fur_Ear_{side}"
    ear.scale.y = 0.72
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ear.data.materials.append(FUR)
    bevel = ear.modifiers.new("SoftEarEdges", "BEVEL")
    bevel.width = 0.025
    bevel.segments = 3
    bpy.context.view_layer.objects.active = ear
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    smooth(ear)
    preserve_world_parent(ear, head)

for index, (x, height, angle) in enumerate(
    ((-0.55, 0.34, -0.16), (-0.25, 0.4, -0.08), (0.06, 0.43, 0.0),
     (0.36, 0.4, 0.08), (0.63, 0.32, 0.15))
):
    for side, y in (("L", -0.515), ("R", 0.515)):
        surface_patch(
            f"Stripe_Body_{index}_{side}", x, y, 1.33, 0.075, height, belly, angle
        )

for side, y in (("L", -0.49), ("R", 0.49)):
    for index, z_offset in enumerate((-0.09, 0.0, 0.09)):
        segment(
            f"Whisker_{side}_{index}",
            (-1.65, y, 1.88 + z_offset * 0.35),
            (-2.15, y, 1.88 + z_offset),
            0.007,
            WHITE,
            head,
            16,
        )
    segment(
        f"Mouth_{side}",
        (-1.75, y, 1.88),
        (-1.59, y, 1.79),
        0.011,
        DARK,
        head,
        16,
    )

leg_specs = (
    ("L_Leg", -0.83, -0.34),
    ("R_Leg", -0.83, 0.34),
    ("L_BLeg", 0.73, -0.34),
    ("R_BLeg", 0.73, 0.34),
)
for prefix, x, y in leg_specs:
    upper = bone(prefix + "_Upper", (x, y, 1.05), chest if "BLeg" not in prefix else butt)
    lower = bone(prefix + "_Lower", (x, y, 0.58), upper)
    foot = bone(prefix.replace("_Leg", "_Foot").replace("_BLeg", "_BFoot"), (x - 0.1, y, 0.18), lower)
    segment("Fur_" + prefix + "_Upper", (x, y, 1.08), (x, y, 0.5), 0.19, FUR, upper)
    segment("Fur_" + prefix + "_Lower", (x, y, 0.67), (x - 0.03, y, 0.2), 0.165, FUR, lower)
    sphere("White_" + prefix + "_Paw", (x - 0.1, y, 0.15), (0.23, 0.19, 0.145), WHITE, foot, 32, 20)

tail1 = bone("tail1", (1.13, 0.0, 1.42), butt)
tail2 = bone("tail2", (1.55, 0.0, 1.7), tail1)
tail_points = (
    (1.02, 0.0, 1.43),
    (1.43, 0.0, 1.35),
    (1.82, 0.0, 1.2),
    (2.18, 0.0, 1.18),
    (2.5, 0.0, 1.32),
)
curve_tube(
    "Fur_Tail_Base",
    tail_points[:3],
    0.165,
    tail1,
    (1.08, 1.0, 0.9),
)
curve_tube(
    "Fur_Tail_Tip",
    tail_points[2:],
    0.145,
    tail2,
    (1.08, 0.9, 0.72),
)
sphere(
    "Fur_Tail_End",
    tail_points[-1],
    (0.105, 0.105, 0.105),
    FUR,
    tail2,
    28,
    18,
)
for index in range(1, len(tail_points) - 1):
    direction = Vector(tail_points[index + 1]) - Vector(tail_points[index - 1])
    tail_ring(
        f"Stripe_Tail_{index}",
        tail_points[index],
        direction,
        0.17 - index * 0.018,
        tail1 if index < 2 else tail2,
    )

parent_map = {obj: obj.parent for obj in bpy.context.scene.objects}
world_map = {obj: obj.matrix_world.copy() for obj in bpy.context.scene.objects}
for obj in bpy.context.scene.objects:
    obj.parent = None
    obj.matrix_world = world_map[obj]

scene_rotation = Matrix.Rotation(-math.pi * 0.5, 4, "X")
for obj in bpy.context.scene.objects:
    obj.matrix_world = scene_rotation @ obj.matrix_world

model.matrix_world = Matrix.Identity(4)


def original_depth(obj):
    depth = 0
    parent = parent_map[obj]
    while parent is not None:
        depth += 1
        parent = parent_map[parent]
    return depth


for obj in sorted(bpy.context.scene.objects, key=original_depth):
    parent = parent_map[obj]
    if parent is not None:
        preserve_world_parent(obj, parent)

for obj in bpy.context.scene.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = model
bpy.ops.export_scene.fbx(
    filepath=OUTPUT,
    use_selection=True,
    global_scale=1.0,
    apply_unit_scale=False,
    add_leaf_bones=False,
    bake_anim=False,
    axis_forward="-Y",
    axis_up="Z",
    path_mode="COPY",
    embed_textures=False,
)
print(f"EXPORTED_TABBY_CAT={OUTPUT}")
