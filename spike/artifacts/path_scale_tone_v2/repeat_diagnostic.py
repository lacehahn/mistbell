import math,statistics,random,json
from PIL import Image
from pathlib import Path
R=Path("spike");tex=Image.open(R/"project/Assets/Art/Textures/Path/path_stone_snow.jpg").convert("RGB");w,h=tex.size;px=tex.load()
def linear(v):
 v/=255;return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
def sample(x,y):
 x=(x%1)*w-.5;y=(1-y%1)*h-.5;ix=math.floor(x);iy=math.floor(y);fx=x-ix;fy=y-iy
 return sum(sum(linear(v)*c for v,c in zip(px[(ix+dx)%w,(iy+dy)%h],[.2126729,.7151522,.072175]))*(fx if dx else 1-fx)*(fy if dy else 1-fy) for dx in [0,1] for dy in [0,1])
def tile_offset(x,z):
 h=(x*374761393+z*668265263)&0xffffffff;h=((h^(h>>13))*1274126177)&0xffffffff;h^=h>>16
 return (h&65535)/65536,(h>>16)/65536
def scramble(u,v,x,z):
 ix=math.floor(x);iz=math.floor(z);fx=x-ix;fz=z-iz;fx=fx*fx*(3-2*fx);fz=fz*fz*(3-2*fz)
 total=0
 for dx in [0,1]:
  for dz in [0,1]:
   ox,oz=tile_offset(ix+dx,iz+dz);total+=sample(u+ox,v+oz)*(fx if dx else 1-fx)*(fz if dz else 1-fz)
 return total
def road(x,z):
 x/=1.35;z/=1.35;return .68*sample(.7986355*x-.601815*z,.601815*x+.7986355*z)+.32*sample((.7986355*x+.601815*z)*.713+.317,(-.601815*x+.7986355*z)*.713+.613)
def corr(x,y):
 mx=statistics.mean(x);my=statistics.mean(y);return sum((a-mx)*(b-my) for a,b in zip(x,y))/math.sqrt(sum((a-mx)**2 for a in x)*sum((b-my)**2 for b in y))
rng=random.Random(135);points=[(rng.uniform(-16,16),rng.uniform(-16,16)) for _ in range(3000)];correlations=[]
for texture_name in ['path_stone_a.jpg','path_stone_snow.jpg']:
 tex=Image.open(R/'project/Assets/Art/Textures/Path'/texture_name).convert('RGB');w,h=tex.size;px=tex.load();base=[road(*p) for p in points]
 for layer,vecs in [('a',[(.7986355,-.601815),(.601815,.7986355)]),('b',[(.7986355/.713,.601815/.713),(-.601815/.713,.7986355/.713)])]:
  for vx,vz in vecs:
   for k in [1,2,3]:correlations.append({'texture':texture_name,'layer':layer,'tiles':k,'shift':[vx*k*1.35,vz*k*1.35],'correlation':corr(base,[road(x+vx*k*1.35,z+vz*k*1.35) for x,z in points])})
repeat=max(v['correlation'] for v in correlations)

print(repeat)
Path("spike/artifacts/path_scale_tone_v2/repeat.json").write_text(json.dumps({"repeat_score":repeat,"correlations":correlations},indent=2))
