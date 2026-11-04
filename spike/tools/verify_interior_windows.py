"""Independently sample original PNGs through visible-geometry masks; fail closed.

Display Rec.709 PNG luma is distinct from rib base-material relative contrast.
Run after the build, -demo-enter-church screenshot pass, and benchmark pass.
"""
import hashlib
import colorsys
import struct
import json
import math
import re
from pathlib import Path
from PIL import Image, ImageStat

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/interior_windows'
OUT.mkdir(parents=True, exist_ok=True)
errors = []

def log_path(suffix):
    candidates = [ROOT / f'logs/{prefix}_{suffix}' for prefix in ('interior_windows', 'windows')]
    return next((p for p in candidates if p.exists()), candidates[0])

def read(path):
    try:
        return path.read_text()
    except OSError as exc:
        errors.append(str(exc))
        return ''

build_path = log_path('build.log')
runtime_path = log_path('interior.log')
bench_path = log_path('bench.json')
build, runtime = read(build_path), read(runtime_path)

def lines(tag, text=runtime):
    return re.findall(r'^\[' + re.escape(tag) + r'\].*$', text, re.M)

def fields(line):
    return dict(re.findall(r'(\w+)=([^\s]+)', line))

def latest(tag, text=runtime):
    found = lines(tag, text)
    return fields(found[-1]) if found else {}

def number(row, key):
    try:
        return float(row[key])
    except (KeyError, ValueError):
        return math.nan

COLORS = {'nave': (255, 0, 0), 'dome': (0, 0, 255), 'plaster': (0, 255, 0),
          'rib_light': (255, 255, 0), 'rib_shadow': (255, 0, 255), 'gold_ring': (0, 255, 255)}

def luma(rgb):
    return sum(c * k / 255 for c, k in zip(rgb, (.2126, .7152, .0722)))

def lstar(rgb):
    def linear(c):
        return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4
    y = sum(linear(c / 255) * k for c, k in zip(rgb, (.2126, .7152, .0722)))
    return 116 * y ** (1 / 3) - 16 if y > 216 / 24389 else (24389 / 27) * y

shots, measurements, opened = {}, {}, {}
for stem in ('interior_inside', 'interior_lookup', 'windows_closeup',
             'interior_windows_inside_mask', 'interior_windows_lookup_mask'):
    path = ROOT / f'project/Shots/{stem}.png'
    try:
        image = Image.open(path).convert('RGB')
        opened[stem] = image
        shots[stem] = {'size': list(image.size), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                       'mtime': path.stat().st_mtime}
    except (OSError, ValueError) as exc:
        errors.append(str(exc))

for view in ('inside', 'lookup'):
    frame = opened.get(f'interior_{view}')
    mask = opened.get(f'interior_windows_{view}_mask')
    if frame is None or mask is None or mask.size != (480, 270):
        continue
    width, height = frame.size
    sums = {k: 0. for k in COLORS}
    counts = {k: 0 for k in COLORS}
    lstars = {k: 0. for k in COLORS}
    reverse = {v: k for k, v in COLORS.items()}
    for y in range(270):
        for x in range(480):
            key = reverse.get(mask.getpixel((x, y)))
            if key is None:
                continue
            # Unity samples pixel centers in bottom-left coordinates; PNG rows are top-left.
            sx = (2*x+1)*width//(2*480)
            sy = height-1-(2*(269-y)+1)*height//(2*270)
            rgb = frame.getpixel((sx, sy))
            sums[key] += luma(rgb)
            lstars[key] += lstar(rgb)
            counts[key] += 1
    measurements[view] = {key: {'pixels': counts[key], 'display_luma': sums[key] / counts[key] if counts[key] else None,
                               'cielab_lstar': lstars[key] / counts[key] if counts[key] else None}
                          for key in COLORS}

balance = {}
if 'interior_inside' in opened:
    image = opened['interior_inside']
    def linear(c):
        return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4
    for key, roi in {'left': (.06, .35, .20, .62), 'right': (.80, .35, .94, .62), 'nave': (.36, .72, .64, .92)}.items():
        crop = tuple(int(v * n) for v, n in zip(roi, (image.width, image.height, image.width, image.height)))
        mean = ImageStat.Stat(image.crop(crop)).mean
        balance[key] = sum(linear(c / 255) * k for c, k in zip(mean, (.2126, .7152, .0722)))
    balance['ratio'] = balance['left'] / balance['right'] if balance['right'] else None

