"""Task-specific independent PNG / serialized-material / runtime / budget checks."""
import json,re,math,hashlib
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1]; O=R/'artifacts/interior_pilasters_dome';O.mkdir(parents=True,exist_ok=True)
def lab(rgb):
 c=[v/255 for v in rgb];r,g,b=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in c]
 xyz=[(.4124564*r+.3575761*g+.1804375*b)/.95047,.2126729*r+.7151522*g+.072175*b,(.0193339*r+.119192*g+.9503041*b)/1.08883]
 x,y,z=[v**(1/3) if v>216/24389 else v*24389/3132+16/116 for v in xyz]
 return 116*y-16,500*(x-y),200*(y-z)
def samples(view,maskname,colors,roi=lambda name,x,y:True):
 im=Image.open(R/f'project/Shots/interior_{view}.png').convert('RGB');mask=Image.open(R/f'project/Shots/{maskname}.png').convert('RGB');data={k:[] for k in colors}
 inv={v:k for k,v in colors.items()}
 for y in range(mask.height):
  for x in range(mask.width):
   name=inv.get(mask.getpixel((x,y)))
   if name and roi(name,x/mask.width,y/mask.height):
    rgb=im.getpixel((x*4+2,y*4+1));data[name].append((*lab(rgb),sum(a*b for a,b in zip(rgb,[.2126,.7152,.0722]))/255))
 return {n:dict(pixels=len(v),**{k:sum(row[j] for row in v)/len(v) if v else None for j,k in enumerate(['L','a','b','luma'])}) for n,v in data.items()}
def mat(name):return (R/f'project/Assets/Settings/{name}.mat').read_text()
def color(name,key):
 m=re.search(r'- '+key+r': \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)',mat(name));return [float(v) for v in m.groups()]
def luma(c):return sum(a*b for a,b in zip(c,[.2126,.7152,.0722]))
build=(R/'logs/interior_pilasters_dome_build.log').read_text();entry=(R/'logs/interior_pilasters_dome_interior.log').read_text()
p=samples('inside','interior_pilasters_inside_mask',{'shaft':(255,0,0),'base':(0,0,255),'cap':(0,255,0),'wall':(255,255,0),'gold':(255,0,255)})
w=samples('inside','interior_tone_inside_mask',{'left':(255,255,0),'right':(255,0,255),'altar':(0,255,0)},lambda n,x,y:n!='altar' or .48<=x<=.52 and .47<=y<=.59)
w.update(samples('lookup','interior_tone_lookup_mask',{'ceiling':(0,0,255)}))
d=samples('lookup','interior_windows_lookup_mask',{'window':(0,0,255),'plaster':(0,255,0),'rib_top':(255,255,0),'rib_side':(255,0,255)})
upper=p['shaft'].copy();total=upper['pixels']+p['base']['pixels']
p['upper_shaft']=upper
p['shaft']={k:(upper[k]*upper['pixels']+p['base'][k]*p['base']['pixels'])/total for k in ['L','a','b','luma']}
p['shaft']['pixels']=total
shaft=p['shaft'];wall=p['wall'];chroma=math.hypot(shaft['a']-wall['a'],shaft['b']-wall['b']);hue=lambda s:math.degrees(math.atan2(s['b'],s['a']));hd=abs(hue(shaft)-hue(wall));hd=min(hd,360-hd)
trim_gain=luma(color('M_InteriorPilasterTrim','_BaseColor'))/luma(color('M_InteriorPilasterShaft','_BaseColor'))-1
rib_gain=luma(color('M_InteriorDome2RibTop','_BaseColor'))/luma(color('M_InteriorWindowsPlaster','_BaseColor'))-1
rib_side=1-luma(color('M_InteriorDome2RibSide','_BaseColor'))/luma(color('M_InteriorDome2RibTop','_BaseColor'))
goldrow=re.findall(r'\[YELLOW_STRIP\].*',entry);gold=float(re.search(r' luma=([\d.]+)',goldrow[-1]).group(1)) if goldrow else 999
# Each disabled control differs only by one shaft; static scene pixels dominate over animated candle noise.
im=Image.open(R/'project/Shots/interior_inside.png').convert('RGB');beams=[]
for k in range(3):
 im=Image.open(R/f'project/Shots/pilasters_with_Interior_LightShaft_{k}.png').convert('RGB')
 ctrl=Image.open(R/f'project/Shots/pilasters_without_Interior_LightShaft_{k}.png').convert('RGB');diff=[]
 for y in range(100,850,2):
  for x in range(150,1770,2):
   a=im.getpixel((x,y));b=ctrl.getpixel((x,y));v=luma(a)-luma(b)
   if v>=1:diff.append(v/255)
 beams.append(dict(index=k,positive_pixels=len(diff),mean_positive_delta=sum(diff)/len(diff) if diff else 0))
