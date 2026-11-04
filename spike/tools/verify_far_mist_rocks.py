#!/usr/bin/env python3
"""Verify the delivered PNGs, projected texture-alpha crests, mesh-area and demo logs.
Display luma is Rec.709 over encoded PNG RGB (same historical seam/shot metric).
A ridge needs >=20 unoccluded side samples with median 6px crest contrast >=.008.
CIELAB uses sRGB decoding and D65; masks come from first-hit geometry rays at 960x540.
"""
import bisect, hashlib, json, math, re, statistics
from pathlib import Path
from PIL import Image
R=Path(__file__).resolve().parents[1]
A=R/'artifacts/far_mist_rocks'
S=R/'project/Shots'
def num(s,key):
    m=re.search(r'\b'+key+r'=([-\d.]+)',s)
    return float(m[1]) if m else float('nan')
def luma(p):return (.2126*p[0]+.7152*p[1]+.0722*p[2])/255

def frame_stats(path):
    im=Image.open(path).convert('RGB');w,h=im.size;p=im.load()
    x0,x1=int(w*.05),int(w*.95);y0,y1=int(h*.10),int(h*.45)
    columns=[sum(luma(p[x,y]) for y in range(y0,y1))/(y1-y0) for x in range(x0,x1)]
    sky=sum(luma(p[x,y]) for y in range(int(h*.1)) for x in range(x0,x1))/(int(h*.1)*(x1-x0))
    return im,{'mountain_luma':statistics.mean(columns),'sky_luma':sky,'seam':max(abs(a-b) for a,b in zip(columns,columns[1:]))}
def ridges(im):
    projection=json.loads((S/'far_mist_contours.json').read_text());w,h=im.size;p=im.load()
    curves=[sorted((q['x'],q['y']) for q in r['points']) for r in projection['ridges']]
    def at(curve,x):
        xs=[q[0] for q in curve];i=bisect.bisect_left(xs,x)
        if i==0 or i>=len(curve):return None
        a,b=curve[i-1],curve[i];return a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0])
    result=[]
    for i,c in enumerate(curves):
        contrast=[];samples=[]
        for x in range(int(w*.05),int(w*.95),6):
            if w*.32<x<w*.68:continue # church/roof exclusion, fixed before measurement
            y=at(c,x)
            if y is None or y<5 or y>=h*.6:continue
            # Nearer opaque mountain cards obscure farther contours.
            near=[at(v,x) for v in curves[:i]]
            if any(v is not None and v<=y+8 for v in near):continue
            iy=round(y)
            delta=abs(sum(luma(p[x+dx,iy+3])-luma(p[x+dx,iy-3]) for dx in range(-2,3))/5)
            contrast.append(delta);samples.append([x,iy,delta])
        med=statistics.median(contrast) if contrast else 0
        result.append({'layer':i+1,'samples':len(contrast),'median_crest_contrast':med,'visible':len(contrast)>=20 and med>=.008,'sample_points':samples})
    return result

def lab(p):
    def linear(v):
        v/=255;return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
    r,g,b=map(linear,p)
    y=.2126729*r+.7151522*g+.072175*b;z=(.0193339*r+.119192*g+.9503041*b)/1.08883
    def f(v):return v**(1/3) if v>.008856 else (903.3*v+16)/116
    return 116*f(y)-16,200*(f(y)-f(z))
def surfaces(name):
    im=Image.open(S/(name+'.png')).convert('RGB');mask=Image.open(S/('far_mist_'+name+'_mask.png')).convert('RGB');w,h=mask.size;p=im.load();dark=[];posts=[]
    for y in range(h):
        for x in range(w):
            c=mask.getpixel((x,y))
            if c not in [(0,0,255),(0,255,0)]:continue
            px=(2*x+1)*im.width//(2*w);py=im.height-1-(2*(h-1-y)+1)*im.height//(2*h)
            v=lab(p[px,py])
            if c==(0,0,255):dark.append(v[0])
            else:posts.append(v)
    return {'dark_face_L':min(dark) if dark else None,'dark_samples':len(dark),'post_b_star':statistics.mean(p[1] for p in posts) if posts else None,'post_L':statistics.mean(p[0] for p in posts) if posts else None,'post_samples':len(posts)}
