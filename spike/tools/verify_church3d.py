#!/usr/bin/env python3
"""Read generated raster pixels, geometry audit and this run's positive terminal logs."""
import json,re,sys,math,statistics,shutil
from pathlib import Path
from PIL import Image
from church3d_textures import ROOT,ART,OUT,lab,sha,tone

def main():
 m=json.loads((OUT/'texture_manifest.json').read_text());assets=json.loads((OUT/'assets.json').read_text())
 atlases=[Image.open(ART/f'Church3D_Atlas{i}.png').convert('RGB') for i in range(3)];charts={c['id']:c for c in m['charts']}
 levels=set();widths=[];darks=[];stone_checks=0
 lum=lambda p:sum(a*b for a,b in zip(p,(.2126,.7152,.0722)))
 for b in m['blocks']:
  c=charts[b['chart']];x,y=b['x'],b['y'];w,h=c['w']-4,c['h']-4
  # Interior samples avoid clipping, snow silhouettes, partial charts and overwritten neighbours.
  yy=y+b['jh']+3;xx=x+b['jw']+3
  if x<0 or x+b['w']>=w or y+b['h']>=h or yy>=h or xx>=w:continue
  if c['snow_depths'] and yy<=max(c['snow_depths']):continue
  im=atlases[c['atlas']];pixel=lambda u,v:im.getpixel((c['x']+2+u,c['y']+2+v))
  body=pixel(xx,yy);joint=pixel(x,yy)
  assert body==tuple(b['rgb']),('block body',b)
  n=0
  while n<b['w'] and pixel(x+n,yy)==joint:n+=1
  assert n==b['jw'],('joint raster width',n,b)
  end=x+n
  while end<w and pixel(end,yy)==body:end+=1
  assert end-x==b['w'],('stone raster length',end-x,b['w'])
  levels.add(body);widths.append(n);darks.append(1-lum(joint)/lum(body));stone_checks+=1
 long_stones=[b for b in m['blocks'] if b['long']];intervals=[b['index']-a['index'] for a,b in zip(long_stones,long_stones[1:])]
 long_ratios=[b['w']/b['baseline'] for b in long_stones]
 roof_jitter=[];roof_dark=[];shadow_rows={};roof_checks=0
 for b in m['roof']:
  c=charts[b['chart']];w,h=c['w']-4,c['h']-4;x,y=b['x'],b['y']
  if x<0 or x+50>=w*5 or y+b['step']+40>=h*5:continue
  im=atlases[c['atlas']];pixel=lambda u,v:im.getpixel((c['x']+2+int(u/5),c['y']+2+int(v/5)))
  col=tuple(b['rgb']);assert pixel(x+25,y+30)==col
  # Detect the next row boundary in actual pixels at this tile's centre.
  assert any(pixel(x+25,y+b['step']+d)!=col for d in (0,5)),('roof row boundary',b)
  roof_jitter.append(abs(b['step']/50-1));roof_checks+=1
  if b['shadow']:
   roof_dark.append(1-lum(pixel(x+25,y+12))/lum(col));shadow_rows.setdefault(b['chart'],set()).add(b['row'])
 eaves=[b-a for rows in shadow_rows.values() for a,b in zip(sorted(rows),sorted(rows)[1:])]
 bstar=[lab(c)[2] for c in levels];b_error=max(abs(v-m['base_lab'][2]) for v in bstar)
 logs={n:(ROOT/f'logs/church3d_{n}.log').read_text(errors='replace') for n in ('walk','interior','reveal','capture','bench')}
 bad={n:len(re.findall(r'^\[PLAY\] .*fps_ok=false|failed|ERROR|Exception|^\[REVEAL\] gate=FAIL',s,re.M)) for n,s in logs.items() if n in ('walk','interior','reveal')}
 walk=re.search(r'\[PLAY\] walk_start=.*distance=([\d.]+)',logs['walk']);assert walk and float(walk[1])>=15
 assert 'ground_error_max=0.000000 raycast_misses=0' in logs['walk']
 assert '[INT_PASS]' in logs['interior'] and 'False' not in re.findall(r'\[INT_PASS\].*',logs['interior'])[-1]
 assert '[REVEAL] gate=pass ' in logs['reveal']
 benches=json.loads((OUT/'bench.json').read_text())['results'];outdoor=[b for b in benches if b['pos'] in ('z01','terrace')]
 assert len(outdoor)==2
 capture=logs['capture'];assert '[CHURCH3D_CAPTURE] distance_m=12.000' in capture
 shots={}
 for name in ('art_runtime','church_closeup'):
  p=ROOT/f'project/Shots/{name}.png';im=Image.open(p);assert im.size==(1920,1080)
  assert re.search(r'\[SHOT\] written .*'+name+r'\.png exists=True bytes=[1-9]',capture)
  shutil.copy2(p,OUT/(name+'.png'));shots[name]=dict(size=list(im.size),sha256=sha(p),bytes=p.stat().st_size)
 metrics=dict(block_levels=len(levels),palette=sorted(levels),b_star_max_error=b_error,joint_var=max(abs(x/5-1) for x in widths),joint_width_px=[min(widths),max(widths)],joint_dark=[min(darks),max(darks)],long_stone_every=[min(intervals),max(intervals)],long_stone_ratio=[min(long_ratios),max(long_ratios)],stone_pixel_samples=stone_checks,roof_jitter=max(roof_jitter),roof_pixel_samples=roof_checks,eave_shadow_every=[min(eaves),max(eaves)],eave_dark=[min(roof_dark),max(roof_dark)],assets=assets,bad=bad,bench=outdoor,shots=shots)
 checks=dict(build='[CHURCH3D_BUILD] result=Succeeded errors=0' in (ROOT/'logs/church3d_build.log').read_text(errors='replace'),levels=len(levels)==4,b_star_quantization=b_error<.25,joints=max(widths)==7 and min(widths)==3 and min(darks)>=.18 and max(darks)<=.22,long_stones=min(intervals)>=6 and max(intervals)<=9 and min(long_ratios)>=1.4 and max(long_ratios)<=1.8,roof=abs(max(roof_jitter)-.02)<1e-6 and min(eaves)>=5 and max(eaves)<=7 and min(roof_dark)>.11 and max(roof_dark)<.13,windows=len(assets['windows'])==7 and all(abs(w['projection']-.15)<1e-5 and abs(w['glow_ratio']-1.2)<1e-5 and abs(w['reveal_ratio']-.92)<1e-5 and w['shadow_quads']==3 and w['glow_vertices']==4 for w in assets['windows']),roof_edges=assets['roof_strips']>0 and abs(assets['roof_alpha']-.35)<1e-5 and 6<=assets['roof_width_texels']<=10,protected=assets['protected_before']==assets['protected_after'],regressions=all(v==0 for v in bad.values()),performance=all(b['fps_avg']>=200 and 0<b['drawcalls']<=400 for b in outdoor),source_unchanged=all(sha(ART/f'Style11_Atlas{i}.png')==v for i,v in enumerate(m['source_sha'])))
 metrics['checks']=checks;(OUT/'measurements.json').write_text(json.dumps(metrics,ensure_ascii=False,indent=2)+'\n')
 print(f"[CHURCH3D] block_levels={len(levels)} joint_var={metrics['joint_var']:.3f} joint_dark={min(darks):.6f}–{max(darks):.6f} long_stone_every={min(intervals)}–{max(intervals)}")
 print(f"[CHURCH3D] roof_jitter={max(roof_jitter):.2f} eave_shadow_every={min(eaves)}–{max(eaves)} window_mullion_shadow={str(checks['windows']).lower()} glass_glow={str(checks['windows']).lower()}")
 print(f"[CHURCH3D] b_star_quantization_max={b_error:.6f} stone_samples={stone_checks} roof_samples={roof_checks} eave_dark={min(roof_dark):.6f}–{max(roof_dark):.6f}")
 for n,v in bad.items():print(f'[REGRESSION] {n} bad={v}')
 for b in outdoor:print(f"[PERF] {b['pos']} fps={b['fps_avg']:.2f} drawcall={b['drawcalls']}")
 print('[CHURCH3D_GATE] '+json.dumps(checks));return 0 if all(checks.values()) else 1
if __name__=='__main__':sys.exit(main())
