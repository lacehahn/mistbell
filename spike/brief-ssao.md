# BRIEF — 队列 11：SSAO + 石材块面起伏（第 18 轮）

## 目标（一句话）

近读石墙现在读成「干净的现代砌块」：块面完全平、砖缝只有一条 1 px 的暗线、没有接触阴影。
本轮同时修两件事：**① 挂 URP SSAO 给转角/砖缝/门洞内凹加接触阴影；② 石材贴图给块面做出真实起伏（倒角 + 块心微凹），并把灰缝颜色改回中性。**

## 必读文件（只读这些，禁止通读全仓）

1. `spike/project/Assets/Editor/SpikeURP.cs` —— 已有 `TiltShift()` 的写法，照它的模式加 `SSAO()`
2. `spike/project/Assets/Editor/SpikeScene.cs` —— 只读第 20–60 行（材质段）与 `ChurchMaterial` 附近；石材 tiling 已写在第 31–36 行
3. `spike/tools/make_textures.py` —— 只读 `make_stone()`、`normal_map()`、`main()`
4. `spike/project/Library/PackageCache/com.unity.render-pipelines.universal@ee0b6bbace10/Runtime/RendererFeatures/ScreenSpaceAmbientOcclusion.cs` —— URP 17 的 SSAO 定义

## A. SSAO RendererFeature

**实现要求（逐条照做）**

1. 在 `SpikeURP.cs` 新增 `public static void SSAO()`，产出**独立资产** `Assets/Settings/SSAOFeature.asset`。
   ⚠️ 必须是独立 `.asset`，不能是子资产（子资产不进构建包，VolumeProfile 与 TiltShift 都栽过）。
2. `ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>()`，加到 `Assets/Settings/MistbellRenderer.asset` 的 `rendererFeatures`。
   **幂等**：重复执行不能重复添加；`TiltShiftFeature` 必须原样保留在列表里（本轮的断言会检查两个都在）。
3. 配置参数：`ScreenSpaceAmbientOcclusionSettings` 是 **internal**，不能直接赋值 → 用 `SerializedObject` / `FindProperty` 走属性路径：
   ```
   m_Settings.Intensity              = 0.75
   m_Settings.DirectLightingStrength = 0.15
   m_Settings.Radius                 = 0.6      // 世界米；块 1.05×1.2 m、缝 5–10 px，先取 0.6
   m_Settings.Source                 = 0        // Depth（见下方硬约束）
   m_Settings.AOMethod               = 1        // InterleavedGradient（0=BlueNoise 也可，但必须能进包）
   m_Settings.Samples                = 1        // Medium(8)
   m_Settings.BlurQuality            = 0        // High(双边上采样，缝才不糊)
   m_Settings.Falloff                = 100
   m_Settings.Downsample             = 0
   m_Settings.AfterOpaque            = 0
   m_Settings.NormalSamples          = 1        // Medium
   ```
4. **硬约束：`Source` 必须 = Depth(0)。** 原因：`Assets/Art/Shaders/TerrainHaze.shader` 只有 `UniversalForward` / `ShadowCaster` / `DepthOnly` 三个 pass，**没有 DepthNormals pass**；用 DepthNormals 会让整片地形在法线缓冲里缺席，AO 结果不可信。地形占画面下半，不能冒这个险。
5. 打印日志（供验收 grep）：
   ```
   [POST] ssao feature=<asset path> intensity=0.75 radius=0.6 source=Depth
   [POST] renderer features=2 entry0=TiltShiftFeature entry1=SSAOFeature
   ```
   并做序列化断言：从磁盘重新 Load `MistbellRenderer.asset`，确认 `rendererFeatures.Count == 2` 且含 SSAO 资产路径，否则 `throw`。
6. 只改 `SpikeURP.cs`。**不要动**相机、机位、地形几何、雾、远山、移轴参数。

## B. 石材块面起伏 + 中性灰缝（`make_textures.py` 的 `make_stone()`）

**现状（实测，不要重复摸索）**：`texstat stone_ashlar.png` → `coarseStd(32px)=9.60`、`saturation=0.134`、`edge%=5.0`；
贴图 256×256、4 皮、一张贴图 = 2.4 m；块尺寸在材质里被放大到 4.8 m（`m_Scale=1/4.8`）。

**要做的两件事**

