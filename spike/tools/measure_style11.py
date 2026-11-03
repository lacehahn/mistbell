#!/usr/bin/env python3
"""STYLE11 measures actual atlas rasters, fixed frame ROIs and fresh benchmark data.
Autocorrelation: Pearson over whole wall, circular diagonal translation by 1/3
and 2/3 of both dimensions. These two nonzero translations test repeated whole
wall paintings, including the snow; there is no crop or color filtering.
Also reports axis-only correlations separately (a continuous top snow band is
intentionally invariant horizontally and must not be confused with tiled stone).
"""
import argparse,json,math,statistics
from pathlib import Path
from PIL import Image
from forge_style10 import ROOT,BASE,sha
from measure_style10 import regions,SHOTS
from measure_style import frame_gate

def corr(data,w,h,dx,dy):
 n=len(data);mean=sum(data)/n;den=sum((x-mean)**2 for x in data)
 if den<1e-9:return 1.
 return sum((data[y*w+x]-mean)*(data[((y+dy)%h)*w+(x+dx)%w]-mean) for y in range(h) for x in range(w))/den

def material_metrics():
 layout=json.loads((ROOT/'style11_layout.json').read_text());atlases=[Image.open(ROOT/f'Style11_Atlas{i}.png').convert('RGB') for i in range(3)]
 walls=[]
 for c in layout['charts']:
  if not c['wall']:continue
  im=atlases[c['atlas']].crop((c['x']+2,c['y']+2,c['x']+c['w']-2,c['y']+c['h']-2))
  # Deterministic point sampling at <=256px, retaining the whole painting.
  small=im.resize((min(256,im.width),min(256,im.height)),Image.Resampling.NEAREST).convert('L');w,h=small.size;data=list(small.getdata())
  diagonal=[corr(data,w,h,round(w*k/3),round(h*k/3)) for k in (1,2)]
  axes=[corr(data,w,h,round(w/3),0),corr(data,w,h,0,round(h/3))]
  # Snow count is recovered from actual authored RGB colors, independently of layout masks.
  snowcolors={(220,231,242),(185,200,216)};points=[(x,y) for y in range(im.height) for x in range(im.width) if im.getpixel((x,y)) in snowcolors]
  snowpct=len(points)/(im.width*im.height);centroid=statistics.mean(1-(y+.5)/im.height for x,y in points) if points else None
  walls.append(dict(id=c['id'],family=c['family'],mesh=c['mesh'],size=[c['w'],c['h']],content_size=im.size,diagonal_corr=diagonal,axis_corr=axes,max_autocorr=max(diagonal),snow_band_pct=snowpct,snow_centroid_v=centroid,texel_per_m=[im.width/c['width'],im.height/c['height']]))
 return layout,walls,all(im.size==(4096,4096) for im in atlases)

def main():
 p=argparse.ArgumentParser();p.add_argument('--bench');p.add_argument('--materials-only',action='store_true');a=p.parse_args()
 layout,walls,atlas_ok=material_metrics();snow=[w for w in walls if w['family'] in ('Stone','Stone_Sub')]
 maxcorr=max(w['max_autocorr'] for w in walls);pct=statistics.mean(w['snow_band_pct'] for w in snow);centroid=min(w['snow_centroid_v'] or 0 for w in snow)
 sizes=[v for w in walls for v in w['size']];density=[v for w in walls for v in w['texel_per_m']]
 frozen={n:sha(ROOT/(n+'_Albedo.png'))==s for n,s in json.loads(BASE.read_text()).items()}
 gates=dict(repeat_ok=maxcorr<=.5,snow_ok=all(.02<=w['snow_band_pct']<=.04 and (w['snow_centroid_v'] or 0)>=.85 for w in snow),atlas_ok=atlas_ok,wall_size_ok=all(v in (512,1024) for w in walls for v in w['size']),unchanged_ok=all(frozen.values()))
 bench=None;drawcalls=None
 if a.bench:
  proof=json.loads((ROOT/'style11_uv_proof.json').read_text());gates['uv_ok']=proof['uv_ok'] and proof['binding_ok'] and proof['layout_sha']==sha(ROOT/'style11_layout.json')
  bench=json.loads(Path(a.bench).read_text());results=bench['results'];drawcalls=max(r['drawcalls'] for r in results)
  gates['perf_ok']=len(results)==3 and {r['pos'] for r in results}=={'z01','terrace','interior'} and all(r['fps_avg']>=200 and 0<r['drawcalls']<=330 for r in results)
  print('[STYLE11_PERF] '+json.dumps(bench),flush=True)
 print(f"[STYLE11] walls={len(walls)} atlas=4096 per_wall_px={min(sizes)}-{max(sizes)} texel_per_m={min(density):.3f}-{max(density):.3f} drawcalls={drawcalls if drawcalls is not None else 'unmeasured'}",flush=True)
 print(f'[STYLE11_REPEAT] max_autocorr={maxcorr:.6f}',flush=True)
 print(f'[STYLE11_SNOW] snow_band_pct={pct:.6f} snow_centroid_v={centroid:.6f}',flush=True)
 print('[STYLE11_UNCHANGED] '+json.dumps(frozen),flush=True)
 tones={};frame={}
 if not a.materials_only:
  values,evidence=regions();tones=dict(church_L=values['church'][0],church_b=values['church'][2],roof_L=values['roof'][0],sub_L=values['sub'][0],interior_L=values['interior'][0])
  print('[STYLE11_FRAME] '+' '.join(f'{k}={v:.6f}' for k,v in tones.items()),flush=True)
  gates['tone_ok']=38<=tones['church_L']<=42 and 12<=tones['church_b']<=18 and tones['roof_L']<=tones['church_L']-3.5 and 10<=tones['sub_L']<=17 and tones['interior_L']<=tones['church_L']-8
  frame=frame_gate();gates['frame_ok']=frame['pass'];print('[STYLE11_FULL_FRAME] '+json.dumps(frame),flush=True)
  capture=json.loads((SHOTS/'style11_capture.json').read_text());gates['capture_ok']=all(sha(ROOT/n)==s for n,s in capture['textures'].items()) and all(sha(SHOTS/n)==s for n,s in capture['shots'].items())
  if a.bench:gates['bench_fresh_ok']=Path(a.bench).stat().st_mtime>=(SHOTS/'style11_capture.json').stat().st_mtime
 # Fast visual validation reports perf pending; only a bench-backed pass is final.
 passed=all(gates.values());print('[STYLE11_GATE] '+' '.join(f'{k}={v}' for k,v in gates.items())+' pass='+str(passed),flush=True)
 (ROOT/'measurement_style11.json').write_text(json.dumps(dict(method=__doc__,walls=walls,charts=len(layout['charts']),frozen=frozen,tones=tones,frame=frame,bench=bench,gates=gates,pass_gate=passed),indent=2)+'\n')
 return 0 if passed else 1
if __name__=='__main__':raise SystemExit(main())
