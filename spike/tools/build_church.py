"""Sheet v2 structural rebuild, metres / Z up, no external dependencies.
Local front/north is -Y, rear/south +Y. Crossing is (-2,0), deliberately
left of the placement datum to keep the fixed bell tower (+10.5,-2) clear.
Overall bounds include the western annex, steps and detached tower; no terrace
slab is counted as architecture. Run with --verify for saved asset round-trip.
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


def ray_hit(obj, start, direction, distance):
    inv = obj.matrix_world.inverted()
    origin = inv @ Vector(start)
    delta = inv.to_3x3() @ Vector(direction)
    return obj.ray_cast(origin, delta.normalized(), distance=distance)[0]


def openings_check():
    # Test the whole recessed passage, not just the final wall cutter.
    portal = [o for o in bpy.context.scene.objects if o.name.startswith('Portal_Layer')]
    wall = bpy.data.objects['Main_Shell']
    for x,z in [(-2,1.6),(-2,4.02),(-1.4,2),(-2.6,2)]:
        for obj in portal+[wall]:
            assert not ray_hit(obj,(x,-9.65,z),(0,1,0),2.5), (obj.name,x,z)
    assert ray_hit(wall,(-.3,-8.5,4.4),(0,1,0),1.3), 'missing solid shoulder'
    tower = bpy.data.objects['BellTower_8']
    for direction in [(1,0,0),(-1,0,0),(0,1,0),(0,-1,0)]:
        n=Vector(direction)
        for u,z in [(0,8.7),(0,11.1),(.55,9.3),(-.55,9.3)]:
            tangent=Vector((-n.y,n.x,0))
            start=Vector((10.5,-2,z))+n*2.2+tangent*u
            assert not ray_hit(tower,start,-n,4.4), ('belfry blocked',direction,u,z)
    assert len(portal)==3
    assert len([o for o in bpy.context.scene.objects if o.name.startswith('Steps_')])==5
    assert len([o for o in bpy.context.scene.objects if o.name.startswith('Lamp_Base')])==2
    print('[VERIFY] recessed portal + four through belfry arches ray tests PASS')


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
        bpy.ops.object.mode_set(mode='OBJECT')
        # Dominant-axis box projection in world metres, including boolean faces.
        uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')
        for face in obj.data.polygons:
            axis = max(range(3), key=lambda i: abs(face.normal[i]))
            axes = ((1, 2), (0, 2), (0, 1))[axis]
            for loop_index in face.loop_indices:
                point = obj.matrix_world @ obj.data.vertices[obj.data.loops[loop_index].vertex_index].co
                uv.data[loop_index].uv = (point[axes[0]], point[axes[1]])
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
    bpy.context.view_layer.update()
    lo,hi=bounds(objects)
    size=hi-lo
    total=sum(triangle_count(o) for o in objects)
    main=[o for o in objects if not o.name.startswith('BellTower')]
    bell=[o for o in objects if o.name.startswith('BellTower')]
    ml,mh=bounds(main)
    bl,bh=bounds(bell)
    gap=math.hypot(max(ml.x-bh.x,bl.x-mh.x,0),max(ml.y-bh.y,bl.y-mh.y,0))
    layers=[bpy.data.objects['Portal_Layer_%d'%i] for i in range(3)]
    pl,ph=bounds(layers)
    # Continuous open air passage measured from the outer jamb to inner wall face.
    depth=-7.4-pl.y
    valid=all(abs(a-b)<=2 for a,b in zip(size,(28,22,24.5)))
    print(' [CHURCH2] bbox=%.3fx%.3fx%.3f bbox_ok=%s tris=%d tower_gap=%.3f portal_depth=%.3f '
          %(*size,valid,total,gap,depth),flush=True)
    assert valid and total>=25000 and gap>=3 and depth>=1.2
    assert bpy.data.objects['BellTower_8']['applied_boolean_count']==3
    assert bpy.data.objects['Main_Shell']['nave_size']=='16x9'
    return total,list(size)


def union(target, other):
    activate(target)
    mod=target.modifiers.new('Cross_plan_UNION','BOOLEAN')
    mod.operation='UNION'
    mod.solver='EXACT'
    mod.object=other
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(other,do_unlink=True)
    normals(target)


def gable(name, cx, cy, width, length, eave, ridge, mat, across=False):
    vertices=[(x,y,z) for y in (-length/2,length/2)
              for x,z in [(-width/2,eave),(width/2,eave),(0,ridge)]]
    if across:
        vertices=[(cx+y,cy+x,z) for x,y,z in vertices]
    else:
        vertices=[(cx+x,cy+y,z) for x,y,z in vertices]
    return mesh(name,vertices,[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],mat)


def arch_stones(prefix,cx,y,bottom,width,height,stone):
    # Individual wedge voussoirs, each bevelled: silhouette geometry, no padding subdivision.
    r=width/2
    for i in range(32):
        a=math.pi*i/32+.004
        b=math.pi*(i+1)/32-.004
        verts=[(cx+rr*math.cos(t),yy,bottom+height-r+rr*math.sin(t))
               for yy in (y-.16,y+.16) for rr,t in [(r,a),(r,b),(r+.27,b),(r+.27,a)]]
        mesh(prefix+'_Voussoir',verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),
              (1,2,6,5),(2,3,7,6),(3,0,4,7)],stone)
    for side in (-1,1):
        h=height-r
        for j in range(math.ceil(h/.42)):
            n=math.ceil(h/.42)
            box(prefix+'_Jamb',(cx+side*(r+.135),y,bottom+(j+.5)*h/n),
                (.27,.32,h/n-.018),stone)


def build():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)
    scene=bpy.context.scene
    scene.unit_settings.system='METRIC'
    scene.unit_settings.scale_length=1
    scene.unit_settings.length_unit='METERS'
    scene['origin_convention']='Crossing (-2,0); front -Y / local north; apse +Y / local south; tower (10.5,-2)'
    stone=material('Stone','C9A87C')
    roof=material('Roof','7D8393')
    wood=material('Wood','6B4A32')
    # A continuous hollow cross shell, with no solid intersecting internal walls.
    shell=box('Main_Shell',(-2,0,5.5),(9,16,11),stone)
    union(shell,box('Transept_Union',(-2,0,5.5),(14,7,11),stone))
    apse=cone('Apse_Union',(-2,8),0,9.8,3.2,64,stone,3.2)
    # Union overlap is the inner half circle; only the rear semicircle is exposed.
    union(shell,apse)
    inside=box('Interior_Cross',(-2,0,6.1),(7.8,14.8,10.2),stone)
    union(inside,box('Interior_Transept',(-2,0,6.1),(12.8,5.8,10.2),stone))
    union(inside,cone('Interior_Apse',(-2,8),1,8.2,2.6,64,stone,2.6))
    subtract(shell,inside)
    shell['nave_size']='16x9'
    shell['transept_size']='14x7'
    shell['apse_radius']=3.2
    subtract(shell,arch('Door_Through',(-2,-8,1),1.9,3.1,3.6))
    # Three separately cut, progressively smaller recesses, ending at the wall passage.
    for i,(w,h) in enumerate([(2.8,4.2),(2.3,3.6),(1.9,3.1)]):
        y=-9.2+i*.6
        layer=box('Portal_Layer_%d'%i,(-2,y,3.35),(4.4,.6,4.7),stone)
        subtract(layer,arch('Portal_Cutter',(-2,y,1),w,h,.9))
        arch_stones('Portal_%d'%i,-2,y-.29,1,w,h,stone)
    for side in (-1,1):
        box('Lamp_Base',(-2+side*2.48,-8.65,3),(.3,.3,.5),stone)
    for i in range(5):
        height=(i+1)*.2
        box('Steps_%02d'%i,(-2,-10.65+i*.3,height/2),(5,.3,height),stone)
    # Fenestration: actual windows through the hollow shell.
    for side in (-1,1):
        for y in (-5.5,5.6):
            x=-2+side*4.5
            subtract(shell,arch('Nave_Window',(x,y,5.4),.95,2.6,1.5,math.pi/2))
            arch_stones('Front_Window',-2+side*3,-8.04,5.4,.85,2.4,stone)
            subtract(shell,arch('Front_Window_Cut',(-2+side*3,-8,5.4),.85,2.4,1.5))
    # Crossed pitched roofs are united for a clean intersection.
    roofs=gable('Cross_Roof',-2,0,9.4,16.4,11,12.5,roof)
    union(roofs,gable('Transept_Roof',-2,0,7.4,14.4,11,12.5,roof,True))
    cone('Apse_Roof',(-2,8),9.8,2.2,3.4,64,roof)
    # Main tower exact requested stack: 12.5+4+6.5+1.5 = 24.5.
    drum=cone('Drum_12',(-2,0),12.5,4,4.2,12,stone,4.2)
    for i in range(12):
        angle=i*math.tau/12
        nx,ny=math.cos(angle),math.sin(angle)
        subtract(drum,arch('Drum_Window',(-2+nx*4,ny*4,13.1),.65,2.5,1,
                           math.atan2(-nx,ny)))
    cone('Dome_12',(-2,0),16.5,6.5,4.2,12,roof)
    cross('Dome_Cross',(-2,0),23,1.5,stone)
    # Detached bell tower, hollow belfry with four real cardinal through openings.
    tower=cone('BellTower_8',(10.5,-2),0,12,1.9,8,stone,1.9)
    subtract(tower,cone('Bell_Core',(10.5,-2),8.3,4,1.35,8,stone,1.35))
    for rotation in (0,math.pi/2):
        subtract(tower,arch('Bell_Through',(10.5,-2,8.3),1.6,3,5,rotation))
    cone('BellTower_Roof',(10.5,-2),12,3,1.9,8,roof)
    cross('BellTower_Cross',(10.5,-2),15,1,stone)
    # Wedge trim around the four belfry faces (also leaves their centres open).
    # Tower radius and four openings remain the specified dimensions.
    for side in (-1,1):
        arch_stones('BellTower_Trim',10.5,-2+side*1.7,8.3,1.6,3,stone)
    # Low western side wing establishes 28m overall X extent without crowding tower.
    box('Annex_West',(-12.25,2.5,2.4),(6.5,6,4.8),stone)
    gable('Annex_Roof',-12.25,2.5,6.5,6,4.8,6.2,roof)
    # Pilasters and stacked capitals provide readable masonry scale at this view distance.
    for x,y in [(-6.55,-7.7),(2.55,-7.7),(-9.04,-3),(-9.04,3),
                (5.04,-3),(5.04,3),(-6.55,7.5),(2.55,7.5)]:
        box('Buttress',(x,y,5.1),(.38,.48,10.2),stone)
        for z in (1.1,4.6,8.4,10.2):
            box('Buttress_Capital',(x,y,z),(.54,.62,.18),stone)
    objects=prepare()
    openings_check()
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
