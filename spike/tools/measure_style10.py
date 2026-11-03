#!/usr/bin/env python3
"""STYLE10: raster/layout verification and fixed render-region Lab gates.
No source-texture Lab is used for tone. Width CV is used because height's
.26-.46m bounded range mathematically cannot have a .40 CV.
Grid score = largest fraction of actual RGB transition edges on exact equally
spaced grid lines (all candidate periods within authored width/height bounds).
Joint ratio = display RGB Rec.709 joint/body, paired on same block.
"""
import argparse,collections,hashlib,json,math,re,statistics
from pathlib import Path
from PIL import Image,ImageDraw
from forge_style10 import ROOT,BASE,NAMES,DENSITY,render,sha
from measure_style import lab,frame_gate
SHOTS=ROOT.parents[2]/'Shots'
REGIONS=Path(__file__).with_name('style10_regions.json')

def luma(p):
 return sum(a*b for a,b in zip(p,(.2126,.7152,.0722)))
def material(n):
 expected,mask,blocks,courses=render(n);im=Image.open(ROOT/(n+'_Albedo.png')).convert('RGB')
 counts=collections.Counter(mask.getdata());px=im.load();m=mask.load();ratios=[]
 for b in blocks:
  if not b['joint']:continue
  x=b['x'];y=(b['y']+courses[b['row']]['ripple'][x]+15)%1024
  # use paired joint/inside texels, away from snow and chips.
  if m[x,y]==1 and m[(x+2)%1024,y]==0:
   ratios.append(luma(px[x,y])/luma(px[(x+2)%1024,y]))
 widths=[b['w']/DENSITY[n] for b in blocks];heights=[(c['h']+courses[(i+1)%len(courses)]['ripple'][x]-c['ripple'][x])/DENSITY[n] for i,c in enumerate(courses) for x in range(1024)]
 # Count actual raster transitions, including absent joints and snow edges.
 edge_x=collections.Counter();edge_y=collections.Counter();pixels=list(im.getdata())
 for y in range(1024):
  row=y*1024;previous=((y-1)%1024)*1024
  for x in range(1024):
   if pixels[row+x]!=pixels[row+(x-1)%1024]:edge_x[x]+=1
   if pixels[row+x]!=pixels[previous+x]:edge_y[y]+=1
 grid=0
 for edges,lo,hi in ((edge_x,25,95),(edge_y,30,51)):
  for period in range(lo,hi+1):
   bins=collections.Counter()
   for position,count in edges.items():bins[position%period]+=count
   grid=max(grid,max(bins.values())/sum(edges.values()))
 histogram=[sum(lo<=w<=hi for w in widths)/len(widths) for lo,hi in ((.22,.35),(.35,.55),(.55,.85))]
 # Manifest must be exactly the deterministic layout, actual texture bytes must match.
 layout=json.loads((ROOT/'style10_layout.json').read_text())[n]
 return dict(size=im.width,block_w_m=[min(widths),max(widths)],block_h_m=[min(heights),max(heights)],size_cv=statistics.pstdev(widths)/statistics.mean(widths),chips_pct=sum(b['chip'] for b in blocks)/len(blocks),chip_pixel_pct=counts[4]/1024**2,joint_ratio=statistics.mean(ratios),snow_cap_pct=counts[3]/1024**2,grid_score=grid,no_joint_pct=sum(not b['joint'] for b in blocks)/len(blocks),width_bins=histogram,raster_ok=im.size==(1024,1024) and im.tobytes()==expected.tobytes() and layout['blocks']==blocks,source_sha=sha(ROOT/(n+'_Albedo.png')))

