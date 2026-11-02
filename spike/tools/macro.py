#!/usr/bin/env python3
"""macro.py — 山体/崖壁的「大结构」尺子：区分「毛绒平滑块」与「有雪线+岩层的岩壁」

为什么需要它：`region.py` 的 Lstd 是逐像素亮度差，雪面颗粒（盐粒噪点）就能把它堆到 30+，
所以它分不出「平滑的毛绒山」和「有崖壁岩层的山」。本尺子先用 B×B 块均值抹掉纹理，
只看大尺度明暗结构：

  macroStd   块均值（B=24 px）的标准差 —— 只有「雪线/崖壁/岩层」这种大块明暗才能抬高它
  dark%      暗部像素占比（luma < 95）＝裸露岩石
  bright%    亮部像素占比（luma > 190）＝积雪
  strata     std(行均值) / std(列均值)：水平岩层/雪线带 → 行之间反差大、列之间反差小 → 远大于 1
             竖直毛绒穹顶（光照左右渐变）→ 列之间反差大 → < 1

判读（山体矩形内）：
  PLUSH   macroStd 低 或 bright% 接近 100（整座山被雪/白噪声糊满）或 dark% < 3 → 没有岩壁
  ROCK    macroStd >= 22 且 5 <= dark% <= 55 且 8 <= bright% <= 70 且 strata >= 1.15

用法：
  python3 spike/tools/macro.py selftest                     # 合成控制图定标（必须先跑）
  python3 spike/tools/macro.py scan IMG x0,y0,x1,y1 [more rects...] [--block 24]
"""
import struct
import sys
import zlib
import math


def read_png(path):
    data = open(path, "rb").read()
    pos, w, h, ct, bd, idat = 8, None, None, 0, 0, b""
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        tag = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + ln]
        if tag == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", chunk[:10])
        elif tag == b"IDAT":
            idat += chunk
        pos += 12 + ln
    raw = zlib.decompress(idat)
    bpp = {0: 1, 2: 3, 4: 2, 6: 4}[ct]
    stride = w * bpp
    rows, prev, i = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[i]
        line = bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        if f == 1:
            for x in range(bpp, stride):
                line[x] = (line[x] + line[x - bpp]) & 255
        elif f == 2:
            for x in range(stride):
                line[x] = (line[x] + prev[x]) & 255
        elif f == 3:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                line[x] = (line[x] + ((a + prev[x]) >> 1)) & 255
        elif f == 4:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                c = prev[x - bpp] if x >= bpp else 0
                b = prev[x]
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 255
        rows.append(bytes(line))
        prev = line
    return w, h, bpp, rows


def crop_luma(path, x0, y0, x1, y1):
    w, h, bpp, rows = read_png(path)
    x0, x1 = max(0, x0), min(w, x1)
    y0, y1 = max(0, y0), min(h, y1)
    out = []
    for y in range(y0, y1):
        r = rows[y]
        row = []
        for x in range(x0, x1):
            base = x * bpp
            if bpp >= 3:
                row.append(0.2126 * r[base] + 0.7152 * r[base + 1] + 0.0722 * r[base + 2])
            else:
                row.append(float(r[base]))
        out.append(row)
    return out


def stats(lum, block=24):
    h, w = len(lum), len(lum[0])
    n = h * w
    flat = [v for row in lum for v in row]
    dark = sum(1 for v in flat if v < 95) / n
    bright = sum(1 for v in flat if v > 190) / n
    # 块均值：抹掉纹理，只留大结构
    means = []
    for by in range(0, h - block + 1, block):
        for bx in range(0, w - block + 1, block):
            s = 0.0
            for yy in range(by, by + block):
                r = lum[yy]
                s += sum(r[bx:bx + block])
            means.append(s / (block * block))
    mstd = stdev(means)
    rowm = [sum(r) / w for r in lum]
    colm = [sum(lum[y][x] for y in range(h)) / h for x in range(w)]
    rs, cs = stdev(rowm), stdev(colm)
    strata = (rs / cs) if cs > 1e-6 else 99.0
    verdict = "PLUSH"
    if mstd >= 22 and 0.05 <= dark <= 0.55 and 0.08 <= bright <= 0.70 and strata >= 1.15:
        verdict = "ROCK"
    return dict(mstd=mstd, dark=dark, bright=bright, strata=strata,
                rowStd=rs, colStd=cs, verdict=verdict)


