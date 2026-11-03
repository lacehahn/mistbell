#!/usr/bin/env python3
"""STYLE5 independent raster/serialized material validation (stdlib, no new packages).

HF = mean(abs(RGB-Gaussian(RGB,sigma=1,radius=3)))/255 with periodic edges;
spread = (max-min)/min across ALL FOUR materials, including Stone_Sub.
Block dimensions/tiers come from the deterministic generation manifest but are
cross-checked against every raster pixel (mortar, bevel, shadow, body and jog).
Chroma and palette use ALL pixels; luma_steps counts the four BODY tiers only,
excluding separately identified mortar/bevel/shadows, as required by the task.
Family delta-E retains the pre-existing fixed same-wall runtime AB ROI/CIE76.
"""
import hashlib
import json
import math
import re
import pathlib
import struct
import subprocess
import sys
import zlib

REPO = pathlib.Path(__file__).resolve().parents[2]
PROJECT = REPO / 'spike/project'
ROOT = PROJECT / 'Assets/Art/MaterialForge'


def load(path):
    data = pathlib.Path(path).read_bytes()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError('PNG required')
    pos, compressed = 8, bytearray()
    while pos < len(data):
        size = struct.unpack('>I', data[pos:pos+4])[0]
        tag, chunk = data[pos+4:pos+8], data[pos+8:pos+8+size]
        if tag == b'IHDR':
            w, h, depth, mode, _, _, interlace = struct.unpack('>IIBBBBB', chunk)
        elif tag == b'IDAT':
            compressed.extend(chunk)
        pos += size+12
    if depth != 8 or mode not in (2, 6) or interlace:
        raise ValueError('Need non-interlaced 8-bit RGB/RGBA PNG')
    bpp = 4 if mode == 6 else 3
    stride, stream, previous, result = w*bpp, zlib.decompress(compressed), bytearray(w*bpp), bytearray()
    for y in range(h):
        start = y*(stride+1)
        filt, row = stream[start], bytearray(stream[start+1:start+1+stride])
        for i in range(stride):
            a, b, c = row[i-bpp] if i >= bpp else 0, previous[i], previous[i-bpp] if i >= bpp else 0
            if filt == 1: pred = a
            elif filt == 2: pred = b
            elif filt == 3: pred = (a+b)//2
            elif filt == 4:
                p = a+b-c
                pa, pb, pc = abs(p-a), abs(p-b), abs(p-c)
                pred = a if pa <= pb and pa <= pc else b if pb <= pc else c
            elif filt == 0: pred = 0
            else: raise ValueError('Invalid PNG filter')
            row[i] = (row[i]+pred) & 255
        for x in range(w): result.extend(row[x*bpp:x*bpp+3])
        previous = row
    return result, w, h


# Immutable v4 encoded files; Snow/Wood retain their pre-v4 baselines.
PRESERVED = {
    'Stone_Sub_Albedo.png':'92c64dad301c95d40fe6f0d9540f6ae0ab5536a87dba803fba20826d83e3ef4e',
    'Stone_Sub_Roughness.png':'ea885450d25823dd5e3c858e79bf519c663749caa00554dee27013480ec7bfca',
    'Snow_Albedo.png':'8a13e96e9daef38854d7d92157246fd3d1d38412de97338bc5f967a2deeb3375',
    'Snow_Roughness.png':'ce3920767eb424e83eecf99ee267039cd24eaf0973b6266ae3f6c3ecdfd970e0',
    'Wood_Albedo.png':'9059633c8b3f92dc3d0a941f717facf05150b0d3e1a1e115800ffdcd9116de2f',
    'Wood_Roughness.png':'ff20e80b124191e5306cb35918965abebb0f78059d66192a7020083314f6d259',
}
RAMPS={'Stone':['6C6C6C','787878','848484','909090'],
       'Stone_Sub':['232323','363636','484848','565656']}


def rgb_hex(value, gain=1):
    return tuple(round(int(value[i:i+2],16)*gain) for i in (0,2,4))


