#!/usr/bin/env python3
"""生成 HD-2D 场景用的可平铺像素材质（128×128 颜色 + 由高度推导的法线图），纯标准库。

为什么这么做：体块教堂/地面此前是纯色块，HD-2D 的「立体绘本」感来自
「高分辨率有细节的环境 + 像素角色」，所以环境必须先有材质层次与法线起伏。

输出（覆盖写）：
  Assets/Art/Textures/{grass,stone_ashlar,roof_slate}.png      颜色（RGB，可平铺）
  Assets/Art/Textures/{grass,stone_ashlar,roof_slate}_n.png    法线（RGB，切线空间，可平铺）
  另在 spike/scratch/tex/ 下写 4× 平铺预览，仅供人眼检查接缝

贴图实际尺寸约定（与 SpikeArt.cs 的 metersPerTile 对齐）：
  grass 1 张 = 3.2 m（细密草丛，近景才看得出像素）
  stone_ashlar 1 张 = 2.4 m（4 皮石砌，每皮 0.6 m）
  roof_slate 1 张 = 2.4 m（4 排石板瓦）
"""
import os
import random
import struct
import zlib

SZ = 128
ROOT = os.path.expanduser("~/workspace/mistbell/spike")
OUT = os.path.join(ROOT, "project/Assets/Art/Textures")
PREVIEW = os.path.join(ROOT, "scratch/tex")


# ---------------------------------------------------------------- 基础工具
def smooth(t):
    return t * t * (3.0 - 2.0 * t)


def value_noise(cell, rnd):
    """可环绕的值噪声：cell 必须能整除 SZ，保证左右/上下接缝连续。"""
    assert SZ % cell == 0, cell
    cw = SZ // cell
    lat = [[rnd.random() for _ in range(cw)] for _ in range(cw)]
    out = [[0.0] * SZ for _ in range(SZ)]
    for y in range(SZ):
        fy = y / cell
        y0 = int(fy) % cw
        y1 = (y0 + 1) % cw
        ty = smooth(fy - int(fy))
        for x in range(SZ):
            fx = x / cell
            x0 = int(fx) % cw
            x1 = (x0 + 1) % cw
            tx = smooth(fx - int(fx))
            a = lat[y0][x0]
            b = lat[y0][x1]
            c = lat[y1][x0]
            d = lat[y1][x1]
            top = a + (b - a) * tx
            bot = c + (d - c) * tx
            out[y][x] = top + (bot - top) * ty
    return out


def fbm(cells, rnd, gain=0.5):
    """多八度叠加，归一化到 0..1。"""
    acc = [[0.0] * SZ for _ in range(SZ)]
    amp = 1.0
    total = 0.0
    for cell in cells:
        n = value_noise(cell, rnd)
        for y in range(SZ):
            row = acc[y]
            nr = n[y]
            for x in range(SZ):
                row[x] += nr[x] * amp
        total += amp
        amp *= gain
    for y in range(SZ):
        for x in range(SZ):
            acc[y][x] /= total
    return acc


def lerp(a, b, t):
    return a + (b - a) * t


def mix(c0, c1, t):
    return (lerp(c0[0], c1[0], t), lerp(c0[1], c1[1], t), lerp(c0[2], c1[2], t))


def clamp8(v):
    return 0 if v < 0 else (255 if v > 255 else int(v + 0.5))


def new_img(base=(0, 0, 0)):
    return [[base for _ in range(SZ)] for _ in range(SZ)]


def put(img, x, y, c):
    """可环绕写点（保证平铺接缝）。"""
    img[y % SZ][x % SZ] = c


def add(img, x, y, d):
    """可环绕的加性提亮/压暗。"""
    r, g, b = img[y % SZ][x % SZ]
    img[y % SZ][x % SZ] = (clamp8(r + d), clamp8(g + d), clamp8(b + d))