def stdev(xs):
    if len(xs) < 2:
        return 0.0
    m = sum(xs) / len(xs)
    return math.sqrt(sum((v - m) ** 2 for v in xs) / len(xs))


def write_png(path, w, h, pix):
    """pix: list of rows, each row list of (r,g,b)"""
    raw = b"".join(b"\x00" + bytes(v for p in row for v in p) for row in pix)

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data +
                struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 6))
    png += chunk(b"IEND", b"")
    open(path, "wb").write(png)


def selftest(tmpdir="/tmp"):
    """合成 5 张控制图，确认本尺子分得清毛绒/噪声/岩层/雪线"""
    W = H = 240
    cases = {}

    # A 毛绒穹顶：左右光照渐变 + 顶部亮，无明暗界限
    rows_a = []
    for y in range(H):
        row = []
        for x in range(W):
            t = y / H
            v = 150 + 70 * (1 - t) + 25 * math.sin(math.pi * x / W)
            row.append((int(min(252, v)), int(min(252, v)), int(min(252, v)) + 4))
        rows_a.append(row)
    cases["plush_dome"] = rows_a

    # B 电视雪花：单像素随机 == 现在雪面的读感（纹理很强、大结构为零）
    s = 12345
    rows_b = []
    for y in range(H):
        row = []
        for x in range(W):
            s = (1103515245 * s + 12345) & 0x7FFFFFFF
            v = 120 + (s >> 16) % 90
            row.append((v, v, v))
        rows_b.append(row)
    cases["tv_noise"] = rows_b

    # C 水平岩层：亮雪带 + 暗岩带交替（有雪线对比，无光照渐变）
    rows_c = []
    for y in range(H):
        band = int(120 + 90 * math.sin(y / 9.0))
        row = [(band, band, band) for _ in range(W)]
        rows_c.append(row)
    cases["strata_bands"] = rows_c

    # D 雪线二分：上半雪、下半岩（有对比但没有层理）
    rows_d = []
    for y in range(H):
        v = 225 if y < H // 2 else 80
        rows_d.append([(v, v, v) for _ in range(W)])
    cases["snow_line"] = rows_d

    # E 平滑山 + 大块阴影（有光照但没有雪线）
    rows_e = []
    for y in range(H):
        row = []
        for x in range(W):
            v = 130 + 40 * math.cos((x - W / 2) / W * 3.0) + 20 * math.sin(y / 60.0)
            row.append((int(v), int(v), int(v) + 3))
        rows_e.append(row)
    cases["smooth_shaded"] = rows_e

    expect = {"plush_dome": "PLUSH", "tv_noise": "PLUSH", "strata_bands": "ROCK",
              "snow_line": "ROCK", "smooth_shaded": "PLUSH"}
    ok = 0
    for name, rows in cases.items():
        path = "%s/macro_%s.png" % (tmpdir, name)
        write_png(path, W, H, rows)
        lum = crop_luma(path, 0, 0, W, H)
        st = stats(lum, 24)
        got = st["verdict"]
        good = got == expect[name]
        ok += good
        print("%-14s macroStd=%5.1f dark=%5.1f%% bright=%5.1f%% strata=%4.2f -> %-5s %s"
              % (name, st["mstd"], st["dark"] * 100, st["bright"] * 100, st["strata"], got,
                 "PASS" if good else "FAIL (expect %s)" % expect[name]))
    print("SELFTEST %d/%d %s" % (ok, len(cases), "PASS" if ok == len(cases) else "FAIL"))
    return 0 if ok == len(cases) else 1


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    if argv[1] == "selftest":
        return selftest()
    if argv[1] != "scan":
        print(__doc__)
        return 2
    img = argv[2]
    block = 24
    rects = []
    for a in argv[3:]:
        if a.startswith("--block"):
            block = int(a.split("=")[1])
            continue
        rects.append([int(v) for v in a.split(",")])
    print("%s" % img)
    for x0, y0, x1, y1 in rects:
        lum = crop_luma(img, x0, y0, x1, y1)
        st = stats(lum, block)
        print("  rect %5d,%5d,%5d,%5d  macroStd=%5.1f dark=%5.1f%% bright=%5.1f%% "
              "strata=%4.2f rowStd=%5.1f colStd=%5.1f  %s"
              % (x0, y0, x1, y1, st["mstd"], st["dark"] * 100, st["bright"] * 100,
                 st["strata"], st["rowStd"], st["colStd"], st["verdict"]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
