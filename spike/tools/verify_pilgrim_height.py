#!/usr/bin/env python3
"""Verify actual task output, rejecting false-green demo summaries."""
import hashlib, json, re
from pathlib import Path
R=Path(__file__).resolve().parents[1]
logs={k:(R/f'logs/pilgrim_height_{k}.log').read_text(errors='replace') for k in ['build','interior','walk','reveal']}
def number(s,key):
    return float(re.search(r'\b'+key+r'=([-\d.]+)',s)[1])
checks={'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build']}
sizes=[]
for kind in ['interior','walk','reveal']:
    s=logs[kind]
    entries=re.findall(r'^\[PILGRIM_SIZE\].*$',s,re.M)
    checks[kind+'_size']=bool(entries) and all(abs(number(e,'world_h')-1.7)<=.03 and number(e,'alpha_h_min')>=1.67 and number(e,'alpha_h_max')<=1.73 and 'aspect_preserved=true collider=1.7' in e for e in entries)
    sizes+=entries
    height=number(s,'pilgrim_px_900')
    checks[kind+'_framing']=341.29*.9<=height<=341.29*1.1
    checks[kind+'_no_errors']=not re.search(r'failed|Exception|ERROR|gate=FAIL',s)
checks['interior']='[INT_PASS]' in logs['interior'] and all(k+'=True' in re.search(r'^\[INT_PASS\].*$',logs['interior'],re.M)[0] for k in ['light','domeLight','ceilingSeen','domeSeen','visible','obstacles','walked'])
checks['walk']=number(logs['walk'],'distance')>=15 and number(logs['walk'],'ground_error_max')<=.02 and number(logs['walk'],'raycast_misses')==0 and 'fps_ok=true' in logs['walk']
checks['reveal']='[REVEAL] gate=pass' in logs['reveal'] and 'bell_once=True pulse_restored=True' in logs['reveal']
shots={}
for n in ['interior_inside','pilgrim_scale']:
    p=R/f'project/Shots/{n}.png'
    # Require a new screenshot from the rebuilt player. Logs keep growing after capture.
    checks[n+'_fresh']=p.exists() and p.stat().st_mtime>(R/'logs/pilgrim_height_build.log').stat().st_mtime
    shots[n]={'path':str(p),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
report={'pass':all(checks.values()),'checks':checks,'size_logs':sizes,'clearance':re.search(r'^\[PILGRIM_CLEARANCE\].*$',logs['build'],re.M)[0], 'walk':re.findall(r'^\[PLAY\].*$',logs['walk'],re.M),'reveal':re.findall(r'^\[REVEAL\] (?:d=|gate=).*$',logs['reveal'],re.M),'shots':shots}
baseline=R/'logs/pilgrim_height_baseline_reveal.log'
if baseline.exists():report['baseline_reveal']=re.search(r'^\[REVEAL\] gate=.*$',baseline.read_text(),re.M)[0]
(R/'artifacts/pilgrim_height/measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False,indent=2))
raise SystemExit(0 if report['pass'] else 1)
