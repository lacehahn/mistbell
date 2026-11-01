#!/usr/bin/env python3
"""对任意矩形区域求平均色 / 标准差 / 饱和度 / 过曝率——用来量「渲染出来的材质是什么色」。

usage: python3 tools/region.py IMAGE x0,y0,x1,y1 [x0,y0,x1,y1 ...]
例：python3 tools/region.py Shots/art_close.png 1400,200,1750,420
输出：mean RGB、HSV 饱和度、亮度 std、R-B 色偏（判断石材是不是偏黄）、clipped%
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


path, rects = sys.argv[1], sys.argv[2:]
w, h, bpp, rows = read_png(path)
print("%s %dx%d" % (path, w, h))
for spec in rects:
    x0, y0, x1, y1 = [int(v) for v in spec.split(",")]
    x0, x1 = max(0, x0), min(w, x1)
    y0, y1 = max(0, y0), min(h, y1)
    n = 0
    sr = sg = sb = 0
    sat = 0.0
    clip = 0
    Ls = []
    for y in range(y0, y1):
        row = rows[y]
        for x in range(x0, x1):
            r, g, b = row[x * bpp], row[x * bpp + 1], row[x * bpp + 2]
            sr += r
            sg += g
            sb += b
            n += 1
            mx, mn = max(r, g, b), min(r, g, b)
            sat += (mx - mn) / mx if mx else 0.0
            if mx >= 250:
                clip += 1
            Ls.append(0.2126 * r + 0.7152 * g + 0.0722 * b)
    mr, mg, mb = sr / n, sg / n, sb / n
    mu = sum(Ls) / n
    std = (sum((v - mu) ** 2 for v in Ls) / n) ** 0.5
    # 暗尾（p5）与中位数（p50）：判断「凹陷/接触阴影有没有真的压暗」，而不是整体变暗/变亮。
    order = sorted(Ls)
    p5 = order[max(0, int(0.05 * (n - 1)))]
    p50 = order[max(0, int(0.50 * (n - 1)))]
    print("   rect %-24s mean=(%3.0f,%3.0f,%3.0f) sat=%.3f R-B=%+5.1f Lstd=%5.1f clipped=%.2f%% p5=%5.1f p50=%5.1f tail=%.3f" %
          (spec, mr, mg, mb, sat / n, mr - mb, std, 100.0 * clip / n, p5, p50, p5 / max(p50, 1e-6)))
