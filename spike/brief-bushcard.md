# 简报：灌木 A/B —— blob 提亮 vs 像素卡片（队列 24，Codex 第 25 轮）

## 背景（只读这段，不要通读仓库）

第 24 轮（`brief-props4.md`）把队列 23 的三件事全做完了，数字都过了：主体 `verts=162/tris=320`、
`bushExposure exposedMin=3 meanExposure=0.306`、`bush.png 256² edge% 18.8 coarseStd 3.65 seam 0.95/1.04`、
轮廓能数出 **7 处凹口**。但**目视反而更差**，有两个新问题：

1. **灌木整体暗了一倍半**：`foliage art_ground` 亮度比 **0.84 → 0.46**（bush luma 34.6 / grass 75.4）。
   机制：`bumpScale 0.9` + 阶梯叶簇高度场把法线大面积掀向背光侧，而直射高光已被关掉
   （`_SPECULARHIGHLIGHTS_OFF`）→ 没有高光补偿，漫反射直接掉了。这是**能量问题，不是配色问题**。
2. **表面读成「龟甲/贝壳鳞片」**：叶簇在 256² 贴图上每簇 10–18 px，到屏幕上约 10 px 一片，
   每片带一圈硬暗边 + 法线顶起 → 一串扇贝状鳞甲。三条独立的 round 22/23/24 都是「换个artifact」：
   方盒塔 → 绿石 → 油亮绿石 → 贝壳堆。**blob 路线在 143 px/m 的近景里大概到顶了。**

队列 24 的待决策就是为此定的：HD-2D 原作（《歧路旅人》）的植被是 **alpha-clip 像素卡片**，
轮廓天然是像素毛边、配色是硬色块，和像素角色同一套语汇。本轮做一次**可量的 A/B**，然后由我定性。

## 基线（2026-11-02 06:46 的构建，全部由我复跑确认，数字与 Codex 自报一致）

```
foliage art_ground   bush (360,540,780,620) mean=(33,36,25) luma=34.6 sat=0.315 Lstd=21.4 clipped=0.00%
                     grass luma=75.4 → ratio 0.46
foliage art_runtime  bush (1300,820,1750,980) luma=74.1 Lstd=46.8 clipped=0.05% → ratio 0.91
texstat bush.png     256x256 meanRGB(52,65,39) sat 0.405 coarseStd 3.65 edge% 18.8 · seam 0.95/1.04
sharp art_ground     top 2.72 / middle 11.27 / bottom 2.73      ← middle 门槛 13.5 未过
sharp art_runtime    top 0.00 / middle 4.14 / bottom 4.35
frame art_runtime    sky(167,183,203) clipped 0.00% heightPct 49.8 pitch 11.8 porch 924.6
```

## 必读文件（只读这四个，不要读别的）

1. `spike/project/Assets/Editor/SpikeScene.cs` —— `BuildProps()` 灌木材质段（605–640 行）与 `void Bush(...)`（665–756 行）
2. `spike/project/Assets/Editor/SpikeArt.cs` —— `MakeTextured()`（250–277 行）、`ImportTex()`（164–189 行）、`Blob()`
3. `spike/tools/make_textures.py` —— `make_bush()`（159–209 行）与 `main()` 的 jobs 表
4. `spike/project/Assets/Scripts/Screenshotter.cs` —— 只确认 `-shots` 参数，不改

## 任务 A：blob 路线提亮（只改反照率，别的都别动）

现在 `foliage art_ground` 比 0.46，目标 0.85 → 需要的亮度倍率 **1.85**。

- 在 `SpikeScene.cs` 现有 `bushCorrection = 0.90f / 1.80f` 那一行**之后**再加一条同形式的校正，
  注释写清「round 24 后 bumpScale 0.9 让漫反射掉了一半，按实测 0.46→目标 0.85 = ×1.85」：
  `const float bumpCompensation = 1.85f;`，把它乘进 `bushTint` 与 `lightTint`（两档都要乘，保持 `1.06` 的相对关系）。
- 跑一次量一次：`foliage.py` 报的 `art_ground` 比若不在 **0.75–1.15**，按 `目标/实测` 再调**一次**（最多一次）。
  两次的比值都写进报告。**不要**动 `bumpScale 0.9`、不要动贴图、不要动几何、不要动 `_SPECULARHIGHLIGHTS_OFF`。

## 任务 B：像素卡片路线（新代码路径，不许删旧路径）

