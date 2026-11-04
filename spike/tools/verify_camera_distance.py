#!/usr/bin/env python3
import hashlib,json,re,struct
from pathlib import Path
R=Path(__file__).resolve().parents[1]
logs={k:(R/f'logs/camera_distance_{k}.log').read_text(errors='replace') for k in ['build','capture','walk','interior','reveal','bench']}
def lines(s,tag): return re.findall(r'^\['+tag+r'\].*$',s,re.M)
def num(s,key): return float(re.search(r'\b'+re.escape(key)+r'=([-\d.]+)',s)[1])
checks={'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build']}
frames=lines(logs['capture'],'CAM_FRAME')
checks['framing']=len(frames)>=2 and all(140<=num(s,'pilgrim_px')<=165 and num(s,'actual_h')==1080 and 'contained=True' in s for s in frames)
sizes=sum((lines(logs[k],'PILGRIM_SIZE') for k in ['capture','walk','interior','reveal']),[])
checks['size']=bool(sizes) and all(1.48<=num(s,'alpha_h_min')<=num(s,'alpha_h_max')<=1.52001 and 'aspect_preserved=true collider=1.5' in s for s in sizes)
checks['church_upper']='complete_upper=True' in logs['capture']
cam=lines(logs['capture'],'CAM_TEST')
checks['camera_clearance']=len(cam)==2 and all(num(s,'bad')==0 and num(s,'seconds')>=6 and num(s,'ground_gap')>=.05 and num(s,'wall_gap')>=.149 for s in cam)
clear=lines(logs['build'],'CAM_CLEARANCE')
checks['doorway_clearance']=bool(clear) and num(clear[-1],'doorway_head_gap')>=.03
bad={}
for k in ['walk','interior','reveal']:
 bad[k]=len(re.findall(r'failed|\w*Exception\b|\bERROR\b|gate=FAIL|fps_ok=false',logs[k],re.I))
 checks[k+'_no_errors']=bad[k]==0
checks['walk']=num(logs['walk'],'distance')>=15 and num(logs['walk'],'ground_error_max')<=.02 and num(logs['walk'],'raycast_misses')==0 and 'fps_ok=true' in logs['walk']
ints=lines(logs['interior'],'INT_PASS')
checks['interior']=bool(ints) and all(k+'=True' in ints[-1] for k in ['light','domeLight','ceilingSeen','domeSeen','visible','obstacles','walked'])
checks['reveal']='[REVEAL] gate=pass' in logs['reveal'] and 'bell_once=True pulse_restored=True' in logs['reveal']
bench=json.loads((R/'logs/camera_distance_bench.json').read_text())
checks['outdoor_fps']=all(s['fps_avg']>=200 for s in bench['results'] if s['pos'] in ['z01','terrace'])
shots={}
for n in ['cam_play_1080','cam_walk_1080']:
 p=R/f'project/Shots/{n}.png'; data=p.read_bytes();wh=struct.unpack('>II',data[16:24])
 checks[n]=wh==(1920,1080) and p.stat().st_mtime>(R/'logs/camera_distance_build.log').stat().st_mtime
 shots[n]={'path':str(p),'size':wh,'sha256':hashlib.sha256(data).hexdigest()}
checks['reveal_unchanged']=hashlib.sha256((R/'project/Assets/Scripts/MistReveal.cs').read_bytes()).hexdigest()==json.loads((R/'artifacts/camera_distance/protected.json').read_text())['MistReveal.cs']
report={'pass':all(checks.values()),'checks':checks,'bad':bad,'framing':frames,'size':sizes,'clearance':clear,'camera_test':cam,'walk':lines(logs['walk'],'PLAY'),'interior':ints,'reveal':lines(logs['reveal'],'REVEAL'),'bench':bench,'benchmark_terminal':lines(logs['bench'],'BENCH_GATE'),'shots':shots}
(R/'artifacts/camera_distance/measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False,indent=2))
for k,v in bad.items(): print(f'demo_{k} bad={v}')
raise SystemExit(0 if report['pass'] else 1)
