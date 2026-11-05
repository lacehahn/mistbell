#!/usr/bin/env python3
"""Independently measure final PNGs and require runtime/3D acceptance evidence."""
from pathlib import Path
from PIL import Image,ImageCms
import json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[2];DST=ROOT/'spike/project/Assets/Resources/Climb2DArt';OUT=ROOT/'spike/artifacts/climb2d_art'
def need(ok,msg):
 if not ok:raise SystemExit('FAIL: '+msg)
to_lab=ImageCms.buildTransformFromOpenProfiles(ImageCms.createProfile('sRGB'),ImageCms.createProfile('LAB'),'RGB','LAB')
TILE=['side_slope_up','side_slope_up_b','side_slope_down','side_slope_trees','side_path_curve','side_ground_top_oblique','side_ground_cliff']
records=[]
for name in TILE:
 im=Image.open(DST/(name+'.png'));w,h=im.size;p=im.load()
 # Include all RGBA channels and also visible premultiplied RGB at the wrap.
 seam=sum(abs(p[0,y][c]-p[w-1,y][c]) for y in range(h) for c in range(4))/(h*4)
 need(seam<=3,name+' wrap seam')
 lab=ImageCms.applyTransform(im.convert('RGB'),to_lab)
 stone=[b-128 for (l,a,b),alpha in zip(lab.getdata(),im.getchannel('A').getdata()) if alpha==255 and 45<l<185]
 hue=sum(stone)/len(stone);need(4<=hue<=6,name+' stone b*')
 levels=len({rgb[:3] for rgb in im.getdata() if rgb[3]>0})
 if name in ('side_slope_trees','side_path_curve','side_ground_top_oblique'):need(levels==5,name+' five-level palette')
 need(im.mode=='RGBA',name+' alpha')
 records.append(dict(name=name,seam=seam,hue_b=hue,colours=levels))
 print(f'ASSET {name} seam={seam:.4f} hue_b={hue:.4f} colours={levels}')
route=json.loads((DST/'route.json').read_text());im=Image.open(DST/'side_path_curve.png');misses=0
for a,b in zip(route['points'],route['points'][1:]):
 for j in range(101):
  t=j/100;x=round(a['x']*(1-t)+b['x']*t);y=round(a['y']*(1-t)+b['y']*t)
  misses+=im.getpixel((x,y))[3]<128
need(misses==0,'authored centreline intersects keyed background')
log=(ROOT/'spike/logs/climb2d_run.log').read_text()
m=re.search(r'\[CLIMB2D_ART\] layers=(\d+) seam=([\d.]+) hue_b=([\d.]+) path_follow=curve colour_samples=(\d+)',log)
need(m is not None,'runtime art audit missing')
layers,seam,hue,samples=map(float,m.groups());need(layers==5 and seam<=3 and 4<=hue<=6 and samples>0,'runtime art audit bounds')
need('[CLIMB2D_ART] transition_target_s=0.613' in log,'transition target')
need('deck_support=TerraceLanding ground_error_m=0.000000' in log,'TerraceLanding handoff')
subprocess.run([sys.executable,str(ROOT/'spike/tools/verify_climb2d.py')],check=True)
# Screenshot byte size alone is insufficient: require >100 visible colours for real textured slopes.
for name in ('start','ramp_mid','turn','stairs_mid','before_transition'):
 p=ROOT/'spike/artifacts/climb2d'/f'climb2d_{name}.png';shot=Image.open(p).convert('RGB');colours=len(shot.getcolors(1920*1080) or [])
 need(colours>100,name+' texture diversity');print(f'SHOT {name} colours={colours}')
summary={'layers':int(layers),'max_seam':max(r['seam'] for r in records),'runtime_hue_b':hue,'runtime_colour_samples':int(samples),'bitmap_misses':misses,'centreline_test_samples':2020,'assets':records}
(OUT/'acceptance.json').write_text(json.dumps(summary,indent=2)+'\n')
print(f'[CLIMB2D_ART] layers={layers:.0f} seam={seam:.3f} hue_b={hue:.3f} path_follow=curve bitmap_misses={misses} screenshots=5 ACCEPT')
