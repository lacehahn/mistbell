#!/usr/bin/env python3
"""Task-specific acceptance; screenshot score is measured, never an authored constant.

Grid score: resize actual frames to 480x270, fixed snow-only ROIs, subtract a
Gaussian radius=3 local mean. For each axis measure normalized autocorrelation
at lags 1..36px. Score=max positive peak prominence at lags 4..32px relative
to the four neighboring lags +/-1,+/-2. Max over both axes and both views.
This tests repeated screen-space edges after removing broad lighting; it is
not a physical metre estimate or a universal perceptual score. ROIs are fixed
before comparing, exclude road/architecture/player, and are saved for review.
"""
import hashlib,json,math,re
from pathlib import Path
from PIL import Image,ImageFilter,ImageDraw
R=Path(__file__).resolve().parents[1]
A=R/'artifacts/snow_grass_fence'
S=R/'project/Shots'
logs={k:(R/f'logs/snow_grass_fence_{k}.log').read_text(errors='replace') for k in ['build','camera','walk','interior','reveal','capture','bench']}
assets=json.loads((A/'assets.json').read_text())
initial=(A/'initial_build.log').read_text(errors='replace') if (A/'initial_build.log').exists() else logs['build']
bench=json.loads((R/'logs/snow_grass_fence_bench.json').read_text())['results']
def lines(k,tag):return re.findall(r'^\['+tag+r'\].*$',logs[k],re.M)
def num(s,k):return float(re.search(r'\b'+re.escape(k)+r'=([-\d.]+)',s)[1])
def grid(path,roi,save=None):
    im=Image.open(path).convert('RGB').resize((480,270),Image.Resampling.LANCZOS)
    if save:
        im.save(save)
        marked=im.copy();ImageDraw.Draw(marked).rectangle(roi,outline='red',width=1)
        marked.save(save.with_name(save.stem+'_roi.png'))
    crop=im.convert('L').crop(roi);w,h=crop.size
    pix=list(crop.getdata());blur=list(crop.filter(ImageFilter.GaussianBlur(3)).getdata())
    a=[x-y for x,y in zip(pix,blur)]
    def corr(lag,axis):
        xy=xx=yy=0.
        for y in range(h-(lag if axis else 0)):
            for x in range(w-(0 if axis else lag)):
                v=a[y*w+x];z=a[(y+(lag if axis else 0))*w+x+(0 if axis else lag)]
                xy+=v*z;xx+=v*v;yy+=z*z
        return xy/max(math.sqrt(xx*yy),1e-12)
    axis_scores=[]
    for axis in range(2):
        c={lag:corr(lag,axis) for lag in range(1,37)}
        peaks={k:max(0,c[k]-sum(c[k+d] for d in [-2,-1,1,2])/4) for k in range(4,33)}
        lag=max(peaks,key=peaks.get)
        axis_scores.append({'score':peaks[lag],'lag_px':lag,'correlation':c})
    return {'grid_score':max(v['score'] for v in axis_scores),'roi_480':roi,'axes':axis_scores}
