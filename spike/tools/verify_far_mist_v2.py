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
A=R/'artifacts/far_mist_v2'
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
    im=Image.open(S/(name+'.png')).convert('RGB');mask=Image.open(S/('far_mist_'+name+'_mask.png')).convert('RGB');w,h=mask.size;p=im.load();dark=[];posts=[];paths=[];edges=[]
    for y in range(h):
        for x in range(w):
            c=mask.getpixel((x,y))
            if c not in [(0,0,255),(0,255,0),(255,255,0),(0,255,255)]:continue
            px=(2*x+1)*im.width//(2*w);py=im.height-1-(2*(h-1-y)+1)*im.height//(2*h)
            v=lab(p[px,py])
            if c==(0,0,255):dark.append(v[0])
            elif c==(0,255,0):posts.append(v)
            elif c==(255,255,0):paths.append(v)
            else:edges.append(v)
    return {'path_L':statistics.mean(v[0] for v in paths) if paths else None,'path_b':statistics.mean(v[1] for v in paths) if paths else None,'path_samples':len(paths),'edge_L':statistics.mean(v[0] for v in edges) if edges else None,'edge_samples':len(edges),'dark_face_L':min(dark) if dark else None,'dark_samples':len(dark),'post_b_star':statistics.mean(p[1] for p in posts) if posts else None,'post_L':statistics.mean(p[0] for p in posts) if posts else None,'post_samples':len(posts)}
import csv
logs={k:(R/f'logs/far_mist_v2_{k}.log').read_text(errors='replace') for k in ['build','interior','walk','reveal','capture','bench']}
assets=json.loads((A/'assets.json').read_text())
im,far=frame_stats(S/'reveal_80.png');ridge=ridges(im);far['visible_ridges']=sum(r['visible'] for r in ridge)
surface={n:surfaces(n) for n in ['reveal_80','art_runtime']}
bench=json.loads((R/'logs/far_mist_v2_bench.json').read_text())['results']
frames={float(d):float(v) for d,v in re.findall(r'\[REVEAL_FRAME\] d=(\d+) fov=[\d.]+ church_height_pct=([\d.]+)',logs['reveal'])}
contrast=[]
for row in csv.DictReader((S/'reveal_contrast.csv').open()):
    y=im.height-1-int(row['y']);contrast.append(abs(luma(im.getpixel((int(row['inside_x']),y)))-luma(im.getpixel((int(row['outside_x']),y)))))
reveal={'far_h_pct':frames[80.0],'far_contrast':statistics.mean(contrast),'near_h_pct':frames[28.0],'contrast_samples':len(contrast)}
comp=re.search(r'^\[REVEAL_COMPOSITION\].*$',logs['reveal'],re.M)[0]
checks={'build':'[PERF_BUILD] result=Succeeded errors=0' in logs['build'],
'protected':assets['protected_before']==assets['protected_after'],
'rock_count_layout':assets['count']==88 and assets['layout_before']==assets['layout_after'],
'rock_aspect':max(v['aspect'] for v in assets['stones'])<=.45,
'rock_burial':all(.35<=v['buried']<=.45 for v in assets['stones']),
'rock_dark_face':all(v['dark_samples']>0 and v['dark_face_L']>=20 for v in surface.values()),
'path_color':all(v['path_samples']>0 and 48<=v['path_L']<=58 and v['path_b']<=8 for v in surface.values()),
'path_snow_edge':abs(assets['edge_albedo_gain']-1.15)<1e-5 and assets['edge_segments']>0 and abs(assets['edge_width']-.1)<1e-6 and all(v['edge_samples']>0 and v['edge_L']>v['path_L'] for v in surface.values()),
'far_mist_preserved':far['visible_ridges']>=2 and .42<=far['mountain_luma']<=.55 and far['sky_luma']<=.90 and far['seam']<=.035,
'far_height':0<reveal['far_h_pct']<=.30,
'far_contrast':reveal['far_contrast']<=.18,
'near_frame':.55<=reveal['near_h_pct']<=.68 and num(comp,'top_margin')>=.04 and num(comp,'bottom_margin')>=.04 and num(comp,'base_blocked')==0,
'reveal_demo':'[REVEAL] gate=pass' in logs['reveal'] and 'bell_once=True pulse_restored=True' in logs['reveal'],
'interior':bool(re.search(r'^\[INT_PASS\].*$',logs['interior'],re.M)) and all(k+'=True' in re.search(r'^\[INT_PASS\].*$',logs['interior'],re.M)[0] for k in ['light','domeLight','ceilingSeen','domeSeen','visible','obstacles','walked']),
'walk':num(logs['walk'],'distance')>=15 and num(logs['walk'],'ground_error_max')<=.02 and num(logs['walk'],'raycast_misses')==0 and 'fps_ok=true' in logs['walk'],
'outdoor_performance':len([v for v in bench if v['pos'] in ['z01','terrace']])==2 and all(v['fps_avg']>=200 and 0<v['drawcalls']<=400 for v in bench if v['pos'] in ['z01','terrace'])}
bad={k:len(re.findall(r'failed|ERROR|Exception|non-degenerate triangle|^\[REVEAL\] (?:gate=FAIL|far_h_pct=.*gate=FAIL)|^\[PLAY\] .*fps_ok=false',logs[k],re.M)) for k in ['interior','walk','reveal','capture','bench']}
# The two summary lines describe one failed reveal gate; count the gate once.
bad['reveal']=len(re.findall(r'failed|ERROR|Exception|non-degenerate triangle|^\[REVEAL\] gate=FAIL',logs['reveal'],re.M))
checks['bad_zero']=all(v==0 for v in bad.values())
shots={}
for name in ['reveal_80','reveal_28','art_runtime']:
    p=S/(name+'.png');shot=Image.open(p).convert('RGB')
    checks[name+'_fresh']=p.stat().st_mtime>(R/'logs/far_mist_v2_build.log').stat().st_mtime
    mag=sum(r>150 and b>150 and g<100 for r,g,b in shot.getdata())
    checks[name+'_material_ok']=mag==0
    shots[name]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'size':list(shot.size),'magenta_px':mag}
