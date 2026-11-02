#!/usr/bin/env python3
"""surface.py — 表面尺子：区分「各向同性颗粒」「方向性涂抹」「离散纹样（波纹/棋盘/平铺重复）」

为什么需要它：frame/region/sharp/hf 都是方差类尺子，正弦波纹与真实颗粒的方差一样高
（PROGRESS 已三次踩坑）。本尺子测**空间相关结构**，三个量：

  grain    去趋势后的 RMS（细节幅度）
  corrLen  自相关第一次跌到 0.5 的 lag（沿 x / 沿 y）＝图案的「尺度」
  aniso    max(corrLenX, corrLenY) / min(...)（方向性：涂抹/条纹会远大于 1）
  peak     自相关在 lag>=6 上的**局部极大值**（离散纹样；波纹/棋盘/平铺重复）

判读（HD-2D 目标：雪/岩/石是各向同性像素颗粒，尺度 3–8 px）：
  OK      peak < 0.40  且  aniso < 1.8             → 颗粒
  SMEAR   aniso >= 1.8 且 peak < 0.40              → 方向性涂抹（横向拉丝/水波）
  PATTERN peak >= 0.40                              → 离散纹样（波纹/棋盘/重复）

用法：
  python3 spike/tools/surface.py selftest                                  # 合成控制图定标（必须先跑）
  python3 spike/tools/surface.py scan IMG --rect x0,y0,x1,y1 [--patch 128 --step 64]

依赖：ffmpeg（本仓库既有约定，无 numpy/PIL）。
"""
import argparse
import math
import subprocess
import sys


def decode_gray(path, x, y, w, h):
    cmd = ["ffmpeg", "-v", "error", "-i", path,
           "-vf", "crop=%d:%d:%d:%d,format=gray" % (w, h, x, y),
           "-f", "rawvideo", "-pix_fmt", "gray", "-"]
    out = subprocess.run(cmd, capture_output=True).stdout
    if len(out) != w * h:
        raise RuntimeError("decode %s -> %d bytes, expected %d" % (path, len(out), w * h))
    return list(out)


def box_mean(src, w, h, r):
    stride = w + 1
    integ = [0] * ((h + 1) * stride)
    for yy in range(h):
        run = 0
        base = (yy + 1) * stride
        prev = yy * stride
        row = yy * w
        for xx in range(w):
            run += src[row + xx]
            integ[base + xx + 1] = integ[prev + xx + 1] + run
    out = [0.0] * (w * h)
    for yy in range(h):
        y0 = max(0, yy - r)
        y1 = min(h - 1, yy + r)
        for xx in range(w):
            x0 = max(0, xx - r)
            x1 = min(w - 1, xx + r)
            s = (integ[(y1 + 1) * stride + x1 + 1] - integ[y0 * stride + x1 + 1]
                 - integ[(y1 + 1) * stride + x0] + integ[y0 * stride + x0])
            out[yy * w + xx] = s / float((y1 - y0 + 1) * (x1 - x0 + 1))
    return out


def detrend(src, w, h, r=6):
    m = box_mean(src, w, h, r)
    return [src[i] - m[i] for i in range(w * h)]


def autocorr_curve(d, w, h, axis, max_lag):
    """逐 lag 归一化自相关（重叠区各自 RMS 归一），返回 [lag1..max_lag]。"""
    curve = []
    for lag in range(1, max_lag + 1):
        s_ab = s_aa = s_bb = 0.0
        if axis == "x":
            for yy in range(h):
                row = yy * w
                for xx in range(w - lag):
                    a = d[row + xx]
                    b = d[row + xx + lag]
                    s_ab += a * b
                    s_aa += a * a
                    s_bb += b * b
        else:
            for yy in range(h - lag):
                base = yy * w
                nxt = (yy + lag) * w
                for xx in range(w):
                    a = d[base + xx]
                    b = d[nxt + xx]
                    s_ab += a * b
                    s_aa += a * a
                    s_bb += b * b
        den = math.sqrt(s_aa * s_bb) if (s_aa > 0 and s_bb > 0) else 0.0
        v = (s_ab / den) if den > 0 else 0.0
        curve.append(max(-1.0, min(1.0, v)))
    return curve


