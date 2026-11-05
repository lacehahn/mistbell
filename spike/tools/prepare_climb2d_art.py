#!/usr/bin/env python3
"""Reproducible staged JPG -> keyed, cold, tiled RGBA; no generated replacement art."""
from pathlib import Path
from PIL import Image, ImageCms, ImageFilter
import json
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'spike/art-prompts/staged'
DST=ROOT/'spike/project/Assets/Resources/Climb2DArt'
OUT=ROOT/'spike/artifacts/climb2d_art'
NAMES=['side_slope_up','side_slope_up_b','side_slope_down','side_slope_trees','side_path_curve','side_ground_top_oblique','side_step_oblique','side_step_alt','side_ground_cliff','side_step']
TILE=set(NAMES)-{'side_step_oblique','side_step_alt','side_step'}
RGB=ImageCms.createProfile('sRGB'); LAB=ImageCms.createProfile('LAB')
to_lab=ImageCms.buildTransformFromOpenProfiles(RGB,LAB,'RGB','LAB')
to_rgb=ImageCms.buildTransformFromOpenProfiles(LAB,RGB,'LAB','RGB')
# Pixel centres on the exposed top, in the staged S-band's top-left coordinate system.
POINTS=[(10,409),(100,377),(225,350),(340,330),(450,315),(600,312),(750,292),(895,270),(946,245),(916,223),(840,203),(735,183),(630,165),(568,146),(565,127),(605,109),(730,89),(900,78),(1040,66),(1150,45),(1260,10)]

def run():
 DST.mkdir(parents=True,exist_ok=True);OUT.mkdir(parents=True,exist_ok=True)
 records=[]
 for name in NAMES:
  original=Image.open(SRC/(name+'.jpg')).convert('RGB'); w,h=original.size
  # Black threshold and one source-pixel erosion discard JPEG halos; RGB is unassociated.
  mask=Image.new('L',(w,h)); mask.putdata([255 if max(p)>22 else 0 for p in original.getdata()]);mask=mask.filter(ImageFilter.MinFilter(3))
  rgb=Image.new('RGB',(w,h))
  rgb.putdata([tuple(round(c*.92) for c in p) if min(p)>145 and max(p)-min(p)<42 else p for p in original.getdata()])
  lab=ImageCms.applyTransform(rgb,to_lab); data=[]
  poster=name in {'side_slope_trees','side_path_curve','side_ground_top_oblique'}
  for l,a,b in lab.getdata():
   # Luminance-only five-level palette keeps hard edges. Neutral stone -> b*=+5.
   if poster:l=min((48,88,128,168,208),key=lambda v:abs(v-l))
   data.append((l,126 if l>195 else 128,122 if l>195 else 133))
  lab.putdata(data);rgb=ImageCms.applyTransform(lab,to_rgb);rgba=rgb.convert('RGBA');rgba.putalpha(mask)
  if name=='side_step_oblique':
   # Separate tread (incl. <=8 source pixels of snow lip) and exposed riser.
   # Original front lip lies around row 415; compress its 120px snow region to 8px.
   tread=rgba.crop((0,105,w,320));lip=rgba.crop((0,320,w,440)).resize((w,8),Image.Resampling.NEAREST)
   riser=rgba.crop((0,440,w,880))
   rgba=Image.new('RGBA',(w,215+8+440));rgba.paste(tread,(0,0));rgba.paste(lip,(0,215));rgba.paste(riser,(0,223))
  if name in TILE:
   # First/last 10% crossfade towards a shared wrap edge. Interior coordinates
   # stay unchanged, including the S-band and its collision centreline.
   n=round(w*.1); original_rgba=rgba.copy(); p=rgba.load(); q=original_rgba.load()
   for x in range(n):
    t=(1-x/(n-1))*.5
    for y in range(rgba.height):
     left=q[x,y];right=q[w-1-x,y]
     p[x,y]=tuple(round(left[c]*(1-t)+right[c]*t) for c in range(4))
     p[w-1-x,y]=tuple(round(right[c]*(1-t)+left[c]*t) for c in range(4))
   if poster:
    # Quantize after crossfade too: exactly five opaque RGB colours in final PNG.
    quant=ImageCms.applyTransform(rgba.convert('RGB'),to_lab)
    quant.putdata([(level,126 if level>195 else 128,122 if level>195 else 133) for l,a,b in quant.getdata() for level in [min((48,88,128,168,208),key=lambda v:abs(v-l))]])
    final=ImageCms.applyTransform(quant,to_rgb).convert('RGBA');final.putalpha(rgba.getchannel('A'));rgba=final
  if name=='side_step_alt':rgba=rgba.crop(rgba.getbbox())
  rgba.save(DST/(name+'.png'))
  pix=rgba.load();seam=sum(abs(pix[0,y][c]-pix[rgba.width-1,y][c]) for y in range(rgba.height) for c in range(4))/(rgba.height*4)
  measured=ImageCms.applyTransform(rgba.convert('RGB'),to_lab)
  stone=[b-128 for (l,a,b),alpha in zip(measured.getdata(),rgba.getchannel('A').getdata()) if alpha>250 and 45<l<185]
  records.append(dict(name=name,width=rgba.width,height=rgba.height,seam=round(seam,4),hue_b=round(sum(stone)/len(stone),4),poster_levels=5 if poster else 0,alpha_min=min(rgba.getchannel('A').getdata())))
 route={'points':[{'x':x,'y':y} for x,y in POINTS], 'sourceWidth':1280,'sourceHeight':427,'worldScaleX':.75,'worldScaleY':.6,'worldOriginX':40,'worldOriginY':40,'sourceStartY':409}
 (DST/'route.json').write_text(json.dumps(route,indent=2)+'\n')
 (OUT/'processing.json').write_text(json.dumps({'black_threshold':22,'edge_erosion_px':1,'overlap_fraction':.1,'snow_rgb_scale':.92,'step_snow_lip_px':8,'assets':records},indent=2)+'\n')
 for r in records:print(f"{r['name']} seam={r['seam']:.4f} hue_b={r['hue_b']:.4f} levels={r['poster_levels']}")
if __name__=='__main__':run()
