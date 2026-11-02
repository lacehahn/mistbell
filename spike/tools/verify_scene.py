#!/usr/bin/env python3
"""尺子有效性复核：把「数点数」换成「数有面积的对象」。

背景（2026-11-02 08:45）：Codex 的 warm_spots 口径是「R-B>35 的 8 连通区域个数」，
没有任何面积下限 —— 雪地上一点暖色噪声、暖色太阳光渗进雪粒、旗杆木料的抗锯齿边
都会被算成「一个暖光点」，于是 4 窗 + 2 火盆 = 6 个的门槛被 179 个噪声点「通过」。
本脚本要求每个连通域 >= MIN_AREA 像素才算一个对象，并打印最大 10 个的 bbox 供人工定位。

用法: python3 spike/tools/verify_scene.py <png> [<png> ...]
输出:
  [WARM] min_area=40 spots=<n> areas=[...] boxes=[...]     暖光对象（阈值 >=6）
  [DARK] lower-half dark blobs area>=60 count=<n> areas=[...]  近黑斑块（露岩是否成「雀斑」）
  [MOUNT] sat / p5,p50 已在 region.py 里，这里只补「山体是否无彩」的均值和最大色度
"""
import sys
from texstat import read_png

MIN_WARM = 40
MIN_DARK = 60


def comps(mask, w, h):
    out = []
    seen = bytearray(len(mask))
    for start in range(len(mask)):
        if not mask[start] or seen[start]:
            continue
        seen[start] = 1
        stack = [start]
        n = 0
        minx = maxx = start % w
        miny = maxy = start // w
        while stack:
            p = stack.pop()
            y, x = divmod(p, w)
            n += 1
            if x < minx: minx = x
            if x > maxx: maxx = x
            if y < miny: miny = y
            if y > maxy: maxy = y
            for dy in (-1, 0, 1):
                yy = y + dy
                if yy < 0 or yy >= h: continue
                for dx in (-1, 0, 1):
                    xx = x + dx
                    if xx < 0 or xx >= w: continue
                    q = yy * w + xx
                    if mask[q] and not seen[q]:
                        seen[q] = 1
                        stack.append(q)
        out.append((n, minx, miny, maxx, maxy))
    return out


def main():
    for path in sys.argv[1:]:
        w, h, bpp, rows = read_png(path)
        warm = bytearray(w * h)
        dark = bytearray(w * h)
        chroma_max = 0
        for y in range(h):
            base = y * w
            row = rows[y]
            for x in range(w):
                r, g, b = row[x * bpp:x * bpp + 3]
                if r - b > 35:
                    warm[base + x] = 1
                if y >= h // 2:
                    v = max(r, g, b)
                    if v < 64:
                        dark[base + x] = 1
                c = max(r, g, b) - min(r, g, b)
                if c > chroma_max and y < h // 2:
                    chroma_max = c
        ws = [c for c in comps(warm, w, h) if c[0] >= MIN_WARM]
        ws.sort(reverse=True)
        ds = [c for c in comps(dark, w, h) if c[0] >= MIN_DARK]
        ds.sort(reverse=True)
        print(f'[WARM] min_area={MIN_WARM} spots={len(ws)} areas={[c[0] for c in ws[:10]]}')
        for n, x0, y0, x1, y1 in ws[:10]:
            print(f'   area={n:5d} box=({x0},{y0})-({x1},{y1}) px_center=({(x0 + x1) // 2},{(y0 + y1) // 2})')
        print(f'[DARK] min_area={MIN_DARK} count={len(ds)} areas={[c[0] for c in ds[:10]]} '
              f'total_px={sum(c[0] for c in ds)}')
        win = [c for c in ws if (c[4] - c[3] + 1) >= 20 and (c[3] - c[1] + 1) <= 20]
        print(f'[WINDOW] tall-narrow (h>=20,w<=20) count={len(win)} boxes='
              f'{[(c[1], c[2], c[3], c[4]) for c in win[:6]]}')
        print(f'[CHROMA] upper-half max(R-B within pixel chroma)={chroma_max}')
        print(path)


if __name__ == '__main__':
    main()
