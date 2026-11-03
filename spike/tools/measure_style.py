#!/usr/bin/env python3
"""Three-sample STYLE2 measurement; stdlib + sips, with fixed existing screenshot ROIs.

step_ratio retains the four-neighbour ramp-boundary definition (both sides counted).
The old STYLE line still reports the original 8–25% step gate independently: the
v2 1px continuous mortar and bevel can exceed it; it is never silently redefined.
STYLE2_GATE includes the v2 gates, albedo calibration and retained original step limits.
Screenshot HF remains the unchanged horizontal two-pixel RGB absolute difference.
Gaussian sigma=1 is a separate albedo calibration, never substituted for screenshot HF.
Exit 0 requires STYLE2_GATE pass, including frame checks; other materials are deferred.
"""
import argparse
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
RANGES = {'Snow': (16, 24), 'Stone': (24, 32), 'Wood': (20, 28)}


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


def texture(entry):
    buf, w, h = load(PROJECT / entry['albedo'])
    palette = sorted(set(tuple(buf[i:i+3]) for i in range(0,len(buf),3)), key=sum)
    # Closest known level in any of the block brightness variants; avoids counting
    # ±8% block variation as a fourth lighting level.
    base = {'Snow': ['DCE8F2','EDF3F8','F6FAFD'], 'Stone': ['6E6050','7D6D5B','8C7A66'], 'Wood': ['3A281D','4C3728','6B4A32']}[entry['name']]
    candidates=[]
    for level, value in enumerate(base):
        for gain in ([.92,1,1.08] if entry['name']=='Stone' else [1]):
            candidates.append((tuple(round(int(value[i:i+2],16)*gain) for i in (0,2,4)),level))
    levels = {c: min(candidates,key=lambda q:sum((a-b)**2 for a,b in zip(c,q[0])))[1] for c in palette}
    pixels = [tuple(buf[i:i+3]) for i in range(0,len(buf),3)]
    boundary = sum(any(levels[pixels[y*w+x]] != levels[pixels[yy*w+xx]] for xx,yy in (((x+1)%w,y),((x-1)%w,y),(x,(y+1)%h),(x,(y-1)%h))) for y in range(h) for x in range(w))
    pairs = [(pixels[y*w],pixels[y*w+w-1]) for y in range(h)] + [(pixels[x],pixels[(h-1)*w+x]) for x in range(w)]
    seam = max(abs(a-b)/255 for p,q in pairs for a,b in zip(p,q))
    scale = entry['tiling_x']
    # Check the actual serialized Unity material, not just the generated manifest.
    mat=(PROJECT / entry['material']).read_text()
    import re
    section=re.search(r'- _BaseMap:\s*\n\s*m_Texture:.*?\n\s*m_Scale: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}',mat)
    if not section: raise ValueError('Cannot read material BaseMap tiling: '+entry['material'])
    sx,sy=map(float,section.groups())
    if abs(sx-scale)>1e-6 or abs(sy-entry['tiling_y'])>1e-6: raise ValueError('Stale manifest tiling')
    density=w*sx
    low,high=RANGES[entry['name']]
    rough,rw,rh=load(PROJECT / entry['roughness'])
    importer=(PROJECT / (entry['albedo']+'.meta')).read_text()
    rough_importer=(PROJECT / (entry['roughness']+'.meta')).read_text()
    point=all('filterMode: 0' in m and 'enableMipMap: 0' in m for m in (importer,rough_importer))
    if not re.search(r'- _ForgeSurface: '+str(entry['forge_surface'])+r'\b',mat):
        raise ValueError('Forge projection is not enabled on material')
    result=dict(palette=len(palette),texel_per_m=density,step_ratio=boundary/(w*h),seam_grad=seam,point_no_mipmap=point,roughness_palette=len(set(tuple(rough[i:i+3]) for i in range(0,len(rough),3))))
    result['pass']=len(palette)<=12 and low<=density<=high and .08<=result['step_ratio']<=.25 and seam<=.02 and point and (w,h,rw,rh)==(256,256,256,256)
    return result


def hf(buf,w,h,box):
    x0,y0,x1,y1=box
    if not 0<=x0<x1<=w or not 0<=y0<y1<=h: raise ValueError('ROI outside image')
    total=n=0
    for y in range(y0,y1,2):
        for x in range(x0,x1-2,2):
            i=(y*w+x)*3
            total+=sum(abs(buf[i+c]-buf[i+6+c]) for c in range(3)); n+=3
    return total/max(n,1)/255


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