开关用命令行参数，方便两条路线并存：

```csharp
static bool ArgFlag(string name) =>
    System.Array.IndexOf(System.Environment.GetCommandLineArgs(), name) >= 0;
bool useCards = ArgFlag("-bushCard");
```

在 `BuildProps()` 里：`useCards` 为 true 时灌木走卡片，否则走现有 blob。两档都要能跑通、都要打日志：

```
[SPIKE] bushVariant=card quads=<每丛张数×丛数> material=M_BushCard tex=<宽x高> clip=0.5 cullOff=true doubleSided=true
[SPIKE] bushVariant=blob quads=0 material=M_Bush   （默认路线也要打这一行）
```

### B1 贴图 `bush_card.png`（RGBA，128×128，`make_textures.py` 新增 `make_bushcard()`）

**像素卡片的语汇是硬色块 + 锯齿轮廓，不是渐变。** 具体要求：

- **轮廓**：一个宽底圆顶的团块，占满画面 88% 宽 / 92% 高；边缘由 **26–40 个半径 3–6 px 的半圆凸起**拼成
  （像云朵边），每个凸起之间允许 1 px 凹入 —— 这就是「像素毛边」。
- **内部**：5 档调色板（沿用现有 `(18,28,17) (30,42,24) (44,58,34) (60,76,44) (84,100,62)`），
  但**只用两种分配方式**：① 由左上向右下的 3 个**硬分界**明暗区（受光/中间/背光，边界是 1 px 直线 + 少量 2–3 px 锯齿）；
  ② 40–60 个 **4–7 px 的叶形小块**（不规则四边形/五边形，硬边，不加暗描边）散在受光区。
  **禁止**：每片叶子描一圈暗边（这是 round 24「龟甲」的病根）、平滑噪声渐变、抗锯齿。
- **底部 12–18%**：整体压一档（`palette[i-1]`），作为接地暗部。
- **顶部边缘**：8–14 个 1–2 px 的亮色块 `(84,100,62)`，只贴在轮廓内的最外一层，模拟天光钩边。
- **alpha**：轮廓内 255、轮廓外 0，**不许半透明、不许 AA 羽化**（alpha clip 才切得干净）。
- 输出：`bush_card.png` 128×128 RGBA；同时在 `main()` 的 jobs 之外单独写出（不要塞进 jobs 表，jobs 那套是 RGB + 法线的）。
- 日志/报告里必须给出：**不透明像素占比**（目标 55–78%）、不透明区 RGB 均值（G 目标 62–80）。

### B2 材质 `M_BushCard.mat`

用 `Shader.Find("Universal Render Pipeline/Lit")` 手工建（**不要**用 `MakeTextured`，它不处理 alpha）：

- `_BaseMap` = `bush_card.png`（导入用 `ImportTex(path, false)`，另外把 `TextureImporter.alphaIsTransparency = true`
  和 `mipmapEnabled = true` 设上，filter Point、无压缩、`wrapMode = Clamp`）
- `_BaseColor` = 白 × 反照率系数：**只统计 alpha > 128 的像素**算均值再套 `TintFor` 那套「按目标反照率反推」的逻辑，
  目标反照率与 blob 提亮后**同一个值**（这样 A/B 比的是形状与轮廓，不是亮度差）
- `_AlphaClip 1`、`_AlphaClipThreshold 0.5`、`EnableKeyword("_ALPHATEST_ON")`、
  `SetOverrideTag("RenderType","TransparentCutout")`、`renderQueue = 2450`、`_Cull 0`（双面）
- `_Smoothness 0`、`_Metallic 0`、**不挂法线图**、`_SpecularHighlights 0` + `EnableKeyword("_SPECULARHIGHLIGHTS_OFF")`
- 存成 `Assets/Settings/M_BushCard.mat`（与 `M_Bush` 同目录）

### B3 几何

- 网格：一个**底边中点为原点**的 quad（宽 1.25 m × 高 1.05 m，UV 0..1，法线朝 -Z），在 `SpikeArt` 里加
  `public static Mesh Quad(string key, float w, float h)`（缓存成 `Assets/Settings/Quad_*.asset`）。
- 每丛 **3 张**，绕 Y 轴 0° / 60° / 120°，各自再叠一个 ±12° 的随机偏航与本丛随机基座偏航；
  三张的缩放分别 `1.00 / 0.94 / 0.88`（避免三张完全重合的轮廓互相打架），**高度**都从地面起。
