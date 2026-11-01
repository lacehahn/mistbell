#!/usr/bin/env python3
"""量一张材质贴图的「像不像石砌」——给石材重做当尺子。

为什么不用肉眼：近景石墙的问题可以被拆成三个可量的量——
  ① 色调偏黄/过饱和  → saturation 均值（HSV 的 S）
  ② 块与块之间没有色调差（看着像贴纸网格）→ 粗尺度（32×32 单元）亮度标准差 coarseStd
  ③ 灰缝占比  → edge%（相邻像素亮度差 > 18 的像素比例）
用法：python3 tools/texstat.py spike/project/Assets/Art/Textures/stone_ashlar.png [更多...]
"""
import sys
import struct
import zlib


def read_png(path):
    data = open(path, "rb").read()
    pos, w, h, ct = 8, None, None, 0
    idat = b""
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        tag = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + ln]
        if tag == b"IHDR":
            w, h, _, ct = struct.unpack(">IIBB", chunk[:10])
        elif tag == b"IDAT":
            idat += chunk
        pos += 12 + ln
    raw = zlib.decompress(idat)
    bpp = {0: 1, 2: 3, 4: 2, 6: 4}[ct]
    stride = w * bpp
    rows, prev, i = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[i]
        i += 1
        line = bytearray(raw[i:i + stride])
        i += stride
        if f == 1:
            for x in range(bpp, stride):
                line[x] = (line[x] + line[x - bpp]) & 0xFF
        elif f == 2:
            for x in range(stride):
                line[x] = (line[x] + prev[x]) & 0xFF
        elif f == 3:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                line[x] = (line[x] + ((a + prev[x]) >> 1)) & 0xFF
        elif f == 4:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                b = prev[x]
                c = prev[x - bpp] if x >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 0xFF
        rows.append(line)
        prev = line
    return w, h, bpp, rows


for path in sys.argv[1:]:
    w, h, bpp, rows = read_png(path)
    L = [[0.2126 * rows[y][x * bpp] + 0.7152 * rows[y][x * bpp + 1] + 0.0722 * rows[y][x * bpp + 2]
          for x in range(w)] for y in range(h)]
    rs = gs = bs = 0
    sat = 0.0
    n = 0
    edges = 0
    for y in range(h):
        row = rows[y]
        for x in range(w):
            r, g, b = row[x * bpp], row[x * bpp + 1], row[x * bpp + 2]
            rs += r
            gs += g
            bs += b
            mx, mn = max(r, g, b), min(r, g, b)
            sat += (mx - mn) / mx if mx else 0.0
            n += 1
            if x + 1 < w and abs(L[y][x + 1] - L[y][x]) > 18:
                edges += 1
            if y + 1 < h and abs(L[y + 1][x] - L[y][x]) > 18:
                edges += 1
    cell = max(8, w // 4)
    means = []
    for cy in range(0, h - cell + 1, cell):
        for cx in range(0, w - cell + 1, cell):
            s = 0.0
            for y in range(cy, cy + cell):
                s += sum(L[y][cx:cx + cell])
            means.append(s / (cell * cell))
    mu = sum(means) / len(means)
    coarse = (sum((v - mu) ** 2 for v in means) / len(means)) ** 0.5
    print("%s  %dx%d" % (path, w, h))
    print("   meanRGB=(%3d,%3d,%3d)  saturation=%.3f  coarseStd(32px)=%.2f  edge%%=%.1f" %
          (rs // n, gs // n, bs // n, sat / n, coarse, 100.0 * edges / (2 * n)))
