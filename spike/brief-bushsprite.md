# 简报：blob 过曝回修 + Y 轴像素 Billboard 灌木（Codex 第 26 轮）

## 背景（只读这段，不要通读仓库）

上一轮做了 blob vs 像素卡片的 A/B，结论是**两条路都没成**，而且暴露了一个共同根因：

- **blob 提亮过头**：按 ×1.85 提反照率后，`art_ground` 亮度比到了 0.83（合格），但**主视角前景灌木炸了**：
  `foliage art_runtime` 灌木矩形 luma **74.1 → 123.7**、`clipped` **11.54%**，`frame.py` 全帧 clipped **1.10%**（上一轮 0.00%）。
  机制：真正让灌木变暗的是 `bumpScale 0.9` 把法线大面积掀离光源，**不该用反照率去补**；
  补上来以后，恰好正对太阳的小面片 NdotL≈1，直接打穿。
- **卡片路线**：轮廓的像素毛边是对的方向，但渲染出来是「三个圆顶竖棱的绿蘑菇/扇贝」——
  3 张平面各自带一条连续明暗渐变，加上贴图里的叶形被拉成竖条，破面感压过了叶团感。`card_art_ground` 亮度比 0.71、`sharp middle 10.71`。
- **共同根因**：两者都是**实时受光的连续明暗**。HD-2D 的植被之所以像植被，是因为像素精灵的**明暗是画进去的**（烘焙在像素里），
  alpha clip 给像素毛边，屏幕上 1 个 texel ≈ 1–2 个像素。本项目已经有这条管线：`Assets/Art/Shaders/PixelBillboard.shader`
  （Y 轴锁定 + alpha clip + 双面，角色正在用）。**灌木应该走同一条路。**

本轮：把 blob 修回不过曝（顺手做，5 分钟），然后**新增第三条路线：Y 轴锁定像素 Billboard**，与 blob 出两套图对比。

## 基线（2026-11-02 07:00，我复跑确认，与 Codex 自报一致）

```
foliage art_ground   bush luma 62.9 / grass 75.4 = 0.83   clipped 0.01%   sharp middle 13.81
foliage art_runtime  bush luma 123.7 / grass 81.2 = 1.52  clipped 11.54%  ← 炸了
foliage card_art_ground  bush luma 54.9 = 0.71  clipped 0.07%  sharp middle 10.71
frame art_runtime    sky(167,183,203) ground_near(78,91,48) max=255 std=53.8  clipped **1.10%**
frame card_art_runtime sky 相同  ground_near(66,80,35) max=217  clipped 0.12%
```

## 必读文件（只读这四个）

1. `spike/project/Assets/Editor/SpikeScene.cs` —— 灌木材质段（605–660 行附近）与 `void Bush(...)`
2. `spike/project/Assets/Editor/SpikeArt.cs` —— `MakeTextured()` / `ImportTex()` / `Blob()` / `Quad()`
3. `spike/project/Assets/Art/Shaders/PixelBillboard.shader` —— 已有 Y 轴锁定 + alpha clip，**允许加 ShadowCaster / DepthOnly pass**
4. `spike/tools/make_textures.py` —— `make_bush()` / `make_bushcard()` 与 `main()` 的写出逻辑

## 任务 A：blob 回修（别再动贴图）

1. `MakeTextured(...)` 的 `bumpScale` **0.9 → 0.5**（灌木那条，`SpikeScene.cs` 609–610 行）。
2. 把上一轮的 `bumpCompensation = 1.85f` 改成按实测推的**一次**校正：
   目标不是只让 `art_ground` 合格，而是**两张都要**：`art_ground` 比 ∈ **0.80–1.10** 且 `art_runtime` 灌木矩形 `clipped ≤ 0.05%`。
   起点建议 `1.85 × 0.75 = 1.39`（bump 降下来会自己亮一截），**最多调两次**，把每次的实测比值与 clipped 都写进报告。
3. 不许动贴图、几何、叶簇、`_SPECULARHIGHLIGHTS_OFF`。

## 任务 B：Y 轴像素 Billboard 灌木（`-bushSprite`）

### B1 贴图 `bush_sprite.png`（RGBA 112×80，`make_textures.py` 新增 `make_bushsprite()`）