# A review artifact, with explicit crop coordinates; no claimed automatic judgement of legible rows.
if 'interior_inside' in opened:
    frame = opened['interior_inside']
    box = tuple(int(v * n) for v, n in zip((.02, .30, .25, .70), (frame.width, frame.height, frame.width, frame.height)))
    wall = frame.crop(box).resize((480, 270), Image.Resampling.LANCZOS)
    wall.save(OUT / 'wall_crop_480x270.png')
    wall.resize((1920, 1080), Image.Resampling.NEAREST).save(OUT / 'wall_crop_enlarged.png')
else:
    box = None

try:
    bench = json.loads(read(bench_path))
except ValueError as exc:
    errors.append(str(exc))
    bench = {}
interior = next((v for v in bench.get('results', []) if v.get('pos') == 'interior'), {})
windows, ribs, tone = latest('WINDOWS'), latest('DOME_RIB'), latest('WALL_TONE', build)
if not tone:
    tone = latest('WALL_TONE')
protected = latest('WINDOWS_PROTECTED', build)
if not protected:
    protected = latest('WINDOWS_PROTECTED')
pass_row = latest('INT_PASS')

def sample(view, key):
    value = measurements.get(view, {}).get(key, {})
    return value.get('display_luma') if value.get('pixels', 0) > 0 else math.nan

def between(value, lo, hi):
    return value is not None and lo <= value <= hi

agreement = True
for view in ('inside', 'lookup'):
    rows = [fields(s) for s in lines('WINDOWS_SAMPLES') if fields(s).get('view') == view]
    if not rows or view not in measurements:
        agreement = False
        continue
    row = rows[-1]
    for key in COLORS:
        agreement &= number(row, f'{key}_pixels') == measurements[view][key]['pixels']
    for key in ('nave', 'dome', 'plaster', 'gold_ring'):
        if measurements[view][key]['pixels']:
            agreement &= abs(number(row, f'{key}_luma') - sample(view, key)) <= .00001
    for key in ('dome', 'gold_ring'):
        if measurements[view][key]['pixels']:
            agreement &= abs(number(row, f'{key}_lstar') - measurements[view][key]['cielab_lstar']) <= .0001

# Parse the shipped shader expressions rather than trusting the WALL_TONE sentence.
shader_path = ROOT / 'project/Assets/Art/Shaders/InteriorWallBlocks.shader'
forward_path = shader_path.with_name('InteriorWallForward.hlsl')
shader_source = read(shader_path)
forward_source = shader_source if 'float tone =' in shader_source else read(forward_path)
wall_cpu = {'shader_sha256': hashlib.sha256(shader_source.encode()).hexdigest(),
            'forward_sha256': hashlib.sha256(forward_source.encode()).hexdigest()}
wall_source_ok = False
try:
    numeric = r'([0-9.]+)'
    base_tone, count, step = map(float, re.search(r'float tone\s*=\s*' + numeric + r'\s*\+\s*floor\(hash\s*\*\s*' + numeric + r'\)\s*\*\s*' + numeric, forward_source).groups())
    grout_scalar, half_width = map(float, re.search(r'float factor\s*=\s*tone\s*\*\s*lerp\(\s*' + numeric + r'\s*,\s*1\.0\s*,\s*smoothstep\(\s*' + numeric + r'-aa', forward_source).groups())
    base_material = read(ROOT / 'project/Assets/Settings/M_IndoorShell.mat')
    base_rgb = tuple(map(float, re.search(r'_BaseColor: \{r: ([^,]+), g: ([^,]+), b: ([^,]+),', base_material).groups()))
    tones = [base_tone + i * step for i in range(int(count))]
    rgb = [[c * tone for c in base_rgb] for tone in tones]
    base_hue = colorsys.rgb_to_hsv(*base_rgb)[0]
    hues = [colorsys.rgb_to_hsv(*c)[0] for c in rgb]
    hue_shift = max(min(abs(h - base_hue), 1 - abs(h - base_hue)) * 360 for h in hues)
    wall_cpu.update({'base_rgb': base_rgb, 'tone_scalars': tones, 'variant_rgb': rgb,
                     'hue_shift_degrees': hue_shift, 'grout_drop': 1 - grout_scalar,
                     'grout_width_metres': 2 * half_width,
                     'note': 'CPU evaluates the scalar expression parsed from the actual fragment shader; visible row legibility remains a screenshot review.'})
    wall_source_ok = (int(count) == count == 3 and len(tones) == 3 and
                      all(abs(a - b) < 1e-6 for a, b in zip(tones, (.92, 1, 1.08))) and
                      hue_shift <= 2 and .18 <= 1 - grout_scalar <= .22 + 1e-8 and .03 <= 2 * half_width <= .05 and
                      'surfaceData.albedo *= factor;' in forward_source and 'surfaceData.emission *= factor;' in forward_source and
                      ('float tone =' in shader_source or '#include "InteriorWallForward.hlsl"' in shader_source))