def gaussian_rgb(buf,w,h):
    """mean(|RGB - GaussianBlur(RGB,sigma=1,radius=3)|)/255, periodic edges.

    All three channels have equal weight. Also report the old Rec.601-luminance
    residual for comparison, but never mix the two definitions in the spread.
    """
    weights=[math.exp(-i*i/2) for i in range(-3,4)]
    weights=[v/sum(weights) for v in weights]
    total=0
    for c in range(3):
        channel=buf[c::3]
        temp=[sum(channel[y*w+(x+d)%w]*weights[d+3] for d in range(-3,4)) for y in range(h) for x in range(w)]
        blurred=[sum(temp[((y+d)%h)*w+x]*weights[d+3] for d in range(-3,4)) for y in range(h) for x in range(w)]
        total+=sum(abs(a-b) for a,b in zip(channel,blurred))
    return total/(w*h*3*255)


def hash32(a,b):
    n=(a*73856093 ^ b*19349663 ^ 731)&0xffffffff
    n^=n>>13
    return (n*1274126177 & 0xffffffff)&0x7fffffff


def cv(values):
    mean=sum(values)/len(values)
    return math.sqrt(sum((v-mean)**2 for v in values)/len(values))/mean


def analyze(entry,manifest):
    name=entry['name'];buf,w,h=load(PROJECT/entry['albedo'])
    pixels=[tuple(buf[i:i+3]) for i in range(0,len(buf),3)];palette=set(pixels)
    mat=(PROJECT/entry['material']).read_text()
    sx,sy=map(float,re.search(r'- _BaseMap:.*?m_Scale: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}',mat,re.S).groups())
    density=w*sx;rough,rw,rh=load(PROJECT/entry['roughness'])
    point=all('filterMode: 0' in (PROJECT/(entry[k]+'.meta')).read_text() and
              'enableMipMap: 0' in (PROJECT/(entry[k]+'.meta')).read_text() for k in ('albedo','roughness'))
    target={'Stone':28,'Stone_Sub':22,'Snow':20,'Wood':24}[name]
    data=dict(palette=len(palette),texel_per_m=density,gaussian_sigma1=gaussian_rgb(buf,w,h),
              chroma_ab_max=max(max(abs(v) for v in lab(p)[1:]) for p in palette),
              mean_L=sum(lab(p)[0] for p in pixels)/len(pixels),
              luma_steps=0,block_w_m=[0,0],block_h_m=[0,0],size_cv=0,checker_score=0,
              texture_ok=point and abs(density-target)<1e-4 and abs(sy*h-target)<1e-4 and
              abs(sx-entry['tiling_x'])<1e-6 and abs(sy-entry['tiling_y'])<1e-6 and (rw,rh)==(w,h))
    if name not in RAMPS:return data
    sub=name=='Stone_Sub';blocks=manifest['sub_blocks' if sub else 'church_blocks']
    ramp=list(map(rgb_hex,RAMPS[name]));mortar=rgb_hex('1A1A1C' if sub else '595959')
    allowed=set(ramp)|{mortar}
    if not sub:allowed|={tuple(round(v*1.12) for v in c) for c in ramp}
    expected=[None]*(w*h); coverage=[0]*(w*h);body_tiers=set();adjacent=[];vertical=[];outline_widths=[]
    for block in blocks:
        row,col,x,y,bw,bh,tier=(block[k] for k in ('row','col','x','y','w','h','tier'))
        peers=[b for b in blocks if b['row']==row]
        prev=next(b for b in peers if b['col']==(col-1)%len(peers))
        nxt=next(b for b in peers if b['col']==(col+1)%len(peers))
        left=0 if bw==4 or prev['w']==4 else hash32(col+31,row+9)%2
        right=0 if bw==4 or nxt['w']==4 else hash32(nxt['col']+31,row+9)%2
        outline_widths.append(bw+right if sub else bw)
        for yy in range(bh):
            jog=left if sub and yy>=bh//2 else 0
            actual_width=bw+(right-left if sub and yy>=bh//2 else 0)
            for xx in range(actual_width):
                idx=(h-1-y-yy)*w+(x+xx+jog)%w;coverage[idx]+=1
                if yy==0 or xx==0 or (sub and bw>=8 and xx==1):color=mortar
                elif not sub and yy==1:
                    color=tuple(round(v*1.12) for v in ramp[tier])
                else:
                    color=ramp[tier]
                    if pixels[idx]==color:body_tiers.add(tier)
                expected[idx]=color
        peers=[b for b in blocks if b['row']==row]
        nxt=next(b for b in peers if b['col']==(col+1)%len(peers))
        adjacent.append((tier,nxt['tier']))
        # Check vertical adjacency over all overlapping spans, including texture wrap.
        last=max(b['row'] for b in blocks)
        for above in blocks:
            if above['row']!=(row+1)%(last+1):continue
            if any((x+xx-above['x'])%w<above['w'] for xx in range(bw)):
                vertical.append((tier,above['tier']))
    widths=[v/density for v in outline_widths];heights=[b['h']/density for b in blocks]
    chroma_body=max(max(abs(v) for v in lab(p)[1:]) for p in ramp)
    bound_w=(.15,.45) if sub else (.55,1.15);bound_h=(.15,.45) if sub else (.32,.60)
    size_ok=(bound_w[0]<=min(widths)<=max(widths)<=bound_w[1] and
             bound_h[0]<=min(heights)<=max(heights)<=bound_h[1] and
             min(cv(widths),cv(heights))>=.15 and (not sub or 5<=len(set(b['row'] for b in blocks))<=7))
    data.update(bright_px_pct=sum(p in {ramp[-1],tuple(round(v*1.12) for v in ramp[-1])} for p in pixels)/len(pixels),
                luma_steps=len(body_tiers),block_w_m=[min(widths),max(widths)],
                block_h_m=[min(heights),max(heights)],size_cv=min(cv(widths),cv(heights)),
                width_cv=cv(widths),height_cv=cv(heights),body_tier_counts={str(t):sum(b['tier']==t for b in blocks) for t in range(4)},blocks=len(blocks),rows=len(set(b['row'] for b in blocks)),
                checker_score=sum({a,b}=={0,3} for a,b in adjacent)/len(adjacent),
                adjacent_tier_max=max(abs(a-b) for a,b in adjacent+vertical),
                chroma_body_max=chroma_body,size_ok=size_ok,
                raster_ok=all(n==1 for n in coverage) and pixels==expected and palette<=allowed,
                body_luma_range=(sum(ramp[-1])/3-sum(ramp[0])/3)/255,outline_jog_px=1 if sub else 0,
                body_palette=['#'+v for v in RAMPS[name]],mortar='#1A1A1C' if sub else '#595959')
    return data


def lab(rgb):
    linear=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in (c/255 for c in rgb)]
    r,g,b=linear
    xyz=((.4124564*r+.3575761*g+.1804375*b)/.95047,
         .2126729*r+.7151522*g+.0721750*b,
         (.0193339*r+.1191920*g+.9503041*b)/1.08883)
    f=[v**(1/3) if v>(6/29)**3 else v/(3*(6/29)**2)+4/29 for v in xyz]
    return (116*f[1]-16,500*(f[0]-f[1]),200*(f[1]-f[2]))


