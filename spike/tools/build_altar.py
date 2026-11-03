"""Hand-painted altar. Blender Y-up construction, baked Z-up FBX export.
Run: blender -b --python-exit-code 1 -P spike/tools/build_altar.py
"""
import bpy, bmesh, math, json, shutil
from pathlib import Path
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'spike/export'
TEX=ROOT/'spike/art-prompts/staged/altar_stone.png'
W,D,BODY_H=2.08,.85,.83
BASE,TOP=.11,1.04
parts=[]

def box(name,center,size,bevel=.008,zone='stone',dark=False):
    mesh=bpy.data.meshes.new(name); ob=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(ob)
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts: v.co=Vector((v.co.x*size[0],v.co.y*size[1],v.co.z*size[2]))+Vector(center)
    bm.to_mesh(mesh); bm.free()
    ob['zone']=zone; ob['dark']=dark; parts.append(ob)
    if bevel:
        m=ob.modifiers.new('Two segment edge bevel','BEVEL');m.width=bevel;m.segments=2
    return ob

def apply(ob):
    bpy.context.view_layer.objects.active=ob
    for m in list(ob.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)

def cyl(name,center,radius,depth,zone='wood',axis='Y'):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=radius,depth=depth,location=center)
    ob=bpy.context.object;ob.name=name
    if axis=='Y':ob.rotation_euler.x=math.pi/2
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    ob['zone']=zone;ob['dark']=False;parts.append(ob)
    m=ob.modifiers.new('Two segment bevel','BEVEL');m.width=.003;m.segments=2
    return ob

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system='METRIC'
box('plinth_lower',(0,.03,0),(2.24,.06,.99),.01)
box('plinth_upper',(0,.085,0),(2.16,.05,.93),.008)
body=box('altar_body',(0,BASE+BODY_H/2,0),(W,BODY_H,D),0)
# Actual 5 cm deep cavities, with separate inset dark backs.
for i,x in enumerate((-.66,0,.66)):
    cutter=box('cutter',(x,.45,-.43),(.34,.52,.11),0)
    m=body.modifiers.new('recess','BOOLEAN');m.operation='DIFFERENCE';m.solver='EXACT';m.object=cutter
    apply(body); parts.remove(cutter);bpy.data.objects.remove(cutter,do_unlink=True)
    box('niche_back_'+str(i),(x,.45,-.369),(.338,.518,.012),.003,dark=True)
    box('icon_gold_border',(x,.45,-.386),(.30,.46,.012),.002,zone='gold',dark=True)
    box('icon_painting',(x,.45,-.394),(.26,.42,.004),0,zone='icon',dark=True)
    for s in (-1,1):
        box('niche_jamb',(x+s*(.17+.0225),.45,-.44),(.045,.61,.03),.005,zone='trim')
        box('niche_lintel',(x,.45+s*(.26+.0225),-.44),(.43,.045,.03),.005,zone='trim')
    box('niche_hood',(x,.45+.26+.045+.015,-.45),(.47,.03,.06),.005,zone='trim')
m=body.modifiers.new('Two segment stone bevel','BEVEL');m.width=.006;m.segments=2
for x in (-.995,.995):
    for z in (-.405,.405):box('corner_pilaster',(x,.525,z),(.09,.81,.06),.006,zone='trim')
box('cornice',(0,.955,0),(2.16,.03,.91),.006,zone='trim')
box('mensa',(0,1.005,0),(2.20,.07,.97),.008,zone='trim')
box('linen_top',(0,TOP+.004,0),(2.14,.008,.91),.003,zone='cloth')
# One continuous closed cloth volume with four curved 6 cm folds (3.5 cm amplitude).
verts=[];faces=[];count=96
samples=[-1.07+2.14*j/count for j in range(count+1)]
for center in (-.78,-.26,.26,.78):
    for value in (center-.03,center,center+.03):
        samples[min(range(len(samples)),key=lambda j:abs(samples[j]-value))]=value
samples.sort()
for back in (0,1):
    for y in (TOP-.34,TOP+.008):
        for j in range(count+1):
            x=samples[j]
            wave=sum(.035*(.5+.5*math.cos((x-c)/.03*math.pi)) if abs(x-c)<.03 else 0 for c in (-.78,-.26,.26,.78))
            verts.append((x,y,-.496-wave+back*.012))
n=count+1
for b in (0,1):
    for j in range(count):
        a=b*2*n+j; q=(a,a+1,a+n+1,a+n);faces.append(q if b else q[::-1])
for row in (0,1):
    for j in range(count):
        a=row*n+j;q=(a,a+2*n,a+2*n+1,a+1);faces.append(q if row else q[::-1])
