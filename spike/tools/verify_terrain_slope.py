#!/usr/bin/env python3
"""Run only terrain_slope.md acceptance against the incrementally built player."""
import json,re,subprocess,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
PROJECT=ROOT/'spike/project'
OUT=ROOT/'spike/artifacts/terrain_slope'
APP=PROJECT/'Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell'

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    build=(ROOT/'spike/logs/terrain_slope_build.log').read_text()
    lines=[s for s in build.splitlines() if s.startswith(('[SLOPE]','[SLOPE_BUILD]'))]
    for s in lines:print(s,flush=True)
    lateral=[float(v) for v in re.findall(r'rise_lateral10m=([\d.-]+)',build)]
    field=[float(v) for v in re.findall(r'field_rise=([\d.-]+)',build)]
    result={'lateral10m':lateral,'field_rise':field,'build': '[SLOPE_BUILD] result=Succeeded errors=0' in build,
            'slope':len(lateral)==4 and all(.8<=v<=1.5 for v in lateral) and len(field)==2 and min(field)>=18,
            'protection':'protected_vertices_changed=0 protected_unchanged=True' in build}
    for name,args in [('walk',['-demo-walk','6']),('enter',['-demo-enter-church']),('reveal',['-demo-reveal']),('steps',['-demo-climb-feel']),('slope_capture',['-demo-terrain-slope']),('bench',['-bench','-bench-foreground','-bench-output',str(OUT/'bench.json')])]:
        log=OUT/(name+'.log')
        with (OUT/(name+'.stdout')).open('w') as stream:
            try:rc=subprocess.run([str(APP),'-force-gfx-direct','-perf-unpaced',*args,'-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-logFile',str(log)],cwd=PROJECT,stdout=stream,stderr=stream,timeout=180).returncode
            except subprocess.TimeoutExpired:rc=124
        body=log.read_text() if log.exists() else ''
        bad=len(re.findall(r'failed|\bERROR\b|\w*Exception\b|gate=FAIL|fps_ok=false|Shader error|\bbad=[1-9]',body,re.I))
        result[name]={'rc':rc,'bad':bad}
        if name=='walk':
            match=re.search(r'\[PLAY\].*distance=([\d.]+)',body)
            result[name]['distance']=float(match[1]) if match else 0
            result[name]['walk_ok']=bool(re.search(r'\[CLIMB_WALK6\] samples=\d+ bad=0\b',body)) and 16.8<=result[name]['distance']<=18.6
        for s in body.splitlines():
            if s.startswith(('[PLAY]','[CLIMB_WALK','[REVEAL] gate','[SLOPE_CAPTURE]')):print(s,flush=True)
        print(f'[EXIT] {name}={rc} bad={bad}',flush=True)
    bench=json.loads((OUT/'bench.json').read_text())['results']
    result['outdoor_fps']={v['pos']:v['fps_avg'] for v in bench if v['pos'] in ('z01','terrace')}
    for name,fps in result['outdoor_fps'].items():print(f'[BENCH] {name} fps={fps:.2f}',flush=True)
    result['shots']={}
    for name in ('climb_slope.png','play_view.png','climb_steps.png'):
        path=PROJECT/'Shots'/name
        result['shots'][name]=path.exists() and path.stat().st_size>10000
        if path.exists():shutil.copy2(path,OUT/name)
    result['pass']=all(result[k] for k in ('build','slope','protection')) and all(result[k]['rc']==0 and result[k]['bad']==0 for k in ('walk','enter','reveal','steps','slope_capture','bench')) and result['walk']['walk_ok'] and len(result['outdoor_fps'])==2 and min(result['outdoor_fps'].values())>=200 and all(result['shots'].values())
    (OUT/'acceptance.json').write_text(json.dumps(result,indent=2)+'\n')
    print('[SLOPE_ACCEPTANCE] '+json.dumps(result),flush=True)
    return 0 if result['pass'] else 1
if __name__=='__main__':raise SystemExit(main())
