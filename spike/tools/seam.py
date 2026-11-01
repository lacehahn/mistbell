#!/usr/bin/env python3
"""贴图平铺接缝尺子：比较「真实环绕边」（末列 vs 首列、末行 vs 首行）与内部相邻像素差。

为什么需要：make_textures.py 打印的 seam 用全局 SZ=128 索引，当贴图尺寸 ≠128 时
（如 stone 提到 256）量的是图片左上象限的内部边，不是真正的接缝，数字没有意义。
本脚本按 PNG 实际尺寸量环绕边，ratio ≈ 1 表示接缝与内部无差别。

用法：
  python3 spike/tools/seam.py spike/project/Assets/Art/Textures/stone_ashlar.png [...]
判据：ratio ≤ 2.0 视为无可见接缝；> 4 视为有明显平铺缝。
"""
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from texstat import read_png  # 复用已有的纯标准库 PNG 解码器


def seam(path):
    w, h, bpp, rows = read_png(path)
    ch = bpp

    def diff_col(a, b):
        return sum(abs(rows[y][a * bpp + c] - rows[y][b * bpp + c]) for y in range(h) for c in range(bpp)) / (h * bpp)

    def diff_row(a, b):
        return sum(abs(rows[a][x * bpp + c] - rows[b][x * bpp + c]) for x in range(w) for c in range(bpp)) / (w * bpp)

    inner_c = sum(diff_col(x, x + 1) for x in range(w - 1)) / (w - 1)
    inner_r = sum(diff_row(y, y + 1) for y in range(h - 1)) / (h - 1)
    wrap_c = diff_col(w - 1, 0)
    wrap_r = diff_row(h - 1, 0)
    rc = wrap_c / max(inner_c, 0.01)
    rr = wrap_r / max(inner_r, 0.01)
    verdict = "OK" if max(rc, rr) <= 2.0 else ("SEAM" if max(rc, rr) > 4.0 else "MARGINAL")
    print("%-58s %dx%d  inner(col,row)=%.2f,%.2f  wrap(col,row)=%.2f,%.2f  ratio=%.2f,%.2f  %s"
          % (path, w, h, inner_c, inner_r, wrap_c, wrap_r, rc, rr, verdict))
    return max(rc, rr)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    worst = max(seam(p) for p in sys.argv[1:])
    print("worst ratio=%.2f" % worst)