# ---------------------------------------------------------------- 草地
def make_grass(rnd):
    base_dark = (0x3A, 0x44, 0x2E)
    base_mid = (0x52, 0x5E, 0x3D)
    base_light = (0x6C, 0x78, 0x4A)
    dry = (0x84, 0x7C, 0x55)

    low = fbm([32, 16, 8], rnd)      # 大块明暗（草皮斑块）
    mid = fbm([8, 4, 2], rnd)        # 中等草丛
    fine = fbm([2], rnd)             # 每 2 px 一级的细节

    img = new_img()
    for y in range(SZ):
        for x in range(SZ):
            n = low[y][x]
            c = mix(base_dark, base_mid, min(1.0, n * 1.6))
            c = mix(c, base_light, max(0.0, (mid[y][x] - 0.55)) * 1.1)
            c = mix(c, dry, max(0.0, (low[y][x] - 0.72)) * 0.9)   # 干草斑
            j = (fine[y][x] - 0.5) * 16.0                          # 逐像素颗粒
            img[y][x] = (clamp8(c[0] + j), clamp8(c[1] + j), clamp8(c[2] + j))

    # 草叶：2–3 px 的竖直短笔触，亮/暗交替
    for _ in range(900):
        x = rnd.randrange(SZ)
        y = rnd.randrange(SZ)
        h = rnd.choice((2, 2, 3, 4))
        bright = rnd.random() < 0.55
        col = mix(base_light, (0x8F, 0x9C, 0x62), rnd.random()) if bright \
            else mix(base_dark, (0x2C, 0x34, 0x24), rnd.random())
        for k in range(h):
            img[(y + k) % SZ][x] = (clamp8(col[0]), clamp8(col[1]), clamp8(col[2]))
        if rnd.random() < 0.35:                       # 一片叶子向侧倾 1 px
            put(img, x + rnd.choice((-1, 1)), y + h - 1, (clamp8(col[0]), clamp8(col[1]), clamp8(col[2])))

    # 深色土斑与碎石（打破均匀感）
    for _ in range(26):
        cx, cy = rnd.randrange(SZ), rnd.randrange(SZ)
        r = rnd.choice((2, 3, 4))
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r and rnd.random() < 0.75:
                    add(img, cx + dx, cy + dy, rnd.choice((-14, -9, -5)))
    for _ in range(30):
        put(img, rnd.randrange(SZ), rnd.randrange(SZ), (0x8E, 0x8B, 0x80))

    # 高山小花（白/淡黄 1 px，很克制）
    for _ in range(18):
        put(img, rnd.randrange(SZ), rnd.randrange(SZ), rnd.choice(((0xD9, 0xD6, 0xC0), (0xC9, 0xBE, 0x86))))

    return img, 1.15


# ---------------------------------------------------------------- 石砌墙面
def block_widths(rnd, total):
    """随机块宽（4 的倍数）拼满一圈，避免等宽网格那种「瓷砖」观感。"""
    widths = []
    left = total
    while left > 0:
        w = rnd.choice((28, 32, 36, 40, 44, 48, 52, 56))
        if left - w < 24:            # 收尾别留窄条
            w = left
        widths.append(w)
        left -= w
    return widths


