#!/usr/bin/env python3
"""V8 independent Lab/HF/seam/frame/benchmark gates with pre-edit byte hashes."""
import argparse
import collections
import json
from pathlib import Path
from PIL import Image
from measure_style import ROOT, load, lab, gaussian_rgb, frame_gate
from forge_style8 import frozen, structure_ok

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--bench')
    args = parser.parse_args()
    results = {}
    for name in ('Stone','Roof_Slate','Stone_Sub','Stone_Interior','Snow','Wood','Rock','Grass'):
        buf,w,h = load(ROOT/(name+'_Albedo.png'))
        counts = collections.Counter(tuple(buf[i:i+3]) for i in range(0,len(buf),3))
        labs = {p:lab(p) for p in counts}
        edge_x = sum(abs(buf[y*w*3+c]-buf[(y*w+w-1)*3+c]) for y in range(h) for c in range(3))/(h*3*255)
        edge_y = sum(abs(buf[x*3+c]-buf[((h-1)*w+x)*3+c]) for x in range(w) for c in range(3))/(w*3*255)
        results[name] = dict(mean_L=sum(labs[p][0]*n for p,n in counts.items())/(w*h),
            ab_max=max(abs(v) for t in labs.values() for v in t[1:]),
            palette=len(counts), hf=gaussian_rgb(buf,w,h), seam_grad=max(edge_x,edge_y))
    roof = results['Roof_Slate']; delta = results['Stone']['mean_L']-roof['mean_L']
    unchanged = frozen()
    labels = {'Stone_Sub':'Sub','Stone_Interior':'Interior'}
    hfs = [r['hf'] for r in results.values()]
    spread = (max(hfs)-min(hfs))/min(hfs)
    print(f'[STYLE8] Roof mean_L={roof["mean_L"]:.6f} Roof ab_max={roof["ab_max"]:.6f} stone_minus_roof={delta:.6f}', flush=True)
    print('[STYLE8_UNCHANGED] '+' '.join(labels.get(n,n)+'_sha_same='+str(v) for n,v in unchanged.items()), flush=True)
    print(f'[STYLE8_HF] sigma=1.0 Roof={roof["hf"]:.6f} spread_all={spread:.6f}', flush=True)
    frame = frame_gate()
    print('[STYLE8_FRAME] '+json.dumps(frame), flush=True)
    gates = dict(roof_ok=34.5<=roof['mean_L']<=36.5 and roof['ab_max']<=6,
        roof_darker_ok=delta>=3.5, unchanged_ok=all(unchanged.values()),
        hf_ok=.010<=roof['hf']<=.018 and spread<=.22,
        palette_ok=roof['palette']<=12, seam_ok=roof['seam_grad']<=.02,
        structure_ok=structure_ok(Image.open(ROOT/'Roof_Slate_Albedo.png').convert('RGB')), frame_ok=frame['pass'])
    performance = None
    if args.bench:
        performance = json.loads(Path(args.bench).read_text())
        samples = performance['results']
        gates['performance_ok'] = len(samples)==3 and {r['pos'] for r in samples}=={'z01','terrace','interior'} and all(r['fps_avg']>=200 and 0<r['drawcalls']<=330 for r in samples)
        print('[STYLE8_PERF] '+json.dumps(performance)+' pass='+str(gates['performance_ok']), flush=True)
    passed = all(gates.values())
    print('[STYLE8_GATE] '+' '.join(k+'='+str(v) for k,v in gates.items())+' pass='+str(passed), flush=True)
    (ROOT/'measurement_style8.json').write_text(json.dumps(dict(materials=results,unchanged=unchanged,spread_all=spread,frame=frame,performance=performance,gates=gates,pass_gate=passed),indent=2)+'\n')
    return 0 if passed else 1

if __name__ == '__main__':
    raise SystemExit(main())
