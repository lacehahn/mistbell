#!/usr/bin/env python3
"""量「画面三个横向带的锐度」——用来验证带状移轴（tilt-shift）有没有真的生效。

为什么这么量：移轴的效果是「上下带模糊、中间带清晰」。肉眼说「好像模糊了」不可靠，
所以用高频能量：相邻像素亮度差的绝对值均值。模糊会把这个值压低。

用法：python3 tools/sharp.py Shots/art_runtime.png [更多图...]
输出：top / middle / bottom 三带的高频能量，以及 top/mid、bottom/mid 的比值。
判据（对 art_runtime 这种构图）：
  未开移轴：ratios ≈ 0.8–1.2（三带差不多）
  开了移轴：top/mid < 0.45，bottom/mid < 0.45，mid 自身变化 < 12%
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


def luminance_grid(rows, w, h, bpp, step):
    """抽样成二维亮度网格：行 step、列 step。"""
    grid = []
    for y in range(0, h - step, step):
        line = rows[y]
        grid.append([0.2126 * line[x * bpp] + 0.7152 * line[x * bpp + 1] + 0.0722 * line[x * bpp + 2]
                     for x in range(0, w - step, step)])
    return grid


def band_energy(grid, y0, y1):
    """带内相邻采样点的亮度差均值（横 + 竖），已按采样步长归一。"""
    total, n = 0.0, 0
    for y in range(y0, min(y1, len(grid))):
        line = grid[y]
        nxt = grid[y + 1] if y + 1 < len(grid) else line
        for x in range(len(line)):
            dx = abs(nxt[x] - line[x])
            dy = abs(line[x + 1] - line[x]) if x + 1 < len(line) else 0.0
            total += dx + dy
            n += 2
    return total / max(1, n)


for path in sys.argv[1:]:
    w, h, bpp, rows = read_png(path)
    step = 4
    grid = luminance_grid(rows, w, h, bpp, step)
    g = len(grid)
    top = band_energy(grid, 0, g * 15 // 100)
    mid = band_energy(grid, g * 42 // 100, g * 58 // 100)
    bot = band_energy(grid, g * 85 // 100, g)
    print("%s  %dx%d" % (path, w, h))
    print("   hf  top=%6.2f  middle=%6.2f  bottom=%6.2f" % (top, mid, bot))
    print("   ratio top/mid=%.2f  bottom/mid=%.2f  (移轴生效应 < 0.45)" %
          (top / mid if mid else -1, bot / mid if mid else -1))
