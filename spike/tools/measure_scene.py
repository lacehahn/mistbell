#!/usr/bin/env python3
"""Runtime screenshot checks, no third-party dependencies.
Warm spots: 8-connected components of the exact R-B>35 mask (no area cutoff).
Sky: top 10% of the fixed reveal frame; mean Rec.709 RGB luminance / 255.
Snow: exact HSV S/V thresholds over the entire lower half, no exclusions.
"""
import sys
from pathlib import Path
from texstat import read_png

def measure(path):
    w,h,bpp,rows=read_png(path)
    warm=bytearray(w*h)
    magenta=snow=0
    sky=0.0
    sky_h=max(1,h//10)
    for y,row in enumerate(rows):
        for x in range(w):
            r,g,b=row[x*bpp:x*bpp+3]
            magenta+=r>150 and b>150 and g<100
            warm[y*w+x]=r-b>35
            hi,lo=max(r,g,b),min(r,g,b)
            if y>=h//2:
                snow+=hi>0.72*255 and (hi-lo)<.18*hi
            if y<sky_h:
                sky+=(.2126*r+.7152*g+.0722*b)/255
    count=0
    for i in range(w*h):
        if not warm[i]: continue
        count+=1
        warm[i]=0
        stack=[i]
        while stack:
            p=stack.pop(); y,x=divmod(p,w)
            for dy in (-1,0,1):
                for dx in (-1,0,1):
                    xx,yy=x+dx,y+dy
                    if 0<=xx<w and 0<=yy<h:
                        q=yy*w+xx
                        if warm[q]: warm[q]=0; stack.append(q)
    ratio=snow/(w*(h-h//2)); mean=sky/(w*sky_h)
    print(f'[SCENE] magenta_px={magenta} warm_spots={count} snow_ratio={ratio:.4f} sky_mean={mean:.4f}')
    print(Path(path).resolve())
    return magenta==0 and count>=6 and ratio>=.5 and .55<=mean<=.85

if __name__=='__main__':
    sys.exit(0 if measure(sys.argv[1] if len(sys.argv)>1 else 'spike/project/Shots/art_runtime.png') else 1)
