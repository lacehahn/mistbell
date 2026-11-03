"""Independent saved BLEND/FBX round-trip, interior and transformed aperture rays."""
import json,math,sys
from pathlib import Path
import bpy
from mathutils import Matrix,Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_church as source
from build_church_proportions_v12 import measurement
report=json.loads((source.ROOT/'spike/logs/proportions_v12.json').read_text())
sx,sy=report['tower_scale'];shift=report['tower_shift_x'];sz=(13-3.4*.22)/13

def apertures(objects):
    for direction in ((1,0,0),(-1,0,0),(0,1,0),(0,-1,0)):
        n=Vector(direction); t=Vector((-n.y,n.x,0))
        def ray(offset,z):
            p=Vector((10.5,-2,z))+n*3.5+t*offset
            p=Vector((10.5+(p.x-10.5)*sx+shift,-2+(p.y+2)*sy,p.z*sz))
            delta=Vector((-n.x*sx,-n.y*sy,0))
            return any(source.ray_hit(o,p,delta,7*delta.length) for o in objects)
        for offset in (-1.45,0,1.45):
            for u,z in ((0,9.8),(0,12.12),(-.42,10.6),(.42,10.6)):
                assert not ray(offset+u,z),('belfry blocked',direction,offset,u,z)
        for offset in (-.725,.725): assert ray(offset,10.6),('missing pier',direction,offset)
    for x,z in [(-2,1.6),(-2,3.5),(-1.4,2),(-2.6,2)]:
        assert not any(source.ray_hit(o,(x,-9.65,z),(0,1,0),2.5) for o in objects if not o.name.startswith(('Door_Leaf','Interior_','Window_Glass_')))

def verify(objects, imported=False):
    lo,hi=source.bounds(objects);m=report['after']
    assert max(abs(a-b) for a,b in zip(hi-lo,(m['bbox'][0],m['bbox'][2],m['bbox'][1])))<.001
    assert sum(source.triangle_count(o) for o in objects)==report['before']['tris']==90774
    for o in objects: source.mesh_integrity(o)
    if not imported: source.interior_check(objects)
    shell=next(o for o in objects if o.name=='Main_Shell')
    assert not source.ray_hit(shell,(-2,-5,.01),(0,0,1),11.98)
    assert source.ray_hit(shell,(-2,-5,11.99),(0,0,1),.03)
    apertures(objects)

bpy.ops.wm.open_mainfile(filepath=str(source.BLEND))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
verify(objects)
actual=measurement(objects)
for key in ('body_h','roof_h','tower_d','tower_h','tower_gap','interior_clear'):
    assert abs(actual[key]-report['after'][key])<1e-5,key
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(source.FBX))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in objects:
    source.activate(o);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    o.data.transform(Matrix.Rotation(-math.pi,4,'Z'))
bpy.context.view_layer.update();verify(objects,imported=True)
print('[PROP_VERIFY] saved_blend=True fbx_roundtrip=True interior_clear_unchanged=True portal_open=True belfry_12_arches_open=True piers_solid=True topology_unchanged=True',flush=True)
print('[PROP_GATE] '+ ' '.join('%s=%s'%(k,v) for k,v in report['gates'].items()),flush=True)
