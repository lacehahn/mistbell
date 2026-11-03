"""Recompute fixed projected surface ROIs from the in-scene PNG (no image editing).
Run after -demo-altar; ROI definitions live in PlayerAltar.cs, not selected after capture.
"""
import json, re
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
log=(ROOT/'spike/logs/altar_model2_closeup.log').read_text()
im=Image.open(ROOT/'spike/project/Shots/altar_closeup.png').convert('RGB')
def linear(n):
    s=n/255
    return s/12.92 if s<=.04045 else ((s+.055)/1.055)**2.4
rois={}
for name,x,y,w,h in re.findall(r'\[ALTAR_ROI\] (\S+) x=([\d.]+) y=([\d.]+) w=([\d.]+) h=([\d.]+)',log):
    x,y,w,h=map(float,(x,y,w,h))
    x0,y0,x1,y1=map(round,(x,y,x+w,y+h))
    values=[]
    for py in range(y0,y1):
        for px in range(x0,x1):
            r,g,b=map(linear,im.getpixel((px,im.height-1-py)))
            values.append(.2126*r+.7152*g+.0722*b)
    rois[name]={'xywh_bottom_left':[x,y,w,h],'pixels':len(values),'luma':sum(values)/len(values)}
def mean(prefix):
    v=[r['luma'] for k,r in rois.items() if k.startswith(prefix)]
    return sum(v)/len(v)
metrics={k+'_luma':mean(k) for k in ('cloth','trim','body','niche')}
metrics['cloth_over_body']=metrics['cloth_luma']/metrics['body_luma']
metrics['fold_contrast']=abs(mean('peak')/mean('valley')-1)
gates={'cloth':metrics['cloth_luma']<=.22,'trim':metrics['trim_luma']<=.42,
       'niche':.09<=metrics['niche_luma']<=.13,'ratio':metrics['cloth_over_body']<=1.25,'folds':.10<=metrics['fold_contrast']<=.15}
report={'space':'sRGB PNG decoded to linear Rec.709','metrics':metrics,'gates':gates,'rois':rois}
out=ROOT/'spike/artifacts/altar/altar_v2_luma.json'
out.write_text(json.dumps(report,indent=2))
print('[ALTAR_MODEL2] '+' / '.join(f'{k}={v:.5f}' for k,v in metrics.items()))
print('[ALTAR_LUMA_GATE] '+json.dumps(gates))
raise SystemExit(0 if all(gates.values()) else 1)
