"""Recompute royal-door acceptance from fresh build/player/benchmark evidence."""
import hashlib
import json
import re
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
build = (ROOT / 'logs/royal_door_build.log').read_text()
log = (ROOT / 'logs/royal_door_interior.log').read_text()
bench = json.loads((ROOT / 'logs/royal_door_bench.json').read_text())
model = re.search(r'\[ALTAR_MODEL\] tris=(\d+) materials=(\d+).*pass=True', build)
assert model, 'Missing model verification'
tris, materials = map(int, model.groups())
interior = next(row for row in bench['results'] if row['pos'] == 'interior')
ray = next(line for line in log.splitlines() if line.startswith('[ROYAL_DOOR_RAY]'))
cross = next(line for line in log.splitlines() if line.startswith('[ALTAR_CROSS]'))
door = next(line for line in build.splitlines() if line.startswith('[ROYAL_DOOR]'))
checks = {
    'door_geometry': all(s in door for s in ['backing_removed=true', 'door_w=0.8600', 'door_h=2.8600', 'frame_mat=M_InteriorV2Gold', 'frame_width=0.0400', 'projection=0.0200']),
    'colliders_and_sides_unchanged': '[ROYAL_DOOR_INVARIANTS] colliders_identical=true side_arches_identical=true idempotent=true' in build,
    'rendered_geometry_ray': 'hit=Interior_Altar_Model ' in ray and 'pass=true' in ray,
    'native_collision_retained': 'native_hit=Interior_Iconostasis ' in ray,
    'original_camera': bool(re.search(r'camera=\(-2.0000,2.1240,-4.77\d+\)', cross)),
    'demo': bool(re.search(r'\[INT_PASS\].*light=True.*domeLight=True.*ceilingSeen=True.*domeSeen=True.*visible=True.*obstacles=True.*walked=True', log)) and 'entry gates failed' not in log,
    'closeup_3m': '[ROYAL_DOOR_SHOT] distance_m=3.000' in log,
    'budget': tris <= 9500 and materials <= 2 and interior['drawcalls'] <= 270 and interior['fps_avg'] >= 200,
    'build': '[PERF_BUILD] result=Succeeded errors=0' in build,
}
shots = {}
for name in ('interior_inside', 'royal_door_closeup'):
    path = ROOT / f'project/Shots/{name}.png'
    with Image.open(path) as im:
        checks[name] = im.size == (1920, 1080)
        shots[name] = {'size': list(im.size), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
report = {'accepted': all(checks.values()), 'checks': checks, 'door': door, 'ray': ray, 'cross': cross,
          'tris': tris, 'materials': materials, 'interior': interior, 'screenshots': shots,
          'ray_definition': 'Opaque rendered source meshes, excluding transparent light effects and duplicate merged batches; native movement ray is recorded independently and still hits the unchanged wall.'}
output = ROOT / 'artifacts/altar/royal_door_measurements.json'
output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
print(json.dumps(report, ensure_ascii=False, indent=2))
raise SystemExit(0 if report['accepted'] else 1)
