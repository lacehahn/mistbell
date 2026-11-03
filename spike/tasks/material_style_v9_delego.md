只做一件事：**去掉教堂的"乐高感"**，按 `art-ref/ref-gergeti-hd2d.jpg`（HD-2D 参考插画）重做石材/屋顶贴图，并把贴图做成**更高分辨率的数字资产**（1024×1024 PNG）再进材质。

用户原话：「确实像乐高，你处理去一下，参考依然是这个，我建议生成贴图的数字资产，在生成」。

## 一、参考图里必须照抄的特征（我已逐条看过）

1. **砌块尺寸分布很宽**：小到 0.22 m、大到 0.75 m，长条石与方石混排；层高不完全一致（每层 ±6 px 起伏），**没有一条完美的水平基准线**
2. **块边是圆钝/崩角**的，不是直角；约 1/3 的块有明显缺角
3. **灰缝不是纯黑均质线**：灰缝颜色 = 该块颜色 × 0.72（跟着块走），且约 20% 的位置两块直接相接、没有缝
4. **积雪是"去乐高"的关键**：墙顶、檐口、窗台、每层瓦的顶缘都有 2–5 px 的积雪条（`#E6EFF8`），带不规则下垂 1–2 px；整张贴图里雪覆盖 **10–18%**
5. **暖色高光边**：朝上的 1 px 边缘被阳光打亮（块色 × 1.22，偏黄），阴面偏冷蓝褐——这是插画感的核心
6. 屋顶：每层瓦 **5–7 px**，**每层顶缘一条 1 px 雪线**；锥顶有竖向肋，肋边提亮；檐口一圈暖色

## 二、产出物

把下列贴图**改成 1024×1024** 的数字资产（原 256 太糊，撑不住这些细节），仍保存在 `spike/project/Assets/Art/MaterialForge/`：

- `Stone_Albedo.png`（教堂墙，1024²）
- `Roof_Slate_Albedo.png`（屋顶，1024²）
- `Stone_Sub_Albedo.png`（挡土墙碎石，1024²；**这次可以改**——用户之前要"保持"，但乐高感主要来自它和教堂同一套规整砌块，改成真碎石乱砌 + 厚雪顶）
- `Stone_Interior_Albedo.png`（室内，1024²，同教堂逻辑但暗 8 以上）

**冻结不动**：`Snow_Albedo.png`、`Wood_Albedo.png`、`Rock_Albedo.png`、`Grass_Albedo.png`（SHA 必须一致）

贴图放大到 1024 后，材质的 `texel_per_m` 与 tiling 必须重算，保证世界尺度不变（不许因为提分辨率让砌块变大或变小）。

## 三、必须量测并打印

```
[STYLE9] <mat> size=<px> palette=<n> block_w_m=<min>-<max> block_h_m=<min>-<max> size_cv=<f> chips_pct=<f>
         joint_ratio=<f> snow_cap_pct=<f> rim_light=<bool> grid_score=<f> mean_L=<f> mean_b=<f>
[STYLE9_UNCHANGED] Snow_sha_same=<bool> Wood_sha_same=<bool> Rock_sha_same=<bool> Grass_sha_same=<bool>
[STYLE9_GATE] cv_ok=<bool> chips_ok=<bool> joint_ok=<bool> snow_ok=<bool> grid_ok=<bool> tone_ok=<bool> pass=<bool>
```

指标定义（照此实现，别自己改）：
- `size_cv` = 砌块尺寸的标准差 / 均值
- `chips_pct` = 有缺角的块数 ÷ 总块数
- `joint_ratio` = 灰缝亮度 ÷ 相邻块亮度（目标 0.65–0.80）
- `snow_cap_pct` = 雪色像素占比（目标 0.10–0.18）
- `grid_score` = 水平/垂直边缘中有多少比例落在"完全等距格点"上（目标 ≤ 0.15，越低越不像乐高）

## 四、硬门槛

- 三张重做贴图 `size_cv ≥ 0.35`、`chips_pct ≥ 0.30`、`joint_ratio ∈ [0.65, 0.80]`、`snow_cap_pct ∈ [0.10, 0.18]`、`grid_score ≤ 0.15`
- 色调保持上一版定稿：`Stone mean_L ∈ [38, 41]`、`mean_b ∈ [+12, +18]`；`Roof mean_L ∈ [34.5, 36.5]` 且 `≤ Stone − 3.5`；`Stone_Interior ≤ Stone − 8`
- `palette ≤ 14`（插画感允许比 12 多两档）
- 四张冻结贴图 `sha_same = True`
- 整帧：`snow_ratio ≥ 0.55`、`white_clip ≤ 0.05`、`magenta_px = 0`、`mountain_luma 0.42–0.60`
- 性能：1920×1080 `fps ≥ 200`、`drawcall ≤ 330`

## 五、必须自己跑

```bash
bash spike/tools/accept.sh --fast
```
外加 `-demo-enter-church` 室内三图、1920×1080 `-bench`。

**截图**：重出 `art_close.png`、`art_runtime.png`、`style_480_upscaled.png`（480×270 LANCZOS → NEAREST ×4），必须基于本轮渲染。

⚠️ 一律用脚本里的 `open -n -a` + caffeinate。

不加包、不 commit、不改玩法/相机/关卡/UI。