def make_stone(rnd):
    courses, row_h = 4, SZ // 4          # 4 皮，每皮 0.6 m
    img = new_img((0x4E, 0x49, 0x42))    # 灰浆底色
    height = [[0.5] * SZ for _ in range(SZ)]

    grain = fbm([16, 8, 2], rnd, 0.55)
    blotch = fbm([32, 16], rnd)

    # 每皮一套宽度布局并错缝半块，接缝处靠取模环绕
    layouts = []
    for course in range(courses):
        widths = block_widths(rnd, SZ)
        offset = widths[0] // 2 if course % 2 else 0
        layouts.append((widths, offset))
        x = offset
        for w in widths:
            tone = 0.56 + rnd.uniform(-0.15, 0.17)
            warm = rnd.uniform(-0.04, 0.06)
            y0 = course * row_h
            top = y0 + 2
            bottom = y0 + row_h - 3
            for yy in range(top, bottom + 1):
                for xx in range(x + 1, x + w - 1):
                    g = grain[yy % SZ][xx % SZ]
                    v = tone + (g - 0.5) * 0.20 + (blotch[yy % SZ][xx % SZ] - 0.5) * 0.12
                    img[yy % SZ][xx % SZ] = (clamp8(v * 236 + warm * 90),
                                             clamp8(v * 232 + warm * 40),
                                             clamp8(v * 222 - warm * 30))
                    height[yy % SZ][xx % SZ] = 0.6 + (g - 0.5) * 0.45
            # 柔和的手工明暗：上缘受光、下缘与灰浆接触处压暗（低分辨率下也读得出体积）
            for xx in range(x + 1, x + w - 1):
                add(img, xx, top, 8)
                add(img, xx, top + 1, 4)
                add(img, xx, bottom, -14)
                height[y0 % SZ][xx % SZ] = 0.0
            for yy in range(top, bottom + 1):
                add(img, x + 1, yy, 5)
                add(img, x + w - 2, yy, -12)
                height[yy % SZ][x % SZ] = 0.0
            # 偶发裂纹
            if rnd.random() < 0.28:
                cx = x + 3 + rnd.randrange(max(1, w - 8))
                cy = top + rnd.randrange(max(1, bottom - top - 6))
                for k in range(rnd.randrange(3, 7)):
                    put(img, cx + (k // 3), cy + k, (0x3C, 0x38, 0x33))
            x += w

    # 皮间灰浆：2 px 暗缝 + 高度下陷
    for course in range(courses):
        for y in ((course * row_h) % SZ, (course * row_h + 1) % SZ, (course * row_h + row_h - 1) % SZ):
            for x in range(SZ):
                add(img, x, y, -22)
                height[y][x] = 0.05

    # 竖向风化 streaks（顺墙流下的水痕），让墙面不再是均匀噪点
    for _ in range(16):
        x = rnd.randrange(SZ)
        y = rnd.randrange(SZ)
        n = rnd.randrange(6, 20)
        d = rnd.choice((-10, -7, -5, 5))
        for k in range(n):
            add(img, x, y + k, d)
            if rnd.random() < 0.3:
                add(img, x + 1, y + k, d // 2)

    return img, 3.0, height


# ---------------------------------------------------------------- 石板瓦屋面
def make_roof(rnd):
    rows, row_h = 4, SZ // 4
    tw = SZ // 4
    img = new_img((0x40, 0x2C, 0x26))
    height = [[0.5] * SZ for _ in range(SZ)]
    grain = fbm([16, 8, 2], rnd, 0.55)

    for r in range(rows):
        y0 = r * row_h
        offset = (tw // 2) if r % 2 else 0
        for col in range(-1, 5):
            x0 = col * tw + offset
            tone = 0.58 + rnd.uniform(-0.18, 0.18)
            for y in range(y0, y0 + row_h):
                for x in range(x0, x0 + tw):
                    g = grain[y % SZ][x % SZ]
                    v = tone + (g - 0.5) * 0.26
                    img[y % SZ][x % SZ] = (clamp8(v * 210), clamp8(v * 152), clamp8(v * 130))
                    height[y % SZ][x % SZ] = 0.45 + (g - 0.5) * 0.4
            # 瓦片上缘受光唇、下缘被上一排压住的 3 px 深影 —— 搭接感全靠这两条
            for x in range(x0, x0 + tw):
                add(img, x, y0, 18)
                add(img, x, y0 + row_h - 1, -46)
                add(img, x, y0 + row_h - 2, -30)
                add(img, x, y0 + row_h - 3, -14)
                height[y0 % SZ][x % SZ] = 1.0
                height[(y0 + row_h - 1) % SZ][x % SZ] = 0.0
            # 竖向瓦缝
            for y in range(y0, y0 + row_h):
                add(img, x0, y, -20)
                height[y % SZ][x0 % SZ] = 0.1

    return img, 2.4, height


# ---------------------------------------------------------------- 法线
def normal_map(img, height=None, strength=2.0):
    """由亮度（或传入高度场）推切线空间法线，采样全程环绕以保持可平铺。"""
    if height is None:
        lum = [[(0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]) / 255.0 for p in row] for row in img]
    else:
        lum = height
    n_img = new_img()
    for y in range(SZ):
        for x in range(SZ):
            dx = lum[y][(x + 1) % SZ] - lum[y][(x - 1) % SZ]
            dy = lum[(y + 1) % SZ][x] - lum[(y - 1) % SZ][x]
            nx = -dx * strength
            ny = dy * strength          # Unity 法线图：绿通道指向上（+V）
            nz = 1.0
            ln = (nx * nx + ny * ny + nz * nz) ** 0.5
            n_img[y][x] = (clamp8((nx / ln * 0.5 + 0.5) * 255),
                           clamp8((ny / ln * 0.5 + 0.5) * 255),
                           clamp8((nz / ln * 0.5 + 0.5) * 255))
    return n_img


# ---------------------------------------------------------------- PNG
def write_png(path, img, alpha=False):
    h = len(img)
    w = len(img[0])
    if alpha:
        raw = b"".join(b"\x00" + bytes(c for p in row for c in (p[0], p[1], p[2], 255)) for row in img)
        ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    else:
        raw = b"".join(b"\x00" + bytes(c for p in row for c in p[:3]) for row in img)
        ihdr = struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    blob = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as fh:
        fh.write(blob)
    return len(blob)


def preview(img, path, times=4, zoom=2):
    """4× 平铺 + 2× 最近邻放大：人眼检查接缝。"""
    n = SZ * times
    big = new_img((0, 0, 0))
    big = [[img[y % SZ][x % SZ] for x in range(n)] for y in range(n)]
    out = [[big[y // zoom][x // zoom] for x in range(n * zoom)] for y in range(n * zoom)]
    return write_png(path, out)


def seam_score(img):
    """接缝自检：首尾列/行差 vs 内部相邻列/行平均差，比值应接近 1。"""
    def diff_col(a, b):
        return sum(abs(img[y][a][0] - img[y][b][0]) for y in range(SZ)) / SZ
    def diff_row(a, b):
        return sum(abs(img[a][x][0] - img[b][x][0]) for x in range(SZ)) / SZ
    inner_c = sum(diff_col(x, x + 1) for x in range(SZ - 1)) / (SZ - 1)
    inner_r = sum(diff_row(y, y + 1) for y in range(SZ - 1)) / (SZ - 1)
    return diff_col(SZ - 1, 0) / max(inner_c, 0.01), diff_row(SZ - 1, 0) / max(inner_r, 0.01)


def main():
    jobs = [
        ("grass", make_grass, 11),
        ("stone_ashlar", make_stone, 23),
        ("roof_slate", make_roof, 37),
    ]
    for name, fn, seed in jobs:
        rnd = random.Random(seed)
        result = fn(rnd)
        img = result[0]
        strength = result[1]
        height = result[2] if len(result) > 2 else None
        col_bytes = write_png(os.path.join(OUT, name + ".png"), img)
        n_img = normal_map(img, height, strength)
        n_bytes = write_png(os.path.join(OUT, name + "_n.png"), n_img)
        preview(img, os.path.join(PREVIEW, name + "_tiled.png"))
        sc = seam_score(img)
        print("%-14s color=%5d B normal=%5d B seam(col,row)=%.2f,%.2f" % (name, col_bytes, n_bytes, sc[0], sc[1]))


if __name__ == "__main__":
    main()