def corr_len(curve, thresh=0.5):
    """第一次跌破 thresh 的 lag（线性插值）；全曲线都高于 thresh 时返回最大 lag。"""
    for i in range(1, len(curve)):
        if curve[i] < thresh:
            prev = curve[i - 1]
            frac = 0.0 if prev <= thresh else (prev - thresh) / (prev - curve[i])
            return (i) + frac  # lag i 处是 i（i=1 对应 lag1）；前一点是 lag i
    return float(len(curve))


def discrete_peak(curve, min_lag=6):
    """lag>=min_lag 上的局部极大值，返回 (peak_value, peak_lag)。"""
    best_v, best_l = 0.0, 0
    for i in range(min_lag - 1, len(curve) - 1):
        v = curve[i]
        if v > 0.0 and v >= curve[i - 1] and v >= curve[i + 1] and v > best_v:
            best_v, best_l = v, i + 1
    return best_v, best_l


def measure_src(src, w, h, max_lag=None, ground=False):
    max_lag = max_lag or w // 2
    d = detrend(src, w, h)
    grain = (sum(v * v for v in d) / len(d)) ** 0.5
    cx = autocorr_curve(d, w, h, "x", max_lag)
    cy = autocorr_curve(d, w, h, "y", max_lag)
    cxl, cyl = corr_len(cx), corr_len(cy)
    # 只在「真正有变化」的那个方向上找离散纹样：相关长度短的一侧才有结构，
    # 沿常量的方向自相关恒为 1，峰值没有意义（正弦波纹在 y 方向恒为 1 就是这种情形）。
    primary = cx if cxl <= cyl else cy
    peak, lag = discrete_peak(primary)
    aniso = (max(cxl, cyl) / min(cxl, cyl)) if min(cxl, cyl) > 0.05 else 99.0
    if grain < 1.5:
        verdict = "NODETAIL"
    elif peak >= 0.40:
        verdict = "PATTERN"
    elif aniso >= 1.8 and not ground:
        # 注意：地面在掠射角下被透视压缩，各向异性是几何必然（近景偏横向拉长），
        # 所以量地面时必须加 --ground，否则会把透视当成涂抹。
        verdict = "SMEAR"
    else:
        verdict = "OK"
    return dict(grain=grain, corrX=cxl, corrY=cyl, aniso=aniso,
                peak=peak, peak_lag=lag, verdict=verdict)


def measure_patch(path, x, y, w, h, max_lag=None, ground=False):
    return measure_src(decode_gray(path, x, y, w, h), w, h, max_lag, ground)


