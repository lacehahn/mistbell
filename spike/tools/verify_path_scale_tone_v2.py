#!/usr/bin/env python3
"""Audit the actual scene and fresh runs; fail if the required baseline is absent."""
import hashlib,json,re,shutil
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1];A=R/'artifacts/path_scale_tone_v2'
scene=(R/'project/Assets/Scenes/ArtSpike.unity').read_text()
material=R/'project/Assets/Settings/M_FT3_Paving.mat'
guid=re.search(r'^guid: (\w+)',Path(str(material)+'.meta').read_text(),re.M)[1]
refs=scene.count(guid);surface=scene.count('m_Name: Paving_Surface\n')
logs={k:(R/f'logs/path_scale_tone_v2_{k}.log').read_text(errors='replace') for k in ['build','camera','closeup','walk','interior','reveal','bench']}
bad={k:len(re.findall(r'failed|\bERROR\b|\w*Exception\b|gate=FAIL|fps_ok=false|Shader error|\bbad=[1-9]',v,re.I)) for k,v in logs.items() if k!='build'}
bench=json.loads((R/'logs/path_scale_tone_v2_bench.json').read_text())['results']
before=json.loads((A/'before_hashes.json').read_text());changed=[]
# Recorded paths are relative to the repository root.
for p,h in before.items():
 f=R.parent/p
 if not f.exists() or hashlib.sha256(f.read_bytes()).hexdigest()!=h:changed.append(p)
shots={}
for n in ['cam_play_1080','path_closeup']:
 p=R/'project/Shots'/f'{n}.png';im=Image.open(p)
 shots[n]={'size':list(im.size),'fresh':p.stat().st_mtime>(R/'logs/path_scale_tone_v2_build.log').stat().st_mtime}
 shutil.copy2(p,A/p.name)
checks={'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],'required_baseline':refs>0 and surface==1,'assets_unchanged_after_rollback':not changed,'camera':'dist=11.50 h=2.10 look=1.20 fov=48.00' in logs['camera'],'demos':all(bad[k]==0 for k in ['walk','interior','reveal']) and '[REVEAL] gate=pass' in logs['reveal'] and '[INT_PASS]' in logs['interior'] and 'fps_ok=true' in logs['walk'],'outdoor_fps':all(v['fps_avg']>=200 for v in bench if v['pos'] in ['z01','terrace']),'terrace_drawcall':any(v['pos']=='terrace' and 0<v['drawcalls']<=330 for v in bench),'shots':all(v['fresh'] and v['size']==[1920,1080] for v in shots.values()),'path_luma':False,'path_repeat':False,'visual_slab_readability':False}
report={'pass':all(checks.values()),'checks':checks,'material_scene_references':refs,'paving_surface_count':surface,'old_paving_count':len(re.findall(r'm_Name: Paving_\d+\n',scene)),'changed_assets':changed,'bad':bad,'bench':bench,'shots':shots,'path_luma':None,'repeat_score':None,'reason':'Required PathTone baseline absent. Parameter-only edit has no rendered effect; reverted. CPU texture tests are diagnostics, not actual-scene acceptance.'}
(A/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(f'[BASELINE] PathTone_material_refs={refs} Paving_Surface={surface} old_paving={report["old_paving_count"]}')
print('[PATH_TEX2] scale_m=N/A luma=N/A repeat_score=N/A (required renderer absent)')
for k in ['walk','interior','reveal']:print(f'[DEMO] {k} bad={bad[k]}')
for b in bench:print(f'[BENCH] {b["pos"]} fps={b["fps_avg"]:.2f} p1={b["fps_p1"]:.2f} drawcall={b["drawcalls"]}')
for k,v in checks.items():print(f'[CHECK] {k}={str(v).lower()}')
print('pass='+str(report['pass']).lower());raise SystemExit(0 if report['pass'] else 1)
