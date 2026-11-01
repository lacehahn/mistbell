#!/usr/bin/env python3
"""生成 64x80 的像素朝圣者精灵表（4 方向 × 3 帧），纯标准库写 PNG。

画布 64x80，人物高 56 px（32 PPU 下约 1.75 m），脚底距画布底 4 px。
输出：spike/project/Assets/Art/Character/pilgrim.png（192x320 图集）
"""
import os
import struct
import zlib

W, H = 64, 80
FRAMES = 3
DIRS = ["down", "up", "left", "right"]

PAL = {
    "cloak": (0x2F, 0x3A, 0x46, 255),
    "cloak_hi": (0x44, 0x53, 0x63, 255),
    "hood": (0x3D, 0x4A, 0x58, 255),
    "face": (0xE8, 0xC9, 0xA0, 255),
    "face_sh": (0xB9, 0x96, 0x74, 255),
    "eye": (0x1B, 0x20, 0x26, 255),
    "staff": (0x6B, 0x4F, 0x2A, 255),
    "boot": (0x1D, 0x24, 0x2C, 255),
    "trim": (0x8C, 0x6B, 0x3F, 255),
    "pack": (0x53, 0x3F, 0x2C, 255),
}


def blank():
    return [[(0, 0, 0, 0) for _ in range(W)] for _ in range(H)]


def px(img, x, y, color):
    if 0 <= x < W and 0 <= y < H:
        img[y][x] = color


def rect(img, x0, y0, x1, y1, color):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px(img, x, y, color)


def draw_figure(frame, direction):
    img = blank()
    bob = [0, -1, 0][frame]          # 迈步时身体上浮 1 px
    leg = [0, 1, -1][frame]          # 腿部偏移

    cx = 32
    feet_y = 76 + bob
    head_top = 20 + bob
    head_bottom = head_top + 14
    body_top = head_bottom - 2
    side = direction in ("left", "right")

    # 斗篷
    half_top = 7 if not side else 5
    half_bottom = 11 if not side else 8
    for y in range(body_top, feet_y + 1):
        t = (y - body_top) / max(1, feet_y - body_top)
        half = int(half_top + (half_bottom - half_top) * t)
        shade = PAL["cloak"] if (x_shift := 0) is None else PAL["cloak"]
        for x in range(cx - half, cx + half + 1):
            px(img, x, y, shade)
    # 斗篷左侧高光边
    for y in range(body_top, feet_y + 1):
        t = (y - body_top) / max(1, feet_y - body_top)
        half = int(half_top + (half_bottom - half_top) * t)
        px(img, cx - half, y, PAL["cloak_hi"])

    # 下摆金边
    t = 1.0
    half = half_bottom
    for x in range(cx - half, cx + half + 1):
        px(img, x, feet_y, PAL["trim"])

    # 兜帽 + 头
    rect(img, cx - 6, head_top, cx + 6, head_bottom, PAL["hood"])
    rect(img, cx - 5, head_top + 2, cx + 5, head_bottom - 1, PAL["hood"])
    if direction == "down":
        rect(img, cx - 4, head_top + 5, cx + 4, head_bottom - 2, PAL["face"])
        rect(img, cx - 4, head_bottom - 3, cx + 4, head_bottom - 1, PAL["face_sh"])
        px(img, cx - 2, head_top + 8, PAL["eye"])
        px(img, cx + 2, head_top + 8, PAL["eye"])
    elif direction == "up":
        rect(img, cx - 4, head_top + 3, cx + 4, head_bottom - 1, PAL["hood"])
        rect(img, cx - 3, head_top + 6, cx + 3, head_bottom - 2, PAL["pack"])
    else:
        face_x = cx + 1 if direction == "right" else cx - 3
        rect(img, face_x, head_top + 6, face_x + 2, head_bottom - 2, PAL["face"])
        px(img, face_x + (1 if direction == "right" else 1), head_top + 8, PAL["eye"])

    # 靴子（两帧错开）
    boot_w = 3 if not side else 4
    rect(img, cx - 5 + leg, feet_y - 2, cx - 5 + leg + boot_w, feet_y, PAL["boot"])
    rect(img, cx + 2 - leg, feet_y - 2, cx + 2 - leg + boot_w, feet_y, PAL["boot"])

    # 手杖（左右侧的人像把杖放近侧）
    staff_x = cx + 9 if direction != "left" else cx - 10
    for y in range(head_top + 4, feet_y + 1):
        px(img, staff_x, y, PAL["staff"])
        px(img, staff_x + 1, y, PAL["staff"])
    px(img, staff_x - 1, head_top + 3, PAL["staff"])
    px(img, staff_x + 2, head_top + 3, PAL["staff"])

    return img


def write_png(path, width, height, rows):
    raw = b"".join(b"\x00" + bytes(c for p in row for c in p) for row in rows)

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    blob = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as fh:
        fh.write(blob)
    return len(blob)


def main():
    out = os.path.expanduser("~/workspace/mistbell/spike/project/Assets/Art/Character/pilgrim.png")
    sheet = [[(0, 0, 0, 0) for _ in range(W * FRAMES)] for _ in range(H * len(DIRS))]
    for row, direction in enumerate(DIRS):
        for col in range(FRAMES):
            img = draw_figure(col, direction)
            for y in range(H):
                for x in range(W):
                    sheet[row * H + y][col * W + x] = img[y][x]
    size = write_png(out, W * FRAMES, H * len(DIRS), sheet)
    print("wrote", out, size, "bytes;", W * FRAMES, "x", H * len(DIRS))


if __name__ == "__main__":
    main()
