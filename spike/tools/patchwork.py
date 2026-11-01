#!/usr/bin/env python3
"""「迷彩度」尺子：量一块墙面里**块与块之间的明度差**有多大。

为什么需要：v2 石材把 `R-B / sat / Lstd / coarseStd` 全部做进门槛，但主视角上看
教堂变成了「白块+黑块」的迷彩拼布 —— 因为 per-block 明度随机幅度过大。
`Lstd` 与 `coarseStd` 都会奖励「有变化」，无法区分「自然的石色微差」与「拼布」。

做法：把矩形按 tile 像素切格（tile 大于颗粒、小于/接近一块石面），
求每格平均亮度，再求这些格均值的 p5/p95 与相对离散度：
  spread% = (p95 - p5) / mean * 100
参考图（写实概念图）的同一面墙给出「自然」的区间，渲染值落进去即可。

用法：
  python3 spike/tools/patchwork.py Shots/art_close.png 1400,200,1750,420 --tile 64
  python3 spike/tools/patchwork.py art-ref/concept-gergeti-sunny.png 500,300,900,600 --tile 64
"""
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from texstat import read_png


def lum(rows, w, h):
    bpp = len(rows[0]) // w
    out = []
    for y in range(h):
        row = rows[y]
        out.append([0.299 * row[x * bpp] + 0.587 * row[x * bpp + 1] + 0.114 * row[x * bpp + 2] for x in range(w)])
    return out


def pct(vals, q):
    s = sorted(vals)
    if not s:
        return 0.0
    i = min(len(s) - 1, max(0, int(round(q * (len(s) - 1)))))
    return s[i]


def measure(path, rect, tile):
    x0, y0, x1, y1 = rect
    w, h, bpp, rows = read_png(path)
    x1, y1 = min(x1, w), min(y1, h)
    L = lum(rows, w, h)
    means = []
    ty = y0
    while ty + tile <= y1:
        tx = x0
        while tx + tile <= x1:
            s = 0.0
            for y in range(ty, ty + tile):
                row = L[y]
                s += sum(row[tx:tx + tile])
            means.append(s / (tile * tile))
            tx += tile
        ty += tile
    m = sum(means) / len(means)
    spread = (pct(means, 0.95) - pct(means, 0.05)) / max(m, 1e-6) * 100
    # 细颗粒能量：与 tile 均值的高频差（同一区域内的逐像素起伏）
    fine = 0.0
    n = 0
    ty = y0
    while ty + tile <= y1:
        tx = x0
        while tx + tile <= x1:
            s = 0.0
            for y in range(ty, ty + tile):
                row = L[y]
                s += sum(row[tx:tx + tile])
            base = s / (tile * tile)
            for y in range(ty, ty + tile):
                for x in range(tx, tx + tile):
                    fine += abs(L[y][x] - base)
                    n += 1
            tx += tile
        ty += tile
    fine /= max(n, 1)
    # 邻块明度差的对数中位数：块与块之间「该不该差这么多」，比 p95-p5 更能抗门洞/阴影污染
    grid = []
    ty = y0
    while ty + tile <= y1:
        tx = x0
        rowmeans = []
        while tx + tile <= x1:
            s = 0.0
            for y in range(ty, ty + tile):
                row = L[y]
                s += sum(row[tx:tx + tile])
            rowmeans.append(s / (tile * tile))
            tx += tile
        grid.append(rowmeans)
        ty += tile
    diffs = []
    for rowmeans in grid:
        for i in range(len(rowmeans) - 1):
            diffs.append(abs(rowmeans[i + 1] - rowmeans[i]))
    # 色度起伏：每格平均饱和度（chroma = (max-min)/max per pixel）的离散度。
    # 明度差合格但「有的块偏橄榄绿、有的偏奶油」时，肉眼看就是迷彩 —— 这个指标抓的就是它。
    bpp = len(rows[0]) // w
    sats = []
    ty = y0
    while ty + tile <= y1:
        tx = x0
        while tx + tile <= x1:
            acc = 0.0
            cnt = 0
            for y in range(ty, ty + tile):
                row = rows[y]
                for x in range(tx, tx + tile):
                    r, g, b = row[x * bpp], row[x * bpp + 1], row[x * bpp + 2]
                    mx = max(r, g, b)
                    acc += (mx - min(r, g, b)) / mx if mx else 0.0
                    cnt += 1
            sats.append(acc / cnt)
            tx += tile
        ty += tile
    sm = sum(sats) / len(sats)
    sstd = (sum((v - sm) ** 2 for v in sats) / len(sats)) ** 0.5
    print("    chroma: meanSat=%.3f  blockSatStd=%.3f  (blockSatStd/meanSat=%.1f%%)" % (sm, sstd, 100 * sstd / max(sm, 1e-6)))
    diffs.sort()
    nd = diffs[len(diffs) // 2] if diffs else 0.0
    print("%-46s rect=%s tile=%d  tiles=%d  meanL=%.1f  blockSpread=%.1f%%  neighborDiff=%.1f%%  fineGrain=%.2f"
          % (os.path.basename(path), rect, tile, len(means), m, spread, nd / max(m, 1e-6) * 100, fine))
    return spread, nd / max(m, 1e-6) * 100, fine, len(means)


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    path = sys.argv[1]
    rects = []
    tile = 64
    args = sys.argv[2:]
    i = 0
    while i < len(args):
        if args[i] == "--tile":
            tile = int(args[i + 1]); i += 2; continue
        rects.append(tuple(int(v) for v in args[i].split(","))); i += 1
    worst = 0.0
    for r in rects:
        spread, nd, fine, n = measure(path, r, tile)
        worst = max(worst, nd)
    print("worst neighborDiff=%.1f%%" % worst)
