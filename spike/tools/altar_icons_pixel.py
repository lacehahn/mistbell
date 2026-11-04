#!/usr/bin/env python3
"""Draw native 72x110 pixel icons; nearest-neighbour delivery and lossless atlas.

Run from any directory: python3 spike/tools/altar_icons_pixel.py [--verify]
No image synthesis, antialiasing, resampled photographs, or dependencies beyond PIL.
"""
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'project/Assets/Art/Interior/Altar_Icons'
REPORT = ROOT / 'artifacts/altar/altar_icons_pixels.json'
# Existing hues, with only out-of-range darks/ivory clamped into L*25..70.
P = dict(ink='#4E3A2D', wood='#594332', stone='#7A6B5C', gold='#C9A227',
         ivory='#ACA393', red='#A35A4A', cold='#3E4654', shadow='#544538',
         skin='#A88569', robe='#6F4140', blue='#555E6A')


def rgb(value):
    return tuple(bytes.fromhex(value[1:]))


def lightness(c):
    linear = [v / 255 / 12.92 if v / 255 <= .04045 else
              ((v / 255 + .055) / 1.055) ** 2.4 for v in c]
    y = sum(a * b for a, b in zip(linear, (.2126, .7152, .0722)))
    return 116 * (y ** (1 / 3) if y > (6 / 29) ** 3 else
                  y / (3 * (6 / 29) ** 2) + 4 / 29) - 16


