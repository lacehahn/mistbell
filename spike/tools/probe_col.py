#!/usr/bin/env python3
"""打印指定列在地平线附近的 RGB 逐行值，用来判断 edge.py 的大跳变到底是什么边。

用法：python3 tools/probe_col.py Shots/x.png 480 735 800 [--step 3]
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


path = sys.argv[1]
xs = [int(v) for v in sys.argv[2].split(",")]
y0, y1 = int(sys.argv[3]), int(sys.argv[4])
step = int(sys.argv[5]) if len(sys.argv) > 5 else 3
w, h, bpp, rows = read_png(path)
for x in xs:
    print("=== x=%d (%s)" % (x, path))
    prev = None
    for y in range(y0, min(y1, h), step):
        r, g, b = rows[y][x * bpp], rows[y][x * bpp + 1], rows[y][x * bpp + 2]
        L = 0.2126 * r + 0.7152 * g + 0.0722 * b
        d = "" if prev is None else "  dL=%6.1f" % (L - prev)
        print("  y=%4d rgb=(%3d,%3d,%3d) L=%6.1f%s" % (y, r, g, b, L, d))
        prev = L
