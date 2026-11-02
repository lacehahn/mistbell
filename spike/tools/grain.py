#!/usr/bin/env python3
"""判断表面细节是「颗粒」还是「周期性条纹」（搓衣板）。

背景（2026-11-02 09:15）：雪面方差被程序化正弦补上来后，近景 Lstd 3.8→20.1 达标，
但画面变成等距波纹。方差这把尺子**分不清噪声与条纹**，因为两者方差都高。
周期条纹的特征是**梯度呈各向异性**：沿条纹法向的相邻像素差远大于沿条纹走向。

用法: python3 spike/tools/grain.py <png> x0,y0,x1,y1 [x0,y0,x1,y1 ...]
输出（每个矩形一行）:
  mean |dx| / mean |dy|  = ratio   颗粒应 0.6–1.7；搓衣板会跑到 <0.4 或 >2.5
  rowPhaseCorr           相邻两行的相关系数（>0.9 说明行行相同 = 直条纹）
"""
import sys
from texstat import read_png


def grad_concentration(rows, bpp, w, h, x0, y0, x1, y1):
    """结构张量相干度：(λ1-λ2)/(λ1+λ2)。各向同性颗粒 ≈0，等距条纹（搓衣板）≈1。
    （第一版写成 Σgx/Σ|g| 是错的：条纹两侧梯度符号相反会互相抵消 → 恒为 0。）"""
    import math
    jxx = jyy = jxy = 0.0
    for y in range(max(0, y0), min(h - 1, y1)):
        row = rows[y]
        nrow = rows[y + 1]
        for x in range(max(0, x0), min(w - 1, x1)):
            i = x * bpp
            gx = (row[i] - row[i + bpp]) + (row[i + 1] - row[i + bpp + 1]) + (row[i + 2] - row[i + bpp + 2])
            gy = (row[i] - nrow[i]) + (row[i + 1] - nrow[i + 1]) + (row[i + 2] - nrow[i + 2])
            jxx += gx * gx
            jyy += gy * gy
            jxy += gx * gy
    tr = jxx + jyy
    if tr < 1e-9:
        return 0.0
    disc = math.sqrt(max((jxx - jyy) ** 2 + 4 * jxy * jxy, 0.0))
    return disc / tr


def rect_stats(rows, bpp, w, h, x0, y0, x1, y1):
    x0, y0 = max(0, x0), max(0, y0)
    x1, y1 = min(w - 1, x1), min(h - 1, y1)
    dx = dy = n = 0
    for y in range(y0, y1):
        row = rows[y]
        nrow = rows[y + 1]
        for x in range(x0, x1):
            i = x * bpp
            r, g, b = row[i], row[i + 1], row[i + 2]
            dx += abs(r - row[i + bpp]) + abs(g - row[i + bpp + 1]) + abs(b - row[i + bpp + 2])
            dy += abs(r - nrow[i]) + abs(g - nrow[i + 1]) + abs(b - nrow[i + 2])
            n += 3
    dx /= max(n, 1)
    dy /= max(n, 1)
    # 相邻两行的行均值相关：把每行压成一个均值序列再求相邻差 / 总方差
    means = []
    for y in range(y0, y1):
        row = rows[y]
        s = 0
        for x in range(x0, x1):
            i = x * bpp
            s += row[i] + row[i + 1] + row[i + 2]
        means.append(s / max(3 * (x1 - x0), 1))
    if len(means) > 3:
        m = sum(means) / len(means)
        var = sum((v - m) ** 2 for v in means) / len(means)
        d2 = sum((means[i + 1] - means[i]) ** 2 for i in range(len(means) - 1)) / (len(means) - 1)
        row_ratio = (d2 / var) ** .5 if var > 1e-9 else 0.0
        # 条带判定：行均值序列的峰谷振幅与峰间距是否规整（周期性 = 搓衣板）
        extrema = []
        for i in range(1, len(means) - 1):
            if (means[i] - means[i - 1]) * (means[i + 1] - means[i]) < 0:
                extrema.append(i)
        amp = 0.0
        if extrema:
            peaks = [means[i] for i in extrema]
            amp = max(peaks) - min(peaks)
        gaps = [extrema[i + 1] - extrema[i] for i in range(len(extrema) - 1)]
        if len(gaps) > 2:
            gm = sum(gaps) / len(gaps)
            gsd = (sum((gv - gm) ** 2 for gv in gaps) / len(gaps)) ** .5
            cv = gsd / gm if gm > 1e-9 else 9.0
        else:
            cv = 9.0
        row_amp, row_cv = amp, cv
    else:
        row_ratio = 0.0
        row_amp, row_cv = 0.0, 9.0
    return dx, dy, row_ratio, row_amp, row_cv


def main():
    path = sys.argv[1]
    w, h, bpp, rows = read_png(path)
    print(f'{path} {w}x{h}')
    for spec in sys.argv[2:]:
        x0, y0, x1, y1 = [int(v) for v in spec.split(',')]
        dx, dy, rr, amp, cv = rect_stats(rows, bpp, w, h, x0, y0, x1, y1)
        ratio = dx / dy if dy > 1e-9 else 999.0
        verdict = '颗粒' if 0.6 <= ratio <= 1.7 else '各向异性'
        band = '条带（周期性）' if (amp >= 4.0 and cv <= 0.35) else '非周期'
        conc = grad_concentration(rows, bpp, w, h, x0, y0, x1, y1)
        print(f'  rect {spec:22s} dx={dx:5.2f} dy={dy:5.2f} ratio={ratio:5.2f} '
              f'rowDelta/rowStd={rr:5.3f} rowAmp={amp:5.1f} rowPeriodCV={cv:4.2f} '
              f'gradConc={conc:4.2f} → {verdict}/{band}')


if __name__ == '__main__':
    main()
