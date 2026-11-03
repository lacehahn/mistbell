#!/usr/bin/env python3
"""室内验收量测：从截图取分区亮度（0-1 线性 luma），口径固定，便于版本间对比。

用法: python3 spike/tools/measure_interior.py [shot.png ...]
默认量 interior_inside.png / interior_lookup.png。
"""
import sys
from pathlib import Path
from PIL import Image, ImageStat

SHOTS = Path(__file__).resolve().parents[1] / "project" / "Shots"


def lin(c: float) -> float:
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def luma(im: Image.Image) -> float:
    r, g, b = (lin(v) for v in ImageStat.Stat(im.convert("RGB")).mean)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


# 相对坐标 (x0, y0, x1, y1)，取自 1600x900 固定机位，缩放到实际尺寸后裁剪
REGIONS = {
    "nave_floor":  (0.36, 0.72, 0.64, 0.92),
    "wall_left":   (0.06, 0.35, 0.20, 0.62),
    "wall_right":  (0.80, 0.35, 0.94, 0.62),
    "bench_top":   (0.18, 0.60, 0.34, 0.66),
    "altar":       (0.42, 0.42, 0.58, 0.60),
    "ceiling_top": (0.30, 0.02, 0.70, 0.14),
}


def main(paths):
    for p in paths:
        img = Image.open(p)
        w, h = img.size
        print(f"\n== {Path(p).name}  ({w}x{h})")
        vals = {}
        for name, (x0, y0, x1, y1) in REGIONS.items():
            box = (int(x0 * w), int(y0 * h), int(x1 * w), int(y1 * h))
            v = luma(img.crop(box))
            vals[name] = v
            print(f"  {name:12s} luma={v:.4f}")
        if "nave_floor" in vals and "ceiling_top" in vals and vals["nave_floor"] > 0:
            print(f"  ceiling/nave = {vals['ceiling_top'] / vals['nave_floor']:.3f}  (顶部应更暗，<1.0)")


if __name__ == "__main__":
    args = sys.argv[1:] or [str(SHOTS / "interior_inside.png"), str(SHOTS / "interior_lookup.png")]
    main(args)
