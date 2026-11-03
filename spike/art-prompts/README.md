# 手绘贴图 · 出图提示词包（v12 材质方案）

目标：用**图像模型**生成手绘石墙/屋顶贴图，再按 UV 图集槽位贴回 Unity。

## 一、总风格提示词（每条 prompt 前面都要带）

```
hand-painted 2D game background texture, HD-2D RPG style (Octopath Traveler),
flat painted lighting, no photorealism, no 3D render, no specular highlights,
visible brush strokes, posterized 5-6 value steps, soft irregular edges,
warm ochre limestone (mid tone #8C7B5F, light #B9A98C, shadow #5D5142),
cool grey-blue for joints and recesses, thin 1px mortar lines that fade in and out,
subtle damp streaks and lichen at the bottom, no text, no logo, no watermark,
seamless only where noted, texture fills the whole frame, top-down orthographic view of a wall
```

**负向提示词（所有图都要加）**

```
photo, realistic, PBR, glossy, plastic, metallic, high contrast rim light,
blurry, depth of field, perspective, vignette, characters, windows, cross, sky,
text, letters, ui, frame, border
```

## 二、按部件的提示词（在总提示词后追加）

### A. 大墙面（Stone，宽高比 ~1.4:1，例如 19×13.6 m）
```
large ashlar limestone wall, blocks 40-90cm wide and 28-45cm tall,
courses slightly wavy (+-3px), 30% of blocks have chipped corners,
masonry gets smaller toward the top, two or three large quoin stones at the left edge,
a 3% tall band of thick snow sitting on the very top edge with 2-3 drips,
warm sunlight from upper left: top edges +18% warmer, recesses -12% cooler,
surface mostly flat with a few damp streaks, 5-6 value steps, 12-16 colors total
```
参考槽位：`stone_big_a`（1024×512）、`stone_big_b`（1024×512）

### B. 山墙面 / 三角区（Stone，宽高比 ~1:1.2）
```
limestone gable triangle wall, stones arranged in horizontal courses that thin toward the apex,
a snow line along the raking edges, small irregular stones filling the corners,
same palette and lighting as A
```
参考槽位：`stone_gable`（512×512）

### C. 塔身立面（Stone，宽高比 ~1:1.8，窄高）
```
tall narrow limestone tower wall, courses of small stones 25-45cm, slight lean,
arched window openings are NOT drawn (they are geometry), keep surface continuous,
snow line only at the very top, warm top edge light
```
参考槽位：`stone_tower`（256×512）

### D. 檐口 / 腰线（Stone 细长条，宽高比 ~8:1）
```
narrow horizontal limestone cornice strip, 1-2 courses only,
thick snow along its top edge with irregular overhang, warm top light, cool under-shadow
```
参考槽位：`stone_cornice`（1024×128）

### E. 挡土墙碎石（Stone_Sub，宽高比 ~24:1 长条可平铺）
```
rough rubble retaining wall, irregular stones 15-45cm, 5-7 courses,
dark charcoal limestone (#2E2C2A to #55504A), strong value contrast,
thick snow cap along the top edge (8% of height) with grass tufts poking through,
THIS ONE MUST TILE HORIZONTALLY (seamless left-right)
```
参考槽位：`rubble_strip`（1024×128）

### F. 屋顶石板（Roof_Slate，宽高比 ~1:1）
```
slate roof shingles viewed orthographically, 6-8px wide tiles in staggered rows,
cool blue-grey (#3A4048 to #6C757F), each row's top edge catches a 1px snow line,
warm reflected light from below on the lower edges, no sky, no ridge line
```
参考槽位：`roof_slate`（512×512）

### G. 屋顶锥面（Roof_Slate，宽高比 ~3:4）
```
conical slate roof surface, shingle rows converging toward the top,
snow accumulating more heavily near the apex, vertical rib highlights every 8-12 tiles
```
参考槽位：`roof_cone`（512×512）

### H. 室内石墙（Stone_Interior，较暗）
```
interior limestone wall in dim candlelight, same stone pattern as A but 30% darker,
warm candle glow pooling from below, deep cool shadows in the joints
```
参考槽位：`interior_wall`（512×512）

## 三、出图规格

| 槽位 | 尺寸 | 格式 |
|---|---|---|
| stone_big_a / stone_big_b | 1024×512 | PNG（不透明） |
| stone_gable | 512×512 | PNG |
| stone_tower | 256×512 | PNG |
| stone_cornice | 1024×128 | PNG |
| rubble_strip | 1024×128 | PNG（**左右无缝**） |
| roof_slate | 512×512 | PNG |
| roof_cone | 512×512 | PNG |
| interior_wall | 512×512 | PNG |

## 四、命名与落位

1. 生成好的图按上表**槽位名**保存为 PNG，放进 `spike/art-prompts/in/`
2. 每个槽位可以多给几张（`stone_big_a_01.png`、`stone_big_a_02.png`…），脚本会**按图表面积从大到小依次分配**，让大墙优先用不同的图，减少重复
3. 运行 `python3 spike/tools/ingest_handpaint.py`：
   - 按 `style11_layout.json` 的图表矩形，把图**缩放并裁切**进 `Style11_Atlas{0,1,2}.png`
   - 小图表（<0.25 m²）不做重绘，直接从同一面墙的大图里**采样调色板**平涂，避免满屏碎图
   - 冻结族（Snow / Wood / Rock / Grass）不动
4. 之后照常 `MaterialForge.Generate` → `SpikeScene.Run` → `SpikeBuild.Run` → `capture_runtime.sh`

## 五、验收判据（脚本会打印）

```
[HAND] slots=<n> charts_filled=<n> big_charts=<n> coverage_area_pct=<f> small_filled_by_sample=<n>
[HAND_GATE] coverage_ok=<bool> repeat_ok=<bool> tone_ok=<bool>
```
- `coverage_area_pct ≥ 0.85`（大图覆盖 85% 墙面面积）
- 相邻大墙用不同图片（重复率 ≤ 20%）
- 渲染后色调仍落在：石墙 `L* 38–46`、`b* +10 ~ +18`；屋顶比墙深 ≥3.5
