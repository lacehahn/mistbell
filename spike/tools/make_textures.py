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


def make_stone(rnd, size=512):
    import colorsys
    import math

    # 独立尺寸与周期值噪声，不改变其他材质使用的 SZ / 随机序列。
    def stone_noise(cell):
        assert size % cell == 0
        count = size // cell
        lattice = [[rnd.random() for _ in range(count)] for _ in range(count)]
        noise = []
        for y in range(size):
            iy, fy = divmod(y, cell)
            ty = smooth(fy / cell)
            row = []
            for x in range(size):
                ix, fx = divmod(x, cell)
                tx = smooth(fx / cell)
                top = lerp(lattice[iy][ix], lattice[iy][(ix + 1) % count], tx)
                bot = lerp(lattice[(iy + 1) % count][ix],
                           lattice[(iy + 1) % count][(ix + 1) % count], tx)
                row.append(lerp(top, bot, ty))
            noise.append(row)
        return noise

    courses = 4
    mortar = (0x46, 0x46, 0x46)
    img = [[mortar for _ in range(size)] for _ in range(size)]
    height = [[0.05] * size for _ in range(size)]
    block_face = [[False] * size for _ in range(size)]
    grain = stone_noise(size // 16)
    fine_grain = stone_noise(size // 32)
    blotch = stone_noise(size // 2)
    low, high = min(map(min, blotch)), max(map(max, blotch))
    blotch = [[(v - low) / (high - low) for v in row] for row in blotch]
    base_h, base_l, base_s = colorsys.rgb_to_hls(0x8A / 255, 0x84 / 255, 0x78 / 255)
    min_w, max_w = math.ceil(size * 0.28 / 2.4), math.floor(size * 1.05 / 2.4)
    all_widths = []
    for course in range(courses):
        # 拒绝无法拼满一圈或宽度分布过窄的布局；不产生接缝窄条。
        while True:
            count = rnd.randint(4, 6)
            widths = [rnd.randint(min_w, max_w) for _ in range(count - 1)]
            widths.append(size - sum(widths))
            mean = size / count
            std = (sum((w - mean) ** 2 for w in widths) / count) ** 0.5
            if min_w <= widths[-1] <= max_w and std / mean >= 0.28:
                break
        all_widths.extend(widths)
        x0 = rnd.randrange(size)
        y0, y1 = round(course * size / courses), round((course + 1) * size / courses)

        for width in widths:
            left_joint, right_joint, top_joint, bottom_joint = [rnd.randint(1, 3) for _ in range(4)]
            joint_colors = []
            for _ in range(4):
                # 灰缝三通道共用偏移和抖动，饱和度为零；保留 15% 浅缝。
                pale = rnd.random() < 0.15
                offset = 0
                if not pale:
                    offset = rnd.randint(25, 45)
                    # 保留既有随机序列，避免后续块宽/布局随灰缝修正而变化。
                    rnd.randint(25, 45)
                    rnd.randint(25, 45)
                value = 104 if pale else 106 - offset
                joint_colors.append((value, value, value))
            hue = base_h + rnd.uniform(-2.5, 2.5) / 360
            direction = rnd.choice((-1, 1))
            # 减弱低频斑驳后，以块间明度差保留老石砌的粗尺度层次。
            light = base_l + direction * rnd.uniform(0.075, 0.095)
            color = tuple(v * 255 for v in colorsys.hls_to_rgb(hue % 1, light, min(0.12, base_s)))
            gradient = rnd.uniform(0.03, 0.06)
            for y in range(y0, y1):
                t = (y - y0) / (y1 - y0)
                for xx in range(x0, x0 + width):
                    x = xx % size
                    distances = (xx - x0, x0 + width - 1 - xx, y - y0, y1 - 1 - y)
                    joints = (left_joint, right_joint, top_joint, bottom_joint)
                    side = min(range(4), key=lambda i: distances[i] - joints[i])
                    edge = distances[side] - joints[side]
                    block_face[y][x] = edge >= 2
                    if edge < 0:
                        img[y][x] = joint_colors[side]
                        continue
                    variation = ((grain[y][x] - 0.5) * 34 +
                                 (fine_grain[y][x] - 0.5) * 40 +
                                 (blotch[y][x] - 0.5) * 50 - 7)
                    # 两像素倒角：0.20 → 0.80 → 满高；块心保留浅凹。
                    u = (xx - x0 - left_joint) / max(1, width - left_joint - right_joint - 1)
                    v = (y - y0 - top_joint) / max(1, y1 - y0 - top_joint - bottom_joint - 1)
                    bowl = max(0.0, 1 - (2 * u - 1) ** 2) * max(0.0, 1 - (2 * v - 1) ** 2)
                    bevel = (0.20, 0.80)[edge] if edge < 2 else 1.0
                    relief = -0.04 * bowl + (blotch[y][x] - 0.5) * 0.02
                    height[y][x] = bevel + relief + (grain[y][x] - 0.5) * 0.30
                    edge_dark = rnd.uniform(-14, -10) if edge == 0 else 0
                    relief_color = edge_dark + 4 * bowl
                    img[y][x] = tuple(clamp8(c * (1 - gradient * t) + variation + relief_color)
                                      for c in color)
            x0 += width

    # 3–5 条整墙竖向流痕，软边横向环绕，避免贴图边界截断。
    for _ in range(rnd.randint(3, 5)):
        center = rnd.randrange(size)
        width = rnd.randint(6, 20)
        alpha = rnd.uniform(0.05, 0.12)
        for y in range(size):
            for dx in range(-width // 2, (width + 1) // 2):
                x = (center + dx) % size
                weight = alpha * (1 - abs(dx) / (width / 2 + 1))
                img[y][x] = tuple(clamp8(v) for v in mix(img[y][x], mortar, weight))
    for y in range(size):
        # 底部积尘；在皮间灰缝中回到起点，平铺时明暗跳变落在灰缝内。
        dust = 0.08 * smooth(max(0.0, (y / (size - 1) - 0.8) / 0.2))
        for x in range(size):
            fine = rnd.uniform(-12, 12)
            if height[y][x] <= 0.1:
                fine *= 2 / 12  # 灰缝只用三通道共用的 ±2 抖动。
            # 少量砂眼打断平滑石面；不沿边界描出闭合轮廓。
            if height[y][x] > 0.1 and rnd.random() < 0.008:
                fine -= rnd.uniform(22, 38)
                height[y][x] -= 0.12
            # 亮砂粒只落在块面内部，避免沿倒角描边。
            if height[y][x] > 0.8 and rnd.random() < 0.012:
                fine += rnd.uniform(10, 18)
                height[y][x] += 0.06
            img[y][x] = tuple(clamp8(c * (1 - dust) + fine) for c in img[y][x])
    # 在原有随机流程之后叠加单 texel 中性砂粒，保留块形、倒角和灰缝。
    for y in range(size):
        for x in range(size):
            if block_face[y][x] and height[y][x] > 0.1:
                grit = rnd.uniform(-7, 7)
                img[y][x] = tuple(clamp8(c + grit) for c in img[y][x])
                height[y][x] += rnd.uniform(-0.04, 0.04)
    mean = sum(all_widths) / len(all_widths)
    std = (sum((w - mean) ** 2 for w in all_widths) / len(all_widths)) ** 0.5
    scale = 2.4 / size
    print("stone px=%d courses=%d blockW mean=%.4f std=%.4f min=%.4f max=%.4f" %
          (size, courses, mean * scale, std * scale, min(all_widths) * scale, max(all_widths) * scale))
    return img, 0.9, height


# ---------------------------------------------------------------- 石板瓦屋面
def make_roof(rnd):
    import colorsys

    rows, row_h = 4, SZ // 4
    tw = SZ // 4
    img = new_img()
    height = [[0.5] * SZ for _ in range(SZ)]
    grain = fbm([16, 8, 2], rnd, 0.55)
    base = colorsys.hls_to_rgb(215 / 360, 0.33, 0.10)
    # HLS S=0.10 对应 HSV S≈0.182；中性石粉降低色度以满足 HSV S≤0.16。
    neutral = sum(base) / 3
    base = tuple((c * 0.85 + neutral * 0.15) * 255 for c in base)

    for r in range(rows):
        y0 = r * row_h + row_h // 2
        offset = tw // 4 + ((tw // 2) if r % 2 else 0)
        for col in range(4):
            x0 = col * tw + offset
            tone = 0.62 + rnd.uniform(-0.08, 0.08)
            tile_base = tuple(c * tone / 0.62 for c in base)
            for dy in range(row_h):
                y = (y0 + dy) % SZ
                for dx in range(tw):
                    x = (x0 + dx) % SZ
                    g = grain[y][x]
                    factor = 1 + (g - 0.5) * 0.26 + rnd.uniform(-0.045, 0.045)
                    if dy == 0:
                        factor *= 1.12
                    elif dy >= row_h - 3:
                        factor *= (0.94, 0.88, 0.80)[dy - (row_h - 3)]
                    if dx == 0:
                        factor *= 0.86
                    factor = max(0.55, factor)
                    img[y][x] = tuple(clamp8(c * factor) for c in tile_base)
                    height[y][x] = 0.45 + (g - 0.5) * 0.4
                    if dy == 0:
                        height[y][x] = 1.0
                    elif dy == row_h - 1:
                        height[y][x] = 0.0
                    if dx == 0:
                        height[y][x] = 0.1

    return img, 2.4, height


# ---------------------------------------------------------------- 法线
def normal_map(img, height=None, strength=2.0, size=SZ):
    """由亮度（或传入高度场）推切线空间法线，采样全程环绕以保持可平铺。"""
    if height is None:
        lum = [[(0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]) / 255.0 for p in row] for row in img]
    else:
        lum = height
    # 调用方原样传入完整贴图；stone 使用自身的 256px 尺寸。
    if size == SZ and len(img) != SZ:
        return normal_map(img, height, strength, size=len(img))
    n_img = [[(0, 0, 0) for _ in range(size)] for _ in range(size)]
    for y in range(size):
        for x in range(size):
            dx = lum[y][(x + 1) % size] - lum[y][(x - 1) % size]
            dy = lum[(y + 1) % size][x] - lum[(y - 1) % size][x]
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
    h, w = len(img), len(img[0])
    out = [[img[(y // zoom) % h][(x // zoom) % w]
            for x in range(w * times * zoom)] for y in range(h * times * zoom)]
    return write_png(path, out)


def seam_score(img):
    """按真实尺寸、RGB 三通道计算，与 tools/seam.py 使用同一把尺子。"""
    h, w = len(img), len(img[0])
    def diff_col(a, b):
        return sum(abs(img[y][a][c] - img[y][b][c])
                   for y in range(h) for c in range(3)) / (h * 3)
    def diff_row(a, b):
        return sum(abs(img[a][x][c] - img[b][x][c])
                   for x in range(w) for c in range(3)) / (w * 3)
    inner_c = sum(diff_col(x, x + 1) for x in range(w - 1)) / (w - 1)
    inner_r = sum(diff_row(y, y + 1) for y in range(h - 1)) / (h - 1)
    return diff_col(w - 1, 0) / max(inner_c, 0.01), diff_row(h - 1, 0) / max(inner_r, 0.01)


def make_mist():
    """1024×256 周期软噪声；两端零斜率渐隐，RGBA alpha 供雾带及雪线复用。"""
    import math
    width, height = 1024, 256
    rnd = random.Random(59)
    octaves = []
    for nx, ny, weight in ((16, 4, 1.0), (32, 8, 0.35), (64, 16, 0.12)):
        lattice = [[rnd.random() for _ in range(nx)] for _ in range(ny)]
        octaves.append((nx, ny, weight, lattice))
    img = []
    for y in range(height):
        row = []
        envelope = math.sin(math.pi * y / (height - 1)) ** 2
        for x in range(width):
            value = 0.0
            for nx, ny, weight, lattice in octaves:
                fx, fy = x * nx / width, y * ny / height
                ix, iy = int(fx), int(fy)
                tx, ty = smooth(fx - ix), smooth(fy - iy)
                top = lerp(lattice[iy % ny][ix % nx], lattice[iy % ny][(ix + 1) % nx], tx)
                bot = lerp(lattice[(iy + 1) % ny][ix % nx], lattice[(iy + 1) % ny][(ix + 1) % nx], tx)
                value += lerp(top, bot, ty) * weight
            alpha = clamp8(255 * envelope * smooth(min(1.0, value / 1.47)))
            row.append((255, 255, 255, alpha))
        img.append(row)
    # 独立 RGBA 写出，不改变已有三类贴图或其 PNG 写出逻辑。
    raw = b"".join(b"\x00" + bytes(c for pixel in row for c in pixel) for row in img)
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff)
    blob = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    with open(os.path.join(OUT, "mist.png"), "wb") as fh:
        fh.write(blob)
    print("mist           RGBA=1024x256 bytes=%d alpha=soft periodic noise" % len(blob))


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
        print("%-14s color=%5d B normal=%5d B seam(col,row)@%dx%d=%.2f,%.2f" %
              (name, col_bytes, n_bytes, len(img[0]), len(img), sc[0], sc[1]))

    make_mist()


if __name__ == "__main__":
    main()