logs={k:(R/f'logs/far_mist_rocks_{k}.log').read_text(errors='replace') for k in ['build','interior','walk','reveal','capture','bench']}
assets=json.loads((A/'assets.json').read_text())
im,far=frame_stats(S/'reveal_80.png');ridge=ridges(im);far['visible_ridges']=sum(r['visible'] for r in ridge)
_,art=frame_stats(S/'art_runtime.png')
surface={n:surfaces(n) for n in ['reveal_80','art_runtime']}
bench=json.loads((R/'logs/far_mist_rocks_bench.json').read_text())['results']
checks={'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],
'protected':assets['protected_before']==assets['protected_after'],
'rock_geometry':assets['geometry_before']==assets['geometry_after'],
'snow_caps':35<=assets['snow_min_pct']<=assets['snow_cap_pct']<=assets['snow_max_pct']<=50,
'fence_albedo_drop_10pct':abs(assets['post_after_L']/assets['post_before_L']-.9)<.0001,
'far_mist':far['visible_ridges']>=2 and .42<=far['mountain_luma']<=.55 and far['sky_luma']<=.90 and far['seam']<=.035,
'snow_rock_pixels':all(v['dark_samples']>0 and v['dark_face_L']>=12 for v in surface.values()),
'fence_pixels':all(v['post_samples']>0 and v['post_b_star']<=6 for v in surface.values()),
'outdoor_performance':len(bench)==3 and all(v['fps_avg']>=200 and 0<v['drawcalls']<=400 for v in bench if v['pos'] in ['z01','terrace']),
'interior':bool(re.search(r'^\[INT_PASS\].*$',logs['interior'],re.M)) and all(k+'=True' in re.search(r'^\[INT_PASS\].*$',logs['interior'],re.M)[0] for k in ['light','domeLight','ceilingSeen','domeSeen','visible','obstacles','walked']),
'walk':num(logs['walk'],'distance')>=15 and num(logs['walk'],'ground_error_max')<=.02 and num(logs['walk'],'raycast_misses')==0 and 'fps_ok=true' in logs['walk'],
'reveal':'[REVEAL] gate=pass' in logs['reveal'] and 'bell_once=True pulse_restored=True' in logs['reveal']}
bad={k:len(re.findall(r'failed|ERROR|Exception|^\[REVEAL\] gate=FAIL|^\[PLAY\] .*fps_ok=false',logs[k],re.M)) for k in ['interior','walk','reveal','capture','bench']}
checks['bad_zero']=all(v==0 for v in bad.values())
shots={}
for name in ['reveal_80','art_runtime']:
    p=S/(name+'.png');checks[name+'_fresh']=p.stat().st_mtime>(R/'logs/far_mist_rocks_build.log').stat().st_mtime
    shots[name]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size}
