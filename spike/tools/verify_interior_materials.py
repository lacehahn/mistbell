"""Task-local acceptance: saved material values, fresh player PNGs, mesh-labelled
pixel samples, entry assertions, and actual 300-frame render counters.
"""
import hashlib
import json
import re
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'project/Assets'
logs = ROOT / 'logs'
build = (logs / 'interior_materials_build.log').read_text()
entry = (logs / 'interior_materials_interior.log').read_text()
altar_log = (logs / 'interior_materials_altar.log').read_text()
bench = json.loads((logs / 'interior_materials_bench.json').read_text())
interior = next(r for r in bench['results'] if r['pos'] == 'interior')
def mat(name):
    return (ASSETS / f'Settings/{name}.mat').read_text()
def scalar(s, name):
    return float(re.search(rf'^    - {re.escape(name)}: ([\d.eE+-]+)$', s, re.M)[1])
def color(s, name):
    return list(map(float, re.search(rf'- {re.escape(name)}: \{{r: ([\d.eE+-]+), g: ([\d.eE+-]+), b: ([\d.eE+-]+), a: ([\d.eE+-]+)\}}', s).groups()))
def luma(c):
    return sum(a*b for a,b in zip(c, (.2126, .7152, .0722)))
def eq(a,b):
    return abs(a-b) < 1e-5
wood = mat('M_InteriorMaterialsSeat')
leg = mat('M_InteriorMaterialsLeg')
rail = mat('M_InteriorMaterialsRail')
stone = mat('M_AltarPainted')
flame = mat('M_InteriorMaterialsFlame')
halo = mat('M_InteriorMaterialsHalo')
w, s = color(wood, '_BaseColor'), color(stone, '_StoneColor')
period, contrast = scalar(wood, '_GrainPeriod'), scalar(wood, '_GrainContrast')
darker = 1-luma(color(leg, '_BaseColor'))/luma(w)
rail_gain = luma(color(rail, '_BaseColor'))/luma(w)-1
delta = (luma(s)-luma(w))*100
joint = scalar(stone, '_JointContrast')
shots = {}
for name in ['pews_closeup','altar_closeup','interior_inside']:
    p = ROOT / f'project/Shots/{name}.png'
    shots[name] = {'size':list(Image.open(p).size),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'fresh':p.stat().st_mtime > (logs/'interior_materials_build.log').stat().st_mtime}
frame = Image.open(ROOT/'project/Shots/pews_closeup.png').convert('RGB')
samples = json.loads((ROOT/'project/Shots/interior_materials_samples.json').read_text())
yellow = []
for name in sorted(set(p['name'] for p in samples['yellow'])):
    points = [p for p in samples['yellow'] if p['name']==name]
    if name=='Interior_RoyalDoor_Frame':
        mid_x=(min(p['x'] for p in points)+max(p['x'] for p in points))/2
        mid_y=(min(p['y'] for p in points)+max(p['y'] for p in points))/2
        points=[p for p in points if p['x']<mid_x and p['y']<mid_y]
    values = [luma([c/255 for c in frame.getpixel((p['x'],frame.height-1-p['y']))]) for p in points]
    yellow.append({'name':name,'material':points[0]['material'],'pixels':len(values),'luma':sum(values)/len(values),'max_luma':max(values)})
counts = re.search(r'\[CANDLE_GLOW\] flames=(\d+) flames_glow=(\d+)',build)
checks = {
    'build': '[PERF_BUILD] result=Succeeded errors=0' in build and 'Shader error' not in build and not re.search(r'error CS\d',build),
    'protected': json.loads((ROOT/'artifacts/interior_materials/protected_digests.json').read_text())['before']==json.loads((ROOT/'artifacts/interior_materials/protected_digests.json').read_text())['after'] and '[MATERIALS_PROTECTED] floor_exterior_camera_royal_door_icons_pew_geometry_colliders=true' in build,
    'pew_base': all(eq(a,b/255) for a,b in zip(w,[138,101,68])),
    'grain': .18<=period<=.26 and .05<=contrast<=.06+1e-6,
    'legs_ends': .12<=darker<=.15,
    'rail': .10<=rail_gain<=.12,
    'stone_base': all(eq(a,b/255) for a,b in zip(s,[140,132,120])),
    'stone_luma_delta': 8<=delta<=12,
    'stone_joints': joint>=.18 and eq(scalar(stone,'_JointWidth'),.012) and eq(scalar(stone,'_CourseHeight'),.22),
    'stone_specular': eq(scalar(stone,'_Specular'),.05),
    'flames': counts and int(counts[1])==15 and counts[1]==counts[2] and eq(color(flame,'_BaseColor')[3],.75) and eq(color(halo,'_BaseColor')[3],.10),
    'yellow_strip': any(p['name']=='Interior_RoyalDoor_Frame' for p in yellow) and all(p['luma']<=.55 for p in yellow if p['name']=='Interior_RoyalDoor_Frame'),
    'entry': bool(re.search(r'\[INT_PASS\] light=True domeLight=True ceilingSeen=True domeSeen=True .*visible=True obstacles=True walked=True .*lamps=20 shafts=3', entry)) and 'entry gates failed' not in entry,
    'colliders': len(re.findall(r'\[INTERIOR_COLLIDE\] expected=.*pass=false',entry))==0,
    'budget': 0<interior['drawcalls']<=300 and interior['fps_avg']>=200,
    'shots': all(v['size']==[1920,1080] and v['fresh'] for v in shots.values()),
    'fresh_benchmark': (logs/'interior_materials_bench.json').stat().st_mtime > (logs/'interior_materials_build.log').stat().st_mtime,
    'no_runtime_exceptions': not re.search(r'(NullReferenceException|MissingReferenceException|Shader error)',entry+altar_log),
    'four_tags': all(f'[{tag}]' in build+entry for tag in ['PEW_MAT','ALTAR_STONE','CANDLE_GLOW','YELLOW_STRIP']),
}
report = {'accepted':all(checks.values()),'checks':checks,'pew':{'base':'#8A6544','grain_period':period,'contrast':contrast,'leg_darker':darker,'rail_brighter':rail_gain},'altar':{'base':'#8C8478','luma_delta_percentage_points':delta,'joint_contrast':joint},'candles':{'flames':int(counts[1]),'halos':int(counts[2]),'flame_alpha':color(flame,'_BaseColor')[3],'halo_alpha':color(halo,'_BaseColor')[3]},'yellow':yellow,'interior':interior,'shots':shots,'logs':[x for x in (build+'\n'+entry).splitlines() if any(x.startswith(f'[{t}]') for t in ['PEW_MAT','ALTAR_STONE','CANDLE_GLOW','YELLOW_STRIP','INT_PASS','MATERIALS_PROTECTED'])]}
out=ROOT/'artifacts/interior_materials/measurements.json'
out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False,indent=2))
raise SystemExit(0 if report['accepted'] else 1)