这张图是**画进去的像素灌木**，不是噪声图。硬要求：

- **画布**：112 宽 × 80 高，**透明背景**（alpha 只有 0 / 255，禁止半透明、禁止抗锯齿）。
- **轮廓**：由 **7–9 个「叶团」** 叠成（不是一个大圆顶）：每个叶团是一个 18–30 px 的不规则团块，
  团块本身再由 **6–10 个 3–5 px 的叶形小块** 拼出（叶形的长轴角度**随机**，禁止统一竖直——
  竖条是上一轮卡片读成扇贝的直接原因）。团块之间允许留 1–2 px 的 V 形缺口，轮廓整体 112×72 内填充。
- **明暗烘焙进像素**（**这是本方案的核心，不要给它挂实时光照**）：光源在**左上**。
  每个叶团内部：左上 1/3 用最亮两档、右下 1/3 用最暗两档，中间两档过渡，**边界是硬直线 + 2–3 px 锯齿**；
  整丛底部 12–16 px 统一压到最暗一档（接地暗部）；丛顶最外 1–2 px 用亮档钩一条天光边。
- **调色板**（沿用，别换色系）：`(18,28,17) (30,42,24) (44,58,34) (60,76,44) (84,100,62)`；
  另外允许 1 档 `(104,120,74)` **只用于顶边钩光**，占不透明像素 < 6%。
- **禁止**：每片叶描暗边、平滑渐变、垂直条纹、把整丛画成圆顶。
- 同时输出 **8 个变体**：同一次绘制 + 8 个不同随机种子，写成 `bush_sprite_0..7.png`（或一张 8 列图集；
  二选一，但渲染端必须真的用上 8 个变体，不能 22 丛全长一样）。
- 报告里必须给：不透明像素占比（目标 45–62%）、不透明区 RGB 均值、`(104,120,74)` 的占比。

### B2 材质 `M_BushSprite.mat`

- Shader 用 `Mistbell/PixelBillboard`（**不要**用 URP/Lit）。
- `_MainTex` = 该变体贴图（导入：Point、无压缩、`alphaIsTransparency=true`、mipmap **关**（像素精灵不需要 mip，
  避免远处糊掉）、wrapMode Clamp）。
- `_Cutoff 0.5`、`_Cull 0`、`_PivotY 0`（脚底为原点）、`_ScaleX 1.35`、`_ScaleY 0.96`。
- `_Color` 是**唯一的亮度旋钮**（因为 unlit）：先给 `(1,1,1,1)` 跑一次，量 `foliage art_ground` 的比值，
  按 `目标/实测` 调 `_Color`，**最多两次**，目标 **0.85–1.05**；把每次实测写进报告。
  ⚠️ 不要用 `_Color` 把图整体压暗到看不出叶子（若需要 >±25% 的调整，说明贴图本身明度不对，回到 B1 改调色板）。

### B3 几何与阴影

- 每丛 **1 张 billboard quad**（顶点用 `SpikeArt.Quad("Quad_Billboard", 1, 1)` 之类，1×1 m、底边中点原点、
  UV 0..1）；位置 = 沿用现有 `Bush()` 的吸附落点，**落点抽样序列一点都不能动**（22 丛、`bushIndex` 顺序、`Snap()`）。
- 每丛从 8 个变体里按 `bushIndex` 取一个，`_ScaleX/_ScaleY` 在 `1.35×[0.9,1.1]` 之间微扰（横向），高度 `0.96×[0.92,1.08]`。
- **必须补 ShadowCaster + DepthOnly pass**（`PixelBillboard.shader` 现在只有 Forward pass → 灌木不会投影、
  也不会进 SSAO 的深度/法线）：两段照抄 URP 的 `ShadowCasterPass.hlsl` / `DepthOnlyPass.hlsl`，
  都要做同样的 Y 轴朝向变换与 `_Cutoff` alpha clip。日志里打一行：
  ```
  [SPIKE] bushSprite quads=22 variants=8 castShadow=true depthOnly=true tex=112x80 pivotY=0 color=(r,g,b)
  ```
- 默认路线仍是 blob；`-bushSprite` 开关切到精灵路线，`-bushCard` 那条**保留不动**（上轮已验过）。