def structure(name):
    buf,w,h=load(ROOT/(name+'_Albedo.png'))
    pixels=[tuple(buf[i:i+3]) for i in range(0,len(buf),3)]
    data=dict(gaussian_sigma1=gaussian_residual(buf,w,h),mortar_px=0,block_px=[0,0])
    if name=='Stone':
        mortar=(94,82,69)
        rows=[y for y in range(h) if all(pixels[y*w+x]==mortar for x in range(w))]
        courses=runs(y in rows for y in range(h))
        widths=[]; offsets=[]
        for lo,hi in zip(courses,courses[1:]):
            y=lo[1]+2
            lines=runs(pixels[y*w+x]==mortar for x in range(w))
            widths.extend(b[0]-a[0] for a,b in zip(lines,lines[1:]))
            offsets.append([a for a,b in lines])
        heights=[b[0]-a[0] for a,b in zip(courses,courses[1:])]
        row_widths=[b-a for a,b in courses]
        line_widths=[b-a for y in range(h) if y not in rows for a,b in runs(pixels[y*w+x]==mortar for x in range(w)) if a>0 and b<w]
        # Every non-horizontal mortar pixel must continue to the next row within its course.
        continuous=all(all((pixels[y*w+x]==mortar)==(pixels[(y+1)*w+x]==mortar) for x in range(w)) for y in range(h-1) if y not in rows and y+1 not in rows)
        bond=all(9<=min(abs(a-b) for b in nxt)<=12 for current,nxt in zip(offsets,offsets[1:]) for a in current[1:-1])
        data.update(mortar_px=max(row_widths+line_widths,default=0),block_px=[max(widths,default=0),max(heights,default=0)],block_width_range=[min(widths,default=0),max(widths,default=0)],block_height_range=[min(heights,default=0),max(heights,default=0)],mortar_continuous=continuous,staggered_bond=bond)
        data['structure_ok']=bool(widths and heights) and all(v==1 for v in row_widths+line_widths) and all(16<=v<=24 for v in widths) and all(12<=v<=16 for v in heights) and continuous and bond
    elif name=='Wood':
        dark=(58,40,29)
        # Door width is measured from the actual mesh by the generator, in native texels.
        manifest=json.loads((ROOT/'manifest.json').read_text())
        door=manifest['door']
        visible=math.ceil(door['width_metres']*door['texels_per_meter'])
        columns=runs(all(pixels[y*w+x]==dark for y in range(h)) for x in range(visible))
        seams=[a for a,b in columns if b-a==1 and a>0 and b<visible]
        grain=[]
        for y in range(h):
            grain.extend(b-a for a,b in runs(pixels[y*w+x]==dark for x in range(1,visible-1)) if b-a>1)
        data.update(board_seams=len(seams),board_count=len(seams)+1,grain_width_px=[min(grain,default=0),max(grain,default=0)],door_width_texels=visible)
        data['structure_ok']=2<=len(seams)<=3 and bool(grain) and all(3<=v<=5 for v in grain)
    else:
        density=sum(p==(195,211,227) for p in pixels)/(w*h)
        data.update(speck_density=density)
        data['structure_ok']=.02<=density<=.04 and (213,228,240) in pixels
    return data


def projection_gate():
    # Read serialized assets, including the terrace and runtime indoor shell. No
    # unrelated interior stone/slate/pew material is claimed as a forged sample.
    paths={'M_ChurchStone':28,'M_TerraceAshlar':28,'M_IndoorShell':24,'M_InteriorWallFoot':24}
    checks={}
    for name,density in paths.items():
        mat=(PROJECT/('Assets/Settings/'+name+'.mat')).read_text()
        scale=re.search(r'- _BaseMap:.*?m_Scale: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}.*?m_Offset: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}',mat,re.S)
        sx,sy,ox,oy=map(float,scale.groups())
        indoor=name in ('M_IndoorShell','M_InteriorWallFoot')
        tint=re.search(r'- _BaseColor: \{r: ([\d.eE+-]+), g: ([\d.eE+-]+), b: ([\d.eE+-]+)',mat)
        rgb=list(map(float,tint.groups()))
        ok=abs(sx*256-density)<1e-4 and abs(sy*256-density)<1e-4 and ox==oy==0 and bool(re.search(r'- _ForgeSurface: 2\b',mat))
        if indoor: ok=ok and all(abs(v-.82)<1e-5 for v in rgb)
        checks[name]=dict(texel_per_m=sx*256,offset=[ox,oy],tint=rgb,pass_gate=ok)
    return checks


def frame_gate():
    # Use the unchanged existing frame measurement, never a new threshold or ROI.
    proc=subprocess.run([sys.executable,str(REPO/'spike/tools/measure_shot.py'),str(PROJECT/'Shots/art_runtime.png')],check=True,capture_output=True,text=True)
    line=next(line for line in proc.stdout.splitlines() if 'snow_ratio' in line)
    result={k:float(v) if '.' in v else int(v) for k,v in re.findall(r'(magenta_px|snow_ratio|white_clip_ratio)=([0-9.]+)',line)}
    result['pass']=result['snow_ratio']>=.55 and result['white_clip_ratio']<=.05 and result['magenta_px']==0
    return result


