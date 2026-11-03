只做一件事：**把教堂石材调白**。挡土墙（`Stone_Sub`）的颜色与砌法**保持现状不动**，雪/木不动。

用户原话：「1 调白，2 调白后就不用了，3 挡土墙当前版本就可以」+「追加，挡土墙保持当前颜色」。

## 一、只改 `Stone`（教堂石材）

当前 4 档：`#3A3A3C / #4C4A48 / #5C5855 / #6E6A66` + 倒角 `#8B867F`，实测平均 `L*=55.7`。
目标：**整体提亮到 `mean_L ∈ [44, 52]`**（修正：上一版任务书写 64–68 是基于错误的 Lab 计算——没做 sRGB→线性化，把 L*25.4 误算成 55.7。正确基线是 `Stone L*=25.4`、`Stone_Sub L*=13.8`、`Snow L*=93.8`）。
保持同一色相（`|a*|,|b*| ≤ 3`）、保持 4 档阶梯与对比关系。**优先级：L* 硬门槛 > 建议色板**——色板可自行调灰阶，只要落进 `mean_L ∈ [44,52]` 且 `chroma_ab_max ≤ 6`。

参考参考照片里石墙的中间调与亮面（`#5C5550` 中间调 / `#A7B0C1` 亮面），**提亮但不要把最亮档推到接近纯白**：
- 参考换算（sRGB→L*）：`#707070≈47.2`、`#808080≈53.6`、`#909090≈59.8`；推荐 4 档 `#6A6A6C / #7C7C7E / #8E8E90 / #A0A0A2`（倒角再 +12%）
- 最亮档的**像素占比不超过 12%**（避免大面积发白）
- 灰缝仍 1 px、比最暗档再暗 18%（约 `#383838`），倒角仍 +12%

其余不变：砌块 0.55–1.15 m × 0.32–0.60 m（±20%）、`texel_per_m=28`、色数 ≤10、相邻块明度差 ≤2 档、无棋盘。

## 二、绝对不许改的

- `Stone_Sub_Albedo.png`：**SHA-256 必须与 v4 一致**（颜色、砌块尺寸、灰缝全部保持现状）
- `Snow_Albedo.png`、`Wood_Albedo.png`：SHA-256 必须与 v4 一致
- 屋顶 / 裸岩 / 草 / 室内：本轮不做

## 三、必须打印

```
[STYLE5] Stone palette=<n> luma_steps=<n> mean_L=<f> bright_px_pct=<f> chroma_ab_max=<f> texel_per_m=<f>
[STYLE5_UNCHANGED] Sub_sha_same=<bool> Snow_sha_same=<bool> Wood_sha_same=<bool>
[STYLE5_FAMILY] church_vs_sub ΔE=<f>
[STYLE5_HF] sigma=1.0 Stone=<f> Sub=<f> Wood=<f> Snow=<f> spread=<f>
[STYLE5_GATE] white_ok=<bool> mono_ok=<bool> unchanged_ok=<bool> hf_ok=<bool> pass=<bool>
```

## 四、硬门槛

- `Stone mean_L ∈ [44, 52]`、`bright_px_pct ≤ 0.12`、`chroma_ab_max ≤ 6`、`palette ≤ 10`
- `Sub_sha_same = Snow_sha_same = Wood_sha_same = True`
- `ΔE(church, sub) ∈ [20, 40]`（教堂提亮后与暗挡土墙的差值会明显变大，仍要求"看得出是两种砌法"）（教堂变亮后差值会变大，这个区间仍要求"看得出是两种砌法"而不会变成两种石头）
- `hf_spread ≤ 0.20`（σ=1.0 口径，四样本）
- 整帧：`snow_ratio ≥ 0.55`、`white_clip_ratio ≤ 0.05`（这个很关键：调白后别把雪/墙一起爆掉）、`magenta_px = 0`

## 五、必须自己跑

```bash
bash spike/tools/accept.sh --fast
```
外加 `-demo-enter-church` 室内三图（室内石材仍比教堂暗 18%，但这次是"教堂变亮"后的相对关系，别把室内容厅化）。
⚠️ 一律用脚本里的 `open -n -a` + caffeinate。

**截图要求**：除了 `art_close.png`，还要重新生成 `style_480_upscaled.png`（480×270 LANCZOS 下采样 → NEAREST 放大 4 倍）。v4 那次这张图是旧的（还是 v3 配色），这次必须基于本轮渲染重出。

不加包、不 commit、不改玩法/相机/关卡/UI。
