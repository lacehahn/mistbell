#!/usr/bin/env python3
"""用「反照率归零」差分图做灌木掩码，量灌木本体（不含背景草/路）的真实统计。

为什么需要：`foliage.py` 的矩形里混了背景草地与石板路。06:05 的黑反照率探针证明——
把灌木反照率置 0 后 art_ground 那个矩形只暗了 40%（101.2→60.6），说明矩形里多数像素不是灌木。
所以用 `art_ground(黑) vs art_ground(正常)` 的逐像素差做掩码，得到「只属于灌木」的像素集合。

用法：python3 spike/scratch/bushmask.py spike/scratch/probe/bush_black_ground.png spike/project/Shots/art_ground.png
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


def luma(r, g, b):
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def main():
    black_path, lit_path = sys.argv[1], sys.argv[2]
    w, h, bpp, A = read_png(black_path)
    w2, h2, bpp2, B = read_png(lit_path)
    assert (w, h, bpp) == (w2, h2, bpp2)
    mask = bytearray(w * h)
    for y in range(h):
        ra, rb = A[y], B[y]
        for x in range(w):
            i = x * bpp
            if abs(luma(rb[i], rb[i + 1], rb[i + 2]) - luma(ra[i], ra[i + 1], ra[i + 2])) > 18:
                mask[y * w + x] = 1
    n = sum(mask)
    print("mask pixels=%d (%.2f%% of frame)" % (n, 100.0 * n / (w * h)))

    # 灌木本体（掩码内）统计
    tot = [0.0, 0.0, 0.0]
    sat = 0.0
    clip = 0
    Ls = []
    for y in range(h):
        rb = B[y]
        for x in range(w):
            if not mask[y * w + x]:
                continue
            i = x * bpp
            r, g, b = rb[i], rb[i + 1], rb[i + 2]
            tot[0] += r
            tot[1] += g
            tot[2] += b
            mx, mn = max(r, g, b), min(r, g, b)
            sat += (mx - mn) / mx if mx else 0.0
            if mx >= 250:
                clip += 1
            Ls.append(luma(r, g, b))
    mr, mg, mb = [v / n for v in tot]
    Ls.sort()
    print("bush-only  mean=(%3.0f,%3.0f,%3.0f) luma=%5.1f sat=%.3f p5=%5.1f p50=%5.1f p95=%5.1f clipped=%.2f%%"
          % (mr, mg, mb, luma(mr, mg, mb), sat / n, Ls[int(0.05 * n)], Ls[int(0.5 * n)], Ls[int(0.95 * n)], 100.0 * clip / n))

    # 掩码内的「黑反照率残余」：判断亮度到底来自 diffuse 还是间接镜面
    rb3 = [0.0, 0.0, 0.0]
    rclip = 0
    rl = []
    for y in range(h):
        ra = A[y]
        for x in range(w):
            if not mask[y * w + x]:
                continue
            i = x * bpp
            r, g, b = ra[i], ra[i + 1], ra[i + 2]
            rb3[0] += r
            rb3[1] += g
            rb3[2] += b
            if max(r, g, b) >= 250:
                rclip += 1
            rl.append(luma(r, g, b))
    ar, ag, ab = [v / n for v in rb3]
    rl.sort()
    print("black-albedo residual  mean=(%3.0f,%3.0f,%3.0f) luma=%5.1f p50=%5.1f p95=%5.1f clipped=%.2f%%"
          % (ar, ag, ab, luma(ar, ag, ab), rl[int(0.5 * n)], rl[int(0.95 * n)], 100.0 * rclip / n))
    print("→ 残余占比 luma %.0f%%（残余高 = 亮度主要来自间接镜面/环境光，不是漫反射）"
          % (100.0 * luma(ar, ag, ab) / max(luma(mr, mg, mb), 1e-6)))

    # 找「整块都在掩码内」的最大矩形（积分图 + 20 px 网格，覆盖率 ≥ 97%）
    integ = [[0] * (w + 1) for _ in range(h + 1)]
    for y in range(h):
        run = 0
        row_i, row_m = integ[y + 1], integ[y]
        base = y * w
        for x in range(w):
            run += mask[base + x]
            row_i[x + 1] = row_m[x + 1] + run

    def cover(x0, y0, x1, y1):
        return (integ[y1][x1] - integ[y0][x1] - integ[y1][x0] + integ[y0][x0])

    best = None
    step = 20
    for x0 in range(0, w - step, step):
        for y0 in range(0, h - step, step):
            for x1 in range(x0 + 40, min(w, x0 + 420) + 1, step):
                for y1 in range(y0 + 40, min(h, y0 + 320) + 1, step):
                    area = (x1 - x0) * (y1 - y0)
                    if best and area <= best[0]:
                        continue
                    if cover(x0, y0, x1, y1) >= 0.97 * area:
                        best = (area, x0, y0, x1, y1)
    if best:
        print("largest clean bush rect = %d,%d,%d,%d  (%dx%d px)" % (best[1], best[2], best[3], best[4],
                                                                     best[3] - best[1], best[4] - best[2]))
    else:
        print("no clean rect found")


if __name__ == "__main__":
    main()
