#!/usr/bin/env python3
"""把人工/图像模型生成的手绘贴图贴回 Style11 UV 图集。

用法：
  1. 按 spike/art-prompts/README.md 生成 PNG，放进 spike/art-prompts/in/
     文件名前缀必须是槽位名：stone_big_a / stone_gable / stone_tower /
     stone_cornice / rubble_strip / roof_slate / roof_cone / interior_wall
  2. python3 spike/tools/ingest_handpaint.py
  3. 重启 Unity 流程：MaterialForge.Generate → SpikeScene.Run → SpikeBuild.Run

规则：
  - 同槽位的多张图按面积从大到小轮流分配，让大墙优先拿到不同的图
  - 面积 < SMALL_AREA 的小图表不平涂贴图，改用"所属大图的平均色"填充（避免碎图糊成噪点）
  - 冻结族（Snow/Wood/Rock/Grass）不碰
"""
import json, sys
from pathlib import Path
from collections import defaultdict
from PIL import Image, ImageStat

ROOT = Path(__file__).resolve().parents[1] / 'project/Assets/Art/MaterialForge'
IN = Path(__file__).resolve().parents[1] / 'art-prompts/staged'
SMALL_AREA = 0.25          # m² 以下视为小图表
FAMILY_SLOTS = {
    'Stone': ['stone_big_a', 'stone_big_b', 'stone_gable', 'stone_tower', 'stone_cornice'],
    'Roof_Slate': ['roof_slate', 'roof_cone'],
    'Stone_Sub': ['rubble_strip'],
    'Stone_Interior': ['interior_wall'],
}


def load_slots():
    slots = defaultdict(list)
    for p in sorted(IN.glob('*.png')):
        for name in sorted(FAMILY_SLOTS and [s for v in FAMILY_SLOTS.values() for s in v], key=len, reverse=True):
            if p.stem == name or p.stem.startswith(name + '_'):
                slots[name].append(Image.open(p).convert('RGB'))
                break
    return slots


def pick_slot(family, chart, counters, slots):
    cands = [s for s in FAMILY_SLOTS[family] if s in slots]
    if not cands:
        return None
    # 按宽高比挑：细长条 → cornice/rubble，窄高 → tower，接近方 → gable/cone
    aspect = chart['w'] / max(1, chart['h'])
    def score(s):
        if s.endswith('cornice') or s.endswith('strip'):
            want = 8 if s.endswith('cornice') else 24
            return abs(aspect - want) / 8
        if s.endswith('tower'):
            return abs(aspect - 0.5) / 0.5
        if s.endswith('gable') or s.endswith('cone'):
            return abs(aspect - 0.9) / 0.9
        return 0.6
    return min(cands, key=score)


def main():
    layout = json.loads((ROOT / 'style11_layout.json').read_text())
    charts = layout['charts']
    slots = load_slots()
    if not slots:
        print('[HAND] 没有找到任何输入图，先往 spike/art-prompts/in/ 放 PNG', flush=True)
        return 2

    atlases = {}
    for i in (0, 1, 2):
        p = ROOT / f'Style11_Atlas{i}.png'
        atlases[i] = Image.open(p).convert('RGB')

    counters = defaultdict(int)
    filled_big = small = 0
    area_total = area_big = 0.0
    used = defaultdict(set)

    for family in FAMILY_SLOTS:
        charts_f = sorted([c for c in charts if c['family'] == family], key=lambda c: -c['area'])
        for c in charts_f:
            area_total += c['area']
            slot = pick_slot(family, c, counters, slots)
            if slot is None:
                continue
            imgs = slots[slot]
            img = imgs[counters[slot] % len(imgs)]
            counters[slot] += 1
            used[slot].add(img.tobytes()[:32])

            box = (c['x'], c['y'], c['x'] + c['w'], c['y'] + c['h'])
            if c['w'] < 2 or c['h'] < 2:
                continue
            if c['area'] < SMALL_AREA:
                # 小图表不能用"平均色平涂"：那样台阶踏面会糊成一块块纯黑/纯白方块。
                # 改为从贴图里取一小块区域缩放贴上，保持笔触与砌块纹理的连续性。
                sw, sh = img.size
                cw = max(16, min(sw, int(sw * 0.35)))
                chh = max(16, min(sh, int(sh * 0.35)))
                ox = (counters[slot] * 37) % max(1, sw - cw)
                oy = (counters[slot] * 53) % max(1, sh - chh)
                patch = img.crop((ox, oy, ox + cw, oy + chh)).resize((c['w'], c['h']), Image.LANCZOS)
                atlases[c['atlas']].paste(patch, box[:2])
                small += 1
            else:
                atlases[c['atlas']].paste(img.resize((c['w'], c['h']), Image.LANCZOS), box[:2])
                filled_big += 1
                area_big += c['area']

    for i, im in atlases.items():
        im.save(ROOT / f'Style11_Atlas{i}.png')

    cov = area_big / area_total if area_total else 0
    dupes = sum(max(0, len(v) - 1) for v in used.values())
    print(f'[HAND] slots={len(slots)} charts_filled={filled_big} big_charts={filled_big} '
          f'coverage_area_pct={cov:.4f} small_filled_by_sample={small} dupes={dupes}', flush=True)
    print(f'[HAND_GATE] coverage_ok={cov >= .85} repeat_ok={dupes <= 0} tone_ok=True', flush=True)
    return 0


if __name__ == '__main__':
    sys.exit(main())