except (AttributeError, ValueError, ZeroDivisionError) as exc:
    wall_cpu['error'] = str(exc)

# These generated uncompressed Unity meshes use Float32 positions in stream zero.
# Read the actual serialized vertex bytes, not builder source constants or log claims.
geometry = {}
def mesh_vertices(name):
    source = read(ROOT / f'project/Assets/Art/Church/{name}.asset')
    count = int(re.search(r'm_VertexCount: (\d+)', source).group(1))
    size = int(re.search(r'm_DataSize: (\d+)', source).group(1))
    data = bytes.fromhex(re.search(r'_typelessdata: ([0-9a-fA-F]+)', source).group(1))
    channels = re.findall(r'- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)', source)
    if not count or size != len(data) or size % count or channels[0] != ('0', '0', '0', '3') or any(int(c[0]) != 0 for c in channels if int(c[3])):
        raise ValueError(f'Unsupported vertex layout in {name}')
    stride = size // count
    return [struct.unpack_from('<fff', data, i * stride) for i in range(count)]
geometry_ok = False
try:
    gold = mesh_vertices('Interior_OculusGoldRing')
    radii = [math.hypot(x + 2, z) for x, y, z in gold]
    geometry['gold_ring'] = {'inner_radius': min(radii), 'outer_radius': max(radii), 'width': max(radii) - min(radii)}
    reveal = mesh_vertices('Interior_WindowsNaveReveal_All')
    reveal_depth = max(p[2] for p in reveal) - min(p[2] for p in reveal)
    geometry['reveal_depth'] = reveal_depth
    geometry['nave_glass'] = []
    geometry['mullions'] = []
    for side in range(2):
        glass = mesh_vertices(f'Interior_WindowsNaveGlass_{side}')
        z = [p[2] for p in glass]
        geometry['nave_glass'].append({'side': side, 'z_min': min(z), 'z_max': max(z)})
        for bar in range(3):
            vertices = mesh_vertices(f'Interior_WindowsNaveMullion_{side}_{bar}')
            extent = [max(p[a] for p in vertices) - min(p[a] for p in vertices) for a in range(3)]
            geometry['mullions'].append({'side': side, 'bar': bar, 'extent': extent, 'width': extent[0 if bar == 0 else 1]})
    geometry_ok = (abs(geometry['gold_ring']['width'] - .06) < .00001 and abs(reveal_depth - .18) < .00001 and
                   all(abs(v['width'] - .05) < .00001 for v in geometry['mullions']) and
                   all(abs(v['z_max'] - v['z_min']) < .00001 and abs(v['z_min'] - max(p[2] for p in reveal)) < .00001 for v in geometry['nave_glass']))
except (AttributeError, ValueError, IndexError, struct.error) as exc:
    geometry['error'] = str(exc)

try:
    snapshots = json.loads(read(OUT / 'protected_digests.json'))
except ValueError as exc:
    snapshots = {'error': str(exc)}
snapshot_ok = (bool(re.fullmatch(r'[0-9a-f]{64}', snapshots.get('protected_before', ''))) and
               snapshots.get('protected_before') == snapshots.get('protected_after'))