def draw_icon(kind):
    im = Image.new('RGB', (72, 110), P[{'left': 'ink', 'mid': 'cold', 'right': 'robe'}[kind]])
    d = ImageDraw.Draw(im)
    def rect(box, c): d.rectangle(box, fill=P[c])
    def poly(points, c): d.polygon(points, fill=P[c])
    def line(points, c, width=1): d.line(points, fill=P[c], width=width)
    def halo(box): d.ellipse(box, outline=P['gold'], width=2)
    if kind == 'left':
        # Thin architectural lines behind a seated, three-quarter Virgin and Child.
        for x in (7, 64): rect((x, 5, x, 102), 'gold')
        halo((14, 7, 43, 35)); halo((40, 28, 59, 47))
        rect((11, 74, 55, 81), 'wood'); rect((15, 81, 19, 103), 'wood')
        rect((49, 81, 53, 103), 'wood')
        poly([(21, 31), (35, 29), (43, 43), (46, 61), (39, 72),
              (49, 94), (51, 101), (16, 101), (21, 79), (15, 62)], 'robe')
        poly([(21, 34), (17, 51), (19, 68), (25, 76), (27, 99),
              (20, 99), (15, 89), (15, 67), (12, 55)], 'red')
        poly([(27, 15), (34, 16), (38, 23), (40, 25), (36, 28),
              (35, 33), (29, 33), (24, 27), (24, 20)], 'skin')
        poly([(24, 13), (32, 12), (37, 17), (29, 17), (26, 25),
              (27, 32), (23, 29), (21, 23)], 'red')
        rect((34, 22, 35, 23), 'ink'); line([(35, 29), (38, 28)], 'ink')
        poly([(45, 33), (51, 32), (54, 36), (54, 42), (50, 46),
              (45, 43), (43, 38)], 'skin')
        rect((50, 36, 51, 37), 'ink')
        poly([(43, 44), (52, 44), (57, 57), (53, 66), (40, 66),
              (34, 60), (39, 51)], 'ivory')
        poly([(43, 49), (45, 59), (40, 65), (36, 61)], 'stone')
        poly([(30, 48), (34, 47), (41, 53), (48, 52), (49, 55),
              (41, 58), (35, 55)], 'skin')
        poly([(44, 64), (49, 65), (47, 74), (44, 76), (41, 74)], 'skin')
        poly([(50, 64), (54, 64), (55, 73), (52, 75), (49, 73)], 'skin')
        line([(29, 69), (32, 82), (38, 94)], 'red', 2)
        line([(37, 76), (41, 85), (46, 94)], 'red', 2)
        rect((17, 103, 56, 105), 'stone')
    elif kind == 'mid':
        # Cold, open negative space makes the cross silhouette distinct at distance.
        poly([(4, 91), (15, 85), (25, 91), (38, 83), (53, 91), (66, 87),
              (69, 105), (4, 105)], 'blue')
        rect((32, 9, 39, 99), 'wood'); rect((13, 28, 58, 34), 'wood')
        rect((33, 10, 34, 97), 'stone'); rect((14, 29, 57, 29), 'stone')
        halo((26, 12, 47, 32))
        poly([(32, 18), (39, 18), (43, 23), (41, 29), (36, 32),
              (31, 28)], 'skin')
        poly([(30, 17), (37, 15), (43, 19), (43, 23), (39, 21),
              (34, 21), (32, 25)], 'ink')
        poly([(31, 33), (39, 33), (43, 46), (41, 60), (32, 60), (29, 45)], 'skin')
        poly([(12, 29), (18, 31), (29, 35), (32, 35), (31, 40),
              (21, 36), (15, 33), (12, 33)], 'skin')
        poly([(39, 35), (43, 35), (53, 31), (59, 29), (59, 33),
              (53, 35), (43, 40)], 'skin')
        line([(34, 40), (33, 48), (36, 51)], 'stone')
        poly([(31, 57), (41, 57), (44, 67), (39, 70), (29, 67)], 'ivory')
        poly([(33, 60), (35, 66), (42, 66), (39, 69), (30, 66)], 'stone')
        poly([(31, 69), (35, 70), (37, 81), (35, 92), (31, 93), (33, 81)], 'skin')
        poly([(37, 70), (41, 69), (41, 80), (36, 94), (32, 95), (36, 81)], 'skin')
        line([(10, 101), (61, 101)], 'gold'); line([(16, 104), (55, 104)], 'gold')
    else:
        # Upright frontal saint, long pale stole and a tall pilgrim staff.
        rect((4, 5, 67, 105), 'red'); rect((8, 9, 63, 102), 'robe')
        halo((22, 7, 48, 33))
        poly([(27, 30), (43, 30), (49, 48), (47, 76), (52, 99),
              (19, 99), (23, 75), (21, 48)], 'cold')
        poly([(25, 37), (29, 36), (27, 79), (24, 96), (20, 96), (24, 69)], 'blue')
        poly([(31, 31), (35, 37), (39, 31), (42, 34), (38, 44),
              (40, 92), (35, 97), (30, 92), (32, 44), (28, 35)], 'ivory')
        line([(35, 46), (35, 86)], 'stone', 2)
        poly([(29, 16), (40, 16), (43, 22), (40, 31), (35, 35),
              (29, 30), (26, 22)], 'skin')
        poly([(28, 15), (34, 13), (41, 15), (43, 21), (39, 18),
              (31, 18), (27, 22)], 'stone')
        poly([(28, 26), (33, 29), (37, 29), (41, 26), (39, 33),
              (35, 36), (30, 32)], 'stone')
        rect((29, 22, 30, 23), 'ink'); rect((38, 22, 39, 23), 'ink')
        line([(34, 23), (33, 27), (35, 27)], 'wood')
        line([(55, 94), (55, 31), (58, 27), (61, 29), (61, 33)], 'wood', 2)
        poly([(43, 44), (46, 43), (49, 52), (55, 49), (57, 50),
              (56, 54), (49, 57), (45, 54)], 'skin')
        poly([(24, 48), (28, 49), (32, 56), (37, 55), (39, 58),
              (33, 61), (28, 57)], 'skin')
        rect((25, 100, 32, 103), 'wood'); rect((39, 100, 46, 103), 'wood')
        rect((17, 105, 55, 106), 'stone')
    return im


