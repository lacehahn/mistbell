#!/usr/bin/env python3
"""Unique paintings in three atlases. All dimensions come from world-space UV charts.
Large structural walls use 1024px; bevels, cut reveals and trim use smaller charts.
No periodic functions, tiled samples or random pixel noise.
"""
import json,random,math
from pathlib import Path
from PIL import Image,ImageDraw
from forge_style10 import ROOT,BASE,sha
COLORS={'Stone':(97,85,70),'Roof_Slate':(111,113,116),'Stone_Sub':(45,44,43),'Stone_Interior':(88,72,39)}
def color(c,g):return tuple(min(255,max(0,round(v*g))) for v in c)
def paint(c):
 w,h=c['w']-4,c['h']-4;r=random.Random(110031+c['id']*971)
 im=Image.new('RGB',(w,h));d=ImageDraw.Draw(im);base=COLORS[c['family']];blocks=[]
 # World dimensions set masonry size; no repeated texture-space cell.
 y=0;row=0
 while y<h:
  bottom=y/h;hm=r.uniform(.28+.06*bottom,.35+.10*bottom)
  bh=max(2,round(hm/c['height']*h));x=-round(r.random()*.5/c['width']*w)
  while x<w:
   bw=max(2,round(r.uniform(.4+.15*bottom,.65+.25*bottom)/c['width']*w));tier=r.randrange(5);stone=color(base,(.88,.94,1,1.06,1.12)[tier])
   d.rectangle((x,y,x+bw-1,min(h-1,y+bh-1)),fill=stone)
   if r.random()>=.25:d.line((x,y,x,y+bh-1),fill=color(stone,.72))
   d.line((x+1,y,x+bw-1,y),fill=color(stone,1.20))
   if r.random()>=.25:d.line((x+1,y+bh-1,x+bw-1,y+bh-1),fill=color(stone,.72))
   if r.random()<.30:d.rectangle((x+1,y+1,x+2,y+2),fill=color(stone,.88))
   blocks.append([x,y,bw,bh,tier]);x+=bw
  y+=bh;row+=1
 # Large quoin stones, painted at both structural edges (world-space .7-.9m high).
 if c['wall']:
  edge=max(2,round(w*.09))
  for side in (0,1):
   y=0
   while y<h:
    bh=max(2,round(r.uniform(.7,.9)/c['height']*h));x=0 if side==0 else w-edge
    stone=color(base,1.12 if side==0 else .88)
    d.rectangle((x,y,x+edge-1,y+bh-1),fill=stone);d.line((x,y,x+edge-1,y),fill=color(stone,1.20));y+=bh
 # Bottom contact darkening is deliberately limited to the bottom 8%.
 overlay=im.copy();pix=im.load();original=overlay.load()
 for yy in range(round(h*.92),h):
  gain=.88 if yy<h-2 else .72
  for xx in range(w):pix[xx,yy]=color(original[xx,yy],gain)
 # Only exterior near-vertical structural walls carry snow. No snow in interior/roof.
 snow=c['wall'] and c['family'] in ('Stone','Stone_Sub')
 depths=[]
 if snow:
  # Piecewise linear irregular silhouette: 3-5 localized droops, mean thickness 3%.
  anchors=[r.uniform(-.004,.004) for _ in range(10)]
  for xx in range(w):
   t=xx/(max(1,w-1))*9;i=min(8,int(t));v=anchors[i]*(1-(t-i))+anchors[i+1]*(t-i)
   droop=sum(.014*max(0,1-abs(xx/w-center)/.025) for center in (.17,.39,.62,.86))
   depth=max(1,round(h*(.0285+v+droop)));depths.append(depth)
   d.line((xx,0,xx,depth-1),fill=(220,231,242));d.point((xx,depth-1),fill=(185,200,216))
 c['snow_depths']=depths;c['blocks']=blocks
 return im

def generate():
 assert all(sha(ROOT/(n+'_Albedo.png'))==s for n,s in json.loads(BASE.read_text()).items())
 layout=json.loads((ROOT/'style11_layout.json').read_text());charts=layout['charts']
 for c in charts:
  long=max(c['width'],c['height']);short=min(c['width'],c['height'])
  size=1024 if c['wall'] else 512 if c['area']>45 else 256 if c['area']>12 else 128 if c['area']>2 else 64 if c['area']>.1 else 32 if c['area']>.01 else 16
  c['w']=size if c['width']>=c['height'] or short/long>.85 else max(16,size//2)
  c['h']=size if c['height']>=c['width'] or short/long>.85 else max(16,size//2)
 # Pack the complete collection across three sheets, largest first.
 shelves=[dict(x=0,y=0,h=0) for _ in range(3)]
 for c in sorted(charts,key=lambda c:(-c['h'],-c['w'],c['id'])):
  choices=[]
  for ai,shelf in enumerate(shelves):
   xx,yy,hh=shelf['x'],shelf['y'],shelf['h']
   if xx+c['w']>4096:xx=0;yy+=hh;hh=0
   if yy+c['h']<=4096:choices.append((yy,xx,ai,hh))
  if not choices:raise RuntimeError('three-atlas capacity exceeded')
  yy,xx,ai,hh=min(choices);c.update(atlas=ai,x=xx,y=yy);shelves[ai]=dict(x=xx+c['w'],y=yy,h=max(hh,c['h']))
 for atlas in range(3):
  items=sorted((c for c in charts if c['atlas']==atlas),key=lambda c:(c['y'],c['x']))
  im=Image.new('RGB',(4096,4096))
  for c in items:
   tile=paint(c);im.paste(tile,(c['x']+2,c['y']+2))
   # Two texels of edge extrusion prevent cross-chart sampling at vertices.
   im.paste(tile.resize((c['w'],c['h']),Image.Resampling.NEAREST),(c['x'],c['y']));im.paste(tile,(c['x']+2,c['y']+2))
  im.save(ROOT/f'Style11_Atlas{atlas}.png')
  print(f"[STYLE11_AUTHOR] atlas={atlas} charts={len(items)} occupied_height={shelves[atlas]['y']+shelves[atlas]['h']}",flush=True)
 (ROOT/'style11_layout.json').write_text(json.dumps(layout,separators=(',',':'))+'\n')
if __name__=='__main__':generate()
