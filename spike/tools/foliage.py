#!/usr/bin/env python3
"""量「灌木读成什么」——灌木矩形 vs 同帧草地矩形的亮度/饱和度/高光占比。

为什么另开一把尺子：`region.py` 只给绝对色值，判不了「灌木比草地亮多少」。
「读成绿色石头」这件事其实由三个可量的量决定：
  ① 亮度比 bush/grass —— 灌木应该比草地**略暗或持平**（0.90–1.15），
     现在 art_ground 是 1.57（灌木高光带比草亮 57%，所以看着「浮」）
  ② clipped% —— 灌木矩形里的削顶像素占比。草是 0.00%，现在灌木 0.99%
  ③ Lstd —— 灌木表面明暗跨度。过大的 Lstd + 低 edge% = 光滑矿石；
     需要「高频多（贴图 edge%）+ 低频柔和」（靠几何叶簇与法线高频化解决）

用法：
  python3 spike/tools/foliage.py                      # 三张图全部默认矩形
  python3 spike/tools/foliage.py art_ground           # 只量一张
矩形定义在 RECTS 里（改机位才需要改这里，验收门槛不许改）。
"""
import sys
import struct
import zlib

ROOT = "spike/project/Shots/"

# shot → (bush rect, grass rect)；矩形是 1920x1080 原图坐标 (x0,y0,x1,y1)
# ⚠️ 灌木矩形必须**落在灌木本体内部**：2026-11-02 用「反照率归零差分」掩码（`tools/bushmask.py`）验证过，
#    最早用的 (330,430,500,560) 只有约两成像素是灌木，其余是背景草与石板路，量出来的
#    「clipped 0.81%」全是背景——灌木本体实际是 0.00%。换矩形前先用 bushmask.py 看一眼覆盖率。
RECTS = {
    "art_ground": ((360, 540, 780, 620), (900, 700, 1300, 900)),
    "art_runtime": ((1300, 820, 1750, 980), (700, 860, 1150, 980)),
    # A/B 用：卡片路线的截图与 blob 同机位同落点，矩形可直接复用
    "card_art_ground": ((360, 540, 780, 620), (900, 700, 1300, 900)),
    "card_art_runtime": ((1300, 820, 1750, 980), (700, 860, 1150, 980)),
}


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


def stats(rows, bpp, spec):
    x0, y0, x1, y1 = [int(v) for v in spec]
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
    luma = 0.2126 * mr + 0.7152 * mg + 0.0722 * mb
    return dict(mean=(mr, mg, mb), luma=luma, sat=sat / n, Lstd=std, clip=100.0 * clip / n)


def main():
    shots = sys.argv[1:] or list(RECTS)
    worst = 0.0
    for shot in shots:
        bush_rect, grass_rect = RECTS[shot]
        w, h, bpp, rows = read_png(ROOT + shot + ".png")
        b = stats(rows, bpp, bush_rect)
        g = stats(rows, bpp, grass_rect)
        ratio = b["luma"] / max(g["luma"], 1e-6)
        worst = max(worst, abs(ratio - 1.0))
        print("%-12s %dx%d" % (shot, w, h))
        print("   bush  %-18s mean=(%3.0f,%3.0f,%3.0f) luma=%5.1f sat=%.3f Lstd=%5.1f clipped=%.2f%%"
              % (bush_rect, b["mean"][0], b["mean"][1], b["mean"][2], b["luma"], b["sat"], b["Lstd"], b["clip"]))
        print("   grass %-18s mean=(%3.0f,%3.0f,%3.0f) luma=%5.1f sat=%.3f Lstd=%5.1f clipped=%.2f%%"
              % (grass_rect, g["mean"][0], g["mean"][1], g["mean"][2], g["luma"], g["sat"], g["Lstd"], g["clip"]))
        print("   ratio bush/grass luma=%.2f  sat=%.2f   (luma 门槛 0.90–1.15；clipped 门槛 ≤0.05%%)"
              % (ratio, b["sat"] / max(g["sat"], 1e-6)))
    print("worst |ratio-1| = %.2f" % worst)


if __name__ == "__main__":
    main()