- 落点、数量（22 丛）、`Snap()` 射线吸附、`bushIndex` 顺序**全部沿用 `Bush()` 现有的**；
  卡片路线只是把「主体 blob + 叶簇」换成 3 张 quad，**不要动落点抽样序列**（否则前景构图会变，A/B 失去意义）。
- 影子用默认 `ShadowCastingMode.On`（URP Lit 的 alpha clip 会给阴影做 clip）。

## 任务 C：两套截图都要出（这是 A/B 的全部价值）

依次跑，不要漏：

```bash
# 1) 卡片路线
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -bushCard -logFile spike/logs/186_scene_card.log
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/188_build_card.log
caffeinate -dimu -t 240 bash spike/tools/capture_runtime.sh
for f in spike/project/Shots/art_*.png; do cp "$f" "spike/project/Shots/card_$(basename "$f")"; done
# 2) blob 路线（默认，最后跑，让仓库停在默认状态）
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/190_scene_blob.log
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/192_build_blob.log
caffeinate -dimu -t 240 bash spike/tools/capture_runtime.sh
```

两套都要量：`foliage.py art_ground` / `foliage.py card_art_ground` / `foliage.py art_runtime` / `foliage.py card_art_runtime`
（`RECTS` 里我已经加好 `card_art_*` 两个键）、`sharp.py` 四张、`region.py` 四张、`frame.py` 两张主视角。

## 不要动

栅栏、石块、落点范围与数量、22 丛计数、相机与 `ShotRig`、光照、Volume、SSAO/移轴、所有 shader、教堂 FBX、
草地/石材/屋顶 6 张贴图（md5 必须前后一致）、`bush.png` 与 `M_Bush` 的其余参数。

## 你必须自己跑一遍的验收（全绿或如实报告，不许改门槛）

| # | 项 | 门槛 |
| --- | --- | --- |
| F1 | `[SPIKE] bushVariant` | 两条路线各有一行，blob 那行 `quads=0`，card 那行 `quads=66`、`clip=0.5`、`cullOff=true` |
| F2 | 卡片材质日志 | `alphaClip=true`、`cull=0`、`specularOff=true`、`normalMap=false` |
| F3 | `bush_card.png` | 128×128 RGBA、不透明占比 **55–78%**、不透明区 G ∈ [62,80] |
| F4 | `foliage art_ground` 比 | **0.75–1.15**（blob 提亮后；两次校正值都写进报告） |
| F5 | `foliage card_art_ground` 比 | **0.75–1.15** |
| F6 | 四张 `clipped` | 全部 **≤ 0.05%** |
| F7 | `sharp art_ground middle`（blob） | 如实报（本轮不设硬门槛，上一轮 11.27） |
| F8 | `sharp card_art_ground middle` | **≥ 13.5**（硬像素边应该把高频抬上去，抬不上去说明卡片被 mip 模糊了） |
| F9 | `sharp art_runtime middle`（两套） | 变化 ≤ 12%（移轴守卫） |
| F10 | `frame` 两张主视角 | sky ±2、clipped ≤0.1%、heightPct ±1.5、pitch ±0.3、porch ±5 |
| F11 | 构建 ×2 | 双平台都 `Succeeded`、`error CS` 计 0 |
| F12 | 确定性 | 同参数连跑两次 `SpikeScene.Run`，`props`/`bushVariant`/`heightPct` 逐字相同 |
| F13 | 其它贴图 md5 | 草/石/瓦 6 个文件 `MD5_OK` |
| F14 | 目视（**必须真读图**） | 用 `--image=` 形式分别读 `art_ground.png` 与 `card_art_ground.png`，各自回答：① 数一下轮廓上明显的叶团凹口有几处；② 表面是「硬色块 + 像素毛边」还是「光滑明暗 + 鳞片」；③ 与画面里像素角色/像素草的语汇配不配；④ 一句话：像灌木丛还是像别的东西（像什么）。 |

## 报告格式

1. blob 提亮：起点比 0.46 → 校正 1 的值 → 实测比 → 校正 2（若用了）→ 最终比。
2. F1–F14 表格：每行 **实测值 / 过或不过**，不过就如实写。
3. 卡片实现要点：quad 尺寸、3 张的偏航与缩放、材质 alpha clip/cull 的实际值、alpha 覆盖率。
4. 你自己的一句话结论：`card_art_ground.png` 里的灌木读成什么，和 `art_ground.png` 比谁更像 HD-2D 的植被。