## 任务 C：出两套图（blob 默认 + sprite），卡片不用重出

```bash
pkill -f "Unity.app/Contents/MacOS/Unity"; sleep 2
# 1) sprite
Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run -bushSprite -logFile spike/logs/196_scene_sprite.log
Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/197_build_sprite.log
caffeinate -dimu -t 240 bash spike/tools/capture_runtime.sh
for f in spike/project/Shots/art_*.png; do cp "$f" "spike/project/Shots/sprite_$(basename "$f")"; done
# 2) blob（默认，最后跑）
Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/198_scene_blob.log
Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/199_build_blob.log
caffeinate -dimu -t 240 bash spike/tools/capture_runtime.sh
```
（`Unity` = `/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity`，照抄上轮写法）

`foliage.py` 已经加好 `sprite_art_ground` / `sprite_art_runtime` 两个矩形键（我这边已改，你**不要**动它）。

## 不要动

栅栏、石块、落点/数量、22 丛、相机与 `ShotRig`、光照、Volume、SSAO/移轴、教堂 FBX、草/石/瓦 6 张贴图（md5 一致）、
`bush.png` / `bush_n.png` / `bush_card.png` 的内容（`bush_card.png` 与卡片代码保持上一轮原样）。

## 验收（自己跑，全绿或如实报告，不许改门槛）

| # | 项 | 门槛 |
| --- | --- | --- |
| F1 | `[SPIKE] bushSprite` | 一行，`quads=22`、`variants=8`、`castShadow=true`、`depthOnly=true`、`tex=112x80`、`pivotY=0` |
| F2 | `bush_sprite` 贴图 | 112×80 RGBA、alpha 只有 0/255、不透明占比 **45–62%**、不透明区 G ∈ [58,86] |
| F3 | `foliage art_ground`（blob） | 比 **0.80–1.10**、`clipped ≤ 0.05%` |
| F4 | `foliage art_runtime`（blob） | 比 **0.75–1.15**、**`clipped ≤ 0.05%`**（上一轮 11.54% 是这轮的硬指标） |
| F5 | `frame art_runtime`（blob） | 全帧 `clipped ≥250` **≤ 0.10%**（上一轮 1.10%） |
| F6 | `foliage sprite_art_ground` | 比 **0.80–1.10**、`clipped ≤ 0.05%` |
| F7 | `foliage sprite_art_runtime` | 比 **0.75–1.15**、`clipped ≤ 0.05%` |
| F8 | `sharp sprite_art_ground` middle | **≥ 11.0**（像素精灵应带来硬边高频；低于 11 说明被 mip/过滤糊了） |
| F9 | `sharp art_ground` middle（blob） | ≥ 12.0（上轮 13.81 有 clipping 加成，回修后会掉，不要低于 12） |
| F10 | `sharp art_runtime` middle（两套） | 变化 ≤ 12%（移轴守卫，基线 4.14） |
| F11 | `frame` 两套主视角 | sky ±2、heightPct ±1.5、pitch ±0.3、porch ±5 |
| F12 | 构建 ×2 | 双平台 `Succeeded`、`error CS` 0 |
| F13 | 确定性 | 同参数连跑两次 `SpikeScene.Run`，`props`/`bushVariant`/`bushSprite`/`heightPct` 逐字相同 |
| F14 | 其它贴图 md5 | 草/石/瓦 6 个 + `bush.png`/`bush_card.png` 前后一致 |
| F15 | 目视 | 用 `--image=` 分别读 `art_ground.png` 与 `sprite_art_ground.png`：① 灌木外轮廓数出几处叶团凹口；② 表面是像素色块还是连续明暗；③ 与右边像素草的笔触像不像同一个人画的；④ 一句话：像灌木丛 / 像什么。 |

## 报告格式

1. blob 回修：bump 值、每次校正的实际比与 clipped（ground / runtime 两列）。
2. F1–F15 表格：实测值 / 过或不过，不过如实写。
3. sprite 路线要点：贴图怎么画的（叶团数、叶子数、明暗分区）、`_Color` 的两次实测、阴影与深度 pass 是否真的接上。
4. 一句话：`sprite_art_ground.png` 读成什么，和 blob 比谁更像 HD-2D 植被。
