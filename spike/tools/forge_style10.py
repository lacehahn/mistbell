#!/usr/bin/env python3
"""Deterministic course masonry. Coordinates are image pixels (top down).
CV is width std/mean: the requested height range cannot attain CV .40.
Snow follows ONE shared course ledge, never individual block silhouettes.
"""
import hashlib,json,math,random,statistics
from pathlib import Path
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[1]/'project/Assets/Art/MaterialForge'
BASE=Path(__file__).with_name('style10_frozen.json')
NAMES=('Stone','Roof_Slate','Stone_Sub','Stone_Interior')
DENSITY={n:112 for n in NAMES}
# Calibrated ONLY against rendered fixed ROIs; these are authoring RGB, not Lab gates.
COLORS={'Stone':(97,85,66),'Roof_Slate':(113,115,118),'Stone_Sub':(45,44,43),'Stone_Interior':(80,65,35)}
# 雪压得薄一点、更接近石色：整片高对比白条会让墙变成斑马纹。
SNOW={'Stone':(150,158,168),'Roof_Slate':(150,160,172),'Stone_Sub':(86,94,106),'Stone_Interior':(104,110,120)}

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def freeze():
 if not BASE.exists():
  BASE.write_text(json.dumps({n:sha(ROOT/(n+'_Albedo.png')) for n in ('Snow','Wood','Rock','Grass')},indent=2)+'\n')
 return json.loads(BASE.read_text())
def gain(c,g):return tuple(max(0,min(255,round(v*g))) for v in c)
def joint(c):
 # Match the preceding task's brightness-ratio definition in encoded RGB.
 return gain(c,.78)   # 灰缝要能读出来，但不要回到纯黑硬线
def layout(seed=731):
 r=random.Random(seed); out=[]; y=0; row=0; courses=[]
 # 26 courses, exact periodic sum. +/-6 px ledge ripple, independently phased.
 heights=[r.randint(36,45) for _ in range(26)]
 while sum(heights)!=1024:
  k=r.randrange(len(heights));d=1 if sum(heights)<1024 else -1
  if 36<=heights[k]+d<=45:heights[k]+=d
 for h in heights:
  # 40/35/25% long-tail widths. Sample toward bin ends, avoid narrow wrap residuals.
  bins=[(25,39)]*8+[(40,61)]*7+[(62,95)]*5
  # Deliberate long tail inside each specified bin; exact 40/35/25 counts.
  widths=[r.randint(25,31) for _ in range(8)]+[r.randint(40,61) for _ in range(7)]+[r.randint(84,95) for _ in range(5)]
  while sum(widths)!=1024:
   k=r.randrange(20);d=1 if sum(widths)<1024 else -1
   if bins[k][0]<=widths[k]+d<=bins[k][1]:widths[k]+=d
  r.shuffle(widths)
  shift=r.randrange(1024);x=shift
  # A smooth irregular shared ledge. Actual course thickness remains .26-.46m.
  phase=r.random()*6.28
  ripple=[round(3*__import__('math').sin(xx*__import__('math').tau/256+phase)) for xx in range(1024)]
  courses.append(dict(y=y,h=h,ripple=ripple))
  for col,w in enumerate(widths):
   out.append(dict(row=row,col=col,x=x%1024,y=y,w=w,h=h,tier=r.randrange(5),joint=r.random()>=.25,chip=r.random()<.35))
   x+=w
  y+=h;row+=1
 # Exactly one quarter of interfaces omit mortar (not merely a probability).
 touching=set(random.Random(1025).sample(range(len(out)),len(out)//4))
 for i,b in enumerate(out):b['joint']=i not in touching
 return out,courses

def render(name):
 blocks,courses=layout();r=random.Random(2104)
 im=Image.new('RGB',(1024,1024));mask=Image.new('L',im.size);px=im.load();mp=mask.load();base=COLORS[name]
 # Masks: 0 body,1 joint,2 upper rim,3 snow,4 chips.
 for b in blocks:
  c=gain(base,.88+b['tier']*.06)   # 块与块之间 ±12% 明度差，避免整面墙糊成一块
  for dx in range(b['w']):
   x=(b['x']+dx)%1024
   top=b['y']+courses[b['row']]['ripple'][x]
   # Shared boundaries with preceding/following course (no holes/overdraw).
   nex=courses[(b['row']+1)%len(courses)]
   bottom=b['y']+b['h']+nex['ripple'][x]
   for yy in range(top,bottom):
    y=yy%1024;v=c;label=0
    if dx==0 and b['joint']:v=joint(c);label=1
    if yy==top and dx>0:v=gain(c,1.045);label=2
    # Tiny isolated face chips: no enclosing lower/side bevels.
    if b['chip'] and 4<=dx<7 and 10<=yy-top<12:v=gain(c,.91);label=4
    px[x,y]=v;mp[x,y]=label
 # 稀疏积雪：只有少数层、且只在局部段上堆，避免整面墙变成"横条纹"。
 # 参考图里雪是压在墙顶/檐口/局部台面上的斑块，不是每层都有。
 for ci,course in enumerate(courses):
  if not (ci==0 or ci%4!=0):            # 墙顶层必有一道，其余约 75% 的层（但只在局部段、且很薄）
   continue
  for x in range(1024):
   # 低频正弦决定这一段是否有雪，约 45% 宽度
   if math.sin(x*0.0135+ci*1.9)*0.5+0.5 < 0.55:
    continue
   top=course['y']+course['ripple'][x]
   depth=3+(int((x+ci*17)/19)+ci*3)%4   # 3..6 px
   if (x+ci*41)%97 in (0,1):depth+=1     # 偶发下垂
   for dy in range(depth):
    y=(top-1-dy)%1024;px[x,y]=SNOW[name];mp[x,y]=3
 return im,mask,blocks,courses

def generate():
 frozen=freeze();assert all(sha(ROOT/(n+'_Albedo.png'))==s for n,s in frozen.items())
 info={}
 for n in NAMES:
  im,mask,b,c=render(n);im.save(ROOT/(n+'_Albedo.png'))
  Image.new('RGB',im.size,(245,245,245)).save(ROOT/(n+'_Roughness.png'))
  info[n]={'texels_per_m':DENSITY[n],'blocks':b,'courses':c}
 (ROOT/'style10_layout.json').write_text(json.dumps(info,separators=(',',':'))+'\n')
 print('[STYLE10_GENERATED] four 1024 textures; course-wide snow; frozen SHA checked',flush=True)
if __name__=='__main__':generate()
