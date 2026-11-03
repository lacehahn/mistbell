#!/usr/bin/env python3
"""Independent STYLE6 gates. Same sigma=1/radius=3 periodic RGB HF as STYLE5.
spread_all = (max-min)/min for all EIGHT named textures (task's list says seven).
Body steps exclude roof overlap/bevel and interior mortar/bevel, as in STYLE5.
seam_grad = max(mean RGB edge jump on horizontal/vertical wrap)/255.
"""
import hashlib
import json
import pathlib
import re
import sys
from measure_style import ROOT, PROJECT, load, gaussian_rgb, lab, frame_gate, PRESERVED
from forge_style6 import FROZEN, RAMPS, rgb

DENSITIES={'Roof_Slate':26,'Rock':24,'Grass':100/3,'Stone_Interior':28}
MATS={'Roof_Slate':'M_Roof','Rock':'M_WinterRock','Grass':'M_STYLE6_Grass','Stone_Interior':'M_IndoorShell'}

def main():
    results={};residuals={}
    for name in ('Stone','Stone_Sub','Wood','Snow',*DENSITIES):
        buf,w,h=load(ROOT/(name+'_Albedo.png'))
        pixels=[tuple(buf[i:i+3]) for i in range(0,len(buf),3)]
        palette=set(pixels); labs={p:lab(p) for p in palette}
        hf=gaussian_rgb(buf,w,h);residuals[name]=hf
        if name not in DENSITIES:continue
        edge_x=sum(abs(buf[(y*w)*3+c]-buf[(y*w+w-1)*3+c]) for y in range(h) for c in range(3))/(h*3*255)
        edge_y=sum(abs(buf[x*3+c]-buf[((h-1)*w+x)*3+c]) for x in range(w) for c in range(3))/(w*3*255)
        mat=(PROJECT/('Assets/Settings/'+MATS[name]+'.mat')).read_text()
        sx,sy=map(float,re.search(r'- _BaseMap:.*?m_Scale: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}',mat,re.S).groups())
        guid=re.search(r'guid: (\w+)',(ROOT/(name+'_Albedo.png.meta')).read_text())[1]
        bound=bool(re.search(r'- _BaseMap:.*?guid: '+guid,mat,re.S))
        shader='ForegroundSnowfield' if name=='Grass' else 'TerrainHaze'
        shader_guid=re.search(r'guid: (\w+)',(PROJECT/('Assets/Art/Shaders/'+shader+'.shader.meta')).read_text())[1]
        bound &= bool(re.search(r'm_Shader: \{[^}]*guid: '+shader_guid,mat))
        bound &= bool(re.search(r'- _Style6Mode: 1\b' if name=='Grass' else r'- _ForgeSurface: 2\b',mat))
        importer=all('filterMode: 0' in (ROOT/(name+suffix+'.png.meta')).read_text() and 'enableMipMap: 0' in (ROOT/(name+suffix+'.png.meta')).read_text() for suffix in ('_Albedo','_Roughness'))
        body=set(map(rgb,RAMPS[name]));steps=len(body & palette)
        # Only authored body tiers count; independently check every tier exists.
        r=dict(palette=len(palette),luma_steps=steps,mean_L=sum(labs[p][0] for p in pixels)/(w*h),
               chroma_ab_max=max(max(abs(v) for v in t[1:]) for t in labs.values()),
               texel_per_m=sx*w,hf=hf,seam_grad=max(edge_x,edge_y),
               density_ok=abs(sx*w-DENSITIES[name])<1e-4 and abs(sy*h-DENSITIES[name])<1e-4,
               binding_ok=bound,import_ok=importer,body_palette=['#'+c for c in RAMPS[name]])
        if name=='Grass':r['density_ok'] &= abs(float(re.search(r'- _Style6Density: ([\d.eE+-]+)',mat)[1])-100/3)<1e-4
        results[name]=r
        print('[STYLE6] '+name+' '+' '.join(k+'='+ (f'{r[k]:.6f}' if isinstance(r[k],float) else str(r[k])) for k in ('palette','luma_steps','mean_L','chroma_ab_max','texel_per_m','hf','seam_grad')),flush=True)
    spread=(max(residuals.values())-min(residuals.values()))/min(residuals.values())
    labels={'Stone_Sub':'Sub','Roof_Slate':'Roof','Stone_Interior':'Interior'}
    print('[STYLE6_HF] sigma=1.0 '+' '.join(labels.get(n,n)+'='+f'{v:.6f}' for n,v in residuals.items())+f' spread_all={spread:.6f}',flush=True)
    unchanged={n:hashlib.sha256((ROOT/(n+'_Albedo.png')).read_bytes()).hexdigest()==sha for n,sha in FROZEN.items()}
    print('[STYLE6_UNCHANGED] '+' '.join(labels.get(n,n)+'_sha_same='+str(v) for n,v in unchanged.items()),flush=True)
    stone,w,h=load(ROOT/'Stone_Albedo.png');stone_L=sum(lab(stone[i:i+3])[0] for i in range(0,len(stone),3))/(w*h)
    frame=frame_gate(); print('[STYLE6_FRAME] '+json.dumps(frame),flush=True)
    # Also protect the roughness baselines and check every serialized rollout binding.
    preserved=all(hashlib.sha256((ROOT/n).read_bytes()).hexdigest()==sha for n,sha in PRESERVED.items())
    gates=dict(palette_ok=all(r['palette']<=12 for r in results.values()),
               mono_ok=all(r['chroma_ab_max']<=(10 if n=='Grass' else 6) for n,r in results.items()),
               hf_ok=spread<=.20 and all(.010<=r['hf']<=.018 for r in results.values()),
               steps_ok=all(4<=r['luma_steps']<=5 for r in results.values()),
               unchanged_ok=all(unchanged.values()) and preserved,
               seam_ok=all(r['seam_grad']<=.02 for r in results.values()),
               interior_ok=results['Stone_Interior']['mean_L']<=stone_L-8,
               textures_ok=all(r['density_ok'] and r['binding_ok'] and r['import_ok'] for r in results.values()),
               frame_ok=frame['pass'])
    performance=None
    if '--bench' in sys.argv:
        bench_path=pathlib.Path(sys.argv[sys.argv.index('--bench')+1])
        performance=json.loads(bench_path.read_text())
        samples=performance['results']
        gates['performance_ok']=({r['pos'] for r in samples}=={'z01','terrace','interior'} and
            all(r['fps_avg']>=200 and 0<r['drawcalls']<=330 for r in samples))
        print('[STYLE6_PERF] '+json.dumps(performance)+' pass='+str(gates['performance_ok']),flush=True)
    passed=all(gates.values())
    print('[STYLE6_CHECKS] '+json.dumps(gates),flush=True)
    print('[STYLE6_GATE] '+' '.join(k+'='+str(gates[k]) for k in ('palette_ok','mono_ok','hf_ok','steps_ok','unchanged_ok'))+' pass='+str(passed),flush=True)
    report=dict(performance=performance,materials=results,hf=residuals,spread_all=spread,unchanged=unchanged,stone_mean_L=stone_L,frame=frame,gates=gates,pass_gate=passed)
    (ROOT/'measurement_style6.json').write_text(json.dumps(report,indent=2)+'\n')
    return 0 if passed else 1

if __name__=='__main__':sys.exit(main())