shots={};grids={}
checks={'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],
        'protected':assets['protected_before']==assets['protected_after'],
        'grass_probe':'[GRASS_PROBE]' in initial,
        'grass':assets['grass_after']==0 and '[GRASS] count=0' in logs['build'],
        'fence':assets['post_color']=='#4A3B2E' and assets['snow_color']=='#EDF3F8' and assets['shadow_coverage']<=.65 and assets['snow_caps']==assets['posts'] and assets['rails']==2*(assets['posts']-2) and 2.199<=assets['spacing_min']<=assets['spacing_max']<=2.201 and all(abs(assets[k]-v)<(.001 if k=='post_spacing' else 1e-5) for k,v in [('post_width',.1),('post_height',1),('post_spacing',2.2),('rail_width',.06),('rail_height',.08),('rail_gap',.45),('snow_depth',.04)]),
        'walk':bool(lines('walk','PLAY')) and 'fps_ok=true' in logs['walk'] and num(logs['walk'],'distance')>=15 and num(logs['walk'],'ground_error_max')<=.02 and num(logs['walk'],'raycast_misses')==0,
        'interior':bool(lines('interior','INT_PASS')) and all(k+'=True' in lines('interior','INT_PASS')[-1] for k in ['light','domeLight','ceilingSeen','domeSeen','visible','obstacles','walked']),
        'reveal':'[REVEAL] gate=pass' in logs['reveal'] and 'bell_once=True pulse_restored=True' in logs['reveal'],
        'camera':len(lines('camera','CAM_TEST'))==2 and all(num(s,'bad')==0 for s in lines('camera','CAM_TEST')) and all(num(s,'dist')==11.5 and num(s,'h')==2.1 and num(s,'look')==1.2 and num(s,'fov')==48 for s in lines('camera','CAM_FRAME')),
        'outdoor_fps':len([v for v in bench if v['pos'] in ['z01','terrace']])==2 and all(v['fps_avg']>=200 for v in bench if v['pos'] in ['z01','terrace']),
        'terrace_drawcalls':len([v for v in bench if v['pos']=='terrace'])==1 and next(v['drawcalls'] for v in bench if v['pos']=='terrace')<=330}
bad={k:len(re.findall(r'failed|\bERROR\b|\w*Exception\b|gate=FAIL|fps_ok=false|Shader error|non-degenerate triangle',logs[k],re.I)) for k in ['camera','walk','interior','reveal','capture','bench']}
checks['bad_zero']=all(v==0 for v in bad.values())
for name,roi in [('cam_play_1080',(5,108,93,245)),('art_runtime',(260,211,340,263))]:
    p=S/(name+'.png');im=Image.open(p).convert('RGB')
    magenta=sum(r>150 and b>150 and g<100 for r,g,b in im.getdata())
    checks[name]=im.size==(1920,1080) and magenta==0 and p.stat().st_mtime>(R/'logs/snow_grass_fence_build.log').stat().st_mtime
    shots[name]={'size':im.size,'magenta_px':magenta,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
    grids[name]=grid(p,roi,A/(name+'_480.png'))
    before=A/'before'/(name+'.png')
    if before.exists():grids[name]['before']=grid(before,roi)
score=max(v['grid_score'] for v in grids.values());checks['snow_tile']=score<=.12
report={'pass':all(checks.values()),'checks':checks,'bad':bad,'assets':assets,'bench':bench,'shots':shots,'snow_tile':grids,'grid_score':score,
        'camera':lines('camera','CAM_FRAME')+lines('camera','CAM_TEST'),'walk':lines('walk','PLAY'),'interior':lines('interior','INT_PASS'),'reveal':lines('reveal','REVEAL')}
(A/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(re.findall(r'^\[GRASS_PROBE\].*$',initial,re.M)[0])
print(f"[GRASS] count={assets['grass_after']}")
print(f"[FENCE2] post_color={assets['post_color']} rail=2 snow_cap=true shadow_strength={assets['shadow_coverage']:.3f} shadow_method=bayer_coverage posts={assets['posts']} rail_total={assets['rails']}")
for name,v in grids.items():print(f"[SNOW_TILE] view={name} grid_score={v['grid_score']:.6f} before={v.get('before',{}).get('grid_score',-1):.6f} resolution=480x270 roi={v['roi_480']} method=autocorrelation_peak_prominence")
for b in bench:print(f"[BENCH] pos={b['pos']} fps_avg={b['fps_avg']:.2f} fps_p1={b['fps_p1']:.2f} drawcall={b['drawcalls']}")
for k,v in bad.items():print(f'demo_{k} bad={v}')
for k,v in checks.items():print(f'[CHECK] {k}={str(v).lower()}')
print('pass='+str(report['pass']).lower())
raise SystemExit(0 if report['pass'] else 1)
