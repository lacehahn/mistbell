import bpy, math, sys
from mathutils import Vector
from pathlib import Path

ROOT = Path('/Users/lance/workspace/mistbell')
FBX = ROOT / 'spike/export/gergeti_altar.fbx'
OUT = ROOT / 'spike/artifacts/altar'

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

bpy.ops.import_scene.fbx(filepath=str(FBX))
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
print('[TEST] imported', [o.name for o in objs])
for o in objs:
    o.select_set(True)
if objs:
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
obj = bpy.context.object
# 居中
bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
ctr = sum(bb, Vector()) / 8
obj.location -= ctr
bpy.ops.object.transform_apply(location=True)

sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'SINGLE'
sc.display.shading.single_color = (0.62, 0.57, 0.50)
sc.display.shading.show_shadows = True
sc.display.shading.show_cavity = True
sc.render.resolution_x = sc.render.resolution_y = 1000
sc.world = bpy.data.worlds.new('w')

cam_d = bpy.data.cameras.new('cam'); cam_d.lens = 50
cam = bpy.data.objects.new('cam', cam_d)
sc.collection.objects.link(cam); sc.camera = cam
OUT.mkdir(parents=True, exist_ok=True)

def look_at(o, target):
    d = (o.location - target)
    o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()

for name, loc in (('altar_34', Vector((3.1, 1.9, -4.6))), ('altar_front', Vector((0.01, 1.35, -5.6)))):
    cam.location = loc
    look_at(cam, Vector((0, 0.62, 0)))
    sc.render.filepath = str(OUT / f'{name}.png')
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(sc.render.filepath)
    px = list(img.pixels)[:4000]
    print('[TEST] shot', name, 'mean', sum(px) / len(px))
