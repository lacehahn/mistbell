#!/usr/bin/env python3
"""STYLE3 sample measurement: stdlib + existing sips/ffmpeg, no dependency install.

Albedo HF: equal-weight RGB absolute Gaussian residual, sigma=1, radius=3,
periodic boundaries. Spread is (max-min)/min for Stone/Wood/Snow only.
Stone step_ratio alone excludes 1px mortar + 1px bevel on all block edges.
Family distance uses the fixed Stone ROI in paired 480x270 runtime renders
of the SAME church wall, swapping only the material via -style3-family.
The seven-material scope stays deferred; this gate is only the v3 samples.
"""
import colorsys
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


def png(path, rgb, w, h):
    def chunk(tag, data):
        return struct.pack('>I', len(data))+tag+data+struct.pack('>I', zlib.crc32(tag+data)&0xffffffff)
    raw = b''.join(b'\0'+rgb[y*w*3:(y+1)*w*3] for y in range(h))
    pathlib.Path(path).write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR', struct.pack('>IIBBBBB', w,h,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))


def runs(values):
    """Inclusive-start/exclusive-end runs of true samples."""
    out=[]; start=None
    for i,v in enumerate(list(values)+[False]):
        if v and start is None: start=i
        if not v and start is not None: out.append((start,i)); start=None
    return out


def gaussian_residual(buf,w,h):
    # Independent albedo calibration: mean absolute luminance residual, sigma=1,
    # separable radius=3 Gaussian, periodic boundaries, normalized to [0,1].
    weights=[math.exp(-i*i/2) for i in range(-3,4)]
    weights=[v/sum(weights) for v in weights]
    a=[sum(buf[i+c]*v for c,v in enumerate((.299,.587,.114)))/255 for i in range(0,len(buf),3)]
    temp=[sum(a[y*w+(x+d)%w]*weights[d+3] for d in range(-3,4)) for y in range(h) for x in range(w)]
    blurred=[sum(temp[((y+d)%h)*w+x]*weights[d+3] for d in range(-3,4)) for y in range(h) for x in range(w)]
    return sum(abs(a-b) for a,b in zip(a,blurred))/len(a)


# v2 reference: generated before this task, immutable original Albedo pixel measurements.
V2_STONE_SHA256 = 'bc522112655fa48e2d7f546ab62ea54e7c85d4d554282a237798d9b64a5031eb'
V2_STONE_SAT = 0.2715841509815626
V2_SNOW_SHA256 = '8a13e96e9daef38854d7d92157246fd3d1d38412de97338bc5f967a2deeb3375'
WARM = ['8C7C66','A8967C','C2B49A']
COOL = ['7E848C','9298A0','A9AEB4']


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


