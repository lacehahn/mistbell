#!/usr/bin/env python3
"""Copy the existing unique UV atlases; repaint exterior church charts only."""
import json, math, random, hashlib
from pathlib import Path
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'project/Assets/Art/MaterialForge'
OUT=ROOT/'artifacts/church3d'
def lab(rgb):
 v=[x/255 for x in rgb]; v=[x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in v]
 x,y,z=v; xyz=[(.4124564*x+.3575761*y+.1804375*z)/.95047,.2126729*x+.7151522*y+.072175*z,(.0193339*x+.119192*y+.9503041*z)/1.08883]
 f=[t**(1/3) if t>.008856 else 7.787*t+16/116 for t in xyz]
 return (116*f[1]-16,500*(f[0]-f[1]),200*(f[1]-f[2]))
def rgb(l,a,b):
 y=(l+16)/116;x=y+a/500;z=y-b/200
 f=lambda v:v**3 if v**3>.008856 else (v-16/116)/7.787
 x,y,z=.95047*f(x),f(y),1.08883*f(z)
 vals=(3.2404542*x-1.5371385*y-.4985314*z,-.969266*x+1.8760108*y+.041556*z,.0556434*x-.2040259*y+1.0572252*z)
 g=lambda v:round(255*(12.92*v if v<=.0031308 else 1.055*v**(1/2.4)-.055))
 return tuple(max(0,min(255,g(v))) for v in vals)
def tone(c,g):return tuple(round(x*g) for x in c)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def generate():
 OUT.mkdir(exist_ok=True,parents=True)
 charts=json.loads((ART/'style11_layout.json').read_text())['charts']
 atlases=[Image.open(ART/f'Style11_Atlas{i}.png').convert('RGB') for i in range(3)]
 L,a,b=lab((97,85,70));palette=[rgb(L+d,a,b) for d in (-6,-2,2,6)]
 records=[]; roofs=[]; stone_index=0;next_long=7
 for c in charts:
  family=c['family'];w,h=c['w']-4,c['h']-4
  if family not in ('Stone','Roof_Slate') or c['mesh'].startswith('Terrace') or c['mesh'].startswith('Steps_'):continue
  # Bevel charts keep their shapes, but share the same four face levels and 20% seams.
  if min(w,h)<60 or c['area']<2:
   if family=='Stone':
    tile=atlases[c['atlas']].crop((c['x'],c['y'],c['x']+c['w'],c['y']+c['h']))
    choices=palette+[tone(p,.8) for p in palette]
    mapping={p:p if min(p)>150 else min(choices,key=lambda q:sum((v-u)**2 for v,u in zip(p,q))) for p in tile.getdata()}
    tile.putdata([mapping[p] for p in tile.getdata()]);atlases[c['atlas']].paste(tile,(c['x'],c['y']))
   continue
  r=random.Random(5317+c['id']);im=Image.new('RGB',(w,h));d=ImageDraw.Draw(im)
  if family=='Stone':
   y=0;row=0
   while y<h:
    bh=max(14,round(r.uniform(.30,.39)/c['height']*h));x=-round(r.random()*.35/c['width']*w)
    while x<w:
     stone_index+=1;long=stone_index==next_long
     baseline=max(12,round(.55/c['width']*w));bw=round(baseline*(1.6 if long else r.uniform(.95,1.05)))
     if long:next_long+=r.randint(6,9)
     col=palette[r.randrange(4)];jw=r.choice((3,5,7));jh=r.choice((3,5,7))
     d.rectangle((x,y,x+bw-1,y+bh-1),fill=col)
     d.rectangle((x,y,x+min(jw,bw-1)-1,y+bh-1),fill=tone(col,.8))
     d.rectangle((x,y,x+bw-1,y+min(jh,bh-1)-1),fill=tone(col,.8))
     records.append(dict(chart=c['id'],x=x,y=y,w=bw,h=bh,jw=jw,jh=jh,rgb=col,long=long,index=stone_index,baseline=baseline))
     x+=bw
    y+=bh;row+=1
   # Preserve existing localized exterior snow silhouette exactly.
   old=atlases[c['atlas']].crop((c['x']+2,c['y']+2,c['x']+2+w,c['y']+2+h))
   if c['snow_depths']:
    for xx,depth in enumerate(c['snow_depths']): im.paste(old.crop((xx,0,xx+1,depth)),(xx,0))
  else:
   # Five samples per atlas texel retain 2% displacement on ~10px tile courses.
   im=Image.new('RGB',(w*5,h*5));d=ImageDraw.Draw(im)
   y=0;row=0;next_shadow=6
   while y<h*5:
    step=r.choice((49,50,51));shadow=row==next_shadow
    if shadow:next_shadow+=r.randint(5,7)
    x=-(row%2)*25+r.randint(-1,1)
    while x<w*5:
     bw=50;col=tone((111,113,116),r.choice((.88,.96,1.04,1.12)))
     d.rectangle((x,y,x+bw-1,y+step-1),fill=col)
     d.rectangle((x,y,x+4,y+step-1),fill=tone(col,.8))
     d.rectangle((x,y,x+bw-1,y+4),fill=tone(col,.8))
     if shadow:d.rectangle((x+5,y+5,x+bw-1,y+19),fill=tone(col,.88))
     roofs.append(dict(chart=c['id'],row=row,y=y,x=x,step=step,shadow=shadow,rgb=col))
     x+=bw
    row+=1;y+=step
   im=im.resize((w,h),Image.Resampling.BOX)
  # Existing UVs address these chart interiors; retain their exact atlas placement.
  atlas=atlases[c['atlas']]
  atlas.paste(im.resize((c['w'],c['h']),Image.Resampling.NEAREST),(c['x'],c['y']))
  atlas.paste(im,(c['x']+2,c['y']+2))
 for i,im in enumerate(atlases):im.save(ART/f'Church3D_Atlas{i}.png')
 used={b['chart'] for b in records+roofs}
 (OUT/'texture_manifest.json').write_text(json.dumps(dict(palette=palette,base_lab=[L,a,b],blocks=records,roof=roofs,charts=[{k:c[k] for k in ('id','atlas','x','y','w','h','snow_depths')} for c in charts if c['id'] in used],source_sha=[sha(ART/f'Style11_Atlas{i}.png') for i in range(3)],output_sha=[sha(ART/f'Church3D_Atlas{i}.png') for i in range(3)]),ensure_ascii=False,separators=(',',':'))+'\n')
 print(f'[CHURCH3D_TEXTURES] blocks={len(records)} roof_tiles={len(roofs)} palette={palette}')
if __name__=='__main__':generate()
