"""Geometry-only V13 overlay; regenerate from source, never cumulatively deform assets."""
import json,sys
from pathlib import Path
import bpy
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import build_church_proportions_v12 as v12
source=v12.source
REPORT=source.ROOT/'spike/logs/proportions_v13.json'
BODY=10.6
CLEAR=10.4
ROOF_SCALE=1.08


def overlay(objects):
    objects=v12.overlay(objects)
    before=v12.measurement(objects)
    cone_lo,_=v12.extrema(v12.vertices(objects,'cone'))
    cross_lo,_=v12.extrema(v12.vertices(objects,'cross'))
    drop=before['body_h']-BODY
    # Leave the entire interior plan (rear floor ends at 11.2m) untouched.
    # Only the external terminal 1.033m of apse roof/masonry is shortened.
    _,hi=v12.extrema(v12.vertices(objects)); tail_start=11.21
    rear_target=hi.y-(before['bbox'][2]-22.55)
    for o in objects:
        tags={g.index:g.name[5:] for g in o.vertex_groups if g.name.startswith('PROP_')}
        inv=o.matrix_world.inverted()
        for vertex in o.data.vertices:
            tag=next((tags[g.group] for g in vertex.groups if g.group in tags),None)
            p=o.matrix_world@vertex.co
            if tag=='cone': p.z=cone_lo.z-drop+(p.z-cone_lo.z)*ROOF_SCALE
            elif tag=='cross':
                # Translate the complete cross, keeping its foot seated on the new cone.
                p.z+=-drop+before['roof_h']*(ROOF_SCALE-1)
            elif tag in ('drum','eave'): p.z-=drop
            elif tag=='shell':
                if p.z>=before['interior_clear']-.001:
                    p.z=CLEAR+(p.z-before['interior_clear'])*(BODY-CLEAR)/(before['body_h']-before['interior_clear'])
                elif p.z>8.2: p.z=8.2+(p.z-8.2)*(CLEAR-8.2)/(before['interior_clear']-8.2)
            elif tag!='tower' and p.z>8.2:
                # Tall columns meet the new ceiling; floor-supported furnishings remain fixed.
                p.z=8.2+(p.z-8.2)*(CLEAR-8.2)/(before['interior_clear']-8.2)
            if tag!='tower' and p.y>tail_start:
                p.y=tail_start+(p.y-tail_start)*(rear_target-tail_start)/(hi.y-tail_start)
            vertex.co=inv@p
        o.data.update();source.normals(o);source.mesh_integrity(o)
    bpy.context.view_layer.update()
    after=v12.measurement(objects)
    gates=dict(body_ok=10.4<=after['body_h']<=10.8,
        interior_ok=10.2<=after['interior_clear']<=10.6,
        roof_ok=1.06<=after['roof_h']/before['roof_h']<=1.10,
        ratio_ok=1.15<=after['body_roof_ratio']<=1.32,
        tower_ok=abs(after['tower_d']/before['tower_d']-1)<=.02 and abs(after['tower_h']/before['tower_h']-1)<=.05,
        gap_ok=after['tower_gap']>=3,
        depth_ok=after['bbox'][2]<=22.6,
        width_ok=27<=after['bbox'][0]<=29.00001,
        triangles_ok=after['tris']<=before['tris']*1.05)
    gates['pass']=all(gates.values())
    print('[PROP2] '+' '.join('%s=%.6f'%(k,after[k]) for k in ('body_h','interior_clear','roof_h','body_roof_ratio','tower_d','tower_h','tower_gap'))+' depth=%.6f tris=%d'%(after['bbox'][2],after['tris']),flush=True)
    print('[PROP2_GATE] '+' '.join('%s=%s'%(k,str(v).lower()) for k,v in gates.items()),flush=True)
    assert gates['pass'],gates
    scene=bpy.context.scene;scene['interior_clear']=CLEAR
    scene['bbox_xyz']=[after['bbox'][0],after['bbox'][2],after['bbox'][1]]
    scene['prop_v13_report']=str(REPORT)
    REPORT.write_text(json.dumps(dict(before_v12=before,after=after,gates=gates,roof_scale=ROOF_SCALE,tail_start=tail_start,rear_target=rear_target),indent=2)+'\n')
    source.interior_check(objects)
    return objects

if __name__=='__main__':
    source.optimize_church=overlay
    source.build()
