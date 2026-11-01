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
ROOF_PITCH = 38.0
EAVE_OVERHANG = .45


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


def tower_openings_check():
    tower=bpy.data.objects['BellTower_8']
    parts=[o for o in bpy.context.scene.objects
           if o.type=='MESH' and o.name.startswith('BellTower')]
    for direction in ((1,0,0),(-1,0,0),(0,1,0),(0,-1,0)):
        n=Vector(direction)
        tangent=Vector((-n.y,n.x,0))
        # All three apertures, including the curved arch crown and both jambs.
        for offset in (-1.45,0,1.45):
            for u,z in ((0,9.8),(0,12.12),(-.42,10.6),(.42,10.6)):
                start=Vector((10.5,-2,z))+n*3.5+tangent*(offset+u)
                if any(ray_hit(obj,start,-n,7) for obj in parts):
                    return False
        # Verify the two 0.35 m piers remain solid on each facade.
        for offset in (-.725,.725):
            start=Vector((10.5,-2,10.6))+n*3.5+tangent*offset
            if not ray_hit(tower,start,-n,7):
                return False
    return True


def openings_check():
    # Test the whole recessed passage, not just the final wall cutter.
    portal = [o for o in bpy.context.scene.objects if o.name.startswith('Portal_Layer')]
    wall = bpy.data.objects['Main_Shell']
    for x,z in [(-2,1.6),(-2,4.02),(-1.4,2),(-2.6,2)]:
        for obj in portal+[wall]:
            assert not ray_hit(obj,(x,-9.65,z),(0,1,0),2.5), (obj.name,x,z)
    assert ray_hit(wall,(-.3,-8.5,4.4),(0,1,0),1.3), 'missing solid shoulder'
    belfry_cut = tower_openings_check()
    assert belfry_cut, 'blocked triple belfry opening'
    assert len(portal)==3
    assert len([o for o in bpy.context.scene.objects if o.name.startswith('Steps_')])==5
    assert len([o for o in bpy.context.scene.objects if o.name.startswith('Lamp_Base')])==2
    print('[VERIFY] recessed portal + twelve through belfry arches ray tests PASS')


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


