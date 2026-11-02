# 简报：雪面去「水波/泡沫」，改成细硬像素颗粒（队列 ⑭）

## 为什么派这一项（实测诊断，不是形容词）

用新尺子 `spike/tools/surface.py`（自相关相关长度 / 离散纹样峰值，已用合成图定标 11/11 通过）量
HEAD `d1dade0` 的 `art_runtime.png`（md5 `dd9f0dfc`）：

- **近景雪地**（rect 150,760,1750,1040，24 个 128² patch）：
  `grain_med=14.38  corrX_med=8.3  corrY_med=1.5  aniso_med=5.69  peak_med=0.14  tally={'OK':24}`
  → **没有离散周期**（不是平铺重复），但**特征尺度横向 8.3 px、纵向 1.5 px**：雪被拉成
  8 px 宽的软块，画面读成「泡沫/水波」。4° 坡在 pitch 16° 下透视只会给出 ~3.6 的
  几何各向异性，实测 5.69 说明**贴图自身还在横向上额外糊了一层**。
- 中景雪地（rect 120,560,900,720）：`corrX_med=1.6 corrY_med=1.3 aniso=1.44` → 中景是好的。
- `measure_shot.py`：`hf_energy_lower=0.0217`（上一版 0.0301，**退步**，门槛 0.025）。

**结论**：问题不是「有波纹图案」，是**近景的图案尺度太粗 + 边缘太软**。HD-2D 的雪应当是
2–4 px 的硬边像素簇，且各向同性（除透视外）。

## 只做这一件事

让近景雪地读成**细、硬、均匀**的像素颗粒，去掉横向软块。

## 允许改的文件（其它一律不动）

- `spike/project/Assets/Art/Shaders/TerrainHaze.shader`（雪段：`snowAlbedo`/`texel`/`middle`/`lowField` 那几个式子）
- `spike/tools/make_textures.py`（若需要重出雪贴图；`Snow_Pixel.png` 等产物可重生成）
- `spike/project/Assets/Editor/SpikeScene.cs` 里**仅**雪材质相关行（tiling / scale 常量）

**禁止**：改相机机位（`ShotRig`）、改教堂几何/材质、改角色、改其它 shader、改 `measure_shot.py`、
改 `surface.py`、改 `frame.py` 等尺子。

## 方向（可自行判断，但必须给出数值理由）

1. 雪面细节按**世界坐标 XZ** 以固定密度采样（建议 1 texel ≤ 4 cm），不要用会被透视拉长的屏幕空间项；
2. 去掉/减弱会把反照率整体调制的大尺度项（`middle`/`lowField` 的 ±0.12/±0.06 若在近景产生 >8 px 的块，必须缩到 ≤4 px 或改成高频）；
3. 边缘要硬：不要用双线性软过渡堆对比，宁可用 2–3 档明度离散 + 点采样；
4. 保留石头的雪帽逻辑与 `snowCoverage` 语义不变。

## 硬门槛（每条都要跑命令，贴数字）

改前基线（HEAD `d1dade0`）→ 目标：

1. `python3 spike/tools/surface.py scan spike/project/Shots/art_runtime.png --rect 150,760,1750,1040 --patch 128 --step 128 --quiet --ground`
   - `corrX_med` **8.3 → ≤ 4.0**
   - `tally` 中 **PATTERN 必须为 0**（现在就是 0，不许引入纹样）
   - `grain_med` **≥ 10**（现在是 14.38，不许为了变细把细节做没）
2. `python3 spike/tools/measure_shot.py spike/project/Shots/art_runtime.png`
   - `hf_energy_lower` **0.0217 → ≥ 0.030**
   - `snow_ratio ≥ 0.55`（现在 0.6329）、`white_clip_ratio ≤ 0.03`（0.0037）、`magenta_px = 0`
   - `ground_shadow_gap ≥ 0.05`（0.061）、`dark_ratio_lower ≤ 0.06`（0.0287）
3. 场景日志必须打印（新增即可）：`[SNOW] texels_per_meter=<n> detail_scale=<n> palette_steps=<n>`，
   且 `[SURF] church_luma/snow_luma ratio` 仍在 **0.72–0.82**（现在 0.7963）
4. 构图不许动：`[SPIKE] frame church heightPct` 仍在 44–46

## 验收顺序（全部跑完再报告，退出码要贴）

```
Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/<n>_snow_scene.log
Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/<n>_snow_build.log
caffeinate -dimu -t 300 bash spike/tools/capture_runtime.sh
python3 spike/tools/measure_shot.py spike/project/Shots/art_runtime.png
python3 spike/tools/surface.py scan spike/project/Shots/art_runtime.png --rect 150,760,1750,1040 --patch 128 --step 128 --quiet --ground
```

**截图只能走 `caffeinate` + 窗口路径**（`capture_runtime.sh`），禁止 `-batchmode` 截图、禁止离屏回读。
`caffeinate` 必须加，显示器休眠时播放器不推进主循环、截图会静默失败。

不要 commit，不要动 `spike/PROGRESS.md`。