def generate():
    OUT.mkdir(parents=True, exist_ok=True)
    atlas = Image.new('RGB', (2048, 2048), P['ink'])
    emission = Image.new('RGB', (2048, 2048), 'black')
    # Preserve every texel of the existing painted atlas in its bottom-left quarter.
    atlas.paste(Image.open(ROOT / 'project/Assets/Art/MaterialForge/Altar_Painted.png').convert('RGB'), (0, 1024))
    for i, kind in enumerate(('left', 'mid', 'right')):
        logical = draw_icon(kind)
        logical.save(OUT / f'icon_{kind}_logical.png')
        delivery = logical.resize((512, 784), Image.Resampling.NEAREST)
        delivery.save(OUT / f'icon_{kind}.png')
        atlas.paste(delivery, (i * 512, 0))
        emission.paste(delivery, (i * 512, 0))
    atlas.save(OUT / 'altar_icons_atlas.png')
    emission.save(OUT / 'altar_icons_emission.png')


def verify():
    rows = []
    grids = []
    for logical, delivery in ((72, 512), (110, 784)):
        labels = Image.new('L', (logical, 1))
        labels.putdata(list(range(logical)))
        expanded = list(labels.resize((delivery, 1), Image.Resampling.NEAREST).getdata())
        edges = [0] + [j for j in range(1, delivery) if expanded[j] != expanded[j-1]] + [delivery]
        error = max(abs(edge-i*delivery/logical) for i,edge in enumerate(edges))
        sizes = [b-a for a,b in zip(edges,edges[1:])]
        assert len(edges)==logical+1 and error<=1 and min(sizes)==7 and max(sizes)==8
        grids.append(dict(logical=logical,delivery=delivery,cell_min=7,cell_max=8,boundary_error_max=error))
    atlas = Image.open(OUT / 'altar_icons_atlas.png').convert('RGB')
    emission = Image.open(OUT / 'altar_icons_emission.png').convert('RGB')
    assert atlas.size == (2048, 2048)
    assert emission.size == atlas.size
    assert emission.crop((0, 784, 2048, 2048)).getbbox() is None
    assert emission.crop((1536, 0, 2048, 784)).getbbox() is None
    original = Image.open(ROOT / 'project/Assets/Art/MaterialForge/Altar_Painted.png').convert('RGB')
    assert atlas.crop((0, 1024, 1024, 2048)).tobytes() == original.tobytes()
    for i, kind in enumerate(('left', 'mid', 'right')):
        path = OUT / f'icon_{kind}.png'
        im = Image.open(path).convert('RGB')
        source = Image.open(OUT / f'icon_{kind}_logical.png').convert('RGB')
        assert im.size == (512, 784) and source.size == (72, 110)
        grid = im.tobytes() == source.resize(im.size, Image.Resampling.NEAREST).tobytes()
        assert grid and source.tobytes() == draw_icon(kind).tobytes()
        assert atlas.crop((i * 512, 0, (i + 1) * 512, 784)).tobytes() == im.tobytes()
        assert emission.crop((i * 512, 0, (i + 1) * 512, 784)).tobytes() == im.tobytes()
        colors = im.getcolors(im.width * im.height)
        lo, hi = min(lightness(c) for _, c in colors), max(lightness(c) for _, c in colors)
        gold = sum(n for n, c in colors if c == rgb(P['gold'])) / (im.width * im.height) * 100
        assert len(colors) <= 12 and lo >= 25 and hi <= 70 and gold <= 15
        row = dict(icon=kind, palette_max=len(colors), luma_min=lo, luma_max=hi,
                   gold_pct=gold, grid_ok=grid, sha256=hashlib.sha256(path.read_bytes()).hexdigest())
        rows.append(row)
        print('[ALTAR_ICONS] icon=%s px_grid=72x110 palette_max=%d luma_min=%.3f luma_max=%.3f gold_pct=%.3f grid_ok=true space=CIELAB_D65' %
              (kind, len(colors), lo, hi, gold))
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(dict(icons=rows, palette=P, grid='PIL NEAREST 72x110 -> 512x784; cells 7 or 8 texels',grid_axes=grids,
        atlas='2048x2048: original1024 bottom-left unchanged, icons top row'), indent=2) + '\n')


if __name__ == '__main__':
    if '--verify' not in sys.argv:
        generate()
    verify()