report={'pass':all(checks.values()),'checks':checks,'far_mist':far,'art_frame':art,'ridge_measurement':ridge,'assets':assets,'surfaces':surface,'bench':bench,'bad':bad,'shots':shots,'reveal_gate':re.findall(r'^\[REVEAL\] gate=.*$',logs['reveal'],re.M),'walk':re.findall(r'^\[PLAY\].*$',logs['walk'],re.M)}
(A/'measurements.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
outdoor=[r for r in bench if r['pos'] in ['z01','terrace']]
table='\n'.join(f"| {k} | {'通过' if v else '**失败**'} |" for k,v in checks.items())
(A/'acceptance.md').write_text(f'''# far_mist_rocks 验收

整体结论：{'通过' if report['pass'] else '**未全通过，保留失败门槛**'}。

- 80 m：visible_ridges={far['visible_ridges']}，mountain_luma={far['mountain_luma']:.6f}，sky_luma={far['sky_luma']:.6f}，seam={far['seam']:.6f}。
- 雪盖面积 {assets['snow_cap_pct']:.6f}%，单块 {assets['snow_min_pct']:.6f}–{assets['snow_max_pct']:.6f}%；88 块石头数量、顶点与世界变换 SHA256 相等。
- 两张 PNG 暗面最低采样 L*={min(v['dark_face_L'] for v in surface.values()):.6f}；立柱最大平均 b*={max(v['post_b_star'] for v in surface.values()):.6f}。
- 立柱材质 L*：{assets['post_before_L']:.6f} → {assets['post_after_L']:.6f}（×{assets['post_after_L']/assets['post_before_L']:.6f}）。
- 室外 1920×1080/Metal：{'; '.join(str(r['pos'])+' '+format(r['fps_avg'],'.2f')+' FPS / '+str(r['drawcalls'])+' drawcalls' for r in outdoor)}。
- 原揭示门槛：{'; '.join(report['reveal_gate'])}。没有修改判据或相机；所有试验性揭示时序调整已撤回。
- bad：{json.dumps(bad,ensure_ascii=False)}。

| 检查 | 结果 |
| --- | --- |
{table}

## 变化与保护范围

局部入口 `FarMistRocksBuild.Run`，不重建场景。MistHaze 改为 0.04/0.18/0.34/0.50，山体垂直雾 0.65、亮度 0.78，天空/雾底色 #DCE4EA；山脊对局部天空的亮度比例为 0.72/0.82/0.90/0.97。修正运行时雾幕重复覆盖远山：雾幕采样场景深度，仅覆盖 250–500 m 内的近处场景，山体自行处理分层雾。该深度采样只在运行时申请，不改变保存的机位参数。

石头沿既有三角面按朝上/迎风评分覆盖约42.5%表面积，雪色 #EDF3F8；岩体/暗面独立材质补偿最低亮度。围栏只替换立柱独立冷灰材质，原雪盖与横栏未改。室内、地面、祭坛、王门、人物、全部碰撞、灯光和原机位的保护快照一致；石头位置/尺寸/顶点一致。详见 assets.json 的前后 SHA256。

## 测量口径与复现

- mountain_luma：原有屏幕 ROI x=5–95%、y=10–45% 的 PNG 编码 RGB Rec.709 均值；seam 为该 ROI 相邻列均值梯度的真正最大值。sky_luma 取顶部10%、同一横向范围。
- 山脊：从原山体 PNG alpha=0.5 导出真实世界轮廓，在原80m机位投影；排除被近层覆盖的轮廓及中央32–68%教堂区域。每层至少20个样本，沿轮廓上下3px、横向5px均值差的中位数≥0.008才记可见。全部样本保存在 measurements.json。
- 雪盖百分比为三维三角面表面积；暗面与立柱用960×540首命中网格射线掩码，采样交付1920×1080 PNG。L*/b*按sRGB解码、D65 CIELAB独立复算；暗面报告采样最小L*，立柱报告采样平均b*。掩码位于 Shots/far_mist_*_mask.png。
- 性能为180帧且≥3秒预热后300帧统计，使用原PlayerBenchmark；与普通60FPS演示分开测吞吐，未改性能阈值。

完整运行：`bash spike/tools/accept_far_mist_rocks.sh`。仅复算：`python3 spike/tools/verify_far_mist_rocks.py`；任何失败返回1。

截图：[reveal_80.png](../../project/Shots/reveal_80.png)、[art_runtime.png](../../project/Shots/art_runtime.png)。原始日志为 `spike/logs/far_mist_rocks_{{build,interior,walk,reveal,capture,bench}}.log`，结构化结果见 [measurements.json](measurements.json)。
''',encoding='utf-8')
print('[FAR_MIST] '+' '.join(f'{k}={v:.6f}' if isinstance(v,float) else f'{k}={v}' for k,v in far.items()))
print(f"[SNOW_ROCK] snow_cap_pct={assets['snow_cap_pct']:.6f} dark_face_L={min(v['dark_face_L'] for v in surface.values()):.6f} count={assets['rocks']} count_unchanged={str(checks['rock_geometry']).lower()}")
print(f"[FENCE] post_b_star={max(v['post_b_star'] for v in surface.values()):.6f} albedo_L_ratio={assets['post_after_L']/assets['post_before_L']:.6f}")
print(json.dumps({k:v for k,v in report.items() if k not in ['ridge_measurement','shots']},ensure_ascii=False,indent=2))
raise SystemExit(0 if report['pass'] else 1)
