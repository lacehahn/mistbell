# 简报（第 38 轮）：雪面第二刀——从「均匀电视雪花」到「像素簇 + 雪堆起伏」

第 37 轮已把「横向软块/水波」修掉，数字全过：
`corrX_med 8.3→1.8`、`aniso 5.69→1.24`、`grain_med 14.4→26.2`、`hf_energy_lower 0.0217→0.0531`、
`snow 0.5704`、`clip 0.0084`、`shadow 0.0682`、`PATTERN=0`（尺子 `spike/tools/surface.py`）。

**但目视新问题是：雪面变成一层均匀的「电视雪花 / 白噪声地毯」**——单像素级噪声，没有 3–4 px 的
像素簇，也没有任何中等尺度的起伏（雪堆、风纹、压痕），远看是一张白纸撒盐。

## 只做这一件事

让近景雪读成 **HD-2D 像素雪**：3–4 px 的硬边像素簇 + 0.5–1 m 尺度的轻微雪堆起伏/风纹。

## 允许改的文件（与上一轮相同）

- `spike/project/Assets/Art/Shaders/TerrainHaze.shader`（雪段）
- `spike/tools/make_textures.py`（雪贴图，可重生成 `Snow_*.png`）
- `spike/project/Assets/Editor/SpikeScene.cs` 仅雪材质行

**禁止**：相机 / 教堂 / 角色 / 其它 shader / 任何尺子脚本（`surface.py`、`measure_shot.py` 等）/ `PROGRESS.md`。

## 做法提示（不强制，但要说清为什么）

1. 噪声要在**世界 XZ 上以固定 texel 密度**取（现在是 `texels_per_meter=40`，即 2.5 cm/texel）；
   把「细颗粒」项量化成 **3–4 px 的簇**（等价于把高频项 clamp 到 `floor(u*W/3.5)` 这类格子，或直接对值做 3–4 阶量化），
   不要用连续噪声直接逐像素调制；
2. 加一个**中等尺度**项（0.5–1 m，反照率 ±4%，无方向性），用来读成雪堆起伏；**不要**用正弦（会变成波纹），
   用 2–3 个不同频率的 value noise 相乘/相加，且必须各向同性；
3. 保持 `snowCoverage`/石头的雪帽逻辑不变。

## 硬门槛（改前 → 目标，全部要贴命令与数字）

1. `python3 spike/tools/surface.py scan spike/project/Shots/art_runtime.png --rect 150,760,1750,1040 --patch 128 --step 128 --quiet --ground`
   - `corrX_med`：**1.8 → 落在 2.2–4.5**（这是「像素簇尺度」，太小＝电视雪花，太大＝回到软块）
   - `aniso_med ≤ 1.8`（1.24）、`tally` 中 **PATTERN=0**
   - `grain_med ≥ 12`（30.4）
2. `python3 spike/tools/measure_shot.py spike/project/Shots/art_runtime.png`
   - `hf_energy_lower ≥ 0.030`（0.0531）
   - `snow_ratio ≥ 0.58`（0.5704，**别让它再往下掉**）
   - `white_clip_ratio ≤ 0.03`（0.0084）、`magenta_px=0`、`ground_shadow_gap ≥ 0.05`（0.0682）、`dark_ratio_lower ≤ 0.06`（0.0283）
3. 场景日志保留 `[SNOW] texels_per_meter / detail_scale / palette_steps`，`[SPIKE] frame church heightPct` 仍在 44–46

## 验收顺序

```
Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/<n>_snow3_scene.log
Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/<n>_snow3_build.log
caffeinate -dimu -t 300 bash spike/tools/capture_runtime.sh
python3 spike/tools/measure_shot.py spike/project/Shots/art_runtime.png
python3 spike/tools/surface.py scan spike/project/Shots/art_runtime.png --rect 150,760,1750,1040 --patch 128 --step 128 --quiet --ground
```

截图只能走 `caffeinate` + `capture_runtime.sh`。不要 commit，不要改 `PROGRESS.md`。