1. **块面起伏（高度场，不是块间色差）**：现在每块的高度只有「缝边 0.35 / 块面 0.6 + 颗粒」。
   改为：每条缝边 2–3 px 的倒角阶梯（0.30 → 0.50 → 0.70，随距离缝的距离），块心再叠 ±0.05 的球面/微凹；
   颜色随高度场同步变化（倒角处压暗 ~8–12、块心提亮 ~3–5），**但块与块之间的明度抖动保持 ±0.03 不变**。
2. **灰缝颜色改中性**：现在 `joint_colors` 对 R/G/B 各自 `- random(25,45)` → 画面上出现**粉线/绿线**（近读肉眼可见，这是明确的缺陷）。
   改成：三通道减**同一个**偏移量，只允许 ±2 的共用抖动；再保留「15% 概率偏亮的浅缝」这一条。色相偏差必须为 0。
3. `bumpScale`：石材法线目前 `_BumpScale = 1`，且写在 `.mat` 里（`Assets/Settings/*` 被 `.gitignore` 忽略 → **不可复现**）。
   把 `stone.SetFloat("_BumpScale", 1.8f)` 写进 `SpikeScene.cs` 材质段（紧跟 tiling 那两行），并在日志打印 `[SPIKE] stone bump=1.80`。

**不许做的事**：不许再加大块间色差（v2 的迷彩拼布就是这么来的）；不许改 `size`、`courses`、块宽分布规则；不许改其它材质。

## C. 验收（你必须自己跑完并把数字贴进报告）

按顺序跑：

```
# 1) 重生成贴图
python3 spike/tools/make_textures.py

# 2) 重建管线资产 + 场景 + 出包
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project \
  -executeMethod SpikeURP.SSAO -logFile spike/logs/120_ssao.log
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project \
  -executeMethod SpikeScene.Run -logFile spike/logs/122_scene.log
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project \
  -executeMethod SpikeBuild.Run -logFile spike/logs/124_build.log

# 3) 运行时截图（必须套 caffeinate，否则显示器休眠时播放器不推进）
caffeinate -dimu -t 150 bash spike/tools/capture_runtime.sh
```

**硬门槛（数字必须贴进报告；我这边会独立复跑一遍做对照）**

| # | 命令 | 门槛 |
| --- | --- | --- |
| 1 | `python3 spike/tools/region.py spike/project/Shots/art_close.png 1400,200,1750,420 700,600,1100,760 1300,760,1750,900` | 墙 `1400,200` 的 **`tail`（p5/p50）从 0.529 降到 ≤ 0.500**；三块的 `p50` 各自变化 **≤ ±4%**（145.9 / 145.7 / 154.2）；`clipped ≤ 0.5%` |
| 2 | `python3 spike/tools/patchwork.py spike/project/Shots/art_close.png 1400,200,1750,420 --tile 64` | `blockSpread ≤ 24%`（基线 18.3%）、`neighborDiff ≤ 10%`（基线 6.2%）—— 不许回到迷彩拼布 |
| 3 | `python3 spike/tools/texstat.py spike/project/Assets/Art/Textures/stone_ashlar.png` | `coarseStd ≥ 16.0`（基线 9.60）、`saturation ≤ 0.16`、`edge% 4.0–8.0` |
| 4 | `python3 spike/tools/seam.py spike/project/Assets/Art/Textures/stone_ashlar.png` | `ratio(col,row)` 两个都 ≤ 1.60（基线 0.75 / 1.24） |
| 5 | `python3 spike/tools/sharp.py spike/project/Shots/art_close.png` | `middle ≥ 10.0`（基线 10.63，不许因 AO 模糊掉） |
| 6 | `grep -E "\[SPIKE\] frame\|\[SPIKE\] stone" spike/logs/122_scene.log` | `church heightPct=49.8` 与 `pitch=11.8` **一字不变**；出现 `stone tiling=(0.20833333, 0.20833333)` 与 `stone bump=1.80` |
| 7 | `grep -E "Succeeded\|error CS" spike/logs/124_build.log` | macOS + Windows 都 `Succeeded`、`error CS` 0 条 |
| 8 | `grep -E "\[POST\] ssao\|\[POST\] renderer" spike/logs/120_ssao.log` | 两行都在，`features=2` |

**任何一条不过**：照实测数字调整（先调 `Radius` / `Intensity` 与倒角强度），重跑，报告里写清调了什么、为什么。

## 交付报告格式（最后必须输出）

1. 改了哪些文件（绝对路径 + 关键行）
2. 8 条门槛逐条的**实测数字**（不是「通过」两个字）
3. 你踩的坑（尤其是 SerializedProperty 路径、构建包里 SSAO 资源是否被剥离）
4. 若某项没做到，明说没做到，不要粉饰
