#!/usr/bin/env python3
"""读 PNG（纯标准库）并对指定区域求平均色，用来量化画面亮度过曝。用法：pngstat.py a.png [b.png]"""
import sys
import struct
import zlib


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
        i += 1
        line = bytearray(raw[i:i + stride])
        i += stride
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
                b = prev[x]
                c = prev[x - bpp] if x >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 255
        rows.append(line)
        prev = line
    return w, h, bpp, rows


def stat(rows, bpp, x0, y0, x1, y1, step=3):
    s, n, mx = [0, 0, 0], 0, 0
    for y in range(y0, y1, step):
        row = rows[y]
        for x in range(x0, x1, step):
            r, g, b = row[x * bpp], row[x * bpp + 1], row[x * bpp + 2]
            s[0] += r
            s[1] += g
            s[2] += b
            mx = max(mx, r, g, b)
            n += 1
    return tuple(v // max(n, 1) for v in s), mx, n


REGIONS = {
    "sky":        (150, 80, 500, 220),
    "ground_far": (150, 300, 500, 380),
    "ground_near": (150, 850, 700, 1000),
}


def main():
    for path in sys.argv[1:]:
        w, h, bpp, rows = read_png(path)
        print(path, w, "x", h, "bpp", bpp)
        for name, (x0, y0, x1, y1) in REGIONS.items():
            mean, mx, n = stat(rows, bpp, x0, y0, x1, y1)
            print("   %-12s mean=%s max=%d n=%d" % (name, mean, mx, n))
        # 直方图：统计过曝（>=250）像素占比
        bright = total = 0
        for y in range(0, h, 5):
            row = rows[y]
            for x in range(0, w, 5):
                v = max(row[x * bpp], row[x * bpp + 1], row[x * bpp + 2])
                bright += 1 if v >= 250 else 0
                total += 1
        print("   clipped>=250: %.2f%%  (%d/%d)" % (100.0 * bright / total, bright, total))


if __name__ == "__main__":
    main()