faces.extend([(0,n,3*n,2*n),(n-1,3*n-1,4*n-1,2*n-1)])
me=bpy.data.meshes.new('woven_folds');me.from_pydata(verts,[],faces);me.update()
for p in list(me.polygons)[:count*2]:p.use_smooth=True
cloth=bpy.data.objects.new('cloth_drape_four_folds',me);bpy.context.collection.objects.link(cloth)
cloth['zone']='cloth';cloth['dark']=False;parts.append(cloth)
m=cloth.modifiers.new('Two segment cloth hem','BEVEL');m.width=.002;m.segments=2;m.limit_method='ANGLE';m.angle_limit=.6
# Small warm wood cross, behind the candles; round boss facing the congregation.
box('cross_vertical',(0,TOP+.55,.29),(.11,1.10,.05),.008,zone='wood')
box('cross_horizontal',(0,TOP+.77,.29),(.34,.10,.05),.008,zone='wood')
cyl('cross_round_boss',(0,TOP+.77,.256),.037,.018,axis='Z')
for i,x in enumerate((-.44,0,.44)):
    box('candle_foot',(x,TOP+.014,.15),(.085,.028,.085),.004,zone='wood')
    cyl('candle_stem',(x,TOP+.086,.15),.0175,.116)
    cyl('candle_dish',(x,TOP+.152,.15),.05,.016)
    candle=cyl('candle_wax',(x,TOP+.22,.15),.022,.12,zone='cloth')

if not TEX.is_file():
    raise FileNotFoundError('Generate staged/altar_stone.png before building the painted altar')
image=bpy.data.images.load(str(TEX)); image.pack()
mats=[]
for name,tint in [('Altar_Painted',1),('Altar_Recess',.82)]:
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.9
    if image:
        tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Closest'
        mix=mat.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(tint,tint,tint,1)
        mat.node_tree.links.new(tex.outputs['Color'],mix.inputs[1]);mat.node_tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
    mats.append(mat)
# Metric planar UV per face. 1024 * .125 = 128 px/metre. Four semantic quadrants.
zones={'stone':(.025,.525),'wood':(.525,.525),'cloth':(.025,.025),'trim':(.525,.025),'icon':(.70,.60),'gold':(.79,.88)}
body_bounds=[]
for index,ob in enumerate(parts):
    apply(ob);ob.data.materials.clear()
    for mat in mats:ob.data.materials.append(mat)
    uv=ob.data.uv_layers.new(name='Altar_128px_per_m')
    for p in ob.data.polygons:
        p.material_index=1 if ob['dark'] else 0
        axis=max(range(3),key=lambda a:abs(p.normal[a]));axes=[a for a in range(3) if a!=axis]
        # Project physical coordinates; face-specific offsets avoid identical opposite faces.
        ox,oy=zones[ob['zone']];off=(axis*.037+(index%4)*.021)
        for li in p.loop_indices:
            v=ob.data.vertices[ob.data.loops[li].vertex_index].co
            a,b=v[axes[0]],v[axes[1]]
            uv.data[li].uv=(ox+(a+1.12)*.125+off,oy+(b+.54)*.125+off)
            if ob['zone']=='gold':
                uv.data[li].uv=(.795+v.x*.001,.889+v.y*.001)
            if ob['zone']=='icon':
                center_x=sum(v.co.x for v in ob.data.vertices)/len(ob.data.vertices)
                uv.data[li].uv=(.801+(v.x-center_x+.13)/.26*.122,.703+(v.y-.24)/.42*.180)
    if not ob.name.startswith('cross_') and not ob.name.startswith('candle_'):
        body_bounds.extend([v.co.copy() for v in ob.data.vertices])
# Fix quadrant extents by translating each object's charts; never change scale/density.
for ob in parts:
    uv=ob.data.uv_layers.active.data;ox,oy=zones[ob['zone']]
    for k,lo in enumerate((ox,oy)):
        values=[v.uv[k] for v in uv];shift=max(0,max(values)-(lo+.45))-max(0,lo-min(values))
        for v in uv:v.uv[k]-=shift
# Pick brighter regions of the same painted atlas for horizontal trim faces.
# This is UV art direction, not a specular gain or an extra material/texture.
pixels=list(image.pixels)
def chart_luma(uvs,du=0,dv=0):
    lo=[min(u[k] for u in uvs) for k in (0,1)]
    hi=[max(u[k] for u in uvs) for k in (0,1)]
    values=[]
    for a in (.2,.5,.8):
        for b in (.2,.5,.8):
            x=min(1023,max(0,int((lo[0]+(hi[0]-lo[0])*a+du)*1024)))
            y=min(1023,max(0,int((lo[1]+(hi[1]-lo[1])*b+dv)*1024)))
            k=(y*1024+x)*4
            values.append(.2126*pixels[k]+.7152*pixels[k+1]+.0722*pixels[k+2])
    return sum(values)/len(values)
