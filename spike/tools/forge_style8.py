#!/usr/bin/env python3
"""Roof-only RGB offset; immutable V7 pixel layout and seven encoded PNGs."""
import hashlib
import json
from pathlib import Path
from PIL import Image
from measure_style import ROOT

BASELINE = json.loads(Path(__file__).with_name('style8_frozen.json').read_text())

def frozen():
    return {n: hashlib.sha256((ROOT/(n+'_Albedo.png')).read_bytes()).hexdigest() == sha
            for n, sha in BASELINE['frozen'].items()}

def structure_ok(im):
    if list(im.size) != BASELINE['size']:
        return False
    pixels = list(im.getdata())
    if not set(pixels) <= set(map(tuple, BASELINE['new_palette'])):
        return False
    original = bytes(v-6 for p in pixels for v in p)
    return hashlib.sha256(original).hexdigest() == BASELINE['roof_before_rgb_sha']

def generate():
    assert all(frozen().values()), frozen()
    path = ROOT/'Roof_Slate_Albedo.png'
    im = Image.open(path).convert('RGB')
    if structure_ok(im):
        print('[STYLE8_GENERATED] already current; no PNG rewritten')
        return
    assert hashlib.sha256(path.read_bytes()).hexdigest() == BASELINE['roof_before_sha']
    im.putdata([tuple(v+6 for v in p) for p in im.getdata()])
    assert structure_ok(im)
    im.save(path)
    assert all(frozen().values()), frozen()
    print('[STYLE8_GENERATED] Roof_Slate RGB+6 only; seven frozen SHA verified')

if __name__ == '__main__':
    generate()