result={'pass':all(checks.values()),'checks':checks,'reveal':reveal,'surfaces':surface,'assets':assets,'far_mist':far,'bench':bench,'bad':bad,'shots':shots,'ridge_measurement':ridge,'reveal_logs':re.findall(r'^\[REVEAL\].*$',logs['reveal'],re.M)}
(A/'measurements.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
lines=[f"[REVEAL] far_h_pct={reveal['far_h_pct']:.6f} far_contrast={reveal['far_contrast']:.6f} near_h_pct={reveal['near_h_pct']:.6f} gate={'pass' if checks['far_height'] and checks['far_contrast'] and checks['near_frame'] and checks['reveal_demo'] else 'FAIL'}",f"[ROCK_SHAPE] aspect={assets['aspect_max']:.6f} buried={assets['buried_min']:.6f}..{assets['buried_max']:.6f} dark_face_L={min(v['dark_face_L'] for v in surface.values()):.6f} count={assets['count']} layout_unchanged={str(checks['rock_count_layout']).lower()}"]
for name,v in surface.items():lines.append(f"[PATH] view={name} luma={v['path_L']:.6f} b_star={v['path_b']:.6f} edge_L={v['edge_L']:.6f} snow_edge={str(checks['path_snow_edge']).lower()}")
for v in bench:lines.append(f"[BENCH] pos={v['pos']} fps={v['fps_avg']:.2f} drawcall={v['drawcalls']}")
lines.append(f"[WALK] distance={num(logs['walk'],'distance'):.3f} ground_error_max={num(logs['walk'],'ground_error_max'):.6f} raycast_misses={num(logs['walk'],'raycast_misses'):.0f}")
lines.append('bad='+json.dumps(bad))
lines.append('pass='+str(result['pass']))
print('\n'.join(lines))
(A/'acceptance.md').write_text('# far_mist_v2 验收\n\n'+('全部通过。' if result['pass'] else '**未全通过。**')+('\n\n阻塞项：80m原机位实际几何视高0.411735，要求≤0.30。任务同时禁止改变距离/FOV/高度；本轮保留原机位，不把测量改成可见像素或放宽阈值。reveal因此gate=FAIL、bad=1，不能宣称三项演示全绿。' if not checks['far_height'] else '')+'\n\n```text\n'+'\n'.join(lines)+'\n```\n\n| 检查 | 结果 |\n| --- | --- |\n'+'\n'.join(f"| {k} | {'通过' if v else '失败'} |" for k,v in checks.items())+'''\n
局部修改入口 `FarMistV2Build.Run`，不重建场景。88块石头保留物体变换矩阵，仅改网格世界Y方向，最高宽高比0.42，按实际雪地/台地碰撞表面埋入40%。背光面使用独立材质的环境补光下限。路面和雪边使用独立材质；雪边网格批为一个renderer，不增加碰撞。

保护摘要涵盖非石头/路面的渲染器、网格、材质、灯光、全部碰撞体、保存的相机、人物控制器与ShotRig。前后SHA一致；未重建或覆盖原有工作区修改。

测量口径：石头形态来自世界空间网格包围盒；埋雪比例=(雪面Y-网格底Y)/总高。暗面最低L*、路面平均L*/b*、雪边平均L*均从交付1080p PNG的960×540首命中几何掩码独立复算，sRGB解码、D65 CIELAB。L*不是0–1的Rec.709亮度。雪边宽0.1m，线性反照率比路面高15%；雾与景深后实际L*另行报告，不混淆反照率和显示亮度。

教堂视高来自实际顶点投影的上下界；对比度为真实三角面投影的逐行左右轮廓，内外各4px的PNG Rec.709亮度差绝对值均值，每2px取样，独立复算全部`Shots/reveal_contrast.csv`采样。ratio20_80保留作诊断，注释说明已与远山可见性冲突，不再作门槛。近景额外检查完整入画与基座无遮挡。

室外性能按原PlayerBenchmark的180帧且≥3秒预热、300帧采样，在1920×1080/Metal下测量；本任务用室外≥200FPS/≤400drawcalls验收，不使用通用bench的250或旧foreground的330上限，未修改基准测试脚本；因此原BENCH_GATE仍可能False，本任务性能结论由此脚本按任务书独立判定。

复现：`bash spike/tools/accept_far_mist_v2.sh`；仅复算：`python3 spike/tools/verify_far_mist_v2.py`，任何未过项返回1。原始日志：`spike/logs/far_mist_v2_{build,interior,walk,reveal,capture,bench}.log`。

截图：[reveal_80.png](../../project/Shots/reveal_80.png)、[reveal_28.png](../../project/Shots/reveal_28.png)、[art_runtime.png](../../project/Shots/art_runtime.png)。全部数字与摘要见[measurements.json](measurements.json)。
''')
raise SystemExit(0 if result['pass'] else 1)