front=[];tops=[]
for ob in parts:
    if ob['zone']!='trim':continue
    for p in ob.data.polygons:
        uv=ob.data.uv_layers.active.data
        uvs=[uv[i].uv.copy() for i in p.loop_indices]
        if p.normal.z<-.95:front.append((chart_luma(uvs),p.area))
        if p.normal.y>.95:tops.append((ob,p,uvs))
front_luma=sum(v*a for v,a in front)/sum(a for _,a in front)
for ob,p,uvs in tops:
    lo=[min(u[k] for u in uvs) for k in (0,1)]
    hi=[max(u[k] for u in uvs) for k in (0,1)]
    candidates=[]
    for ox,oy in ((.525,.025),(.025,.525)):
        for i in range(21):
            for j in range(21):
                du=ox+(.45-(hi[0]-lo[0]))*i/20-lo[0]
                dv=oy+(.45-(hi[1]-lo[1]))*j/20-lo[1]
                candidates.append((abs(chart_luma(uvs,du,dv)-front_luma*1.10),du,dv))
    _,du,dv=min(candidates)
    for li in p.loop_indices:ob.data.uv_layers.active.data[li].uv+=Vector((du,dv))
top_luma=sum(chart_luma([ob.data.uv_layers.active.data[i].uv for i in p.loop_indices])*p.area for ob,p,_ in tops)/sum(p.area for _,p,_ in tops)
trim_top_ratio=top_luma/front_luma
assert 1.08<=trim_top_ratio<=1.12,trim_top_ratio
bpy.ops.object.select_all(action='DESELECT')
for ob in parts:ob.select_set(True)
bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
altar=bpy.context.object;altar.name='Interior_Altar_Model'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# Recalculate consistent outward normals on the continuous cloth and booleans.
bm=bmesh.new();bm.from_mesh(altar.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(altar.data);bm.free()
# Stylized cloth normals retain the 35mm silhouette without black specular-like
# bands on the steep, narrow folds. Tilt the valley normals slightly away from
# the low warm wash; leave every stone/wood normal unchanged.
normals=[None]*len(altar.data.loops)
for p in altar.data.polygons:
    for li in p.loop_indices:
        v=altar.data.vertices[altar.data.loops[li].vertex_index].co
        normal=p.normal.copy()
        if -.533<=v.z<=-.495 and .699<=v.y<=1.049 and p.normal.z<-.1:
            wave=max(0,min(1,(-.496-v.z)/.035))
            tilt=.045*(1-wave)
            normal=Vector((0,tilt,-math.sqrt(1-tilt*tilt)))
        normals[li]=normal
altar.data.normals_split_custom_set(normals)
altar.data.calc_loop_triangles();area=uvarea=0
uv=altar.data.uv_layers.active.data
for t in altar.data.loop_triangles:
    a,b,c=[altar.data.vertices[i].co for i in t.vertices];area+=(b-a).cross(c-a).length/2
    u,v,w=[uv[i].uv for i in t.loops];uvarea+=abs((v.x-u.x)*(w.y-u.y)-(v.y-u.y)*(w.x-u.x))/2
bbox=[max(v[a] for v in body_bounds)-min(v[a] for v in body_bounds) for a in range(3)]
report={'tris':len(altar.data.loop_triangles),'materials':len(altar.data.materials),'bbox':bbox,'texel_px_per_m':1024*math.sqrt(uvarea/area),'surface_m2':area,'uv_area':uvarea,'niche_depth_m':.05,'cloth_drop_m':.34,'fold_count':4,'fold_amplitude_m':.035,'fold_width_m':.06,'candlestick_height_m':.16,'candle_height_m':.12,'icon_panel_m':[.30,.46],'icon_border_m':.02,'trim_top_albedo_over_front':trim_top_ratio}
print('[ALTAR_MODEL] '+json.dumps(report))
assert report['tris']<=9000 and report['materials']<=2 and 110<=report['texel_px_per_m']<=150
assert 2.1<=bbox[0]<=2.3 and .97<=bbox[1]<=1.13 and .85<=bbox[2]<=1.05
OUT.mkdir(parents=True,exist_ok=True);(OUT/'altar_metrics.json').write_text(json.dumps(report,indent=2))
# Y-up construction -> native Blender Z-up; Blender -Y maps to Unity +Z.
# Source front -Z therefore needs to become Blender +Y (Unity -Z).
altar.data.transform(Matrix.Rotation(math.pi/2,4,'X'))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'gergeti_altar.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'gergeti_altar.fbx'),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,mesh_smooth_type='FACE',use_custom_props=False,use_triangles=True,bake_anim=False)
target=ROOT/'spike/project/Assets/Art/Church/gergeti_altar.fbx';shutil.copyfile(OUT/'gergeti_altar.fbx',target)
