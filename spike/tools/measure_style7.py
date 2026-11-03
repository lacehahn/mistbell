#!/usr/bin/env python3
"""Independent V7 Lab, palette, frozen-byte, periodic sigma=1 HF and frame gates."""
import argparse
import collections
import hashlib
import json
import re
from measure_style import ROOT, PROJECT, load, lab, gaussian_rgb, frame_gate, PRESERVED
from forge_style7 import FROZEN, STONE, INTERIOR, ROOF, stone_palette, roof_palette

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--bench')
    args=parser.parse_args()
    results={}
    for name in ('Stone','Roof_Slate','Stone_Interior','Stone_Sub','Snow','Wood','Rock','Grass'):
        buf,w,h=load(ROOT/(name+'_Albedo.png'))
        counts=collections.Counter(tuple(buf[i:i+3]) for i in range(0,len(buf),3))
        labs={p:lab(p) for p in counts}
        means=[sum(labs[p][c]*k for p,k in counts.items())/(w*h) for c in range(3)]
        results[name]=dict(mean_L=means[0],mean_a=means[1],mean_b=means[2],palette=len(counts),
            chroma_ab_max=max(max(abs(v) for v in t[1:]) for t in labs.values()),hf=gaussian_rgb(buf,w,h))
        if name in ('Stone','Roof_Slate','Stone_Interior'):
            ramp={'Stone':STONE,'Roof_Slate':ROOF,'Stone_Interior':INTERIOR}[name]
            allowed=roof_palette(ramp) if name=='Roof_Slate' else stone_palette(ramp,name=='Stone_Interior')
            results[name]['steps']=sum(c in counts for c in ramp)
            results[name]['palette_ok']=set(counts)<=set(allowed) and len(counts)<=(12 if name=='Roof_Slate' else 10) and results[name]['steps']==4
            results[name]['bright_px_pct']=sum(counts[c] for c in (ramp[-1],tuple(round(v*1.12) for v in ramp[-1])))/(w*h)
            meta=(ROOT/(name+'_Albedo.png.meta')).read_text()
            results[name]['import_ok']='filterMode: 0' in meta and 'enableMipMap: 0' in meta
    stone,roof,indoor=[results[n] for n in ('Stone','Roof_Slate','Stone_Interior')]
    sl,rl,il=[r['mean_L'] for r in (stone,roof,indoor)]
    print(f'[STYLE7] Stone mean_L={sl:.6f} Roof mean_L={rl:.6f} Interior mean_L={il:.6f} roof_minus_stone={rl-sl:.6f}',flush=True)
    print(f'[STYLE7_CHROMA] Stone ab_max={stone["chroma_ab_max"]:.6f} Roof ab_max={roof["chroma_ab_max"]:.6f}',flush=True)
    print('[STYLE7_LAB] '+json.dumps({n:{k:r[k] for k in ('mean_L','mean_a','mean_b','chroma_ab_max')} for n,r in results.items() if n in ('Stone','Roof_Slate','Stone_Interior')}),flush=True)
    unchanged={n:hashlib.sha256((ROOT/(n+'_Albedo.png')).read_bytes()).hexdigest()==s for n,s in FROZEN.items()}
    labels={'Stone_Sub':'Sub','Roof_Slate':'Roof','Stone_Interior':'Interior'}
    print('[STYLE7_UNCHANGED] '+' '.join(labels.get(n,n)+'_sha_same='+str(v) for n,v in unchanged.items()),flush=True)
    hfs={n:r['hf'] for n,r in results.items()};spread=(max(hfs.values())-min(hfs.values()))/min(hfs.values())
    print('[STYLE7_HF] sigma=1.0 '+' '.join(n+f'={v:.6f}' for n,v in hfs.items())+f' spread_all={spread:.6f}',flush=True)
    frame=frame_gate(); print('[STYLE7_FRAME] '+json.dumps(frame),flush=True)
    # Grass is frozen with the previous accepted ab_max=7.67; never recolor it to satisfy a new hue bound.
    gates=dict(stone_ok=38<=sl<=41 and 1<=stone['mean_a']<=3 and 12<=stone['mean_b']<=18 and stone['chroma_ab_max']<=20 and stone['bright_px_pct']<=.12,
        roof_ok=31<=rl<=34 and roof['chroma_ab_max']<=6 and roof['mean_b']<0,
        roof_darker_ok=rl<=sl-5,
        interior_ok=il<=min(32,sl-8) and 8<=indoor['mean_b']<=12 and indoor['chroma_ab_max']<=12,
        unchanged_ok=all(unchanged.values()) and all(hashlib.sha256((ROOT/n).read_bytes()).hexdigest()==s for n,s in PRESERVED.items()),
        hf_ok=spread<=.22,
        palette_ok=all(r['palette_ok'] and r['import_ok'] for r in (stone,roof,indoor)), frame_ok=frame['pass'])
    performance=None
    if args.bench:
        performance=json.loads(__import__('pathlib').Path(args.bench).read_text())
        samples=performance['results']
        gates['performance_ok']={r['pos'] for r in samples}=={'z01','terrace','interior'} and all(r['fps_avg']>=200 and 0<r['drawcalls']<=330 for r in samples)
        print('[STYLE7_PERF] '+json.dumps(performance)+' pass='+str(gates['performance_ok']),flush=True)
    passed=all(gates.values())
    print('[STYLE7_GATE] '+' '.join(k+'='+str(v) for k,v in gates.items())+' pass='+str(passed),flush=True)
    (ROOT/'measurement_style7.json').write_text(json.dumps(dict(materials=results,unchanged=unchanged,spread_all=spread,frame=frame,performance=performance,gates=gates,pass_gate=passed),indent=2)+'\n')
    return 0 if passed else 1

if __name__=='__main__':raise SystemExit(main())