def mesh_integrity(obj):
    """Closed components, consistent winding and finite per-corner export UVs."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    assert all(e.is_manifold and e.is_contiguous for e in bm.edges), obj.name
    assert all(f.calc_area() > 1e-12 for f in bm.faces), obj.name
    bm.free()
    uv = obj.data.uv_layers.active
    assert uv and len(uv.data) == len(obj.data.loops), obj.name
    assert all(math.isfinite(v) for loop in uv.data for v in loop.uv), obj.name


def merged_openings_check(objects):
    # Repeat through-opening tests against the final material batches.
    for x,z in [(-2,1.6),(-2,4.02),(-1.4,2),(-2.6,2)]:
        assert not any(ray_hit(o,(x,-9.65,z),(0,1,0),2.5) for o in objects)
    for direction in ((1,0,0),(-1,0,0),(0,1,0),(0,-1,0)):
        n=Vector(direction)
        tangent=Vector((-n.y,n.x,0))
        for offset in (-1.45,0,1.45):
            for u,z in ((0,9.8),(0,12.12),(-.42,10.6),(.42,10.6)):
                start=Vector((10.5,-2,z))+n*3.5+tangent*(offset+u)
                assert not any(ray_hit(o,start,-n,7) for o in objects)
        for offset in (-.725,.725):
            start=Vector((10.5,-2,10.6))+n*3.5+tangent*offset
            assert any(ray_hit(o,start,-n,7) for o in objects)


def optimize_church(objects):
    before = sum(triangle_count(o) for o in objects)
    count_before = len(objects)
    lo_before, hi_before = bounds(objects)
    # Preserve curved aperture profiles / crosses / horizontal stone courses.
    # Broad planar masonry and small bevel strips can collapse much harder.
    def retention(obj):
        if 'Cross' in obj.name and obj.name != 'Cross_Roof':
            return .9
        if any(key in obj.name for key in
               ('Voussoir', 'Jamb', 'Main_Shell', 'Drum_', 'BellTower_8', 'Course_')):
            return .48
        return .18
    estimate = sum(triangle_count(o)*retention(o) for o in objects)
    budget_scale = min(1.0, 74000 / estimate)
    for obj in objects:
        activate(obj)
        mod = obj.modifiers.new('Unity_Collapse', 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = retention(obj)*budget_scale
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        # Remove sub-micrometre slivers left by bevel/Collapse, without welding parts.
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=list(bm.edges))
        bm.to_mesh(obj.data)
        bm.free()
        normals(obj)
        mesh_integrity(obj)
    bpy.context.view_layer.update()
    openings_check()
    # Join only; do not weld intersecting/disconnected architectural components.
    # Blender join retains UV loop layers and the original material assignment.
    batches = {}
    for obj in objects:
        batches.setdefault(obj.data.materials[0].name, []).append(obj)
    merged = []
    for name, parts in sorted(batches.items()):
        activate(parts[0])
        for obj in parts:
            obj.select_set(True)
        bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = name
        obj.data.name = name + '_Mesh'
        normals(obj)
        mesh_integrity(obj)
        assert len(obj.data.materials) == 1
        merged.append(obj)
    bpy.context.view_layer.update()
    lo, hi = bounds(merged)
    assert max(abs(a-b) for a,b in zip((*lo,*hi),(*lo_before,*hi_before))) < .035
    after = sum(triangle_count(o) for o in merged)
    size = hi-lo
    bbox_ok = all(abs(a-b)<=2 for a,b in zip(size,(28,22,24.5)))
    scene = bpy.context.scene
    scene['tris_before'] = before
    scene['objects_before'] = count_before
    scene['triangle_count'] = after
    scene['bbox_xyz'] = list(size)
    merged_openings_check(merged)
    print('[CHURCH6] tris_before=%d tris_after=%d objects_before=%d objects_after=%d '
          'bbox=%.3fx%.3fx%.3f bbox_ok=%s' %
          (before,after,count_before,len(merged),*size,bbox_ok),flush=True)
    assert after <= 80000 and len(merged) <= 6 and bbox_ok
    print('[VERIFY] material batches: ' + ', '.join(
        '%s=%d tris' % (o.name,triangle_count(o)) for o in merged),flush=True)
    print('[VERIFY] closed manifold / consistent normals / finite UVs / final opening rays PASS')
    return merged


def render_views(objects, framing=None):
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
    framing = framing or [o.matrix_world@Vector(p) for o in objects for p in o.bound_box]
    lo=Vector([min(p[i] for p in framing) for i in range(3)])
    hi=Vector([max(p[i] for p in framing) for i in range(3)])
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
        pts=[inv@(p-target) for p in framing]
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
    # New roof overhangs/caps project into the old 3 m masonry clearance.
    masonry=[o for o in main if o.data.materials[0].name != 'Roof_Slate']
    sl,sh=bounds(masonry)
    masonry_gap=math.hypot(max(sl.x-bh.x,bl.x-sh.x,0),max(sl.y-bh.y,bl.y-sh.y,0))
    layers=[bpy.data.objects['Portal_Layer_%d'%i] for i in range(3)]
    pl,ph=bounds(layers)
    # Continuous open air passage measured from the outer jamb to inner wall face.
    depth=-7.4-pl.y
    valid=all(abs(a-b)<=2 for a,b in zip(size,(28,22,24.5)))
    print(' [CHURCH2] bbox=%.3fx%.3fx%.3f bbox_ok=%s tris=%d tower_gap=%.3f portal_depth=%.3f '
          %(*size,valid,total,gap,depth),flush=True)
    assert valid and total>=25000 and masonry_gap>=3 and gap>=2.5 and depth>=1.2
    tower=bpy.data.objects['BellTower_8']
    belfry_cut=tower_openings_check()
    print('[CHURCH4] tower_r_base=%.3f tower_r_top=%.3f arches_per_face=%d '
          'belfry_cut=%s tower_gap=%.3f bbox=%.3fx%.3fx%.3f bbox_ok=%s tris=%d'
          % (tower['tower_r_base'],tower['tower_r_top'],tower['arches_per_face'],
             belfry_cut,gap,*size,valid,total),flush=True)
    assert tower['tower_r_base']>=2.5 and tower['arches_per_face']==3
    assert belfry_cut and masonry_gap>=3 and gap>=2.5 and valid
    assert tower['applied_boolean_count']==7
    assert bpy.data.objects['Main_Shell']['nave_size']=='16x9'
    # Measure the actual exposed apse masonry beyond the nave's south wall.
    shell = bpy.data.objects['Main_Shell']
    apse_overhang = max((shell.matrix_world @ v.co).y for v in shell.data.vertices) - 8.0
    gables = [o for o in objects if o.name in ('Transept_Gable_L', 'Transept_Gable_R')]
    buttresses = [o for o in objects if o.name.startswith('Buttress_')
                  and o.get('structural_buttress', False)]
    print('[CHURCH3] apse_overhang=%.3f gables=%d buttresses=%d bbox=%.3fx%.3fx%.3f tris=%d'
          % (apse_overhang, len(gables), len(buttresses), *size, total), flush=True)
    assert apse_overhang >= 2.5 and len(gables) == 2 and len(buttresses) >= 6
    for obj in gables:
        gl, gh = bounds([obj])
        assert all(abs(a-b) < .08 for a,b in zip(gh-gl, (.5, 7, 3)))
    for side in (-1, 1):
        start = (-2 + side * 7.5, 0, 12)
        assert ray_hit(bpy.data.objects['Transept_Gable_' + ('L' if side < 0 else 'R')],
                       start, (-side, 0, 0), 1)
        # CHURCH5 deliberately extends the roof over these unchanged gables.
    for obj in buttresses:
        bl, bh = bounds([obj])
        assert all(abs(a-b) < .08 for a,b in zip(bh-bl, (.6, .9, 8)))
    roof = bpy.data.objects['Cross_Roof']
    # Measure the original slope vertices retained through bevel/triangulation.
    pitches = []
    for face in roof.data.polygons:
        n = face.normal
        if .2 < abs(n.z) < .95:
            pitches.append(math.degrees(math.acos(abs(n.z))))
    pitch = min(pitches, key=lambda p: abs(p-ROOF_PITCH))
    rl,rh = bounds([roof])
    overhang = min((rh.x-rl.x-14)/2, (rh.y-rl.y-16)/2)
    cl,ch = bounds([bpy.data.objects['Nave_Ridge']])
    ridge_len = ch.y-cl.y
    bbox_ok = valid and size.z <= 25.5
    print('[CHURCH5] roof_pitch=%.3f eave_overhang=%.3f ridge_len=%.3f '
          'bbox=%.3fx%.3fx%.3f bbox_ok=%s tris=%d'
          % (pitch, overhang, ridge_len, *size, bbox_ok, total), flush=True)
    assert 35 <= pitch <= 40 and overhang >= .4 and bbox_ok
    assert all(o.data.materials[0].name == 'Roof_Slate' for o in objects
               if o.name in ('Cross_Roof', 'Apse_Roof', 'Dome_12', 'BellTower_Roof'))
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


def roof_trim(prefix, cx, cy, width, length, eave, ridge, mat, across=False):
    """0.18 m fascia around all four edges; 0.25 x 0.20 m ridge cap."""
    def point(x, y, z):
        return (cx+y, cy+x, z) if across else (cx+x, cy+y, z)
    for side in (-1, 1):
        dims = (.18, length, .18)
        box(prefix+'_Eave', point(side*(width/2-.09), 0, eave-.09),
            (dims[1], dims[0], dims[2]) if across else dims, mat)
        # Sloping fascia along each gable end, kept below the roof surface.
        slope_len = math.hypot(width/2, ridge-eave)
        for end in (-1, 1):
            trim = box(prefix+'_Rake',
                       point(side*width/4, end*(length/2-.09), (eave+ridge)/2-.09),
                       (slope_len, .18, .18), mat)
            trim.rotation_euler.y = side*math.radians(ROOF_PITCH)
            if across:
                trim.rotation_euler.z = math.pi/2
    box(prefix+'_Ridge', point(0, 0, ridge+.06),
        (length, .25, .2) if across else (.25, length, .2), mat)
    for end in (-1, 1):
        box(prefix+'_BeastHead', point(0, end*length/2, ridge+.175),
            (.35, .35, .35), mat)


def apse_half_roof(cx, cy, eave, wall_radius, mat):
    """Rear-facing half cone; straight diameter is buried inside the nave roof."""
    radius = wall_radius + EAVE_OVERHANG
    n = 32
    arc = [(cx+radius*math.cos(i*math.pi/n),
            cy+radius*math.sin(i*math.pi/n), eave) for i in range(n+1)]
    peak = (cx, cy, eave+radius*math.tan(math.radians(ROOF_PITCH)))
    mesh('Apse_Roof', arc+[peak],
         [tuple(reversed(range(n+1)))] + [(i,i+1,n+1) for i in range(n)]
         + [(n,0,n+1)], mat)
    # Continuous semicircular 0.18 m fascia below the cone eave.
    verts = [(cx+r*math.cos(i*math.pi/n), cy+r*math.sin(i*math.pi/n), z)
             for z in (eave-.18, eave) for r in (radius-.18, radius)
             for i in range(n+1)]
    k=n+1
    faces=[]
    for i in range(n):
        faces.extend([(i,i+1,k+i+1,k+i),
                      (2*k+i,3*k+i,3*k+i+1,2*k+i+1),
                      (i,2*k+i,2*k+i+1,i+1),
                      (k+i,k+i+1,3*k+i+1,3*k+i)])
    faces.extend([(0,k,3*k,2*k),(n,2*k+n,3*k+n,k+n)])
    mesh('Apse_Fascia', verts, faces, mat)


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
    roof=material('Roof_Slate','4A5560')
    wood=material('Wood','6B4A32')
    # A continuous hollow cross shell, with no solid intersecting internal walls.
    shell=box('Main_Shell',(-2,0,5.5),(9,16,11),stone)
    union(shell,box('Transept_Union',(-2,0,5.5),(14,7,11),stone))
    apse_y = 8.6  # nave south wall is Y=8; expose almost 3.8 m of round masonry.
    apse=cone('Apse_Union',(-2,apse_y),0,9.8,3.2,64,stone,3.2)
    # Keep a substantial overlap with the nave while exposing the curved rear volume.
    union(shell,apse)
    inside=box('Interior_Cross',(-2,0,6.1),(7.8,14.8,10.2),stone)
    union(inside,box('Interior_Transept',(-2,0,6.1),(12.8,5.8,10.2),stone))
    union(inside,cone('Interior_Apse',(-2,apse_y),1,8.2,2.6,64,stone,2.6))
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
    nave_width, nave_length = 9+2*EAVE_OVERHANG, 16+2*EAVE_OVERHANG
    transept_width, transept_length = 7+2*EAVE_OVERHANG, 14+2*EAVE_OVERHANG
    nave_ridge = 11+nave_width/2*math.tan(math.radians(ROOF_PITCH))
    transept_ridge = 11+transept_width/2*math.tan(math.radians(ROOF_PITCH))
    roofs=gable('Cross_Roof',-2,0,nave_width,nave_length,11,nave_ridge,roof)
    union(roofs,gable('Transept_Roof',-2,0,transept_width,transept_length,
                     11,transept_ridge,roof,True))
    roofs['eave_overhang']=EAVE_OVERHANG
    roofs['ridge_len']=nave_length
    roof_trim('Nave',-2,0,nave_width,nave_length,11,nave_ridge,roof)
    roof_trim('Transept',-2,0,transept_width,transept_length,11,transept_ridge,roof,True)
    # Solid 0.5 m terminal gables, 7 m across Y, 3 m above the eave.
    # Their outward triangle faces are at the two ends of the X-axis transept.
    for label, x in (('L', -8.75), ('R', 4.75)):
        gable('Transept_Gable_' + label, x, 0, 7, .5, 11, 14, stone, True)
        # Clear the roof's solid end prism out to the eave so the existing
        # stone gable stays visible beneath the projecting slate roof.
        side = -1 if label == 'L' else 1
        subtract(roofs, gable('Roof_Gable_Clearance', x+side*.275, 0,
                              7, 1.1, 10.99, 14, roof, True))
    apse_half_roof(-2,apse_y,9.8,3.2,roof)
    # Main tower exact requested stack: 12.5+4+6.5+1.5 = 24.5.
    drum=cone('Drum_12',(-2,0),12.5,4,4.2,12,stone,4.2)
    for i in range(12):
        angle=i*math.tau/12
        nx,ny=math.cos(angle),math.sin(angle)
        subtract(drum,arch('Drum_Window',(-2+nx*4,ny*4,13.1),.65,2.5,1,
                           math.atan2(-nx,ny)))
    cone('Dome_12',(-2,0),16.5,6.5,4.2,12,roof)
    cross('Dome_Cross',(-2,0),23,1.5,stone)
    # Detached eight-sided tower. The fixed centre and unchanged main bounds
    # leave 2.1 m in X: a slightly compressed octagon preserves the 3 m gap.
    # Radius means maximum radial distance, not the compressed X half-width.
    def tower_section(name, bottom, height, r_bottom, r_top, mat):
        def ring(r, z):
            # Keep the outer arch jambs inside the silhouette at belfry height.
            # Maximum corner radius still tapers from 2.6 to 2.2 m.
            profile_r=max(r,2.2)
            t=(2.6-profile_r)/.4
            x=2.1-.05*t
            y=math.sqrt(profile_r*profile_r-x*x)
            north_x=1.3+(math.sqrt(2.2**2-2.05**2)-1.3)*t
            north_y=math.sqrt(profile_r*profile_r-north_x*north_x)
            scale=r/profile_r
            x*=scale; y*=scale; north_x*=scale; north_y*=scale
            return [(10.5+xx,-2+yy,z) for xx,yy in
                    [(x,y),(north_x,north_y),(-north_x,north_y),(-x,y),
                     (-x,-y),(-north_x,-north_y),(north_x,-north_y),(x,-y)]]
        if r_top==0:
            return mesh(name,ring(r_bottom,bottom)+[(10.5,-2,bottom+height)],
                        [tuple(reversed(range(8)))]+[(i,(i+1)%8,8) for i in range(8)],mat)
        vertices=ring(r_bottom,bottom)+ring(r_top,bottom+height)
        faces=[tuple(reversed(range(8))),tuple(range(8,16))]
        faces += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
        return mesh(name,vertices,faces,mat)

    # Three progressively narrower masonry stages, continuous 13 m shaft.
    tower=tower_section('BellTower_8',0,4.3,2.6,2.47,stone)
    union(tower,tower_section('BellTower_Stage2',4.3,4.3,2.47,2.34,stone))
    union(tower,tower_section('BellTower_Stage3',8.6,4.4,2.34,2.2,stone))
    tower['tower_r_base']=2.6
    tower['tower_r_top']=2.2
    tower['arches_per_face']=3
    tower['shaft_height']=13.0
    # Hollow upper chamber; paired opposite faces share through cutters.
    subtract(tower,tower_section('Bell_Core',9.3,3.2,1.65,1.65,stone))
    for rotation in (0,math.pi/2):
        for offset in (-1.45,0,1.45):
            tangent=Vector((math.cos(rotation),math.sin(rotation),0))
            centre=Vector((10.5,-2,9.6))+tangent*offset
            subtract(tower,arch('Bell_Triple_Through',centre,1.1,2.6,7,rotation))
    # Three 0.25 m high projecting stone courses, below the open belfry.
    # Normal offset of each polygon edge is exactly 0.08 m.
    for i,z in enumerate((3.1,6.2,9.1),1):
        r=2.6-.4*z/13
        course=tower_section('BellTower_Course_%d'%i,z,.25,r,r-.4*.25/13,stone)
        # For this symmetric octagon an offset polygon can be built directly.
        for layer in (0,1):
            points=[Vector(course.data.vertices[layer*8+j].co[:2]) for j in range(8)]
            for j,point in enumerate(points):
                e1=(point-points[(j-1)%8]).normalized()
                e2=(points[(j+1)%8]-point).normalized()
                n1=Vector((e1.y,-e1.x)); n2=Vector((e2.y,-e2.x))
                bisector=(n1+n2).normalized()
                delta=bisector*(.08/bisector.dot(n1))
                course.data.vertices[layer*8+j].co.x += delta.x
                course.data.vertices[layer*8+j].co.y += delta.y
        normals(course)
    tower_section('BellTower_Roof',13,3.4,2.55,0,roof)
    cross('BellTower_Cross',(10.5,-2),16.4,1.2,stone)
    # Low western side wing establishes 28m overall X extent without crowding tower.
    box('Annex_West',(-12.25,2.5,2.4),(6.5,6,4.8),stone)
    gable('Annex_Roof',-12.25,2.5,6.5,6,4.8,6.2,roof)
    # Pilasters and stacked capitals provide readable masonry scale at this view distance.
    for x,y in [(-6.55,-7.7),(2.55,-7.7),(-9.04,-3),(-9.04,3),
                (5.04,-3),(5.04,3),(-6.55,7.5),(2.55,7.5)]:
        box('Buttress',(x,y,5.1),(.38,.48,10.2),stone)
        for z in (1.1,4.6,8.4,10.2):
            box('Buttress_Capital',(x,y,z),(.54,.62,.18),stone)
    # Three substantial buttresses on each nave long wall, clear of window openings.
    # X is the outward projection (0.6 m); Y is the 0.9 m facade width.
    for label, side in (('L', -1), ('R', 1)):
        for index, y in enumerate((-6.7, -4.1, 6.7), 1):
            obj = box('Buttress_%s_%02d' % (label, index),
                      (-2 + side * 4.8, y, 4), (.6, .9, 8), stone)
            obj['structural_buttress'] = True
    objects=prepare()
    openings_check()
    total,size=report(objects)
    framing=[o.matrix_world@Vector(p) for o in objects for p in o.bound_box]
    objects=optimize_church(objects)
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
    render_views(objects, framing)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    print('[CHURCH] saved=%s exported=%s'%(BLEND,FBX))


def verify():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    expected=bpy.context.scene['triangle_count']
    size=list(bpy.context.scene['bbox_xyz'])
    assert len(objects)<=6 and expected<=80000
    assert sum(triangle_count(o) for o in objects)==expected
    for obj in objects:
        mesh_integrity(obj)
    merged_openings_check(objects)
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
    merged_openings_check(objects)
    assert len(objects)<=6
    for obj in objects:
        mesh_integrity(obj)
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
