#!/usr/bin/env python3
"""Fail closed on missing measurements, terminal gates, screenshots, or 3D regression evidence."""
from pathlib import Path
import re
import struct

root = Path(__file__).resolve().parents[2]
logs = root / 'spike/logs'
out = root / 'spike/artifacts/climb2d'

def require(condition, message):
    if not condition:
        raise SystemExit('FAIL: ' + message)

def clean(name):
    text = (logs / name).read_text(errors='replace')
    require(not re.search(r'Exception|\berror CS\d+|\bfailed\b|\bFAIL\b|fps_ok=false', text, re.I), name + ' errors')
    return text

build = (logs / 'climb2d_build.log').read_text(errors='replace')
require(not re.search(r'error CS\d+',build), 'C# compile errors')
require('[CLIMB2D_BUILD] result=Succeeded errors=0' in build, 'build terminal result')
text = clean('climb2d_run.log')
require('[CLIMB2D] ramps=2 rise=240px stairs=48 transition=ok' in text, 'transition terminal result')
require('[CLIMB2D] mode=side resolution=480x270 scale=4 parallax=0.2/0.5/0.8' in text, 'logical framebuffer/parallax')
require(re.search(r'input_audit idle_samples=[1-9]\d* left_samples=[1-9]\d* right_samples=[1-9]\d* idle_frame=0',text), 'walk/stop and left/right input evidence')
measure = re.search(r'measured walk_s=([\d.]+) stairs_s=([\d.]+) total_s=([\d.]+) fade_s=([\d.]+).*support_error_px=([\d.]+)', text)
require(measure is not None, 'missing timing/support measurements')
walk, stairs, total, fade, support = map(float, measure.groups())
require(40 <= total <= 60 and 6 <= stairs <= 8 and .6 <= fade <= .75 and support <= .01, 'timing/support bounds')
require(re.search(r'arrival scene=ArtSpike .* valid=True bad=0', text) is not None, 'actual 3D arrival')
require('deck_support=Terrace' in text and 'ground_error_m=0.000000' in text, 'terrace support')
geometry = re.search(r'geometry ramps=(\d+) rise=([\d.]+)px lengths=([\d.]+)/([\d.]+) angle=([\d.]+) landings=([\d.]+) stairs=(\d+) step=([\d.]+)x([\d.]+) total_stair_rise=([\d.]+)px', text)
require(geometry is not None, 'geometry measurements')
ramps, rise, l1, l2, angle, landing, steps, h, d, sr = map(float, geometry.groups())
require((ramps, rise, l1, l2, landing, steps, h, d, sr) == (2,240,900,900,40,48,8,10,384) and 6 <= angle <= 10, 'route geometry')
for name in ('start','ramp_mid','turn','stairs_mid','before_transition'):
    path = out / f'climb2d_{name}.png'
    data = path.read_bytes()
    require(data[:8] == b'\x89PNG\r\n\x1a\n', str(path)+' PNG signature')
    require(struct.unpack('>II',data[16:24]) == (1920,1080), str(path)+' dimensions')
    require(re.search(r'\[CLIMB2D_SHOT\] name='+name+r' size=1920x1080',text) is not None, name+' screenshot capture evidence')
    print(f'SCREENSHOT {path.name} 1920x1080 bytes={len(data)}')
walktext = clean('climb2d_walk.log')
require(re.search(r'\[PLAY\].*fps_ok=true',walktext), 'walk FPS terminal result')
require(re.search(r'\[CLIMB_WALK6\].*bad=0',walktext), 'walk support terminal result')
stairtext = clean('climb2d_stairs.log')
require('on_deck=true' in stairtext and 'actual_church_overlap=false' in stairtext, '3D stair regression')
camtext = clean('climb2d_camera.log')
require(len(re.findall(r'\[CAM_TEST\] phase=\d .*bad=0',camtext)) == 2, '3D camera regression')
for name in ('climb2d_run.log','climb2d_walk.log','climb2d_stairs.log','climb2d_camera.log'):
    for line in (logs/name).read_text(errors='replace').splitlines():
        if re.match(r'\[(CLIMB2D|PLAY|CLIMB_WALK6|CAM_TEST)\]',line):
            print(line)
print(f'ACCEPT climb2d total_s={total:.3f} stairs_s={stairs:.3f} fade_s={fade:.3f} screenshots=5 bad=0; 3D walk/stairs/camera bad=0')
