"""Recompute original-camera cross evidence; exit 1 when acceptance fails."""
import hashlib
import json
import math
import re
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
log = (ROOT / 'logs/altar_model3_interior.log').read_text()
audit = (ROOT / 'logs/altar_model3_audit.log').read_text()
shot = ROOT / 'project/Shots/interior_inside.png'
frame = Image.open(shot).convert('RGB')
roi = re.search(r'\[ALTAR_CROSS_ROI\] x0=([\d.]+) y0=([\d.]+) x1=([\d.]+) y1=([\d.]+).*image_w=(\d+) image_h=(\d+)', log)
if roi is None:
    raise SystemExit('Missing original-camera ROI; rerun the built player demo.')
x0, y0, x1, y1 = map(float, roi.groups()[:4])
assert frame.size == tuple(map(int, roi.groups()[4:])), 'Screenshot resolution does not match log'
# Keep only whole pixel centres inside the projected rod, excluding bevel edges.
left, right = math.ceil(x0), math.floor(x1)
top, bottom = frame.height - math.floor(y1), frame.height - math.ceil(y0)
width = right - left
assert width > 0 and bottom > top


def linear(v):
    v /= 255
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4


def luminance(box):
    pixels = list(frame.crop(box).getdata())
    return sum(.2126 * linear(r) + .7152 * linear(g) + .0722 * linear(b)
               for r, g, b in pixels) / len(pixels)


rod = luminance((left, top, right, bottom))
background = (luminance((left - width, top, left, bottom)) +
              luminance((right, top, right + width, bottom))) / 2
contrast = rod / background - 1
runtime_line = next(line for line in log.splitlines() if line.startswith('[ALTAR_CROSS]'))
ray_pass = 'visible_from_cam=true' in runtime_line
assert 'hit=Interior_Altar_Model' in runtime_line if ray_pass else True
demo_pass = bool(re.search(r'\[INT_PASS\].*light=True.*domeLight=True.*ceilingSeen=True.*domeSeen=True.*visible=True.*obstacles=True.*walked=True', log))
metrics = json.loads((ROOT / 'export/altar_metrics.json').read_text())
bench = json.loads((ROOT / 'logs/altar_model3_bench.json').read_text())
interior = next(row for row in bench['results'] if row['pos'] == 'interior')
budget_pass = (metrics['tris'] <= 9000 and metrics['materials'] <= 2 and
               interior['drawcalls'] <= 260 and interior['fps_avg'] >= 200)
pixel_pass = 6 <= width <= 14 and contrast >= .15
report = {
    'accepted': ray_pass and pixel_pass and demo_pass and budget_pass,
    'ray_pass': ray_pass, 'runtime_ray': runtime_line,
    'sweep_total_including_baseline': audit.count('[ALTAR_CROSS]'),
    'sweep_visible_count': audit.count('visible_from_cam=true'),
    'pixel_roi_top_left_xyxy': [left, top, right, bottom],
    'rod_width_px': width, 'rod_luma_linear_rec709': rod,
    'background_luma_linear_rec709': background, 'contrast': contrast,
    'pixel_pass': pixel_pass, 'demo_pass': demo_pass, 'budget_pass': budget_pass,
    'model_tris': metrics['tris'], 'model_materials': metrics['materials'],
    'interior_bench': interior,
    'screenshot_sha256': hashlib.sha256(shot.read_bytes()).hexdigest(),
    'note': 'ROI is the geometric projection of the upper cross rod; occlusion means it samples the wall, not the cross.'
}
output = ROOT / 'artifacts/altar/altar_v3_measurements.json'
output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
print(json.dumps(report, ensure_ascii=False, indent=2))
raise SystemExit(0 if report['accepted'] else 1)