def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--shot',type=pathlib.Path,default=PROJECT/'Shots/art_close.png')
    ap.add_argument('--regions',type=pathlib.Path,default=REPO/'spike/editor/material_style_regions.json')
    args=ap.parse_args()
    manifest=json.loads((ROOT/'manifest.json').read_text())
    results={e['name']:texture(e) for e in manifest['materials']}
    for name,r in results.items():
        r.update(structure(name))
        print(f"[STYLE] {name} palette={r['palette']} texel_per_m={r['texel_per_m']:.4f} step_ratio={r['step_ratio']:.4f} seam_grad={r['seam_grad']:.4f} point_no_mipmap={r['point_no_mipmap']} pass={r['pass']}")
        print(f"[STYLE2] {name} palette={r['palette']} texel_per_m={r['texel_per_m']:.4f} mortar_px={r['mortar_px']} block_px={r['block_px'][0]}x{r['block_px'][1]} step_ratio={r['step_ratio']:.4f} seam_grad={r['seam_grad']:.4f}")
        print('[STYLE2_STRUCTURE] '+name+' '+json.dumps({k:v for k,v in r.items() if k in ('board_seams','board_count','grain_width_px','speck_density','structure_ok','mortar_continuous','staggered_bond','block_width_range','block_height_range')}))
    small=args.shot.parent/'style_480.png'
    subprocess.run(['sips','-z','270','480',str(args.shot),'--out',str(small)],check=True,stdout=subprocess.DEVNULL)
    sb,sw,sh=load(small)
    up=bytearray()
    for y in range(sh):
        row=b''.join(bytes(sb[(y*sw+x)*3:(y*sw+x)*3+3])*4 for x in range(sw))
        for _ in range(4): up.extend(row)
    png(args.shot.parent/'style_480_upscaled.png',up,sw*4,sh*4)
    spreads={}; energies={}
    regions=json.loads(args.regions.read_text())
    for label,path in [('native',args.shot),('480',small)]:
        buf,w,h=load(path)
        values={name:hf(buf,w,h,[round(v*(w if i%2==0 else h)) for i,v in enumerate(box)]) for name,box in regions['normalized_rects'].items()}
        spread=(max(values.values())-min(values.values()))/min(values.values()) if values and min(values.values())>0 else float('inf')
        spreads[label]=spread; energies[label]=values
        line='resolution='+label+' '+ ' '.join(f'{k}={v:.6f}' for k,v in values.items())+f' spread={spread:.6f}'
        print('[STYLE_HF] '+line); print('[STYLE2_HF] '+line)
    palette_ok=all(r['palette']<=12 for r in results.values())
    texel_ok=all(RANGES[n][0]<=r['texel_per_m']<=RANGES[n][1] for n,r in results.items())
    mortar_ok=results['Stone']['structure_ok']
    surface_ok=all(r['structure_ok'] and r['seam_grad']<=.02 and r['point_no_mipmap'] for r in results.values())
    hf_ok=spreads['480']<=.20 and .010<=energies['480']['Snow']<=.030
    residuals={n:r['gaussian_sigma1'] for n,r in results.items()}
    calibration_ok=.008<=residuals['Snow']<=.013 and .009<=residuals['Wood']<=.012
    print('[STYLE2_ALBEDO] method=mean_absolute_luminance_gaussian_residual sigma=1.0 radius=3 wrap=True '+' '.join(f'{n}={v:.6f}' for n,v in residuals.items())+f' calibration_ok={calibration_ok}')
    frame=frame_gate(); print('[STYLE2_FRAME] '+json.dumps(frame))
    projection=projection_gate(); print('[STYLE2_PROJECTION] '+json.dumps(projection))
    projection_ok=all(v['pass_gate'] for v in projection.values())
    legacy_step_ok=all(.08<=r['step_ratio']<=.25 for r in results.values())
    print(f'[STYLE2_LEGACY_STEP] step_ratio_ok={legacy_step_ok} definition=both_sides_four_neighbour_boundary original_limits=0.08..0.25')
    v2_hard_gates_pass=palette_ok and texel_ok and mortar_ok and surface_ok and hf_ok and frame['pass'] and calibration_ok and projection_ok
    passed=v2_hard_gates_pass and legacy_step_ok
    print(f'[STYLE2_DETAILS] v2_hard_gates_pass={v2_hard_gates_pass} retained_legacy_step_ok={legacy_step_ok}')
    report=dict(scope='style2_three_samples',materials=results,hf=energies,hf_spread=spreads,frame=frame,projection=projection,legacy_step_ok=legacy_step_ok,v2_hard_gates_pass=v2_hard_gates_pass,albedo_calibration_ok=calibration_ok,pass_gate=passed)
    (ROOT/'measurement.json').write_text(json.dumps(report,indent=2)+'\n')
    print(f'[STYLE_SAMPLE_GATE] textures_ok={surface_ok} hf_ok={hf_ok} pending_visual_confirmation=True')
    print(f"[STYLE_GATE] palette_ok={palette_ok} texel_ok={texel_ok} hf_spread={spreads['480']:.6f} pass=False scope=seven_material_gate_deferred")
    print(f'[STYLE2_GATE] palette_ok={palette_ok} texel_ok={texel_ok} mortar_ok={mortar_ok} hf_ok={hf_ok} pass={passed}')
    return 0 if passed else 1

if __name__=='__main__': sys.exit(main())
