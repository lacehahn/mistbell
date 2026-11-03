"""V12 geometry-only overlay on the v11 church builder; no material changes.
Run with Blender -b --python-exit-code 1 --python spike/tools/build_church_proportions_v12.py.
The 12m source interior is a hard stop, even when strict proportion gates fail.
"""
import json
import sys
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.geometry import convex_hull_2d
sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_church as source

REPORT = source.ROOT / 'spike/logs/proportions_v12.json'
ORIGINAL_OPTIMIZE = source.optimize_church


def category(o):
    if o.name.startswith('BellTower'): return 'tower'
    if o.name == 'Dome_12': return 'cone'
    if o.name.startswith('Dome_Cross'): return 'cross'
    if o.name.startswith('Drum_'): return 'drum'
    if o.name == 'Main_Shell': return 'shell'
    if o.name in ('Cross_Roof', 'Apse_Roof') or o.name.startswith(('Nave_', 'Transept_')): return 'eave'
    return None


def vertices(objects, tag=None):
    result=[]
    for o in objects:
        group=o.vertex_groups.get('PROP_'+tag) if tag else None
        if tag and group is None: continue
        for v in o.data.vertices:
            if tag and not any(g.group==group.index for g in v.groups): continue
            result.append(o.matrix_world @ v.co)
    return result


def extrema(points):
    return (Vector([min(p[i] for p in points) for i in range(3)]),
            Vector([max(p[i] for p in points) for i in range(3)]))


def diameter(points):
    unique=list({(round(p.x,6),round(p.y,6)) for p in points})
    xy=[Vector(p) for p in unique]
    hull=[xy[i] for i in convex_hull_2d(xy)]
    return max((a-b).length for a in hull for b in hull)


def measurement(objects):
    lo,hi=extrema(vertices(objects)); sl,sh=extrema(vertices(objects,'shell'))
    cl,ch=extrema(vertices(objects,'cone')); xl,xh=extrema(vertices(objects,'cross'))
    tl,th=extrema(vertices(objects,'tower'))
    # Actual vertical ray: floor-to-source-shell ceiling, independent of bounds.
    shell=bpy.data.objects['Main_Shell']; inv=shell.matrix_world.inverted()
    hit,p,_,_=shell.ray_cast(inv@Vector((-2,-5,.01)),Vector((0,0,1)))
    assert hit
    clear=(shell.matrix_world@p).z
    main=[]
    for o in objects:
        group=o.vertex_groups.get('PROP_tower')
        main += [o.matrix_world@v.co for v in o.data.vertices if group is None or not any(g.group==group.index for g in v.groups)]
    ml,mh=extrema(main)
    gap=max(tl.x-mh.x,ml.x-th.x,0)
    roof_h=xl.z-cl.z  # Eave to cross foot, rather than the bevelled cone tip.
    return dict(bbox=[hi.x-lo.x,hi.z-lo.z,hi.y-lo.y],body_h=sh.z,roof_h=roof_h,
                body_roof_ratio=sh.z/roof_h,tower_d=diameter(vertices(objects,'tower')),
                tower_h=th.z-tl.z,tower_h_over_body=(th.z-tl.z)/sh.z,
                tower_gap=gap,interior_clear=clear,tris=sum(source.triangle_count(o) for o in objects))


def print_metrics(label,m):
    keys=['body_h','roof_h','body_roof_ratio','tower_d','tower_h']
    keys += ['tower_h_over_body'] if label=='BEFORE' else ['tower_gap','interior_clear']
    print('[PROP_'+label+'] bbox='+ 'x'.join('%.4f'%x for x in m['bbox'])+' '+ ' '.join('%s=%.6f'%(k,m[k]) for k in keys),flush=True)