def family_gate(shots,box):
    averages=[];paths=[]
    for name in ('church','sub'):
        source=shots/f'style4_family_{name}.png';small=shots/f'style4_family_{name}_480.png'
        subprocess.run(['sips','-z','270','480',str(source),'--out',str(small)],check=True,stdout=subprocess.DEVNULL)
        buf,w,h=load(small);x0,y0,x1,y1=[round(v*(w if i%2==0 else h)) for i,v in enumerate(box)]
        values=[lab(buf[(y*w+x)*3:(y*w+x)*3+3]) for y in range(y0,y1) for x in range(x0,x1)]
        averages.append([sum(v[c] for v in values)/len(values) for c in range(3)]);paths.append(str(small))
    distance=math.sqrt(sum((a-b)**2 for a,b in zip(*averages)))
    return dict(delta_e=distance,pass_gate=20<=distance<=40,method='CIE76_D65_mean_Lab_same_wall_AB_480',roi=box,mean_lab=averages,files=paths)


def projection_gate():
    checks={}
    paths={'M_ChurchStone':('Stone',28,1.5),'Stone_Sub':('Stone_Sub',22,2.8),
           'Stone_Sub_Steps':('Stone_Sub',22,2.8),'Stone_Sub_Coping':('Stone_Sub',22,2.8),'M_IndoorShell':('Stone',28,1.5*.82),
           'M_InteriorWallFoot':('Stone',28,1.5*.82)}
    for name,(texture,density,tint) in paths.items():
        mat=(PROJECT/f'Assets/Settings/{name}.mat').read_text()
        scale=re.search(r'- _BaseMap:.*?m_Texture: \{[^}]*guid: (\w+)[^}]*\}.*?m_Scale: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}.*?m_Offset: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}',mat,re.S)
        guid,sx,sy,ox,oy=scale.groups();sx,sy,ox,oy=map(float,(sx,sy,ox,oy))
        expected_guid=re.search(r'guid: (\w+)',(ROOT/(texture+'_Albedo.png.meta')).read_text())[1]
        rgb=list(map(float,re.search(r'- _BaseColor: \{r: ([\d.eE+-]+), g: ([\d.eE+-]+), b: ([\d.eE+-]+)',mat).groups()))
        top=float(re.search(r'- _ForgeTopGain: ([\d.eE+-]+)',mat)[1])
        ok=(guid==expected_guid and abs(sx*(56 if texture=='Stone_Sub' else 256)-density)<1e-4 and abs(sy*(56 if texture=='Stone_Sub' else 256)-density)<1e-4 and ox==oy==0
            and bool(re.search(r'- _ForgeSurface: 2\b',mat)) and all(abs(v-tint)<1e-5 for v in rgb)
            and abs(top-(1.08 if name=='Stone_Sub_Steps' else 1))<1e-5)
        checks[name]=dict(texture=texture,texel_per_m=sx*(56 if texture=='Stone_Sub' else 256),tint=rgb,top_gain=top,stone_exposure=1.5 if texture=='Stone' else 2.8,interior_relative_gain=tint/1.5 if name in ('M_IndoorShell','M_InteriorWallFoot') else tint/(1.5 if texture=='Stone' else 2.8),pass_gate=ok)
    coping=(PROJECT/'Assets/Settings/Stone_Sub_Coping.mat').read_text()
    plane=list(map(float,re.search(r'- _ForgeCopingPlane: \{r: ([\d.eE+-]+), g: ([\d.eE+-]+), b: ([\d.eE+-]+), a: ([\d.eE+-]+)',coping).groups()))
    shader=(PROJECT/'Assets/Art/Shaders/TerrainHaze.shader').read_text()
    checks['coping_band']=dict(top_plane=plane,height_m=.25,body_srgb='#565656',cap_srgb='#6A6A6A',
        pass_gate=abs(plane[1]-1)<1e-5 and plane[3]<0 and 'capDepth <= .25' in shader)
    # Verify actual serialized renderer material bindings, including every existing tread.
    scene=(PROJECT/'Assets/Scenes/ArtSpike.unity').read_text()
    step_guid=re.search(r'guid: (\w+)',(PROJECT/'Assets/Settings/Stone_Sub_Steps.mat.meta').read_text())[1]
    checks['step_binding']=dict(renderer_references=scene.count('guid: '+step_guid),pass_gate=scene.count('guid: '+step_guid)==9)
    coping_guid=re.search(r'guid: (\w+)',(PROJECT/'Assets/Settings/Stone_Sub_Coping.mat.meta').read_text())[1]
    checks['coping_binding']=dict(renderer_references=scene.count('guid: '+coping_guid),pass_gate=scene.count('guid: '+coping_guid)>0)
    return checks