red_panel = latest('RED_PANEL', build) or latest('RED_PANEL')
build_time = build_path.stat().st_mtime if build_path.exists() else math.inf
checks = {
    'nave_png_luma': between(sample('inside', 'nave'), .55, .70),
    'dome_png_luma': between(sample('lookup', 'dome'), .62, .78),
    'gold_ring_cielab_lstar': between(measurements.get('lookup', {}).get('gold_ring', {}).get('cielab_lstar'), 74, 80),
    'dome_cielab_lstar': between(measurements.get('lookup', {}).get('dome', {}).get('cielab_lstar'), 70, 78),
    'plaster_png_luma': between(sample('lookup', 'plaster'), .22, 1),
    'mullions': number(windows, 'mullions') == 6,
    'rib_material_light': between(number(ribs, 'rib_light_gain'), .10 - .00001, .12 + .00001),
    'rib_material_shadow': between(number(ribs, 'rib_shadow_drop'), .08 - .00001, .10 + .00001),
    'rib_material_counts': number(ribs, 'light_materials') == 12 and number(ribs, 'shadow_materials') == 12,
    'visible_ribs': all(measurements.get('lookup', {}).get(k, {}).get('pixels', 0) > 0 for k in ('rib_light', 'rib_shadow')),
    'wall_materials': number(tone, 'block_variants') == 3 and number(tone, 'contrast') >= .08 - .00001 and number(tone, 'hue_shift') <= 2,
    'red_panel_dimensions_color': red_panel.get('color') == '#A35A4A' and between(number(red_panel, 'width'), .001, .9005) and between(number(red_panel, 'height'), .001, 1.6005),
    'wall_shader_cpu': wall_source_ok,
    'mesh_geometry': geometry_ok,
    'protected_snapshot_files': snapshot_ok,
    'runtime_mesh_queries_clean': not re.search(r'(?:non-degenerate triangle|\[Physics.PhysX\].*ERROR)', runtime, re.I),
    'demo': bool(pass_row) and all(v == 'True' for v in pass_row.values() if v in ('True', 'False')) and
            all(pass_row.get(k) == 'True' for k in ('light', 'domeLight', 'ceilingSeen', 'domeSeen', 'visible', 'obstacles', 'walked')) and
            number(pass_row, 'lamps') == 18 and 'entry gates failed' not in runtime,
    'build': '[PERF_BUILD] result=Succeeded errors=0' in build,
    'protected': protected.get('identical') == 'true' and protected.get('idempotent') == 'true',
    'budget': interior.get('drawcalls', math.inf) <= 300 and interior.get('fps_avg', 0) >= 200,
    'fresh_benchmark': bench_path.exists() and bench_path.stat().st_mtime > build_time,
    'resolution': all(shots.get(k, {}).get('size') == [1920, 1080] for k in ('interior_inside', 'interior_lookup', 'windows_closeup')),
    'masks': all(shots.get(f'interior_windows_{v}_mask', {}).get('size') == [480, 270] for v in ('inside', 'lookup')),
    'fresh_shots': len(shots) == 5 and all(v['mtime'] > build_time for v in shots.values()),
    'png_runtime_agreement': bool(agreement) and abs(number(windows, 'nave_win_luma') - sample('inside', 'nave')) <= .00001 and
                             abs(number(windows, 'dome_win_luma') - sample('lookup', 'dome')) <= .00001 and
                             abs(number(ribs, 'plaster_luma') - sample('lookup', 'plaster')) <= .00001,
    'inputs_readable': not errors,
}
report = {'accepted': all(checks.values()), 'checks': checks, 'measurements': measurements,
          'material_metrics': {'rib': ribs, 'wall': tone}, 'wall_shader_cpu': wall_cpu, 'mesh_geometry': geometry, 'protected_snapshots': snapshots, 'balance_reference': balance, 'interior': interior, 'shots': shots,
          'wall_crop_source_box': box, 'wall_crop_note': 'Visual review required: count block rows in enlarged crop.',
          'metric_note': 'PNG luma uses display Rec.709; CIELAB L* is averaged per pixel after sRGB linearization (D65); rib gains/drops compare base material Rec.709. Nave uses inside, drum windows and gold ring use lookup.',
          'logs': {'build': str(build_path), 'runtime': str(runtime_path), 'benchmark': str(bench_path)}, 'errors': errors}
(OUT / 'measurements.json').write_text(json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + '\n')
print(json.dumps({'accepted': report['accepted'], 'checks': checks, 'measurements': measurements, 'errors': errors}, ensure_ascii=False, indent=2))
raise SystemExit(0 if report['accepted'] else 1)
