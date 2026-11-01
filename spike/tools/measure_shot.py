#!/usr/bin/env python3
"""独立量测一帧调色/氛围指标（不依赖 PIL/numpy）。

用法: python3 spike/tools/measure_shot.py <png> [--json]

指标口径固定，供每轮验收复用：
  magenta_px    R>150 且 B>150 且 G<100 的像素数（材质错误检查）
  snow_ratio    下半画面中 HSV: S<0.18 且 V>0.72 的占比（雪覆盖）
  sky_mean      上半画面顶行平均亮度（天空）/ 3 行带
  white_clip    V>0.95 且 S<0.10 的像素占全图比例（过曝检查）
  dark_ratio    下半画面 V<0.25 的占比（黑斑/岩石占比）
  warm_spots    R-B>35 的像素数（暖光）
  ground_shadow 画面下 1/3 左右两条竖带的平均亮度差（有投影则 >0.05）
"""
import subprocess, sys, json

def load(path):
    w, h = 0, 0
    out = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", path, "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
        capture_output=True, check=True).stdout
    # 尺寸从 ffprobe 拿
    probe = subprocess.run(
        ["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
         "stream=width,height", "-of", "csv=p=0", path], capture_output=True, text=True).stdout.strip()
    w, h = [int(x) for x in probe.split(",")[:2]]
    return out, w, h

def hsv(r, g, b):
    mx, mn = max(r, g, b), min(r, g, b)
    v = mx / 255.0
    s = 0.0 if mx == 0 else (mx - mn) / mx
    return s, v

def main():
    path = sys.argv[1]
    buf, w, h = load(path)
    step = 2
    mag = clip = warm = 0
    lower = snow = dark = 0
    left_sum = right_sum = 0
    left_n = right_n = 0
    for y in range(0, h, step):
        row = y * w * 3
        for x in range(0, w, step):
            i = row + x * 3
            r, g, b = buf[i], buf[i + 1], buf[i + 2]
            s, v = hsv(r, g, b)
            if r > 150 and b > 150 and g < 100:
                mag += 1
            if r - b > 35:
                warm += 1
            if v > 0.95 and s < 0.10:
                clip += 1
            if y > h * 0.5:
                lower += 1
                if s < 0.18 and v > 0.72:
                    snow += 1
                if v < 0.25:
                    dark += 1
            if y > h * 0.66:
                lum = 0.299 * r + 0.587 * g + 0.114 * b
                if x < w * 0.42:
                    left_sum += lum; left_n += 1
                elif x > w * 0.58:
                    right_sum += lum; right_n += 1
    n = (w // step) * (h // step)
    res = {
        "magenta_px": mag,
        "snow_ratio": round(snow / max(lower, 1), 4),
        "white_clip_ratio": round(clip / max(n, 1), 4),
        "dark_ratio_lower": round(dark / max(lower, 1), 4),
        "warm_px": warm,
        "ground_shadow_gap": round(abs(left_sum / max(left_n, 1) - right_sum / max(right_n, 1)) / 255.0, 4),
        "size": f"{w}x{h}",
    }
    if "--json" in sys.argv:
        print(json.dumps(res))
    else:
        print("[MEASURE] " + " ".join(f"{k}={v}" for k, v in res.items()))

if __name__ == "__main__":
    main()