def analyze(entry):
    name=entry['name']; buf,w,h=load(PROJECT/entry['albedo'])
    pixels=[tuple(buf[i:i+3]) for i in range(0,len(buf),3)]
    palette=set(pixels)
    sat=sum((max(p)-min(p))/max(p) if max(p) else 0 for p in pixels)/len(pixels)
    mean_rgb=[sum(buf[c::3])/len(pixels) for c in range(3)]
    # Hue of the mean RGB; avoids the wrap-around problem of arithmetic hue means.
    hue=colorsys.rgb_to_hsv(*(v/255 for v in mean_rgb))[0]*360
    seam=max(abs(a-b)/255 for p,q in
             [(pixels[y*w],pixels[y*w+w-1]) for y in range(h)]+
             [(pixels[x],pixels[(h-1)*w+x]) for x in range(w)] for a,b in zip(p,q))
    mat=(PROJECT/entry['material']).read_text()
    scale=re.search(r'- _BaseMap:.*?m_Scale: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}',mat,re.S)
    sx,sy=map(float,scale.groups()); density=w*sx
    serialized_ok=abs(sx-entry['tiling_x'])<1e-6 and abs(sy-entry['tiling_y'])<1e-6
    projection=re.search(r'- _ForgeSurface: (\d+)',mat)
    serialized_ok=serialized_ok and int(projection[1])==entry['forge_surface']
    rough,rw,rh=load(PROJECT/entry['roughness'])
    point=all('filterMode: 0' in (PROJECT/(entry[k]+'.meta')).read_text() and
              'enableMipMap: 0' in (PROJECT/(entry[k]+'.meta')).read_text() for k in ('albedo','roughness'))
    data=dict(palette=len(palette),texel_per_m=density,seam_grad=seam,mean_sat=sat,
              mean_hue=hue,mean_rgb=mean_rgb,gaussian_sigma1=gaussian_rgb(buf,w,h),
              legacy_luma_hf=gaussian_residual(buf,w,h),point_no_mipmap=point,
              mortar_px=0,block_px=[0,0],cool_block_pct=0,step_ratio=None)
    if name in ('Stone','Stone_Sub'):
        sub=name=='Stone_Sub'; gain=.8 if sub else 1
        mortar=rgb_hex('8C7F6E',gain)
        levels={}
        for ramp in (WARM,COOL):
            body=rgb_hex(ramp[0]);shadow=tuple(round(v*.94) for v in body)
            levels[tuple(round(v*gain) for v in shadow)]=0
            levels[rgb_hex(ramp[0],gain)]=1
            for value in ramp[1:]:levels[rgb_hex(value,gain)]=2
        expected=set(levels)|{mortar}
        rows=[y for y in range(h) if all(pixels[y*w+x]==mortar for x in range(w))]
        heights=[(b-a)%h for a,b in zip(rows,rows[1:]+rows[:1])]
        widths=[]; temperatures=[]; courses=[]; interior=set()
        continuous=True
        for row,top in enumerate(rows):
            height=heights[row]; y=(top+3)%h
            cols=[x for x in range(w) if pixels[y*w+x]==mortar]
            spans=[(b-a)%w for a,b in zip(cols,cols[1:]+cols[:1])]
            widths.extend(spans); courses.append(cols)
            temps=[]
            for left,width in zip(cols,spans):
                center=pixels[y*w+(left+width//2)%w]
                cool=center[2]>center[0];temps.append(cool);temperatures.append(cool)
                # Interior excludes one pixel mortar and one pixel bevel on ALL sides.
                for yy in range(2,height-1):
                    for xx in range(2,width-1):interior.add(((top+yy)%h)*w+(left+xx)%w)
            for yy in range(1,height):
                line=(top+yy)%h
                continuous=continuous and all(pixels[line*w+x]==mortar for x in cols)
        changed=0
        for idx in interior:
            y,x=divmod(idx,w)
            neighbors=[y*w+(x+1)%w,y*w+(x-1)%w,((y+1)%h)*w+x,((y-1)%h)*w+x]
            changed+=any(j in interior and levels.get(pixels[j])!=levels.get(pixels[idx]) for j in neighbors)
        # Exclude no pixels from palette/seam/HF; the interior mask applies ONLY to step_ratio.
        step=changed/max(1,len(interior))
        bond=all(9<=min((abs(a-b)%w) if abs(a-b)<=w/2 else w-abs(a-b) for b in nxt)<=12
                 for current,nxt in zip(courses,courses[1:]+courses[:1]) for a in current)
        bw=(min(widths,default=0),max(widths,default=0));bh=(min(heights,default=0),max(heights,default=0))
        data.update(mortar_px=1 if rows and continuous else 0,block_px=[bw[1],bh[1]],
                    block_width_range=bw,block_height_range=bh,cool_block_pct=sum(temperatures)/max(1,len(temperatures)),
                    cool_blocks=sum(temperatures),blocks=len(temperatures),mortar_continuous=continuous,
                    staggered_bond=bond,step_ratio=step,step_ratio_scope='block_interior',interior_pixels=len(interior))
        allowed_height=(10,14) if sub else (16,16)
        allowed_width=(18,26) if sub else (23,24)
        data['structure_ok']=(palette==expected and continuous and bond and
                              allowed_width[0]<=bw[0]<=bw[1]<=allowed_width[1] and
                              allowed_height[0]<=bh[0]<=bh[1]<=allowed_height[1] and .08<=step<=.25)
        data['temperature_neighbor_difference']=sum(
            (pixels[((top+3)%h)*w+(a+3)%w][2]>pixels[((top+3)%h)*w+(a+3)%w][0]) !=
            (pixels[((top+3)%h)*w+(b+3)%w][2]>pixels[((top+3)%h)*w+(b+3)%w][0])
            for top,cols in zip(rows,courses) for a,b in zip(cols,cols[1:]+cols[:1]))/max(1,len(temperatures))
    elif name=='Wood':
        door=json.loads((ROOT/'manifest.json').read_text())['door']
        visible=math.ceil(door['width_metres']*door['texels_per_meter'])
        dark=rgb_hex('2E2018');grain_color=rgb_hex('3A281D')
        columns=runs(all(pixels[y*w+x]==dark for y in range(h)) for x in range(visible))
        seams=[a for a,b in columns if b-a==1 and a>0 and b<visible]
        widths=[b-a for y in range(h) for a,b in runs(pixels[y*w+x]==grain_color for x in range(visible))]
        board_edges=[0]+seams+[visible];max_density=0
        for left,right in zip(board_edges,board_edges[1:]):
            for y in range(h):
                grain_density=sum(pixels[y*w+x]==grain_color for x in range(left+1,right))/max(1,right-left-1)
                max_density=max(max_density,grain_density)
        data.update(wood_seams=len(seams),grain_width_px=[min(widths,default=0),max(widths,default=0)],grain_max_density=max_density,
                    door_width_texels=visible,seam_columns=seams)
        data['structure_ok']=2<=len(seams)<=3 and bool(widths) and all(3<=v<=5 for v in widths) and max_density<=.25
    else:
        data['sha256']=hashlib.sha256((PROJECT/entry['albedo']).read_bytes()).hexdigest()
        data['structure_ok']=data['sha256']==V2_SNOW_SHA256
    target={'Stone':28,'Stone_Sub':24,'Snow':20,'Wood':24}[name]
    data['texture_ok']=(len(palette)<=12 and seam<=.02 and point and serialized_ok and
                        abs(density-target)<1e-4 and abs(sy*w-target)<1e-4 and (w,h,rw,rh)==(256,256,256,256))
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
        source=shots/f'style3_family_{name}.png';small=shots/f'style3_family_{name}_480.png'
        subprocess.run(['sips','-z','270','480',str(source),'--out',str(small)],check=True,stdout=subprocess.DEVNULL)
        buf,w,h=load(small);x0,y0,x1,y1=[round(v*(w if i%2==0 else h)) for i,v in enumerate(box)]
        values=[lab(buf[(y*w+x)*3:(y*w+x)*3+3]) for y in range(y0,y1) for x in range(x0,x1)]
        averages.append([sum(v[c] for v in values)/len(values) for c in range(3)]);paths.append(str(small))
    distance=math.sqrt(sum((a-b)**2 for a,b in zip(*averages)))
    return dict(delta_e=distance,pass_gate=6<=distance<=18,method='CIE76_D65_mean_Lab_same_wall_AB_480',roi=box,mean_lab=averages,files=paths)


def projection_gate():
    checks={}
    paths={'M_ChurchStone':('Stone',28,1),'Stone_Sub':('Stone_Sub',24,1),
           'Stone_Sub_Steps':('Stone_Sub',24,1),'M_IndoorShell':('Stone_Sub',24,.82),
           'M_InteriorWallFoot':('Stone_Sub',24,.82)}
    for name,(texture,density,tint) in paths.items():
        mat=(PROJECT/f'Assets/Settings/{name}.mat').read_text()
        scale=re.search(r'- _BaseMap:.*?m_Texture: \{[^}]*guid: (\w+)[^}]*\}.*?m_Scale: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}.*?m_Offset: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}',mat,re.S)
        guid,sx,sy,ox,oy=scale.groups();sx,sy,ox,oy=map(float,(sx,sy,ox,oy))
        expected_guid=re.search(r'guid: (\w+)',(ROOT/(texture+'_Albedo.png.meta')).read_text())[1]
        rgb=list(map(float,re.search(r'- _BaseColor: \{r: ([\d.eE+-]+), g: ([\d.eE+-]+), b: ([\d.eE+-]+)',mat).groups()))
        top=float(re.search(r'- _ForgeTopGain: ([\d.eE+-]+)',mat)[1])
        ok=(guid==expected_guid and abs(sx*256-density)<1e-4 and abs(sy*256-density)<1e-4 and ox==oy==0
            and bool(re.search(r'- _ForgeSurface: 2\b',mat)) and all(abs(v-tint)<1e-5 for v in rgb)
            and abs(top-(1.08 if name=='Stone_Sub_Steps' else 1))<1e-5)
        checks[name]=dict(texture=texture,texel_per_m=sx*256,tint=rgb,top_gain=top,pass_gate=ok)
    # Verify actual serialized renderer material bindings, including every existing tread.
    scene=(PROJECT/'Assets/Scenes/ArtSpike.unity').read_text()
    step_guid=re.search(r'guid: (\w+)',(PROJECT/'Assets/Settings/Stone_Sub_Steps.mat.meta').read_text())[1]
    checks['step_binding']=dict(renderer_references=scene.count('guid: '+step_guid),pass_gate=scene.count('guid: '+step_guid)==9)
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
    results={e['name']:analyze(e) for e in manifest['materials']}
    for name,r in results.items():
        print(f"[STYLE3] {name} palette={r['palette']} texel_per_m={r['texel_per_m']:.4f} mortar_px={r['mortar_px']} block_px={r['block_px'][0]}x{r['block_px'][1]} cool_block_pct={r['cool_block_pct']:.6f} mean_sat={r['mean_sat']:.6f}",flush=True)
        print('[STYLE3_DETAILS] '+name+' '+json.dumps(r),flush=True)
    residuals={n:results[n]['gaussian_sigma1'] for n in ('Stone','Wood','Snow')}
    spread=(max(residuals.values())-min(residuals.values()))/min(residuals.values())
    hf_ok=spread<=.20 and .010<=residuals['Snow']<=.016
    print('[STYLE3_HF] sigma=1.0 '+' '.join(f'{n}={v:.6f}' for n,v in residuals.items())+f' spread={spread:.6f} method=mean_abs_RGB_gaussian_radius3_wrap',flush=True)
    shots=PROJECT/'Shots';regions=json.loads((REPO/'spike/editor/material_style_regions.json').read_text())
    subprocess.run(['sips','-z','270','480',str(shots/'art_close.png'),'--out',str(shots/'style_480.png')],check=True,stdout=subprocess.DEVNULL)
    sb,sw,sh=load(shots/'style_480.png');up=bytearray()
    for y in range(sh):
        row=b''.join(bytes(sb[(y*sw+x)*3:(y*sw+x)*3+3])*4 for x in range(sw))
        for _ in range(4):up.extend(row)
    png(shots/'style_480_upscaled.png',up,sw*4,sh*4)
    try:family=family_gate(shots,regions['normalized_rects']['Stone'])
    except (FileNotFoundError,subprocess.CalledProcessError) as exc:family=dict(delta_e=None,pass_gate=False,error=str(exc))
    print('[STYLE3_FAMILY] church_vs_sub ΔE='+str(family['delta_e'])+' '+json.dumps(family),flush=True)
    projection=projection_gate();frame=frame_gate()
    print('[STYLE3_PROJECTION] '+json.dumps(projection),flush=True)
    print('[STYLE3_FRAME] '+json.dumps(frame),flush=True)
    stone,sub=results['Stone'],results['Stone_Sub']
    sat_drop=1-stone['mean_sat']/V2_STONE_SAT
    # Ratios compare each of the identical six family palette slots (8-bit rounding allowed).
    dark_ratios=[b/a for ramp in (WARM,COOL) for value in ramp for a,b in zip(rgb_hex(value),rgb_hex(value,.8))]
    luma=lambda rgb:sum(v*c for v,c in zip(rgb,(.299,.587,.114)))
    sub_darkening=1-luma(sub['mean_rgb'])/luma(stone['mean_rgb'])
    gates=dict(palette=all(v['palette']<=12 for v in results.values()),textures=all(v['texture_ok'] for v in results.values()),
               structure=all(v['structure_ok'] for v in results.values()),
               cool=.30<=stone['cool_block_pct']<=.40 and .45<=sub['cool_block_pct']<=.55,
               sat_drop=.15<=sat_drop<=.25,mean_hue=0<=stone['mean_hue']<=60,
               sub_dark=all(.78<=r<=.82 for r in dark_ratios) and .18<=sub_darkening<=.22,hf=hf_ok,family=family['pass_gate'],
               projection=all(v['pass_gate'] for v in projection.values()),frame=frame['pass'])
    passed=all(gates.values());print('[STYLE3_CHECKS] '+json.dumps(dict(gates=gates,v2_stone_sha256=V2_STONE_SHA256,sat_drop=sat_drop,sub_mean_luma_darkening=sub_darkening,step_ratio_scope='block_interior')),flush=True)
    print(f"[STYLE3_GATE] palette_ok={gates['palette']} hf_ok={hf_ok} family_ok={family['pass_gate']} wood_seams={results['Wood']['wood_seams']} pass={passed}",flush=True)
    report=dict(scope='style3_four_samples',materials=results,hf=residuals,hf_spread=spread,family=family,projection=projection,frame=frame,gates=gates,sat_drop=sat_drop,sub_mean_luma_darkening=sub_darkening,pass_gate=passed)
    (ROOT/'measurement.json').write_text(json.dumps(report,indent=2)+'\n')
    return 0 if passed else 1

if __name__=='__main__':sys.exit(main())