def regions():
 values={};evidence={};spec=json.loads(REGIONS.read_text())
 for name,entry in spec.items():
  if name=='method':continue
  im=Image.open(SHOTS/entry['shot']).convert('RGB');w,h=im.size
  box=tuple(round(v*(w if i%2==0 else h)) for i,v in enumerate(entry['rect']))
  crop=im.crop(box);count=collections.Counter(crop.getdata());total=crop.width*crop.height
  avg=[sum(lab(p)[i]*n for p,n in count.items())/total for i in range(3)]
  values[name]=avg;evidence[name]=dict(**entry,mean_lab=avg,sha=sha(SHOTS/entry['shot']),pixel_count=total)
  draw=ImageDraw.Draw(im);draw.rectangle(box,outline='red',width=3);im.save(SHOTS/('style10_roi_'+name+'.png'))
 return values,evidence

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--bench');parser.add_argument('--materials-only',action='store_true');a=parser.parse_args()
 mats={n:material(n) for n in NAMES}
 for n,v in mats.items():
  print(f"[STYLE10] {n} size={v['size']} block_w_m={v['block_w_m'][0]:.4f}-{v['block_w_m'][1]:.4f} block_h_m={v['block_h_m'][0]:.4f}-{v['block_h_m'][1]:.4f} size_cv={v['size_cv']:.6f} chips_pct={v['chips_pct']:.6f} joint_ratio={v['joint_ratio']:.6f} snow_cap_pct={v['snow_cap_pct']:.6f} grid_score={v['grid_score']:.6f}",flush=True)
 frozen=json.loads(BASE.read_text());unchanged={n:sha(ROOT/(n+'_Albedo.png'))==s for n,s in frozen.items()}
 print('[STYLE10_UNCHANGED] '+' '.join(n+'_sha_same='+str(v) for n,v in unchanged.items()),flush=True)
 gates=dict(cv_ok=all(v['size_cv']>=.4 for v in mats.values()),joint_ok=all(.72<=v['joint_ratio']<=.90 and abs(v['no_joint_pct']-.25)<1e-9 for v in mats.values()),snow_ok=all(.025<=v['snow_cap_pct']<=.14 for v in mats.values()),grid_ok=all(v['grid_score']<=.10 for v in mats.values()),raster_ok=all(v['raster_ok'] for v in mats.values()),size_ok=all(.22<=v['block_w_m'][0]<=v['block_w_m'][1]<=.85 and .26<=v['block_h_m'][0]<=v['block_h_m'][1]<=.46 and all(abs(x-t)<.005 for x,t in zip(v['width_bins'],(.4,.35,.25))) for v in mats.values()),unchanged_ok=all(unchanged.values()))
 frame={};evidence={};bench=None;tones={}
 if not a.materials_only:
  v,evidence=regions();tones=dict(church_L=v['church'][0],church_b=v['church'][2],roof_L=v['roof'][0],wall_minus_roof=v['church'][0]-v['roof'][0],sub_L=v['sub'][0],interior_L=v['interior'][0])
  print('[STYLE10_FRAME] '+' '.join(k+'='+f'{val:.6f}' for k,val in tones.items()),flush=True)
  gates['tone_ok']=34<=tones['church_L']<=42 and 11<=tones['church_b']<=17 and 30<=tones['roof_L']<=36.5 and tones['wall_minus_roof']>=3.5 and 10<=tones['sub_L']<=17 and tones['interior_L']<=tones['church_L']-8
  frame=frame_gate();gates['frame_ok']=frame['pass'];print('[STYLE10_FULL_FRAME] '+json.dumps(frame),flush=True)
  manifest=json.loads((ROOT/'manifest.json').read_text());project=ROOT.parents[2]
  projection={}
  for entry in manifest['materials']:
   if entry['name'] not in NAMES:continue
   text=(project/entry['material']).read_text();match=re.search(r'- _BaseMap:.*?m_Scale: \{x: ([\d.eE+-]+)',text,re.S)
   actual=float(match[1])*1024;projection[entry['name']]=actual
  gates['projection_ok']=len(projection)==4 and all(abs(v-112)<.0001 for v in projection.values())
  print('[STYLE10_PROJECTION] '+json.dumps(projection),flush=True)
  # Refuse stale screenshots after texture authoring. Capture evidence contains build hashes.
  captured=json.loads((SHOTS/'style10_capture.json').read_text())
  gates['capture_ok']=all(captured['textures'][n]==mats[n]['source_sha'] for n in NAMES) and all(captured['shots'][n]==sha(SHOTS/n) for n in captured['shots'])
 if a.bench:
  bench=json.loads(Path(a.bench).read_text());samples=bench['results'];gates['performance_ok']=len(samples)==3 and {r['pos'] for r in samples}=={'z01','terrace','interior'} and all(r['fps_avg']>=200 and 0<r['drawcalls']<=330 for r in samples)
  print('[STYLE10_PERF] '+json.dumps(bench),flush=True)
 passed=all(gates.values());print('[STYLE10_GATE] '+' '.join(k+'='+str(v) for k,v in gates.items())+' pass='+str(passed),flush=True)
 (ROOT/'measurement_style10.json').write_text(json.dumps(dict(materials=mats,unchanged=unchanged,tones=tones,regions=evidence,frame=frame,performance=bench,gates=gates,pass_gate=passed),indent=2)+'\n')
 return 0 if passed else 1
if __name__=='__main__':raise SystemExit(main())
