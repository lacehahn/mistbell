"""Gergeti rebuild; Blender only, metres / Z up.

Run: blender -b --python spike/tools/build_church.py
Verify saved files: blender -b --python spike/tools/build_church.py -- --verify
The explicit placement datum takes precedence over the incompatible entrance-origin
request: nave centre (0,-1), front -Y, terrace datum (0,-6.5,0); raised entrance floor z=1.4.
Terrace occupies z=-1.2..0, so the complete height is exactly 26.6m.
"""
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
BLEND = ROOT / 'spike/blend/gergeti_church.blend'
FBX = ROOT / 'spike/export/gergeti_church.fbx'
VIEWS = BLEND.parent / 'views'
ARCH_SEGMENTS = 64


def activate(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def normals(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def material(name, color):
    rgb = [int(color[i:i+2], 16) / 255 for i in (0, 2, 4)]
    rgb = [v / 12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Roughness'].default_value = .85
    mat['source_srgb'] = '#' + color
    return mat


def mesh(name, vertices, faces, mat=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    if mat:
        data.materials.append(mat)
    normals(obj)
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
        radius2=top_radius, depth=height, location=(*xy, bottom+height/2),
        rotation=(0, 0, math.pi/sides))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def arch(name, centre, width, height, depth, rotation=0):
    """Watertight outward-facing cutter: semicircle + vertical jambs."""
    r = width/2
    profile = [(-r, 0), (r, 0)] + [
        (r*math.cos(math.pi*i/ARCH_SEGMENTS),
         height-r+r*math.sin(math.pi*i/ARCH_SEGMENTS))
        for i in range(ARCH_SEGMENTS+1)]
    n = len(profile)
    vertices = [(x,y,z) for y in (-depth/2, depth/2) for x,z in profile]
    faces = [tuple(range(n)), tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    obj = mesh(name,vertices,faces)
    obj.location = centre
    obj.rotation_euler.z = rotation
    return obj


def subtract(target, cutter):
    activate(target)
    mod = target.modifiers.new('True_arch_DIFFERENCE','BOOLEAN')
    mod.operation = 'DIFFERENCE'
    mod.solver = 'EXACT'
    mod.object = cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter,do_unlink=True)
    assigned = target.data.materials[0]
    target.data.materials.clear()
    target.data.materials.append(assigned)
    for face in target.data.polygons:
        face.material_index = 0
    normals(target)
    assert len(target.data.polygons), target.name
    target['applied_boolean_count'] = target.get('applied_boolean_count',0)+1


def bevel(obj, amount=.035):
    activate(obj)
    mod = obj.modifiers.new('Stone_edge_highlights','BEVEL')
    mod.width = amount
    mod.segments = 4
    mod.limit_method = 'ANGLE'
    mod.angle_limit = math.radians(25)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def cross(name, xy, bottom, height, mat):
    box(name+'_Stem',(*xy,bottom+height/2),(.13*height,.13*height,height),mat)
    box(name+'_Arms',(*xy,bottom+.7*height),(.65*height,.13*height,.13*height),mat)


def bounds(objects):
    pts = [o.matrix_world @ Vector(p) for o in objects for p in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    return lo,hi


def triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def openings_check():
    """Ray tests sample jamb-height and curved head, plus solid arch shoulders.
    Test only the relevant wall, so opposite walls cannot mask a true opening.
    Objects have baked identity transforms before this function runs.
    """
    scene = bpy.context.scene
    def probe(name,centre,width,height,normal,depth):
        obj = scene.objects[name]
        normal = Vector(normal)
        tangent = Vector((-normal.y,normal.x,0))
        centre = Vector(centre)
        inverse = obj.matrix_world.inverted()
        def hit(start):
            end = start + normal*(depth+.2)
            local_start = inverse @ start
            local_delta = inverse @ end - local_start
            return obj.ray_cast(local_start,local_delta.normalized(),
                                distance=local_delta.length)[0]
        r = width/2
        for u,z in [(0,.2),(0,height-.08),(.75*r,height-r+.35*r),
                    (-.75*r,height-r+.35*r),(.8*r,.5)]:
            start = centre + tangent*u + Vector((0,0,z)) - normal*(depth/2+.1)
            assert not hit(start), (name,u,z,'blocked')
        # Outside the circular head must remain solid: catches rectangular cuts.
        start = centre+tangent*(.9*r)+Vector((0,0,height-.05))-normal*(depth/2+.1)
        assert hit(start), (name,'missing shoulder')
    probe('Nave_Front',(0,-6.2,1.4),3,4.2,(0,1,0),.6)
    probe('Porch_Arch',(0,-8.15,1.4),3.2,4.6,(0,1,0),.3)
    for x in (-4.8,4.8):
        probe('Nave_Front',(x,-6.2,5.3),1,2.4,(0,1,0),.6)
    probe('Gable_Stone',(0,-6.2,13.1),.9,1.6,(0,1,0),.6)
    for name,x in [('Nave_West',-8.7),('Nave_East',8.7)]:
        for y in (-3.4,1.4):
            probe(name,(x,y,5.4),.9,2.2,(1,0,0),.6)
    probe('Nave_Back',(0,4.2,5),1.2,2.4,(0,1,0),.6)
    for i in range(8):
        angle=i*math.pi/4
        nx,ny=math.sin(angle),math.cos(angle)
        probe('BellTower_8',(10+nx*2.9,-6+ny*2.9,9.4),
              2.2,3.2,(nx,ny,0),1.6)
    print('[VERIFY] 8 belfry arches + porch/door/8 windows: clear rays and solid shoulders PASS')


def facade_check():
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    steps=sorted((o for o in objects if o.name.startswith('Steps_')),key=lambda o:o.name)
    assert len(steps)==5
    for i,o in enumerate(steps):
        lo,hi=bounds([o])
        assert abs((hi-lo).x-8)<.001
        assert abs((hi-lo).y-.45)<.001
        assert abs(hi.z-(i+1)*.28)<.001 and abs(lo.z)<.001
    lo,hi=bounds([bpy.data.objects['Gable_Stone']])
    assert abs(hi.z-16)<.12 and abs(lo.z-12.5)<.001
    porch=[o for o in objects if 'porch' in o.name.lower()]
    lo,hi=bounds(porch)
    assert all(abs(a-b)<.001 for a,b in zip(hi-lo,(6,1.8,5.7)))
    assert abs(hi.z-(1.4+5.5))<.001  # slab extends 0.2m below the entrance floor
    assert len([o for o in objects if o.name.startswith('Porch_Column')])==2
    assert len([o for o in objects if 'Window_Wood' in o.name])==7
    assert bpy.data.objects['BellTower_8'].get('applied_boolean_count')==9
    for o in objects:
        uv=o.data.uv_layers.active
        assert uv and len(uv.data)==len(o.data.loops), o.name
        assert all(math.isfinite(c) for t in uv.data for c in t.uv), o.name
    print('[VERIFY] gable + porch 6x1.8x5.5m / two columns + five 0.28x0.45m steps + seven recessed windows + full finite UVs PASS')


def prepare():
    objects = [o for o in bpy.context.scene.objects if o.type=='MESH']
    for obj in objects:
        activate(obj)
        # Actual edge geometry, not a face-count subdivision, gives stone silhouettes.
        bevel(obj,.025 if 'Cross' in obj.name else .035)
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        normals(obj)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.quads_convert_to_tris(quad_method='BEAUTY',ngon_method='BEAUTY')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.02)
        bpy.ops.object.mode_set(mode='OBJECT')
        assert obj.data.uv_layers.active and not obj.modifiers
        assert len(obj.data.materials)==1
        bm=bmesh.new()
        bm.from_mesh(obj.data)
        assert all(e.is_manifold for e in bm.edges), obj.name
        bm.free()
    return objects


def render_views(objects):
    scene=bpy.context.scene
    scene.render.engine='BLENDER_WORKBENCH'
    shade=scene.display.shading
    shade.light='STUDIO'
    shade.studio_light='paint.sl'
    shade.color_type='MATERIAL'
    shade.show_shadows=True
    shade.show_cavity=True
    shade.cavity_type='BOTH'
    shade.show_specular_highlight=False
    shade.background_type='WORLD'
    scene.world.color=(.18,.18,.18)
    scene.display.render_aa='32'
    scene.view_settings.view_transform='Standard'
    scene.render.resolution_x=1024
    scene.render.resolution_y=1024
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.render.film_transparent=False
    VIEWS.mkdir(parents=True,exist_ok=True)
    lo,hi=bounds(objects)
    target=(lo+hi)/2
    directions={'front':(0,-1,0),'side':(1,0,0),'top':(0,0,1),'iso':(1,-1,.8)}
    cameras=[]
    for name,direction in directions.items():
        data=bpy.data.cameras.new('View_'+name)
        cam=bpy.data.objects.new('View_'+name,data)
        scene.collection.objects.link(cam)
        cam.location=target+Vector(direction).normalized()*90
        cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
        data.type='ORTHO'
        # Fit all corners in camera space, using the same centre in every view.
        inv=cam.rotation_euler.to_matrix().transposed()
        pts=[inv@(o.matrix_world@Vector(p)-target) for o in objects for p in o.bound_box]
        data.ortho_scale=2*max(max(abs(p.x),abs(p.y)) for p in pts)*1.13
        data.clip_end=300
        scene.camera=cam
        scene.render.filepath=str(VIEWS/(name+'.png'))
        bpy.ops.render.render(write_still=True)
        cameras.append(cam)
    scene.camera=cameras[-1]
    print('[VERIFY] 4 orthographic Workbench PNG views rendered at 1024x1024')


def report(objects):
    lo,hi=bounds(objects)
    size=hi-lo
    total=sum(triangle_count(o) for o in objects)
    front=triangle_count(bpy.data.objects['Nave_Front'])
    bell=triangle_count(bpy.data.objects['BellTower_8'])
    valid=all(abs(a-b)<=3 for a,b in zip(size,(34,24,27)))
    print('[CHURCH] triangles=%d bbox=%.3fx%.3fx%.3f objects=%d bbox_in_range=%s'
          %(total,*size,len(objects),valid))
    print('[CHURCH] nave_front_tris=%d bell_tower_tris=%d'%(front,bell))
    assert valid and 50000 < total < 60000 and front>=120
    assert all(abs(a-b)<.001 for a,b in zip(size,(34,24,26.6))), tuple(size)
    assert set(bpy.data.materials)=={bpy.data.materials[n] for n in ('Stone','Roof','Wood')}
    return total,list(size)


def build():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)
    scene=bpy.context.scene
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    scene.unit_settings.length_unit='METERS'
    scene['origin_convention']='Placement datum: nave (0,-1), front -Y; terrace ground (0,-6.5,0); porch/door floor z=1.4'
    stone=material('Stone','C9A87C')
    roof=material('Roof','A06B4A')
    wood=material('Wood','6B4A32')
    box('Terrace',(0,-1,-.6),(34,24,1.2),stone)
    front=box('Nave_Front',(0,-6.2,6.25),(18,.6,12.5),stone)
    subtract(front,arch('Door_Cutter',(0,-6.2,1.4),3,4.2,1.4))
    for x in (-4.8,4.8):
        subtract(front,arch('Front_Window_Cutter',(x,-6.2,5.3),1,2.4,1.4))
        backing=arch('Front_Window_Wood',(x,-6.2,5.3),1,2.4,.1)
        backing.data.materials.append(wood)
    door=arch('Door_Wood',(0,-5.85,1.4),3,4.2,.1)
    door.data.materials.append(wood)
    back=box('Nave_Back',(0,4.2,6.25),(18,.6,12.5),stone)
    subtract(back,arch('Rear_Window_Cutter',(0,4.2,5),1.2,2.4,1.4))
    for name,x in [('Nave_West',-8.7),('Nave_East',8.7)]:
        wall=box(name,(x,-1,6.25),(.6,9.8,12.5),stone)
        for y in (-3.4,1.4):
            subtract(wall,arch('Side_Window_Cutter',(x,y,5.4),.9,2.2,1.4,math.pi/2))
            backing=arch('Side_Window_Wood',(x,y,5.4),.9,2.2,.1,math.pi/2)
            backing.data.materials.append(wood)
    # Front gable rises 3.5m above the unchanged 12.5m eave.
    gable=mesh('Gable_Stone',[(x,y,z) for y in (-6.5,-5.9)
        for x,z in [(-9,12.5),(9,12.5),(0,16)]],
        [(0,1,2),(3,5,4),(0,3,4,1),(1,4,5,2),(2,5,3,0)],stone)
    subtract(gable,arch('Gable_Window_Cutter',(0,-6.2,13.1),.9,1.6,1.4))
    backing=arch('Gable_Window_Wood',(0,-6.2,13.1),.9,1.6,.1)
    backing.data.materials.append(wood)
    # Matching roof slopes, 0.25m thick and 0.4m overhang.
    for side in (-1,1):
        mesh('Gable_Roof',[(x,y,z) for y in (-6.9,4.9)
             for x,z in [(0,16),(side*9.4,12.344),(side*9.4,12.594),(0,16.25)]],
             [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],roof)
    # Porch floor is five 0.28m risers above the existing terrace.
    box('Porch_Floor',(0,-7.4,1.3),(6,1.8,.2),stone)
    for x in (-2.525,2.525):
        box('Porch_Column',(x,-7.95,3.6),(.7,.7,4.4),stone)
        box('Porch_Capital',(x,-7.95,5.875),(.95,.7,.15),stone)
    porch=box('Porch_Arch',(0,-8.15,5.275),(6,.3,2.75),stone)
    subtract(porch,arch('Porch_Cutter',(0,-8.15,1.4),3.2,4.6,1))
    box('Porch_Roof',(0,-7.4,6.775),(6,1.8,.25),roof)
    for i in range(5):
        height=(i+1)*.28
        box('Steps_%02d'%(i+1),(0,-10.55+i*.45+.225,height/2),
            (8,.45,height),stone)
    cone('Drum_12',(0,-1),12.5,4.3,4.6,12,stone,4.6)
    cone('Dome_12',(0,-1),16.8,7,4.6,12,roof)
    cross('Dome_Cross',(0,-1),23.8,1.6,stone)
    tower=cone('BellTower_8',(10,-6),0,13,3.2,8,stone,3.2)
    core=cone('Belfry_Core',(10,-6),9.35,3.85,2.5,8,stone,2.5)
    subtract(tower,core)
    for i in range(8):
        theta=i*math.pi/4
        nx,ny=math.sin(theta),math.cos(theta)
        angle=math.atan2(-nx,ny)
        subtract(tower,arch('Belfry_Cutter',(10+nx*2.9,-6+ny*2.9,9.4),
                             2.2,3.2,2,angle))
    cone('BellTower_Roof_8',(10,-6),13,3.2,3.2,8,roof)
    cross('BellTower_Cross',(10,-6),16.2,1,stone)
    # Annex x=-17..-10, y=-8..6, highest roof point z=5.
    # Hollow walls, single slope roof; high side faces the nave.
    mesh('Annex_West',[(-17,y,z) for y,z in [(-8,0),(6,0),(6,4.1),(-8,4.1)]]+
         [(-16.4,y,z) for y,z in [(-8,0),(6,0),(6,4.1),(-8,4.1)]],
         [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],stone)
    box('Annex_East',(-10.3,-1,2.4),(.6,14,4.8),stone)
    for name,y in [('Front',-8),('Back',5.4)]:
        mesh('Annex_'+name,[(x,yy,z) for yy in (y,y+.6)
             for x,z in [(-16.4,0),(-10.6,0),(-10.6,4.74),(-16.4,4.16)]],
             [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],stone)
    mesh('Annex_SingleSlope_Roof',[(x,y,z) for y in (-8,6)
         for x,z in [(-17,4),(-10,4.7),(-10,5),(-17,4.3)]],
         [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],roof)
    # Stone courses/cornices add readable scale, with real bevelled geometry.
    for z in (1,4.1,8.5,12.1):
        # Front cornice is split around the doorway below its crown.
        if z<5.6:
            for side in (-1,1):
                box('Front_Course', (side*5.25,-6.52,z),(7.3,.12,.16),stone)
        else:
            box('Front_Course',(0,-6.52,z),(18,.12,.16),stone)
        box('Rear_Course',(0,4.52,z),(18,.12,.16),stone)
    # Rear course at z=4.1 is below the window sill; others clear its head.
    objects=prepare()
    openings_check()
    facade_check()
    total,size=report(objects)
    scene['triangle_count']=total
    scene['bbox_xyz']=size
    BLEND.parent.mkdir(parents=True,exist_ok=True)
    FBX.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    # Unity normalizes FBX axis metadata, mapping Blender -Y to Unity +Z.
    # Bake a half-turn for the export only, so the church front faces Unity -Z
    # and the existing downhill path/cameras. Keep the BLEND datum unchanged.
    export_turn=Matrix.Rotation(math.pi,4,'Z')
    for o in objects:
        o.data.transform(export_turn)
    try:
        bpy.ops.export_scene.fbx(filepath=str(FBX),use_selection=True,object_types={'MESH'},
            global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
            axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,
            mesh_smooth_type='FACE',use_custom_props=True,use_triangles=True,
            add_leaf_bones=False,bake_anim=False)
    finally:
        for o in objects:
            o.data.transform(export_turn.inverted())
    bpy.context.view_layer.update()
    render_views(objects)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    print('[CHURCH] saved=%s exported=%s'%(BLEND,FBX))


def verify():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    expected,size=report(objects)
    openings_check()
    facade_check()
    assert all(o.matrix_world.is_identity and o.data.uv_layers.active for o in objects)
    for name in ('front','side','top','iso'):
        img=bpy.data.images.load(str(VIEWS/(name+'.png')),check_existing=False)
        assert tuple(img.size)==(1024,1024)
        bpy.data.images.remove(img)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(FBX))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    lo,hi=bounds(objects)
    assert all(abs(a-b)<.001 for a,b in zip(hi-lo,size))
    assert sum(triangle_count(o) for o in objects)==expected
    assert all(o.data.uv_layers.active for o in objects)
    # Undo the documented Unity-facing export half-turn for source-space ray tests.
    for o in objects:
        activate(o)
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        o.data.transform(Matrix.Rotation(-math.pi,4,'Z'))
    bpy.context.view_layer.update()
    openings_check()
    facade_check()
    print('[VERIFY] saved BLEND + FBX metre/triangle/UV round-trip + four PNG sizes PASS')


if __name__=='__main__':
    try:
        verify() if '--verify' in sys.argv else build()
    except Exception:
        import traceback
        traceback.print_exc()
        sys.stdout.flush()
        sys.stderr.flush()
        sys.exit(1)
