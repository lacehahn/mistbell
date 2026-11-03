只做一件事：修**第一版材质小样**被驳回的 5 个问题（石/木/雪三个材质，仍不铺开到其余四类）。不改玩法/相机/关卡/UI。

必读：`Shots/art_close.png`（当前小样）、`Shots/style_480_upscaled.png`、`spike/project/Assets/Editor/MaterialForge.cs`、`spike/tasks/material_style_octopath.md`（总规格仍有效）。

## 一、当前判定

`palette_ok=True`、`texel_ok=True`，但 **`hf_spread=26.1`（门槛 0.20）**：Stone 0.031 / Wood 0.025 / Snow 0.0012。雪面几乎没有颗粒，三者不同源。
肉眼问题：石材退化成大块单色补丁（无灰缝、无块内明暗）、砌块过大、木门像大理石、雪地纯白无颗粒、台阶与围墙边缘是不对齐的锯齿。

## 二、五个必须修的

1) **石材灰缝**：每块砌块之间必须有 **1 px 深色灰缝**（比基色暗 25–30%，色 `#5E5245`），并且灰缝**连续不断**；块边缘 1 px 亮边（倒角，亮 12%）。
2) **砌块尺寸**：当前一块看起来 0.8–1.0 m。改为 **0.42–0.58 m 高 × 0.55–0.85 m 宽**，即按 `texel_per_m=28` 换算是 **12–16 texel 高**。竖缝与横缝错半块（错缝砌法）。
3) **块内明暗**：每块内部允许 **3 阶**，但阶与阶之间必须沿块的**上/下缘**分布（上亮下暗，模拟倒角受光），不要随机噪点。
4) **木门**：必须能读出**门板分缝**（竖向 2–3 条 1 px 深缝，把门分成 2–3 块板）+ 每块板内 **3–5 px 宽的木纹线**（深色 `#3A281D`，沿板长方向，允许 1 px 抖动）；不许出现整片大理石纹。
5) **雪地颗粒**：加 **1 px 蓝灰斑点**（`#C3D3E3`，密度 2–4%）+ **硬边阴影块**（3–6 px，`#D5E4F0`）。
   目标值按**独立复量**校准（高斯 σ=1.0 残差法，直接量三张 Albedo）：
   `Snow 0.0036 / Stone 0.0104 / Wood 0.0118` → 雪要拉到 **0.008–0.013**，同时木头略降到 **0.009–0.012**，
   这样两者与石材同量级（spread ≤ 0.20 才有意义）。注意：别为了提数值把雪做成噪点，斑点要成"块"不成"沙"。
6) **UV 对齐**：台阶/围墙的材质 `_BaseMap` 必须按世界坐标对齐 texel（用 `texel_per_m` 反算 tiling 与 offset），消除“不对齐的锯齿”；同一面墙上砌块不应被 UV 拉伸变形。

## 三、必须打印

```
[STYLE2] <material> palette=<n> texel_per_m=<f> mortar_px=<n> block_px=<w>x<h> step_ratio=<f> seam_grad=<f>
[STYLE2_HF] resolution=native Stone=<f> Wood=<f> Snow=<f> spread=<f>
[STYLE2_HF] resolution=480    Stone=<f> Wood=<f> Snow=<f> spread=<f>
[STYLE2_GATE] palette_ok=<bool> texel_ok=<bool> mortar_ok=<bool> hf_ok=<bool> pass=<bool>
```

## 四、硬门槛

- 各材质 `palette ≤ 12`、`texel_per_m` 按总规格表、`seam_grad ≤ 0.02`
- 石材 `mortar_px=1`、`block_px` 在 12–16 高 × 16–24 宽
- 木门 `board_seams ∈ [2,3]`、`grain_width_px ∈ [3,5]`
- 雪地 `hf_energy(480) ∈ [0.010, 0.030]`
- **`hf_spread ≤ 0.20`（480 缩放后）**
- 整帧仍满足 `snow_ratio ≥ 0.55`、`white_clip_ratio ≤ 0.05`、`magenta_px = 0`

## 五、必须自己跑

```bash
bash spike/tools/accept.sh --fast   # 建场景→出包→11 项演示→截图→量测
```
再单独跑 `-demo-enter-church` 出室内三图（室内石材沿用同一套逻辑，整体暗 18%）。
⚠️ 一律用脚本里的 `open -n -a` + caffeinate 启动；直接执行二进制会静默卡死。

不铺开到屋顶/岩/草/室内其余材质；不加包、不 commit、不改玩法/相机/关卡/UI。
