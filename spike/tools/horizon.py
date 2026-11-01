#!/usr/bin/env python3
"""量「天地交界线是否笔直」：逐列找第一条绿色像素所在行，返回左右半幅的落差。
用法：python3 tools/horizon.py Shots/art_runtime.png
判据：落差 < 15 px = 一条平直的地平线（桌面模型感）；> 40 px = 地形有起伏/收边。
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


def green_row(rows, bpp, x):
    """从画面中部往下扫，找第一个「草绿」像素的行号（G 明显高于 R、B）。"""
    for y in range(200, len(rows)):
        r, g, b = rows[y][x * bpp], rows[y][x * bpp + 1], rows[y][x * bpp + 2]
        if g > 60 and g > r + 18 and g > b + 25:
            return y
    return None


w, h, bpp, rows = read_png(sys.argv[1])
cols = [w // 10, w // 4, w // 2, 3 * w // 4, 9 * w // 10]
found = [(x, green_row(rows, bpp, x)) for x in cols]
print(sys.argv[1], w, "x", h)
for x, y in found:
    print("   col x=%4d horizon_row=%s" % (x, y))
ys = [y for _, y in found if y is not None]
if len(ys) >= 2:
    print("   span=%d px  (min=%d max=%d)  → %s" % (
        max(ys) - min(ys), min(ys), max(ys),
        "笔直地平线（问题）" if max(ys) - min(ys) < 15 else "有起伏"))
else:
    print("   没找到地平线（画面里没有草地行）")