bench=json.loads((R/'logs/interior_pilasters_dome_bench.json').read_text());perf=next(v for v in bench['results'] if v['pos']=='interior')
prot=json.loads((O/'protected_digests.json').read_text())
checks={'build':'[PERF_BUILD] result=Succeeded errors=0' in build,'protected':prot['before']==prot['after'],'runtime_errors':not re.search('Exception:|Shader error|entry gates failed',entry),'entry':bool(re.search(r'\[INT_PASS\] light=True domeLight=True ceilingSeen=True domeSeen=True .*visible=True obstacles=True walked=True .*lamps=20 shafts=3',entry)), 'base_L':p['base']['pixels']>0 and 30<=p['base']['L']<=36,'shaft_L':30<=shaft['L']<=36,'shaft_b':9<=shaft['b']<=11,'color_shift':chroma<=3,'hue_delta':hd<=3,'cap_contrast':trim_gain>=.08,'rib_top_gain':rib_gain>=.10,'rib_side_delta':rib_side>=.15,'window_L':68<=d['window']['L']<=72,'black_bits':'black_bits=0' in build,'mullions':'mullions=12' in build,'gold_frame':gold<=.55,'beams':all(v['positive_pixels']>=100 and v['mean_positive_delta']>=.004 for v in beams),'performance':0<perf['drawcalls']<=300 and perf['fps_avg']>=200}
lit,shadow=sorted([w['left'],w['right']],key=lambda s:s['L'],reverse=True)
checks.update(wall_lit=36<=lit['L']<=42,wall_shadow=20<=shadow['L']<=24,wall_split=lit['L']-shadow['L']>=12,wall_chroma=all(8<=v['b']<=11 and 2<=v['a']<=5 for v in [lit,shadow]),ceiling=w['ceiling']['L']<=22,altar=28<=w['altar']['L']<=34)
shader=(R/'project/Assets/Art/Shaders/InteriorDomeMottle.shader').read_text();checks['mottle']='0.04*(2.0*n-1.0)' in shader and 'input.positionWS/0.9' in shader
shots={}
for n in ['interior_inside','interior_lookup']:
 f=R/f'project/Shots/{n}.png';shots[n]=dict(size=Image.open(f).size,sha256=hashlib.sha256(f.read_bytes()).hexdigest(),fresh=f.stat().st_mtime>(R/'logs/interior_pilasters_dome_build.log').stat().st_mtime)
checks['gold_multiplier']=all(abs(a-b*.93)<.00001 for key in ['_BaseColor','_EmissionColor'] for a,b in zip(color('M_InteriorV2Gold_RoyalDoor',key)[:3],color('M_InteriorV2Gold',key)[:3]))
checks['shaft_alpha_retained']=all(abs(float(v)-.08)<.0001 for v in re.findall(r'shaft_alpha=([\d.]+)',entry))
checks['idempotent']='idempotent=true' in build
geo=re.search(r'\[DOME2_GEOMETRY\].*',build).group(0)
geometry={k:float(v) for k,v in re.findall(r'(\w+)=([0-9.]+)',geo)}
checks['geometry']=all(abs(geometry[k]-expected)<.0001 for k,expected in [('rib_width_min',.12),('rib_width_max',.12),('rib_depth_min',.05),('rib_depth_max',.05),('mullion_width_min',.03),('mullion_width_max',.03)]) and geometry['max_decoration_aabb']<=.06
checks['fresh_shots']=all(v['size']==(1920,1080) and v['fresh'] for v in shots.values())
print('[PILASTER] color_shift_vs_wall=%.4f hue_delta=%.4f base_L=%s shaft_L=%.4f b_star=%.4f cap_contrast=%.4f metric=PNG_CIELAB_chroma_distance_hue_degrees trim_metric=material_Rec709'%(chroma,hd,p['base']['L'],shaft['L'],shaft['b'],trim_gain))
print('[DOME2] mottle_amp=0.04 scale_m=0.9 rib_side_delta=%.4f rib_top_gain=%.4f window_L=%.4f black_bits=%d'%(rib_side,rib_gain,d['window']['L'],0 if checks['black_bits'] else -1))
print('[GOLD_FRAME] luma=%.5f unchanged_geometry=%s metric=PNG_display_Rec709_left_vertical_lower_half'%(gold,checks['protected']))
print('[BEAMS] visible=%s count=%d'%(checks['beams'],sum(v['positive_pixels']>=100 for v in beams)))
report=dict(geometry=geometry,base_L_definition='lowest exposed shaft below world y=3.5m; foot molding itself hidden by pews',accepted=all(checks.values()),checks=checks,pilasters=p,color_shift_vs_wall=chroma,hue_delta=hd,cap_contrast=trim_gain,dome=d,rib_side_delta=rib_side,rib_top_gain=rib_gain,gold_frame_luma=gold,gold_frame_original_view_luma=float(re.search(r' luma=([\d.]+)',goldrow[0]).group(1)),beams=beams,regression=w,interior=perf,shots=shots)
(O/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps(report,ensure_ascii=False,indent=2));raise SystemExit(0 if report['accepted'] else 1)