def overlay(objects):
    for o in objects:
        tag=category(o)
        if tag: o.vertex_groups.new(name='PROP_'+tag).add(list(range(len(o.data.vertices))),1,'REPLACE')
    objects=ORIGINAL_OPTIMIZE(objects)
    before=measurement(objects); print_metrics('BEFORE',before)
    # Preserve a 10cm stone slab above the original 12m clear shell.
    body=max(before['body_h']*.86,before['interior_clear']+.10)
    drop=before['body_h']-body
    cone_lo,cone_hi=extrema(vertices(objects,'cone'))
    tower_lo,tower_hi=extrema(vertices(objects,'tower'))
    other=[p for p in vertices(objects) if p.x<7]
    main_lo,main_hi=extrema(other)
    # Keep the tower on the same terrace. Use the available X envelope, then
    # solve Y scale for the true maximum horizontal diameter (convex hull).
    max_width=29+main_lo.x-(main_hi.x+3.01)
    sx=min(1.20,max_width/(tower_hi.x-tower_lo.x))
    sy=1.20
    tower_points=vertices(objects,'tower')
    target=before['tower_d']*1.20
    low,high=1.,1.6
    for _ in range(35):
        sy=(low+high)/2
        pts=[Vector(((p.x-10.5)*sx,(p.y+2)*sy,p.z)) for p in tower_points]
        if diameter(pts)<target: low=sy
        else: high=sy
    shift=main_hi.x+3.01-(10.5+(tower_lo.x-10.5)*sx)
    # A taller 3.4m cone, lower shaft, unchanged cross height: total tower
    # height stays exactly unchanged. Architectural ornaments follow the shaft.
    shaft=13-3.4*.22
    for o in objects:
        tags={g.index:g.name[5:] for g in o.vertex_groups if g.name.startswith('PROP_')}
        inv=o.matrix_world.inverted()
        for v in o.data.vertices:
            tag=next((tags[g.group] for g in v.groups if g.group in tags),None)
            p=o.matrix_world@v.co
            if tag=='tower':
                p.x=10.5+(p.x-10.5)*sx+shift; p.y=-2+(p.y+2)*sy
                if p.z<=13: p.z*=shaft/13
                elif p.z<=16.4: p.z=shaft+(p.z-13)*1.22
                # Cone tip stays at 16.4; cross remains seated there.
            elif tag=='cone': p.z=cone_lo.z-drop+(p.z-cone_lo.z)*1.22
            elif tag=='cross':
                anchor=Vector((-2,0,23))
                p=anchor+(p-anchor)*1.22
                p.z += -drop+before['roof_h']*.22
            elif tag in ('drum','eave'): p.z-=drop
            elif tag=='shell' and p.z>before['interior_clear']+.001:
                p.z=before['interior_clear']+(p.z-before['interior_clear'])*(.10/(before['body_h']-before['interior_clear']))
            v.co=inv@p
        o.data.update(); source.normals(o); source.mesh_integrity(o)
    bpy.context.view_layer.update()
    after=measurement(objects); print_metrics('AFTER',after)
    gates=dict(body_ok=.84<=after['body_h']/before['body_h']<=.88,
               roof_ok=1.18<=after['roof_h']/before['roof_h']<=1.25 and 1.15<=after['body_roof_ratio']<=1.35,
               tower_ok=1.18<=after['tower_d']/before['tower_d']<=1.24 and after['tower_h']<=before['tower_h']+1e-5,
               gap_ok=after['tower_gap']>=3,interior_ok=after['interior_clear']>=before['interior_clear']-1e-5,
               footprint_ok=27<=after['bbox'][0]<=29.00001 and 21<=after['bbox'][2]<=23,
               triangles_ok=after['tris']<=before['tris']*1.1)
    gates['pass']=all(gates.values())
    print('[PROP_GATE] '+' '.join('%s=%s'%(k,v) for k,v in gates.items()),flush=True)
    REPORT.write_text(json.dumps(dict(before=before,after=after,gates=gates,tower_scale=[sx,sy],tower_shift_x=shift,
        limitation='12m interior + 0.10m shell slab clamps wall reduction; original depth already exceeds 23m'),indent=2)+'\n')
    scene=bpy.context.scene; scene['bbox_xyz']=[after['bbox'][0],after['bbox'][2],after['bbox'][1]]
    scene['prop_v12_report']=str(REPORT)
    source.interior_check(objects)
    return objects


if __name__=='__main__':
    source.optimize_church=overlay
    source.build()