def synth(kind, w, h, seed=12345):
    """自建控制图（不用 ffmpeg 的 random：它的序列有行间相关，会把控制组污染）。"""
    def h32(a, b):
        v = ((a * 73856093) ^ (b * 19349663) ^ seed) & 0xFFFFFFFF
        v = (v * 0x9E3779B1) & 0xFFFFFFFF
        v ^= v >> 15
        return (v & 0xFFFF) / 65535.0

    out = []
    for y in range(h):
        for x in range(w):
            if kind == "noise":
                v = 128 + (h32(x, y) - 0.5) * 60
            elif kind == "ripple":
                v = 128 + 40 * math.sin(x / 16.0 * 2 * math.pi)
            elif kind == "checker":
                v = 128 + 40 * (((x // 16) + (y // 16)) % 2 * 2 - 1)
            elif kind == "flat":
                v = 120 + y / 8.0
            elif kind == "pixelgrain":
                v = 128 + (h32(x // 4, y // 4) - 0.5) * 50
            elif kind == "smear":  # 横向涂抹：x 方向 13 px 均值（模拟各向异性过滤拉丝）
                acc = 0.0
                for dx in range(-6, 7):
                    xx = min(w - 1, max(0, x + dx))
                    acc += h32(xx, y)
                v = 128 + (acc / 13.0 - 0.5) * 60
            else:
                v = 128
            out.append(v)
    return out


def selftest():
    w = h = 128
    res, checks = {}, []
    for kind in ("noise", "pixelgrain", "ripple", "checker", "smear", "flat"):
        m = measure_src(synth(kind, w, h), w, h, 40)
        res[kind] = m
        print("[SELFTEST] %-10s grain=%5.2f corrX=%5.1f corrY=%5.1f aniso=%5.2f peak=%5.2f(lag %2d) -> %s"
              % (kind, m["grain"], m["corrX"], m["corrY"], m["aniso"], m["peak"], m["peak_lag"], m["verdict"]))
    checks = [
        ("noise 判 OK(颗粒)", res["noise"]["verdict"] == "OK"),
        ("pixelgrain 判 OK(颗粒)", res["pixelgrain"]["verdict"] == "OK"),
        ("pixelgrain 尺度<=4px", max(res["pixelgrain"]["corrX"], res["pixelgrain"]["corrY"]) <= 4.0),
        ("ripple 判 PATTERN", res["ripple"]["verdict"] == "PATTERN"),
        ("ripple 主周期=16", abs(res["ripple"]["peak_lag"] - 16) <= 1),
        ("checker 判 PATTERN", res["checker"]["verdict"] == "PATTERN"),
        ("smear 判 SMEAR", res["smear"]["verdict"] == "SMEAR"),
        ("smear 方向性>=1.8", res["smear"]["aniso"] >= 1.8),
        ("flat 判 NODETAIL", res["flat"]["verdict"] == "NODETAIL"),
        ("峰值在 0..1", all(0.0 <= r["peak"] <= 1.0 for r in res.values())),
        ("相关长度>=1", all(min(r["corrX"], r["corrY"]) >= 0.9 for r in res.values())),
    ]
    ok = True
    for label, good in checks:
        print("[SELFTEST] %-22s %s" % (label, "PASS" if good else "FAIL"))
        ok = ok and good
    print("[SELFTEST] result=%s" % ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


def scan(path, rect, patch, step, quiet=False, ground=False):
    x0, y0, x1, y1 = rect
    results = []
    yy = y0
    while yy + patch <= y1:
        xx = x0
        while xx + patch <= x1:
            m = measure_patch(path, xx, yy, patch, patch, ground=ground)
            m.update(x=xx, y=yy)
            results.append(m)
            if not quiet:
                print("[PATCH] %4d,%4d grain=%5.2f corrX=%4.1f corrY=%4.1f aniso=%5.2f peak=%5.2f -> %s"
                      % (xx, yy, m["grain"], m["corrX"], m["corrY"], m["aniso"], m["peak"], m["verdict"]))
            xx += step
        yy += step
    if not results:
        print("[SURFACE] no patch fits in rect")
        return 1
    def med(key):
        vals = sorted(r[key] for r in results)
        return vals[len(vals) // 2]
    counts = {}
    for r in results:
        counts[r["verdict"]] = counts.get(r["verdict"], 0) + 1
    print("[SURFACE] patches=%d grain_med=%.2f corrX_med=%.1f corrY_med=%.1f aniso_med=%.2f peak_med=%.2f"
          % (len(results), med("grain"), med("corrX"), med("corrY"), med("aniso"), med("peak")))
    print("[SURFACE] tally=%s" % counts)
    return 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["selftest", "scan"])
    ap.add_argument("image", nargs="?")
    ap.add_argument("--rect", default="0,0,0,0")
    ap.add_argument("--patch", type=int, default=128)
    ap.add_argument("--step", type=int, default=64)
    ap.add_argument("--quiet", action="store_true")
    ap.add_argument("--ground", action="store_true",
                    help="地面/斜坡：透视压缩会让 aniso 必然偏大，只判 PATTERN")
    a = ap.parse_args()
    if a.mode == "selftest":
        return selftest()
    if not a.image:
        print("need IMAGE", file=sys.stderr)
        return 2
    rect = tuple(int(v) for v in a.rect.split(","))
    return scan(a.image, rect, a.patch, a.step, a.quiet, a.ground)


if __name__ == "__main__":
    sys.exit(main())
