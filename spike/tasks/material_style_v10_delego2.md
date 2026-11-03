只做一件事：修掉**"木箱感"**——上一版（v9）把积雪压在每个石块顶缘 + 每块加厚倒角，结果整面墙像码起来的木箱。这一轮按参考图重做，并且**必须把挡土墙恢复成深色**（用户明确要求保持深色，v9 把它做亮了）。

参考：`art-ref/ref-gergeti-hd2d.jpg`（HD-2D 插画）、当前失败样张 `Shots/art_close.png`（可自行回看）。

## 一、v9 失败的三个原因（逐条改掉）

1. **积雪压在每个石块顶缘** → 变成"每块砖描了白边"。参考图里雪只出现在**整层砌筑的水平台面**上：墙顶、檐口、窗台、每层瓦的顶缘。
   - 改为：每 **一层（course）** 只在最上面 2–6 px 压雪，块与块之间**不描白边**；雪厚不规则（含 1–2 px 下垂），整张贴图雪覆盖 **8–14%**
2. **每块都有厚倒角 + 深描边** → 变成积木。
   - 去掉块四周的整圈深描边；灰缝改为 `joint_ratio ∈ [0.80, 0.90]`（很淡），且 **25% 的位置两块直接相接没有缝**
   - 倒角只在**朝向光的上边缘**（1 px），下边缘不加
3. **砌块尺寸太均匀** → 像砖墙。按参考改成**长尾分布**：
   - 宽：40% 在 0.22–0.35 m、35% 在 0.35–0.55 m、25% 在 0.55–0.85 m
   - 高：0.26–0.46 m，层高 ±6 px 起伏
   - `size_cv ≥ 0.40`、`grid_score ≤ 0.10`

## 二、必须恢复的色调（用户已确认过的观感）

**判据一律在"渲染后的画面区域"上量，不要用原始贴图的 L\***（材质应用时有 exposure 增益，两者差很多；上一轮就是混用导致数值对不上）：
- 教堂墙面（画面中教堂主体区域均值）：`L* 40 ± 2`、`b* +14 ± 3`
- 屋顶：`L* 35 ± 1.5`，且 **比墙面深 ≥ 3.5**
- **挡土墙：`L* 14 ± 2`**（v9 现在是 38.8，明显错了；参考图里挡土墙是深色乱砌石，不是浅色）
- 室内：`≤ 墙面 − 8`

## 三、产出

- 保持 **1024×1024** 贴图（`Stone`、`Roof_Slate`、`Stone_Sub`、`Stone_Interior`）
- 世界尺度不变（`texels_per_m` 重算后砌块实际尺寸必须落在上面区间）
- 冻结不动：`Snow`、`Wood`、`Rock`、`Grass`（SHA 一致）

## 四、必须打印

```
[STYLE10] <mat> size=<px> block_w_m=<min>-<max> block_h_m=<min>-<max> size_cv=<f> chips_pct=<f> joint_ratio=<f> snow_cap_pct=<f> grid_score=<f>
[STYLE10_FRAME] church_L=<f> church_b=<f> roof_L=<f> wall_minus_roof=<f> sub_L=<f> interior_L=<f>
[STYLE10_UNCHANGED] Snow_sha_same=<bool> Wood_sha_same=<bool> Rock_sha_same=<bool> Grass_sha_same=<bool>
[STYLE10_GATE] cv_ok=<bool> joint_ok=<bool> snow_ok=<bool> grid_ok=<bool> tone_ok=<bool> pass=<bool>
```

## 五、硬门槛

- `size_cv ≥ 0.40`、`grid_score ≤ 0.10`、`joint_ratio ∈ [0.80, 0.90]`、`snow_cap_pct ∈ [0.08, 0.14]`
- 渲染后：`church_L ∈ [38, 42]`、`church_b ∈ [+11, +17]`、`roof_L ∈ [33.5, 36.5]`、`wall−roof ≥ 3.5`、`sub_L ∈ [12, 16]`、`interior_L ≤ church_L − 8`
- 四张冻结贴图 SHA 一致
- 整帧：`snow_ratio ≥ 0.55`、`white_clip ≤ 0.05`、`magenta_px = 0`
- 性能：1920×1080 `fps ≥ 200`、`drawcall ≤ 330`

## 六、必须自己跑

```bash
bash spike/tools/accept.sh --fast
```
外加 `-demo-enter-church` 室内三图、1920×1080 `-bench`。
截图必出：`art_close.png`、`art_runtime.png`、`style_480_upscaled.png`（480×270 LANCZOS → NEAREST ×4），**必须基于本轮渲染**。
⚠️ 一律用 `open -n -a` + caffeinate 启动。

不加包、不 commit、不改玩法/相机/关卡/UI。
