#!/usr/bin/env python3
"""量「地平线是不是硬边」：逐列找到草地顶行，再看该行上下的亮度过渡有多陡。

为什么要这个：`horizon.py` 只量地平线高度跨度（起伏），量不出「草地一刀切到天空」。
HD-2D/真实照片里，远处地面会随距离变浅、最终与天空同色，交界是一条几十像素的软过渡；
程序渲染的平板地形则是 1 px 硬跳变。

用法：python3 tools/edge.py Shots/art_runtime.png [更多图...]
输出（每列）：
  horizon  草地第一行的行号
  Lgrnd    地平线下 3 px 处的地面亮度（0..255）
  Lnear    地平线下 40 px 处的亮度 → Lgrnd-Lnear 就是「近地平线被雾提亮了多少」
  Lsky     地平线上 30 px 处的天空亮度
  jump     地平线附近单行最大亮度跳变
判据：
  mean jump > 30  → 硬边（问题）
  12..30          → 偏硬
  < 12            → 软过渡（合格）
  Lgrnd-Lnear < 6 → 地面完全没有距离雾（几乎没有空气渗染）
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


def lum(rows, bpp, x, y):
    if y < 0 or y >= len(rows):
        return None
    p = y * 0  # placeholder for clarity
    r, g, b = rows[y][x * bpp], rows[y][x * bpp + 1], rows[y][x * bpp + 2]
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def green_row(rows, bpp, x):
    for y in range(120, len(rows)):
        r, g, b = rows[y][x * bpp], rows[y][x * bpp + 1], rows[y][x * bpp + 2]
        if g > 60 and g > r + 18 and g > b + 25:
            return y
    return None


def analyse(path, skip=None):
    """skip：不采样的列区间（如教堂所在的画面中央），避免把石墙自身的纹理边当成地平线边。"""
    w, h, bpp, rows = read_png(path)
    cols = [w // 16, w // 6, w // 4, 3 * w // 4, 5 * w // 6, 15 * w // 16]
    if skip:
        lo, hi = skip
        cols = [x for x in cols if not (lo <= x <= hi)]
    print("%s  %d x %d" % (path, w, h))
    jumps, lifts = [], []
    for x in cols:
        y0 = green_row(rows, bpp, x)
        if y0 is None:
            print("   col x=%4d  没有草地行" % x)
            continue
        lgrnd = lum(rows, bpp, x, y0 + 3)
        lnear = lum(rows, bpp, x, y0 + 40)
        lsky = lum(rows, bpp, x, max(0, y0 - 30))
        # 有效性检查（2026-11-02 round 24 加）：前景灌木长到 1.2 m 后会插进采样列，
        # 此时 `green_row` 找到的是**灌木顶边**而不是天地交界（x=320 的 Lsky 掉到 29.2、
        # x=480 的 jump 154.2 都是这么来的）。要求该列「上方是天空、下方是草地」：
        #   天空像天空 = 偏蓝（b-r ≥ 10）；草地像草地 = 够亮且很绿（luma ≥ 90 且 g-r ≥ 30）。
        # 不满足就跳过该列，不参与 mean（这是采样有效性，不是门槛）。
        if lsky is not None:
            r, g, b = rows[max(0, y0 - 30)][x * bpp], rows[max(0, y0 - 30)][x * bpp + 1], rows[max(0, y0 - 30)][x * bpp + 2]
            sky_like = (b - r) >= 10
            r2, g2, b2 = rows[min(h - 1, y0 + 3)][x * bpp], rows[min(h - 1, y0 + 3)][x * bpp + 1], rows[min(h - 1, y0 + 3)][x * bpp + 2]
            ground_like = lum(rows, bpp, x, y0 + 3) >= 90 and (g2 - r2) >= 15 and (g2 - b2) >= 20
            # 地平线上方 2..40 px 必须是**平滑**的天空/雾带：灌木顶边会插进这条带，
            # 那时会出现 60–150 的单行亮度跌落（x=480 灌木顶边 295→147）。真正的
            # 天地雾过渡在这条带里每行只变 3–7（art_ground x=120：553→347 共 30 行）。
            band_clear = True
            prev = None
            for yy in range(max(0, y0 - 40), max(0, y0 - 2)):
                v = lum(rows, bpp, x, yy)
                if v is None:
                    continue
                if prev is not None and prev - v > 25:
                    band_clear = False
                    break
                prev = v
        else:
            sky_like = ground_like = band_clear = False
        window = [lum(rows, bpp, x, y) for y in range(max(0, y0 - 3), min(h, y0 + 16))]
        window = [v for v in window if v is not None]
        jump = max(abs(window[i + 1] - window[i]) for i in range(len(window) - 1))
        valid = sky_like and ground_like and band_clear
        print("   col x=%4d horizon=%4d Lgrnd=%6.1f Lnear=%6.1f Lsky=%6.1f jump=%5.1f%s" %
              (x, y0, lgrnd, lnear if lnear is not None else -1,
               lsky if lsky is not None else -1, jump,
               "" if valid else "   ← 跳过：该列不满足「上方天空 / 下方草地」"))
        if not valid:
            continue
        jumps.append(jump)
        if lnear is not None:
            lifts.append(lgrnd - lnear)
    if jumps:
        mj = sum(jumps) / len(jumps)
        verdict = "硬边（问题）" if mj > 30 else ("偏硬" if mj > 12 else "软过渡（合格）")
        print("   mean jump=%.1f max=%.1f → %s" % (mj, max(jumps), verdict))
        if lifts:
            print("   ground distance-lift mean=%.1f (L地平线下3px - 下40px; >6 才有空气渗染)" %
                  (sum(lifts) / len(lifts)))


SKIP = None
for p in sys.argv[1:]:
    if p.startswith("--skip="):
        lo, hi = p.split("=", 1)[1].split(":")
        SKIP = (int(lo), int(hi))
    else:
        analyse(p, skip=SKIP)