def frame_gate():
    proc=subprocess.run([sys.executable,str(REPO/'spike/tools/measure_shot.py'),str(PROJECT/'Shots/art_runtime.png'),'--json'],check=True,capture_output=True,text=True)
    data=json.loads(proc.stdout)
    buf,w,h=load(PROJECT/'Shots/art_runtime.png')
    # Fixed unobscured mountain band at left, chosen before v3 captures: no sky/church/snowfield.
    roi=(.02,.32,.20,.40);x0,y0,x1,y1=[round(v*(w if i%2==0 else h)) for i,v in enumerate(roi)]
    lumas=[sum(buf[(y*w+x)*3+c]*v for c,v in enumerate((.299,.587,.114)))/255 for y in range(y0,y1) for x in range(x0,x1)]
    data['mountain_luma']=sum(lumas)/len(lumas);data['mountain_roi']=roi
    data['pass']=data['snow_ratio']>=.55 and data['white_clip_ratio']<=.05 and data['magenta_px']==0 and .42<=data['mountain_luma']<=.60
    return data


def main():
    manifest=json.loads((ROOT/'manifest.json').read_text())
    results={e['name']:analyze(e,manifest) for e in manifest['materials']}
    for name,r in results.items():
        print(f"[STYLE5] {name} palette={r['palette']} texel_per_m={r['texel_per_m']:.4f} luma_steps={r['luma_steps']} chroma_ab_max={r['chroma_ab_max']:.4f} block_w_m={r['block_w_m'][0]:.4f}-{r['block_w_m'][1]:.4f} block_h_m={r['block_h_m'][0]:.4f}-{r['block_h_m'][1]:.4f} size_cv={r['size_cv']:.4f} checker_score={r['checker_score']:.4f}",flush=True)
        print('[STYLE5_DETAILS] '+name+' '+json.dumps(r),flush=True)
    residuals={n:results[n]['gaussian_sigma1'] for n in ('Stone','Stone_Sub','Wood','Snow')}
    spread=(max(residuals.values())-min(residuals.values()))/min(residuals.values());hf_ok=spread<=.20
    print('[STYLE5_HF] sigma=1.0 '+' '.join(f'{"Sub" if n=="Stone_Sub" else n}={v:.6f}' for n,v in residuals.items())+f' spread={spread:.6f} method=mean_abs_RGB_gaussian_radius3_wrap_all_four',flush=True)
    regions=json.loads((REPO/'spike/editor/material_style_regions.json').read_text())
    try:family=family_gate(PROJECT/'Shots',regions['normalized_rects']['Stone'])
    except (FileNotFoundError,subprocess.CalledProcessError) as exc:family=dict(delta_e=None,pass_gate=False,error=str(exc))
    print('[STYLE5_FAMILY] church_vs_sub ΔE='+str(family['delta_e'])+' '+json.dumps(family),flush=True)
    projection=projection_gate();frame=frame_gate()
    print('[STYLE5_PROJECTION] '+json.dumps(projection),flush=True)
    print('[STYLE5_FRAME] '+json.dumps(frame),flush=True)
    preserved={n:dict(sha256=hashlib.sha256((ROOT/n).read_bytes()).hexdigest(),expected=v) for n,v in PRESERVED.items()}
    for r in preserved.values():r['pass']=r['sha256']==r['expected']
    print('[STYLE5_PRESERVED] '+json.dumps(preserved),flush=True)
    stone,sub=results['Stone'],results['Stone_Sub'];stones=(stone,sub)
    gates=dict(white=44<=stone['mean_L']<=52 and stone['bright_px_pct']<=.12,
               palette=all(v['palette']<=10 for v in stones),
               mono=all(v['chroma_ab_max']<=6 and v['chroma_body_max']<=3 for v in stones),
               luma=all(v['luma_steps']==4 for v in stones) and sub['body_luma_range']>=.18,
               checker=all(v['checker_score']<=.25 for v in stones) and stone['adjacent_tier_max']<=2,
               size=all(v['size_ok'] for v in stones),raster=all(v['raster_ok'] for v in stones),
               textures=all(v['texture_ok'] for v in results.values()),hf=hf_ok,family=family['pass_gate'],
               projection=all(v['pass_gate'] for v in projection.values()),frame=frame['pass'],preserved=all(v['pass'] for v in preserved.values()))
    passed=all(gates.values());print('[STYLE5_CHECKS] '+json.dumps(gates),flush=True)
    print(f"[STYLE5] Stone palette={stone['palette']} luma_steps={stone['luma_steps']} mean_L={stone['mean_L']:.6f} bright_px_pct={stone['bright_px_pct']:.6f} chroma_ab_max={stone['chroma_ab_max']:.6f} texel_per_m={stone['texel_per_m']:.4f}",flush=True)
    print('[STYLE5_UNCHANGED] '+' '.join(label+'_sha_same='+str(preserved[name+'_Albedo.png']['pass']) for label,name in [('Sub','Stone_Sub'),('Snow','Snow'),('Wood','Wood')]),flush=True)
    print(f"[STYLE5_GATE] white_ok={gates['white']} mono_ok={gates['mono']} unchanged_ok={gates['preserved']} hf_ok={hf_ok} pass={passed}",flush=True)
    report=dict(scope='style5_bright_church',materials=results,hf=residuals,hf_spread=spread,family=family,projection=projection,frame=frame,preserved=preserved,gates=gates,pass_gate=passed)
    (ROOT/'measurement.json').write_text(json.dumps(report,indent=2)+'\n')
    return 0 if passed else 1

if __name__=='__main__':sys.exit(main())
