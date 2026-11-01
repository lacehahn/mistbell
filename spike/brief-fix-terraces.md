# 派遣任务：台地贴图（UV 米制）、坏机位 art_ground、近景过曝

## 已确诊的根因（不用再猜，直接改）

**① 台地/挡土墙一片平涂米色 —— UV 单位错了，不是缺材质。**

- `SpikeArt.MakeTextured()` 的约定：`tiling = 1 / metersPerTile`，即 **1 个 UV 单位 = `metersPerTile` 米**（石材约 2 m）。
  贴图密度依赖「UV 以米计」。
- 但 `spike/tools/build_church.py` 给每个对象用的是 `bpy.ops.uv.smart_project(...)`，
  UV 被归一化到 **0..1**。于是 34 m 宽的台地正面只铺 **1 块** 128 px 贴图 → mip 之后平均成一个纯色。
  这就是 `Shots/art_close.png` 下三分之一那片平涂米色、`art_ground.png` 整帧平涂的来源。
- 修法：把 smart_project 换成 **立方投影、1 UV 单位 = 1 米**（`bpy.ops.uv.cube_project(cube_size=1.0, ...)`，
  或直接按面法线取主导轴、用顶点世界坐标（米）写 UV，避免依赖操作符上下文）。
  注意布尔切出来的拱洞面片也要有 UV（现有断言 `uv and len(uv.data)==len(o.data.loops)` 会兜住）。

**② `art_ground` 机位贴在台地侧面里**，pitch 26° 撞上 1.2 m 厚的台地，整帧就是两块平面（削顶 48%）。

**③ `art_close` 削顶 23.2%**：正面被 2.2 强度的太阳直打，\(y\) 方向中间调全丢。

## 只允许改这些文件

- `spike/tools/build_church.py`（UV 生成 + `--verify` 增加 UV 米制断言）
- `spike/project/Assets/Editor/SpikeScene.cs`（只改机位表里 `art_ground` 一行、曝光常量，`Fov=35` 揭示机位**不许动**）
- `spike/tools/frame.py`（只增加每区域灰度 **std**，用于量化「是否平涂」）
- 产物：`spike/blend/gergeti_church.blend`、`spike/export/gergeti_church.fbx`、
  `spike/project/Assets/Art/Church/gergeti_church.fbx`、`spike/project/Shots/*.png`

**不要**改：材质生成器 `tools/make_textures.py`、山体 `BuildMountains()`、雾、角色、
后期 `BuildRuntimeProfile()`、`SpikeBuild.cs`、`Screenshotter.cs`、docs/、AGENTS.md。

## 要做的三件事

1. **UV 米制**：`build_church.py` 里所有对象改用「1 UV 单位 = 1 m」的投影。
   在 `--verify` 里加断言并打印 `UVSCALE OK`：对 `Terrace` 对象，UV 的 u 或 v 跨度 ∈ **[33, 37]**
   （台地 34 m 宽）；全场景任何对象 UV 跨度 ≤ 45。三角面总数保持在 **50000–60000**（几何不许动）。
2. **`art_ground` 机位**：把眼位挪到台地外的草地上（台地在 Unity 里占据 z ≈ -11..13、x ≈ -17..17，
   正面在 z ≈ -11；草地在 z < -12 的坡面上），视线**斜看过草地**，画面里要同时出现
   **草（带像素草叶颗粒）+ 石板路或台地转角**，不要正对台地平面。
   pitch 建议 12°–18°（不是 26°），aim 点落在 6–8 m 外的草面。
3. **过曝**：目标 `frame.py` 全部机位 **削顶率 < 3%**。当前 `art_runtime` 1.41%、`art_close` 23.20%、`art_ground` 48.05%。
   手段（可组合，但**不许**改 Fov、不许改机位构图、不许关后期）：sun 2.2 → **1.8–1.9**、
   stone 目标反照率 0.36 → **0.30–0.32**、必要时 `postExposure` 0.4 → 0.35。

## 验收（必须自己跑，把数字贴回回答）

```bash
cd /Users/lance/workspace/mistbell
blender -b --python spike/tools/build_church.py ; echo BUILD=$?
blender -b --python spike/tools/build_church.py -- --verify ; echo VERIFY=$?
cp spike/export/gergeti_church.fbx spike/project/Assets/Art/Church/gergeti_church.fbx
pkill -f "Unity.app/Contents/MacOS/Unity" ; sleep 2
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/60_scene.log ; echo SCENE=$?
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/61_build.log ; echo BUILDAPP=$?
caffeinate -dimu -t 180 bash spike/tools/capture_runtime.sh
for f in art_runtime art_close art_ground; do python3 spike/tools/frame.py spike/project/Shots/$f.png; done
```

必须同时满足：

1. `BUILD=0`、`VERIFY=0`、`SCENE=0`、`BUILDAPP=0`，`grep -c "error CS"` 为 0
2. `--verify` 输出含 `UVSCALE OK`；三角面 50000–60000；`[SPIKE] frame church heightPct ∈ [45,55]`、
   `pitch ∈ [10,14]`、`frame porch centerX ∈ [760,1160]`（几何与机位没被改坏的证据）
3. 三张图 `clipped>=250` 全部 **< 3%**
4. `art_ground.png` 的 `ground_near` 区域 **std > 10**（现在接近 0，即平涂）且
   `clipped < 3%`；文件大小 **> 150 KB**（现在是 50 KB，平涂帧的典型特征）
5. `art_close.png` 里台地正面能看到**石砌块缝**（人眼可辨），不是纯色

**跑不动就报告卡在哪一步**（贴最后一屏日志），不要靠改阈值/改机位/缩小教堂来凑数字。
整个流程比单次 7 分钟长：**分步跑**（blender → scene → build → capture 各一条命令），
每条命令单独执行并看 exit code，不要串成一条长链等超时。
